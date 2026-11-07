using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Muestra la quemadura sobre la piel de la mano del paciente, en vez de la
    // esfera roja del prototipo. Copia los triangulos de la piel que mueven
    // los huesos de esa mano y los dibuja apenas por encima, con el shader
    // Hito1/PielQuemada. Como la copia usa los mismos huesos, se deforma
    // junto con la mano. La forma (una mancha irregular en el dorso, con la
    // piel levantada en segundo grado) la arma BurnSkinPattern.
    //   Burned: roja y cada vez mas intensa.  Cooling (bajo el agua): se calma.
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

        [Header("Forma (en el espacio del hueso de la mano, en metros)")]
        [Tooltip("Centro de la mancha: el dorso, entre la muneca y los nudillos.")]
        public Vector3 burnCenter = new Vector3(-0.012f, 0.055f, -0.02f);
        [Tooltip("Hacia donde mira el dorso de la mano (el eje -Z en el modelo de Mixamo).")]
        public Vector3 dorsumDirection = new Vector3(0f, 0f, -1f);
        [Tooltip("Radio de la mancha a lo ancho de la mano.")]
        public float burnRadius = 0.04f;

        [Header("Calor (0 enfriada, 1 roja viva) y visibilidad segun la etapa")]
        [Tooltip("Al quemarse; sube hasta 1 mientras no se enfria.")]
        public float burnedHeat = 0.75f;
        [Tooltip("Segundos sin agua hasta llegar al rojo mas intenso.")]
        public float heatRiseSeconds = 30f;
        public float cooledHeat = 0.2f;
        public float successHeat = 0.12f;
        [Range(0f, 1f)] public float cooledAlpha = 0.85f;
        [Range(0f, 1f)] public float successAlpha = 0.6f;
        [Tooltip("Segundos de agua para pasar del rojo al rosado (el detector pide 10).")]
        public float coolingSeconds = 10f;

        private SkinnedMeshRenderer overlay;
        private BurnInjury injury;
        private MaterialPropertyBlock block;
        private float heat;
        private float alpha;
        private float targetHeat;
        private float targetAlpha;
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
            if (overlay != null)
            {
                Paint();
            }

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
            float untreated = injury != null ? injury.UntreatedSeconds : 0f;
            float hot = Mathf.Lerp(burnedHeat, 1f, Mathf.Clamp01(untreated / Mathf.Max(1f, heatRiseSeconds)));
            switch (state)
            {
                case BurnSequenceManager.BurnState.Idle:
                    targetAlpha = 0f;
                    targetHeat = burnedHeat;
                    cooledAmount = 0f;
                    break;
                case BurnSequenceManager.BurnState.Burned:
                    // Si se saco la mano del agua antes de tiempo, vuelve a
                    // enrojecer de a poco.
                    cooledAmount = Mathf.MoveTowards(cooledAmount, 0f, Time.deltaTime / (coolingSeconds * 2f));
                    targetHeat = Mathf.Lerp(hot, cooledHeat, cooledAmount);
                    targetAlpha = Mathf.Lerp(1f, cooledAlpha, cooledAmount);
                    break;
                case BurnSequenceManager.BurnState.Cooling:
                    cooledAmount = Mathf.MoveTowards(cooledAmount, 1f, Time.deltaTime / coolingSeconds);
                    targetHeat = Mathf.Lerp(hot, cooledHeat, cooledAmount);
                    targetAlpha = Mathf.Lerp(1f, cooledAlpha, cooledAmount);
                    break;
                case BurnSequenceManager.BurnState.Cooled:
                    cooledAmount = 1f;
                    targetHeat = cooledHeat;
                    targetAlpha = cooledAlpha;
                    break;
                case BurnSequenceManager.BurnState.Covered:
                case BurnSequenceManager.BurnState.Success:
                    targetHeat = successHeat;
                    targetAlpha = successAlpha;
                    break;
                default:
                    targetHeat = hot;
                    targetAlpha = 1f;
                    break;
            }

            // El agua caliente la vuelve a enrojecer enseguida.
            if (injury != null && injury.HotWaterNow)
            {
                cooledAmount = Mathf.MoveTowards(cooledAmount, 0f, Time.deltaTime / 1.5f);
                targetHeat = 1f;
                targetAlpha = 1f;
            }

            // La quemadura aparece en un segundo, no de golpe.
            float k = 1f - Mathf.Exp(-Time.deltaTime * 2.5f);
            heat = Mathf.Lerp(heat, targetHeat, k);
            alpha = Mathf.Lerp(alpha, targetAlpha, k);
            Apply();
        }

        private void Apply()
        {
            if (overlay == null)
            {
                return;
            }

            overlay.enabled = alpha > 0.01f;
            block.SetFloat("_Heat", heat);
            block.SetFloat("_Alpha", alpha);
            block.SetFloat("_Degree", SecondDegree ? 1f : 0f);
            block.SetTexture("_NoiseTex", BurnSkinPattern.NoiseTexture);
            block.SetTexture("_NormalTex", BurnSkinPattern.NormalTexture);
            overlay.SetPropertyBlock(block);
        }

        private bool SecondDegree
        {
            get { return injury != null && injury.degree == BurnInjury.BurnDegree.SegundoSuperficial; }
        }

        private void Paint()
        {
            PatientBody body = GetComponent<PatientBody>();
            Transform hand = body != null && body.IsReady ? body.HandBone(rightHand) : null;
            if (hand == null)
            {
                string side = rightHand ? "RightHand" : "LeftHand";
                foreach (Transform bone in overlay.bones)
                {
                    if (bone != null && bone.name.EndsWith(side))
                    {
                        hand = bone;
                        break;
                    }
                }
            }

            if (hand == null)
            {
                Debug.LogWarning("[BurnSkinOverlay] Sin hueso de la mano: la quemadura cubre toda la mano.", this);
                return;
            }

            BurnSkinPattern.Paint(overlay, hand, burnCenter, dorsumDirection, burnRadius, SecondDegree,
                Random.Range(0, 100000));
        }

        private SkinnedMeshRenderer BuildOverlay()
        {
            return HandSkinMesh.Build(this, rightHand, minHandWeight, surfaceOffset, skinRendererName,
                "QuemaduraPiel", "Piel_Quemada_Real");
        }
    }
}
