using UnityEngine;

namespace SojaExiles
{
    // Material para cubrir la quemadura despues de enfriarla: una bolsa
    // plastica limpia (para una mano, NHS). Se suelta
    // sobre la mano quemada y la cubre sin apretar (una capa sobre la piel
    // que sigue a la mano) y pasa a Covered (falta decidir si va al centro de
    // salud). Si se intenta antes de terminar de enfriar, no se pone: es un
    // error y la paciente se queja. La bolsa se usa (desaparece).
    // El film ya no usa este componente: se cubre con tiras (FilmRoll y
    // FilmStrip). Kind.Film y Kind.Gasa quedan sin uso por las escenas que
    // guardan el numero (la gasa se quito: se pega a la herida).
    [RequireComponent(typeof(Rigidbody))]
    public class CoverItem : MonoBehaviour
    {
        public enum Kind
        {
            Film,
            Gasa,   // sin uso
            Bolsa
        }

        public Kind kind;
        public BurnSequenceManager burnSequence;
        [Tooltip("Si se suelta a menos de esta distancia de la mano quemada, la cubre.")]
        public float attachDistance = 0.15f;

        private Grabbable grabbable;
        private Collider[] colliders;
        private bool wasHeld;

        private void Start()
        {
            grabbable = GetComponent<Grabbable>();
            colliders = GetComponentsInChildren<Collider>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }
        }

        private void Update()
        {
            bool held = grabbable != null && grabbable.IsHeld;
            if (wasHeld && !held)
            {
                Released();
            }

            wasHeld = held;
        }

        private void Released()
        {
            PatientBody patient = FindObjectOfType<PatientBody>();
            Transform hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            if (hand == null || burnSequence == null || Distance(hand.position) > attachDistance)
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

            if (state != BurnSequenceManager.BurnState.Cooled)
            {
                return;
            }

            // Capa sobre la piel de la mano, un poco mas separada que la
            // quemadura y la mano mojada. La bolsa llega hasta la muneca y
            // queda holgada.
            SkinnedMeshRenderer layer;
            switch (kind)
            {
                case Kind.Bolsa:
                    layer = HandSkinMesh.Build(patient, true, 0.25f, 0.01f, "Body", "BolsaMano", "Bolsa_Mano");
                    break;
                default:
                    layer = HandSkinMesh.Build(patient, true, 0.45f, 0.0035f, "Body", "FilmMano", "Film_Mano");
                    break;
            }

            if (layer == null)
            {
                Debug.LogWarning("[CoverItem] No se pudo cubrir la mano (sin malla de piel).", this);
            }

            Hito1FX.PlayAt("film_estirar", hand.position, 0.7f);
            burnSequence.Report(BurnSequenceManager.BurnEvent.Covered, kind.ToString());
            burnSequence.ChangeState(BurnSequenceManager.BurnState.Covered);

            if (kind != Kind.Film)
            {
                Destroy(gameObject);
            }
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
