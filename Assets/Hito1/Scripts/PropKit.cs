using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Utilidades para armar en runtime los objetos de la escena de quemadura
    // (film, gasa, chompa, remedios caseros...) sin assets externos: piezas
    // con primitivas de Unity, un collider de caja y un Rigidbody que
    // SimpleGrab puede agarrar. Tambien busca un lugar libre sobre el meson
    // para dejarlos.
    public static class PropKit
    {
        private static readonly Dictionary<string, Material> Fallbacks = new Dictionary<string, Material>();

        // Raiz agarrable: Rigidbody + Grabbable + collider de caja del tamano
        // dado (con la base en el origen del objeto).
        public static GameObject Grabbable(string name, Vector3 position, Quaternion rotation, Vector3 size,
            float mass)
        {
            GameObject root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<Grabbable>();
            return root;
        }

        // Pieza visual sin collider.
        public static Renderer Part(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale,
            Quaternion localRotation, string materialName, Color fallback)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = materialName;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(materialName, fallback);
            return renderer;
        }

        public static Renderer Part(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale,
            string materialName, Color fallback)
        {
            return Part(parent, type, localPosition, localScale, Quaternion.identity, materialName, fallback);
        }

        public static Renderer MeshPart(Transform parent, Mesh mesh, Vector3 localPosition, Quaternion localRotation,
            string materialName, Color fallback)
        {
            GameObject go = new GameObject(mesh.name);
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat(materialName, fallback);
            return renderer;
        }

        // Material de Resources/Hito1FX; si falta, uno Standard del color dado
        // (para que la escena no quede rosada).
        public static Material Mat(string name, Color fallback)
        {
            Material material = Resources.Load<Material>("Hito1FX/" + name);
            if (material != null)
            {
                return material;
            }

            if (!Fallbacks.TryGetValue(name, out material))
            {
                Debug.LogWarning("[PropKit] Falta el material Resources/Hito1FX/" + name + "; se usa un color plano.");
                material = new Material(Shader.Find("Standard")) { name = name, color = fallback };
                Fallbacks[name] = material;
            }

            return material;
        }

        // Anillo (toro) alrededor del eje Y local.
        public static Mesh Torus(string name, float ringRadius, float tubeRadius, int segments = 28, int sides = 10)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int s = 0; s <= sides; s++)
                {
                    float b = s / (float)sides * Mathf.PI * 2f;
                    Vector3 normal = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v.Add(dir * ringRadius + normal * tubeRadius);
                    n.Add(normal);
                }
            }

            for (int i = 0; i < segments; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s;
                    int b = a + sides + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(b); t.Add(a + 1); t.Add(b + 1);
                }
            }

            return Finish(name, v, n, t);
        }

        // Ajusta el orden de los triangulos para que miren hacia sus normales.
        public static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<int> t)
        {
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
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Punto libre sobre una superficie horizontal a la altura indicada
        // (por ejemplo, el meson), entre minRadius y maxRadius de 'center'.
        // Evita los puntos ya usados y el quemador.
        public static bool FindSurfaceSpot(Vector3 center, float surfaceHeight, float minRadius, float maxRadius,
            float clearance, List<Vector3> taken, Vector3 avoid, float avoidRadius, out Vector3 spot)
        {
            const int rings = 6;
            const int perRing = 16;
            for (int r = 0; r < rings; r++)
            {
                float radius = Mathf.Lerp(minRadius, maxRadius, r / (float)(rings - 1));
                for (int k = 0; k < perRing; k++)
                {
                    float a = (k + (r % 2) * 0.5f) / perRing * Mathf.PI * 2f;
                    Vector3 probe = new Vector3(center.x + Mathf.Cos(a) * radius, surfaceHeight + 0.35f,
                        center.z + Mathf.Sin(a) * radius);
                    if (!SurfaceAt(probe, surfaceHeight, out Vector3 hit))
                    {
                        continue;
                    }

                    // Que haya meson alrededor y no solo debajo: un objeto en
                    // el borde se vuelca (la taza del tutorial caia al suelo).
                    if (!Supported(hit, Mathf.Min(clearance, 0.05f)))
                    {
                        continue;
                    }

                    if (Flat(hit - avoid).magnitude < avoidRadius || TooClose(hit, taken, clearance * 2f))
                    {
                        continue;
                    }

                    // Que no haya nada encima (otro objeto, una pared).
                    if (Physics.CheckSphere(hit + Vector3.up * (clearance + 0.01f), clearance, ~0,
                        QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    spot = hit;
                    taken?.Add(hit);
                    return true;
                }
            }

            spot = center;
            return false;
        }

        private static bool Supported(Vector3 point, float margin)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Vector3 probe = point + new Vector3(Mathf.Cos(a) * margin, 0.1f, Mathf.Sin(a) * margin);
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 0.16f, ~0, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(hit.point.y - point.y) > 0.01f)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SurfaceAt(Vector3 probe, float surfaceHeight, out Vector3 point)
        {
            point = probe;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 0.7f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Superficie plana, a la altura del meson y que no sea un objeto
            // suelto (la olla, el pano...).
            if (hit.normal.y < 0.9f || Mathf.Abs(hit.point.y - surfaceHeight) > 0.06f
                || (hit.rigidbody != null && !hit.rigidbody.isKinematic))
            {
                return false;
            }

            point = hit.point;
            return true;
        }

        private static bool TooClose(Vector3 p, List<Vector3> taken, float distance)
        {
            if (taken == null)
            {
                return false;
            }

            foreach (Vector3 q in taken)
            {
                if (Flat(p - q).magnitude < distance)
                {
                    return true;
                }
            }

            return false;
        }

        public static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        // Transform hijo del hueso cuyo nombre contiene el texto (busqueda en
        // profundidad).
        public static Transform FindChild(Transform root, string contains)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Contains(contains))
                {
                    return t;
                }
            }

            return null;
        }
    }
}
