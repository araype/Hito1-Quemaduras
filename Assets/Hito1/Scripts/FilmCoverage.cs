using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Cuanto de la quemadura cubren las tiras de film (FilmStrip). Cada tira
    // se pone a lo largo de la mano (de la muneca a los dedos) y cubre una
    // franja del lado donde se apoyo; varias tiras, una al lado de la otra,
    // cubren la quemadura. La zona a cubrir es la mascara de BurnSkinOverlay
    // (lo que se ve quemado), asi una quemadura mas grande pide mas tiras.
    // Si la tira va atravesada (envolviendo la mano) no se pone: es un error,
    // porque al hincharse la mano el film aprieta (NHS, Cruz Roja: "lay it
    // lengthways, don't wrap it around").
    // Se agrega sola al paciente la primera vez que se pone una tira.
    public class FilmCoverage : MonoBehaviour
    {
        [Tooltip("Parte de la quemadura que debe quedar cubierta para dar el paso por hecho.")]
        [Range(0.3f, 1f)] public float coverageNeeded = 0.8f;
        [Tooltip("Coseno minimo entre la tira y el largo de la mano para contar como 'a lo largo'.")]
        [Range(0f, 1f)] public float lengthwiseMin = 0.6f;
        [Tooltip("Margen a lo largo de la tira (m): cubre un poco mas alla de sus extremos.")]
        public float lengthMargin = 0.03f;
        [Tooltip("Valor minimo de la mascara de la quemadura (canal rojo de BurnSkinPattern) para contar como zona quemada.")]
        [Range(0f, 1f)] public float burnAlphaMin = 0.3f;

        public float Coverage { get; private set; }
        public int Strips { get; private set; }

        // Capa de la ultima tira puesta (FilmStrip la hace aparecer de a poco
        // mientras la tira baja a la piel).
        public Renderer LastLayer { get; private set; }

        // Ya se conto el error de envolver la mano (se cuenta una vez).
        public bool WrapReported { get; set; }

        [Tooltip("Segundos que se ve el film envuelto alrededor de la mano antes de retirarse.")]
        public float wrapVisibleSeconds = 6f;

        private HandFrame frame;
        private PatientBody patient;
        private readonly List<GameObject> layers = new List<GameObject>();
        private readonly List<System.Func<Vector3, Vector3, bool>> strips = new List<System.Func<Vector3, Vector3, bool>>();

        public static FilmCoverage For(PatientBody patient)
        {
            if (patient == null)
            {
                return null;
            }

            FilmCoverage coverage = patient.GetComponent<FilmCoverage>();
            return coverage != null ? coverage : patient.gameObject.AddComponent<FilmCoverage>();
        }

        // La mano quemada (o null si la paciente no esta lista).
        public Transform Hand
        {
            get
            {
                PatientBody p = Patient;
                return p != null && p.IsReady ? p.HandBone(true) : null;
            }
        }

        private PatientBody Patient
        {
            get
            {
                if (patient == null)
                {
                    patient = GetComponent<PatientBody>();
                }

                return patient;
            }
        }

        private bool Ready()
        {
            if (frame == null)
            {
                frame = HandFrame.Build(this, true, 0.45f, "Body");
                if (frame != null)
                {
                    Debug.Log($"[FilmCoverage] Mano: largo {frame.HalfLength * 2f / frame.UnitsPerMeter:F3} m, "
                        + $"ancho {frame.HalfWidth * 2f / frame.UnitsPerMeter:F3} m");
                }
            }

            return frame != null;
        }

        // Pone una tira con centro y direccion (mundo). Devuelve false si no
        // se pudo; 'wrapped' es true si iba atravesada.
        public bool Apply(Vector3 center, Vector3 direction, float width, float length, out bool wrapped)
        {
            wrapped = false;
            if (!Ready())
            {
                return false;
            }

            Vector3 dir = frame.DirToBind(direction);
            if (Mathf.Abs(Vector3.Dot(dir, frame.Long)) < lengthwiseMin)
            {
                wrapped = true;
                return false;
            }

            float units = frame.UnitsPerMeter;
            Vector3 c = frame.ToBind(center) - frame.Center;
            float wideAt = Vector3.Dot(c, frame.Wide);
            float longAt = Vector3.Dot(c, frame.Long);
            float halfWidth = width * 0.5f * units;
            float halfLength = (length * 0.5f + lengthMargin) * units;

            // Lado de la mano donde se apoyo (dorso o palma).
            float side = Mathf.Sign(Vector3.Dot(c, frame.Thick));
            if (Mathf.Abs(Vector3.Dot(c, frame.Thick)) < 1e-6f)
            {
                side = 1f;
            }

            Vector3 thick = frame.Thick * side;
            Vector3 hc = frame.Center;
            Vector3 longAxis = frame.Long;
            Vector3 wideAxis = frame.Wide;
            System.Func<Vector3, Vector3, bool> inStrip = (p, n) =>
            {
                Vector3 d = p - hc;
                return Mathf.Abs(Vector3.Dot(d, wideAxis) - wideAt) <= halfWidth
                    && Mathf.Abs(Vector3.Dot(d, longAxis) - longAt) <= halfLength
                    && Vector3.Dot(n, thick) > -0.2f;
            };

            // La capa visible: cada tira un poco mas afuera que la anterior.
            SkinnedMeshRenderer layer = HandSkinMesh.Build(Patient, true, 0.45f, 0.0035f + 0.0006f * Strips, "Body",
                "FilmTira" + (Strips + 1), "Film_Mano", inStrip);
            if (layer == null)
            {
                return false;
            }

            layers.Add(layer.gameObject);
            LastLayer = layer;
            strips.Add(inStrip);
            Strips++;
            Measure();
            Debug.Log($"[FilmCoverage] Tira {Strips}: cubierto {Coverage:P0}");
            return true;
        }

        // Centro y eje largo de la mano en el mundo (para medir el gesto de
        // envolver). False si la mano aun no esta lista.
        public bool WorldAxis(out Vector3 center, out Vector3 longAxis, out float halfLength)
        {
            center = Vector3.zero;
            longAxis = Vector3.forward;
            halfLength = 0f;
            if (!Ready())
            {
                return false;
            }

            Matrix4x4 toWorld = frame.WorldToBind.inverse;
            center = toWorld.MultiplyPoint3x4(frame.Center);
            longAxis = toWorld.MultiplyVector(frame.Long).normalized;
            halfLength = frame.HalfLength / frame.UnitsPerMeter;
            return true;
        }

        // Film envuelto alrededor de la mano: una banda de 'width' que da la
        // vuelta completa (dorso y palma) a la altura de 'worldPoint'. No
        // cuenta para cubrir (es el error de envolver); se ve unos segundos y
        // se retira, como si el jugador lo quitara tras la correccion.
        public bool Wrap(Vector3 worldPoint, float width)
        {
            if (!Ready())
            {
                return false;
            }

            float units = frame.UnitsPerMeter;
            Vector3 hc = frame.Center;
            Vector3 longAxis = frame.Long;
            float longAt = Mathf.Clamp(Vector3.Dot(frame.ToBind(worldPoint) - hc, longAxis),
                -frame.HalfLength * 0.8f, frame.HalfLength * 0.8f);
            float halfWidth = width * 0.5f * units;
            System.Func<Vector3, Vector3, bool> inBand = (p, n) =>
                Mathf.Abs(Vector3.Dot(p - hc, longAxis) - longAt) <= halfWidth;

            // Un poco mas afuera que las tiras, para que se vea por encima.
            SkinnedMeshRenderer band = HandSkinMesh.Build(Patient, true, 0.45f, 0.006f, "Body", "FilmEnvuelto",
                "Film_Mano", inBand);
            if (band == null)
            {
                return false;
            }

            StartCoroutine(RemoveAfter(band, wrapVisibleSeconds));
            Debug.Log("[FilmCoverage] Film envuelto alrededor de la mano (error).");
            return true;
        }

        private System.Collections.IEnumerator RemoveAfter(SkinnedMeshRenderer band, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (band != null)
            {
                if (band.sharedMesh != null)
                {
                    Destroy(band.sharedMesh);
                }

                Destroy(band.gameObject);
            }
        }

        // Parte de la quemadura (capa de BurnSkinOverlay, en el mismo espacio
        // que la piel) que tapan las tiras puestas. Se lee la mascara cada vez
        // porque cambia cuando la quemadura toma la forma de la pose.
        private void Measure()
        {
            BurnSkinOverlay overlay = GetComponent<BurnSkinOverlay>();
            Mesh mesh = overlay != null && overlay.Overlay != null ? overlay.Overlay.sharedMesh : null;
            if (mesh == null)
            {
                // Sin mascara: cada tira cuenta un tercio.
                Coverage = Mathf.Clamp01(Strips / 3f);
                return;
            }

            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            Color[] col = mesh.colors;
            int[] t = mesh.triangles;
            float total = 0f, done = 0f;
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                // BurnSkinPattern guarda la mancha en el rojo (el alfa es un borde
                // mas ancho, solo para el shader).
                float alpha = col.Length == v.Length ? (col[a].r + col[b].r + col[c].r) / 3f : 1f;
                if (alpha < burnAlphaMin)
                {
                    continue;
                }

                float weight = Vector3.Cross(v[b] - v[a], v[c] - v[a]).magnitude * 0.5f * alpha;
                Vector3 center = (v[a] + v[b] + v[c]) / 3f;
                Vector3 normal = n.Length == v.Length ? (n[a] + n[b] + n[c]).normalized : Vector3.zero;
                total += weight;
                foreach (System.Func<Vector3, Vector3, bool> strip in strips)
                {
                    if (strip(center, normal))
                    {
                        done += weight;
                        break;
                    }
                }
            }

            Coverage = total > 0f ? done / total : 1f;
        }

        private void OnDestroy()
        {
            foreach (GameObject layer in layers)
            {
                if (layer != null)
                {
                    SkinnedMeshRenderer r = layer.GetComponent<SkinnedMeshRenderer>();
                    if (r != null && r.sharedMesh != null)
                    {
                        Destroy(r.sharedMesh);
                    }
                }
            }
        }
    }
}
