using UnityEngine;

namespace SojaExiles
{
    // Material para cubrir la quemadura despues de enfriarla:
    //   - Gasa esteril (la opcion principal: aposito esteril que no suelta
    //     pelusa, Cruz Roja Britanica): se apoya sobre la quemadura y se
    //     mantiene encima 1 s (un circulo se llena, HoldHint). Mientras se
    //     acerca, la paciente ofrece la mano. Queda una capa de gasa solo sobre
    //     la quemadura. (Version de experiencia-inmersiva, recuperada.)
    //   - Bolsa plastica limpia (para una mano, NHS): se suelta sobre la mano
    //     quemada y la cubre holgada hasta la muneca.
    // Las dos pasan a Covered. Si se intenta antes de terminar de enfriar, no
    // se pone: es un error y la paciente se queja. La gasa y la bolsa se usan
    // (desaparecen). La gasa tambien se pone si se suelta sobre la mano.
    // El film no usa este componente: se cubre con tiras (FilmRoll y
    // FilmStrip). Kind.Film queda sin uso por las escenas que guardan el
    // numero.
    [RequireComponent(typeof(Rigidbody))]
    public class CoverItem : MonoBehaviour
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
            }
        }

        private void OnDestroy()
        {
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
            float distance = Vector3.Distance(body.worldCenterOfMass, burn);
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

                bool near = distance < placeDistance;
                progress = near ? progress + Time.deltaTime / Mathf.Max(0.1f, holdSeconds)
                    : Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2f);
                hint.Show(burn, "Mantén encima\nde la quemadura", progress);
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

            // Capa sobre la piel de la mano, un poco mas separada que la
            // quemadura y la mano mojada. La bolsa llega hasta la muneca y
            // queda holgada; la gasa, solo sobre la quemadura (y un poco
            // alrededor).
            SkinnedMeshRenderer layer;
            switch (kind)
            {
                case Kind.Bolsa:
                    layer = HandSkinMesh.Build(patient, true, 0.25f, 0.01f, "Body", "BolsaMano", "Bolsa_Mano");
                    break;
                case Kind.Gasa:
                    SkinnedMeshRenderer source = burnOverlay != null ? burnOverlay.Overlay : null;
                    layer = HandSkinMesh.CopyWhere(source, c => c.a > 0.3f || c.r > 0.1f, 0.002f, "GasaMano",
                        "Gasa_Mano");
                    if (layer == null)
                    {
                        layer = HandSkinMesh.Build(patient, true, 0.45f, 0.003f, "Body", "GasaMano", "Gasa_Mano");
                    }

                    break;
                default:
                    layer = HandSkinMesh.Build(patient, true, 0.45f, 0.0035f, "Body", "FilmMano", "Film_Mano");
                    break;
            }

            if (layer == null)
            {
                Debug.LogWarning("[CoverItem] No se pudo cubrir la mano (sin malla de piel).", this);
            }

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
