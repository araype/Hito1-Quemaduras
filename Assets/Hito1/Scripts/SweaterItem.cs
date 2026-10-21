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
            float halfWidth = Mathf.Max(0.14f, Vector3.Distance(left, rightShoulder) * 0.5f + 0.05f);
            const float depth = 0.13f;
            const float drop = 0.36f;
            Vector3 top = center + up * 0.035f;

            List<Vector3> v = new List<Vector3>();
            List<Vector3> n = new List<Vector3>();
            List<int> t = new List<int>();
            const int segments = 18;
            const int rows = 6;

            // Espalda: de -100 a 100 grados alrededor (0 = atras).
            for (int r = 0; r <= rows; r++)
            {
                float h = r / (float)rows;
                // Mas angosta arriba (sobre los hombros) y abierta abajo.
                float widen = Mathf.Lerp(0.9f, 1.08f, h);
                for (int s = 0; s <= segments; s++)
                {
                    float a = Mathf.Lerp(-100f, 100f, s / (float)segments) * Mathf.Deg2Rad;
                    Vector3 dir = right * Mathf.Sin(a) * halfWidth * widen - forward * Mathf.Cos(a) * depth * widen;
                    v.Add(top + dir - up * drop * h);
                    n.Add((right * Mathf.Sin(a) - forward * Mathf.Cos(a)).normalized);
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
                v.Add(top + outer);
                n.Add(up);
                v.Add(top + outer * 0.55f + up * 0.012f);
                n.Add(up);
            }

            for (int s = 0; s < segments; s++)
            {
                int a = collarStart + s * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 2);
                t.Add(a + 2); t.Add(a + 1); t.Add(a + 3);
            }

            Mesh mesh = DoubleSided(PropKit.Finish("Chompa", v, n, t));

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

            // Mangas colgando por delante de cada hombro.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = top + right * side * halfWidth * 0.85f + forward * 0.03f;
                Renderer sleeve = PropKit.Part(go.transform, PrimitiveType.Capsule, Vector3.zero, Vector3.one,
                    materialName, fallbackColor);
                Transform st = sleeve.transform;
                st.position = shoulder - up * 0.17f + forward * 0.035f;
                st.rotation = Quaternion.LookRotation(forward, up);
                Vector3 ls = st.parent.lossyScale;
                st.localScale = new Vector3(0.075f / Mathf.Abs(ls.x), 0.19f / Mathf.Abs(ls.y), 0.06f / Mathf.Abs(ls.z));
            }
        }

        private static Mesh DoubleSided(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            int count = v.Length;
            Vector3[] v2 = new Vector3[count * 2];
            Vector3[] n2 = new Vector3[count * 2];
            int[] t2 = new int[t.Length * 2];
            for (int i = 0; i < count; i++)
            {
                v2[i] = v[i];
                n2[i] = n[i];
                v2[i + count] = v[i];
                n2[i + count] = -n[i];
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

            mesh.vertices = v2;
            mesh.normals = n2;
            mesh.triangles = t2;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
