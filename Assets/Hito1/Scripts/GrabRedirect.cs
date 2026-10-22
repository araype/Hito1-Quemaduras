using UnityEngine;

namespace SojaExiles
{
    // Va sobre un collider que no es agarrable por si mismo (p.ej. el
    // antebrazo del paciente) y hace que SimpleGrab tome otro Rigidbody en su
    // lugar (el objetivo IK de la mano). Asi agarrar el brazo mueve la mano.
    [RequireComponent(typeof(Collider))]
    public class GrabRedirect : MonoBehaviour
    {
        public Rigidbody target;

        // Solo se puede agarrar si el objetivo tiene algun collider activo
        // (ManoHeridaVisibility los apaga hasta que ocurre el corte).
        public bool CanGrab
        {
            get
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    return false;
                }

                foreach (Collider col in target.GetComponentsInChildren<Collider>())
                {
                    if (col.enabled && !col.isTrigger && col.attachedRigidbody == target)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
