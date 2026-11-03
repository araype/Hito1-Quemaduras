using UnityEngine;

namespace SojaExiles
{
    // Movimiento de tela para la chompa puesta (SweaterItem.BuildDrape): al
    // ponerla cae desde los hombros y se asienta con un pequeno rebote; luego
    // el borde de abajo se balancea un poco y se queda atras cuando la
    // paciente se mueve, como una prenda suelta. Mueve los vertices de la
    // malla segun su altura: el cuello queda quieto y el borde inferior es lo
    // que mas se mueve. Las mangas (hijos) caen con la misma curva.
    [RequireComponent(typeof(MeshFilter))]
    public class SweaterDrape : MonoBehaviour
    {
        public float settleSeconds = 0.75f;
        public float swayAmount = 0.012f;
        public float swaySpeed = 1.3f;
        public float lagAmount = 0.06f;

        // Datos en coordenadas de la malla (iguales al mundo al crearla).
        private Vector3 top, up, forward;
        private float drop;
        private Vector3[] baseVertices;
        private Vector3[] vertices;
        private Mesh mesh;
        private Transform[] sleeves;
        private Vector3[] sleeveScale;
        private Vector3[] sleevePosition;
        private float start;
        private Vector3 lastWorld;
        private Vector3 lag;

        public void Init(Vector3 topPoint, Vector3 upDir, Vector3 forwardDir, float dropLength)
        {
            top = topPoint;
            up = upDir.normalized;
            forward = forwardDir.normalized;
            drop = Mathf.Max(0.05f, dropLength);

            mesh = GetComponent<MeshFilter>().mesh;
            mesh.MarkDynamic();
            baseVertices = mesh.vertices;
            vertices = new Vector3[baseVertices.Length];

            sleeves = new Transform[transform.childCount];
            sleeveScale = new Vector3[sleeves.Length];
            sleevePosition = new Vector3[sleeves.Length];
            for (int i = 0; i < sleeves.Length; i++)
            {
                sleeves[i] = transform.GetChild(i);
                sleeveScale[i] = sleeves[i].localScale;
                sleevePosition[i] = sleeves[i].localPosition;
            }

            start = Time.time;
            lastWorld = transform.position;
            Apply(0f);
        }

        private void LateUpdate()
        {
            if (baseVertices == null)
            {
                return;
            }

            // Cuanto se quedo atras la tela por el movimiento de la paciente
            // (en coordenadas de la malla); vuelve como un resorte.
            Vector3 world = transform.position;
            Vector3 moved = transform.InverseTransformVector(world - lastWorld);
            lastWorld = world;
            lag = Vector3.Lerp(lag - moved * 6f, Vector3.zero, 1f - Mathf.Exp(-4f * Time.deltaTime));
            lag = Vector3.ClampMagnitude(lag, lagAmount);

            float t = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.05f, settleSeconds));
            Apply(t);
        }

        // Caida con rebote: 0 = recogida en los hombros, 1 = puesta.
        private static float Settle(float t)
        {
            if (t >= 1f)
            {
                return 1f;
            }

            float eased = 1f - Mathf.Pow(1f - t, 3f);
            return eased + Mathf.Sin(t * Mathf.PI * 2.2f) * 0.08f * (1f - t);
        }

        private void Apply(float t)
        {
            float unfold = Settle(t);
            float sway = Mathf.Sin(Time.time * swaySpeed) * swayAmount * t;

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float below = Mathf.Max(0f, Vector3.Dot(top - v, up));
                float h = Mathf.Clamp01(below / drop); // 0 cuello, 1 borde
                Vector3 p = v + up * below * (1f - unfold);
                p += (forward * sway + lag) * h * h;
                vertices[i] = p;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();

            for (int i = 0; i < sleeves.Length; i++)
            {
                if (sleeves[i] == null)
                {
                    continue;
                }

                Vector3 s = sleeveScale[i];
                sleeves[i].localScale = new Vector3(s.x, s.y * Mathf.Max(0.05f, unfold), s.z);
                sleeves[i].localPosition = sleevePosition[i] + (forward * sway + lag) * 0.6f;
            }
        }
    }
}
