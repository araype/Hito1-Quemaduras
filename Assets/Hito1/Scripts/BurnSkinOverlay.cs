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

        [Header("Forma de la quemadura")]
        [Tooltip("Tamano de las manchas (mas alto = mas chicas).")]
        public float blotchScale = 22f;
        [Tooltip("Cuanto pesa el lado de arriba de la mano (donde cayo el agua) frente a las manchas.")]
        [Range(0f, 1f)] public float topBias = 0.6f;

        private bool maskFromPose;

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
            BuildMask(false);
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

            // Al quemarse, la mascara se rehace con la pose de ese momento:
            // lo que mira hacia arriba es donde cayo el agua.
            if (!maskFromPose && state != BurnSequenceManager.BurnState.Idle)
            {
                maskFromPose = true;
                BuildMask(true);
            }

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

        // Mascara por vertice (la usa el shader Hito1/PielQuemada): alfa =
        // cuanto se ve la quemadura, rojo = intensidad del centro. Borde
        // difuso hacia la muneca, manchas irregulares y, con la pose, mas
        // fuerte en el lado que mira hacia arriba.
        private void BuildMask(bool usePose)
        {
            if (overlay == null || overlay.sharedMesh == null)
            {
                return;
            }

            Mesh mesh = overlay.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Transform[] bones = overlay.bones;
            string side = rightHand ? "RightHand" : "LeftHand";
            bool[] handBone = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                handBone[i] = bones[i] != null && bones[i].name.Contains(side);
            }

            Vector3[] posed = null;
            Vector3[] posedNormals = null;
            if (usePose)
            {
                Mesh baked = new Mesh();
                overlay.BakeMesh(baked);
                posed = baked.vertices;
                posedNormals = baked.normals;
                Destroy(baked);
                if (posed.Length != vertices.Length || posedNormals.Length != vertices.Length)
                {
                    posed = null;
                }
            }

            Transform t = overlay.transform;
            Vector3 seed = new Vector3(Random.value, Random.value, Random.value) * 50f;
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float hand = 0f;
                if (weights.Length == vertices.Length)
                {
                    BoneWeight w = weights[i];
                    hand = Weight(handBone, w.boneIndex0, w.weight0) + Weight(handBone, w.boneIndex1, w.weight1)
                        + Weight(handBone, w.boneIndex2, w.weight2) + Weight(handBone, w.boneIndex3, w.weight3);
                }

                // Hacia la muneca los pesos de la mano bajan: ahi se esfuma.
                float edge = Mathf.SmoothStep(0f, 1f, (hand - minHandWeight) / Mathf.Max(0.05f, 1f - minHandWeight) * 1.6f);

                Vector3 p = t.TransformPoint(posed != null ? posed[i] : vertices[i]);
                float blotch = Noise(p * blotchScale + seed);

                float top = 0.6f;
                if (posed != null)
                {
                    Vector3 n = t.TransformDirection(posedNormals[i]).normalized;
                    top = Mathf.SmoothStep(0f, 1f, (n.y + 0.35f) / 1.1f);
                }

                float shape = Mathf.Lerp(blotch, top, topBias);
                float alpha = edge * Mathf.Lerp(0.12f, 1f, shape * shape);
                float core = Mathf.Clamp01(edge * top * (1.4f * blotch) - 0.35f) * 1.6f;
                colors[i] = new Color(core, 0f, 0f, alpha);
            }

            mesh.colors = colors;
        }

        private static float Weight(bool[] handBone, int index, float weight)
        {
            return index >= 0 && index < handBone.Length && handBone[index] ? weight : 0f;
        }

        // Ruido 3D suave (0..1) con algo de contraste, para las manchas.
        private static float Noise(Vector3 p)
        {
            float n = (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y + 17.3f, p.z) + Mathf.PerlinNoise(p.z + 41.7f, p.x)) / 3f;
            return Mathf.Clamp01((n - 0.5f) * 2.2f + 0.5f);
        }

        private SkinnedMeshRenderer BuildOverlay()
        {
            return HandSkinMesh.Build(this, rightHand, minHandWeight, surfaceOffset, skinRendererName,
                "QuemaduraPiel", "Piel_Quemada");
        }
    }
}
