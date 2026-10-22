using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Arma una capa sobre la piel de una mano del paciente (la usan la
    // quemadura y la mano mojada).
    public static class HandSkinMesh
    {
        private static float HandWeight(bool[] handBone, int index, float weight)
        {
            return index >= 0 && index < handBone.Length && handBone[index] ? weight : 0f;
        }

        // Copia de los triangulos de la piel que mueven los huesos de una mano,
        // apenas por encima de la piel y con los mismos huesos (se deforma con
        // la mano). Devuelve null si no se puede (y avisa en la consola).
        public static SkinnedMeshRenderer Build(Component owner, bool rightHand, float minHandWeight,
            float surfaceOffset, string skinRendererName, string objectName, string materialName)
        {
            SkinnedMeshRenderer skin = null;
            foreach (SkinnedMeshRenderer r in owner.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.Contains(skinRendererName))
                {
                    skin = r;
                    break;
                }
            }

            if (skin == null || skin.sharedMesh == null)
            {
                Debug.LogWarning("[HandSkinMesh] No se encontro la piel del paciente (" + skinRendererName + ").", owner);
                return null;
            }

            Mesh source = skin.sharedMesh;
            if (!source.isReadable)
            {
                Debug.LogWarning("[HandSkinMesh] La malla del paciente no es legible: activa Read/Write en el FBX.", owner);
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
                Debug.LogWarning("[HandSkinMesh] La malla del paciente no tiene pesos o normales.", owner);
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
                Debug.LogWarning("[HandSkinMesh] No se encontraron triangulos de la mano en la piel.", owner);
                return null;
            }

            Mesh mesh = new Mesh { name = objectName };
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

            GameObject go = new GameObject(objectName);
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
            Material material = Hito1FX.Mat(materialName);
            if (material != null)
            {
                result.sharedMaterial = material;
            }

            Debug.Log($"[HandSkinMesh] {objectName} sobre {t2.Count / 3} triangulos de la mano");
            return result;
        }
    }
}
