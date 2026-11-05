using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Chompa doblada para abrigar a la paciente mientras se le enfria la mano
    // (el agua fria puede enfriar el resto del cuerpo). Se agarra y se suelta
    // sobre su espalda u hombros: queda puesta sobre los hombros, con las
    // mangas colgando por delante, y se mueve con ella.
    [RequireComponent(typeof(Rigidbody))]
    public class SweaterItem : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Si se suelta a menos de esta distancia del pecho o la espalda, queda puesta.")]
        public float attachDistance = 0.4f;
        public string materialName = "Lana";
        public Color fallbackColor = new Color(0.55f, 0.18f, 0.2f);

        public bool IsOn { get; private set; }

        private Grabbable grabbable;
        private bool wasHeld;

        private void Start()
        {
            grabbable = GetComponent<Grabbable>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }
        }

        private void Update()
        {
            bool held = grabbable != null && grabbable.IsHeld;
            if (wasHeld && !held && !IsOn)
            {
                TryPutOn();
            }

            wasHeld = held;
        }

        private void TryPutOn()
        {
            PatientBody patient = FindObjectOfType<PatientBody>();
            if (patient == null || !patient.IsReady || patient.Chest == null)
            {
                return;
            }

            if (Vector3.Distance(transform.position, patient.Chest.position) > attachDistance)
            {
                return;
            }

            IsOn = true;
            BuildDrape(patient);
            Hito1FX.PlayAt("tela_roce_1", patient.Chest.position, 0.8f);
            if (burnSequence != null && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.SweaterOn);
            }

            Destroy(gameObject);
        }

        // Media capa alrededor de la espalda, desde un hombro al otro, que
        // pasa por encima de los hombros; y las dos mangas colgando adelante.
        // Se arma en el mundo con la postura actual y se cuelga del pecho.
        private void BuildDrape(PatientBody patient)
        {
            Transform chest = patient.Chest;
            Vector3 up = patient.TorsoUp;
            Vector3 forward = patient.TorsoForward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 left = patient.ShoulderPosition(false);
            Vector3 rightShoulder = patient.ShoulderPosition(true);
            Vector3 center = (left + rightShoulder) * 0.5f;
            // Ancha y honda para que entren los brazos y la espalda (antes se
            // veian los hombros azules a traves de la lana).
            float shoulderHalf = Vector3.Distance(left, rightShoulder) * 0.5f;
            float halfWidth = Mathf.Max(0.15f, shoulderHalf + 0.065f);
            const float depth = 0.14f;
            const float drop = 0.36f;
            // Arranca por encima de los hombros: si no, asomaban por arriba.
            Vector3 top = center + up * 0.06f;

            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> t = new List<int>();
            List<Vector2> uv = new List<Vector2>();
            // Malla mas fina para que se vean los pliegues.
            const int segments = 40;
            const int rows = 10;

            // Espalda: de -100 a 100 grados alrededor (0 = atras).
            for (int r = 0; r <= rows; r++)
            {
                float h = r / (float)rows;
                // Mas angosta arriba (sobre los hombros); al caer se pega a la
                // espalda en vez de abrirse como un cilindro.
                float widen = Mathf.Lerp(0.92f, 1f, h);
                float cling = Mathf.Lerp(1f, 0.92f, h);
                for (int s = 0; s <= segments; s++)
                {
                    float a = Mathf.Lerp(-100f, 100f, s / (float)segments) * Mathf.Deg2Rad;
                    // Pliegues verticales que crecen hacia abajo y un borde
                    // inferior disparejo, como tela que cuelga.
                    float fold = Mathf.Sin(a * 7f + 0.6f) * 0.5f + Mathf.Sin(a * 13f + 1.7f) * 0.25f;
                    float radial = 1f + fold * 0.08f * h;
                    float hem = 1f + Mathf.Sin(a * 3f + 0.4f) * 0.06f * h;
                    Vector3 dir = right * Mathf.Sin(a) * halfWidth * widen * radial
                        - forward * Mathf.Cos(a) * depth * widen * cling * radial;
                    // Los costados bajan siguiendo la curva de los hombros.
                    float shoulder = Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 2f) * 0.05f;
                    v.Add(top - up * shoulder + dir - up * drop * h * hem);
                    n.Add((right * Mathf.Sin(a) - forward * Mathf.Cos(a)).normalized);
                    uv.Add(new Vector2(s / (float)segments * 3f, h * 2.5f));
                }
            }

            for (int r = 0; r < rows; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s;
                    int b = a + segments + 1;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            }

            // Cuello: borde superior doblado hacia adentro, sobre los hombros.
            int collarStart = v.Count;
            for (int s = 0; s <= segments; s++)
            {
                float a = Mathf.Lerp(-100f, 100f, s / (float)segments) * Mathf.Deg2Rad;
                Vector3 outer = right * Mathf.Sin(a) * halfWidth * 0.9f - forward * Mathf.Cos(a) * depth * 0.9f;
                float shoulder = Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 2f) * 0.05f;
                v.Add(top - up * shoulder + outer);
                n.Add(up);
                v.Add(top - up * shoulder + outer * 0.55f + up * 0.012f);
                n.Add(up);
                uv.Add(new Vector2(s / (float)segments * 3f, 0f));
                uv.Add(new Vector2(s / (float)segments * 3f, 0.15f));
            }

            for (int s = 0; s < segments; s++)
            {
                int a = collarStart + s * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 2); t.Add(a + 1); t.Add(a + 3);
            }

            Mesh flat = PropKit.Finish("Chompa", v, n, t);
            flat.SetUVs(0, uv); // para que se vea el tejido de la lana
            flat.RecalculateNormals(); // los pliegues dan sombra
            Mesh mesh = DoubleSided(flat);

            GameObject go = new GameObject("ChompaPuesta");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(chest, true);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            // La malla esta en coordenadas del mundo: el objeto arranca sin
            // desplazamiento ni giro en el mundo y luego queda colgado del pecho.
            Vector3 p = chest.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Abs(p.x), 1f / Mathf.Abs(p.y), 1f / Mathf.Abs(p.z));
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PropKit.Mat(materialName, fallbackColor);

            // Mangas vacias que caen por delante, sobre el pecho (como una
            // chompa echada sobre los hombros). SweaterDrape las saca del
            // cuerpo si quedan dentro.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = top + right * side * Mathf.Max(0.07f, shoulderHalf - 0.04f);
                Renderer sleeve = PropKit.Part(go.transform, PrimitiveType.Capsule, Vector3.zero, Vector3.one,
                    materialName, fallbackColor);
                Transform st = sleeve.transform;
                st.position = shoulder - up * 0.15f + forward * 0.12f;
                st.rotation = Quaternion.LookRotation(forward, Quaternion.AngleAxis(side * 8f, forward) * up);
                Vector3 ls = st.parent.lossyScale;
                st.localScale = new Vector3(0.06f / Mathf.Abs(ls.x), 0.17f / Mathf.Abs(ls.y), 0.04f / Mathf.Abs(ls.z));
            }

            // Cae desde los hombros y se mueve como tela.
            go.AddComponent<SweaterDrape>().Init(top, up, forward, drop + 0.02f);
        }

        private static Mesh DoubleSided(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            int count = v.Length;
            Vector3[] v2 = new Vector3[count * 2];
            Vector3[] n2 = new Vector3[count * 2];
            Vector2[] uv = mesh.uv;
            bool hasUv = uv != null && uv.Length == count;
            Vector2[] uv2 = new Vector2[count * 2];
            int[] t2 = new int[t.Length * 2];
            for (int i = 0; i < count; i++)
            {
                v2[i] = v[i];
                n2[i] = n[i];
                v2[i + count] = v[i];
                n2[i + count] = -n[i];
                if (hasUv)
                {
                    uv2[i] = uv[i];
                    uv2[i + count] = uv[i];
                }
            }

            for (int i = 0; i < t.Length; i += 3)
            {
                t2[i] = t[i];
                t2[i + 1] = t[i + 1];
                t2[i + 2] = t[i + 2];
                t2[t.Length + i] = t[i] + count;
                t2[t.Length + i + 1] = t[i + 2] + count;
                t2[t.Length + i + 2] = t[i + 1] + count;
            }

            mesh.Clear();
            mesh.vertices = v2;
            mesh.normals = n2;
            if (hasUv)
            {
                mesh.uv = uv2;
            }

            mesh.triangles = t2;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
