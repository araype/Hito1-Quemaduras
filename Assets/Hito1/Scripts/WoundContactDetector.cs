using UnityEngine;

namespace SojaExiles
{
    // Va sobre un collider trigger en la herida (hijo del marcador de la
    // mano). Cuenta cuanto tiempo un FirstAidItem del tipo indicado esta en
    // contacto continuo con la herida; si el contacto se interrumpe, el
    // contador se reinicia (ensena a no levantar la gasa para "mirar").
    // Se usan tres instancias: Gasa (Cut -> PressureApplied ->
    // BleedingControlled), Agua (BleedingControlled -> Cleaned) y Venda
    // (Cleaned -> Success).
    [RequireComponent(typeof(Collider))]
    public class WoundContactDetector : MonoBehaviour
    {
        public CutSequenceManager cutSequence;
        public FirstAidItem.ItemType requiredItem;
        public CutSequenceManager.CutState requiredState;

        // Estado al que se pasa apenas empieza el contacto (opcional).
        public bool setStateOnContact;
        public CutSequenceManager.CutState stateOnContact;

        public CutSequenceManager.CutState stateOnComplete;
        public float requiredSeconds = 5f;

        // Margen para no reiniciar por un frame sin OnTriggerStay.
        public float contactGraceSeconds = 0.25f;

        private float accumulated;
        private float lastContactTime = float.NegativeInfinity;

        private void OnTriggerStay(Collider other)
        {
            if (cutSequence == null || !IsExpectedState())
            {
                return;
            }

            FirstAidItem item = other.GetComponentInParent<FirstAidItem>();
            if (item == null || item.type != requiredItem)
            {
                return;
            }

            if (setStateOnContact && cutSequence.CurrentState == requiredState)
            {
                cutSequence.ChangeState(stateOnContact);
            }

            lastContactTime = Time.time;
        }

        private void Update()
        {
            if (cutSequence == null || !IsExpectedState())
            {
                accumulated = 0f;
                return;
            }

            bool inContact = Time.time - lastContactTime <= contactGraceSeconds;
            if (!inContact)
            {
                if (accumulated > 0f)
                {
                    Debug.Log($"[CutSequence] Contacto con {requiredItem} interrumpido, contador reiniciado");
                }

                accumulated = 0f;
                if (setStateOnContact && cutSequence.CurrentState == stateOnContact)
                {
                    cutSequence.ChangeState(requiredState);
                }
                return;
            }

            accumulated += Time.deltaTime;
            if (accumulated >= requiredSeconds)
            {
                accumulated = 0f;
                cutSequence.ChangeState(stateOnComplete);
            }
        }

        private bool IsExpectedState()
        {
            CutSequenceManager.CutState s = cutSequence.CurrentState;
            return s == requiredState || (setStateOnContact && s == stateOnContact);
        }
    }
}
