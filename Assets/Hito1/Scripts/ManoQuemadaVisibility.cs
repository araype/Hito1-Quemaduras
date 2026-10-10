using UnityEngine;

namespace SojaExiles
{
    // Oculta el marcador de la mano quemada (y desactiva su collider, para
    // que no se pueda agarrar) hasta que BurnSequenceManager salga de Idle.
    // Antes de eso no deberia existir nada que agarrar: la persona todavia no
    // se ha quemado.
    [RequireComponent(typeof(Collider))]
    public class ManoQuemadaVisibility : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;

        private Renderer targetRenderer;
        private Collider targetCollider;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            targetCollider = GetComponent<Collider>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (burnSequence == null)
            {
                return;
            }

            burnSequence.OnStateChanged += HandleStateChanged;

            // Si la secuencia ya avanzo antes de que este objeto se habilitara
            // (reactivado, o activado tarde), no quedarse oculto.
            if (burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle)
            {
                SetVisible(true);
            }
        }

        private void OnDisable()
        {
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(BurnSequenceManager.BurnState newState)
        {
            if (newState == BurnSequenceManager.BurnState.Burned)
            {
                SetVisible(true);
            }
        }

        private void SetVisible(bool visible)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = visible;
            }

            if (targetCollider != null)
            {
                targetCollider.enabled = visible;
            }
        }
    }
}
