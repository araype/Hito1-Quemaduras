using UnityEngine;

namespace SojaExiles
{
    // Rollo de film transparente. Con una mano se sostiene el rollo y con la
    // otra se tira el borde: sale una tira (FilmStrip) que se corta al llegar
    // a 'tearLength'. Asi se cubre la quemadura: tiras a lo largo, sueltas,
    // sin envolver (NHS, Cruz Roja, guia LSEBN).
    // Mientras el rollo esta en una mano, agarrar el rollo con la otra toma el
    // borde de la tira (GrabRedirect), no pasa el rollo de mano.
    // El rollo se arma tumbado: eje a lo largo de X local, centrado en
    // 'axisCenter'. Va en la raiz del rollo (BurnSceneProps).
    [RequireComponent(typeof(Rigidbody))]
    public class FilmRoll : MonoBehaviour
    {
        public float stripWidth = 0.06f;
        [Tooltip("Largo al que se corta la tira (m).")]
        public float tearLength = 0.2f;
        [Tooltip("Si se suelta el rollo mientras se tira, la tira se corta si ya mide esto (m).")]
        public float minKeepLength = 0.08f;
        public float radius = 0.026f;
        public float halfLength = 0.15f;
        public Vector3 axisCenter = new Vector3(0f, 0.026f, 0f);

        private Rigidbody body;
        private Grabbable grabbable;
        private GrabRedirect redirect;
        private Collider[] ownColliders;
        private FilmStrip strip;
        private bool pulling;

        public Vector3 Axis
        {
            get { return transform.right; }
        }

        private void Start()
        {
            body = GetComponent<Rigidbody>();
            grabbable = GetComponent<Grabbable>();
            ownColliders = GetComponentsInChildren<Collider>();
            redirect = gameObject.AddComponent<GrabRedirect>();
            redirect.target = body;
        }

        // Punto del borde del rollo mas cercano a 'point' (de donde sale la tira).
        public Vector3 EdgePoint(Vector3 point)
        {
            Vector3 center = transform.TransformPoint(axisCenter);
            Vector3 axis = Axis;
            float along = Mathf.Clamp(Vector3.Dot(point - center, axis), -halfLength + stripWidth * 0.5f,
                halfLength - stripWidth * 0.5f);
            Vector3 onAxis = center + axis * along;
            Vector3 radial = Vector3.ProjectOnPlane(point - onAxis, axis);
            if (radial.sqrMagnitude < 1e-6f)
            {
                radial = transform.up;
            }

            return onAxis + radial.normalized * radius;
        }

        private void Update()
        {
            bool held = grabbable != null && grabbable.IsHeld;
            if (!held)
            {
                redirect.target = body;
                if (strip != null && !strip.Torn)
                {
                    float length = Vector3.Distance(EdgePoint(strip.transform.position), strip.transform.position);
                    if (strip.IsHeld && length >= minKeepLength)
                    {
                        strip.Tear(EdgePoint(strip.transform.position), Axis);
                        strip = null;
                    }
                    else
                    {
                        Destroy(strip.gameObject);
                        strip = null;
                    }
                }

                pulling = false;
                return;
            }

            bool leftHolds = SimpleGrab.IsHeldBy(body, true);
            Vector3 freeHand;
            if (!SimpleGrab.TryGetHand(!leftHolds, out freeHand, out bool _))
            {
                freeHand = transform.TransformPoint(axisCenter) + Vector3.up * 0.1f;
            }

            if (strip == null)
            {
                strip = FilmStrip.Create(this, EdgePoint(freeHand));
                foreach (Collider mine in ownColliders)
                {
                    foreach (Collider other in strip.GetComponentsInChildren<Collider>())
                    {
                        if (mine != null && other != null)
                        {
                            Physics.IgnoreCollision(mine, other, true);
                        }
                    }
                }
            }

            if (!strip.IsHeld)
            {
                // El borde espera en la superficie del rollo, del lado de la
                // mano libre.
                pulling = false;
                strip.transform.position = EdgePoint(freeHand);
                redirect.target = strip.GetComponent<Rigidbody>();
                return;
            }

            redirect.target = body;
            Vector3 edge = EdgePoint(strip.transform.position);
            float pulled = Vector3.Distance(edge, strip.transform.position);
            if (!pulling)
            {
                pulling = true;
                Hito1FX.PlayAt("film_estirar", edge, 0.35f);
            }

            if (pulled >= tearLength)
            {
                strip.Tear(edge, Axis);
                strip = null;
                pulling = false;
            }
        }

        // La tira se destruyo sin cortarse (por ejemplo, al reiniciar).
        public void StripGone(FilmStrip gone)
        {
            if (strip == gone)
            {
                strip = null;
            }
        }

        private void OnDestroy()
        {
            if (strip != null && !strip.Torn)
            {
                Destroy(strip.gameObject);
            }
        }
    }
}
