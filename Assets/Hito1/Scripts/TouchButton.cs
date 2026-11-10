using UnityEngine;

namespace SojaExiles
{
    // Boton de un panel que se aprieta dejando la punta del indice (o la
    // esfera fisica de la mano de SimpleGrab) dentro de el un momento: una barra se llena de izquierda a
    // derecha y al completarse hace clic y vibra. Si la mano sale antes, la
    // barra se vacia. Asi todos los botones cuestan lo mismo y un roce al
    // pasar no elige nada. Despues de apretarlo hay que sacar la mano para
    // volver a usarlo.
    // Usa el tiempo sin escalar: funciona tambien con el juego en pausa
    // (PauseMenu), y entonces mira la posicion de la mano en vez de su esfera
    // fisica, que no se mueve sin FixedUpdate.
    [RequireComponent(typeof(BoxCollider))]
    public class TouchButton : MonoBehaviour
    {
        public System.Action OnPressed;
        public Renderer background;
        public Color idleColor = new Color(0.16f, 0.42f, 0.78f);
        public Color pressedColor = new Color(0.45f, 0.75f, 1f);
        [Tooltip("Segundos que hay que mantener la mano dentro del boton.")]
        public float holdSeconds = 0.6f;
        public float cooldown = 0.8f;
        [Tooltip("Segundos desde que aparece hasta que se puede apretar (evita toques sin querer).")]
        public float armDelay = 0.8f;

        private BoxCollider box;
        private Transform fill;
        private Renderer fillRenderer;
        private RoundedPanel roundedFill;
        private Vector2 size;
        private float enabledTime;
        private float nextPress;
        private float flashUntil;
        private float held;
        private bool waitExit;
        private int lastHand = -1;
        private float nextTick;
        private readonly Collider[] overlap = new Collider[16];

        // Hay una mano dentro del boton (lo usa el cursor del panel).
        public bool Hovered { get; private set; }

        // 0 a 1: cuanto se lleno la barra.
        public float Progress
        {
            get { return Mathf.Clamp01(held / Mathf.Max(0.05f, holdSeconds)); }
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            enabledTime = Time.unscaledTime;
            held = 0f;
            // Si el panel aparece donde ya esta la mano (por ejemplo, mientras
            // sostiene el brazo de la paciente), no cuenta: hay que sacarla y
            // volver a entrar.
            waitExit = true;
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void Start()
        {
            BuildFill();

            // Un cursor por panel muestra donde esta la punta del dedo.
            Transform panel = PanelRoot();
            if (panel != null && panel.GetComponent<PanelFingerCursor>() == null)
            {
                panel.gameObject.AddComponent<PanelFingerCursor>();
            }
        }

        // Raiz del panel: la que se dibuja encima (OverlayPanel) o, si no,
        // el padre del boton.
        private Transform PanelRoot()
        {
            OverlayPanel overlay = GetComponentInParent<OverlayPanel>();
            return overlay != null ? overlay.transform : transform.parent;
        }

        // Barra de llenado sobre el fondo del boton (por detras del texto).
        private void BuildFill()
        {
            if (background == null)
            {
                return;
            }

            size = new Vector2(box.size.x, box.size.y);
            RoundedPanel roundedBackground = background.GetComponent<RoundedPanel>();
            if (roundedBackground != null)
            {
                // Boton redondeado: la barra tambien es una pildora que crece.
                roundedFill = RoundedPanel.Create(transform, "Llenado", Vector2.zero, new Vector2(size.y, size.y),
                    -0.001f, roundedBackground.radius, 0f, 0f);
                roundedFill.sheen = 0f;
                roundedFill.SetSolid(Color.Lerp(idleColor, new Color(0.02f, 0.05f, 0.12f), 0.45f), 0f);
                fillRenderer = roundedFill.Renderer;
                fillRenderer.enabled = false;
                return;
            }

            GameObject go = new GameObject("Llenado");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-size.x * 0.5f, 0f, -0.001f);
            fill = go.transform;
            fillRenderer = VRPanelKit.Background(fill, new Vector2(0.5f, 0f), new Vector2(1f, size.y), "Panel_Boton",
                pressedColor);
            // Mas oscura que el boton: el texto blanco se sigue leyendo.
            IndicatorUtil.SetColor(fillRenderer, Color.Lerp(idleColor, new Color(0.02f, 0.05f, 0.12f), 0.55f));
            fill.localScale = new Vector3(0f, 1f, 1f);
        }

        private void Update()
        {
            int hand = HandInside();
            Hovered = hand >= 0;
            float now = Time.unscaledTime;
            bool armed = now - enabledTime >= armDelay && now >= nextPress;
            if (hand < 0)
            {
                waitExit = false;
            }

            if (hand >= 0 && armed && !waitExit)
            {
                held += Time.unscaledDeltaTime;
                lastHand = hand;
                if (now >= nextTick)
                {
                    nextTick = now + 0.12f;
                    Buzz(hand, 0.12f + 0.2f * Progress, 0.03f);
                }

                if (held >= holdSeconds)
                {
                    Press();
                }
            }
            else
            {
                held = Mathf.MoveTowards(held, 0f, Time.unscaledDeltaTime * 2f);
            }

            IndicatorUtil.SetColor(background, now < flashUntil ? pressedColor
                : hand >= 0 && armed ? Color.Lerp(idleColor, Color.white, 0.12f) : idleColor);
            if (roundedFill != null)
            {
                float w = Mathf.Max(size.y, size.x * Progress);
                roundedFill.SetRect(new Vector2(-size.x * 0.5f + w * 0.5f, 0f), new Vector2(w, size.y));
                fillRenderer.enabled = Progress > 0.001f;
            }
            else if (fill != null)
            {
                fill.localScale = new Vector3(size.x * Progress, 1f, 1f);
                fillRenderer.enabled = Progress > 0.001f;
            }
        }

        private void Press()
        {
            held = 0f;
            waitExit = true;
            nextPress = Time.unscaledTime + cooldown;
            flashUntil = Time.unscaledTime + 0.25f;
            Hito1FX.PlayAt("perilla_clic", transform.position, 0.7f, 0.02f);
            Buzz(lastHand, 0.5f, 0.06f);
            OnPressed?.Invoke();
        }

        // Mano del jugador dentro del boton: 0 izquierda, 1 derecha, -1
        // ninguna. Se consulta cada cuadro (los avisos de trigger se pierden
        // si la mano queda quieta).
        private int HandInside()
        {
            if (box == null)
            {
                return -1;
            }

            // Primero la punta del indice: es con lo que uno toca un boton.
            int tip = FingertipInside();
            if (tip >= 0)
            {
                return tip;
            }

            if (Time.timeScale <= 0f)
            {
                return HandPointInside();
            }

            Vector3 center = transform.TransformPoint(box.center);
            Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            int n = Physics.OverlapBoxNonAlloc(center, half, overlap, transform.rotation, ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (SimpleGrab.IsHandCollider(overlap[i]))
                {
                    // Si esa mano tiene punta de dedo, solo cuenta el dedo: la
                    // esfera de la palma pasaba por los botones de arriba al
                    // bajar a otro y los marcaba sin querer.
                    bool left = SimpleGrab.IsLeftHandCollider(overlap[i]);
                    if (SimpleGrab.TryGetFingertip(left, out Vector3 _))
                    {
                        continue;
                    }

                    return left ? 0 : 1;
                }
            }

            return -1;
        }

        // La punta del indice de cada mano dentro de la caja del boton.
        private int FingertipInside()
        {
            for (int h = 0; h < 2; h++)
            {
                if (!SimpleGrab.TryGetFingertip(h == 0, out Vector3 p) || !PointInside(p, 0.008f))
                {
                    continue;
                }

                // Mas fino que la caja (que es honda para la esfera de la
                // mano): el dedo cuenta desde 3,5 cm delante del boton hasta
                // 3 cm detras (si lo atraviesa).
                float depth = Vector3.Dot(p - transform.position, transform.forward);
                if (depth >= -0.035f && depth <= 0.03f)
                {
                    return h;
                }
            }

            return -1;
        }

        // Sin fisica: el punto de agarre de cada mano dentro de la caja del
        // boton (con un margen del tamano de la esfera de la mano).
        private int HandPointInside()
        {
            for (int h = 0; h < 2; h++)
            {
                if (SimpleGrab.TryGetHand(h == 0, out Vector3 p, out bool _) && PointInside(p, 0.02f))
                {
                    return h;
                }
            }

            return -1;
        }

        private bool PointInside(Vector3 p, float margin)
        {
            Vector3 local = transform.InverseTransformPoint(p) - box.center;
            Vector3 scale = transform.lossyScale;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x * scale.x) <= Mathf.Abs(half.x * scale.x) + margin
                && Mathf.Abs(local.y * scale.y) <= Mathf.Abs(half.y * scale.y) + margin
                && Mathf.Abs(local.z * scale.z) <= Mathf.Abs(half.z * scale.z) + margin;
        }

        private static BurnHaptics haptics;

        private static void Buzz(int hand, float amplitude, float seconds)
        {
            if (haptics == null)
            {
                haptics = FindObjectOfType<BurnHaptics>();
            }

            if (haptics != null)
            {
                haptics.Buzz(hand, amplitude, seconds);
            }
        }
    }
}
