using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace SojaExiles
{
    // Locomocion unica del jugador. Reemplaza al teletransporte/paso/giro que
    // trae el rig de Meta (HandsControllersUnifier los apaga) para que todo
    // pase por un solo CharacterController, ya sea con controles o con manos:
    //
    //  - Controles / manos conducidas por control: joystick izquierdo camina
    //    en la direccion de la mirada; joystick derecho gira a saltos.
    //  - Solo manos (sin controles): pellizco de dedo medio de la mano
    //    izquierda = avanzar, dedo anular izquierda = retroceder; pellizco de
    //    dedo medio derecha = girar a la derecha, dedo anular derecha =
    //    girar a la izquierda. El indice queda libre para agarrar. Los gestos
    //    de una mano se ignoran mientras esa mano sostiene algo o pellizca
    //    con el indice, para no caminar sin querer al agarrar.
    //
    // La capsula del CharacterController sigue a la cabeza: cuando el
    // jugador camina fisicamente, el cuerpo se mueve con controller.Move
    // (que respeta paredes y muebles) y el rig se corre al reves para que la
    // vista no salte. Asi la capsula nunca queda metida dentro de una pared.
    [RequireComponent(typeof(CharacterController))]
    public class VRLocomotion : MonoBehaviour
    {
        // walkSpeed y snapAngle tienen nombre nuevo a proposito: la escena
        // guarda moveSpeed 1.8 y snapTurnAngle 45, que pisarian estos valores.
        [Tooltip("Velocidad al caminar con el joystick a fondo (m/s).")]
        public float walkSpeed = 1.3f;
        [Tooltip("Segundos para pasar de quieto a walkSpeed, o de walkSpeed a quieto.")]
        public float moveRampSeconds = 0.12f;
        public float gravity = -9.81f;

        [Header("Entrada")]
        public float deadZone = 0.15f;
        public bool enableSnapTurn = true;
        [Tooltip("Grados de cada salto de giro.")]
        public float snapAngle = 30f;
        public float snapTurnThreshold = 0.7f;
        public bool enableHandPinchLocomotion = true;
        public float pinchStrengthThreshold = 0.8f;

        [Header("Capsula")]
        public float capsuleRadius = 0.2f;
        public float minHeight = 0.9f;
        public float maxHeight = 2.2f;

        [Header("Paciente")]
        [Tooltip("Radio alrededor de los ojos que no puede entrar en el cuerpo de la paciente (m).")]
        public float headClearance = 0.15f;

        private CharacterController controller;

        // Red de seguridad: ultimo lugar con piso firme. Si el jugador cae
        // muy por debajo (se reporto "caer al vacio" junto al meson), vuelve
        // ahi en vez de seguir cayendo.
        [Tooltip("Metros por debajo del ultimo piso firme a partir de los cuales el jugador vuelve a el.")]
        public float fallRecoverDistance = 0.8f;
        private Vector3 lastSafePosition;
        private bool hasSafePosition;
        private OVRCameraRig rig;
        private OVRHand leftHand;
        private OVRHand rightHand;
        private float verticalVelocity;
        private Vector3 horizontalVelocity;
        private bool snapTurnReady = true;
        private float nextLookup;
        private HandGrabInteractor[] handGrabInteractors = new HandGrabInteractor[0];
        private SimpleGrab simpleGrab;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            ResolveReferences();
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            Transform head = GetHead();
            Vector2 moveInput;
            float turnInput;
            ReadInput(out moveInput, out turnInput);

            if (enableSnapTurn)
            {
                HandleSnapTurn(head, turnInput);
            }

            SyncCapsuleToHead(head);

            Vector3 move = Vector3.zero;
            if (head != null && moveInput.sqrMagnitude > 0f)
            {
                Vector3 forward = head.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    // Mirando casi recto arriba o abajo: usar hacia donde
                    // apunta la parte superior de la cabeza.
                    forward = head.up;
                    forward.y = 0f;
                }
                forward.Normalize();

                Vector3 right = head.right;
                right.y = 0f;
                right.Normalize();

                move = forward * moveInput.y + right * moveInput.x;
                if (move.sqrMagnitude > 1f)
                {
                    move.Normalize();
                }
            }

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -0.5f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }

            // Rampa corta: sin ella la velocidad cambia de golpe al arrancar y
            // al frenar. Larga marea; por eso son solo unas decimas.
            float acceleration = walkSpeed / Mathf.Max(0.001f, moveRampSeconds);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, move * walkSpeed, acceleration * Time.deltaTime);

            Vector3 velocity = horizontalVelocity;
            velocity.y = verticalVelocity;

            Vector3 step = velocity * Time.deltaTime;
            Vector3 clipped = PlayArea.ClipPlayer(transform.position, step);
            if (clipped.x != step.x)
            {
                horizontalVelocity.x = 0f;
            }

            if (clipped.z != step.z)
            {
                horizontalVelocity.z = 0f;
            }

            standingOnProp = null;
            Vector3 beforeMove = transform.position;
            controller.Move(clipped);
            if (ClimbedFurniture(beforeMove))
            {
                // Se trepo por el borde de un mueble (la mesa de centro de la
                // sala): se deshace el paso, como chocar con el. Antes lo
                // atrapaba GuardAgainstFalling y devolvia al jugador al ultimo
                // lugar firme, que se sentia como un empujon hacia atras.
                UndoMove(beforeMove);
                horizontalVelocity = Vector3.zero;
            }

            PushObjects();
            SlideOffProp();
            KeepHeadOutOfPatient(head);
            GuardAgainstFalling();
        }

        // Objetos sueltos (con Rigidbody: la olla, la taza, el pano...) no son
        // piso: la capsula se subia a la olla al caminar a su alrededor y el
        // jugador quedaba parado encima. Si la capsula apoya en uno, se corre
        // hacia afuera de el.
        [Tooltip("Velocidad con la que el jugador se desliza fuera de un objeto suelto que piso (m/s).")]
        public float propSlideSpeed = 1.5f;
        private Collider standingOnProp;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.rigidbody != null && hit.normal.y > 0.3f)
            {
                standingOnProp = hit.collider;
            }
        }

        // Empujar al caminar (p.ej. la olla en el piso tras el accidente). La
        // capsula del CharacterController no empuja nada y, contra un objeto
        // bajo, se subia por su borde antes de que el empujon hiciera efecto.
        // Por eso la capsula no choca con estos objetos y, cada cuadro, el que
        // queda dentro de su radio se corre hacia afuera en horizontal, a la
        // velocidad del jugador (entre minPushSpeed y maxPushSpeed). Lo que
        // esta fijo (kinematico, como la olla antes del accidente) no se mueve.
        // Son la olla y los objetos del SDK de Meta (no los sueltos de
        // SimpleGrab, que ya no chocan con el jugador).
        [Tooltip("Velocidad maxima a la que el jugador empuja un objeto con fisica al caminar contra el (m/s).")]
        public float maxPushSpeed = 0.8f;
        [Tooltip("Velocidad minima del empujon, aunque el jugador camine muy lento (m/s).")]
        public float minPushSpeed = 0.3f;

        private readonly System.Collections.Generic.List<Rigidbody> pushables =
            new System.Collections.Generic.List<Rigidbody>();
        private float nextPushScan;

        private void FindPushables()
        {
            if (Time.unscaledTime < nextPushScan)
            {
                return;
            }

            nextPushScan = Time.unscaledTime + 1f;
            IgnorePatientBody();
            pushables.RemoveAll(rb => rb == null);
            foreach (Rigidbody rb in FindObjectsOfType<Rigidbody>())
            {
                // Solo la olla y los objetos que se agarran con el SDK de Meta:
                // puertas y cajones tambien tienen fisica y deben seguir
                // frenando al jugador.
                bool movable = rb.GetComponent<PotAccident>() != null
                    || rb.GetComponentInChildren<Oculus.Interaction.Grabbable>() != null;
                if (!movable || rb.GetComponent<Joint>() != null)
                {
                    continue;
                }

                if (pushables.Contains(rb) || rb.GetComponent<Grabbable>() != null
                    || rb.GetComponentInParent<PatientBody>() != null || rb.GetComponentInParent<VRLocomotion>() != null
                    || (PatientPhysics.Current != null && rb.transform.IsChildOf(PatientPhysics.Current.transform)))
                {
                    continue;
                }

                bool any = false;
                foreach (Collider col in rb.GetComponentsInChildren<Collider>())
                {
                    if (!col.isTrigger && col.attachedRigidbody == rb)
                    {
                        Physics.IgnoreCollision(controller, col, true);
                        any = true;
                    }
                }

                if (any)
                {
                    pushables.Add(rb);
                }
            }
        }

        // La capsula no choca con el cuerpo de la paciente: sus 14 capsulas
        // (piernas hasta 0,65 m, muslos hasta 1,1 m) son redondeadas y la del
        // jugador se subia por ellas como por una rampa y de ahi al meson (pasaba
        // al ponerle la chompa, pegado a ella). KeepHeadOutOfPatient sigue
        // evitando que la cabeza entre en su cuerpo.
        private int patientCollidersIgnored = -1;

        private void IgnorePatientBody()
        {
            PatientBody patient = FindObjectOfType<PatientBody>();
            if (patient == null)
            {
                return;
            }

            Collider[] cols = patient.GetComponentsInChildren<Collider>(true);
            if (cols.Length == patientCollidersIgnored)
            {
                return;
            }

            patientCollidersIgnored = cols.Length;
            foreach (Collider col in cols)
            {
                if (col != null && col != controller && !col.isTrigger)
                {
                    Physics.IgnoreCollision(controller, col, true);
                }
            }
        }

        private void PushObjects()
        {
            FindPushables();
            Vector3 feet = transform.position;
            float top = feet.y + controller.height;
            Vector3 walking = controller.velocity;
            walking.y = 0f;
            float speed = Mathf.Clamp(walking.magnitude, minPushSpeed, maxPushSpeed);
            foreach (Rigidbody rb in pushables)
            {
                if (rb == null || rb.isKinematic || SimpleGrab.IsHeldObject(rb))
                {
                    continue;
                }

                Bounds b = new Bounds(rb.position, Vector3.zero);
                bool first = true;
                foreach (Collider col in rb.GetComponentsInChildren<Collider>())
                {
                    if (col.isTrigger || col.attachedRigidbody != rb)
                    {
                        continue;
                    }

                    if (first)
                    {
                        b = col.bounds;
                        first = false;
                    }
                    else
                    {
                        b.Encapsulate(col.bounds);
                    }
                }

                if (first || b.max.y < feet.y - 0.05f || b.min.y > top)
                {
                    continue;
                }

                Vector3 closest = b.ClosestPoint(new Vector3(feet.x, Mathf.Clamp(feet.y, b.min.y, b.max.y), feet.z));
                Vector3 away = closest - feet;
                away.y = 0f;
                if (away.magnitude > controller.radius + 0.02f)
                {
                    continue;
                }

                Vector3 dir = new Vector3(b.center.x - feet.x, 0f, b.center.z - feet.z);
                if (dir.sqrMagnitude < 1e-6f)
                {
                    dir = walking.sqrMagnitude > 1e-6f ? walking : transform.forward;
                    dir.y = 0f;
                }

                dir.Normalize();
                Vector3 velocity = rb.velocity;
                float along = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), dir);
                if (along < speed)
                {
                    rb.WakeUp();
                    rb.velocity = velocity + dir * (speed - along);
                }
            }
        }

        private void SlideOffProp()
        {
            if (standingOnProp == null)
            {
                return;
            }

            Vector3 away = transform.position - standingOnProp.bounds.center;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f)
            {
                away = -transform.forward;
                away.y = 0f;
            }

            // Hacia afuera y un poco hacia abajo, para volver al piso.
            Vector3 slide = away.normalized * propSlideSpeed * Time.deltaTime + Vector3.down * 0.02f;
            controller.Move(slide);
            verticalVelocity = Mathf.Min(verticalVelocity, -0.5f);
        }

        private void GuardAgainstFalling()
        {
            bool grounded = controller.isGrounded && verticalVelocity <= 0f && standingOnProp == null;
            if (grounded && (!hasFloor || transform.position.y < floorY - maxStandAboveFloor))
            {
                // El primer apoyo es el piso de la cocina (BurnStaging deja ahi
                // al jugador al empezar); si despues apoya bastante mas abajo,
                // ese era el piso.
                floorY = transform.position.y;
                hasFloor = true;
            }

            // Parado sobre un mueble (el meson, la cocina): no es piso. Vuelve
            // al ultimo lugar firme en el piso.
            if (grounded && hasFloor && hasSafePosition && transform.position.y > floorY + maxStandAboveFloor)
            {
                Debug.LogWarning("[VRLocomotion] El jugador quedo parado sobre un mueble (" +
                    transform.position.ToString("F2") + "); vuelve al piso " + lastSafePosition.ToString("F2"));
                ReturnToSafePosition();
                return;
            }

            if (grounded && (!hasFloor || transform.position.y <= floorY + maxStandAboveFloor))
            {
                lastSafePosition = transform.position;
                hasSafePosition = true;
                return;
            }

            if (hasSafePosition && transform.position.y < lastSafePosition.y - fallRecoverDistance)
            {
                Debug.LogWarning("[VRLocomotion] El jugador cayo por debajo del piso; vuelve al ultimo lugar firme " +
                    lastSafePosition.ToString("F2"));
                ReturnToSafePosition();
            }
        }

        [Tooltip("Altura sobre el piso a partir de la cual estar parado cuenta como subido a un mueble (m).")]
        public float maxStandAboveFloor = 0.3f;
        private float floorY;
        private bool hasFloor;

        private void ReturnToSafePosition()
        {
            controller.enabled = false;
            transform.position = lastSafePosition + Vector3.up * 0.05f;
            controller.enabled = true;
            verticalVelocity = 0f;
            horizontalVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        // La camara no entra en el cuerpo de la paciente. La capsula ya se
        // frenaba en ella, pero la cabeza seguia de largo al inclinarse o al
        // caminar en el espacio real, y tambien cuando ella caminaba hacia el
        // jugador. Se corre al jugador lo justo, en horizontal.
        private void KeepHeadOutOfPatient(Transform head)
        {
            PatientPhysics patient = PatientPhysics.Current;
            if (head == null || patient == null || headClearance <= 0f)
            {
                return;
            }

            Vector3 push = patient.PushOut(head.position, headClearance);
            float amount = push.magnitude;
            if (amount < 0.001f)
            {
                return;
            }

            push.y = 0f;
            if (push.magnitude < amount * 0.5f)
            {
                // Empuje casi vertical (cabeza sobre su hombro): hacia afuera
                // de ella, en horizontal.
                Vector3 away = head.position - patient.transform.position;
                away.y = 0f;
                push = away.sqrMagnitude > 1e-6f ? away.normalized * amount : push;
            }

            Vector3 before = transform.position;
            controller.Move(push);
            Vector3 moved = transform.position - before;
            moved.y = 0f;

            // Si una pared frena la capsula, igual se corre la vista.
            Transform trackingRoot = rig != null ? rig.transform : null;
            if (trackingRoot != null && trackingRoot != transform && trackingRoot.IsChildOf(transform))
            {
                trackingRoot.position += push - moved;
            }
        }

        private void ResolveReferences()
        {
            if (Time.unscaledTime < nextLookup)
            {
                return;
            }

            if (rig != null && leftHand != null && rightHand != null && handGrabInteractors.Length > 0)
            {
                return;
            }

            // El rig de Meta se arma en runtime; reintentar con calma.
            nextLookup = Time.unscaledTime + 0.5f;

            handGrabInteractors = GetComponentsInChildren<HandGrabInteractor>(true);
            if (simpleGrab == null)
            {
                simpleGrab = GetComponent<SimpleGrab>();
            }

            if (rig == null)
            {
                rig = GetComponentInChildren<OVRCameraRig>(true);
            }

            OVRHand[] hands = GetComponentsInChildren<OVRHand>(true);
            for (int i = 0; i < hands.Length; i++)
            {
                OVRPlugin.Hand type = hands[i].GetHand();
                if (type == OVRPlugin.Hand.HandLeft)
                {
                    leftHand = hands[i];
                }
                else if (type == OVRPlugin.Hand.HandRight)
                {
                    rightHand = hands[i];
                }
            }
        }

        private Transform GetHead()
        {
            if (rig != null && rig.centerEyeAnchor != null)
            {
                return rig.centerEyeAnchor;
            }

            if (Camera.main != null)
            {
                return Camera.main.transform;
            }

            Camera child = GetComponentInChildren<Camera>();
            return child != null ? child.transform : null;
        }

        private void ReadInput(out Vector2 moveInput, out float turnInput)
        {
            Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);

            moveInput = ApplyDeadZone(leftStick);
            turnInput = Mathf.Abs(rightStick.x) > deadZone ? rightStick.x : 0f;

            bool handsActive = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
            if (!enableHandPinchLocomotion || !handsActive)
            {
                return;
            }

            // Sin controles activos: los gestos reemplazan a los joysticks.
            if (moveInput.sqrMagnitude <= 0f && IsGestureHandFree(leftHand, true))
            {
                if (IsPinching(leftHand, OVRHand.HandFinger.Middle))
                {
                    moveInput = new Vector2(0f, 1f);
                }
                else if (IsPinching(leftHand, OVRHand.HandFinger.Ring))
                {
                    moveInput = new Vector2(0f, -1f);
                }
            }

            if (Mathf.Approximately(turnInput, 0f) && IsGestureHandFree(rightHand, false))
            {
                if (IsPinching(rightHand, OVRHand.HandFinger.Middle))
                {
                    turnInput = 1f;
                }
                else if (IsPinching(rightHand, OVRHand.HandFinger.Ring))
                {
                    turnInput = -1f;
                }
            }
        }

        // La mano esta libre para hacer gestos de locomocion si esta
        // trackeada, no sostiene nada (SimpleGrab ni Interaction SDK) y no
        // esta pellizcando con el indice (eso es agarrar).
        private bool IsGestureHandFree(OVRHand hand, bool isLeft)
        {
            if (hand == null || !hand.IsTracked)
            {
                return false;
            }

            if (IsPinching(hand, OVRHand.HandFinger.Index))
            {
                return false;
            }

            if (simpleGrab != null && simpleGrab.IsHolding(isLeft))
            {
                return false;
            }

            Handedness handedness = isLeft ? Handedness.Left : Handedness.Right;
            for (int i = 0; i < handGrabInteractors.Length; i++)
            {
                HandGrabInteractor interactor = handGrabInteractors[i];
                if (interactor != null && interactor.HasSelectedInteractable && interactor.Hand != null
                    && interactor.Hand.Handedness == handedness)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPinching(OVRHand hand, OVRHand.HandFinger finger)
        {
            return hand.GetFingerPinchStrength(finger) >= pinchStrengthThreshold;
        }

        // Reescala lo que queda fuera de la zona muerta a 0..1: al salir de
        // ella se empieza desde 0 y no con un salto de velocidad.
        private Vector2 ApplyDeadZone(Vector2 axis)
        {
            float magnitude = axis.magnitude;
            if (magnitude < deadZone)
            {
                return Vector2.zero;
            }

            return axis / magnitude * Mathf.InverseLerp(deadZone, 1f, magnitude);
        }

        private void HandleSnapTurn(Transform head, float turnInput)
        {
            if (Mathf.Abs(turnInput) >= snapTurnThreshold)
            {
                if (snapTurnReady)
                {
                    snapTurnReady = false;
                    Vector3 pivot = head != null ? head.position : transform.position;
                    float angle = Mathf.Sign(turnInput) * snapAngle;
                    transform.RotateAround(pivot, Vector3.up, angle);
                    // La rampa guarda la velocidad en el mundo: girarla tambien
                    // para no derrapar hacia la direccion anterior.
                    horizontalVelocity = Quaternion.AngleAxis(angle, Vector3.up) * horizontalVelocity;
                    Physics.SyncTransforms();
                }
            }
            else if (Mathf.Abs(turnInput) < 0.3f)
            {
                snapTurnReady = true;
            }
        }

        private void SyncCapsuleToHead(Transform head)
        {
            if (head == null)
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(head.position);
            float height = Mathf.Clamp(local.y + 0.1f, minHeight, maxHeight);

            controller.radius = capsuleRadius;
            controller.height = height;

            Transform trackingRoot = rig != null ? rig.transform : null;
            if (trackingRoot == null || trackingRoot == transform || !trackingRoot.IsChildOf(transform))
            {
                // Sin un rig hijo que compensar: mover el centro de la
                // capsula hasta la cabeza (version simple).
                controller.center = new Vector3(local.x, height * 0.5f, local.z);
                return;
            }

            controller.center = new Vector3(0f, height * 0.5f, 0f);

            // La cabeza se alejo del centro de la capsula (el jugador camino en
            // su espacio real): mover el cuerpo hasta ahi con colisiones y
            // devolver el rig lo que el cuerpo avanzo, para que la camara siga
            // exactamente donde esta la cabeza.
            Vector3 offset = head.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < HeadFollowThreshold * HeadFollowThreshold)
            {
                return;
            }

            Vector3 before = transform.position;
            controller.Move(offset);
            if (ClimbedFurniture(before))
            {
                // Caminando en el espacio real contra un mueble: el cuerpo no
                // se sube; la cabeza sigue donde esta.
                UndoMove(before);
                return;
            }

            Vector3 moved = transform.position - before;
            moved.y = 0f;
            trackingRoot.position -= moved;
        }

        // El paso subio la capsula sobre un mueble (mas alto que el piso de
        // lo que se considera estar parado en el).
        private bool ClimbedFurniture(Vector3 before)
        {
            return hasFloor && transform.position.y > floorY + maxStandAboveFloor
                && transform.position.y > before.y + 0.05f;
        }

        private void UndoMove(Vector3 before)
        {
            controller.enabled = false;
            transform.position = before;
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private const float HeadFollowThreshold = 0.01f;
    }
}
