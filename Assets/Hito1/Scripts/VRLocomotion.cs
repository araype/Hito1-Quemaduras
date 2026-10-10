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
    //    girar a la izquierda. El indice queda libre para agarrar.
    //
    // La capsula del CharacterController se mantiene sincronizada con la
    // cabeza (posicion y altura reales del jugador), asi la camara choca
    // contra paredes y muebles donde de verdad esta, no donde esta el centro
    // del rig.
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

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            ResolveReferences();

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

            if (rig != null && leftHand != null && rightHand != null)
            {
                return;
            }

            // El rig de Meta se arma en runtime; reintentar con calma.
            nextLookup = Time.unscaledTime + 0.5f;

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
            if (moveInput.sqrMagnitude <= 0f && leftHand != null && leftHand.IsTracked)
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

            if (Mathf.Approximately(turnInput, 0f) && rightHand != null && rightHand.IsTracked)
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
            controller.center = new Vector3(local.x, height * 0.5f, local.z);
        }
    }
}
