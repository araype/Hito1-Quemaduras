using UnityEngine;

namespace SojaExiles
{
    // Llave del grifo: se abre o cierra tocandola con la mano (la esfera
    // fisica de SimpleGrab, que sigue al control o a la mano trackeada). Un
    // punto dorado marca donde tocar, como en el prototipo. Con la llave
    // cerrada no sale agua y el chorro no cuenta para enfriar o lavar.
    [RequireComponent(typeof(Collider))]
    public class WaterTap : MonoBehaviour
    {
        public ParticleSystem water;
        public Collider waterTrigger;
        public AudioSource waterSound;
        public bool startOpen;

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

        private Renderer marker;
        private float nextToggleTime;
        private float lastHandInsideTime = float.NegativeInfinity;

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
            IsOpen = open;

            if (water != null)
            {
                if (open)
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
                waterTrigger.enabled = open;
            }

            if (waterSound != null)
            {
                if (open)
                {
                    waterSound.Play();
                }
                else
                {
                    waterSound.Stop();
                }
            }

            IndicatorUtil.SetColor(marker, open ? openColor : closedColor);
            Debug.Log("[Hito1] Grifo " + (open ? "abierto" : "cerrado"));
        }
    }
}
