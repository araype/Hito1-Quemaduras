using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Como evoluciona la quemadura con el tiempo y con lo que hace el
    // jugador:
    //   - la mano se hincha de a poco: rapido sin agua, casi nada bajo el
    //     chorro. Si se hincha demasiado, el anillo ya no sale (PatientRing);
    //   - en segundo grado salen ampollas a los pocos segundos y crecen
    //     mientras la quemadura no se enfria; enfriar a tiempo las deja
    //     chicas;
    //   - el agua caliente vuelve a quemar: todo empeora mas rapido.
    // La hinchazon engrosa el hueso de la mano (el modelo no tiene
    // blendshapes); la capa de la quemadura y la piel la siguen porque usan
    // los mismos huesos. Va en el paciente (el objeto con PatientBody).
    [RequireComponent(typeof(PatientBody))]
    public class BurnInjury : MonoBehaviour
    {
        public enum BurnDegree
        {
            Primero,
            SegundoSuperficial
        }

        public BurnSequenceManager burnSequence;
        public BurnDegree degree = BurnDegree.SegundoSuperficial;
        [Tooltip("Elige el grado al azar en cada partida.")]
        public bool randomDegree;
        public bool rightHand = true;

        [Header("Hinchazon")]
        [Tooltip("Cuanto engrosa la mano hinchada del todo (0,12 = 12 %).")]
        public float maxSwelling = 0.12f;
        [Tooltip("Segundos sin agua hasta hincharse del todo.")]
        public float swellSecondsUntreated = 70f;
        [Tooltip("Cuanto mas lento se hincha bajo el agua o ya enfriada.")]
        public float treatedSlowdown = 8f;

        [Header("Ampollas (segundo grado)")]
        public int blisterCount = 6;
        [Tooltip("Segundos despues de la quemadura en que empiezan a verse.")]
        public float blisterOnsetSeconds = 6f;
        [Tooltip("Segundos sin agua hasta llegar a su tamano mayor.")]
        public float blisterGrowSeconds = 45f;
        public Vector2 blisterDiameter = new Vector2(0.005f, 0.013f);
        [Tooltip("Tamano (0 a 1) que alcanzan aunque se enfrie enseguida.")]
        [Range(0f, 1f)] public float minimumBlister = 0.3f;

        [Tooltip("El agua caliente quema este numero de veces mas rapido que el aire.")]
        public float hotWaterFactor = 3f;

        // 0 a 1.
        public float Swelling { get; private set; }

        // Segundos de quemadura sin tratar (el agua caliente cuenta doble o mas).
        public float UntreatedSeconds { get; private set; }

        // El agua caliente cayo sobre la quemadura en este frame o hace poco.
        public bool HotWaterNow
        {
            get { return Time.time - lastHotWater < 0.3f; }
        }

        public static BurnInjury Instance { get; private set; }

        private PatientBody body;
        private Transform hand;
        private Vector3 handBaseScale = Vector3.one;
        private readonly List<Blister> blisters = new List<Blister>();
        private float blisterAmount;
        private float lastHotWater = float.NegativeInfinity;

        private class Blister
        {
            public Transform transform;
            public float diameter;
            public float delay;
        }

        private void Awake()
        {
            body = GetComponent<PatientBody>();
            Instance = this;
            if (randomDegree)
            {
                degree = Random.value < 0.5f ? BurnDegree.Primero : BurnDegree.SegundoSuperficial;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            if (!body.IsReady)
            {
                enabled = false;
                yield break;
            }

            hand = body.HandBone(rightHand);
            handBaseScale = hand.localScale;
            Debug.Log("[BurnInjury] Quemadura de " + (degree == BurnDegree.Primero ? "primer grado" : "segundo grado superficial"));

            // La capa de la quemadura se arma en el Start de BurnSkinOverlay.
            yield return null;
            BurnSkinOverlay overlay = GetComponent<BurnSkinOverlay>();
            if (overlay != null && degree == BurnDegree.SegundoSuperficial)
            {
                // Mas roja e intensa que una de primer grado.
                overlay.burnedColor = new Color(1f, 0.42f, 0.38f, 0.95f);
            }

            if (degree == BurnDegree.SegundoSuperficial)
            {
                BuildBlisters(overlay != null ? overlay.Overlay : null);
            }
        }

        // Lo llama el detector del chorro mientras cae agua caliente sola
        // sobre la quemadura.
        public void OnHotWater()
        {
            lastHotWater = Time.time;
        }

        private void Update()
        {
            if (burnSequence == null || hand == null)
            {
                return;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            if (state == BurnSequenceManager.BurnState.Idle)
            {
                return;
            }

            float dt = Time.deltaTime;
            bool untreated = state == BurnSequenceManager.BurnState.Burned;
            float rate = HotWaterNow ? hotWaterFactor : (untreated ? 1f : 1f / Mathf.Max(1f, treatedSlowdown));
            UntreatedSeconds += dt * rate;

            float swellMax = degree == BurnDegree.Primero ? 0.55f : 1f;
            Swelling = Mathf.Min(swellMax, Swelling + dt * rate / Mathf.Max(1f, swellSecondsUntreated));

            // Mas gruesa (ancho y alto de la mano), no mas larga: el eje Y de
            // los huesos de Mixamo va a lo largo de la mano.
            float s = 1f + maxSwelling * Swelling;
            hand.localScale = new Vector3(handBaseScale.x * s, handBaseScale.y * (1f + maxSwelling * 0.25f * Swelling),
                handBaseScale.z * s);

            UpdateBlisters(dt);
        }

        private void UpdateBlisters(float dt)
        {
            if (blisters.Count == 0 || burnSequence.BurnTime < 0f)
            {
                return;
            }

            float sinceBurn = Time.time - burnSequence.BurnTime;
            float target = sinceBurn < blisterOnsetSeconds
                ? 0f
                : Mathf.Max(Mathf.Clamp01((sinceBurn - blisterOnsetSeconds) / 10f) * minimumBlister,
                    Mathf.Clamp01((UntreatedSeconds - blisterOnsetSeconds) / Mathf.Max(1f, blisterGrowSeconds)));
            blisterAmount = Mathf.MoveTowards(blisterAmount, target, dt / 6f);

            foreach (Blister b in blisters)
            {
                float k = Mathf.Clamp01(blisterAmount * 1.3f - b.delay);
                bool show = k > 0.02f;
                if (b.transform.gameObject.activeSelf != show)
                {
                    b.transform.gameObject.SetActive(show);
                }

                if (show)
                {
                    float d = b.diameter * Mathf.Sqrt(k);
                    SetWorldSize(b.transform, new Vector3(d, d * 0.45f, d));
                }
            }
        }

        // ---------------- ampollas ----------------

        // Elige puntos del dorso de la mano (la cara que mira hacia arriba al
        // quemarse, donde cayo el agua) y cuelga cada ampolla del hueso que
        // mas mueve ese punto, para que siga a la mano.
        private void BuildBlisters(SkinnedMeshRenderer overlay)
        {
            if (overlay == null || overlay.sharedMesh == null)
            {
                Debug.LogWarning("[BurnInjury] Sin capa de quemadura: no se arman las ampollas.", this);
                return;
            }

            Mesh mesh = overlay.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            Transform[] bones = overlay.bones;

            Matrix4x4[] skin = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                skin[i] = bones[i] != null ? bones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity;
            }

            List<int> candidates = new List<int>();
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 n = Skin(skin, weights[v], normals[v], true).normalized;
                if (n.y > 0.55f)
                {
                    candidates.Add(v);
                }
            }

            if (candidates.Count == 0)
            {
                for (int v = 0; v < vertices.Length; v++)
                {
                    candidates.Add(v);
                }
            }

            List<Vector3> used = new List<Vector3>();
            int tries = 0;
            while (blisters.Count < blisterCount && tries++ < 400)
            {
                int v = candidates[Random.Range(0, candidates.Count)];
                Vector3 p = Skin(skin, weights[v], vertices[v], false);
                bool tooClose = false;
                foreach (Vector3 q in used)
                {
                    if (Vector3.Distance(p, q) < 0.016f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                used.Add(p);
                Vector3 n = Skin(skin, weights[v], normals[v], true).normalized;
                Transform bone = bones[Dominant(weights[v])];
                if (bone == null)
                {
                    continue;
                }

                Renderer r = PropKit.Part(bone, PrimitiveType.Sphere, Vector3.zero, Vector3.one, "Ampolla",
                    new Color(1f, 0.93f, 0.8f, 0.7f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Transform t = r.transform;
                t.name = "Ampolla";
                t.position = p + n * 0.0008f;
                t.rotation = Quaternion.FromToRotation(Vector3.up, n);
                t.gameObject.SetActive(false);
                blisters.Add(new Blister
                {
                    transform = t,
                    diameter = Random.Range(blisterDiameter.x, blisterDiameter.y),
                    delay = blisters.Count * 0.08f
                });
            }

            Debug.Log($"[BurnInjury] {blisters.Count} ampollas preparadas");
        }

        private static Vector3 Skin(Matrix4x4[] skin, BoneWeight w, Vector3 v, bool direction)
        {
            Vector3 r = Vector3.zero;
            r += Apply(skin, w.boneIndex0, v, direction) * w.weight0;
            r += Apply(skin, w.boneIndex1, v, direction) * w.weight1;
            r += Apply(skin, w.boneIndex2, v, direction) * w.weight2;
            r += Apply(skin, w.boneIndex3, v, direction) * w.weight3;
            return r;
        }

        private static Vector3 Apply(Matrix4x4[] skin, int index, Vector3 v, bool direction)
        {
            if (index < 0 || index >= skin.Length)
            {
                return Vector3.zero;
            }

            return direction ? skin[index].MultiplyVector(v) : skin[index].MultiplyPoint3x4(v);
        }

        private static int Dominant(BoneWeight w)
        {
            int best = w.boneIndex0;
            float bestWeight = w.weight0;
            if (w.weight1 > bestWeight) { best = w.boneIndex1; bestWeight = w.weight1; }
            if (w.weight2 > bestWeight) { best = w.boneIndex2; bestWeight = w.weight2; }
            if (w.weight3 > bestWeight) { best = w.boneIndex3; }
            return best;
        }

        // Escala local para que el tamano en el mundo sea el pedido aunque el
        // hueso tenga escala (el FBX puede venir en cm, y la mano hinchada).
        private static void SetWorldSize(Transform t, Vector3 size)
        {
            Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
            float s = Mathf.Max(1e-5f, (Mathf.Abs(p.x) + Mathf.Abs(p.y) + Mathf.Abs(p.z)) / 3f);
            t.localScale = size / s;
        }
    }
}
