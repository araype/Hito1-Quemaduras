using UnityEngine;

namespace SojaExiles
{
    // Mueve el CharacterController del rig con el joystick izquierdo, en la
    // dirección hacia donde mira la cabeza (yaw), y aplica gravedad para que
    // el jugador choque con paredes, muebles y piso en vez de atravesarlos.
    // El giro de la vista (joystick derecho) lo sigue manejando el rig de Meta.
    [RequireComponent(typeof(CharacterController))]
    public class VRLocomotion : MonoBehaviour
    {
        public float moveSpeed = 1.8f;
        public float gravity = -9.81f;

        private CharacterController controller;
        private Transform head;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }

            Vector2 axis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

            Vector3 move = Vector3.zero;
            if (head != null)
            {
                Vector3 forward = head.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = head.right;
                right.y = 0f;
                right.Normalize();

                move = forward * axis.y + right * axis.x;
            }

            if (controller.isGrounded)
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
    }
}
