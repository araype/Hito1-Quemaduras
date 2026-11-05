using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Donde poner un panel frente al jugador sin tapar lo que hay que mirar:
    // la paciente (cabeza, pecho y mano quemada), el reloj y lo que la guia
    // este resaltando. Se prueban varias posiciones cerca del centro de la
    // vista y se elige la que no tapa nada y queda mas al centro. Los paneles
    // se dibujan encima de todo (OverlayPanel), asi que taparian aunque el
    // objeto este mas cerca.
    public static class PanelPlacement
    {
        // (giro, altura) en grados respecto de la posicion base.
        private static readonly Vector2[] Candidates =
        {
            new Vector2(0f, 0f),
            new Vector2(-27f, 0f),
            new Vector2(27f, 0f),
            new Vector2(0f, 17f),
            new Vector2(0f, -20f),
            new Vector2(-25f, 14f),
            new Vector2(25f, 14f),
            new Vector2(-25f, -18f),
            new Vector2(25f, -18f)
        };

        private const float Margin = 3f;
        private const float Hysteresis = 8f;

        // Puntos extra que no se deben tapar (p.ej. los brillos de la guia):
        // cada funcion agrega los suyos a la lista.
        public static readonly List<System.Action<List<Vector3>>> ExtraFocus = new List<System.Action<List<Vector3>>>();

        private static readonly List<Vector3> Focus = new List<Vector3>();
        private static int focusFrame = -1;
        private static PatientBody[] patients = new PatientBody[0];
        private static CoolingClock[] clocks = new CoolingClock[0];
        private static float lastSearch = float.NegativeInfinity;

        // Posicion del panel. 'forward' es la direccion horizontal base,
        // 'drop' cuanto baja respecto de los ojos, 'halfSize' la mitad del
        // tamano del panel en metros. 'choice' guarda la opcion elegida entre
        // llamadas (empezar en -1) para no saltar de un lado a otro.
        public static Vector3 Choose(Vector3 eye, Vector3 forward, float distance, float drop, Vector2 halfSize,
            ref int choice)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            CollectFocus();

            float basePitch = -Mathf.Atan2(drop, distance) * Mathf.Rad2Deg;
            float halfYaw = Mathf.Atan2(halfSize.x, distance) * Mathf.Rad2Deg + Margin;
            float halfPitch = Mathf.Atan2(halfSize.y, distance) * Mathf.Rad2Deg + Margin;

            int best = 0;
            float bestScore = float.MaxValue;
            float currentScore = float.MaxValue;
            for (int i = 0; i < Candidates.Length; i++)
            {
                float yaw = Candidates[i].x;
                float pitch = basePitch + Candidates[i].y;
                int covered = 0;
                foreach (Vector3 p in Focus)
                {
                    Vector3 v = p - eye;
                    Vector3 flat = Vector3.ProjectOnPlane(v, Vector3.up);
                    if (flat.sqrMagnitude < 1e-4f)
                    {
                        continue;
                    }

                    float pYaw = Vector3.SignedAngle(forward, flat, Vector3.up);
                    float pPitch = Mathf.Atan2(v.y, flat.magnitude) * Mathf.Rad2Deg;
                    if (Mathf.Abs(pYaw - yaw) < halfYaw && Mathf.Abs(pPitch - pitch) < halfPitch)
                    {
                        covered++;
                    }
                }

                float score = covered * 100f + Mathf.Abs(Candidates[i].x) * 0.6f + Mathf.Abs(Candidates[i].y) * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }

                if (i == choice)
                {
                    currentScore = score;
                }
            }

            if (choice < 0 || choice >= Candidates.Length || currentScore > bestScore + Hysteresis)
            {
                choice = best;
            }

            return Position(eye, forward, distance, basePitch, choice);
        }

        private static Vector3 Position(Vector3 eye, Vector3 forward, float distance, float basePitch, int index)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Quaternion q = Quaternion.AngleAxis(Candidates[index].x, Vector3.up)
                * Quaternion.AngleAxis(-(basePitch + Candidates[index].y), side);
            return eye + q * forward * distance;
        }

        // Gira el panel de frente a los ojos (tambien en altura, para que no
        // se vea inclinado).
        public static Quaternion Facing(Vector3 panel, Vector3 eye)
        {
            Vector3 look = panel - eye;
            return look.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
        }

        private static void CollectFocus()
        {
            if (focusFrame == Time.frameCount && Time.unscaledTime >= lastSearch)
            {
                return;
            }

            focusFrame = Time.frameCount;
            Focus.Clear();
            // Busca de nuevo cada segundo (o si el tiempo volvio atras: en el
            // editor estas variables pasan de una partida a la otra).
            float now = Time.unscaledTime;
            if (now - lastSearch > 1f || now < lastSearch)
            {
                lastSearch = now;
                patients = Object.FindObjectsOfType<PatientBody>();
                clocks = Object.FindObjectsOfType<CoolingClock>();
            }

            foreach (PatientBody patient in patients)
            {
                if (patient == null || !patient.IsReady)
                {
                    continue;
                }

                if (patient.Head != null)
                {
                    Focus.Add(patient.Head.position);
                }

                if (patient.Chest != null)
                {
                    Focus.Add(patient.Chest.position);
                }

                Transform hand = patient.HandBone(true);
                if (hand != null)
                {
                    Focus.Add(hand.position);
                }
            }

            foreach (CoolingClock clock in clocks)
            {
                // El reloj visible es otro objeto (Face), no el del componente.
                if (clock != null && clock.isActiveAndEnabled && clock.Face != null)
                {
                    Focus.Add(clock.Face.position);
                }
            }

            foreach (System.Action<List<Vector3>> extra in ExtraFocus)
            {
                extra(Focus);
            }
        }
    }

    // Panel fijo frente al jugador (menu, decision medica, resumen): lo
    // sigue sin prisa cuando gira la cabeza, siempre de frente y sin tapar
    // lo que hay que mirar (PanelPlacement).
    public class PanelFollow : MonoBehaviour
    {
        public float distance = 0.7f;
        public float drop = 0.12f;
        public Vector2 halfSize = new Vector2(0.3f, 0.2f);
        [Tooltip("Cuanto puede girar la cabeza antes de que el panel la siga.")]
        public float followAngle = 32f;

        private Transform head;
        private Vector3 anchor;
        private Vector3 velocity;
        private int choice = -1;

        public static PanelFollow Add(Transform panel, float distance, float drop, Vector2 size)
        {
            PanelFollow follow = panel.gameObject.AddComponent<PanelFollow>();
            follow.distance = distance;
            follow.drop = drop;
            follow.halfSize = size * 0.5f;
            follow.Snap();
            return follow;
        }

        private void Snap()
        {
            head = VRPanelKit.Head();
            if (head == null)
            {
                return;
            }

            anchor = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            transform.position = PanelPlacement.Choose(head.position, anchor, distance, drop, halfSize, ref choice);
            transform.rotation = PanelPlacement.Facing(transform.position, head.position);
        }

        private void LateUpdate()
        {
            if (head == null)
            {
                Snap();
                return;
            }

            // Mientras una mano esta cerca (por apretar un boton) se queda quieto.
            if (HandNear())
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude > 1e-4f && Vector3.Angle(forward, anchor) > followAngle)
            {
                anchor = forward.normalized;
            }

            Vector3 wanted = PanelPlacement.Choose(head.position, anchor, distance, drop, halfSize, ref choice);
            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref velocity, 0.35f);
            transform.rotation = PanelPlacement.Facing(transform.position, head.position);
        }

        private bool HandNear()
        {
            for (int i = 0; i < 2; i++)
            {
                if (!SimpleGrab.TryGetHand(i == 0, out Vector3 hand, out bool _))
                {
                    continue;
                }

                Vector3 local = transform.InverseTransformPoint(hand);
                if (Mathf.Abs(local.x) < halfSize.x + 0.05f && Mathf.Abs(local.y) < halfSize.y + 0.05f
                    && local.z > -0.15f && local.z < 0.05f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
