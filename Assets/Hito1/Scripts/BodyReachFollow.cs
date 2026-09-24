using UnityEngine;

namespace SojaExiles
{
    // Si el objetivo (la mano quemada que el jugador arrastra) se aleja mas
    // de lo que el brazo puede alcanzar, desplaza el cuerpo completo en el
    // plano horizontal para cerrar la distancia. No es locomocion real
    // (no hay animacion de caminata): es un "empujon" simple que simula que
    // el personaje se acerca a donde lo estas guiando, para que el agua y el
    // panuelo sigan siendo alcanzables aunque esten lejos de la olla.
    public class BodyReachFollow : MonoBehaviour
    {
        public Transform shoulderBone;
        public Transform target;
        public float maxReach = 0.4f;
        public float followSpeed = 1.5f;

        private void Update()
        {
            if (shoulderBone == null || target == null)
            {
                return;
            }

            Vector3 toTarget = target.position - shoulderBone.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= maxReach)
            {
                return;
            }

            float overreach = distance - maxReach;
            Vector3 desiredOffset = toTarget.normalized * overreach;

            transform.position = Vector3.MoveTowards(
                transform.position,
                transform.position + desiredOffset,
                followSpeed * Time.deltaTime);
        }
    }
}
