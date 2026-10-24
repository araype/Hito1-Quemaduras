using UnityEngine;

namespace SojaExiles
{
    // Mantiene el objeto sobre otro que se mueve (por ejemplo, el brillo de
    // la guia sobre la taza o sobre la mano de la paciente). Corre despues de
    // que se arma la pose de la paciente (PatientBody).
    [DefaultExecutionOrder(320)]
    public class FollowTarget : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;

        private void LateUpdate()
        {
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }
    }
}
