using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Material para cubrir la quemadura despues de enfriarla:
    //   - Gasa esteril (la opcion principal: aposito esteril que no suelta
    //     pelusa, Cruz Roja Britanica). Sale de su sobre (GauzePacket). Se
    //     toma de los bordes (el centro va sobre la herida: tomarla del
    //     centro es un error leve), se apoya plana y centrada sobre la
    //     quemadura, sin apretar, y se mantiene 1 s (un circulo se llena,
    //     HoldHint). Si se aprieta contra la mano, ella se queja y no avanza.
    //     Al completarse se asienta sobre el dorso de la mano (medio segundo)
    //     y queda una capa de gasa donde se apoyo: si quedo corrida y no
    //     cubre al menos el 85 % de la quemadura, es un error. Mientras se
    //     acerca, la paciente ofrece la mano. Despues se puede fijar con la
    //     venda (BandageItem).
    //   - Bolsa plastica limpia (para una mano, NHS): se suelta sobre la mano
    //     quemada y la cubre holgada hasta la muneca.
    // Las dos pasan a Covered. Si se intenta antes de terminar de enfriar, no
    // se pone: es un error y la paciente se queja. La gasa y la bolsa se usan
    // (desaparecen). La gasa tambien se pone si se suelta sobre la mano.
    // El film no usa este componente: se cubre con tiras (FilmRoll y
    // FilmStrip). Kind.Film queda sin uso por las escenas que guardan el
    // numero.
    [RequireComponent(typeof(Rigidbody))]
    public partial class CoverItem : MonoBehaviour
    {
        public enum Kind
        {
            Film,
            Gasa,
            Bolsa
        }

        public Kind kind;
        public BurnSequenceManager burnSequence;
        [Tooltip("Si se suelta a menos de esta distancia de la mano quemada, la cubre.")]
        public float attachDistance = 0.15f;

        [Header("Gasa")]
        [Tooltip("Distancia de la gasa al centro de la quemadura para que cuente como apoyada encima (m).")]
        public float placeDistance = 0.1f;
        [Tooltip("Segundos que hay que mantener la gasa encima de la quemadura.")]
        public float holdSeconds = 1f;
        [Tooltip("A esta distancia de la quemadura, con la gasa en la mano, ella ofrece la mano (m).")]
        public float offerDistance = 0.4f;
        [Tooltip("Corrimiento maximo del centro de la gasa respecto al de la quemadura, sobre la piel (m).")]
        public float centerTolerance = 0.045f;
        [Tooltip("Grados que puede estar inclinada respecto al dorso de la mano.")]
        public float flatTolerance = 50f;
        [Tooltip("Cuanto puede hundirse la mano del jugador bajo la piel antes de contar como apretar (m).")]
        public float pressDepth = 0.012f;
        [Tooltip("Medio lado de la gasa (m).")]
        public float gauzeHalfSize = 0.05f;
        [Tooltip("Medio lado del centro de la gasa que no se debe tocar (m).")]
        public float sterileHalfSize = 0.028f;
        [Range(0f, 1f)] public float coverageNeeded = 0.85f;

        // La gasa puesta en la mano (para la venda): centro sobre la piel en
        // el espacio del hueso de la mano y si ya esta puesta.
        public static bool GauzeOnHand { get; private set; }
        public static Vector3 GauzeCenterLocal { get; private set; }

        private Grabbable grabbable;
        private Rigidbody body;
        private Collider[] colliders;
        private bool wasHeld;
        private bool used;
        private PatientBody patient;
        private PatientBurnArm arm;
        private BurnSkinOverlay burnOverlay;
        private HoldHint hint;
        private float progress;
        private float nextEarlyReport;
        private float pressedTime;
        private float nextPressReport;
        private bool centerReported;

        private void Start()
        {
            grabbable = GetComponent<Grabbable>();
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            if (kind == Kind.Gasa)
            {
                hint = new HoldHint("IndicacionGasa", 0.05f);
                SimpleGrab.OnGrabbed += HandleGrab;
            }
        }

        // Tomarla del centro toca el lado que va sobre la herida.
        private void HandleGrab(Collider touched, Rigidbody rb, bool left)
        {
            if (rb != body || used || centerReported || burnSequence == null
                || !SimpleGrab.TryGetHand(left, out Vector3 point, out bool _))
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(point);
            if (Mathf.Abs(local.x) < sterileHalfSize && Mathf.Abs(local.z) < sterileHalfSize)
            {
                centerReported = true;
                burnSequence.Report(BurnSequenceManager.BurnEvent.GauzeTouchedCenter, null,
                    BurnSequenceManager.Severity.Error);
            }
        }

        private void OnDestroy()
        {
            SimpleGrab.OnGrabbed -= HandleGrab;
            if (hint != null)
            {
                hint.Destroy();
            }
        }

        private void Update()
        {
            if (used)
            {
                return;
            }

            bool held = grabbable != null && grabbable.IsHeld;
            if (wasHeld && !held)
            {
                Released();
            }

            wasHeld = held;
            if (!used && kind == Kind.Gasa)
            {
                UpdateGauze(held);
            }
        }

        // La gasa se apoya encima de la quemadura y se mantiene 1 s.
        private void UpdateGauze(bool held)
        {
            if (burnSequence == null || !Patient(out Transform hand))
            {
                return;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            Vector3 burn = BurnCenter(hand);
            float distance = Vector3.Distance(GauzeCenter, burn);
            bool early = state == BurnSequenceManager.BurnState.Burned || state == BurnSequenceManager.BurnState.Cooling;
            bool ready = state == BurnSequenceManager.BurnState.Cooled;

            if (held && early && distance < placeDistance)
            {
                // Todavia no: primero hay que terminar de enfriar.
                if (Time.time >= nextEarlyReport)
                {
                    nextEarlyReport = Time.time + 8f;
                    burnSequence.Report(BurnSequenceManager.BurnEvent.CoveredTooEarly, kind.ToString(),
                        BurnSequenceManager.Severity.Error);
                }

                progress = 0f;
                hint.Hide();
                return;
            }

            if (held && ready && distance < offerDistance)
            {
                if (arm != null && arm.Current != PatientBurnArm.Mode.InWater)
                {
                    arm.ShowHand(1.5f);
                }

                Placement(hand, out float offset, out float tilt, out float depth);
                bool centered = offset < centerTolerance;
                bool flat = tilt < flatTolerance;
                bool pressing = centered && depth < -pressDepth;
                bool resting = centered && flat && !pressing && depth < 0.04f;

                // Apretar: no avanza y, si sigue, ella se queja.
                pressedTime = pressing ? pressedTime + Time.deltaTime : 0f;
                if (pressedTime > 0.35f && Time.time >= nextPressReport)
                {
                    nextPressReport = Time.time + 8f;
                    burnSequence.Report(BurnSequenceManager.BurnEvent.GauzePressed, null,
                        BurnSequenceManager.Severity.Error);
                }

                progress = resting ? progress + Time.deltaTime / Mathf.Max(0.1f, holdSeconds)
                    : Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2f);
                string words = pressing ? "Sin apretar:\nsolo apóyala"
                    : distance < placeDistance && !centered ? "Céntrala sobre\nla quemadura"
                    : distance < placeDistance && !flat ? "Ponla plana\nsobre la quemadura"
                    : "Mantén encima\nde la quemadura";
                hint.Show(burn, words, progress);
                // Vibra suave en la mano que la sostiene y crece al llenarse;
                // apretando, un golpe seco.
                if (BurnHaptics.Current != null && (resting || pressing))
                {
                    int h = SimpleGrab.IsHeldBy(body, true) ? 0 : 1;
                    BurnHaptics.Current.Buzz(h, pressing ? 0.6f : 0.1f + 0.3f * progress, 0.05f, 0f,
                        pressing ? 1f : 0.5f);
                }

                if (progress >= 1f)
                {
                    SimpleGrab.ForceRelease(body);
                    Cover(hand);
                }

                return;
            }

            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2f);
            hint.Hide();
        }

        private void Released()
        {
            if (burnSequence == null || !Patient(out Transform hand) || Distance(hand.position) > attachDistance)
            {
                return;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            if (state == BurnSequenceManager.BurnState.Burned || state == BurnSequenceManager.BurnState.Cooling)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.CoveredTooEarly, kind.ToString(),
                    BurnSequenceManager.Severity.Error);
                return;
            }

            if (state == BurnSequenceManager.BurnState.Cooled)
            {
                Cover(hand);
            }
        }

        private void Cover(Transform hand)
        {
            used = true;
            if (hint != null)
            {
                hint.Hide();
            }

            // La gasa se asienta sobre la mano antes de quedar puesta.
            if (kind == Kind.Gasa && patient != null)
            {
                StartCoroutine(SettleGauze(hand));
                return;
            }

            // Capa sobre la piel de la mano, un poco mas separada que la
            // quemadura y la mano mojada. La bolsa llega hasta la muneca y
            // queda holgada.
            SkinnedMeshRenderer layer = kind == Kind.Bolsa
                ? HandSkinMesh.Build(patient, true, 0.25f, 0.01f, "Body", "BolsaMano", "Bolsa_Mano")
                : HandSkinMesh.Build(patient, true, 0.45f, 0.0035f, "Body", "FilmMano", "Film_Mano");
            if (layer == null)
            {
                Debug.LogWarning("[CoverItem] No se pudo cubrir la mano (sin malla de piel).", this);
            }

            FinishCover(hand);
        }

        private void FinishCover(Transform hand)
        {
            Hito1FX.PlayAt(kind == Kind.Gasa ? "tela_roce_2" : "film_estirar", hand.position, 0.7f);
            burnSequence.Report(BurnSequenceManager.BurnEvent.Covered, kind.ToString());
            burnSequence.ChangeState(BurnSequenceManager.BurnState.Covered);
            if (arm != null)
            {
                arm.EndShow();
            }

            if (kind != Kind.Film)
            {
                Destroy(gameObject);
            }
        }

        // La paciente y su mano quemada (false si aun no esta lista).
        private bool Patient(out Transform hand)
        {
            if (patient == null)
            {
                patient = FindObjectOfType<PatientBody>();
                arm = patient != null ? patient.GetComponent<PatientBurnArm>() : null;
                burnOverlay = patient != null ? patient.GetComponent<BurnSkinOverlay>() : null;
            }

            hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            return hand != null;
        }

        // Centro de la quemadura (el de BurnSkinOverlay, en el hueso de la mano).
        private Vector3 BurnCenter(Transform hand)
        {
            return burnOverlay != null ? hand.position + hand.rotation * burnOverlay.burnCenter : hand.position;
        }

        private float Distance(Vector3 point)
        {
            float best = Vector3.Distance(transform.position, point);
            foreach (Collider col in colliders)
            {
                if (col != null && col.enabled)
                {
                    best = Mathf.Min(best, Vector3.Distance(col.ClosestPoint(point), point));
                }
            }

            return best;
        }
    }
}
