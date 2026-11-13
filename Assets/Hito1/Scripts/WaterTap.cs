using UnityEngine;

namespace SojaExiles
{
    // Llave del grifo: se abre o cierra acercando la mano y apretando (boton
    // lateral, gatillo o pinza: ver HandPress); antes bastaba con tocarla y
    // se abria o cerraba al pasar la mano. Si las dos llaves estan al
    // alcance, cuenta la mas cercana. Un punto marca donde esta. Con la llave
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
        [Tooltip("Distancia de la mano a la llave para poder apretarla (m).")]
        public float pressReach = 0.1f;

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
        private HandPress press;

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

        // Apretar junto a la llave no agarra otra cosa.
        private bool BlocksGrab(Vector3 point)
        {
            return press != null && press.Contains(point, transform.position);
        }

        // Otra llave queda mas cerca de ese punto que esta.
        private bool OtherCloser(Vector3 point)
        {
            float mine = Vector3.Distance(point, transform.position);
            foreach (WaterTap tap in All)
            {
                if (tap != null && tap != this && Vector3.Distance(point, tap.transform.position) < mine)
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            Collider own = GetComponent<Collider>();
            own.isTrigger = true;
            marker = IndicatorUtil.CreateSphere("PuntoGrifo", transform, transform.position, markerRadius,
                indicatorMaterial, closedColor);
            press = new HandPress(pressReach);
        }

        private void Start()
        {
            SetOpen(startOpen);
        }

        private void Update()
        {
            if (press != null)
            {
                press.reach = pressReach;
                if (press.Update(transform.position, OtherCloser) >= 0 && Time.time >= nextToggleTime)
                {
                    nextToggleTime = Time.time + toggleCooldown;
                    SetOpen(!IsOpen);
                }
            }

            if (marker == null)
            {
                return;
            }

            marker.enabled = IndicatorUtil.HintsVisible;

            // Pulso suave para llamar la atencion mientras esta cerrada; con
            // la mano al alcance el punto crece (ya se puede apretar).
            float pulse = IsOpen ? 1f : 1f + 0.15f * Mathf.Sin(Time.time * 4f);
            float near = press != null && press.Near >= 0 ? 1.35f : 1f;
            IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * pulse * near);
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
