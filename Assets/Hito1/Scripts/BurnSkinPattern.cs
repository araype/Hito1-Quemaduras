using UnityEngine;

namespace SojaExiles
{
    // Forma de la quemadura sobre la capa de la mano (shader
    // Hito1/PielQuemada). En vez de pintar la mano entera de rojo, marca una
    // mancha irregular en el dorso, donde cayo el agua hirviendo: intensa en
    // el centro, que se escurre hacia los nudillos y la muneca y se apaga en
    // el borde. En segundo grado marca ademas la zona donde la piel se
    // levanta (alli salen las ampollas). Lo guarda en los colores de los
    // vertices (ver el shader) y pone coordenadas de textura planas sobre el
    // dorso para el ruido y el relieve, que tambien se generan aqui.
    public static class BurnSkinPattern
    {
        private static Texture2D noiseTex;
        private static Texture2D normalTex;

        // Ruido de la quemadura: r manchas grandes, g arrugas finas, b manchas.
        public static Texture2D NoiseTexture
        {
            get
            {
                if (noiseTex == null)
                {
                    BuildTextures();
                }

                return noiseTex;
            }
        }

        // Relieve de las arrugas (rg = normal en espacio tangente).
        public static Texture2D NormalTexture
        {
            get
            {
                if (normalTex == null)
                {
                    BuildTextures();
                }

                return normalTex;
            }
        }

        // centerLocal y dorsumLocal van en el espacio del hueso de la mano (en
        // metros, sin su escala). radius es el radio de la mancha en el dorso.
        public static void Paint(SkinnedMeshRenderer overlay, Transform hand, Vector3 centerLocal,
            Vector3 dorsumLocal, float radius, bool secondDegree, int seed)
        {
            Mesh mesh = overlay.sharedMesh;
            Mesh baked = new Mesh();
            overlay.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            Vector3[] normals = baked.normals;
            Object.Destroy(baked);

            Transform tr = overlay.transform;
            Quaternion toHand = Quaternion.Inverse(hand.rotation);
            Vector3 dorsum = dorsumLocal.normalized;
            Random.State saved = Random.state;
            Random.InitState(seed);
            Vector3 offset = new Vector3(Random.Range(-100f, 100f), Random.Range(-100f, 100f), Random.Range(-100f, 100f));
            Random.state = saved;

            Color[] colors = new Color[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 p = toHand * (tr.TransformPoint(vertices[v]) - hand.position);
                Vector3 n = (toHand * tr.TransformDirection(normals[v])).normalized;

                // Se estira a lo largo de la mano (el agua corre hacia los
                // nudillos y la muneca) y el borde se deforma con ruido.
                Vector3 d = p - centerLocal;
                float dist = new Vector2(d.x, d.y / 1.6f).magnitude / Mathf.Max(0.01f, radius);
                dist *= 1f + 0.6f * (Noise(p * 28f + offset) - 0.5f);

                // Solo donde la piel mira hacia donde cayo el agua.
                float facing = Smooth(-0.3f, 0.45f, Vector3.Dot(n, dorsum));
                float mask = Smooth(0f, 1f, 1.15f - dist) * facing;

                float zone = 0f;
                if (secondDegree)
                {
                    zone = Smooth(0.4f, 0.9f, mask + 0.35f * (Noise(p * 55f + offset * 2f) - 0.5f));
                }

                colors[v] = new Color(mask, zone, Hash(v + seed), 1f);
                uv[v] = new Vector2(p.x, p.y) / 0.035f;
            }

            mesh.colors = colors;
            mesh.uv = uv;
            mesh.RecalculateTangents();
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        // Ruido de valor en 3D (suave, entre 0 y 1).
        private static float Noise(Vector3 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float fx = p.x - ix, fy = p.y - iy, fz = p.z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float Corner(int x, int y, int z) => Hash(x * 73856093 ^ y * 19349663 ^ z * 83492791);
            float x00 = Mathf.Lerp(Corner(ix, iy, iz), Corner(ix + 1, iy, iz), fx);
            float x10 = Mathf.Lerp(Corner(ix, iy + 1, iz), Corner(ix + 1, iy + 1, iz), fx);
            float x01 = Mathf.Lerp(Corner(ix, iy, iz + 1), Corner(ix + 1, iy, iz + 1), fx);
            float x11 = Mathf.Lerp(Corner(ix, iy + 1, iz + 1), Corner(ix + 1, iy + 1, iz + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        // ---------------- texturas ----------------

        private const int Size = 256;

        // Ruido de valor en 2D que se repite cada 'period' celdas (la textura
        // no muestra costuras al repetirse).
        private static float Tile(float x, float y, int period, int salt)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float Corner(int cx, int cy)
            {
                cx = ((cx % period) + period) % period;
                cy = ((cy % period) + period) % period;
                return Hash(cx * 7919 + cy * 104729 + salt * 1299709);
            }

            return Mathf.Lerp(Mathf.Lerp(Corner(ix, iy), Corner(ix + 1, iy), fx),
                Mathf.Lerp(Corner(ix, iy + 1), Corner(ix + 1, iy + 1), fx), fy);
        }

        private static float Fbm(float u, float v, int baseCells, int octaves, int salt)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                sum += Tile(u * cells, v * cells, cells, salt + o) * amp;
                norm += amp;
                amp *= 0.5f;
                cells *= 2;
            }

            return sum / norm;
        }

        private static void BuildTextures()
        {
            float[] height = new float[Size * Size];
            Color32[] noise = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size, v = (float)y / Size;
                    float big = Fbm(u, v, 4, 4, 1);
                    // Arrugas: ruido "en cresta" (lineas finas).
                    float ridge = 1f - Mathf.Abs(Fbm(u, v, 12, 3, 7) * 2f - 1f);
                    float wrinkles = Mathf.Pow(ridge, 3f);
                    float blotch = Smooth(0.38f, 0.68f, Fbm(u, v, 6, 3, 13));
                    noise[y * Size + x] = new Color32((byte)(big * 255f), (byte)(wrinkles * 255f), (byte)(blotch * 255f), 255);
                    height[y * Size + x] = wrinkles * 0.7f + Fbm(u, v, 24, 2, 21) * 0.3f;
                }
            }

            Color32[] normals = new Color32[Size * Size];
            const float strength = 6f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                    float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    normals[y * Size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), 255, 255);
                }
            }

            noiseTex = NewTexture("QuemaduraRuido", noise);
            normalTex = NewTexture("QuemaduraRelieve", normals);
        }

        private static Texture2D NewTexture(string name, Color32[] pixels)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }
    }
}
