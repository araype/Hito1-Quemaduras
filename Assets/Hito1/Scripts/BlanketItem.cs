using UnityEngine;

namespace SojaExiles
{
    // Manta doblada sobre el sofa de la sala, para abrigar a la paciente
    // mientras se le enfria la mano (el agua fria tambien le enfria el
    // cuerpo; NHS y Cruz Roja piden mantener abrigada a la persona). Hay que
    // ir a buscarla: la paciente se queda en la cocina con la mano bajo el
    // agua. Se agarra y se suelta sobre sus hombros o su espalda: queda
    // echada encima, con tela simulada (BlanketDrape).
    [RequireComponent(typeof(Rigidbody))]
    public class BlanketItem : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Si se suelta a menos de esta distancia del pecho o la espalda, queda puesta.")]
        public float attachDistance = 0.45f;
        public string materialName = "Manta";

        public bool IsOn { get; private set; }

        private Grabbable grabbable;
        private bool wasHeld;

        private void Start()
        {
            grabbable = GetComponent<Grabbable>();
            if (grabbable != null)
            {
                // Si se cae al piso de la cocina, se queda ahi: volver sola al
                // sofa de la sala seria raro.
                grabbable.returnFromFloor = false;
            }

            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }
        }

        private void Update()
        {
            bool held = grabbable != null && grabbable.IsHeld;
            if (wasHeld && !held && !IsOn)
            {
                TryPutOn();
            }

            wasHeld = held;
        }

        // Tambien lo usan las pruebas en el editor (soltarla junto a ella).
        public bool TryPutOn()
        {
            PatientBody patient = FindObjectOfType<PatientBody>();
            if (patient == null || !patient.IsReady || patient.Chest == null)
            {
                return false;
            }

            // Cerca del pecho, la espalda o los hombros.
            Vector3 p = transform.position;
            float distance = Mathf.Min(Vector3.Distance(p, patient.Chest.position),
                Vector3.Distance(p, patient.ShoulderPosition(true)),
                Vector3.Distance(p, patient.ShoulderPosition(false)));
            if (distance > attachDistance)
            {
                return false;
            }

            IsOn = true;
            BlanketDrape.PutOn(patient, materialName);
            if (burnSequence != null && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.SweaterOn);
            }

            Destroy(gameObject);
            return true;
        }
    }
}
