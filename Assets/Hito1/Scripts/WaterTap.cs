using UnityEngine;

namespace SojaExiles
{
    // Llave del grifo: se abre o cierra tocandola con la mano (la esfera
    // fisica de SimpleGrab, que sigue al control o a la mano trackeada). Un
    // punto dorado marca donde tocar, como en el prototipo. Con la llave
    // cerrada no sale agua y el chorro no cuenta para enfriar o lavar.
    // Un grifo puede tener dos llaves (fria y caliente, 'pair') que comparten
    // el mismo chorro: sale agua si cualquiera esta abierta, y Heat dice si
    // sale fria, tibia o caliente.
    [RequireComponent(typeof(Collider))]
    public class WaterTap : MonoBehaviour
    {
        public ParticleSystem water;
        public Collider waterTrigger;
        public AudioSource waterSound;
        public bool startOpen;
        [Tooltip("Llave del agua caliente.")]
        public bool hot;
        [Tooltip("La otra llave del mismo grifo (comparten el chorro).")]
        public WaterTap pair;

        [Header("Indicador")]
        public Material indicatorMaterial;
        public float markerRadius = 0.03f;
        public Color closedColor = new Color(1f, 0.78f, 0.15f, 0.65f);
        public Color openColor = new Color(0.3f, 0.75f, 1f, 0.45f);

        public float toggleCooldown = 0.8f;

        // La mano tiene que haber salido de la llave al menos este tiempo para
        // volver a accionarla (evita abrir y cerrar con un solo toque).
        public float rearmSeconds = 0.3f;

        public bool IsOpen { get; private set; }

        // Sale agua por cualquiera de las dos llaves.
        public bool Flowing
        {
            get { return IsOpen || (pair != null && pair.IsOpen); }
        }

        // 0 = fria, 0,5 = tibia (las dos abiertas), 1 = solo caliente.
        public float Heat
        {
            get
            {
                if (!Flowing)
                {
                    return 0f;
                }

                bool hotOpen = (hot && IsOpen) || (pair != null && pair.hot && pair.IsOpen);
                bool coldOpen = (!hot && IsOpen) || (pair != null && !pair.hot && pair.IsOpen);
                return hotOpen ? (coldOpen ? 0.5f : 1f) : 0f;
            }
        }

        private static readonly System.Collections.Generic.List<WaterTap> All =
            new System.Collections.Generic.List<WaterTap>();

        // Llave cuyo chorro tiene ese collider (para saber si el agua que
        // toca algo es fria o caliente).
        public static WaterTap ForTrigger(Collider trigger)
        {
            foreach (WaterTap tap in All)
            {
                if (tap != null && tap.waterTrigger == trigger)
                {
                    return tap;
                }
            }

            return null;
        }

        private Renderer marker;
        private float nextToggleTime;
        private float lastHandInsideTime = float.NegativeInfinity;

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Awake()
        {
            Collider own = GetComponent<Collider>();
            own.isTrigger = true;
            marker = IndicatorUtil.CreateSphere("PuntoGrifo", transform, transform.position, markerRadius,
                indicatorMaterial, closedColor);
        }

        private void Start()
        {
            SetOpen(startOpen);
        }

        private void Update()
        {
            if (marker == null)
            {
                return;
            }

            // Pulso suave para llamar la atencion mientras esta cerrada.
            float pulse = IsOpen ? 1f : 1f + 0.15f * Mathf.Sin(Time.time * 4f);
            IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * pulse);
        }

        // No se cuenta cuantas manos hay adentro con OnTriggerExit: si la
        // esfera de la mano se apaga (se perdio el tracking) estando dentro,
        // Unity no siempre avisa la salida y el grifo quedaria trabado.
        private void OnTriggerEnter(Collider other)
        {
            if (!SimpleGrab.IsHandCollider(other))
            {
                return;
            }

            bool rearmed = Time.time - lastHandInsideTime > rearmSeconds;
            lastHandInsideTime = Time.time;
            if (rearmed && Time.time >= nextToggleTime)
            {
                nextToggleTime = Time.time + toggleCooldown;
                SetOpen(!IsOpen);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (SimpleGrab.IsHandCollider(other))
            {
                lastHandInsideTime = Time.time;
            }
        }

        public void SetOpen(bool open)
        {
            bool wasOpen = IsOpen;
            IsOpen = open;
            bool flowing = Flowing;

            // Con la otra llave abierta el chorro no cambia; el chirrido de la
            // llave lo pone esta (si no, lo pone WaterStreamFX).
            if (pair != null && pair.IsOpen && wasOpen != open && Time.timeSinceLevelLoad > 0.5f)
            {
                Hito1FX.PlayAt(open ? "grifo_abrir" : "grifo_cerrar", transform.position, 0.8f);
            }

            if (water != null)
            {
                if (flowing)
                {
                    water.Play();
                }
                else
                {
                    water.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (waterTrigger != null)
            {
                waterTrigger.enabled = flowing;
            }

            if (waterSound != null)
            {
                if (flowing)
                {
                    waterSound.Play();
                }
                else
                {
                    waterSound.Stop();
                }
            }

            IndicatorUtil.SetColor(marker, open ? openColor : closedColor);
            Debug.Log("[Hito1] Llave " + (hot ? "caliente " : "fria ") + (open ? "abierta" : "cerrada"));

            if (open && !wasOpen && Time.timeSinceLevelLoad > 0.5f)
            {
                BurnSequenceManager burn = FindObjectOfType<BurnSequenceManager>();
                if (burn != null)
                {
                    burn.Report(hot ? BurnSequenceManager.BurnEvent.HotWaterOn : BurnSequenceManager.BurnEvent.ColdWaterOn);
                }
            }
        }
    }
}
