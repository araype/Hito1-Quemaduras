using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Puerta o cajon de un mueble de la cocina que se abre con la mano, y lo
    // que esta guardado adentro. Usa las puertas y cajones que ya trae la
    // cocina (opencloseDoor y Drawer_Pull_X, que solo se abrian con el mouse):
    // apaga su Animator y mueve la pieza segun la mano.
    //   - Se toma del tirador (boton lateral, gatillo o pinza) y se tira: la
    //     puerta gira alrededor de su bisagra siguiendo la mano y el cajon se
    //     desliza hacia afuera. Un toque corto la abre o la cierra.
    //   - Lo guardado queda quieto (kinematico) y, con el mueble cerrado, sin
    //     colliders: no se puede sacar a traves de la puerta. En un cajon, lo
    //     guardado se mueve con el cajon.
    //   - Al sacar algo y soltarlo, vuelve a caer con normalidad; si despues
    //     se cae al piso, vuelve al meson (outsideHome), no adentro del mueble.
    // Lo arma BurnSceneProps (KitchenStorage).
    public class StorageFront : MonoBehaviour
    {
        public enum Kind
        {
            Door,
            Drawer
        }

        public Kind kind;
        [Tooltip("Puerta: grados que se abre. Cajon: metros que sale.")]
        public float openAmount = 95f;
        [Tooltip("Distancia al tirador para tomarlo (m).")]
        public float handleReach = 0.09f;

        private class Stored
        {
            public Rigidbody rb;
            public Grabbable grab;
            public Collider[] colliders;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public bool taken;
        }

        private readonly List<Stored> stored = new List<Stored>();

        public static readonly List<StorageFront> All = new List<StorageFront>();

        // Se saco algo de adentro (lo usa BurnSceneProps para avisar).
        public event System.Action<Rigidbody> OnTaken;

        public bool IsOpen
        {
            get { return amount > openAmount * 0.45f; }
        }

        public float Amount01
        {
            get { return openAmount > 0f ? amount / openAmount : 0f; }
        }

        public Vector3 HandlePosition
        {
            get { return transform.TransformPoint(handleLocal); }
        }

        // Hacia donde se abre (afuera del mueble).
        public Vector3 Outward { get; private set; }

        // Lugar del meson donde queda lo que se saca (si se cae al piso).
        public Vector3 OutsideHome { get; set; }
        public bool HasOutsideHome { get; set; }

        private Vector3 handleLocal;
        private Vector3 closedPosition;
        private Quaternion closedRotation;
        private float sign = 1f;
        private float forcedDoorSign;
        private Transform interiorFrame;
        private Bounds interior;
        // Lo que se saco de aqui: si se vuelve a soltar adentro, se guarda.
        private readonly List<Rigidbody> loose = new List<Rigidbody>();
        private Vector3 edgeClosed;    // borde libre de la puerta, cerrada (relativo a la bisagra)
        private float amount;
        private float target;
        private int dragHand = -1;
        private float dragStartTime;
        private float dragStartAmount;
        private float dragStartOffset;
        private Vector3 dragStartPoint;
        private readonly bool[] wasPressed = new bool[2];
        private bool announcedOpen;

        // Prepara la pieza: kind, hacia donde esta la habitacion y a que
        // altura de la puerta va el tirador (-1 abajo, 1 arriba).
        public static StorageFront Setup(Transform piece, Kind kind, Vector3 roomPoint, float handleHeight)
        {
            Vector3 position = piece.position;
            Quaternion rotation = piece.rotation;
            // La pose abierta de su propia animacion dice hacia donde y cuanto
            // se abre (sin adivinar por la posicion de la habitacion).
            bool sampled = SampleOpenPose(piece, kind == Kind.Door ? new[] { "Opening", "Opening 1" } : new[] { "openpull_01" },
                out Vector3 openPosition, out Quaternion openRotation);
            piece.SetPositionAndRotation(position, rotation);
            foreach (Animator animator in piece.GetComponents<Animator>())
            {
                animator.enabled = false;
            }

            foreach (MonoBehaviour m in piece.GetComponents<MonoBehaviour>())
            {
                // Los scripts de la cocina que la abrian con el mouse.
                if (m is opencloseDoor || m is opencloseDoor1 || m is Drawer_Pull_X)
                {
                    m.enabled = false;
                }
            }

            StorageFront front = piece.gameObject.AddComponent<StorageFront>();
            front.kind = kind;
            front.openAmount = kind == Kind.Door ? 95f : 0.3f;
            front.closedPosition = position;
            front.closedRotation = rotation;
            if (sampled)
            {
                if (kind == Kind.Drawer && (openPosition - position).magnitude > 0.05f)
                {
                    Vector3 pull = PropKit.Flat(openPosition - position);
                    // Hacia la habitacion queda la pose abierta.
                    roomPoint = position + pull.normalized * 10f;
                    front.openAmount = Mathf.Min(pull.magnitude, 0.4f);
                }
                else if (kind == Kind.Door)
                {
                    float angle = Quaternion.Angle(rotation, openRotation);
                    if (angle > 30f)
                    {
                        front.openAmount = Mathf.Max(angle + 10f, 90f);
                        front.forcedDoorSign = Vector3.SignedAngle(rotation * Vector3.forward, openRotation * Vector3.forward,
                            Vector3.up) >= 0f ? 1f : -1f;
                    }
                }
            }

            front.Measure(roomPoint, handleHeight);
            return front;
        }

        // Evalua el estado de apertura de su Animator al final y lee la pose.
        private static bool SampleOpenPose(Transform piece, string[] states, out Vector3 position, out Quaternion rotation)
        {
            position = piece.position;
            rotation = piece.rotation;
            Animator animator = piece.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            foreach (string state in states)
            {
                if (!animator.HasState(0, Animator.StringToHash(state)))
                {
                    continue;
                }

                animator.Play(state, 0, 1f);
                animator.Update(0f);
                position = piece.position;
                rotation = piece.rotation;
                animator.Play(state, 0, 0f);
                animator.Update(0f);
                return true;
            }

            return false;
        }

        // Limites de los meshes de 'root' (o solo el suyo) en el espacio local de 'frame'.
        private static Bounds LocalBounds(Transform frame, Transform root, bool children)
        {
            Bounds local = new Bounds();
            bool any = false;
            MeshFilter[] filters = children ? root.GetComponentsInChildren<MeshFilter>()
                : root.GetComponents<MeshFilter>();
            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = frame.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any)
                    {
                        local = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        local.Encapsulate(p);
                    }
                }
            }

            return local;
        }

        // Si el cajon es un bloque macizo (AutoColliders le pone un
        // BoxCollider cuando no tiene MeshCollider), no se puede guardar nada
        // adentro: se cambia por una bandeja hueca (fondo y cuatro paredes).
        private void HollowOut(Bounds local)
        {
            foreach (MeshCollider mc in GetComponentsInChildren<MeshCollider>())
            {
                if (mc.enabled && !mc.convex)
                {
                    return;
                }
            }

            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                // El del objeto raiz es el frente que usaba el mouse; los de
                // las piezas son el bloque macizo.
                if (c.gameObject != gameObject && !c.isTrigger)
                {
                    c.enabled = false;
                }
            }

            GameObject tray = new GameObject("BandejaCajon");
            tray.AddComponent<NoAutoCollider>();
            tray.transform.SetParent(transform, false);
            Vector3 s = transform.lossyScale;
            // 1,2 cm de grosor en el mundo, en unidades locales.
            Vector3 t = new Vector3(0.012f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 0.012f / Mathf.Max(1e-4f, Mathf.Abs(s.y)),
                0.012f / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
            Vector3 c0 = local.center;
            Vector3 size = local.size;
            AddBox(tray, new Vector3(c0.x, local.min.y + t.y * 0.5f, c0.z), new Vector3(size.x, t.y, size.z));
            AddBox(tray, new Vector3(local.min.x + t.x * 0.5f, c0.y, c0.z), new Vector3(t.x, size.y, size.z));
            AddBox(tray, new Vector3(local.max.x - t.x * 0.5f, c0.y, c0.z), new Vector3(t.x, size.y, size.z));
            AddBox(tray, new Vector3(c0.x, c0.y, local.min.z + t.z * 0.5f), new Vector3(size.x, size.y, t.z));
            AddBox(tray, new Vector3(c0.x, c0.y, local.max.z - t.z * 0.5f), new Vector3(size.x, size.y, t.z));
        }

        private static void AddBox(GameObject on, Vector3 center, Vector3 size)
        {
            BoxCollider box = on.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        // Adentro del mueble (o del cajon): lo que se suelta ahi vuelve a
        // quedar guardado.
        private bool Inside(Vector3 world)
        {
            if (interiorFrame == null || interior.size == Vector3.zero)
            {
                return false;
            }

            return interior.Contains(interiorFrame.InverseTransformPoint(world));
        }

        private void Measure(Vector3 roomPoint, float handleHeight)
        {
            // Limites de la pieza en su espacio local (todos sus meshes).
            Bounds local = LocalBounds(transform, transform, true);

            // El interior: el cajon mismo, o el cuerpo del mueble de la puerta.
            if (kind == Kind.Drawer)
            {
                interiorFrame = transform;
                interior = local;
                HollowOut(local);
            }
            else if (transform.parent != null)
            {
                interiorFrame = transform.parent;
                interior = LocalBounds(interiorFrame, interiorFrame, false);
            }

            Vector3 centerWorld = transform.TransformPoint(local.center);
            Vector3 room = PropKit.Flat(roomPoint - centerWorld);

            if (kind == Kind.Drawer)
            {
                // Sale por el lado de la pieza que mira a la habitacion: el
                // eje local horizontal mas alineado con ella.
                Vector3 best = transform.right;
                float bestDot = 0f;
                foreach (Vector3 axis in new[] { transform.right, -transform.right, transform.forward, -transform.forward })
                {
                    float d = Vector3.Dot(PropKit.Flat(axis).normalized, room.normalized);
                    if (d > bestDot)
                    {
                        bestDot = d;
                        best = PropKit.Flat(axis).normalized;
                    }
                }

                Outward = best;
                // El frente del cajon: el punto de los limites mas hacia afuera,
                // a media altura (ahi esta su tirador).
                Vector3 frontWorld = centerWorld + Outward * ExtentAlong(local, Outward);
                handleLocal = transform.InverseTransformPoint(frontWorld + Outward * 0.015f);
                return;
            }

            // Puerta: el pivote es la bisagra; el borde libre es el punto de
            // los limites mas lejos de ella, en horizontal.
            Vector3 hinge = transform.position;
            Vector3 toCenter = PropKit.Flat(centerWorld - hinge);
            Vector3 edge = hinge + toCenter * 2f;
            edgeClosed = PropKit.Flat(edge - hinge);
            Vector3 normal = Vector3.Cross(Vector3.up, edgeClosed.normalized);
            if (Vector3.Dot(normal, room) < 0f)
            {
                normal = -normal;
            }

            Outward = normal;
            // Al abrir, el borde libre debe ir hacia la habitacion (o como
            // dice su animacion).
            Vector3 swung = Quaternion.AngleAxis(30f, Vector3.up) * edgeClosed;
            sign = forcedDoorSign != 0f ? forcedDoorSign : (Vector3.Dot(swung - edgeClosed, normal) > 0f ? 1f : -1f);
            if (Vector3.Dot(Quaternion.AngleAxis(30f * sign, Vector3.up) * edgeClosed - edgeClosed, normal) < 0f)
            {
                normal = -normal;
                Outward = normal;
            }

            float halfHeight = ExtentAlong(local, Vector3.up);
            float faceOut = ExtentAlong(local, Outward);
            Vector3 handle = centerWorld + toCenter * 0.8f + Vector3.up * (halfHeight * handleHeight)
                + Outward * (faceOut + 0.015f);
            handleLocal = transform.InverseTransformPoint(handle);
        }

        // Media medida de los limites locales a lo largo de una direccion del mundo.
        private float ExtentAlong(Bounds local, Vector3 worldDirection)
        {
            Vector3 d = transform.InverseTransformDirection(worldDirection);
            Vector3 s = transform.lossyScale;
            return Mathf.Abs(d.x) * local.extents.x * s.x + Mathf.Abs(d.y) * local.extents.y * s.y
                + Mathf.Abs(d.z) * local.extents.z * s.z;
        }

        // Guarda un objeto adentro (ya ubicado en su lugar).
        public void Store(Rigidbody rb)
        {
            if (rb == null)
            {
                return;
            }

            Stored s = new Stored
            {
                rb = rb,
                grab = rb.GetComponent<Grabbable>(),
                colliders = rb.GetComponentsInChildren<Collider>(true),
                taken = false
            };
            Transform frame = Frame();
            s.localPosition = frame != null ? frame.InverseTransformPoint(rb.position) : rb.position;
            s.localRotation = frame != null ? Quaternion.Inverse(frame.rotation) * rb.rotation : rb.rotation;
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            stored.Add(s);
            SetColliders(s, IsOpen);
        }

        public bool Holds(Rigidbody rb)
        {
            foreach (Stored s in stored)
            {
                if (s.rb == rb)
                {
                    return true;
                }
            }

            return false;
        }

        // Guardado en algun mueble cerrado: no se alcanza.
        public static bool IsEnclosed(Rigidbody rb)
        {
            StorageFront f = Holder(rb);
            return f != null && !f.IsOpen;
        }

        // Guardado en algun mueble (abierto o cerrado).
        public static bool IsStored(Rigidbody rb)
        {
            return Holder(rb) != null;
        }

        // El mueble donde esta guardado (o null).
        public static StorageFront Holder(Rigidbody rb)
        {
            if (rb == null)
            {
                return null;
            }

            foreach (StorageFront f in All)
            {
                if (f.Holds(rb))
                {
                    return f;
                }
            }

            return null;
        }

        // Lo de un cajon se mueve con el cajon; lo de detras de una puerta, no.
        private Transform Frame()
        {
            return kind == Kind.Drawer ? transform : null;
        }

        private void OnEnable()
        {
            All.Add(this);
            SimpleGrab.AddGrabBlocker(BlocksGrab);
        }

        private void OnDisable()
        {
            All.Remove(this);
            SimpleGrab.RemoveGrabBlocker(BlocksGrab);
        }

        // Apretar en el tirador no agarra otra cosa.
        private bool BlocksGrab(Vector3 point)
        {
            return Vector3.Distance(point, HandlePosition) < handleReach;
        }

        // Abre o cierra por codigo (pruebas, o si se quiere empezar abierto).
        public void SetOpen(bool open, bool instant = false)
        {
            target = open ? openAmount : 0f;
            if (instant)
            {
                amount = target;
                Apply();
            }
        }

        private void LateUpdate()
        {
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
                    && Vector3.Distance(point, HandlePosition) < handleReach)
                {
                    dragHand = h;
                    dragStartTime = Time.time;
                    dragStartAmount = amount;
                    dragStartPoint = point;
                    dragStartOffset = kind == Kind.Door ? HandAngle(point) - amount : 0f;
                    Buzz(h, 0.3f, 0.04f);
                }

                if (dragHand == h)
                {
                    if (pressed)
                    {
                        float before = amount;
                        if (kind == Kind.Door)
                        {
                            amount = Mathf.Clamp(HandAngle(point) - dragStartOffset, 0f, openAmount);
                        }
                        else
                        {
                            amount = Mathf.Clamp(dragStartAmount + Vector3.Dot(point - dragStartPoint, Outward), 0f,
                                openAmount);
                        }

                        target = amount;
                        float step = kind == Kind.Door ? 4f : 0.02f;
                        if (Mathf.Abs(amount - before) > step)
                        {
                            Buzz(h, 0.12f, 0.03f);
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
                float speed = kind == Kind.Door ? 260f : 0.9f;
                amount = Mathf.MoveTowards(amount, target, speed * Time.deltaTime);
            }

            Apply();
            UpdateStored();

            if (IsOpen && !announcedOpen)
            {
                announcedOpen = true;
                Hito1FX.PlayAt("perilla_clic", HandlePosition, 0.45f);
            }
            else if (!IsOpen && amount < openAmount * 0.05f && announcedOpen)
            {
                announcedOpen = false;
                Hito1FX.PlayAt("perilla_clic", HandlePosition, 0.35f);
            }
        }

        // Deja la puerta o el cajon entreabierto (0 a 1 de la apertura total).
        // Por debajo de la mitad sigue contando como cerrado: lo de adentro
        // no se puede sacar hasta abrirlo mas.
        public void SetAjar(float fraction)
        {
            amount = target = Mathf.Clamp(fraction, 0f, 0.4f) * openAmount;
            Apply();
        }

        // Angulo de la mano alrededor de la bisagra, medido desde la puerta cerrada.
        private float HandAngle(Vector3 point)
        {
            Vector3 v = PropKit.Flat(point - closedPosition);
            if (v.sqrMagnitude < 1e-4f)
            {
                return amount;
            }

            return Vector3.SignedAngle(edgeClosed, v, Vector3.up) * sign;
        }

        private void Apply()
        {
            if (kind == Kind.Door)
            {
                transform.rotation = Quaternion.AngleAxis(sign * amount, Vector3.up) * closedRotation;
            }
            else
            {
                transform.position = closedPosition + Outward * amount;
            }
        }

        private void UpdateStored()
        {
            bool open = IsOpen;
            Transform frame = Frame();

            // Lo que se devuelve al mueble abierto y queda quieto adentro vuelve
            // a estar guardado (si no, al cerrar, la puerta o el cajon lo
            // atravesarian).
            for (int i = loose.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = loose[i];
                if (rb == null)
                {
                    loose.RemoveAt(i);
                    continue;
                }

                Grabbable g = rb.GetComponent<Grabbable>();
                bool held = g != null ? g.IsHeld : SimpleGrab.IsHeldObject(rb);
                if (open && !held && !rb.isKinematic && rb.velocity.sqrMagnitude < 0.01f && Inside(rb.position)
                    && !IsStored(rb))
                {
                    loose.RemoveAt(i);
                    Store(rb);
                }
            }
            for (int i = stored.Count - 1; i >= 0; i--)
            {
                Stored s = stored[i];
                if (s.rb == null)
                {
                    stored.RemoveAt(i);
                    continue;
                }

                bool held = s.grab != null ? s.grab.IsHeld : SimpleGrab.IsHeldObject(s.rb);
                if (held)
                {
                    s.taken = true;
                    continue;
                }

                if (s.taken)
                {
                    // Se saco y se solto: vuelve a la fisica normal.
                    stored.RemoveAt(i);
                    SetColliders(s, true);
                    s.rb.isKinematic = false;
                    s.rb.useGravity = true;
                    if (s.grab != null && HasOutsideHome)
                    {
                        s.grab.SetHome(OutsideHome, s.rb.rotation);
                    }

                    if (!loose.Contains(s.rb))
                    {
                        loose.Add(s.rb);
                    }

                    OnTaken?.Invoke(s.rb);
                    continue;
                }

                SetColliders(s, open);
                if (frame != null)
                {
                    Vector3 p = frame.TransformPoint(s.localPosition);
                    Quaternion r = frame.rotation * s.localRotation;
                    s.rb.position = p;
                    s.rb.rotation = r;
                    s.rb.transform.SetPositionAndRotation(p, r);
                }
            }
        }

        private static void SetColliders(Stored s, bool on)
        {
            foreach (Collider c in s.colliders)
            {
                if (c != null && !c.isTrigger && c.enabled != on)
                {
                    c.enabled = on;
                }
            }
        }

        private void EndDrag()
        {
            float half = openAmount * 0.5f;
            bool tap = Time.time - dragStartTime < 0.35f && Mathf.Abs(amount - dragStartAmount) < openAmount * 0.15f;
            if (tap)
            {
                target = dragStartAmount > half ? 0f : openAmount;
            }
            else
            {
                target = amount > half ? openAmount : (amount > openAmount * 0.2f ? amount : 0f);
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
