using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Anillo en el dedo anular de la mano quemada. El protocolo pide sacarlo
    // antes de que la mano se hinche: se pellizca con la mano libre (boton
    // lateral o pinza; un punto dorado marca donde) y se desliza hacia la
    // punta del dedo. El anillo sigue a la mano y el control vibra al
    // moverlo; si se suelta antes, vuelve. Mantener el pellizco sin moverse
    // tambien lo saca, mas lento (holdSeconds), por si el gesto cuesta con
    // seguimiento de manos.
    // Antes costaba entender cuando y como: ahora, al acercar la mano libre,
    // ella ofrece la mano (PatientBurnArm.ShowHand, si no esta bajo el agua)
    // y junto al anillo aparecen "Pellizcalo y deslizalo hacia la punta" y el circulo
    // vacio del progreso. La zona alcanza casi toda la mano: apretar ahi
    // nunca agarra la mano de la paciente ni la saca del agua
    // (SimpleGrab.AddGrabBlocker).
    // Si la mano ya se hincho demasiado (BurnInjury), el anillo no sale.
    // Al salir queda como objeto suelto que se puede agarrar.
    // Va en el paciente (el objeto con PatientBody).
    // Corre despues de PatientBody (300): antes leia el dedo en la pose de la
    // animacion, antes de que la cinematica inversa lo llevara a donde se ve,
    // y al pellizcar el anillo visible no avanzaba (la mano quedaba lejos del
    // anillo "real").
    [DefaultExecutionOrder(310)]
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
        [Tooltip("Cuanto hay que deslizar la mano hacia la punta del dedo para sacarlo (m).")]
        public float slideDistance = 0.035f;
        [Tooltip("Segundos de pellizco sin moverse que tambien lo sacan (mas lento que deslizarlo).")]
        public float holdSeconds = 3f;
        [Tooltip("Distancia del punto de agarre al anillo para tirar de el (m).")]
        public float grabZone = 0.1f;
        [Tooltip("Distancia de la mano libre al anillo a la que ella ofrece la mano y aparece la indicacion (m).")]
        public float offerDistance = 0.35f;
        [Tooltip("Con la mano hinchada por encima de esto (0 a 1), ya no sale.")]
        // 0,9 con BurnInjury.swellSecondsUntreated 150: se traba tras unos
        // 2 min 15 s sin enfriar (bajo el agua tarda 8 veces mas). Con 0,6
        // y 70 s se trababa a los 42 s y casi nadie llegaba a sacarlo.
        // En primer grado la hinchazon no pasa de 0,55: nunca se traba.
        [Range(0f, 1f)] public float stuckSwelling = 0.9f;

        [Header("Indicador")]
        public Material indicatorMaterial;
        public float markerRadius = 0.0105f;
        public Color markerColor = new Color(1f, 0.82f, 0.3f, 0.32f);
        public Color pullColor = new Color(0.35f, 0.95f, 0.45f, 0.8f);
        [Tooltip("Radio del circulo que se llena mientras se tira del anillo (m).")]
        public float progressRadius = 0.045f;

        public bool Removed { get; private set; }
        public bool Stuck { get; private set; }

        private PatientBody body;
        private Transform finger, nextBone;
        private Transform ring;
        private Renderer marker;
        // 0 a 1: cuanto lleva sacado.
        private float progress;
        private int lastPullHand = -1;
        private Vector3 lastPullPoint;
        private float nextTick;
        private bool busy;
        private BurnHaptics haptics;
        private LineRenderer progressTrack, progressArc;
        private Transform head;
        private Transform hintLabel;
        private PatientBurnArm arm;

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

            progressTrack = CreateArc("ProgresoAnilloFondo", new Color(1f, 1f, 1f, 0.3f), 0.004f);
            progressArc = CreateArc("ProgresoAnillo", pullColor, 0.007f);
            hintLabel = CreateHint();
            arm = GetComponent<PatientBurnArm>();
        }

        // Indicacion junto al anillo (dibujada encima de todo, como los paneles).
        private Transform CreateHint()
        {
            GameObject go = new GameObject("IndicacionAnillo");
            go.AddComponent<NoAutoCollider>();
            go.AddComponent<OverlayPanel>();
            VRPanelKit.CardPanel(go.transform, Vector2.zero, new Vector2(0.15f, 0.056f));
            VRPanelKit.Text(go.transform, "Pellízcalo y deslízalo\nhacia la punta", new Vector2(0f, 0.017f), 0.017f,
                Color.white, TextAnchor.UpperCenter, TextAlignment.Center);
            go.SetActive(false);
            return go.transform;
        }

        // La indicacion queda sobre el anillo, mirando al jugador.
        private void UpdateHint(bool show)
        {
            if (hintLabel == null)
            {
                return;
            }

            if (hintLabel.gameObject.activeSelf != show)
            {
                hintLabel.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            Vector3 toHead = head != null ? head.position - ring.position : Vector3.up;
            Vector3 pos = ring.position + Vector3.up * 0.09f + toHead.normalized * 0.03f;
            hintLabel.position = pos;
            if (head != null)
            {
                hintLabel.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
            }
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

        // Hacia la punta del dedo, en el mundo.
        private Vector3 TipDirection()
        {
            Vector3 dir = nextBone != null ? nextBone.position - finger.position : finger.up;
            return dir.sqrMagnitude > 1e-8f ? dir.normalized : finger.up;
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
        // Mientras una mano esta en el anillo, la otra tampoco agarra la mano
        // quemada (en el simulador las dos pellizcan a la vez y la otra la
        // tomaba de la mano: "De la mano no, me duele mucho").
        private bool ClaimsGrab(Vector3 point)
        {
            if (!Active)
            {
                return false;
            }

            float distance = Vector3.Distance(point, ring.position);
            return distance < grabZone || (handAtRing && distance < engagedZone);
        }

        [Tooltip("Con una mano en el anillo, hasta donde la otra no agarra la mano (m).")]
        public float engagedZone = 0.17f;
        // Una mano libre esta junto al anillo (cuadro anterior).
        private bool handAtRing;

        private void LateUpdate()
        {
            if (ring == null || Removed)
            {
                return;
            }

            bool active = Active;
            int pulling = -1;
            Vector3 pullPoint = Vector3.zero;
            bool hovering = false;
            bool near = false;
            bool atRing = false;
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
                    near |= distance < offerDistance;
                    atRing |= distance < grabZone;
                    if (pressed && distance < grabZone)
                    {
                        pulling = h;
                        pullPoint = point;
                    }
                    else if (!pressed && distance < grabZone * 1.4f)
                    {
                        hovering = true;
                    }
                }
            }

            handAtRing = atRing;
            if (pulling >= 0)
            {
                // Lo que la mano avanzo hacia la punta del dedo desde el
                // cuadro anterior, medido sobre el dedo (si la paciente mueve
                // la mano, no cuenta). Hacia atras no lo devuelve.
                float slid = 0f;
                Vector3 tip = TipDirection();
                if (lastPullHand == pulling)
                {
                    float before = Vector3.Dot(lastPullPoint - finger.position, tip);
                    float now = Vector3.Dot(pullPoint - finger.position, tip);
                    slid = Mathf.Max(0f, now - before);
                }

                lastPullHand = pulling;
                lastPullPoint = pullPoint;
                progress = Mathf.Clamp01(progress + slid / Mathf.Max(0.005f, slideDistance)
                    + Time.deltaTime / Mathf.Max(0.5f, holdSeconds));

                // Vibra mas al deslizarlo (se siente que avanza por el dedo).
                if (Time.time >= nextTick || slid > 0.002f)
                {
                    nextTick = Time.time + 0.15f;
                    if (haptics == null)
                    {
                        haptics = FindObjectOfType<BurnHaptics>();
                    }

                    if (haptics != null)
                    {
                        float amplitude = slid > 0.0005f ? 0.45f + 0.35f * Progress : 0.15f + 0.2f * Progress;
                        haptics.Buzz(pulling, amplitude, 0.05f);
                    }
                }
            }
            else
            {
                // Si lo suelta antes de sacarlo, el anillo vuelve a su lugar.
                lastPullHand = -1;
                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 1.5f);
            }

            if (!busy)
            {
                PlaceOnFinger(slideDistance * Progress);
            }

            if (marker != null)
            {
                // El circulo de progreso sigue en los dos modos: es respuesta
                // a lo que hace el jugador, no una pista.
                bool showMarker = active && IndicatorUtil.HintsVisible;
                marker.enabled = showMarker;
                if (showMarker)
                {
                    float pulse = pulling >= 0 ? 1f : 1f + 0.15f * Mathf.Sin(Time.time * 4f);
                    marker.transform.position = ring.position;
                    IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * (pulse + Progress * 0.3f));
                    Color c = pulling >= 0 ? Color.Lerp(markerColor, pullColor, 0.4f + 0.6f * Progress)
                        : hovering ? Color.Lerp(markerColor, Color.white, 0.35f) : markerColor;
                    IndicatorUtil.SetColor(marker, c);
                }
            }

            // Al acercar la mano libre: ella la ofrece (si no esta bajo el
            // agua) y se ve como sacarlo, antes de empezar a apretar.
            if (active && near && arm != null && arm.Current != PatientBurnArm.Mode.InWater)
            {
                arm.ShowHand(1.5f);
            }

            UpdateHint(active && near);
            UpdateProgress(active && (near || pulling >= 0 || progress > 0.02f));

            if (!active || pulling < 0)
            {
                return;
            }

            BurnInjury injury = BurnInjury.Instance;
            bool swollen = injury != null && injury.Swelling >= stuckSwelling;
            if (swollen && progress >= 0.25f)
            {
                progress = 0f;
                StartCoroutine(TryStuck());
            }
            else if (progress >= 1f)
            {
                StartCoroutine(SlideOff());
            }
        }

        private LineRenderer CreateArc(string name, Color color, float width)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 0;
            line.enabled = false;
            return line;
        }

        // Circulo alrededor del anillo, mirando al jugador, que se llena
        // mientras se mantiene apretado: deja claro que hay que sostener y
        // cuanto falta (antes solo crecia un poco el punto dorado).
        private void UpdateProgress(bool show)
        {
            if (progressArc == null || progressTrack == null)
            {
                return;
            }

            progressTrack.enabled = show;
            progressArc.enabled = show && Progress > 0.001f;
            if (!show)
            {
                return;
            }

            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            Vector3 center = ring.position;
            Vector3 normal = head != null ? (head.position - center).normalized : Vector3.up;
            Vector3 side = Vector3.Cross(Vector3.up, normal);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.Cross(Vector3.forward, normal);
            }

            side.Normalize();
            Vector3 upDir = Vector3.Cross(normal, side).normalized;
            center += normal * 0.01f;
            SetArc(progressTrack, center, side, upDir, 1f);
            Color c = Color.Lerp(markerColor, pullColor, Progress);
            c.a = 0.95f;
            progressArc.startColor = c;
            progressArc.endColor = c;
            SetArc(progressArc, center, side, upDir, Progress);
        }

        private void SetArc(LineRenderer line, Vector3 center, Vector3 side, Vector3 upDir, float fraction)
        {
            const int segments = 40;
            int count = Mathf.Max(2, Mathf.CeilToInt(segments * fraction) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float a = fraction * (i / (float)(count - 1)) * Mathf.PI * 2f;
                line.SetPosition(i, center + (upDir * Mathf.Cos(a) + side * Mathf.Sin(a)) * progressRadius);
            }
        }

        // 0 a 1: cuanto lleva tirando del anillo.
        public float Progress
        {
            get { return progress; }
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
                PlaceOnFinger(Mathf.Lerp(slideDistance, distance, Mathf.SmoothStep(0f, 1f, t / 0.5f)));
                yield return null;
            }

            Removed = true;
            if (marker != null)
            {
                Destroy(marker.gameObject);
            }

            UpdateHint(false);
            UpdateProgress(false);
            if (arm != null)
            {
                arm.EndShow();
            }

            // Queda como objeto suelto: cae y se puede recoger.
            GameObject loose = PropKit.Grabbable("AnilloSuelto", ring.position, ring.rotation,
                new Vector3(0.024f, 0.006f, 0.024f), 0.01f);
            loose.GetComponent<BoxCollider>().center = Vector3.zero;
            ring.SetParent(loose.transform, true);
            ring.localPosition = Vector3.zero;
            ring.localRotation = Quaternion.identity;
            ring.localScale = Vector3.one;
            // Se queda donde cae: su "lugar" era el punto en el aire donde
            // estaba la mano, y al volver ahi caia otra vez, en bucle.
            loose.GetComponent<Grabbable>().returnFromFloor = false;
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
