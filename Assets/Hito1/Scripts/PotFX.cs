using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Convierte el objeto provisional de la olla (una pila de bowls del kit)
    // en una olla de acero con mangos, agua hirviendo, vapor, la llama del
    // quemador y el sonido del hervor. Solo cambia lo visual y el sonido: los
    // colliders, el Rigidbody y el agarre (con sus poses de mano) quedan
    // como estaban, y la olla se arma con el tamano de esos colliders para
    // que la mano siga cayendo sobre el borde.
    public class PotFX : MonoBehaviour
    {
        [Header("Forma")]
        [Tooltip("Margen entre la olla y el borde de su collider (m).")]
        public float margin = 0.006f;
        public float wallThickness = 0.005f;
        [Range(0f, 1f)] public float waterLevel = 0.62f;

        [Header("Efectos")]
        public bool steam = true;
        public bool burnerFlame = true;
        public float boilVolume = 0.45f;

        private const int Segments = 36;

        private Transform visual;
        private ParticleSystem steamSystem;
        private Renderer flameRenderer;
        private MaterialPropertyBlock flameBlock;
        private AudioSource boil;
        private Vector3 burnerPosition;
        private float radius, height;

        private void Start()
        {
            Bounds local;
            if (!LocalColliderBounds(out local))
            {
                Debug.LogWarning("[PotFX] La olla no tiene colliders para tomar su tamano.", this);
                enabled = false;
                return;
            }

            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = false;
            }

            // Hijo sin la escala (no uniforme) del objeto: la olla se arma en
            // metros y queda redonda.
            GameObject go = new GameObject("OllaVisual");
            go.AddComponent<NoAutoCollider>();
            visual = go.transform;
            visual.SetParent(transform, false);
            visual.localPosition = new Vector3(local.center.x, local.min.y, local.center.z);
            Vector3 s = transform.lossyScale;
            visual.localScale = new Vector3(1f / Mathf.Abs(s.x), 1f / Mathf.Abs(s.y), 1f / Mathf.Abs(s.z));

            Vector3 world = Vector3.Scale(local.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            bool handlesOnZ = world.z >= world.x;
            radius = Mathf.Min(world.x, world.z) * 0.5f - margin;
            height = world.y - margin;
            float handleReach = Mathf.Max(0.02f, (Mathf.Max(world.x, world.z) * 0.5f - margin) - radius);

            AddPart("Cuerpo", BuildBody(radius, height, wallThickness), "Acero");
            AddPart("Mangos", BuildHandles(radius, height * 0.82f, handleReach, handlesOnZ), "Mango");
            AddPart("Agua", BuildDisc(radius - wallThickness - 0.001f, height * waterLevel), "Agua_Superficie");

            burnerPosition = visual.position + Vector3.down * 0.004f;
            if (burnerFlame)
            {
                BuildFlame(radius);
            }

            if (steam)
            {
                BuildSteam();
            }

            GameObject audio = new GameObject("SonidoHervor");
            audio.transform.SetParent(visual, false);
            audio.transform.localPosition = Vector3.up * height;
            boil = Hito1FX.CreateSource(audio, "olla_hervor_loop", boilVolume, true, 0.3f, 5f);
            if (boil.clip != null)
            {
                boil.time = Random.Range(0f, boil.clip.length * 0.9f);
                boil.Play();
            }
        }

        private void Update()
        {
            // La olla esta caliente mientras sigue sobre el quemador: si se la
            // aleja, el hervor y el vapor bajan.
            float onBurner = visual != null
                ? 1f - Mathf.Clamp01((Vector3.Distance(visual.position, burnerPosition) - 0.05f) / 0.3f)
                : 0f;

            if (boil != null)
            {
                boil.volume = Mathf.MoveTowards(boil.volume, boilVolume * Mathf.Lerp(0.25f, 1f, onBurner),
                    Time.deltaTime);
            }

            if (steamSystem != null)
            {
                ParticleSystem.EmissionModule emission = steamSystem.emission;
                emission.rateOverTime = Mathf.Lerp(1.5f, 7f, onBurner);
            }

            if (flameRenderer != null)
            {
                // Parpadeo suave de la llama.
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 9f, 0.3f);
                flameBlock.SetColor("_EmissionColor", new Color(0.35f, 0.6f, 2.2f) * flicker);
                flameRenderer.SetPropertyBlock(flameBlock);
            }
        }

        private bool LocalColliderBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                if (col.isTrigger)
                {
                    continue;
                }

                Bounds b;
                BoxCollider boxCol = col as BoxCollider;
                if (boxCol != null && boxCol.transform == transform)
                {
                    b = new Bounds(boxCol.center, boxCol.size);
                }
                else
                {
                    Bounds w = col.bounds;
                    b = new Bounds(transform.InverseTransformPoint(w.center), Vector3.zero);
                    b.Encapsulate(transform.InverseTransformPoint(w.min));
                    b.Encapsulate(transform.InverseTransformPoint(w.max));
                }

                if (!any)
                {
                    bounds = b;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return any;
        }

        private void AddPart(string partName, Mesh mesh, string materialName)
        {
            GameObject go = new GameObject(partName);
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(visual, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Material material = Hito1FX.Mat(materialName);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            if (materialName == "Agua_Superficie")
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ---------------- mallas ----------------

        // Cuerpo por revolucion de un perfil (radio, altura): fondo, pared
        // exterior, borde enrollado, pared interior y fondo interior.
        private static Mesh BuildBody(float r, float h, float wall)
        {
            Vector2[] profile =
            {
                new Vector2(0f, 0f),
                new Vector2(r - 0.012f, 0f),
                new Vector2(r - 0.002f, 0.004f),
                new Vector2(r, 0.014f),
                new Vector2(r, h - 0.004f),
                new Vector2(r + 0.004f, h),
                new Vector2(r - wall * 0.5f, h + 0.003f),
                new Vector2(r - wall, h - 0.003f),
                new Vector2(r - wall, wall + 0.01f),
                new Vector2(r - wall - 0.01f, wall),
                new Vector2(0f, wall)
            };
            return Lathe(profile, Segments);
        }

        private static Mesh Lathe(Vector2[] profile, int segments)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int p = 0; p < profile.Length - 1; p++)
            {
                Vector2 a = profile[p];
                Vector2 b = profile[p + 1];
                Vector2 dir = (b - a).normalized;
                // El perfil va hacia afuera por abajo y vuelve por dentro: la
                // normal es el giro a la derecha del sentido de avance.
                Vector2 normal2 = new Vector2(dir.y, -dir.x);
                int start = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float angle = s / (float)segments * Mathf.PI * 2f;
                    float c = Mathf.Cos(angle);
                    float sn = Mathf.Sin(angle);
                    Vector3 nn = new Vector3(normal2.x * c, normal2.y, normal2.x * sn);
                    v.Add(new Vector3(a.x * c, a.y, a.x * sn));
                    v.Add(new Vector3(b.x * c, b.y, b.x * sn));
                    n.Add(nn);
                    n.Add(nn);
                    uv.Add(new Vector2(s / (float)segments, a.y));
                    uv.Add(new Vector2(s / (float)segments, b.y));
                }

                for (int s = 0; s < segments; s++)
                {
                    int i0 = start + s * 2;
                    int i1 = i0 + 1;
                    int i2 = i0 + 2;
                    int i3 = i0 + 3;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i2); tris.Add(i1); tris.Add(i3);
                }
            }

            Mesh mesh = new Mesh { name = "OllaCuerpo" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            FixWinding(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Ajusta el orden de los triangulos para que sus caras miren hacia
        // donde apuntan las normales (asi no depende de la convencion de giro).
        private static void FixWinding(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Vector3 avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(face, avg) < 0f)
                {
                    int tmp = t[i + 1];
                    t[i + 1] = t[i + 2];
                    t[i + 2] = tmp;
                }
            }

            mesh.triangles = t;
        }

        // Tubo a lo largo de un camino (mangos, anillo de la llama).
        private static void Tube(List<Vector3> path, float tubeRadius, bool closed, List<Vector3> v,
            List<Vector3> n, List<int> tris)
        {
            const int sides = 8;
            int start = v.Count;
            int rings = path.Count;
            for (int i = 0; i < rings; i++)
            {
                Vector3 prevP = path[closed ? (i - 1 + rings) % rings : Mathf.Max(i - 1, 0)];
                Vector3 nextP = path[closed ? (i + 1) % rings : Mathf.Min(i + 1, rings - 1)];
                Vector3 tangent = (nextP - prevP).normalized;
                Vector3 side = Vector3.Cross(tangent, Vector3.up);
                if (side.sqrMagnitude < 1e-4f)
                {
                    side = Vector3.Cross(tangent, Vector3.right);
                }

                side.Normalize();
                Vector3 up = Vector3.Cross(side, tangent);
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    v.Add(path[i] + dir * tubeRadius);
                    n.Add(dir);
                }
            }

            int segs = closed ? rings : rings - 1;
            for (int i = 0; i < segs; i++)
            {
                int i2 = (i + 1) % rings;
                for (int s = 0; s < sides; s++)
                {
                    int s2 = (s + 1) % sides;
                    int a = start + i * sides + s;
                    int b = start + i * sides + s2;
                    int c = start + i2 * sides + s;
                    int d = start + i2 * sides + s2;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
        }

        private static Mesh BuildHandles(float r, float y, float reach, bool onZ)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                Vector3 outward = (onZ ? Vector3.forward : Vector3.right) * sideSign;
                Vector3 along = onZ ? Vector3.right : Vector3.forward;
                List<Vector3> path = new List<Vector3>();
                float halfWidth = Mathf.Min(0.045f, r * 0.45f);
                for (int i = 0; i <= 12; i++)
                {
                    float a = i / 12f * Mathf.PI;
                    Vector3 p = outward * (r - 0.004f + Mathf.Sin(a) * reach) + along * (Mathf.Cos(a) * halfWidth);
                    p.y = y;
                    path.Add(p);
                }

                Tube(path, 0.0065f, false, v, n, tris);
            }

            return Finish("OllaMangos", v, n, tris);
        }

        private static Mesh BuildDisc(float r, float y)
        {
            List<Vector3> v = new List<Vector3> { new Vector3(0f, y, 0f) };
            List<Vector3> n = new List<Vector3> { Vector3.up };
            List<int> tris = new List<int>();
            for (int s = 0; s <= Segments; s++)
            {
                float a = s / (float)Segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                n.Add(Vector3.up);
                if (s > 0)
                {
                    tris.Add(0); tris.Add(s + 1); tris.Add(s);
                }
            }

            return Finish("OllaAgua", v, n, tris);
        }

        private static Mesh Finish(string meshName, List<Vector3> v, List<Vector3> n, List<int> tris)
        {
            Mesh mesh = new Mesh { name = meshName };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tris, 0);
            FixWinding(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------- llama y vapor ----------------

        private void BuildFlame(float potRadius)
        {
            // Queda en el quemador (no se mueve con la olla).
            GameObject go = new GameObject("LlamaQuemador");
            go.AddComponent<NoAutoCollider>();
            go.transform.position = burnerPosition;

            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> tris = new List<int>();
            float ring = Mathf.Min(0.065f, potRadius * 0.55f);
            const int tongues = 18;
            for (int i = 0; i < tongues; i++)
            {
                // Lenguas cortas que salen del anillo hacia afuera y arriba.
                float a = i / (float)tongues * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                List<Vector3> path = new List<Vector3>
                {
                    dir * ring,
                    dir * (ring + 0.008f) + Vector3.up * 0.006f,
                    dir * (ring + 0.013f) + Vector3.up * 0.011f
                };
                Tube(path, 0.0028f, false, v, n, tris);
            }

            go.AddComponent<MeshFilter>().sharedMesh = Finish("Llama", v, n, tris);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = Hito1FX.Mat("Llama");
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            flameRenderer = renderer;
            flameBlock = new MaterialPropertyBlock();
        }

        private void BuildSteam()
        {
            GameObject go = new GameObject("Vapor");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(visual, false);
            go.transform.localPosition = Vector3.up * (height * waterLevel + 0.01f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            steamSystem = go.AddComponent<ParticleSystem>();
            steamSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = steamSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.006f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = steamSystem.emission;
            emission.rateOverTime = 7f;

            ParticleSystem.ShapeModule shape = steamSystem.shape;
            // Cono casi vertical (el objeto esta girado para que su eje Z
            // apunte hacia arriba): el vapor sale de toda la superficie.
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = radius * 0.75f;

            ParticleSystem.SizeOverLifetimeModule size = steamSystem.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.6f));

            ParticleSystem.RotationOverLifetimeModule rotation = steamSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystem.ColorOverLifetimeModule color = steamSystem.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.NoiseModule noise = steamSystem.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 10f;
            Material material = Hito1FX.Mat("Vapor");
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            steamSystem.Play();
        }
    }
}
