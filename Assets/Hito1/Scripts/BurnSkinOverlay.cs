using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Muestra la quemadura sobre la piel de la mano del paciente, en vez de la
    // esfera roja del prototipo. Copia los triangulos de la piel que mueven
    // los huesos de esa mano y los dibuja apenas por encima, con un material
    // de piel enrojecida. Como la copia usa los mismos huesos, se deforma
    // junto con la mano.
    //   Burned: roja e intensa.   Cooling (bajo el agua): se va calmando.
    //   Cooled: rosada.           Covered/Success (cubierta): queda tenue.
    // Va en el paciente (el objeto con PatientBody).
    public class BurnSkinOverlay : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public bool rightHand = true;
        [Tooltip("Oculta la esfera del marcador (sigue sirviendo para agarrar la mano).")]
        public bool hideMarker = true;
        [Tooltip("Peso minimo de los huesos de la mano para que un vertice cuente como mano.")]
        [Range(0.1f, 1f)] public float minHandWeight = 0.6f;
        public float surfaceOffset = 0.0012f;
        public string skinRendererName = "Body";

        [Header("Color segun la etapa")]
        public Color burnedColor = new Color(1f, 0.55f, 0.5f, 0.9f);
        public Color cooledColor = new Color(1f, 0.78f, 0.72f, 0.55f);
        public Color successColor = new Color(1f, 0.82f, 0.78f, 0.4f);
        [Tooltip("Segundos de agua para pasar del rojo al rosado (el detector pide 10).")]
        public float coolingSeconds = 10f;

        private SkinnedMeshRenderer overlay;
        private BurnInjury injury;
        private MaterialPropertyBlock block;
        private Color current = new Color(1f, 0.55f, 0.5f, 0f);
        private Color target;
        private float cooledAmount;

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            if (hideMarker)
            {
                foreach (ManoQuemadaVisibility marker in FindObjectsOfType<ManoQuemadaVisibility>())
                {
                    marker.SetMarkerShown(false);
                }
            }

            injury = GetComponent<BurnInjury>();
            overlay = BuildOverlay();
            block = new MaterialPropertyBlock();
            target = current;
            Apply();
        }

        // Capa de la quemadura (BurnInjury pone las ampollas sobre ella).
        public SkinnedMeshRenderer Overlay
        {
            get { return overlay; }
        }

        private void OnDestroy()
        {
            if (overlay != null)
            {
                Destroy(overlay.sharedMesh);
                Destroy(overlay.gameObject);
            }
        }

        private void Update()
        {
            if (overlay == null || burnSequence == null)
            {
                return;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            switch (state)
            {
                case BurnSequenceManager.BurnState.Idle:
                    target = new Color(burnedColor.r, burnedColor.g, burnedColor.b, 0f);
                    cooledAmount = 0f;
                    break;
                case BurnSequenceManager.BurnState.Burned:
                    // Si se saco la mano del agua antes de tiempo, vuelve a
                    // enrojecer de a poco.
                    cooledAmount = Mathf.MoveTowards(cooledAmount, 0f, Time.deltaTime / (coolingSeconds * 2f));
                    target = Color.Lerp(burnedColor, cooledColor, cooledAmount);
                    break;
                case BurnSequenceManager.BurnState.Cooling:
                    cooledAmount = Mathf.MoveTowards(cooledAmount, 1f, Time.deltaTime / coolingSeconds);
                    target = Color.Lerp(burnedColor, cooledColor, cooledAmount);
                    break;
                case BurnSequenceManager.BurnState.Cooled:
                    cooledAmount = 1f;
                    target = cooledColor;
                    break;
                case BurnSequenceManager.BurnState.Covered:
                case BurnSequenceManager.BurnState.Success:
                    target = successColor;
                    break;
                default:
                    target = burnedColor;
                    break;
            }

            // El agua caliente la vuelve a enrojecer enseguida.
            if (injury != null && injury.HotWaterNow)
            {
                cooledAmount = Mathf.MoveTowards(cooledAmount, 0f, Time.deltaTime / 1.5f);
                target = burnedColor;
            }

            // La quemadura aparece en un segundo, no de golpe.
            current = Color.Lerp(current, target, 1f - Mathf.Exp(-Time.deltaTime * 2.5f));
            Apply();
        }

        private void Apply()
        {
            if (overlay == null)
            {
                return;
            }

            overlay.enabled = current.a > 0.01f;
            block.SetColor("_Color", current);
            overlay.SetPropertyBlock(block);
        }

        private SkinnedMeshRenderer BuildOverlay()
        {
            return HandSkinMesh.Build(this, rightHand, minHandWeight, surfaceOffset, skinRendererName,
                "QuemaduraPiel", "Piel_Quemada");
        }
    }
}
