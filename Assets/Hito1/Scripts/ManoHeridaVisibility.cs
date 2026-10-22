using UnityEngine;

namespace SojaExiles
{
    // Equivalente de ManoQuemadaVisibility para el caso de corte: oculta el
    // marcador de la mano herida (y desactiva sus colliders, incluido el
    // trigger de la herida) hasta que CutSequenceManager entre en Cut.
    public class ManoHeridaVisibility : MonoBehaviour
    {
        public CutSequenceManager cutSequence;

        private Renderer targetRenderer;
        private Collider[] targetColliders;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            targetColliders = GetComponentsInChildren<Collider>(true);
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (cutSequence != null)
            {
                cutSequence.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (cutSequence != null)
            {
                cutSequence.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(CutSequenceManager.CutState newState)
        {
            if (newState == CutSequenceManager.CutState.Cut)
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

            foreach (Collider col in targetColliders)
            {
                col.enabled = visible;
            }
        }
    }
}
