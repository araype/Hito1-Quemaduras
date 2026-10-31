using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Que hace la paciente con su mano quemada cuando el jugador no se la
    // sostiene (antes la mano quedaba flotando donde se la soltaba, o
    // estirada a la altura de la cara):
    //   - antes del accidente revuelve la olla con una cuchara de palo y mira
    //     lo que cocina;
    //   - al quemarse suelta la cuchara y protege la mano contra el pecho,
    //     sosteniendola con la otra mano;
    //   - si el jugador la suelta cerca del chorro (y el agua corre), ella
    //     misma la pone bajo el agua y la deja ahi;
    //   - si la suelta en otro lado, la deja un momento y vuelve a
    //     protegerla;
    //   - si se cierra el agua, o cuando ya termino de enfriar, la saca del
    //     agua y la vuelve a proteger (asi es facil cubrirla);
    //   - aparta la mano de lo que no quiere que le pongan (Avoid);
    //   - puede recibir un objeto que le alcanza el jugador (ReceiveObject).
    // Mueve el objetivo de la mano (kinematico) que usa PatientReach; si el
    // objetivo esta lejos, PatientReach la hace caminar.
    // Va en la paciente (el objeto con PatientBody y PatientReach).
    [RequireComponent(typeof(PatientBody))]
    [DefaultExecutionOrder(150)]
    public class PatientBurnArm : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public bool rightArm = true;

        [Header("Cocinar (antes del accidente)")]
        public bool cookBeforeAccident = true;
        public float stirRadius = 0.03f;
        public float stirSeconds = 2.4f;
        [Tooltip("Altura de la muneca sobre el borde de la olla al revolver (m).")]
        public float stirHeight = 0.09f;
        public bool spoon = true;

        [Header("Proteger la mano")]
        [Tooltip("Muneca respecto del pecho: adelante, al costado de la mano, abajo (m).")]
        public Vector3 guardOffset = new Vector3(0.2f, 0.05f, 0.2f);
        public bool cradleWithOtherHand = true;
        [Tooltip("Segundos que deja la mano donde la soltaron antes de volver a protegerla.")]
        public float holdAfterRelease = 2.5f;

        [Header("Agua")]
        [Tooltip("Si la sueltan a esta distancia horizontal del chorro, ella pone la mano bajo el agua.")]
        public float assistRadius = 0.32f;
        [Tooltip("Altura de la palma bajo la boca del grifo (m).")]
        public float palmBelowNozzle = 0.11f;
        [Tooltip("Segundos para girar la mano (dorso arriba) al ponerla bajo el chorro.")]
        public float palmTurnSeconds = 0.4f;
        [Tooltip("Segundos despues de enfriar hasta que saca la mano del agua.")]
        public float withdrawAfterCooled = 3.5f;

        [Header("Movimiento")]
        public float smoothTime = 0.22f;
        public float maxHandSpeed = 1.6f;

        public enum Mode
        {
            Cooking,
            Guard,
            Placed,
            InWater
        }

        public Mode Current { get; private set; } = Mode.Cooking;

        private PatientBody body;
        private PatientReach reach;
        private Rigidbody target;
        private PotFX pot;
        private WaterStreamFX stream;
        private Transform cradle;
        private Transform spoonRoot;
        private Vector3 spoonTip;
        private Vector3 velocity;
        private Vector3 placedAt;
        private float placedTime;
        private bool wasHeld;
        private float waterLostTime = -1f;
        private float cooledTime = -1f;
        private Vector3 avoidOffset;
        private float avoidUntil;
        private float lookAtPlayerUntil;
        private Transform viewer;
        private bool receiving;
        private float palmDown;

        private void Awake()
        {
            body = GetComponent<PatientBody>();
            reach = GetComponent<PatientReach>();
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            target = reach != null ? reach.target : null;
            pot = FindObjectOfType<PotFX>();
            stream = FindObjectOfType<WaterStreamFX>();
            viewer = VRPanelKit.Head();
            if (target == null || !body.IsReady)
            {
                enabled = false;
                return;
            }

            GameObject helper = new GameObject("ApoyoManoQuemada");
            helper.AddComponent<NoAutoCollider>();
            cradle = helper.transform;

            if (burnSequence != null)
            {
                burnSequence.OnStateChanged += HandleState;
            }

            bool idle = burnSequence == null || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle;
            Current = idle && cookBeforeAccident && pot != null ? Mode.Cooking : Mode.Guard;
            if (Current == Mode.Cooking && spoon)
            {
                BuildSpoon();
            }

            // Empieza ya en su lugar, sin cruzar la cocina.
            Vector3 start = Goal();
            target.position = start;
            target.transform.position = start;
            Application.onBeforeRender += UpdateSpoon;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= UpdateSpoon;
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged -= HandleState;
            }

            if (cradle != null)
            {
                Destroy(cradle.gameObject);
            }
        }

        // ---------------- API ----------------

        // Aparta la mano de algo que le acercan. Devuelve false si el jugador
        // se la tiene agarrada (entonces solo se sobresalta).
        public bool Avoid(Vector3 from)
        {
            if (target == null || SimpleGrab.IsHeldObject(target))
            {
                return false;
            }

            Vector3 away = Vector3.ProjectOnPlane(target.position - from, Vector3.up);
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -body.TorsoForward;
            }

            avoidOffset = away.normalized * 0.14f + Vector3.up * 0.04f;
            avoidUntil = Time.time + 1.4f;

            // Si la tenia bajo el agua, igual la aparta (y deja de enfriar).
            if (Current == Mode.InWater)
            {
                SetPlaced(target.position);
            }

            return true;
        }

        // Mira al jugador un rato (por ejemplo, al pedirle algo).
        public void LookAtPlayer(float seconds)
        {
            lookAtPlayerUntil = Time.time + seconds;
        }

        // Recibe un objeto con la mano libre y lo deja sobre el meson.
        public void ReceiveObject(Rigidbody rb, Vector3 putDown)
        {
            if (!receiving && rb != null)
            {
                StartCoroutine(Receive(rb, putDown));
            }
        }

        // ---------------- estados ----------------

        private void HandleState(BurnSequenceManager.BurnState state)
        {
            switch (state)
            {
                case BurnSequenceManager.BurnState.Burned:
                    if (Current == Mode.Cooking)
                    {
                        DropSpoon();
                        Current = Mode.Guard;
                        // Retira la mano de golpe.
                        velocity = (Goal() - target.position).normalized * 1.2f;
                    }

                    cooledTime = -1f;
                    break;

                case BurnSequenceManager.BurnState.Cooled:
                    cooledTime = Time.time;
                    break;
            }
        }

        private void SetPlaced(Vector3 at)
        {
            Current = Mode.Placed;
            placedAt = at;
            placedTime = Time.time;
        }

        private bool WaterRunning
        {
            get
            {
                if (stream == null || stream.tap == null || !stream.tap.Flowing)
                {
                    return false;
                }

                // Con el agua caliente sola no la pone (le quema).
                return stream.tap.Heat < 0.9f;
            }
        }

        private bool NearStream(Vector3 wrist)
        {
            if (stream == null)
            {
                return false;
            }

            // Palma que corresponde a esa muneca (la mano puede ir un poco
            // atrasada respecto del objetivo).
            Transform hand = body.HandBone(rightArm);
            Vector3 palm = wrist + (stream.PalmOf(hand) - hand.position);
            Vector3 nozzle = stream.NozzlePosition;
            Vector3 flat = PatientBody.Flat(palm - nozzle);
            float below = nozzle.y - palm.y;
            return flat.magnitude < assistRadius && below > -0.25f && below < 0.5f;
        }

        // La palma (donde esta ahora) queda bajo el chorro, a menos de
        // 'radius' en horizontal.
        private bool PalmUnderStream(float radius)
        {
            if (stream == null)
            {
                return false;
            }

            Vector3 palm = stream.PalmOf(body.HandBone(rightArm));
            Vector3 nozzle = stream.NozzlePosition;
            return PatientBody.Flat(palm - nozzle).magnitude < radius && palm.y < nozzle.y + 0.02f
                && palm.y > nozzle.y - 0.45f;
        }

        // Muneca tal que la palma quede bajo el chorro con la mano plana y el
        // dorso (donde esta la quemadura) hacia arriba: los dedos apuntan
        // desde el hombro hacia el grifo (ver PatientBody.LayHandFlat).
        private Vector3 WaterGoal()
        {
            Transform hand = body.HandBone(rightArm);
            float palmDistance = Vector3.Distance(stream.PalmOf(hand), hand.position);
            Vector3 nozzle = stream.NozzlePosition;
            Vector3 along = PatientBody.Flat(nozzle - body.ShoulderPosition(rightArm));
            along = along.sqrMagnitude > 1e-4f ? along.normalized : PatientBody.Flat(body.TorsoForward).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Vector3 fingers = Quaternion.AngleAxis(body.palmDownTiltDegrees, side) * along;
            Vector3 wantedPalm = nozzle + Vector3.down * palmBelowNozzle;
            return wantedPalm - fingers * palmDistance;
        }

        private Vector3 GuardGoal()
        {
            Vector3 up = Vector3.up;
            Vector3 fwd = PatientBody.Flat(body.TorsoForward).normalized;
            Vector3 side = PatientBody.Flat(transform.right).normalized * (rightArm ? 1f : -1f);
            return body.Chest.position + fwd * guardOffset.x + side * guardOffset.y - up * guardOffset.z;
        }

        private Vector3 CookGoal()
        {
            Vector3 toHer = PatientBody.Flat(transform.position - pot.BottomCenter);
            toHer = toHer.sqrMagnitude > 1e-4f ? toHer.normalized : -transform.forward;
            float a = Time.time / Mathf.Max(0.5f, stirSeconds) * Mathf.PI * 2f;
            Vector3 circle = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * stirRadius;
            Vector3 center = pot.BottomCenter + Vector3.up * (pot.Height + stirHeight) + toHer * (pot.Radius * 0.35f);
            spoonTip = pot.BottomCenter + Vector3.up * (pot.Height * 0.35f) + circle * 2.2f;
            return center + circle;
        }

        private Vector3 Goal()
        {
            Vector3 goal;
            switch (Current)
            {
                case Mode.Cooking:
                    goal = CookGoal();
                    break;
                case Mode.Placed:
                    goal = placedAt;
                    break;
                case Mode.InWater:
                    goal = WaterGoal();
                    break;
                default:
                    goal = GuardGoal();
                    break;
            }

            if (Time.time < avoidUntil)
            {
                goal += avoidOffset * Mathf.Clamp01((avoidUntil - Time.time) / 0.5f);
            }

            return goal;
        }

        // ---------------- cada cuadro ----------------

        private void FixedUpdate()
        {
            if (target == null)
            {
                return;
            }

            bool held = SimpleGrab.IsHeldObject(target);
            if (held)
            {
                wasHeld = true;
                velocity = Vector3.zero;
                if (Current != Mode.Cooking)
                {
                    Current = Mode.Guard;
                }

                return;
            }

            if (wasHeld)
            {
                // Lo acaban de soltar: si quedo cerca del chorro, ella pone la
                // mano bajo el agua; si no, la deja un momento ahi.
                wasHeld = false;
                if (WaterRunning && NearStream(target.position))
                {
                    Current = Mode.InWater;
                    waterLostTime = -1f;
                }
                else
                {
                    SetPlaced(target.position);
                }
            }

            UpdateMode();

            if (!target.isKinematic)
            {
                return;
            }

            Vector3 goal = Goal();
            Vector3 next = Vector3.SmoothDamp(target.position, goal, ref velocity, smoothTime, maxHandSpeed,
                Time.fixedDeltaTime);
            target.MovePosition(next);
        }

        private void UpdateMode()
        {
            BurnSequenceManager.BurnState state = burnSequence != null
                ? burnSequence.CurrentState
                : BurnSequenceManager.BurnState.Burned;

            switch (Current)
            {
                case Mode.Placed:
                    // Soltada justo bajo el agua (o el agua se abrio despues):
                    // la deja ahi.
                    if (state == BurnSequenceManager.BurnState.Cooling && WaterRunning)
                    {
                        Current = Mode.InWater;
                        waterLostTime = -1f;
                    }
                    else if (Time.time - placedTime > holdAfterRelease)
                    {
                        Current = Mode.Guard;
                    }

                    break;

                case Mode.InWater:
                    if (!WaterRunning)
                    {
                        if (waterLostTime < 0f)
                        {
                            waterLostTime = Time.time;
                        }
                        else if (Time.time - waterLostTime > 1.5f)
                        {
                            Current = Mode.Guard;
                        }
                    }
                    else
                    {
                        waterLostTime = -1f;
                    }

                    if (cooledTime >= 0f && Time.time - cooledTime > withdrawAfterCooled)
                    {
                        Current = Mode.Guard;
                    }

                    break;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            UpdateLeftHand();
            UpdateHead();
            UpdatePalm();
        }

        // Bajo el chorro (puesta por ella o sostenida ahi por el jugador) la
        // mano queda plana con el dorso hacia arriba, para que se vea que el
        // agua cae sobre la quemadura.
        private void UpdatePalm()
        {
            bool held = SimpleGrab.IsHeldObject(target);
            bool underWater = Current == Mode.InWater
                || (held && Current != Mode.Cooking && WaterRunning && PalmUnderStream(0.1f));
            float speed = Time.deltaTime / Mathf.Max(0.05f, palmTurnSeconds);
            palmDown = Mathf.MoveTowards(palmDown, underWater ? 1f : 0f, speed);
            if (rightArm)
            {
                body.rightPalmDown = palmDown;
            }
            else
            {
                body.leftPalmDown = palmDown;
            }
        }

        // La otra mano sostiene la quemada por debajo de la muneca, salvo
        // mientras cocina, mientras el jugador la lleva o mientras esta bajo
        // el agua (ahi se apoya en el meson).
        private void UpdateLeftHand()
        {
            bool held = SimpleGrab.IsHeldObject(target);
            bool cradling = cradleWithOtherHand && Current == Mode.Guard && !held && !receiving;
            Transform hand = body.HandBone(rightArm);
            Transform fore = body.ForeArm(rightArm);
            if (cradling && hand != null && fore != null)
            {
                Vector3 along = (hand.position - fore.position).normalized;
                cradle.position = body.WristPosition(rightArm) - along * 0.07f + Vector3.down * 0.035f;
                SetOtherArm(PatientBody.ArmMode.Reach, cradle);
            }
            else if (!receiving)
            {
                SetOtherArm(PatientBody.ArmMode.Rest, null);
            }
        }

        private void SetOtherArm(PatientBody.ArmMode mode, Transform t)
        {
            if (rightArm)
            {
                body.leftMode = mode;
                body.leftTarget = t;
            }
            else
            {
                body.rightMode = mode;
                body.rightTarget = t;
            }
        }

        // Mientras cocina mira la olla (salvo que le hable al jugador).
        private void UpdateHead()
        {
            if (Current != Mode.Cooking || pot == null)
            {
                return;
            }

            Vector3 look = Time.time < lookAtPlayerUntil && viewer != null ? viewer.position : pot.BottomCenter;
            Vector3 toLook = look - body.Head.position;
            Vector3 flat = Vector3.ProjectOnPlane(toLook, Vector3.up);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(PatientBody.Flat(transform.forward), flat, Vector3.up), -60f, 60f);
            float pitch = Mathf.Clamp(Mathf.Atan2(-toLook.y, Mathf.Max(0.05f, flat.magnitude)) * Mathf.Rad2Deg, -15f, 40f);
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            body.headYaw = Mathf.Lerp(body.headYaw, yaw, k);
            body.headPitch = Mathf.Lerp(body.headPitch, pitch * 0.8f, k);
        }

        // ---------------- recibir un objeto ----------------

        private IEnumerator Receive(Rigidbody rb, Vector3 putDown)
        {
            receiving = true;
            Transform hand = body.HandBone(!rightArm);
            GameObject point = new GameObject("PuntoRecibir");
            point.AddComponent<NoAutoCollider>();

            // Estira la mano libre hacia el objeto.
            float t = 0f;
            SetOtherArm(PatientBody.ArmMode.Reach, point.transform);
            while (t < 0.6f && rb != null)
            {
                t += Time.deltaTime;
                point.transform.position = rb.worldCenterOfMass;
                yield return null;
            }

            if (rb == null)
            {
                receiving = false;
                Destroy(point);
                yield break;
            }

            // Lo toma.
            SimpleGrab.ForceRelease(rb);
            bool wasKinematic = rb.isKinematic;
            RigidbodyInterpolation interpolation = rb.interpolation;
            rb.isKinematic = true;
            // Sin interpolacion: si no, el Rigidbody pisa la posicion que se
            // le da al Transform.
            rb.interpolation = RigidbodyInterpolation.None;
            Collider[] cols = rb.GetComponentsInChildren<Collider>();
            foreach (Collider c in cols)
            {
                c.enabled = false;
            }

            Vector3 offset = Quaternion.Inverse(hand.rotation) * (rb.position - hand.position);
            Quaternion rotOffset = Quaternion.Inverse(hand.rotation) * rb.rotation;

            // Lo lleva hasta el meson.
            Vector3 from = point.transform.position;
            t = 0f;
            while (t < 1.1f && rb != null)
            {
                t += Time.deltaTime;
                point.transform.position = Vector3.Lerp(from, putDown + Vector3.up * 0.08f, Mathf.SmoothStep(0f, 1f, t / 1.1f));
                rb.transform.SetPositionAndRotation(hand.position + hand.rotation * offset,
                    Quaternion.Slerp(hand.rotation * rotOffset, Quaternion.identity, Mathf.Clamp01(t / 1.1f)));
                yield return null;
            }

            if (rb != null)
            {
                rb.transform.SetPositionAndRotation(putDown + Vector3.up * 0.003f, Quaternion.identity);
                rb.position = rb.transform.position;
                rb.rotation = rb.transform.rotation;
                rb.interpolation = interpolation;
                foreach (Collider c in cols)
                {
                    c.enabled = true;
                }

                rb.isKinematic = wasKinematic;
                if (!rb.isKinematic)
                {
                    rb.velocity = Vector3.zero;
                }

                Grabbable g = rb.GetComponent<Grabbable>();
                if (g != null)
                {
                    g.SetHome();
                }
            }

            Destroy(point);
            SetOtherArm(PatientBody.ArmMode.Rest, null);
            receiving = false;
        }

        // ---------------- cuchara ----------------

        private void BuildSpoon()
        {
            GameObject root = new GameObject("CucharaDePalo");
            root.AddComponent<NoAutoCollider>();
            spoonRoot = root.transform;
            Color wood = new Color(0.62f, 0.43f, 0.24f);
            // Mango a lo largo del eje Z local (de la mano hacia la olla).
            PropKit.Part(spoonRoot, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.13f), new Vector3(0.012f, 0.13f, 0.012f),
                Quaternion.Euler(90f, 0f, 0f), "Madera", wood);
            PropKit.Part(spoonRoot, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.27f), new Vector3(0.04f, 0.012f, 0.055f),
                Quaternion.identity, "Madera", wood);
        }

        private void UpdateSpoon()
        {
            if (spoonRoot == null || Current != Mode.Cooking || pot == null)
            {
                return;
            }

            Transform hand = body.HandBone(rightArm);
            if (hand == null)
            {
                return;
            }

            Vector3 grip = stream != null ? stream.PalmOf(hand) : hand.position;
            Vector3 dir = spoonTip - grip;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            spoonRoot.SetPositionAndRotation(grip - dir.normalized * 0.02f, Quaternion.LookRotation(dir, transform.forward));
        }

        private void DropSpoon()
        {
            if (spoonRoot == null)
            {
                return;
            }

            CapsuleCollider col = spoonRoot.gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.center = new Vector3(0f, 0f, 0.14f);
            col.radius = 0.015f;
            col.height = 0.3f;
            Rigidbody rb = spoonRoot.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.velocity = (-transform.forward + Vector3.up * 0.6f) * 1.2f;
            rb.angularVelocity = Random.insideUnitSphere * 6f;
            spoonRoot.gameObject.AddComponent<Grabbable>();
            spoonRoot = null;
        }
    }
}
