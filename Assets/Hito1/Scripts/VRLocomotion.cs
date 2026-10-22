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
        public float moveSpeed = 1.8f;
        public float gravity = -9.81f;

        [Header("Entrada")]
        public float deadZone = 0.15f;
        public bool enableSnapTurn = true;
        public float snapTurnAngle = 45f;
        public float snapTurnThreshold = 0.7f;
        public bool enableHandPinchLocomotion = true;
        public float pinchStrengthThreshold = 0.8f;

        [Header("Capsula")]
        public float capsuleRadius = 0.2f;
        public float minHeight = 0.9f;
        public float maxHeight = 2.2f;

        private CharacterController controller;
        private OVRCameraRig rig;
        private OVRHand leftHand;
        private OVRHand rightHand;
        private float verticalVelocity;
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

            Vector3 velocity = move * moveSpeed;
            velocity.y = verticalVelocity;

            controller.Move(velocity * Time.deltaTime);
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

        private Vector2 ApplyDeadZone(Vector2 axis)
        {
            if (axis.magnitude < deadZone)
            {
                return Vector2.zero;
            }

            return axis;
        }

        private void HandleSnapTurn(Transform head, float turnInput)
        {
            if (Mathf.Abs(turnInput) >= snapTurnThreshold)
            {
                if (snapTurnReady)
                {
                    snapTurnReady = false;
                    Vector3 pivot = head != null ? head.position : transform.position;
                    transform.RotateAround(pivot, Vector3.up, Mathf.Sign(turnInput) * snapTurnAngle);
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
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            trackingRoot.position -= moved;
        }

        private const float HeadFollowThreshold = 0.01f;
    }
}
