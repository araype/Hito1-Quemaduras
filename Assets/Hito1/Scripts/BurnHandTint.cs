using UnityEngine;

namespace SojaExiles
{
    // Colorea el marcador de la mano quemada segun la etapa, para que se vea
    // el estado sin leer la consola: rojo (quemada), azul (enfriandose bajo
    // el agua), rosado (enfriada, falta cubrirla) y verde (cubierta).
    [RequireComponent(typeof(Renderer))]
    public class BurnHandTint : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public Color burnedColor = new Color(0.9f, 0.15f, 0.1f, 1f);
        public Color coolingColor = new Color(0.25f, 0.55f, 1f, 1f);
        public Color cooledColor = new Color(1f, 0.7f, 0.6f, 1f);
        public Color successColor = new Color(0.3f, 0.85f, 0.35f, 1f);

        private Renderer target;

        private void Awake()
        {
            target = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged += Apply;
                Apply(burnSequence.CurrentState);
            }
        }

        private void OnDisable()
        {
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged -= Apply;
            }
        }

        private void Apply(BurnSequenceManager.BurnState state)
        {
            switch (state)
            {
                case BurnSequenceManager.BurnState.Cooling:
                    IndicatorUtil.SetColor(target, coolingColor);
                    break;
                case BurnSequenceManager.BurnState.Cooled:
                    IndicatorUtil.SetColor(target, cooledColor);
                    break;
                case BurnSequenceManager.BurnState.Success:
                    IndicatorUtil.SetColor(target, successColor);
                    break;
                default:
                    IndicatorUtil.SetColor(target, burnedColor);
                    break;
            }
        }
    }
}
