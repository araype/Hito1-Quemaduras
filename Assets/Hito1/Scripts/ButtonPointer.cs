using UnityEngine;

namespace SojaExiles
{
    // Rayo de cada mano para apuntar a los botones de los paneles
    // (TouchButton) de lejos, como en el menu del Quest: con controles sale
    // del control; con las manos, del rayo del sistema (OVRHand.PointerPose).
    // El rayo solo se ve cuando apunta a un boton; ahi el boton se ilumina y
    // se aprieta con el gatillo, el boton lateral o la pinza. Si la punta del
    // dedo ya esta en un panel, esa mano no usa el rayo (cuenta el toque).
    // Tambien lleva la cuenta de cuando se empieza a apretar cada mano (los
    // botones se activan al apretar, no por tocar) y evita que esa pinza
    // agarre un objeto cercano. Lo crea el primer TouchButton que aparece.
    [DefaultExecutionOrder(-100)]
    public class ButtonPointer : MonoBehaviour
    {
        [Tooltip("Distancia maxima del rayo a un boton (m).")]
        public float maxDistance = 4f;
        public Color rayColor = new Color(1f, 1f, 1f, 0.55f);

        private static ButtonPointer instance;

        private readonly bool[] pressed = new bool[2];
        private readonly bool[] down = new bool[2];
        private readonly bool[] holding = new bool[2];
        private readonly bool[] consumed = new bool[2];
        private readonly bool[] aiming = new bool[2];
        private readonly Vector3[] origin = new Vector3[2];
        private readonly LineRenderer[] rays = new LineRenderer[2];
        private readonly LineRenderer[] dots = new LineRenderer[2];

        public static void Ensure()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject("PunteroBotones");
            go.AddComponent<NoAutoCollider>();
            instance = go.AddComponent<ButtonPointer>();
        }

        // Esa mano empezo a apretar en este cuadro (y no sostiene nada, y
        // ningun otro boton uso ya ese apreton).
        public static bool PressedDown(int hand)
        {
            return instance != null && hand >= 0 && hand < 2 && instance.down[hand] && !instance.holding[hand]
                && !instance.consumed[hand];
        }

        // Un apreton aprieta un solo boton.
        public static void Consume(int hand)
        {
            if (instance != null && hand >= 0 && hand < 2)
            {
                instance.consumed[hand] = true;
            }
        }

        private void Awake()
        {
            for (int h = 0; h < 2; h++)
            {
                rays[h] = Line(h == 0 ? "RayoIzquierdo" : "RayoDerecho", 0.004f, 0.0015f, 0);
                dots[h] = Line(h == 0 ? "PuntoIzquierdo" : "PuntoDerecho", 0.014f, 0.014f, 4);
            }

            SimpleGrab.AddGrabBlocker(BlocksGrab);
        }

        private void OnDestroy()
        {
            SimpleGrab.RemoveGrabBlocker(BlocksGrab);
            if (instance == this)
            {
                instance = null;
            }
        }

        private LineRenderer Line(string name, float startWidth, float endWidth, int caps)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.numCapVertices = caps;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Material m = new Material(Shader.Find("Sprites/Default"));
            // Despues de los paneles, que se dibujan encima de todo.
            m.renderQueue = 4000;
            line.material = m;
            line.startColor = rayColor;
            line.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, rayColor.a * 0.3f);
            line.enabled = false;
            return line;
        }

        private void Update()
        {
            foreach (TouchButton button in TouchButton.Active)
            {
                if (button != null)
                {
                    button.RayHand = -1;
                }
            }

            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                bool valid = SimpleGrab.TryGetPointer(left, out Ray ray, out bool now, out bool isHolding);
                down[h] = valid && now && !pressed[h];
                pressed[h] = valid && now;
                holding[h] = isHolding;
                consumed[h] = false;
                aiming[h] = false;
                origin[h] = ray.origin;

                TouchButton target = null;
                float best = maxDistance;
                if (valid && !isHolding && !FingerOnPanel(left))
                {
                    foreach (TouchButton button in TouchButton.Active)
                    {
                        if (button != null && button.RayHit(ray, best, out float distance))
                        {
                            best = distance;
                            target = button;
                        }
                    }
                }

                if (target != null && target.RayHand < 0)
                {
                    target.RayHand = h;
                    aiming[h] = true;
                    Vector3 hit = ray.GetPoint(best);
                    rays[h].SetPosition(0, ray.origin);
                    rays[h].SetPosition(1, hit);
                    dots[h].SetPosition(0, hit - ray.direction * 0.004f);
                    dots[h].SetPosition(1, hit - ray.direction * 0.003f);
                    Color c = pressed[h] ? new Color(0.45f, 0.75f, 1f, 0.95f) : rayColor;
                    dots[h].startColor = c;
                    dots[h].endColor = c;
                }

                rays[h].enabled = aiming[h];
                dots[h].enabled = aiming[h];
            }
        }

        // La punta del indice de esa mano esta sobre algun boton.
        private static bool FingerOnPanel(bool left)
        {
            if (!SimpleGrab.TryGetFingertip(left, out Vector3 tip))
            {
                return false;
            }

            foreach (TouchButton button in TouchButton.Active)
            {
                if (button != null && button.Near(tip))
                {
                    return true;
                }
            }

            return false;
        }

        // Apretar sobre un boton (o apuntandole con el rayo) no agarra nada.
        private bool BlocksGrab(Vector3 point)
        {
            for (int h = 0; h < 2; h++)
            {
                if (aiming[h] && Vector3.Distance(point, origin[h]) < 0.25f)
                {
                    return true;
                }
            }

            foreach (TouchButton button in TouchButton.Active)
            {
                if (button != null && button.Near(point))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
