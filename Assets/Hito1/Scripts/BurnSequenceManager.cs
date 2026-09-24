using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Fase 0: maquina de estados de la secuencia de quemadura/mitigacion.
    // Fase 1: dispara "Burned" automaticamente tras un retraso, como
    // sustituto temporal del contacto real mano-olla (fases futuras
    // reemplazaran TriggerBurn() por un trigger fisico real).
    public class BurnSequenceManager : MonoBehaviour
    {
        public enum BurnState
        {
            Idle,
            Burned,
            WaitingWater,
            Cooled,
            WaitingCloth,
            Success,
            Failed
        }

        public float burnDelaySeconds = 3f;

        public BurnState CurrentState { get; private set; } = BurnState.Idle;

        public event System.Action<BurnState> OnStateChanged;

        private void Start()
        {
            StartCoroutine(TriggerBurnAfterDelay());
        }

        private IEnumerator TriggerBurnAfterDelay()
        {
            yield return new WaitForSeconds(burnDelaySeconds);
            TriggerBurn();
        }

        public void TriggerBurn()
        {
            ChangeState(BurnState.Burned);
        }

        public void ChangeState(BurnState newState)
        {
            if (newState == CurrentState)
            {
                return;
            }

            CurrentState = newState;
            Debug.Log($"[BurnSequence] Estado -> {newState}");
            OnStateChanged?.Invoke(newState);
        }
    }
}
