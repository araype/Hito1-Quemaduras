using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Arma la caja de film y la bolsa de cierre a partir de los modelos de
    // Sketchfab (CC BY 4.0) que estan en Kits/Cubiertas:
    // - "Aluminum Foil Box - Low Poly", de Ian Valeta (caja de film, con una
    //   etiqueta propia: FilmCaja/FilmCaja_Etiqueta.png).
    // - "Ziplock bag", de Phoenix_Connor (bolsa transparente sin el logo:
    //   BolsaZip/BolsaZip_Color.png y BolsaZip_Normal.png).
    // Guarda una malla en metros, con la base en y = 0 y centrada en X y Z, y
    // un material Standard en Resources/Hito1FX. BurnSceneProps las carga al
    // dar Play (si faltan, usa las piezas hechas por codigo).
    public static class CoverModelsSetup
    {
        private const string Kit = "Assets/Hito1/Kits/Cubiertas/";
        private const string Out = "Assets/Hito1/Resources/Hito1FX/";

        // Giros para dejar cada modelo en la pose que espera el codigo:
        // caja con el largo en X y la tapa arriba; bolsa plana (lo delgado en
        // Y), con el cierre hacia -Z.
        // La caja va girada media vuelta: el titulo de la tapa se lee desde
        // el frente (+Z, hacia quien abre el cajon).
        public static Vector3 BoxEuler = new Vector3(0f, 180f, 0f);
        public static Vector3 BagEuler = new Vector3(90f, 0f, 0f);
        // Bolsa del tamano de una mano (el modelo mide 35 x 45 cm) y caja de
        // film de 24,5 cm de largo (el glTF llega en centimetros).
        public const float BagLength = 0.27f;
        public const float BoxLength = 0.245f;

        [MenuItem("Hito1/Preparar film y bolsa importados")]
        public static void Run()
        {
            Textures();
            Mesh box = Bake(Kit + "FilmCaja/scene.gltf", "LOD_0", BoxEuler, BoxLength, "FilmCaja_Malla");
            Mesh bag = Bake(Kit + "BolsaZip/scene.gltf", null, BagEuler, BagLength, "BolsaZip_Malla");

            Material boxMat = MakeMaterial("FilmCaja", false);
            boxMat.SetTexture("_MainTex", Load<Texture2D>(Kit + "FilmCaja/FilmCaja_Etiqueta.png"));
            boxMat.SetTexture("_BumpMap", Load<Texture2D>(Kit + "FilmCaja/textures/TIN_FOIL_ATLAS_normal.png"));
            boxMat.EnableKeyword("_NORMALMAP");
            boxMat.SetFloat("_Glossiness", 0.3f);
            boxMat.SetFloat("_Metallic", 0f);

            Material bagMat = MakeMaterial("BolsaZip", true);
            bagMat.SetTexture("_MainTex", Load<Texture2D>(Kit + "BolsaZip/BolsaZip_Color.png"));
            bagMat.SetTexture("_BumpMap", Load<Texture2D>(Kit + "BolsaZip/BolsaZip_Normal.png"));
            bagMat.EnableKeyword("_NORMALMAP");
            bagMat.SetFloat("_BumpScale", 0.8f);
            bagMat.SetFloat("_Glossiness", 0.88f);
            bagMat.SetFloat("_Metallic", 0f);

            AssetDatabase.SaveAssets();
            Debug.Log("[CoverModelsSetup] Caja " + Describe(box) + "; bolsa " + Describe(bag));
        }

        private static string Describe(Mesh m)
        {
            return m == null ? "(sin malla)" : m.bounds.size.ToString("F3") + ", " + m.triangles.Length / 3 + " triangulos";
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogWarning("[CoverModelsSetup] Falta " + path);
            }

            return asset;
        }

        // Texturas a 1024 (presupuesto del Quest) y los mapas de normales
        // marcados como tales.
        private static void Textures()
        {
            Set(Kit + "FilmCaja/FilmCaja_Etiqueta.png", false, false);
            Set(Kit + "FilmCaja/textures/TIN_FOIL_ATLAS_normal.png", true, false);
            Set(Kit + "BolsaZip/BolsaZip_Color.png", false, true);
            Set(Kit + "BolsaZip/BolsaZip_Normal.png", true, false);
        }

        private static void Set(string path, bool normal, bool alpha)
        {
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("[CoverModelsSetup] Falta " + path);
                return;
            }

            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.maxTextureSize = 1024;
            ti.alphaIsTransparency = alpha;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }

        private static Material MakeMaterial(string name, bool transparent)
        {
            string path = Out + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }

            m.color = Color.white;
            if (transparent)
            {
                // Igual que Bolsa_Plastico: Transparent premultiplicado.
                m.SetFloat("_Mode", 3f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        // Junta la malla del nodo (con sus transformaciones) en una sola, la
        // gira, la escala y la deja con la base en y = 0.
        private static Mesh Bake(string gltf, string nodeContains, Vector3 euler, float length, string outName)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(gltf);
            if (root == null)
            {
                Debug.LogWarning("[CoverModelsSetup] No se importo " + gltf + " (falta glTFast o el archivo).");
                return null;
            }

            MeshFilter source = null;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (nodeContains == null || mf.name.Contains(nodeContains) || mf.transform.parent.name.Contains(nodeContains))
                {
                    source = mf;
                    break;
                }
            }

            if (source == null || source.sharedMesh == null)
            {
                Debug.LogWarning("[CoverModelsSetup] Sin malla en " + gltf);
                return null;
            }

            Mesh src = source.sharedMesh;
            Matrix4x4 m = Matrix4x4.Rotate(Quaternion.Euler(euler)) * root.transform.worldToLocalMatrix
                * source.transform.localToWorldMatrix;
            Vector3[] v = src.vertices;
            Vector3[] n = src.normals;
            Vector4[] t = src.tangents;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = m.MultiplyPoint3x4(v[i]);
            }

            Matrix4x4 nm = m.inverse.transpose;
            for (int i = 0; i < n.Length; i++)
            {
                n[i] = nm.MultiplyVector(n[i]).normalized;
            }

            for (int i = 0; i < t.Length; i++)
            {
                Vector3 d = m.MultiplyVector(t[i]).normalized;
                t[i] = new Vector4(d.x, d.y, d.z, t[i].w);
            }

            Bounds b = new Bounds(v[0], Vector3.zero);
            foreach (Vector3 p in v)
            {
                b.Encapsulate(p);
            }

            float scale = length > 0f ? length / Mathf.Max(b.size.x, b.size.z) : 1f;
            Vector3 offset = new Vector3(b.center.x, b.min.y, b.center.z);
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = (v[i] - offset) * scale;
            }

            List<int> tris = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                tris.AddRange(src.GetTriangles(s));
            }

            if (m.determinant < 0f)
            {
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i];
                    tris[i] = tris[i + 1];
                    tris[i + 1] = a;
                }
            }

            string path = Out + outName + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.name = outName;
            mesh.vertices = v;
            mesh.normals = n;
            if (t.Length == v.Length)
            {
                mesh.tangents = t;
            }

            mesh.uv = src.uv;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (t.Length != v.Length)
            {
                mesh.RecalculateTangents();
            }

            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
