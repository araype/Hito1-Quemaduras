using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Cuerpo fisico de la paciente (antes no tenia ningun collider: las manos
    // y los objetos la atravesaban como a un fantasma).
    //   - Capsulas en el torso, la cabeza, los brazos y las piernas que siguen
    //     a los huesos. El jugador no la atraviesa al caminar y los objetos
    //     (la olla, la chompa, los remedios) chocan con ella.
    //   - El brazo quemado se agarra de cualquier parte: brazo, antebrazo,
    //     muneca o mano (todas llevan al objetivo de la mano, GrabRedirect).
    //     Asi es facil encontrarlo y llevarla al agua.
    //   - Si se la agarra de la mano quemada (y no del antebrazo), le duele:
    //     se avisa BurnedHandGrabbed (error) y ella reacciona.
    //   - Al acercar una mano libre al brazo, un brillo suave marca de donde
    //     se puede tomar (verde: antebrazo; naranja: la mano quemada).
    //   - Tocarla vibra un poco, para que se sienta que hay alguien.
    // Va en la paciente (el objeto con PatientBody).
    [RequireComponent(typeof(PatientBody))]
    public class PatientPhysics : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public bool rightArmIsBurned = true;

        [Header("Agarre")]
        [Tooltip("Distancia a la que aparece el brillo de agarre.")]
        public float hoverDistance = 0.13f;
        public bool showGrabHint = true;
        public Material hintMaterial;
        public Color forearmHintColor = new Color(0.35f, 0.95f, 0.5f, 0.45f);
        public Color burnHintColor = new Color(1f, 0.55f, 0.2f, 0.45f);
        [Tooltip("Segundos minimos entre dos avisos de mano quemada agarrada.")]
        public float burnGrabCooldown = 8f;

        [Header("Tacto")]
        public bool touchHaptics = true;

        private PatientBody body;
        private PatientReach reach;
        private Rigidbody target;
        private readonly List<Collider> colliders = new List<Collider>();
        private readonly List<Collider> armColliders = new List<Collider>();
        private Collider burnedHandCollider;
        private Renderer hint;
        private int layerMask;
        private float lastBurnGrab = float.NegativeInfinity;
        private readonly bool[] touching = new bool[2];
        private readonly float[] nextTouchBuzz = new float[2];
        private BurnHaptics haptics;
        private readonly Collider[] overlap = new Collider[8];

        public IList<Collider> BodyColliders
        {
            get { return colliders; }
        }

        private void Awake()
        {
            body = GetComponent<PatientBody>();
            reach = GetComponent<PatientReach>();
        }

        private IEnumerator Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            target = reach != null ? reach.target : null;
            haptics = FindObjectOfType<BurnHaptics>();
            if (!body.IsReady)
            {
                enabled = false;
                yield break;
            }

            Build();
            SimpleGrab.OnGrabbed += HandleGrab;

            // La capsula con que camina (PatientReach) se crea en su Start:
            // se espera un cuadro para que sus propios colliders no la frenen.
            yield return null;
            IgnoreOwn();
        }

        private void OnDestroy()
        {
            SimpleGrab.OnGrabbed -= HandleGrab;
        }

        // ---------------- colliders ----------------

        private void Build()
        {
            int layer = LayerMask.NameToLayer("Paciente");
            if (layer < 0)
            {
                layer = gameObject.layer;
            }

            layerMask = 1 << layer;

            // Un solo Rigidbody kinematico en la raiz: las capsulas de los
            // huesos forman un cuerpo compuesto que se mueve con la animacion.
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;
            rb.useGravity = false;

            Transform hips = body.Hips, spine = body.Spine, chest = body.Chest, neck = body.Neck, head = body.Head;
            Capsule("Cadera", hips, spine, 0.12f, layer, 0.04f);
            Capsule("Torso", spine, neck, 0.115f, layer, 0f);
            Capsule("Cuello", neck, head, 0.05f, layer, 0f);
            Sphere("Cabeza", head, head.position + body.TorsoUp * 0.09f, 0.1f, layer);

            foreach (bool right in new[] { true, false })
            {
                bool burned = right == rightArmIsBurned;
                Transform upper = body.UpperArm(right), fore = body.ForeArm(right), hand = body.HandBone(right);
                Collider a = Capsule(right ? "Brazo_D" : "Brazo_I", upper, fore, 0.045f, layer, 0f);
                Collider b = Capsule(right ? "Antebrazo_D" : "Antebrazo_I", fore, hand, 0.038f, layer, 0f);
                Transform knuckle = FindChild(hand, "Middle1");
                Collider c = knuckle != null
                    ? Capsule(right ? "Mano_D" : "Mano_I", hand, knuckle, 0.032f, layer, 0.035f)
                    : null;
                if (burned)
                {
                    armColliders.Add(a);
                    armColliders.Add(b);
                    if (c != null)
                    {
                        armColliders.Add(c);
                        burnedHandCollider = c;
                    }
                }
            }

            foreach (string side in new[] { "Right", "Left" })
            {
                Transform upLeg = FindBone(side + "UpLeg"), leg = FindBone(side + "Leg"), foot = FindBone(side + "Foot");
                if (upLeg != null && leg != null)
                {
                    Capsule("Muslo_" + side, upLeg, leg, 0.07f, layer, 0f);
                }

                if (leg != null && foot != null)
                {
                    Capsule("Pierna_" + side, leg, foot, 0.05f, layer, 0f);
                }
            }

            // Agarrar cualquier parte del brazo quemado = agarrar su mano.
            if (target != null)
            {
                foreach (Collider col in armColliders)
                {
                    col.gameObject.AddComponent<GrabRedirect>().target = target;
                }

                // La esfera de la muneca era el unico punto de agarre (6 cm,
                // invisible). Ahora es chica: el brazo entero se puede tomar.
                SphereCollider sphere = target.GetComponent<SphereCollider>();
                if (sphere != null)
                {
                    float scale = Mathf.Max(1e-4f, target.transform.lossyScale.x);
                    sphere.radius = Mathf.Min(sphere.radius, 0.03f / scale);
                }

                Grabbable grabbable = target.GetComponent<Grabbable>();
                if (grabbable != null)
                {
                    grabbable.breakDistance = 0.9f;
                }
            }

            if (showGrabHint)
            {
                Material material = hintMaterial;
                if (material == null)
                {
                    WaterTap tap = FindObjectOfType<WaterTap>();
                    material = tap != null ? tap.indicatorMaterial : null;
                }

                hint = IndicatorUtil.CreateSphere("BrilloAgarre", null, transform.position, 0.035f, material,
                    forearmHintColor);
                hint.enabled = false;
            }

            Debug.Log($"[PatientPhysics] {colliders.Count} colliders en el cuerpo; el brazo quemado se agarra en {armColliders.Count} partes");
        }

        private Collider Capsule(string partName, Transform from, Transform to, float radius, int layer, float extend)
        {
            if (from == null || to == null)
            {
                return null;
            }

            Vector3 a = from.position;
            Vector3 b = to.position;
            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length < 1e-3f)
            {
                return null;
            }

            dir /= length;
            b += dir * extend;
            length += extend;

            GameObject go = Part(partName, from, (a + b) * 0.5f, Quaternion.LookRotation(dir), layer);
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = radius;
            capsule.height = length + radius * 2f;
            colliders.Add(capsule);
            return capsule;
        }

        private Collider Sphere(string partName, Transform bone, Vector3 center, float radius, int layer)
        {
            GameObject go = Part(partName, bone, center, Quaternion.identity, layer);
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = radius;
            colliders.Add(sphere);
            return sphere;
        }

        // Hijo del hueso con escala 1 en el mundo, para medir en metros.
        private static GameObject Part(string partName, Transform bone, Vector3 position, Quaternion rotation, int layer)
        {
            GameObject go = new GameObject("Fisica_" + partName);
            go.layer = layer;
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(bone, false);
            go.transform.SetPositionAndRotation(position, rotation);
            Vector3 s = bone.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(1e-5f, Mathf.Abs(s.x)),
                1f / Mathf.Max(1e-5f, Mathf.Abs(s.y)), 1f / Mathf.Max(1e-5f, Mathf.Abs(s.z)));
            return go;
        }

        private Transform FindBone(string suffix)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == body.bonePrefix + suffix)
                {
                    return t;
                }
            }

            return null;
        }

        private static Transform FindChild(Transform hand, string contains)
        {
            if (hand == null)
            {
                return null;
            }

            foreach (Transform child in hand)
            {
                if (child.name.Contains(contains))
                {
                    return child;
                }
            }

            return null;
        }

        // Ni la capsula con que camina ni el objetivo de su mano deben chocar
        // con su propio cuerpo.
        private void IgnoreOwn()
        {
            List<Collider> own = new List<Collider>();
            if (reach != null && reach.Mover != null)
            {
                own.Add(reach.Mover);
            }

            if (target != null)
            {
                own.AddRange(target.GetComponentsInChildren<Collider>(true));
            }

            foreach (Collider o in own)
            {
                foreach (Collider c in colliders)
                {
                    if (o != null && c != null)
                    {
                        Physics.IgnoreCollision(o, c, true);
                    }
                }
            }
        }

        // ---------------- interaccion ----------------

        private bool Injured
        {
            get
            {
                if (burnSequence == null)
                {
                    return false;
                }

                BurnSequenceManager.BurnState s = burnSequence.CurrentState;
                return s == BurnSequenceManager.BurnState.Burned || s == BurnSequenceManager.BurnState.Cooling
                    || s == BurnSequenceManager.BurnState.Cooled;
            }
        }

        private void HandleGrab(Collider col, Rigidbody rb, bool leftHand)
        {
            if (col == null || col != burnedHandCollider || !Injured)
            {
                return;
            }

            if (Time.time - lastBurnGrab < burnGrabCooldown)
            {
                return;
            }

            lastBurnGrab = Time.time;
            burnSequence.Report(BurnSequenceManager.BurnEvent.BurnedHandGrabbed, null,
                BurnSequenceManager.Severity.Error);
        }

        private void Update()
        {
            if (colliders.Count == 0)
            {
                return;
            }

            bool grabbable = target != null && burnSequence != null
                && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle;
            Vector3 bestPoint = Vector3.zero;
            float bestDistance = float.MaxValue;
            Collider bestCollider = null;

            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                if (!SimpleGrab.TryGetHand(left, out Vector3 hand, out bool holding))
                {
                    touching[h] = false;
                    continue;
                }

                UpdateTouch(h, hand, holding);

                if (!grabbable || holding)
                {
                    continue;
                }

                foreach (Collider col in armColliders)
                {
                    if (col == null)
                    {
                        continue;
                    }

                    Vector3 p = col.ClosestPoint(hand);
                    float d = Vector3.Distance(p, hand);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        bestPoint = p;
                        bestCollider = col;
                    }
                }
            }

            if (hint == null)
            {
                return;
            }

            bool show = bestCollider != null && bestDistance < hoverDistance && !SimpleGrab.IsHeldObject(target);
            hint.enabled = show;
            if (show)
            {
                hint.transform.position = bestPoint;
                bool onBurn = bestCollider == burnedHandCollider && Injured;
                Color color = onBurn ? burnHintColor : forearmHintColor;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
                color.a *= pulse * Mathf.Clamp01((hoverDistance - bestDistance) / 0.04f);
                IndicatorUtil.SetColor(hint, color);
            }
        }

        // Una mano libre que entra en el cuerpo de la paciente vibra un poco.
        private void UpdateTouch(int h, Vector3 hand, bool holding)
        {
            bool inside = !holding && Physics.OverlapSphereNonAlloc(hand, 0.03f, overlap, layerMask,
                QueryTriggerInteraction.Ignore) > 0;
            if (inside && !touching[h] && touchHaptics && Time.time >= nextTouchBuzz[h])
            {
                nextTouchBuzz[h] = Time.time + 0.4f;
                if (haptics == null)
                {
                    haptics = FindObjectOfType<BurnHaptics>();
                }

                if (haptics != null)
                {
                    haptics.Buzz(h, 0.25f, 0.05f, 0f, 0.6f);
                }
            }

            touching[h] = inside;
        }
    }
}
