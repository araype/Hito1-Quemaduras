using UnityEngine;

namespace SojaExiles
{
    // Pone al personaje (rig de Mixamo en T-pose, sin animaciones) en la
    // señal universal de atragantamiento: ambas manos sujetando el cuello y
    // el torso inclinado hacia adelante. Cada brazo se resuelve con IK de dos
    // huesos a partir de las posiciones de los huesos, así no depende de sus
    // ejes locales. Todas las rotaciones apuntan a direcciones absolutas, por
    // lo que aplicarla varias veces deja la misma pose (se ve también en el editor).
    [ExecuteAlways]
    public class PoseAtragantamiento : MonoBehaviour
    {
        public string prefijoHuesos = "mixamorig8:";
        [Range(0f, 35f)] public float inclinacionTorso = 18f;
        [Range(-20f, 30f)] public float inclinacionCabeza = 10f;

        [Header("Muñecas (relativas al hueso del cuello)")]
        public float muniecasAdelante = 0.13f;
        public float muniecasAbajo = 0.08f;
        public float muniecasSeparacion = 0.05f;

        [Header("Dedos: apuntan a los costados del cuello")]
        public float dedosSeparacion = 0.06f;
        public float dedosAltura = 0.03f;

        private bool pendiente;

        private void OnEnable()
        {
            Aplicar();
        }

        private void OnValidate()
        {
            // No se pueden mover transforms dentro de OnValidate; se aplica en el siguiente Update.
            pendiente = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }

        private void Update()
        {
            if (pendiente)
            {
                pendiente = false;
                Aplicar();
            }
        }

        [ContextMenu("Aplicar pose")]
        public void Aplicar()
        {
            Transform spine = Hueso("Spine");
            Transform neck = Hueso("Neck");
            Transform head = Hueso("Head");
            Transform headTop = Hueso("HeadTop");
            if (spine == null || neck == null)
            {
                Debug.LogWarning("PoseAtragantamiento: no se encontraron los huesos con prefijo " + prefijoHuesos, this);
                return;
            }

            Vector3 arriba = transform.up;
            Vector3 adelante = transform.forward;
            Vector3 derecha = transform.right;

            // Torso: la columna (Spine -> Neck) apunta hacia arriba inclinada hacia adelante.
            Apuntar(spine, neck.position, Quaternion.AngleAxis(inclinacionTorso, derecha) * arriba);

            // Cabeza: levemente hacia abajo / adelante.
            if (head != null && headTop != null)
            {
                Apuntar(head, headTop.position, Quaternion.AngleAxis(inclinacionTorso + inclinacionCabeza, derecha) * arriba);
            }

            // Direcciones del cuerpo ya inclinado.
            Vector3 arribaTorso = (neck.position - spine.position).normalized;
            Vector3 adelanteTorso = Vector3.ProjectOnPlane(adelante, arribaTorso).normalized;

            Brazo("Left", -derecha, neck.position, arribaTorso, adelanteTorso);
            Brazo("Right", derecha, neck.position, arribaTorso, adelanteTorso);
        }

        private void Brazo(string lado, Vector3 haciaAfuera, Vector3 cuello, Vector3 arriba, Vector3 adelante)
        {
            Transform arm = Hueso(lado + "Arm");
            Transform foreArm = Hueso(lado + "ForeArm");
            Transform hand = Hueso(lado + "Hand");
            if (arm == null || foreArm == null || hand == null)
            {
                return;
            }

            Vector3 muniecaObjetivo = cuello + adelante * muniecasAdelante - arriba * muniecasAbajo + haciaAfuera * muniecasSeparacion;

            float a = Vector3.Distance(arm.position, foreArm.position);
            float b = Vector3.Distance(foreArm.position, hand.position);
            Vector3 alObjetivo = muniecaObjetivo - arm.position;
            float d = Mathf.Clamp(alObjetivo.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 dir = alObjetivo.normalized;

            // El codo cae hacia abajo, un poco hacia afuera y adelante.
            Vector3 polo = -arriba * 1f + haciaAfuera * 0.5f + adelante * 0.3f;
            Vector3 perp = Vector3.ProjectOnPlane(polo, dir).normalized;

            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 codo = arm.position + dir * (a * cosA) + perp * (a * sinA);

            Apuntar(arm, foreArm.position, codo - arm.position);
            Apuntar(foreArm, hand.position, muniecaObjetivo - foreArm.position);

            // Mano: los dedos van hacia el costado del cuello y la palma mira al cuello
            // (con la palma hacia el cuerpo y los dedos arriba, el pulgar queda hacia afuera).
            Transform medio = Hueso(lado + "HandMiddle1");
            Transform pulgar = Hueso(lado + "HandThumb1");
            if (medio == null)
            {
                return;
            }
            Vector3 dedosObjetivo = cuello + arriba * dedosAltura + haciaAfuera * dedosSeparacion;
            Apuntar(hand, medio.position, dedosObjetivo - hand.position);

            if (pulgar != null)
            {
                Vector3 eje = (medio.position - hand.position).normalized;
                Vector3 pulgarActual = Vector3.ProjectOnPlane(pulgar.position - hand.position, eje);
                Vector3 pulgarDeseado = Vector3.ProjectOnPlane(haciaAfuera + adelante * 0.5f, eje);
                if (pulgarActual.sqrMagnitude > 1e-8f && pulgarDeseado.sqrMagnitude > 1e-8f)
                {
                    hand.rotation = Quaternion.FromToRotation(pulgarActual, pulgarDeseado) * hand.rotation;
                }
            }
        }

        // Rota el hueso para que la dirección hacia su hijo quede en la dirección deseada.
        private static void Apuntar(Transform hueso, Vector3 posicionHijo, Vector3 direccionDeseada)
        {
            Vector3 actual = posicionHijo - hueso.position;
            if (actual.sqrMagnitude < 1e-8f || direccionDeseada.sqrMagnitude < 1e-8f)
            {
                return;
            }
            hueso.rotation = Quaternion.FromToRotation(actual, direccionDeseada) * hueso.rotation;
        }

        private Transform Hueso(string nombre)
        {
            return Buscar(transform, prefijoHuesos + nombre);
        }

        private static Transform Buscar(Transform raiz, string nombre)
        {
            if (raiz.name == nombre)
            {
                return raiz;
            }
            for (int i = 0; i < raiz.childCount; i++)
            {
                Transform t = Buscar(raiz.GetChild(i), nombre);
                if (t != null)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
