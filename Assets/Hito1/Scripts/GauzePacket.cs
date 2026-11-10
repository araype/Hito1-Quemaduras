using UnityEngine;

namespace SojaExiles
{
    // Sobre de gasa esteril del botiquin. Se abre como uno real: con el sobre
    // en una mano, se pellizca la esquina marcada con la otra (boton lateral
    // o pinza) y se separan las manos. Al rasgarse, la gasa queda en la mano
    // que tenia el sobre (lista para apoyarla sobre la quemadura, CoverItem)
    // y el sobre roto cae. Sin la otra mano libre no se puede abrir.
    public class GauzePacket : MonoBehaviour
    {
        public const float Width = 0.105f;
        public const float Length = 0.115f;

        [Tooltip("Cuanto hay que separar las manos para rasgarlo (m).")]
        public float tearDistance = 0.05f;
        [Tooltip("Distancia a la esquina para tomarla (m).")]
        public float cornerReach = 0.07f;

        private Rigidbody body;
        private Transform corner;
        private int tearHand = -1;
        private float startGap;
        private float nextTick;
        private bool torn;
        private HoldHint hint;

        public static GameObject Build()
        {
            GameObject root = PropKit.Grabbable("SobreGasa", Vector3.zero, Quaternion.identity,
                new Vector3(Width, 0.008f, Length), 0.02f);
            // Papel con una banda azul y el rotulo; la esquina para rasgar,
            // con una muesca de color.
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.004f, 0f),
                new Vector3(Width, 0.006f, Length), "Sobre_Papel", new Color(0.97f, 0.97f, 0.95f));
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.0072f, -Length * 0.32f),
                new Vector3(Width, 0.001f, 0.022f), "Plastico_Azul", new Color(0.2f, 0.45f, 0.85f));
            Renderer tab = PropKit.Part(root.transform, PrimitiveType.Cube,
                new Vector3(-Width * 0.5f + 0.012f, 0.0072f, -Length * 0.5f + 0.012f),
                new Vector3(0.024f, 0.0015f, 0.024f), "Plastico_Rojo", new Color(0.8f, 0.1f, 0.1f));

            GameObject label = new GameObject("Rotulo");
            label.AddComponent<NoAutoCollider>();
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.0076f, 0.005f);
            // Se lee desde el frente del botiquin (el jugador mira hacia -Z
            // del sobre); la esquina roja queda arriba a la derecha.
            label.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            VRPanelKit.Text(label.transform, "GASA ESTÉRIL\n10 × 10 cm\n<size=70%>Rasgar por la esquina roja</size>", Vector2.zero,
                0.011f, new Color(0.12f, 0.25f, 0.55f), TextAnchor.MiddleCenter, TextAlignment.Center);

            GauzePacket packet = root.AddComponent<GauzePacket>();
            packet.corner = tab.transform;
            return root;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            SimpleGrab.AddGrabBlocker(BlocksGrab);
        }

        private void OnDisable()
        {
            SimpleGrab.RemoveGrabBlocker(BlocksGrab);
            if (hint != null)
            {
                hint.Hide();
            }
        }

        private void OnDestroy()
        {
            if (hint != null)
            {
                hint.Destroy();
            }
        }

        // Con el sobre en una mano, apretar junto a la esquina no agarra otra
        // cosa: empieza a rasgarlo.
        private bool BlocksGrab(Vector3 point)
        {
            return !torn && corner != null && SimpleGrab.IsHeldObject(body)
                && Vector3.Distance(point, corner.position) < cornerReach;
        }

        private void LateUpdate()
        {
            if (torn || corner == null)
            {
                return;
            }

            bool heldLeft = SimpleGrab.IsHeldBy(body, true);
            bool heldRight = SimpleGrab.IsHeldBy(body, false);
            if (!heldLeft && !heldRight)
            {
                tearHand = -1;
                HideHint();
                return;
            }

            bool otherLeft = !heldLeft;
            if (!SimpleGrab.TryGetHandInput(otherLeft, out Vector3 point, out bool pressed, out bool holding)
                || holding)
            {
                tearHand = -1;
                HideHint();
                return;
            }

            float toCorner = Vector3.Distance(point, corner.position);
            if (!pressed)
            {
                tearHand = -1;
                if (toCorner < cornerReach * 2f)
                {
                    ShowHint(0f);
                }
                else
                {
                    HideHint();
                }

                return;
            }

            int other = otherLeft ? 0 : 1;
            if (tearHand < 0)
            {
                if (toCorner < cornerReach)
                {
                    tearHand = other;
                    startGap = Vector3.Distance(point, body.position);
                    Buzz(-1, 0.3f, 0.04f);
                }

                return;
            }

            // Cuanto se separaron las manos desde que tomo la esquina.
            float pulled = Vector3.Distance(point, body.position) - startGap;
            float progress = Mathf.Clamp01(pulled / Mathf.Max(0.01f, tearDistance));
            ShowHint(progress);
            if (progress > 0.05f && Time.time >= nextTick)
            {
                nextTick = Time.time + 0.08f;
                Buzz(-1, 0.25f + 0.45f * progress, 0.05f);
            }

            if (progress >= 1f)
            {
                Tear(heldLeft, point);
            }
        }

        private void Tear(bool packetInLeft, Vector3 otherHand)
        {
            torn = true;
            HideHint();
            Hito1FX.PlayAt("film_estirar", corner.position, 0.8f, 0.15f);
            Buzz(-1, 0.7f, 0.12f);

            // La gasa queda en la mano que tenia el sobre.
            Vector3 at = body.position + transform.up * 0.012f;
            Quaternion rot = transform.rotation;
            SimpleGrab.ForceRelease(body);
            GameObject gauze = BurnSceneProps.BuildGauze();
            gauze.transform.SetPositionAndRotation(at, rot);
            Grabbable grab = gauze.GetComponent<Grabbable>();
            Rigidbody gauzeBody = gauze.GetComponent<Rigidbody>();
            if (!SimpleGrab.ForceGrab(gauzeBody, packetInLeft))
            {
                // Sin mano que la tome: queda donde estaba el sobre.
                grab.SetHome();
            }

            // El sobre roto (abierto, mas chico) cae.
            Transform t = transform;
            foreach (Transform child in t)
            {
                if (child.name == "Rotulo")
                {
                    child.gameObject.SetActive(false);
                }
            }

            t.localScale = new Vector3(1f, 0.5f, 0.85f);
            Grabbable own = GetComponent<Grabbable>();
            if (own != null)
            {
                own.returnFromFloor = false;
            }

            Destroy(this);
        }

        private void ShowHint(float progress)
        {
            if (hint == null)
            {
                hint = new HoldHint("IndicacionSobre", 0.04f);
            }

            hint.Show(corner.position, "Sepáralo para\nrasgarlo", progress);
        }

        private void HideHint()
        {
            if (hint != null)
            {
                hint.Hide();
            }
        }

        private static void Buzz(int hand, float amplitude, float seconds)
        {
            if (BurnHaptics.Current != null)
            {
                BurnHaptics.Current.Buzz(hand, amplitude, seconds);
            }
        }
    }
}
