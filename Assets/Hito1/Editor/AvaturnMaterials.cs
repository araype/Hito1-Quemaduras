using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Materiales de la tia de Avaturn con el shader Standard de Unity, en vez
    // de los de glTFast. Con los de glTFast los mapas de normales salian mal
    // (media blusa, medio brazo y media cara oscuros, en las zonas con la
    // textura en espejo) y la piel se veia gris y plana.
    //
    // Saca las imagenes tal cual del GLB a Personajes/Avaturn/Texturas (las
    // de normales se importan como Normal map, las de datos sin sRGB), arma
    // el mapa metalico/suavidad de Standard a partir del de glTF y guarda los
    // materiales en Personajes/Avaturn/Materiales. La piel y los ojos no son
    // metalicos. El pelo, las pestanas y las cejas usan Hito1/CabelloA2C
    // (bordes suaves con el antialiasing del Quest).
    public static class AvaturnMaterials
    {
        private const string Folder = "Assets/Hito1/Personajes/Avaturn";
        private const string TexFolder = Folder + "/Texturas";
        private const string MatFolder = Folder + "/Materiales";

        [System.Serializable]
        private class Gltf
        {
            public GImage[] images;
            public GTexture[] textures;
            public GMaterial[] materials;
            public GBufferView[] bufferViews;
        }

        [System.Serializable]
        private class GImage
        {
            public string mimeType;
            public int bufferView = -1;
        }

        [System.Serializable]
        private class GTexture
        {
            public int source = -1;
        }

        [System.Serializable]
        private class GTexRef
        {
            public int index = -1;
        }

        [System.Serializable]
        private class GPbr
        {
            public GTexRef baseColorTexture;
            public GTexRef metallicRoughnessTexture;
            public float metallicFactor = 1f;
            public float roughnessFactor = 1f;
        }

        [System.Serializable]
        private class GMaterial
        {
            public string name;
            public GPbr pbrMetallicRoughness;
            public GTexRef normalTexture;
            public GTexRef occlusionTexture;
            public string alphaMode;
            public float alphaCutoff = 0.5f;
        }

        [System.Serializable]
        private class GBufferView
        {
            public int byteOffset;
            public int byteLength;
        }

        private enum Kind { Skin, Eyes, Cloth, Hair, Overlay }

        // Cambia los materiales de 'root' (instancia del GLB) por los nuevos.
        public static void Apply(GameObject root, string glbPath)
        {
            byte[] glb = File.ReadAllBytes(glbPath);
            int jsonLength = System.BitConverter.ToInt32(glb, 12);
            string json = Encoding.UTF8.GetString(glb, 20, jsonLength);
            int binStart = 20 + jsonLength + 8;
            Gltf gltf = JsonUtility.FromJson<Gltf>(json);

            EnsureFolder(Folder, "Texturas");
            EnsureFolder(Folder, "Materiales");

            Dictionary<string, Material> byName = new Dictionary<string, Material>();
            foreach (GMaterial m in gltf.materials)
            {
                if (m == null || string.IsNullOrEmpty(m.name) || byName.ContainsKey(m.name))
                {
                    continue;
                }

                byName[m.name] = Build(m, gltf, glb, binStart);
            }

            AssetDatabase.SaveAssets();

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && byName.TryGetValue(mats[i].name, out Material built))
                    {
                        mats[i] = built;
                    }
                }

                r.sharedMaterials = mats;
            }
        }

        private static Kind KindOf(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("hair") || n.Contains("eyelash") || n.Contains("brow"))
            {
                return Kind.Hair;
            }

            if (n == "eyeao")
            {
                return Kind.Overlay;
            }

            if (n == "eyes" || n == "teeth")
            {
                return Kind.Eyes;
            }

            if (n == "body" || n == "head")
            {
                return Kind.Skin;
            }

            return Kind.Cloth;
        }

        private static Material Build(GMaterial m, Gltf gltf, byte[] glb, int binStart)
        {
            Kind kind = KindOf(m.name);
            GPbr pbr = m.pbrMetallicRoughness ?? new GPbr();
            string safe = m.name.Replace(" ", "_");

            Texture2D color = ExtractTexture(Ref(pbr.baseColorTexture), gltf, glb, binStart, safe + "_color", Role.Color,
                kind == Kind.Skin || kind == Kind.Eyes);
            Texture2D normal = ExtractTexture(Ref(m.normalTexture), gltf, glb, binStart, safe + "_normal", Role.Normal,
                false);
            Texture2D occlusion = ExtractTexture(Ref(m.occlusionTexture), gltf, glb, binStart, safe + "_oclusion",
                Role.Data, false);

            string path = MatFolder + "/" + safe + ".mat";
            AssetDatabase.DeleteAsset(path);

            Material mat;
            if (kind == Kind.Hair)
            {
                mat = new Material(Shader.Find("Hito1/CabelloA2C"));
                mat.SetTexture("_MainTex", color);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                }

                mat.SetFloat("_Cutoff", m.alphaMode == "BLEND" ? 0.35f : Mathf.Clamp(m.alphaCutoff, 0.2f, 0.6f));
                mat.SetFloat("_Glossiness", 0.3f);
                AssetDatabase.CreateAsset(mat, path);
                return mat;
            }

            mat = new Material(Shader.Find("Standard"));
            mat.SetTexture("_MainTex", color);
            mat.SetColor("_Color", Color.white);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", kind == Kind.Skin ? 0.8f : 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", occlusion);
                mat.SetFloat("_OcclusionStrength", 1f);
            }

            // Metal y suavidad: Standard los lee de R y A; glTF los trae en B
            // (metal) y G (rugosidad). La piel y los ojos no son metalicos.
            bool metalAllowed = kind == Kind.Cloth;
            float glossScale = kind == Kind.Skin ? 0.55f : kind == Kind.Eyes ? 1f : 0.8f;
            Texture2D metalGloss = BuildMetalGloss(Ref(pbr.metallicRoughnessTexture), gltf, glb, binStart,
                safe + "_metalsuave", metalAllowed ? pbr.metallicFactor : 0f, pbr.roughnessFactor);
            if (metalGloss != null)
            {
                mat.SetTexture("_MetallicGlossMap", metalGloss);
                mat.SetFloat("_GlossMapScale", glossScale);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Glossiness", kind == Kind.Eyes ? 0.85f : (1f - pbr.roughnessFactor) * glossScale);
            }

            if (kind == Kind.Overlay || m.alphaMode == "BLEND")
            {
                SetFade(mat);
            }
            else if (m.alphaMode == "MASK")
            {
                SetCutout(mat, m.alphaCutoff);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static int Ref(GTexRef r)
        {
            return r != null ? r.index : -1;
        }

        private enum Role { Color, Normal, Data }

        // Escribe la imagen del GLB como archivo y la importa con su tipo.
        private static Texture2D ExtractTexture(int textureIndex, Gltf gltf, byte[] glb, int binStart, string name,
            Role role, bool sharp)
        {
            byte[] bytes = ImageBytes(textureIndex, gltf, glb, binStart, out string ext);
            if (bytes == null)
            {
                return null;
            }

            string path = TexFolder + "/" + name + ext;
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = role == Role.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = role == Role.Color;
            importer.alphaIsTransparency = role == Role.Color;
            importer.mipmapEnabled = true;
            importer.anisoLevel = role == Role.Color ? 4 : 2;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // En el Quest, la cara, el cuerpo y los ojos con la mejor
            // compresion (ASTC 4x4); lo demas con 6x6.
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = sharp || role == Role.Normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D BuildMetalGloss(int textureIndex, Gltf gltf, byte[] glb, int binStart, string name,
            float metallic, float roughness)
        {
            byte[] bytes = ImageBytes(textureIndex, gltf, glb, binStart, out string _);
            if (bytes == null)
            {
                return null;
            }

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!source.LoadImage(bytes))
            {
                Object.DestroyImmediate(source);
                return null;
            }

            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                float metal = p.b / 255f * metallic;
                float smooth = 1f - p.g / 255f * roughness;
                pixels[i] = new Color32((byte)(metal * 255f), 0, 0, (byte)(Mathf.Clamp01(smooth) * 255f));
            }

            Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            result.SetPixels32(pixels);
            result.Apply();
            string path = TexFolder + "/" + name + ".png";
            File.WriteAllBytes(path, result.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static byte[] ImageBytes(int textureIndex, Gltf gltf, byte[] glb, int binStart, out string ext)
        {
            ext = ".png";
            if (textureIndex < 0 || gltf.textures == null || textureIndex >= gltf.textures.Length)
            {
                return null;
            }

            int imageIndex = gltf.textures[textureIndex].source;
            if (imageIndex < 0 || imageIndex >= gltf.images.Length)
            {
                return null;
            }

            GImage image = gltf.images[imageIndex];
            if (image.bufferView < 0 || image.bufferView >= gltf.bufferViews.Length)
            {
                return null;
            }

            GBufferView view = gltf.bufferViews[image.bufferView];
            byte[] bytes = new byte[view.byteLength];
            System.Array.Copy(glb, binStart + view.byteOffset, bytes, 0, view.byteLength);
            ext = image.mimeType == "image/jpeg" ? ".jpg" : ".png";
            return bytes;
        }

        private static void SetCutout(Material mat, float cutoff)
        {
            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        private static void SetFade(Material mat)
        {
            mat.SetFloat("_Mode", 2f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
