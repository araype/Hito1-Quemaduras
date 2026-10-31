using UnityEngine;

namespace SojaExiles
{
    // Ajustes opcionales de agarre para un Rigidbody que SimpleGrab puede
    // tomar. Sin este componente, SimpleGrab igual agarra cualquier Rigidbody
    // dinamico con valores por defecto; con el, tambien se puede agarrar un
    // Rigidbody kinematico (p.ej. el objetivo IK de la mano del paciente).
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        // Si es false, el objeto solo se traslada con la mano y mantiene su
        // orientacion (util para la mano del paciente, que no debe torcerse).
        public bool trackRotation = true;

        // Si es true, al soltarlo queda quieto donde se dejo (kinematico) en
        // vez de caer por gravedad.
        public bool freezeOnRelease;

        // Si el objeto cae por debajo de su altura inicial menos esta
        // distancia (se salio del escenario), vuelve a su posicion inicial.
        public bool respawnIfLost = true;
        public float respawnDrop = 3f;

        // Distancia entre la mano y el objeto a partir de la cual SimpleGrab
        // lo suelta (negativo: usa la de SimpleGrab). La mano de la paciente
        // usa una mayor para que no se escape mientras ella camina.
        public float breakDistance = -1f;

        public bool IsHeld { get; internal set; }

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        // Toma la pose actual como lugar al que vuelve si se pierde (para
        // objetos creados en runtime y ubicados despues de crearlos).
        public void SetHome()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (!respawnIfLost || IsHeld || transform.position.y > startPosition.y - respawnDrop)
            {
                return;
            }

            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.position = startPosition;
            body.rotation = startRotation;
            transform.SetPositionAndRotation(startPosition, startRotation);
        }
    }
}
