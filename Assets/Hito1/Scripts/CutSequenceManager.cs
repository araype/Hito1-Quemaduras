using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Maquina de estados del caso "corte con cuchillo" (analoga a
    // BurnSequenceManager). Orden del protocolo de primeros auxilios:
    //   Cut -> presion directa con gasa (PressureApplied) ->
    //   sangrado controlado (BleedingControlled) -> lavado con agua
    //   (Cleaned) -> cubrir con venda/aposito (Success).
    // Mientras la herida sangra se acumula "sangre perdida"; si llega al
    // maximo antes de controlar el sangrado, la simulacion falla.
    public class CutSequenceManager : MonoBehaviour
    {
        public enum CutState
        {
            Idle,
            Cut,
            PressureApplied,
            BleedingControlled,
            Cleaned,
            Success,
            Failed
        }

        public float cutDelaySeconds = 3f;

        // Sangre perdida por segundo segun el estado (unidades arbitrarias,
        // el fallo ocurre al llegar a maxBloodLoss).
        public float bleedRateUncontrolled = 2f;
        public float bleedRateWithPressure = 0.3f;
        public float maxBloodLoss = 100f;

        public CutState CurrentState { get; private set; } = CutState.Idle;
        public float BloodLoss { get; private set; }

        public event System.Action<CutState> OnStateChanged;

        private void Start()
        {
            StartCoroutine(TriggerCutAfterDelay());
        }

        private IEnumerator TriggerCutAfterDelay()
        {
            yield return new WaitForSeconds(cutDelaySeconds);
            TriggerCut();
        }

        private void Update()
        {
            float rate = CurrentBleedRate();
            if (rate <= 0f)
            {
                return;
            }

            BloodLoss += rate * Time.deltaTime;
            if (BloodLoss >= maxBloodLoss)
            {
                ChangeState(CutState.Failed);
            }
        }

        public float CurrentBleedRate()
        {
            switch (CurrentState)
            {
                case CutState.Cut:
                    return bleedRateUncontrolled;
                case CutState.PressureApplied:
                    return bleedRateWithPressure;
                default:
                    return 0f;
            }
        }

        public void TriggerCut()
        {
            ChangeState(CutState.Cut);
        }

        public void ChangeState(CutState newState)
        {
            if (newState == CurrentState)
            {
                return;
            }

            CurrentState = newState;
            Debug.Log($"[CutSequence] Estado -> {newState} (sangre perdida: {BloodLoss:F1})");
            OnStateChanged?.Invoke(newState);
        }
    }
}
