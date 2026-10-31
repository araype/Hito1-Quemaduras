using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Anillo en el dedo anular de la mano quemada. El protocolo pide sacarlo
    // antes de que la mano se hinche: se saca acercando la mano libre al
    // anillo (un punto dorado marca donde) y manteniendo apretado el boton
    // lateral o la pinza, como al agarrar. Mientras se aprieta el anillo
    // avanza por el dedo y el control vibra; si se suelta antes, vuelve.
    // Apretar ahi no agarra la mano de la paciente (SimpleGrab.AddGrabBlocker).
    // Si la mano ya se hincho demasiado (BurnInjury), el anillo no sale.
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
        [Tooltip("Segundos que hay que mantener apretado junto al anillo.")]
        public float holdSeconds = 1.5f;
        [Tooltip("Distancia del punto de agarre al anillo para tirar de el (m).")]
        public float grabZone = 0.05f;
        [Tooltip("Cuanto avanza el anillo por el dedo mientras se tira (m).")]
        public float pullPreview = 0.012f;
        [Tooltip("Con la mano hinchada por encima de esto (0 a 1), ya no sale.")]
        [Range(0f, 1f)] public float stuckSwelling = 0.6f;

        [Header("Indicador")]
        public Material indicatorMaterial;
        public float markerRadius = 0.014f;
        public Color markerColor = new Color(1f, 0.78f, 0.15f, 0.65f);
        public Color pullColor = new Color(0.35f, 0.95f, 0.45f, 0.8f);

        public bool Removed { get; private set; }
        public bool Stuck { get; private set; }

        private PatientBody body;
        private Transform finger, nextBone;
        private Transform ring;
        private Renderer marker;
        private float held;
        private float nextTick;
        private bool busy;
        private BurnHaptics haptics;

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

        private void OnEnable()
        {
            SimpleGrab.AddGrabBlocker(ClaimsGrab);
        }

        private void OnDisable()
        {
            SimpleGrab.RemoveGrabBlocker(ClaimsGrab);
        }

        private bool Active
        {
            get
            {
                return ring != null && !Removed && !Stuck && !busy && burnSequence != null
                    && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle;
            }
        }

        // Apretar junto al anillo no agarra la mano de la paciente: tira
        // del anillo.
        private bool ClaimsGrab(Vector3 point)
        {
            return Active && Vector3.Distance(point, ring.position) < grabZone;
        }

        private void LateUpdate()
        {
            if (ring == null || Removed)
            {
                return;
            }

            bool active = Active;
            int pulling = -1;
            bool hovering = false;
            if (active)
            {
                for (int h = 0; h < 2; h++)
                {
                    if (!SimpleGrab.TryGetHandInput(h == 0, out Vector3 point, out bool pressed, out bool holding)
                        || holding)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(point, ring.position);
                    if (pressed && distance < grabZone)
                    {
                        pulling = h;
                    }
                    else if (!pressed && distance < grabZone * 1.4f)
                    {
                        hovering = true;
                    }
                }
            }

            if (pulling >= 0)
            {
                held += Time.deltaTime;
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + 0.18f;
                    if (haptics == null)
                    {
                        haptics = FindObjectOfType<BurnHaptics>();
                    }

                    if (haptics != null)
                    {
                        haptics.Buzz(pulling, 0.25f + 0.4f * Progress, 0.05f);
                    }
                }
            }
            else
            {
                // Si suelta antes de tiempo, el anillo vuelve a su lugar.
                held = Mathf.MoveTowards(held, 0f, Time.deltaTime * 2f);
            }

            if (!busy)
            {
                PlaceOnFinger(pullPreview * Progress);
            }

            if (marker != null)
            {
                marker.enabled = active;
                if (active)
                {
                    float pulse = pulling >= 0 ? 1f : 1f + 0.15f * Mathf.Sin(Time.time * 4f);
                    marker.transform.position = ring.position;
                    IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * (pulse + Progress * 0.6f));
                    Color c = pulling >= 0 ? Color.Lerp(markerColor, pullColor, 0.4f + 0.6f * Progress)
                        : hovering ? Color.Lerp(markerColor, Color.white, 0.35f) : markerColor;
                    IndicatorUtil.SetColor(marker, c);
                }
            }

            if (!active || pulling < 0)
            {
                return;
            }

            BurnInjury injury = BurnInjury.Instance;
            bool swollen = injury != null && injury.Swelling >= stuckSwelling;
            if (swollen && held >= holdSeconds * 0.4f)
            {
                held = 0f;
                StartCoroutine(TryStuck());
            }
            else if (held >= holdSeconds)
            {
                StartCoroutine(SlideOff());
            }
        }

        // 0 a 1: cuanto lleva tirando del anillo.
        public float Progress
        {
            get { return Mathf.Clamp01(held / Mathf.Max(0.1f, holdSeconds)); }
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
                PlaceOnFinger(Mathf.Lerp(pullPreview, distance, Mathf.SmoothStep(0f, 1f, t / 0.5f)));
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
