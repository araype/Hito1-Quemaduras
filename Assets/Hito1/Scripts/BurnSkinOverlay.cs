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
    // La quemadura no cubre toda la mano (antes parecia un guante rojo): una
    // mascara por vertice la concentra en el dorso, donde cayo el agua, con
    // bordes irregulares (PaintMask, shader Hito1/PielQuemada). Mientras no
    // se enfria, el enrojecimiento se extiende y el centro se oscurece
    // (segundo grado); enfriarla a tiempo la deja chica.
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

        [Header("Extension")]
        [Tooltip("Cuanto de la zona se ve recien quemada y cuanto si nunca se enfria (0 a 1).")]
        public float startSpread = 0.65f;
        public float maxSpread = 0.95f;
        [Tooltip("Segundos sin agua hasta llegar a la extension mayor.")]
        public float spreadSeconds = 60f;

        private SkinnedMeshRenderer overlay;
        private BurnInjury injury;
        private MaterialPropertyBlock block;
        private Color current = new Color(1f, 0.55f, 0.5f, 0f);
        private Color target;
        private float cooledAmount;
        private float spread;
        private float core;

        private static readonly int SpreadId = Shader.PropertyToID("_Spread");
        private static readonly int CoreId = Shader.PropertyToID("_Core");

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
            if (overlay != null)
            {
                PaintMask(overlay, rightHand);
            }

            block = new MaterialPropertyBlock();
            spread = startSpread;
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

            // Sin agua se extiende y el centro se oscurece; no se achica al
            // enfriar (solo cambia el color).
            float untreated = injury != null ? injury.UntreatedSeconds : 0f;
            float k = Mathf.Clamp01(untreated / Mathf.Max(1f, spreadSeconds));
            spread = Mathf.Max(spread, Mathf.Lerp(startSpread, maxSpread, k));
            bool second = injury == null || injury.degree == BurnInjury.BurnDegree.SegundoSuperficial;
            core = second ? Mathf.Lerp(0.35f, 1f, k) : 0.15f;
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
            block.SetFloat(SpreadId, spread);
            block.SetFloat(CoreId, core);
            overlay.SetPropertyBlock(block);
        }

        private SkinnedMeshRenderer BuildOverlay()
        {
            return HandSkinMesh.Build(this, rightHand, minHandWeight, surfaceOffset, skinRendererName,
                "QuemaduraPiel", "Piel_Quemada_Zonas");
        }

        // Mascara de la quemadura en el color de cada vertice: alfa = cuanto
        // pertenece a la zona quemada, rojo = centro. Se calcula con la pose
        // actual y los huesos de la mano:
        //   - el dorso (no la palma) es donde cae el agua de la olla;
        //   - a lo largo de la mano: desde la muneca hasta las primeras
        //     falanges, y se apaga hacia las puntas de los dedos;
        //   - ruido para que el borde sea irregular, como una quemadura real.
        public static void PaintMask(SkinnedMeshRenderer overlay, bool rightHand)
        {
            string side = rightHand ? "Right" : "Left";
            Transform hand = null, middle = null, index = null, pinky = null;
            foreach (Transform bone in overlay.bones)
            {
                if (bone == null)
                {
                    continue;
                }

                string n = bone.name;
                if (n.EndsWith(side + "Hand"))
                {
                    hand = bone;
                }
                else if (n.EndsWith(side + "HandMiddle1"))
                {
                    middle = bone;
                }
                else if (n.EndsWith(side + "HandIndex1"))
                {
                    index = bone;
                }
                else if (n.EndsWith(side + "HandPinky1"))
                {
                    pinky = bone;
                }
            }

            if (hand == null || middle == null || index == null || pinky == null)
            {
                Debug.LogWarning("[BurnSkinOverlay] Faltan huesos de la mano: la quemadura cubre toda la mano.");
                return;
            }

            Vector3 along = middle.position - hand.position;
            float length = Mathf.Max(0.01f, along.magnitude);
            along /= length;
            // Dorso: con la mano derecha, dedos x (indice - menique) apunta
            // hacia la palma (comprobado en el modelo); en la izquierda, al
            // reves.
            Vector3 dorsal = Vector3.Cross(along, index.position - pinky.position).normalized * (rightHand ? -1f : 1f);

            Mesh baked = new Mesh();
            overlay.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            Vector3[] normals = baked.normals;
            if (Application.isPlaying)
            {
                Object.Destroy(baked);
            }
            else
            {
                Object.DestroyImmediate(baked);
            }

            // Donde cayo el agua: el centro del dorso, un poco hacia los nudillos.
            Vector3 spot = hand.position + along * length * 0.65f;
            Transform t = overlay.transform;
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = t.position + t.rotation * vertices[i];
                Vector3 n = t.rotation * normals[i];

                float back = Smooth(-0.25f, 0.55f, Vector3.Dot(n, dorsal));
                float u = Vector3.Dot(p - hand.position, along) / length;
                // Se apaga antes del borde de la capa en la muneca (si no, se
                // ve un corte recto) y hacia las puntas de los dedos.
                float lengthwise = Smooth(0.05f, 0.45f, u) * (1f - Smooth(1.15f, 1.8f, u));

                // Dos octavas de ruido (manchas de unos 3 cm y de 1 cm) que
                // deforman la distancia al centro y salpican la mascara: el
                // borde queda irregular y el enrojecimiento, manchado.
                float noise = Noise(p * 35f) * 0.65f + Noise(p * 90f + new Vector3(7f, 3f, 11f)) * 0.35f;
                float blotch = Noise(p * 60f + new Vector3(19f, 5f, 2f));
                float distance = Vector3.Distance(p, spot) / length * (0.75f + (noise - 0.5f) * 2.6f);
                float radial = 1f - Smooth(0.05f, 0.95f, distance);
                float mask = Mathf.Clamp01(back * lengthwise * radial * (0.9f + (blotch - 0.5f) * 0.9f));
                float center = Smooth(0.5f, 0.85f, mask);
                colors[i] = new Color(center, 0f, 0f, mask);
            }

            overlay.sharedMesh.colors = colors;
        }

        private static float Smooth(float from, float to, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));
        }

        // Ruido 3D aproximado con dos planos de ruido de Perlin.
        private static float Noise(Vector3 p)
        {
            return (Mathf.PerlinNoise(p.x + 13.1f, p.y + 5.7f) + Mathf.PerlinNoise(p.y + 41.3f, p.z + 2.9f)
                + Mathf.PerlinNoise(p.z + 17.7f, p.x + 29.1f)) / 3f;
        }
    }
}
