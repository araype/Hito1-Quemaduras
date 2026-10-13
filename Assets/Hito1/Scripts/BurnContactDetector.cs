using UnityEngine;

namespace SojaExiles
{
    // Va sobre el collider trigger de la quemadura (hijo del marcador de la
    // mano quemada). Analogo a WoundContactDetector: cuenta cuanto tiempo un
    // FirstAidItem del tipo indicado esta en contacto continuo con la
    // quemadura; si el contacto se interrumpe, el contador se reinicia
    // (ensena a no sacar la mano del agua antes de tiempo). Se usan dos
    // instancias: Agua (Burned -> Cooling -> Cooled) y Panuelo
    // (Cooled -> Success).
    [RequireComponent(typeof(Collider))]
    public class BurnContactDetector : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public FirstAidItem.ItemType requiredItem;
        public BurnSequenceManager.BurnState requiredState;

        // Estado al que se pasa apenas empieza el contacto (opcional).
        public bool setStateOnContact;
        public BurnSequenceManager.BurnState stateOnContact;

        public BurnSequenceManager.BurnState stateOnComplete;
        public float requiredSeconds = 10f;

        // Margen para no reiniciar por un frame sin OnTriggerStay.
        public float contactGraceSeconds = 0.25f;

        private float accumulated;
        private float lastContactTime = float.NegativeInfinity;

        // Progreso del contacto actual entre 0 y 1 (para indicadores).
        public float Progress
        {
            get { return requiredSeconds > 0f ? Mathf.Clamp01(accumulated / requiredSeconds) : 0f; }
        }

        private void OnTriggerStay(Collider other)
        {
            if (burnSequence == null || !IsExpectedState())
            {
                return;
            }

            FirstAidItem item = other.GetComponentInParent<FirstAidItem>();
            if (item == null || item.type != requiredItem)
            {
                return;
            }

            // El chorro solo cuenta si el agua esta corriendo.
            WaterTap tap = other.GetComponentInParent<WaterTap>();
            if (tap != null && !tap.IsOpen)
            {
                return;
            }

            if (setStateOnContact && burnSequence.CurrentState == requiredState)
            {
                burnSequence.ChangeState(stateOnContact);
            }

            lastContactTime = Time.time;
        }

        private void Update()
        {
            if (burnSequence == null || !IsExpectedState())
            {
                accumulated = 0f;
                return;
            }

            bool inContact = Time.time - lastContactTime <= contactGraceSeconds;
            if (!inContact)
            {
                if (accumulated > 0f)
                {
                    Debug.Log($"[BurnSequence] Contacto con {requiredItem} interrumpido, contador reiniciado");
                }

                accumulated = 0f;
                if (setStateOnContact && burnSequence.CurrentState == stateOnContact)
                {
                    burnSequence.ChangeState(requiredState);
                }
                return;
            }

            accumulated += Time.deltaTime;
            if (accumulated >= requiredSeconds)
            {
                accumulated = 0f;
                burnSequence.ChangeState(stateOnComplete);
            }
        }

        private bool IsExpectedState()
        {
            BurnSequenceManager.BurnState s = burnSequence.CurrentState;
            return s == requiredState || (setStateOnContact && s == stateOnContact);
        }
    }
}
