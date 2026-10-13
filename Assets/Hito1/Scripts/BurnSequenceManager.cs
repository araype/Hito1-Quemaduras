using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Maquina de estados del caso "quemadura de primer grado" (analoga a
    // CutSequenceManager). Orden del protocolo de primeros auxilios:
    //   Burned (la mano se quema; por ahora se dispara tras un retraso, como
    //   sustituto del contacto real mano-olla) -> Cooling (la mano esta bajo
    //   el chorro de agua fria) -> Cooled (se mantuvo bajo el agua el tiempo
    //   necesario) -> Success (se cubrio con un pano limpio).
    // Los cambios Cooling/Cooled/Success los hacen los BurnContactDetector
    // del trigger de la quemadura. Si se saca la mano del agua antes de
    // tiempo, vuelve a Burned y el enfriamiento empieza de cero.
    public class BurnSequenceManager : MonoBehaviour
    {
        public enum BurnState
        {
            Idle,
            Burned,
            Cooling,
            Cooled,
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
