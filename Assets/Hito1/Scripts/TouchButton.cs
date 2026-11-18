using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Boton de un panel, como un boton normal: se toca con el dedo (Poked) o
    // se apunta y se aprieta.
    // Apuntar es tocarlo con la punta del indice (o la esfera de la mano) o
    // apuntarle de lejos con el rayo de la mano (ButtonPointer). Apretar es
    // el gatillo o el boton lateral del control, o la pinza con la mano. Solo
    // tocarlo ya no lo aprieta (antes bastaba dejar la mano 0,6 s dentro y se
    // elegia sin querer). Cuenta el momento de apretar: si ya se tenia
    // apretado al llegar al boton, hay que soltar y volver a apretar. Con
    // esa mano sosteniendo algo no cuenta.
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
        [Tooltip("Sin uso: el boton se aprieta con el gatillo o la pinza, no manteniendo la mano.")]
        public float holdSeconds = 0.6f;
        public float cooldown = 0.5f;
        [Tooltip("Segundos desde que aparece hasta que se puede apretar (evita toques sin querer).")]
        public float armDelay = 0.6f;

        // Botones visibles (los recorre ButtonPointer para el rayo).
        public static readonly List<TouchButton> Active = new List<TouchButton>();

        private BoxCollider box;
        private Transform fill;
        private Renderer fillRenderer;
        private Vector2 size;
        private float enabledTime;
        private float nextPress;
        private float flashUntil;
        private int lastHovered = -1;
        private int lastTouchHand = -1;
        private float lastTouchTime = -10f;
        private const float TouchGrace = 0.4f;
        private readonly Collider[] overlap = new Collider[16];

        // Mano que le apunta con el rayo (-1 ninguna); la pone ButtonPointer.
        public int RayHand { get; set; } = -1;

        // Hay una mano tocandolo o apuntandole (lo usa el cursor del panel).
        public bool Hovered { get; private set; }

        // 1 mientras destella al apretarlo.
        public float Progress
        {
            get { return Time.unscaledTime < flashUntil ? 1f : 0f; }
        }

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            enabledTime = Time.unscaledTime;
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            RayHand = -1;
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            ButtonPointer.Ensure();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            RayHand = -1;
            Hovered = false;
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

        // Destello sobre el fondo del boton al apretarlo (por detras del texto).
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
                RoundedPanel flash = RoundedPanel.Create(transform, "Llenado", Vector2.zero, size,
                    -0.001f, roundedBackground.radius, 0f, 0f);
                flash.sheen = 0f;
                flash.SetSolid(Color.Lerp(idleColor, new Color(0.02f, 0.05f, 0.12f), 0.45f), 0f);
                fillRenderer = flash.Renderer;
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
            IndicatorUtil.SetColor(fillRenderer, Color.Lerp(idleColor, new Color(0.02f, 0.05f, 0.12f), 0.55f));
            fill.localScale = new Vector3(size.x, 1f, 1f);
            fillRenderer.enabled = false;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            int touch = HandInside();
            // Al hacer la pinza la punta del indice se va hacia el pulgar y
            // sale del boton justo al apretar: el toque sigue contando un
            // momento despues de salir.
            if (touch >= 0)
            {
                lastTouchHand = touch;
                lastTouchTime = now;
            }
            else if (lastTouchHand >= 0 && now - lastTouchTime <= TouchGrace)
            {
                touch = lastTouchHand;
            }

            int hand = touch >= 0 ? touch : RayHand;
            Hovered = hand >= 0;
            bool armed = now - enabledTime >= armDelay && now >= nextPress;

            // Al llegar al boton, un toque leve.
            if (hand >= 0 && hand != lastHovered && armed)
            {
                Buzz(hand, 0.15f, 0.02f);
            }

            lastHovered = hand;
            int poke = Poked(now);
            if (armed && poke >= 0)
            {
                Press(poke);
            }
            else if (hand >= 0 && armed && ButtonPointer.PressedDown(hand))
            {
                Press(hand);
            }

            IndicatorUtil.SetColor(background, now < flashUntil ? pressedColor
                : hand >= 0 && armed ? Color.Lerp(idleColor, Color.white, 0.25f) : idleColor);
            if (fillRenderer != null)
            {
                fillRenderer.enabled = now < flashUntil;
            }
        }

        // Tocarlo con el dedo, como en el menu del Quest: la punta del indice
        // llega por delante del boton y lo atraviesa. Con las manos del visor
        // la pinza sobre el boton costaba (al pinzar, el dedo se sale). Para no
        // apretarlo sin querer, el dedo tiene que haber estado delante del
        // boton, dentro de su rectangulo, en el ultimo medio segundo.
        [Tooltip("Cuanto debe pasar el dedo detras del frente del boton para apretarlo (m).")]
        public float pokeDepth = 0.006f;
        private readonly float[] frontAt = { -10f, -10f };

        private int Poked(float now)
        {
            int result = -1;
            for (int h = 0; h < 2; h++)
            {
                // Con control el indice esta en el gatillo: no se "atraviesa"
                // el boton, se acerca el control y se aprieta.
                if (SimpleGrab.UsesController(h == 0) || !SimpleGrab.TryGetFingertip(h == 0, out Vector3 tip)
                    || (SimpleGrab.TryGetHand(h == 0, out Vector3 _, out bool holding) && holding)
                    || !PointInside(tip, 0.008f, false))
                {
                    frontAt[h] = -10f;
                    continue;
                }

                float depth = Vector3.Dot(tip - transform.position, transform.forward);
                if (depth < -0.004f && depth > -0.06f)
                {
                    frontAt[h] = now;
                }
                else if (depth >= pokeDepth && depth < 0.04f && now - frontAt[h] < 0.5f && result < 0)
                {
                    frontAt[h] = -10f;
                    result = h;
                }
            }

            return result;
        }

        private void Press(int hand)
        {
            nextPress = Time.unscaledTime + cooldown;
            flashUntil = Time.unscaledTime + 0.25f;
            ButtonPointer.Consume(hand);
            Hito1FX.PlayAt("perilla_clic", transform.position, 0.7f, 0.02f);
            Buzz(hand, 0.5f, 0.06f);
            OnPressed?.Invoke();
        }

        // Punto del mundo junto a este boton (para que apretar ahi no agarre
        // un objeto que este detras del panel).
        public bool Near(Vector3 point)
        {
            return box != null && isActiveAndEnabled && PointInside(point, 0.03f);
        }

        // Si el rayo corta al boton de frente dentro de su rectangulo (el
        // frente del panel es -Z local) y a que distancia.
        public bool RayHit(Ray ray, float maxDistance, out float distance)
        {
            distance = 0f;
            if (box == null || !isActiveAndEnabled)
            {
                return false;
            }

            Vector3 normal = transform.forward;
            float facing = Vector3.Dot(ray.direction, normal);
            if (facing <= 0.05f)
            {
                return false;
            }

            Vector3 center = transform.TransformPoint(box.center);
            distance = Vector3.Dot(center - ray.origin, normal) / facing;
            if (distance < 0f || distance > maxDistance)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance)) - box.center;
            Vector3 scale = transform.lossyScale;
            return Mathf.Abs(local.x * scale.x) <= Mathf.Abs(box.size.x * scale.x) * 0.5f
                && Mathf.Abs(local.y * scale.y) <= Mathf.Abs(box.size.y * scale.y) * 0.5f;
        }

        // Mano del jugador tocando el boton: 0 izquierda, 1 derecha, -1
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
                    // bajar a otro.
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
                if (SimpleGrab.TryGetFingertip(h == 0, out Vector3 p) && TipTouching(p, SimpleGrab.UsesController(h == 0)))
                {
                    return h;
                }
            }

            return -1;
        }

        // La punta del dedo (o del control) cuenta como tocando el boton.
        // Mas fino que la caja (que es honda para la esfera de la mano): el
        // dedo cuenta desde 3,5 cm delante del boton hasta 3 cm detras. Con
        // control, mas generoso (2 cm alrededor, de 6 cm delante a 5 cm
        // detras): con el control en la mano no se ve bien donde queda la
        // punta del dedo, y antes al acercarlo el rayo se apagaba sin que el
        // toque contara, y el gatillo no hacia nada (2026-11-17).
        public bool TipTouching(Vector3 tip, bool controller)
        {
            if (box == null || !isActiveAndEnabled || !PointInside(tip, controller ? 0.02f : 0.008f, false))
            {
                return false;
            }

            float depth = Vector3.Dot(tip - transform.position, transform.forward);
            return controller ? depth >= -0.06f && depth <= 0.05f : depth >= -0.035f && depth <= 0.03f;
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

        private bool PointInside(Vector3 p, float margin, bool checkDepth = true)
        {
            Vector3 local = transform.InverseTransformPoint(p) - box.center;
            Vector3 scale = transform.lossyScale;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x * scale.x) <= Mathf.Abs(half.x * scale.x) + margin
                && Mathf.Abs(local.y * scale.y) <= Mathf.Abs(half.y * scale.y) + margin
                && (!checkDepth || Mathf.Abs(local.z * scale.z) <= Mathf.Abs(half.z * scale.z) + margin);
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
