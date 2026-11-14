using UnityEngine;

namespace SojaExiles
{
    // Palanca lateral del grifo de la cocina (la del modelo, Faucet_Handle),
    // en lugar de las dos llaves marcadas con esferas. Funciona como un grifo
    // de palanca lateral real (mezcladora secuencial): arriba esta cerrado y,
    // al girarla hacia uno, primero sale fria, luego tibia y al final
    // caliente.
    //
    // Se agarra con el boton lateral, el gatillo o la pinza cerca de la
    // palanca y se gira con la mano; al soltarla queda donde se dejo. Un
    // apreton corto sin mover la mano la abre en frio o la cierra (para
    // controles y para quien no pueda girar la muneca).
    //
    // Mueve las dos WaterTap de siempre (fria y caliente), asi que el resto
    // del juego (Heat, Flowing, eventos, templar el agua) no cambia:
    //   cerrada -> ninguna; fria -> la fria; tibia -> las dos; caliente -> la
    //   caliente.
    //
    // La palanca original es estatica (batching): al dar Play se esconde y se
    // pone una copia que se mueve (Resources/Hito1FX/PalancaGrifo, se arma con
    // el menu Hito1 > Preparar palanca del grifo). No cambia la escena.
    public class FaucetLever : MonoBehaviour
    {
        public WaterTap cold;
        public WaterTap hot;

        [Tooltip("Giro maximo de la palanca hacia el jugador (grados).")]
        public float maxAngle = 100f;
        [Tooltip("Por debajo de este angulo el grifo esta cerrado.")]
        public float closedBelow = 12f;
        [Tooltip("Desde este angulo sale tibia (fria y caliente).")]
        public float warmFrom = 50f;
        [Tooltip("Desde este angulo sale solo caliente.")]
        public float hotFrom = 78f;
        [Tooltip("Margen para no saltar entre dos zonas en el borde (grados).")]
        public float hysteresis = 3f;
        [Tooltip("Angulo al que queda con un apreton corto (agua fria).")]
        public float quickOpenAngle = 30f;
        [Tooltip("Distancia de la mano a la palanca para tomarla (m).")]
        public float grabReach = 0.08f;
        [Tooltip("Un apreton mas corto que esto, sin girar, abre o cierra.")]
        public float tapSeconds = 0.35f;

        public static FaucetLever Current { get; private set; }

        // 0 cerrada, 1 fria, 2 tibia, 3 caliente.
        public int Zone { get; private set; }
        public float Angle { get { return angle; } }

        // Punto que se toma (a media altura de la palanca).
        public Vector3 GripPosition
        {
            get { return hub + Quaternion.AngleAxis(angle, axis) * Vector3.up * gripHeight; }
        }

        private Transform pivot;
        private Renderer leverRenderer;
        private Material[] glowMaterials;
        private Vector3 hub;
        private Vector3 axis;
        private float gripHeight = 0.06f;

        private float angle;
        private float target;
        private int dragHand = -1;
        private float dragOffset;
        private float dragStartAngle;
        private float dragStartTime;
        private bool dragMoved;
        private readonly bool[] wasPressed = new bool[2];
        private int nearHand = -1;
        private BurnSequenceManager burn;

        private static readonly Color GlowColor = new Color(0.3f, 0.85f, 1f);

        // Pone la palanca sobre el grifo de 'coldTap'. Devuelve null si no
        // encuentra la palanca del modelo o la copia (quedan las llaves con
        // sus puntos, como antes).
        public static FaucetLever Install(WaterTap coldTap, WaterTap hotTap)
        {
            if (coldTap == null || hotTap == null)
            {
                return null;
            }

            Renderer original = null;
            float best = 0.6f;
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                if (!r.name.StartsWith("Faucet_Handle"))
                {
                    continue;
                }

                float d = Vector3.Distance(r.bounds.center, coldTap.transform.position);
                if (d < best)
                {
                    best = d;
                    original = r;
                }
            }

            GameObject prefab = Resources.Load<GameObject>("Hito1FX/PalancaGrifo");
            if (original == null || prefab == null)
            {
                Debug.LogWarning("[Hito1] No se encontro la palanca del grifo (" + (original == null ? "escena" : "Resources/Hito1FX/PalancaGrifo")
                    + "): quedan las llaves con puntos.");
                return null;
            }

            GameObject go = new GameObject("PalancaGrifo");
            go.transform.SetParent(coldTap.transform.parent, false);
            FaucetLever lever = go.AddComponent<FaucetLever>();
            lever.cold = coldTap;
            lever.hot = hotTap;
            lever.Build(original, prefab);
            return lever;
        }

        private void Build(Renderer original, GameObject prefab)
        {
            Transform source = original.transform;
            Bounds b = original.bounds;

            // Eje de la palanca: horizontal, del cuerpo del grifo hacia la
            // palanca (el cubo sale de costado).
            Vector3 side = PropKit.Flat(b.center - source.position);
            if (side.sqrMagnitude < 1e-6f)
            {
                side = source.right;
            }

            side.Normalize();
            Vector3 across = Vector3.Cross(Vector3.up, side);
            float hubRadius = 0.5f * (Mathf.Abs(b.size.x * across.x) + Mathf.Abs(b.size.z * across.z));
            hubRadius = Mathf.Clamp(hubRadius, 0.01f, 0.04f);
            hub = new Vector3(b.center.x, b.min.y + hubRadius, b.center.z);
            gripHeight = Mathf.Clamp((b.max.y - hub.y) * 0.6f, 0.03f, 0.1f);

            // Que la palanca baje hacia el jugador (hacia la poceta, donde cae
            // el chorro) y no hacia la pared.
            Vector3 front = cold.waterTrigger != null ? cold.waterTrigger.bounds.center : cold.transform.position;
            Vector3 toFront = PropKit.Flat(front - hub);
            axis = side;
            if (Vector3.Dot(Quaternion.AngleAxis(90f, axis) * Vector3.up, toFront) < 0f)
            {
                axis = -side;
            }

            pivot = new GameObject("Giro").transform;
            pivot.SetParent(transform, false);
            pivot.position = hub;
            pivot.rotation = Quaternion.identity;

            GameObject copy = Instantiate(prefab);
            copy.name = "PalancaGrifo_Malla";
            copy.transform.SetPositionAndRotation(source.position, source.rotation);
            copy.transform.localScale = source.lossyScale;
            copy.transform.SetParent(pivot, true);
            leverRenderer = copy.GetComponent<Renderer>();
            if (leverRenderer != null)
            {
                leverRenderer.shadowCastingMode = original.shadowCastingMode;
                leverRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                leverRenderer.reflectionProbeUsage = original.reflectionProbeUsage;
                glowMaterials = leverRenderer.materials;
                foreach (Material m in glowMaterials)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                }
            }

            original.enabled = false;

            // Marcas azul y roja junto al cubo, como en los grifos reales:
            // indican hacia donde sale fria y hacia donde caliente.
            float halfAlong = 0.5f * (Mathf.Abs(b.size.x * side.x) + Mathf.Abs(b.size.z * side.z));
            Vector3 face = hub + side * (halfAlong + 0.002f);
            Mark(face, (closedBelow + warmFrom) * 0.5f, hubRadius, "Plastico_Azul", new Color(0.2f, 0.45f, 1f));
            Mark(face, (hotFrom + maxAngle) * 0.5f, hubRadius, "Plastico_Rojo", new Color(0.95f, 0.2f, 0.15f));
        }

        private void Mark(Vector3 face, float atAngle, float radius, string material, Color fallback)
        {
            Vector3 dir = Quaternion.AngleAxis(atAngle, axis) * Vector3.up;
            Renderer r = PropKit.Part(transform, PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.009f, 0.0008f, 0.009f), material, fallback);
            r.name = "Marca_" + material;
            r.transform.position = face + dir * radius * 0.65f;
            r.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnEnable()
        {
            Current = this;
            SimpleGrab.AddGrabBlocker(BlocksGrab);
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }

            SimpleGrab.RemoveGrabBlocker(BlocksGrab);
        }

        // Tomar la palanca no agarra otra cosa.
        private bool BlocksGrab(Vector3 point)
        {
            return pivot != null && Vector3.Distance(point, GripPosition) < grabReach;
        }

        private void Start()
        {
            burn = FindObjectOfType<BurnSequenceManager>();
            Zone = ZoneFromTaps();
            angle = target = MidAngle(Zone);
            Apply();
        }

        private void LateUpdate()
        {
            if (pivot == null || cold == null || hot == null)
            {
                return;
            }

            // Si otro codigo abrio o cerro una llave, la palanca la sigue.
            int fromTaps = ZoneFromTaps();
            if (fromTaps != Zone && dragHand < 0)
            {
                Zone = fromTaps;
                target = MidAngle(Zone);
            }

            UpdateHands();

            if (dragHand < 0)
            {
                angle = Mathf.MoveTowards(angle, target, 240f * Time.deltaTime);
            }

            Apply();
            SetZone(ZoneFor(angle));
            UpdateGlow();
        }

        private void UpdateHands()
        {
            int near = -1;
            Vector3 grip = GripPosition;
            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                if (!SimpleGrab.TryGetHandInput(left, out Vector3 point, out bool pressed, out bool holding))
                {
                    wasPressed[h] = false;
                    if (dragHand == h)
                    {
                        dragHand = -1;
                    }

                    continue;
                }

                // La mano que se ve puede quedar frenada en la tia (su mano
                // bajo el chorro esta cerca): vale la mano real si esta mas
                // cerca (igual que HandPress).
                if (SimpleGrab.TryGetRawHandPoint(left, out Vector3 raw)
                    && Vector3.Distance(raw, grip) < Vector3.Distance(point, grip))
                {
                    point = raw;
                }

                bool inReach = !holding && Vector3.Distance(point, grip) < grabReach;
                if (inReach && near < 0)
                {
                    near = h;
                }

                if (dragHand < 0 && inReach && pressed && !wasPressed[h])
                {
                    dragHand = h;
                    dragStartTime = Time.time;
                    dragStartAngle = angle;
                    dragOffset = HandAngle(point, angle) - angle;
                    dragMoved = false;
                    Buzz(h, 0.35f, 0.04f);
                }

                if (dragHand == h)
                {
                    if (pressed)
                    {
                        float wanted = Mathf.Clamp(HandAngle(point, angle + dragOffset) - dragOffset, 0f, maxAngle);
                        if (!dragMoved && Mathf.Abs(wanted - dragStartAngle) > 4f)
                        {
                            dragMoved = true;
                        }

                        if (dragMoved)
                        {
                            angle = target = wanted;
                        }
                    }
                    else
                    {
                        // Apreton corto sin girar: abre en frio o cierra.
                        if (!dragMoved && Time.time - dragStartTime < tapSeconds)
                        {
                            target = angle < closedBelow ? quickOpenAngle : 0f;
                        }

                        dragHand = -1;
                    }
                }

                wasPressed[h] = pressed;
            }

            if (near >= 0 && near != nearHand)
            {
                Buzz(near, 0.15f, 0.02f);
            }

            nearHand = near;
        }

        // Angulo de la mano alrededor del eje de la palanca, medido desde
        // arriba. Muy cerca del eje no se puede medir: queda 'fallback'.
        private float HandAngle(Vector3 point, float fallback)
        {
            Vector3 v = Vector3.ProjectOnPlane(point - hub, axis);
            if (v.sqrMagnitude < 0.015f * 0.015f)
            {
                return fallback;
            }

            return Vector3.SignedAngle(Vector3.up, v, axis);
        }

        private void Apply()
        {
            pivot.rotation = Quaternion.AngleAxis(angle, axis);
        }

        private float Bound(int zone)
        {
            return zone == 0 ? closedBelow : (zone == 1 ? warmFrom : hotFrom);
        }

        private int ZoneFor(float a)
        {
            int z = Zone;
            while (z < 3 && a >= Bound(z) + hysteresis)
            {
                z++;
            }

            while (z > 0 && a <= Bound(z - 1) - hysteresis)
            {
                z--;
            }

            return z;
        }

        private float MidAngle(int zone)
        {
            switch (zone)
            {
                case 1: return quickOpenAngle;
                case 2: return (warmFrom + hotFrom) * 0.5f;
                case 3: return (hotFrom + maxAngle) * 0.5f;
                default: return 0f;
            }
        }

        private int ZoneFromTaps()
        {
            bool c = cold.IsOpen;
            bool h = hot.IsOpen;
            return c ? (h ? 2 : 1) : (h ? 3 : 0);
        }

        private void SetZone(int zone)
        {
            if (zone == Zone)
            {
                return;
            }

            int previous = Zone;
            Zone = zone;
            bool coldOpen = zone == 1 || zone == 2;
            bool hotOpen = zone >= 2;

            // Primero se abre la fria: pasar por tibia no cuenta como abrir
            // la caliente sola.
            if (coldOpen && !cold.IsOpen)
            {
                cold.SetOpen(true);
            }

            if (hotOpen != hot.IsOpen)
            {
                hot.SetOpen(hotOpen);
            }

            if (!coldOpen && cold.IsOpen)
            {
                cold.SetOpen(false);
            }

            // De tibia a caliente la llave caliente ya estaba abierta: se
            // avisa igual, porque ahora sale solo caliente.
            if (zone == 3 && previous == 2 && burn != null)
            {
                burn.Report(BurnSequenceManager.BurnEvent.HotWaterOn);
            }

            Hito1FX.PlayAt("perilla_clic", hub, 0.25f, 0.1f);
            Buzz(dragHand >= 0 ? dragHand : nearHand, 0.25f, 0.03f);
        }

        // En modo guiado, despues del accidente y con el grifo cerrado, la
        // palanca brilla suave (antes latia un punto azul). Con la mano al
        // alcance brilla un poco mas.
        private void UpdateGlow()
        {
            if (glowMaterials == null)
            {
                return;
            }

            bool hint = IndicatorUtil.HintsVisible && Zone == 0 && burn != null
                && burn.CurrentState == BurnSequenceManager.BurnState.Burned;
            float k = 0f;
            if (hint)
            {
                k = 0.22f + 0.14f * Mathf.Sin(Time.time * 4f);
                if (nearHand >= 0)
                {
                    k += 0.2f;
                }
            }

            Color c = GlowColor * k;
            foreach (Material m in glowMaterials)
            {
                m.SetColor("_EmissionColor", c);
            }
        }

        private static void Buzz(int hand, float amplitude, float seconds)
        {
            if (hand >= 0 && BurnHaptics.Current != null)
            {
                BurnHaptics.Current.Buzz(hand, amplitude, seconds);
            }
        }

        private void OnDestroy()
        {
            if (glowMaterials != null)
            {
                foreach (Material m in glowMaterials)
                {
                    Destroy(m);
                }
            }
        }
    }
}
