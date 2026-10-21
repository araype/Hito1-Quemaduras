using UnityEngine;

namespace SojaExiles
{
    // Perilla de la cocina que controla el quemador de la olla. Se gira
    // tocandola con la mano, igual que la llave del grifo (un punto dorado
    // marca donde tocar mientras el fuego esta prendido). Apagar el fuego es
    // lo primero ante una quemadura en la cocina: se elimina el peligro.
    // Va en la pieza de la perilla del kit (Knob.00X) mas cercana al quemador.
    // Corre despues de PotFX, que ubica el quemador en su Start.
    [DefaultExecutionOrder(100)]
    public class StoveKnob : MonoBehaviour
    {
        public PotFX pot;
        public bool startOn = true;
        [Tooltip("Cuanto gira la perilla entre apagado y encendido (grados).")]
        public float turnAngle = 90f;
        public float turnSpeed = 360f;

        [Header("Indicador")]
        public Material indicatorMaterial;
        public float markerRadius = 0.018f;
        public Color onColor = new Color(1f, 0.78f, 0.15f, 0.65f);

        public float toggleCooldown = 0.8f;
        public float rearmSeconds = 0.3f;

        public bool IsOn { get; private set; }

        private Quaternion offRotation;
        private Vector3 axisLocal;
        private float angle;
        private Renderer marker;
        private float nextToggleTime;
        private float lastHandInsideTime = float.NegativeInfinity;

        private void Awake()
        {
            offRotation = transform.localRotation;

            Renderer own = GetComponent<Renderer>();
            Bounds bounds = own != null ? own.bounds : new Bounds(transform.position, Vector3.one * 0.04f);

            // Zona de toque un poco mas grande que la perilla.
            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            float scale = Mathf.Max(1e-4f, transform.lossyScale.x);
            trigger.center = transform.InverseTransformPoint(bounds.center);
            trigger.radius = Mathf.Max(0.035f, bounds.extents.magnitude) / scale;
        }

        private void Start()
        {
            if (pot == null)
            {
                pot = FindObjectOfType<PotFX>();
            }

            // La perilla gira sobre el eje que sale del panel: horizontal,
            // desde el quemador hacia la perilla.
            Vector3 burner = pot != null ? pot.BurnerPosition : transform.position - transform.forward;
            Vector3 outward = Vector3.ProjectOnPlane(GetComponent<Renderer>() != null
                ? GetComponent<Renderer>().bounds.center - burner
                : transform.position - burner, Vector3.up);
            if (outward.sqrMagnitude < 1e-6f)
            {
                outward = transform.forward;
            }

            axisLocal = transform.parent != null
                ? transform.parent.InverseTransformDirection(outward.normalized)
                : outward.normalized;

            Renderer own = GetComponent<Renderer>();
            Vector3 front = (own != null ? own.bounds.center : transform.position) + outward.normalized * 0.025f;
            marker = IndicatorUtil.CreateSphere("PuntoPerilla", transform.parent, front, markerRadius,
                indicatorMaterial, onColor);

            IsOn = startOn;
            angle = IsOn ? turnAngle : 0f;
            ApplyRotation();
            if (pot != null)
            {
                pot.SetBurner(IsOn);
            }

            UpdateMarker();
        }

        private void Update()
        {
            float target = IsOn ? turnAngle : 0f;
            if (!Mathf.Approximately(angle, target))
            {
                angle = Mathf.MoveTowards(angle, target, turnSpeed * Time.deltaTime);
                ApplyRotation();
            }

            if (marker != null && marker.enabled)
            {
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 4f);
                IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * pulse);
            }
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.AngleAxis(angle, axisLocal) * offRotation;
        }

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
                SetOn(!IsOn);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (SimpleGrab.IsHandCollider(other))
            {
                lastHandInsideTime = Time.time;
            }
        }

        public void SetOn(bool on)
        {
            IsOn = on;
            Hito1FX.PlayAt("perilla_clic", transform.position, 0.8f);
            if (pot != null)
            {
                pot.SetBurner(on);
            }

            UpdateMarker();
            Debug.Log("[Hito1] Cocina " + (on ? "encendida" : "apagada"));

            BurnSequenceManager burn = FindObjectOfType<BurnSequenceManager>();
            if (burn != null)
            {
                burn.Report(on ? BurnSequenceManager.BurnEvent.BurnerOn : BurnSequenceManager.BurnEvent.BurnerOff);
            }
        }

        // El punto dorado solo se ve mientras hay fuego (invita a apagarlo).
        private void UpdateMarker()
        {
            if (marker != null)
            {
                marker.enabled = IsOn;
            }
        }
    }
}
