using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Anillo en el dedo anular de la mano quemada. El protocolo pide sacarlo
    // antes de que la mano se hinche: se saca dejando la mano sobre el anillo
    // un momento (un punto dorado marca donde, como en el grifo). Si la mano
    // ya se hincho demasiado (BurnInjury), el anillo no sale.
    // Al salir queda como objeto suelto que se puede agarrar.
    // Va en el paciente (el objeto con PatientBody).
    [RequireComponent(typeof(PatientBody))]
    public class PatientRing : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public bool rightHand = true;

        [Header("Forma")]
        public float innerRadius = 0.0095f;
        public float bandThickness = 0.0016f;
        [Tooltip("Donde va el anillo a lo largo de la primera falange (0 = nudillo).")]
        [Range(0f, 1f)] public float alongFinger = 0.35f;

        [Header("Sacarlo")]
        [Tooltip("Segundos que hay que dejar la mano sobre el anillo.")]
        public float holdSeconds = 1.2f;
        public float touchRadius = 0.035f;
        [Tooltip("Con la mano hinchada por encima de esto (0 a 1), ya no sale.")]
        [Range(0f, 1f)] public float stuckSwelling = 0.6f;

        [Header("Indicador")]
        public Material indicatorMaterial;
        public float markerRadius = 0.014f;
        public Color markerColor = new Color(1f, 0.78f, 0.15f, 0.65f);

        public bool Removed { get; private set; }
        public bool Stuck { get; private set; }

        private PatientBody body;
        private Transform finger, nextBone;
        private Transform ring;
        private Renderer marker;
        private float held;
        private float lastTouch = float.NegativeInfinity;
        private bool busy;
        private readonly Collider[] overlap = new Collider[16];

        private void Awake()
        {
            body = GetComponent<PatientBody>();
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            Transform hand = body.IsReady ? body.HandBone(rightHand) : null;
            finger = FindBone(hand, "Ring1");
            nextBone = FindBone(hand, "Ring2");
            if (finger == null)
            {
                Debug.LogWarning("[PatientRing] No se encontro el dedo anular de la mano.", this);
                enabled = false;
                return;
            }

            ring = PropKit.MeshPart(finger, PropKit.Torus("Anillo", innerRadius + bandThickness, bandThickness),
                Vector3.zero, Quaternion.identity, "Oro", new Color(0.95f, 0.75f, 0.3f)).transform;
            ring.name = "Anillo";
            PlaceOnFinger(0f);

            marker = IndicatorUtil.CreateSphere("PuntoAnillo", ring, ring.position, markerRadius, indicatorMaterial,
                markerColor);
            marker.enabled = false;
        }

        private static Transform FindBone(Transform hand, string suffix)
        {
            if (hand == null)
            {
                return null;
            }

            foreach (Transform t in hand.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(suffix))
                {
                    return t;
                }
            }

            return null;
        }

        // Ubica el anillo sobre la falange; 'slide' lo corre hacia la punta
        // del dedo (en metros).
        private void PlaceOnFinger(float slide)
        {
            Vector3 dir = nextBone != null ? (nextBone.position - finger.position) : finger.up;
            float length = dir.magnitude;
            dir = length > 1e-5f ? dir / length : finger.up;
            ring.position = finger.position + dir * (length * alongFinger + slide);
            ring.rotation = Quaternion.FromToRotation(Vector3.up, dir);

            // Tamano fijo en el mundo: el metal no se agranda con la mano
            // hinchada (el dedo si).
            Vector3 p = finger.lossyScale;
            float s = Mathf.Max(1e-5f, (Mathf.Abs(p.x) + Mathf.Abs(p.y) + Mathf.Abs(p.z)) / 3f);
            ring.localScale = Vector3.one / s;
        }

        private void LateUpdate()
        {
            if (ring == null || Removed)
            {
                return;
            }

            if (!busy)
            {
                PlaceOnFinger(0f);
            }

            bool active = burnSequence != null && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle
                && !Stuck && !busy;
            if (marker != null)
            {
                marker.enabled = active;
                if (active)
                {
                    float fill = held / Mathf.Max(0.1f, holdSeconds);
                    float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 4f);
                    marker.transform.position = ring.position;
                    IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * (pulse + fill));
                }
            }

            if (!active)
            {
                held = 0f;
                return;
            }

            if (HandTouching())
            {
                lastTouch = Time.time;
            }

            if (Time.time - lastTouch > 0.25f)
            {
                held = 0f;
                return;
            }

            held += Time.deltaTime;
            if (held >= holdSeconds)
            {
                held = 0f;
                BurnInjury injury = BurnInjury.Instance;
                bool swollen = injury != null && injury.Swelling >= stuckSwelling;
                StartCoroutine(swollen ? TryStuck() : SlideOff());
            }
        }

        private bool HandTouching()
        {
            int n = Physics.OverlapSphereNonAlloc(ring.position, touchRadius, overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (SimpleGrab.IsFreeHandCollider(overlap[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator SlideOff()
        {
            busy = true;
            Hito1FX.PlayAt("anillo_sale", ring.position, 0.7f);
            float distance = nextBone != null ? Vector3.Distance(finger.position, nextBone.position) * 2.6f : 0.06f;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                PlaceOnFinger(distance * Mathf.SmoothStep(0f, 1f, t / 0.5f));
                yield return null;
            }

            Removed = true;
            if (marker != null)
            {
                Destroy(marker.gameObject);
            }

            // Queda como objeto suelto: cae y se puede recoger.
            GameObject loose = PropKit.Grabbable("AnilloSuelto", ring.position, ring.rotation,
                new Vector3(0.024f, 0.006f, 0.024f), 0.01f);
            loose.GetComponent<BoxCollider>().center = Vector3.zero;
            ring.SetParent(loose.transform, true);
            ring.localPosition = Vector3.zero;
            ring.localRotation = Quaternion.identity;
            ring.localScale = Vector3.one;
            busy = false;

            if (burnSequence != null)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.RingRemoved);
            }
        }

        // La mano esta hinchada: el anillo avanza un poco y se traba.
        private IEnumerator TryStuck()
        {
            busy = true;
            Stuck = true;
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                PlaceOnFinger(0.003f * Mathf.Sin(Mathf.Clamp01(t / 0.6f) * Mathf.PI));
                yield return null;
            }

            busy = false;
            if (burnSequence != null)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.RingStuck, null, BurnSequenceManager.Severity.Error);
            }
        }
    }
}
