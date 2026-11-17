using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;

namespace SojaExiles
{
    // Teletransporte con las manos (sin controles) para ir lejos, por ejemplo
    // de la cocina a la sala por la manta. Caminar en el cuarto real sirve
    // para lo cercano, pero al salir del limite del Quest el visor oculta el
    // juego (prueba en el Quest Pro, 2026-11-17). Al teletransportarse, el
    // limite viaja con el jugador: en el destino vuelve a tener todo su
    // espacio real para agacharse o rodear la mesa.
    //
    //  - Apuntar: indice estirado y medio y anular doblados (como senalar),
    //    con la mano adelante. Sale un arco hasta el piso y un anillo donde
    //    se llegaria (azul si se puede, gris si no).
    //  - Saltar: con el pulgar arriba, bajarlo sobre el dedo medio (como
    //    disparar). Si el pulgar ya estaba abajo al empezar a apuntar, hay
    //    que levantarlo primero: al senalar con naturalidad el pulgar queda
    //    sobre el medio y saltaria sin querer.
    //  - Solo se llega al piso por donde se puede caminar (PlayArea), sin
    //    muebles encima y a no mas de 'maxDistance'.
    //  - No con la mano que sostiene algo, ni mientras se lleva a la paciente
    //    del antebrazo (ella se quedaria atras). Con la otra mano si: lo que
    //    se sostiene (la manta) viaja con el jugador.
    // VRLocomotion ignora los gestos de caminar de una mano mientras apunta
    // (Busy): el pulgar sobre el medio es tambien el gesto de caminar.
    [RequireComponent(typeof(VRLocomotion))]
    public class HandTeleport : MonoBehaviour
    {
        [Tooltip("Distancia maxima del salto (m, en horizontal).")]
        public float maxDistance = 5f;
        [Tooltip("Distancia minima del salto (m): mas cerca se camina.")]
        public float minDistance = 0.7f;
        [Tooltip("Segundos que hay que mantener la pose de apuntar antes de que salga el arco.")]
        public float holdSeconds = 0.25f;
        [Tooltip("Velocidad inicial del arco (m/s): mas alta, llega mas lejos.")]
        public float launchSpeed = 7f;
        [Tooltip("Indice estirado: distancia de la punta al centro de la palma, en largos de palma.")]
        public float extendedRatio = 1.15f;
        [Tooltip("Medio y anular doblados: distancia de la punta al centro de la palma, en largos de palma.")]
        public float curledRatio = 0.95f;
        [Tooltip("Pulgar abajo (salta) si su punta queda a menos de esto del medio, en largos de palma.")]
        public float thumbDownRatio = 0.35f;
        [Tooltip("Pulgar arriba (listo para saltar) si su punta queda a mas de esto, en largos de palma.")]
        public float thumbUpRatio = 0.55f;
        [Tooltip("La mano debe estar al menos a esta distancia de la cabeza (m): pegada al cuerpo no apunta.")]
        public float minReach = 0.22f;
        [Tooltip("Sin arco si la punta del indice esta a menos de esto de un boton (m): se esta por tocarlo.")]
        public float buttonClearance = 0.15f;
        public float cooldownSeconds = 0.6f;

        public Color validColor = new Color(0.2f, 0.65f, 1f, 0.9f);
        public Color invalidColor = new Color(0.6f, 0.62f, 0.68f, 0.5f);

        private const float ArcStep = 0.03f;
        private const int ArcMaxSteps = 60;
        private const float BusyAfterSeconds = 0.5f;

        private static readonly float[] busyUntil = { -1f, -1f };

        // True si esa mano esta apuntando para teletransportarse (o lo acaba
        // de hacer): sus gestos de caminar no cuentan.
        public static bool Busy(bool leftHand)
        {
            return Time.unscaledTime < busyUntil[leftHand ? 0 : 1];
        }

        // Veces que se teletransporto en la partida (para el registro).
        public static int Count { get; private set; }

#if UNITY_EDITOR
        // Mano simulada (con SimpleGrab.SimActive / SimPosition / SimRotation):
        // SimAim apunta hacia SimRotation * adelante; SimThumbDown baja el
        // pulgar. Para probar desde el MCP de Unity sin visor.
        public static readonly bool[] SimAim = new bool[2];
        public static readonly bool[] SimThumbDown = new bool[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSimulated()
        {
            for (int i = 0; i < 2; i++)
            {
                SimAim[i] = false;
                SimThumbDown[i] = false;
                busyUntil[i] = -1f;
            }

            Count = 0;
        }
#endif

        private class HandState
        {
            public float aimSince = -1f;
            public bool thumbArmed;
            public bool hasTarget;
            public bool targetValid;
            public Vector3 target;
            public LineRenderer arc;
            public LineRenderer ring;
        }

        private readonly HandState[] hands = { new HandState(), new HandState() };
        private readonly List<Vector3> arcPoints = new List<Vector3>();
        private VRLocomotion locomotion;
        private Material material;
        private float nextTeleport;
        private float nextLookup;
        private Oculus.Interaction.HandVisual[] visuals = new Oculus.Interaction.HandVisual[0];
        private TouchButton[] buttons = new TouchButton[0];

        private void Awake()
        {
            locomotion = GetComponent<VRLocomotion>();
            material = new Material(Shader.Find("Sprites/Default"));
            material.renderQueue = 3002;
            for (int i = 0; i < 2; i++)
            {
                hands[i].arc = CreateLine("ArcoTeleport" + i, false);
                hands[i].ring = CreateLine("DestinoTeleport" + i, true);
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private LineRenderer CreateLine(string name, bool loop)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = loop ? 0.02f : 0.012f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextLookup)
            {
                // El rig y los paneles se arman en runtime: se buscan cada 2 s.
                nextLookup = Time.unscaledTime + 2f;
                visuals = FindObjectsOfType<Oculus.Interaction.HandVisual>();
                buttons = FindObjectsOfType<TouchButton>();
            }

            for (int i = 0; i < 2; i++)
            {
                UpdateHand(i == 0, hands[i]);
            }
        }

        private void UpdateHand(bool isLeft, HandState state)
        {
            bool pointing = false;
            bool thumbDown = false;
            bool thumbUp = false;
            Vector3 origin = Vector3.zero;
            Vector3 direction = Vector3.forward;
            if (CanTeleport(isLeft))
            {
                pointing = ReadPose(isLeft, out origin, out direction, out thumbDown, out thumbUp);
            }

            Transform head = VRPanelKit.Head();
            if (pointing && head != null)
            {
                Vector3 reach = origin - head.position;
                reach.y = 0f;
                // Pegada al cuerpo o por tocar un boton: no es apuntar lejos.
                if (reach.magnitude < minReach || NearButton(origin))
                {
                    pointing = false;
                }
            }

            float now = Time.unscaledTime;
            if (!pointing)
            {
                state.aimSince = -1f;
                state.thumbArmed = false;
                state.hasTarget = false;
                Show(state, false);
                return;
            }

            busyUntil[isLeft ? 0 : 1] = now + BusyAfterSeconds;
            if (state.aimSince < 0f)
            {
                state.aimSince = now;
                state.thumbArmed = false;
            }

            if (thumbUp)
            {
                state.thumbArmed = true;
            }

            if (now - state.aimSince < holdSeconds)
            {
                Show(state, false);
                return;
            }

            state.hasTarget = TraceArc(origin, direction, head, out state.target, out state.targetValid);
            Draw(state);

            if (thumbDown && state.thumbArmed && now >= nextTeleport)
            {
                state.thumbArmed = false;
                if (state.hasTarget && state.targetValid)
                {
                    nextTeleport = now + cooldownSeconds;
                    Count++;
                    locomotion.TeleportTo(state.target);
                    Debug.Log("[HandTeleport] Salto a " + state.target.ToString("F2"));
                    // Tras el salto, el arco vuelve a salir recien al apuntar
                    // de nuevo (si no, al seguir apuntando se encadenan).
                    state.aimSince = now;
                    Show(state, false);
                }
            }
        }

        // Solo con hand tracking (con controles se camina con el joystick),
        // fuera del menu de pausa, con esa mano libre y sin llevar a la
        // paciente.
        private bool CanTeleport(bool isLeft)
        {
            if (PauseMenu.Paused || locomotion == null || !locomotion.enabled)
            {
                return false;
            }

#if UNITY_EDITOR
            bool simulated = SimpleGrab.SimActive[isLeft ? 0 : 1];
#else
            const bool simulated = false;
#endif
            if (!simulated && (OVRInput.GetActiveController() & OVRInput.Controller.Hands) == 0)
            {
                return false;
            }

            if (SimpleGrab.HeldBody(isLeft) != null || SimpleGrab.IsGripping(isLeft))
            {
                return false;
            }

            return !HoldingPatient(true) && !HoldingPatient(false);
        }

        private static bool HoldingPatient(bool leftHand)
        {
            Rigidbody rb = SimpleGrab.HeldBody(leftHand);
            return rb != null && (rb.GetComponentInParent<PatientBody>() != null
                || (PatientPhysics.Current != null && rb.transform.IsChildOf(PatientPhysics.Current.transform)));
        }

        private bool NearButton(Vector3 tip)
        {
            float limit = buttonClearance * buttonClearance;
            foreach (TouchButton button in buttons)
            {
                if (button != null && button.isActiveAndEnabled
                    && (button.transform.position - tip).sqrMagnitude < limit)
                {
                    return true;
                }
            }

            return false;
        }

        // Pose de senalar con la mano que se ve: indice estirado, medio y
        // anular doblados. Devuelve la punta del indice, hacia donde apunta y
        // si el pulgar esta abajo (sobre el medio) o arriba.
        private bool ReadPose(bool isLeft, out Vector3 origin, out Vector3 direction, out bool thumbDown,
            out bool thumbUp)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            thumbDown = false;
            thumbUp = false;
#if UNITY_EDITOR
            int s = isLeft ? 0 : 1;
            if (SimpleGrab.SimActive[s])
            {
                if (!SimAim[s])
                {
                    return false;
                }

                origin = SimpleGrab.SimPosition[s];
                direction = SimpleGrab.SimRotation[s] * Vector3.forward;
                thumbDown = SimThumbDown[s];
                thumbUp = !SimThumbDown[s];
                return true;
            }
#endif
            IHand hand = VisibleHand(isLeft);
            if (hand == null)
            {
                return false;
            }

            if (!hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist)
                || !hand.GetJointPose(HandJointId.HandMiddle1, out Pose middle1)
                || !hand.GetJointPose(HandJointId.HandMiddle2, out Pose middle2)
                || !hand.GetJointPose(HandJointId.HandIndex1, out Pose index1)
                || !hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexTip)
                || !hand.GetJointPose(HandJointId.HandMiddleTip, out Pose middleTip)
                || !hand.GetJointPose(HandJointId.HandRingTip, out Pose ringTip)
                || !hand.GetJointPose(HandJointId.HandThumbTip, out Pose thumbTip))
            {
                return false;
            }

            float palm = Vector3.Distance(wrist.position, middle1.position);
            if (palm < 0.03f)
            {
                return false;
            }

            // Mismo centro de la palma que SimpleGrab.UpdatePalm.
            Vector3 center = Vector3.Lerp(wrist.position, middle1.position, 0.6f);
            float index = Vector3.Distance(indexTip.position, center) / palm;
            float middle = Vector3.Distance(middleTip.position, center) / palm;
            float ring = Vector3.Distance(ringTip.position, center) / palm;
            if (index < extendedRatio || middle > curledRatio || ring > curledRatio)
            {
                return false;
            }

            // El pulgar se apoya en el costado del dedo medio doblado.
            float thumb = Mathf.Min(Vector3.Distance(thumbTip.position, middle2.position),
                Vector3.Distance(thumbTip.position, middle1.position)) / palm;
            thumbDown = thumb < thumbDownRatio;
            thumbUp = thumb > thumbUpRatio;

            origin = indexTip.position;
            direction = (indexTip.position - index1.position).normalized;
            return direction.sqrMagnitude > 0.5f;
        }

        private IHand VisibleHand(bool isLeft)
        {
            Handedness side = isLeft ? Handedness.Left : Handedness.Right;
            foreach (Oculus.Interaction.HandVisual visual in visuals)
            {
                if (visual == null || !visual.isActiveAndEnabled || !visual.IsVisible)
                {
                    continue;
                }

                IHand h = visual.Hand;
                if (h != null && h.IsTrackedDataValid && h.Handedness == side && h.IsHighConfidence)
                {
                    return h;
                }
            }

            return null;
        }

        // Arco de un tiro parabolico desde la punta del indice. Devuelve
        // false si no toca nada; si toca, 'valid' dice si se puede llegar ahi.
        private bool TraceArc(Vector3 origin, Vector3 direction, Transform head, out Vector3 point, out bool valid)
        {
            arcPoints.Clear();
            arcPoints.Add(origin);
            point = Vector3.zero;
            valid = false;

            Vector3 p = origin;
            Vector3 v = direction * launchSpeed;
            for (int i = 0; i < ArcMaxSteps; i++)
            {
                Vector3 next = p + v * ArcStep;
                v += Physics.gravity * ArcStep;
                if (Cast(p, next, out RaycastHit hit))
                {
                    arcPoints.Add(hit.point);
                    point = hit.point;
                    valid = ValidTarget(hit, head);
                    return true;
                }

                arcPoints.Add(next);
                p = next;
                if (p.y < transform.position.y - 2f)
                {
                    break;
                }
            }

            return false;
        }

        // Lo primero que toca el arco, sin contar el cuerpo del jugador, sus
        // manos ni lo que sostiene.
        private bool Cast(Vector3 from, Vector3 to, out RaycastHit best)
        {
            best = default;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-5f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(from, delta / length, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance >= nearest || IsPlayer(hit.collider))
                {
                    continue;
                }

                nearest = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        private bool IsPlayer(Collider col)
        {
            if (col.transform.IsChildOf(transform))
            {
                return true;
            }

            Rigidbody rb = col.attachedRigidbody;
            return rb != null && (rb.transform.IsChildOf(transform) || SimpleGrab.IsHeldObject(rb)
                || SimpleGrab.IsHandBody(rb));
        }

        // Piso (casi horizontal, a la altura de donde esta parado el
        // jugador), dentro de lo caminable, a una distancia razonable y con
        // lugar para el cuerpo.
        private bool ValidTarget(RaycastHit hit, Transform head)
        {
            if (hit.normal.y < 0.75f || Mathf.Abs(hit.point.y - transform.position.y) > 0.2f)
            {
                return false;
            }

            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
            {
                // Un objeto suelto en el piso (la olla, la manta caida).
                return false;
            }

            Vector3 from = head != null ? head.position : transform.position;
            Vector3 flat = hit.point - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < minDistance || distance > maxDistance || !PlayArea.Allowed(hit.point))
            {
                return false;
            }

            return Clear(hit.point);
        }

        private readonly Collider[] overlap = new Collider[16];

        private bool Clear(Vector3 floor)
        {
            const float radius = 0.22f;
            Vector3 bottom = floor + Vector3.up * (0.25f + radius);
            Vector3 top = floor + Vector3.up * 1.6f;
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = overlap[i];
                if (col == null || IsPlayer(col))
                {
                    continue;
                }

                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    // Objetos sueltos: se los empuja al caminar.
                    continue;
                }

                return false;
            }

            return true;
        }

        private void Draw(HandState state)
        {
            Color color = state.hasTarget && state.targetValid ? validColor : invalidColor;
            Color faded = color;
            faded.a *= 0.15f;

            LineRenderer arc = state.arc;
            arc.positionCount = arcPoints.Count;
            for (int i = 0; i < arcPoints.Count; i++)
            {
                arc.SetPosition(i, arcPoints[i]);
            }

            arc.startColor = faded;
            arc.endColor = color;
            arc.enabled = true;

            LineRenderer ring = state.ring;
            if (!state.hasTarget)
            {
                ring.enabled = false;
                return;
            }

            const int segments = 32;
            const float radius = 0.25f;
            ring.positionCount = segments;
            Vector3 c = state.target + Vector3.up * 0.015f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }

            ring.startColor = color;
            ring.endColor = color;
            ring.enabled = true;
        }

        private static void Show(HandState state, bool visible)
        {
            state.arc.enabled = visible;
            state.ring.enabled = visible;
        }
    }
}
