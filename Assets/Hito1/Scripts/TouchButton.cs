using UnityEngine;

namespace SojaExiles
{
    // Boton de un panel que se aprieta dejando la mano (la esfera fisica de
    // SimpleGrab) dentro de el un momento: una barra se llena de izquierda a
    // derecha y al completarse hace clic y vibra. Si la mano sale antes, la
    // barra se vacia. Asi todos los botones cuestan lo mismo y un roce al
    // pasar no elige nada. Despues de apretarlo hay que sacar la mano para
    // volver a usarlo.
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
        private Vector2 size;
        private float enabledTime;
        private float nextPress;
        private float flashUntil;
        private float held;
        private bool waitExit;
        private int lastHand = -1;
        private float nextTick;
        private readonly Collider[] overlap = new Collider[16];

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
            enabledTime = Time.time;
            held = 0f;
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void Start()
        {
            BuildFill();
        }

        // Barra de llenado sobre el fondo del boton (por detras del texto).
        private void BuildFill()
        {
            if (background == null)
            {
                return;
            }

            size = new Vector2(box.size.x, box.size.y);
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
            bool armed = Time.time - enabledTime >= armDelay && Time.time >= nextPress;
            if (hand < 0)
            {
                waitExit = false;
            }

            if (hand >= 0 && armed && !waitExit)
            {
                held += Time.deltaTime;
                lastHand = hand;
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + 0.12f;
                    Buzz(hand, 0.12f + 0.2f * Progress, 0.03f);
                }

                if (held >= holdSeconds)
                {
                    Press();
                }
            }
            else
            {
                held = Mathf.MoveTowards(held, 0f, Time.deltaTime * 2f);
            }

            IndicatorUtil.SetColor(background, Time.time < flashUntil ? pressedColor
                : hand >= 0 && armed ? Color.Lerp(idleColor, Color.white, 0.12f) : idleColor);
            if (fill != null)
            {
                fill.localScale = new Vector3(size.x * Progress, 1f, 1f);
                fillRenderer.enabled = Progress > 0.001f;
            }
        }

        private void Press()
        {
            held = 0f;
            waitExit = true;
            nextPress = Time.time + cooldown;
            flashUntil = Time.time + 0.25f;
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

            Vector3 center = transform.TransformPoint(box.center);
            Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            int n = Physics.OverlapBoxNonAlloc(center, half, overlap, transform.rotation, ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (SimpleGrab.IsHandCollider(overlap[i]))
                {
                    return SimpleGrab.IsLeftHandCollider(overlap[i]) ? 0 : 1;
                }
            }

            return -1;
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
