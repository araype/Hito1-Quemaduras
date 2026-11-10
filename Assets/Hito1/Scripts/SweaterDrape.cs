using System.Collections.Generic;
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

        // Capsulas del cuerpo (PatientPhysics): la tela no entra en ellas.
        public float skin = 0.012f;
        private CapsuleCollider[] body = new CapsuleCollider[0];
        // Todas, con brazos y manos: solo para las mangas colgantes, que si
        // no atravesaban los brazos.
        private CapsuleCollider[] bodyWithArms = new CapsuleCollider[0];

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

            PatientBody patient = GetComponentInParent<PatientBody>();
            if (patient != null)
            {
                List<CapsuleCollider> found = new List<CapsuleCollider>();
                List<CapsuleCollider> all = new List<CapsuleCollider>();
                foreach (CapsuleCollider c in patient.GetComponentsInChildren<CapsuleCollider>())
                {
                    if (!c.isTrigger && c.transform != transform && !c.transform.IsChildOf(transform))
                    {
                        all.Add(c);
                    }

                    // Sin las de antebrazos y manos: al levantar la mano
                    // (protegerla, ofrecerla, recibir la gasa) su capsula
                    // empujaba la tela hacia afuera y la chompa se abria como
                    // alas o quedaba como un bloque delante del pecho. La del
                    // brazo (hombro a codo) se queda: por ella la tela cae
                    // sobre los hombros y no la atraviesa el brazo.
                    string n = c.name.ToLowerInvariant();
                    if (n.Contains("antebrazo") || n.Contains("mano") || n.Contains("forearm") || n.Contains("hand"))
                    {
                        continue;
                    }

                    if (!c.isTrigger && c.transform != transform && !c.transform.IsChildOf(transform))
                    {
                        found.Add(c);
                    }
                }

                body = found.ToArray();
                bodyWithArms = all.ToArray();
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

        // Saca el punto (mundo) de las capsulas del cuerpo.
        private Vector3 PushOut(Vector3 point, float extra = 0f)
        {
            return PushOut(point, body, extra);
        }

        private Vector3 PushOut(Vector3 point, CapsuleCollider[] capsules, float extra)
        {
            foreach (CapsuleCollider c in capsules)
            {
                if (c == null || !c.enabled)
                {
                    continue;
                }

                Transform t = c.transform;
                Vector3 axisLocal = c.direction == 0 ? Vector3.right : (c.direction == 1 ? Vector3.up : Vector3.forward);
                Vector3 s = t.lossyScale;
                float axisScale = Mathf.Abs(Vector3.Scale(axisLocal, s).magnitude);
                float radiusScale = c.direction == 0 ? Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))
                    : (c.direction == 1 ? Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)) : Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)));
                float radius = c.radius * radiusScale + skin + extra;
                float half = Mathf.Max(0f, c.height * 0.5f * axisScale - c.radius * radiusScale);
                Vector3 center = t.TransformPoint(c.center);
                Vector3 axis = t.TransformDirection(axisLocal).normalized;
                float along = Mathf.Clamp(Vector3.Dot(point - center, axis), -half, half);
                Vector3 closest = center + axis * along;
                Vector3 out_ = point - closest;
                float distance = out_.magnitude;
                if (distance < radius)
                {
                    point = closest + (distance > 1e-5f ? out_ / distance : -axis) * radius;
                }
            }

            return point;
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
                vertices[i] = body.Length > 0 ? transform.InverseTransformPoint(PushOut(transform.TransformPoint(p))) : p;
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
                if (body.Length > 0)
                {
                    // El centro de la manga queda fuera del cuerpo (mas su grosor).
                    sleeves[i].position = PushOut(sleeves[i].position, bodyWithArms, 0.02f);
                }
            }
        }
    }
}
