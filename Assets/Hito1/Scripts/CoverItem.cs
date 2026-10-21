using UnityEngine;

namespace SojaExiles
{
    // Material para cubrir la quemadura despues de enfriarla: film
    // transparente de cocina o una gasa esteril. Se suelta sobre la mano
    // quemada y la cubre sin apretar (una capa sobre la piel que sigue a la
    // mano). Si se intenta antes de terminar de enfriar, no se pone: es un
    // error y la paciente se queja.
    // El rollo de film queda (se corta un pedazo); la gasa se usa.
    [RequireComponent(typeof(Rigidbody))]
    public class CoverItem : MonoBehaviour
    {
        public enum Kind
        {
            Film,
            Gasa
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
            // quemadura y la mano mojada.
            SkinnedMeshRenderer layer = HandSkinMesh.Build(patient, true, 0.45f, kind == Kind.Film ? 0.0035f : 0.003f,
                "Body", kind == Kind.Film ? "FilmMano" : "GasaMano", kind == Kind.Film ? "Film_Mano" : "Gasa_Mano");
            if (layer == null)
            {
                Debug.LogWarning("[CoverItem] No se pudo cubrir la mano (sin malla de piel).", this);
            }

            Hito1FX.PlayAt(kind == Kind.Film ? "film_estirar" : "tela_roce_2", hand.position, 0.7f);
            burnSequence.Report(BurnSequenceManager.BurnEvent.Covered, kind.ToString());
            burnSequence.ChangeState(BurnSequenceManager.BurnState.Success);

            if (kind == Kind.Gasa)
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
