using System.IO;
using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Genera la textura de la gasa (tejido de algodon abierto, con mapa de
    // normales) y los materiales Gasa_Tejido y Remedio_Pomada en
    // Resources/Hito1FX. Se corre una vez desde el menu; los archivos quedan
    // en el proyecto.
    public static class FirstAidTextures
    {
        private const string Folder = "Assets/Hito1/Resources/Hito1FX";
        private const int Size = 512;
        private const int Threads = 32;

        [MenuItem("Hito1/Generar textura de la gasa")]
        public static void Run()
        {
            float[,] height = new float[Size, Size];
            Color32[] color = new Color32[Size * Size];
            System.Random random = new System.Random(7);
            float cell = Size / (float)Threads;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Hilos verticales (urdimbre) y horizontales (trama), con
                    // un poco de ondulacion para que no se vea perfecto.
                    float wobbleX = Mathf.Sin(y * 0.05f + (x / cell) * 1.7f) * 0.6f;
                    float wobbleY = Mathf.Sin(x * 0.05f + (y / cell) * 2.3f) * 0.6f;
                    float fx = Mathf.Repeat(x + wobbleX, cell) / cell - 0.5f;
                    float fy = Mathf.Repeat(y + wobbleY, cell) / cell - 0.5f;
                    float warp = Thread(fx);
                    float weft = Thread(fy);
                    int ix = Mathf.FloorToInt((x + wobbleX) / cell);
                    int iy = Mathf.FloorToInt((y + wobbleY) / cell);
                    // Por encima y por debajo, alternado (tejido tafetan).
                    bool warpOnTop = ((ix + iy) & 1) == 0;
                    float h = warpOnTop ? Mathf.Max(warp, weft * 0.6f) : Mathf.Max(weft, warp * 0.6f);
                    float fuzz = (float)random.NextDouble() * 0.08f;
                    h = Mathf.Clamp01(h + fuzz * h);
                    height[x, y] = h;

                    // Hilo: blanco algodon; hueco: se ve la capa de abajo, mas
                    // gris.
                    float shade = Mathf.Lerp(0.78f, 1f, h);
                    Color c = new Color(0.98f, 0.97f, 0.94f) * shade;
                    c.a = 1f;
                    color[y * Size + x] = c;
                }
            }

            Texture2D albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            albedo.SetPixels32(color);
            albedo.Apply();
            string colorPath = Folder + "/Gasa_Tejido.png";
            File.WriteAllBytes(colorPath, albedo.EncodeToPNG());

            Color32[] normal = new Color32[Size * Size];
            const float strength = 3f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = height[(x + 1) % Size, y] - height[(x + Size - 1) % Size, y];
                    float dy = height[x, (y + 1) % Size] - height[x, (y + Size - 1) % Size];
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    normal[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            Texture2D bump = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true);
            bump.SetPixels32(normal);
            bump.Apply();
            string normalPath = Folder + "/Gasa_Tejido_Normal.png";
            File.WriteAllBytes(normalPath, bump.EncodeToPNG());
            Object.DestroyImmediate(albedo);
            Object.DestroyImmediate(bump);

            AssetDatabase.ImportAsset(colorPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter ci = (TextureImporter)AssetImporter.GetAtPath(colorPath);
            ci.wrapMode = TextureWrapMode.Repeat;
            ci.anisoLevel = 4;
            ci.SaveAndReimport();
            TextureImporter ni = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            ni.textureType = TextureImporterType.NormalMap;
            ni.wrapMode = TextureWrapMode.Repeat;
            ni.SaveAndReimport();

            Material gauze = LoadOrCreate(Folder + "/Gasa_Tejido.mat");
            gauze.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath));
            gauze.SetTextureScale("_MainTex", new Vector2(2f, 2f));
            gauze.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            gauze.SetFloat("_BumpScale", 0.6f);
            gauze.EnableKeyword("_NORMALMAP");
            gauze.SetFloat("_Glossiness", 0.08f);
            gauze.SetFloat("_Metallic", 0f);
            gauze.color = Color.white;
            EditorUtility.SetDirty(gauze);

            // Pomada untada: crema blanca, brillante y semitransparente.
            Material ointment = LoadOrCreate(Folder + "/Remedio_Pomada.mat");
            Material paste = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Remedio_PastaDental.mat");
            if (paste != null)
            {
                ointment.CopyPropertiesFromMaterial(paste);
                ointment.shaderKeywords = paste.shaderKeywords;
                ointment.renderQueue = paste.renderQueue;
            }

            ointment.color = new Color(0.97f, 0.96f, 0.9f, 0.6f);
            ointment.SetFloat("_Glossiness", 0.8f);
            EditorUtility.SetDirty(ointment);
            AssetDatabase.SaveAssets();
            Debug.Log("[FirstAidTextures] Textura de la gasa y materiales listos en " + Folder);
        }

        // Perfil de un hilo: alto en el centro, cae a los lados.
        private static float Thread(float f)
        {
            float w = Mathf.Abs(f) / 0.32f;
            return w >= 1f ? 0f : Mathf.Cos(w * Mathf.PI * 0.5f);
        }

        private static Material LoadOrCreate(string path)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }

            return m;
        }
    }
}
