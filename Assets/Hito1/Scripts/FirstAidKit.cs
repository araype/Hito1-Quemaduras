using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Botiquin: caja blanca con cruz roja y tapa con bisagra atras. Empieza
    // guardado en el mueble alto junto al lavadero (StorageFront); se toma de
    // la caja y se lleva con todo adentro (lo de adentro se mueve con ella).
    // La tapa se abre tomandola del tirador de
    // adelante (boton lateral o pinza) y levantandola; un toque corto en el
    // tirador tambien la abre o la cierra. Adentro: sobres de gasa esteril
    // (GauzePacket: se rasgan con las dos manos), una venda (de adorno) y una
    // pomada, que no se debe usar en una quemadura (HomeRemedy.Pomada).
    // Con la tapa cerrada no se puede sacar nada de adentro, y dentro de un
    // mueble cerrado no se alcanza el tirador. Cada cosa tiene su lugar: si se
    // suelta cerca (a menos de slotReach) con la tapa abierta, vuelve a
    // acomodarse ahi.
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
            public Collider[] colliders;
            public bool inKit = true;
            public bool wasHeld;
            public float floorTime;
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

        // Dentro de un mueble cerrado: no se alcanza.
        public bool Enclosed
        {
            get { return body != null && StorageFront.IsEnclosed(body); }
        }

        // Fuera del mueble y con la tapa abierta: listo para usar.
        public bool ReadyToUse
        {
            get { return IsOpen && (body == null || !StorageFront.IsStored(body)); }
        }

        public Rigidbody Body
        {
            get { return body; }
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
        private bool reportedReady;
        private BurnSequenceManager sequence;
        private Rigidbody body;
        private Collider[] kitColliders;

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

            // Se puede llevar: se toma de cualquier parte de la caja o la tapa.
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<Grabbable>();

            FirstAidKit kit = root.AddComponent<FirstAidKit>();
            kit.lid = pivot.transform;
            kit.handle = grip.transform;
            kit.body = rb;
            kit.kitColliders = root.GetComponentsInChildren<Collider>(true);
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
            // Si se cae al piso vuelve al botiquin (UpdateSlots), no al lugar
            // donde estaba al empezar: el botiquin ya no esta ahi.
            grab.returnFromFloor = false;
            Slot slot = new Slot
            {
                body = item.GetComponent<Rigidbody>(),
                grab = grab,
                colliders = item.GetComponentsInChildren<Collider>(true),
                localPosition = transform.InverseTransformPoint(item.transform.position),
                localRotation = Quaternion.Inverse(transform.rotation) * item.transform.rotation
            };
            slots.Add(slot);
            Stow(slot);
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

        // Apretar en el tirador no agarra otra cosa (ni la caja). Con la tapa
        // cerrada, lo de adentro no tiene colliders: no se puede sacar.
        private bool BlocksGrab(Vector3 point)
        {
            return handle != null && !Enclosed && Vector3.Distance(point, handle.position) < handleReach;
        }

        // Guardado en su lugar: quieto, se mueve con la caja y no choca con ella.
        private void Stow(Slot slot)
        {
            slot.inKit = true;
            if (slot.body != null && !slot.body.isKinematic)
            {
                slot.body.velocity = Vector3.zero;
                slot.body.angularVelocity = Vector3.zero;
                slot.body.isKinematic = true;
            }

            IgnoreKit(slot, true);
        }

        private void IgnoreKit(Slot slot, bool ignore)
        {
            if (kitColliders == null || slot.colliders == null)
            {
                return;
            }

            foreach (Collider a in slot.colliders)
            {
                foreach (Collider b in kitColliders)
                {
                    if (a != null && b != null)
                    {
                        Physics.IgnoreCollision(a, b, ignore);
                    }
                }
            }
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

                if (dragHand < 0 && pressed && !wasPressed[h] && !holding && !Enclosed
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

            // Fuera del mueble y abierto: listo para cubrir cuando termine de
            // enfriar. No suma ni resta puntos (ninguna guia lo pide); queda en
            // el registro de la sesion.
            if (!reportedReady && ReadyToUse)
            {
                if (sequence == null)
                {
                    sequence = FindObjectOfType<BurnSequenceManager>();
                }

                if (sequence != null && sequence.CurrentState != BurnSequenceManager.BurnState.Idle)
                {
                    reportedReady = true;
                    sequence.Report(BurnSequenceManager.BurnEvent.SuppliesReady, "Botiquin");
                }
            }
            else if (!IsOpen && angle < 5f)
            {
                announcedOpen = false;
            }
        }

        // Lo guardado sigue a la caja; lo que se suelta cerca de su lugar, con
        // la tapa abierta, vuelve ahi.
        private void UpdateSlots()
        {
            bool reachable = IsOpen && !Enclosed;
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
                Quaternion homeRotation = transform.rotation * slot.localRotation;
                if (slot.inKit)
                {
                    if (held)
                    {
                        // Se saco: SimpleGrab ya lo hizo dinamico.
                        slot.inKit = false;
                    }
                    else
                    {
                        SetColliders(slot, reachable);
                        slot.body.position = home;
                        slot.body.rotation = homeRotation;
                        slot.body.transform.SetPositionAndRotation(home, homeRotation);
                    }
                }
                else if (slot.wasHeld && !held)
                {
                    if (IsOpen && Vector3.Distance(slot.body.position, home) < slotReach)
                    {
                        Stow(slot);
                        slot.body.transform.SetPositionAndRotation(home, homeRotation);
                        Hito1FX.PlayAt("perilla_clic", home, 0.3f);
                    }
                    else
                    {
                        // Afuera: vuelve a caer y a chocar con la caja.
                        IgnoreKit(slot, false);
                        slot.body.isKinematic = false;
                        slot.body.useGravity = true;
                    }
                }
                else if (!held && slot.body.position.y < transform.position.y - 0.3f
                    && slot.body.velocity.sqrMagnitude < 0.01f)
                {
                    // Se cayo al piso: despues de un momento vuelve a su lugar
                    // en el botiquin, este donde este (su lugar de inicio era el
                    // mueble).
                    slot.floorTime += Time.deltaTime;
                    if (slot.floorTime > 1.5f)
                    {
                        slot.floorTime = 0f;
                        Stow(slot);
                        slot.body.transform.SetPositionAndRotation(home, homeRotation);
                    }
                }
                else
                {
                    slot.floorTime = 0f;
                }

                slot.wasHeld = held;
            }
        }

        private static void SetColliders(Slot slot, bool on)
        {
            foreach (Collider c in slot.colliders)
            {
                if (c != null && !c.isTrigger && c.enabled != on)
                {
                    c.enabled = on;
                }
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
