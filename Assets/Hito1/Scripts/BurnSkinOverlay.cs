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
    //   Cooled: rosada.           Success (cubierta): queda tenue.
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

            overlay = BuildOverlay();
            block = new MaterialPropertyBlock();
            target = current;
            Apply();
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
                case BurnSequenceManager.BurnState.Success:
                    target = successColor;
                    break;
                default:
                    target = burnedColor;
                    break;
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

        private static float HandWeight(bool[] handBone, int index, float weight)
        {
            return index >= 0 && index < handBone.Length && handBone[index] ? weight : 0f;
        }

        private SkinnedMeshRenderer BuildOverlay()
        {
            SkinnedMeshRenderer skin = null;
            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.Contains(skinRendererName))
                {
                    skin = r;
                    break;
                }
            }

            if (skin == null || skin.sharedMesh == null)
            {
                Debug.LogWarning("[BurnSkinOverlay] No se encontro la piel del paciente (" + skinRendererName + ").", this);
                return null;
            }

            Mesh source = skin.sharedMesh;
            if (!source.isReadable)
            {
                Debug.LogWarning("[BurnSkinOverlay] La malla del paciente no es legible: activa Read/Write en el FBX.", this);
                return null;
            }

            string side = rightHand ? "RightHand" : "LeftHand";
            Transform[] bones = skin.bones;
            bool[] handBone = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                handBone[i] = bones[i] != null && bones[i].name.Contains(side);
            }

            BoneWeight[] weights = source.boneWeights;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uv = source.uv;
            int[] triangles = source.triangles;
            if (weights.Length != vertices.Length || normals.Length != vertices.Length)
            {
                Debug.LogWarning("[BurnSkinOverlay] La malla del paciente no tiene pesos o normales.", this);
                return null;
            }

            bool[] isHand = new bool[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                BoneWeight w = weights[v];
                float sum = HandWeight(handBone, w.boneIndex0, w.weight0)
                    + HandWeight(handBone, w.boneIndex1, w.weight1)
                    + HandWeight(handBone, w.boneIndex2, w.weight2)
                    + HandWeight(handBone, w.boneIndex3, w.weight3);
                isHand[v] = sum >= minHandWeight;
            }

            // Desplazamiento en unidades de la malla (el FBX puede venir en cm).
            float scale = Mathf.Abs(skin.transform.lossyScale.x);
            float offset = scale > 1e-6f ? surfaceOffset / scale : surfaceOffset;

            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> v2 = new List<Vector3>();
            List<Vector3> n2 = new List<Vector3>();
            List<Vector2> uv2 = new List<Vector2>();
            List<BoneWeight> w2 = new List<BoneWeight>();
            List<int> t2 = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (!isHand[a] || !isHand[b] || !isHand[c])
                {
                    continue;
                }

                for (int k = 0; k < 3; k++)
                {
                    int index = triangles[t + k];
                    if (!remap.TryGetValue(index, out int mapped))
                    {
                        mapped = v2.Count;
                        remap[index] = mapped;
                        v2.Add(vertices[index] + normals[index] * offset);
                        n2.Add(normals[index]);
                        uv2.Add(uv.Length == vertices.Length ? uv[index] * 3f : Vector2.zero);
                        w2.Add(weights[index]);
                    }

                    t2.Add(mapped);
                }
            }

            if (t2.Count == 0)
            {
                Debug.LogWarning("[BurnSkinOverlay] No se encontraron triangulos de la mano en la piel.", this);
                return null;
            }

            Mesh mesh = new Mesh { name = "QuemaduraMano" };
            if (v2.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(v2);
            mesh.SetNormals(n2);
            mesh.SetUVs(0, uv2);
            mesh.boneWeights = w2.ToArray();
            mesh.bindposes = source.bindposes;
            mesh.SetTriangles(t2, 0);
            mesh.RecalculateBounds();

            GameObject go = new GameObject("QuemaduraPiel");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(skin.transform.parent, false);
            go.transform.localPosition = skin.transform.localPosition;
            go.transform.localRotation = skin.transform.localRotation;
            go.transform.localScale = skin.transform.localScale;

            SkinnedMeshRenderer result = go.AddComponent<SkinnedMeshRenderer>();
            result.sharedMesh = mesh;
            result.bones = bones;
            result.rootBone = skin.rootBone;
            result.localBounds = skin.localBounds;
            result.updateWhenOffscreen = skin.updateWhenOffscreen;
            result.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = Hito1FX.Mat("Piel_Quemada");
            if (material != null)
            {
                result.sharedMaterial = material;
            }

            Debug.Log($"[BurnSkinOverlay] Quemadura sobre {t2.Count / 3} triangulos de la mano");
            return result;
        }
    }
}
