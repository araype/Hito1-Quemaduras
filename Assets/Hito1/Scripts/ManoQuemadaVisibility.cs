using UnityEngine;

namespace SojaExiles
{
    // Oculta el marcador de la mano quemada (y desactiva sus colliders,
    // incluido el trigger de la quemadura, para que no se pueda agarrar ni
    // enfriar) hasta que BurnSequenceManager salga de Idle.
    // Antes de eso no deberia existir nada que agarrar: la persona todavia no
    // se ha quemado.
    [RequireComponent(typeof(Collider))]
    public class ManoQuemadaVisibility : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;

        // Si es false, la esfera del marcador no se dibuja aunque la mano ya
        // este quemada (sus colliders si se activan). BurnSkinOverlay la
        // apaga porque muestra la quemadura sobre la piel del paciente.
        public bool showMarker = true;

        private bool visible;

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
            if (newState != BurnSequenceManager.BurnState.Idle)
            {
                SetVisible(true);
            }
        }

        public void SetMarkerShown(bool shown)
        {
            showMarker = shown;
            SetVisible(visible);
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (targetRenderer != null)
            {
                targetRenderer.enabled = visible && showMarker;
            }

            foreach (Collider col in targetColliders)
            {
                col.enabled = visible;
            }
        }
    }
}
