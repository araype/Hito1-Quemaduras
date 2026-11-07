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

        // Si queda quieto en el suelo (se cayo del meson), vuelve a su lugar
        // despues de unos segundos: si no, no hay forma de recuperarlo.
        public bool returnFromFloor = true;
        public float floorReturnSeconds = 2f;

        // Distancia entre la mano y el objeto a partir de la cual SimpleGrab
        // lo suelta (negativo: usa la de SimpleGrab). La mano de la paciente
        // usa una mayor para que no se escape mientras ella camina.
        public float breakDistance = -1f;

        // Seguir a la mano con retraso, como un resorte (por segundo; 0 = al
        // instante) y con una velocidad maxima (0 = la de SimpleGrab).
        public float followSharpness;
        public float maxFollowSpeed;

        public bool IsHeld { get; internal set; }

        // Lugar al que vuelve (lo usa tambien FloorRecovery, para que los dos
        // devuelvan el objeto al mismo sitio).
        public Vector3 HomePosition
        {
            get { return startPosition; }
        }

        public Quaternion HomeRotation
        {
            get { return startRotation; }
        }

        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float floorY = float.NaN;
        private float onFloorTime;

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
            floorY = float.NaN;
        }

        private void FixedUpdate()
        {
            if (OnFloorTooLong())
            {
                Respawn();
                return;
            }

            if (!respawnIfLost || IsHeld || transform.position.y > startPosition.y - respawnDrop)
            {
                return;
            }

            Respawn();
        }

        private bool OnFloorTooLong()
        {
            if (!returnFromFloor || IsHeld || body.isKinematic)
            {
                onFloorTime = 0f;
                return false;
            }

            if (float.IsNaN(floorY))
            {
                floorY = MeasureFloor();
            }

            bool resting = transform.position.y < floorY + 0.25f && body.velocity.sqrMagnitude < 0.04f;
            onFloorTime = resting ? onFloorTime + Time.fixedDeltaTime : 0f;
            return onFloorTime > floorReturnSeconds;
        }

        // Altura del suelo bajo el lugar de inicio: la superficie horizontal
        // mas baja. Si el objeto empieza en el suelo, no aplica.
        private float MeasureFloor()
        {
            float lowest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(startPosition + Vector3.up * 0.05f, Vector3.down, 4f, ~0,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.8f && !hit.collider.transform.IsChildOf(transform) && hit.point.y < lowest)
                {
                    lowest = hit.point.y;
                }
            }

            return startPosition.y - lowest < 0.3f ? float.NegativeInfinity : lowest;
        }

        private void Respawn()
        {
            onFloorTime = 0f;
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
