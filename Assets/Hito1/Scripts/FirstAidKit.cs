using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Botiquin sobre el meson, junto al lavadero: caja blanca con cruz roja y
    // tapa con bisagra atras. La tapa se abre tomandola del tirador de
    // adelante (boton lateral o pinza) y levantandola; un toque corto en el
    // tirador tambien la abre o la cierra. Adentro: sobres de gasa esteril
    // (GauzePacket: se rasgan con las dos manos), una venda (de adorno) y una
    // pomada, que no se debe usar en una quemadura (HomeRemedy.Pomada).
    // Con la tapa cerrada no se puede sacar nada de adentro. Cada cosa tiene
    // su lugar: si se suelta cerca (a menos de slotReach) con la tapa abierta,
    // vuelve a acomodarse ahi.
    // Lo arma BurnSceneProps.
    public class FirstAidKit : MonoBehaviour
    {
        public const float Width = 0.3f;
        public const float Depth = 0.2f;
        public const float Height = 0.09f;
        private const float Wall = 0.008f;

        [Tooltip("Grados que se abre la tapa.")]
        public float openAngle = 105f;
        [Tooltip("Distancia al tirador para tomar la tapa (m).")]
        public float handleReach = 0.08f;
        [Tooltip("Si algo se suelta a menos de esto de su lugar en el botiquin, vuelve a acomodarse (m).")]
        public float slotReach = 0.12f;

        private class Slot
        {
            public Rigidbody body;
            public Grabbable grab;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public bool wasHeld;
        }

        private readonly List<Slot> slots = new List<Slot>();

        public static FirstAidKit Current { get; private set; }

        public bool IsOpen
        {
            get { return angle > 60f; }
        }

        public Vector3 HandlePosition
        {
            get { return handle != null ? handle.position : transform.position; }
        }

        private Transform lid;
        private Transform handle;
        private float angle;
        private float target;
        private int dragHand = -1;
        private float dragStartTime;
        private float dragStartAngle;
        private readonly bool[] wasPressed = new bool[2];
        private bool announcedOpen;

        public static FirstAidKit Build()
        {
            GameObject root = new GameObject("Botiquin");
            root.AddComponent<NoAutoCollider>();
            Color white = new Color(0.95f, 0.95f, 0.93f);
            Color red = new Color(0.8f, 0.1f, 0.1f);

            // Caja: fondo y cuatro paredes (piezas visuales y colliders).
            Box(root.transform, new Vector3(0f, Wall * 0.5f, 0f), new Vector3(Width, Wall, Depth), white);
            Box(root.transform, new Vector3(0f, Height * 0.5f, Depth * 0.5f - Wall * 0.5f),
                new Vector3(Width, Height, Wall), white);
            Box(root.transform, new Vector3(0f, Height * 0.5f, -Depth * 0.5f + Wall * 0.5f),
                new Vector3(Width, Height, Wall), white);
            Box(root.transform, new Vector3(Width * 0.5f - Wall * 0.5f, Height * 0.5f, 0f),
                new Vector3(Wall, Height, Depth), white);
            Box(root.transform, new Vector3(-Width * 0.5f + Wall * 0.5f, Height * 0.5f, 0f),
                new Vector3(Wall, Height, Depth), white);

            // Cruz roja y rotulo en el frente.
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(-0.07f, Height * 0.55f, Depth * 0.5f + 0.001f),
                new Vector3(0.036f, 0.012f, 0.002f), "Plastico_Rojo", red);
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(-0.07f, Height * 0.55f, Depth * 0.5f + 0.001f),
                new Vector3(0.012f, 0.036f, 0.002f), "Plastico_Rojo", red);
            GameObject labelRoot = new GameObject("Rotulo");
            labelRoot.AddComponent<NoAutoCollider>();
            labelRoot.transform.SetParent(root.transform, false);
            labelRoot.transform.localPosition = new Vector3(0.03f, Height * 0.55f, Depth * 0.5f + 0.004f);
            labelRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            VRPanelKit.Text(labelRoot.transform, "PRIMEROS\nAUXILIOS", Vector2.zero, 0.014f, red,
                TextAnchor.MiddleCenter, TextAlignment.Center);

            // Tapa: gira alrededor del borde de atras.
            GameObject pivot = new GameObject("Tapa");
            pivot.AddComponent<NoAutoCollider>();
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, Height, -Depth * 0.5f);
            Box(pivot.transform, new Vector3(0f, 0.006f, Depth * 0.5f), new Vector3(Width + 0.006f, 0.012f, Depth + 0.004f),
                white);
            PropKit.Part(pivot.transform, PrimitiveType.Cube, new Vector3(0f, 0.0125f, Depth * 0.5f),
                new Vector3(0.075f, 0.002f, 0.024f), "Plastico_Rojo", red);
            PropKit.Part(pivot.transform, PrimitiveType.Cube, new Vector3(0f, 0.0125f, Depth * 0.5f),
                new Vector3(0.024f, 0.002f, 0.075f), "Plastico_Rojo", red);
            Renderer grip = PropKit.Part(pivot.transform, PrimitiveType.Cube, new Vector3(0f, 0.004f, Depth + 0.012f),
                new Vector3(0.07f, 0.01f, 0.018f), "Plastico_Gris", new Color(0.55f, 0.57f, 0.6f));

            FirstAidKit kit = root.AddComponent<FirstAidKit>();
            kit.lid = pivot.transform;
            kit.handle = grip.transform;
            return kit;
        }

        // Pieza blanca con su collider (la caja no se mueve).
        private static void Box(Transform parent, Vector3 localPosition, Vector3 size, Color color)
        {
            Renderer r = PropKit.Part(parent, PrimitiveType.Cube, localPosition, size, "Botiquin", color);
            r.gameObject.AddComponent<BoxCollider>();
        }

        // Lo de adentro, ya con la caja en su lugar.
        public List<GameObject> Fill()
        {
            List<GameObject> items = new List<GameObject>();
            float floor = Wall + 0.003f;
            items.Add(Put(GauzePacket.Build(), new Vector3(-0.07f, floor, -0.02f), 15f));
            items.Add(Put(GauzePacket.Build(), new Vector3(-0.065f, floor + 0.008f, 0.04f), -10f));
            items.Add(Put(BuildBandage(), new Vector3(0.065f, floor, -0.045f), 90f));
            items.Add(Put(BuildOintment(), new Vector3(0.06f, floor, 0.045f), 0f));
            return items;
        }

        private GameObject Put(GameObject item, Vector3 local, float yaw)
        {
            item.transform.SetPositionAndRotation(transform.TransformPoint(local),
                transform.rotation * Quaternion.Euler(0f, yaw, 0f));
            Grabbable grab = item.GetComponent<Grabbable>();
            grab.SetHome();
            slots.Add(new Slot
            {
                body = item.GetComponent<Rigidbody>(),
                grab = grab,
                localPosition = transform.InverseTransformPoint(item.transform.position),
                localRotation = Quaternion.Inverse(transform.rotation) * item.transform.rotation
            });
            return item;
        }

        private static GameObject BuildBandage()
        {
            GameObject root = PropKit.Grabbable("Venda", Vector3.zero, Quaternion.identity,
                new Vector3(0.05f, 0.05f, 0.05f), 0.05f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f),
                new Vector3(0.05f, 0.025f, 0.05f), Quaternion.Euler(0f, 0f, 90f), "Gasa_Tejido",
                new Color(0.97f, 0.96f, 0.92f));
            // Con la gasa puesta, sirve para fijarla con una vuelta suelta.
            root.AddComponent<BandageItem>();
            return root;
        }

        // Pomada (crema): un distractor. Las guias dicen no poner cremas ni
        // pomadas sobre una quemadura.
        private static GameObject BuildOintment()
        {
            GameObject root = PropKit.Grabbable("Pomada", Vector3.zero, Quaternion.identity,
                new Vector3(0.12f, 0.03f, 0.035f), 0.06f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f),
                new Vector3(0.028f, 0.05f, 0.028f), Quaternion.Euler(0f, 0f, 90f), "Pomada_Tubo",
                new Color(0.95f, 0.95f, 0.97f));
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.056f, 0.015f, 0f),
                new Vector3(0.016f, 0.008f, 0.016f), Quaternion.Euler(0f, 0f, 90f), "Plastico_Verde",
                new Color(0.15f, 0.55f, 0.3f));
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(-0.012f, 0.0295f, 0f),
                new Vector3(0.05f, 0.001f, 0.014f), "Plastico_Verde", new Color(0.15f, 0.55f, 0.3f));
            root.AddComponent<HomeRemedy>().remedy = HomeRemedy.Remedy.Pomada;
            return root;
        }

        private void Awake()
        {
            Current = this;
        }

        private void OnEnable()
        {
            SimpleGrab.AddGrabBlocker(BlocksGrab);
        }

        private void OnDisable()
        {
            SimpleGrab.RemoveGrabBlocker(BlocksGrab);
            if (Current == this)
            {
                Current = null;
            }
        }

        // Apretar en el tirador no agarra otra cosa; con la tapa cerrada no se
        // saca nada de adentro.
        private bool BlocksGrab(Vector3 point)
        {
            if (handle != null && Vector3.Distance(point, handle.position) < handleReach)
            {
                return true;
            }

            if (IsOpen)
            {
                return false;
            }

            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) < Width * 0.5f && Mathf.Abs(local.z) < Depth * 0.5f
                && local.y > 0f && local.y < Height + 0.02f;
        }

        private void LateUpdate()
        {
            if (lid == null)
            {
                return;
            }

            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                if (!SimpleGrab.TryGetHandInput(left, out Vector3 point, out bool pressed, out bool holding))
                {
                    wasPressed[h] = false;
                    if (dragHand == h)
                    {
                        EndDrag();
                    }

                    continue;
                }

                if (dragHand < 0 && pressed && !wasPressed[h] && !holding
                    && Vector3.Distance(point, handle.position) < handleReach)
                {
                    dragHand = h;
                    dragStartTime = Time.time;
                    dragStartAngle = angle;
                    Buzz(h, 0.3f, 0.04f);
                }

                if (dragHand == h)
                {
                    if (pressed)
                    {
                        // Angulo de la mano alrededor de la bisagra.
                        Vector3 v = point - lid.position;
                        float a = Mathf.Atan2(Vector3.Dot(v, transform.up), Vector3.Dot(v, transform.forward))
                            * Mathf.Rad2Deg;
                        float before = angle;
                        angle = Mathf.Clamp(a, 0f, openAngle);
                        target = angle;
                        if (Mathf.Abs(angle - before) > 3f)
                        {
                            Buzz(h, 0.15f, 0.03f);
                        }
                    }
                    else
                    {
                        EndDrag();
                    }
                }

                wasPressed[h] = pressed;
            }

            if (dragHand < 0)
            {
                angle = Mathf.MoveTowards(angle, target, 300f * Time.deltaTime);
            }

            UpdateSlots();

            lid.localRotation = Quaternion.Euler(-angle, 0f, 0f);

            if (IsOpen && !announcedOpen)
            {
                announcedOpen = true;
                Hito1FX.PlayAt("perilla_clic", lid.position, 0.5f);
            }
            else if (!IsOpen && angle < 5f)
            {
                announcedOpen = false;
            }
        }

        // Lo que se suelta cerca de su lugar, con la tapa abierta, vuelve ahi.
        private void UpdateSlots()
        {
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                Slot slot = slots[i];
                if (slot.body == null || slot.grab == null)
                {
                    slots.RemoveAt(i);
                    continue;
                }

                bool held = slot.grab.IsHeld;
                Vector3 home = transform.TransformPoint(slot.localPosition);
                if (slot.wasHeld && !held && IsOpen && Vector3.Distance(slot.body.position, home) < slotReach)
                {
                    slot.body.velocity = Vector3.zero;
                    slot.body.angularVelocity = Vector3.zero;
                    slot.body.position = home;
                    slot.body.rotation = transform.rotation * slot.localRotation;
                    slot.body.transform.SetPositionAndRotation(home, transform.rotation * slot.localRotation);
                    slot.grab.SetHome();
                    Hito1FX.PlayAt("perilla_clic", home, 0.3f);
                }

                slot.wasHeld = held;
            }
        }

        private void EndDrag()
        {
            bool tap = Time.time - dragStartTime < 0.35f && Mathf.Abs(angle - dragStartAngle) < 15f;
            if (tap)
            {
                target = dragStartAngle > 50f ? 0f : openAngle;
            }
            else
            {
                target = angle > 50f ? openAngle : 0f;
            }

            dragHand = -1;
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
