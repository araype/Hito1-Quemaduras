using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Cajas y cilindros con los bordes redondeados para las piezas de
    // PropKit.Part. Las primitivas de Unity tienen aristas filosas que no
    // reflejan luz y delatan que los objetos (chompa doblada, mantequilla,
    // celular, botellas...) son cubos: con un bisel pequeno los bordes atrapan
    // la luz como los objetos reales. La malla se arma ya del tamano pedido
    // (el bisel no se deforma con la escala) y se reutiliza entre piezas
    // iguales.
    public static class RoundedMeshes
    {
        private const float MaxBevel = 0.01f;
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        // Cambia la malla de una primitiva recien creada (escala = tamano) por
        // una con bisel y deja la escala en 1. Devuelve false si no aplica
        // (tipo no soportado o pieza demasiado delgada).
        public static bool Apply(GameObject go, PrimitiveType type, Vector3 size)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter == null)
            {
                return false;
            }

            Vector3 s = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            Mesh mesh = null;
            if (type == PrimitiveType.Cube)
            {
                float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                if (min < 0.004f)
                {
                    return false;
                }

                mesh = Box(s, Mathf.Min(MaxBevel, min * 0.22f));
            }
            else if (type == PrimitiveType.Cylinder)
            {
                // Cilindro de Unity: diametro = escala x/z, alto = 2 * escala y.
                float rx = s.x * 0.5f, rz = s.z * 0.5f, half = s.y;
                float min = Mathf.Min(Mathf.Min(rx, rz), half);
                if (min < 0.002f)
                {
                    return false;
                }

                mesh = Cylinder(rx, rz, half, Mathf.Min(MaxBevel * 0.8f, min * 0.3f));
            }

            if (mesh == null)
            {
                return false;
            }

            filter.sharedMesh = mesh;
            go.transform.localScale = new Vector3(Mathf.Sign(size.x == 0f ? 1f : size.x),
                Mathf.Sign(size.y == 0f ? 1f : size.y), Mathf.Sign(size.z == 0f ? 1f : size.z));
            return true;
        }

        private static string Key(string kind, params float[] values)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(kind);
            foreach (float v in values)
            {
                sb.Append('_').Append(Mathf.RoundToInt(v * 10000f));
            }

            return sb.ToString();
        }

        // Coordenadas de un eje: tramo plano y un arco de 'steps' pasos en
        // cada extremo.
        private static List<float> Axis(float half, float r, int steps)
        {
            List<float> c = new List<float>();
            float inner = half - r;
            for (int j = steps; j >= 1; j--)
            {
                c.Add(-inner - r * Mathf.Sin(j / (float)steps * Mathf.PI * 0.5f));
            }

            c.Add(-inner);
            if (inner > 1e-5f)
            {
                c.Add(inner);
            }

            for (int j = 1; j <= steps; j++)
            {
                c.Add(inner + r * Mathf.Sin(j / (float)steps * Mathf.PI * 0.5f));
            }

            return c;
        }

        public static Mesh Box(Vector3 size, float r)
        {
            string key = Key("Caja", size.x, size.y, size.z, r);
            Mesh cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            Vector3 e = size * 0.5f;
            Vector3 inner = new Vector3(e.x - r, e.y - r, e.z - r);
            const int steps = 3;
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> t = new List<int>();

            // 6 caras: eje normal y los dos ejes del plano.
            int[,] faces = { { 0, 1, 2 }, { 1, 2, 0 }, { 2, 0, 1 } };
            for (int f = 0; f < 3; f++)
            {
                int a = faces[f, 0], b = faces[f, 1], c = faces[f, 2];
                List<float> cb = Axis(e[b], r, steps);
                List<float> cc = Axis(e[c], r, steps);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int start = v.Count;
                    for (int i = 0; i < cb.Count; i++)
                    {
                        for (int j = 0; j < cc.Count; j++)
                        {
                            Vector3 p = Vector3.zero;
                            p[a] = e[a] * sign;
                            p[b] = cb[i];
                            p[c] = cc[j];
                            Vector3 core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 dir = p - core;
                            Vector3 normal = dir.sqrMagnitude > 1e-12f ? dir.normalized : Vector3.zero;
                            if (normal == Vector3.zero)
                            {
                                normal[a] = sign;
                            }

                            v.Add(core + normal * r);
                            n.Add(normal);
                            uv.Add(new Vector2((cb[i] + e[b]) / (2f * e[b]), (cc[j] + e[c]) / (2f * e[c])));
                        }
                    }

                    int cols = cc.Count;
                    for (int i = 0; i < cb.Count - 1; i++)
                    {
                        for (int j = 0; j < cols - 1; j++)
                        {
                            int q = start + i * cols + j;
                            t.Add(q); t.Add(q + 1); t.Add(q + cols);
                            t.Add(q + 1); t.Add(q + cols + 1); t.Add(q + cols);
                        }
                    }
                }
            }

            Mesh mesh = Finish("CajaRedondeada", v, n, uv, t);
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh Cylinder(float rx, float rz, float half, float r)
        {
            string key = Key("Cilindro", rx, rz, half, r);
            Mesh cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            const int sides = 32;
            const int steps = 3;
            // Perfil (radio relativo, altura) de abajo hacia arriba, con su
            // normal en 2D (hacia afuera, hacia arriba).
            List<Vector2> prof = new List<Vector2>();
            List<Vector2> pn = new List<Vector2>();
            float rr = Mathf.Min(rx, rz);
            float inR = rr - r;
            prof.Add(new Vector2(0f, -half)); pn.Add(new Vector2(0f, -1f));
            for (int j = 0; j <= steps; j++)
            {
                float a = j / (float)steps * Mathf.PI * 0.5f;
                prof.Add(new Vector2(inR + r * Mathf.Sin(a), -half + r - r * Mathf.Cos(a)));
                pn.Add(new Vector2(Mathf.Sin(a), -Mathf.Cos(a)));
            }

            for (int j = 0; j <= steps; j++)
            {
                float a = j / (float)steps * Mathf.PI * 0.5f;
                prof.Add(new Vector2(inR + r * Mathf.Cos(a), half - r + r * Mathf.Sin(a)));
                pn.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }

            prof.Add(new Vector2(0f, half)); pn.Add(new Vector2(0f, 1f));

            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> t = new List<int>();
            float sx = rx / rr, sz = rz / rr;
            for (int s = 0; s <= sides; s++)
            {
                float ang = s / (float)sides * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                for (int k = 0; k < prof.Count; k++)
                {
                    v.Add(new Vector3(cx * prof[k].x * sx, prof[k].y, cz * prof[k].x * sz));
                    n.Add(new Vector3(cx * pn[k].x / sx, pn[k].y, cz * pn[k].x / sz).normalized);
                    uv.Add(new Vector2(s / (float)sides, k / (float)(prof.Count - 1)));
                }
            }

            int rows = prof.Count;
            for (int s = 0; s < sides; s++)
            {
                for (int k = 0; k < rows - 1; k++)
                {
                    int q = s * rows + k;
                    t.Add(q); t.Add(q + rows); t.Add(q + 1);
                    t.Add(q + 1); t.Add(q + rows); t.Add(q + rows + 1);
                }
            }

            Mesh mesh = Finish("CilindroRedondeado", v, n, uv, t);
            Cache[key] = mesh;
            return mesh;
        }

        private static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            // Triangulos mirando hacia sus normales.
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }

            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
