using UnityEngine;

namespace SojaExiles
{
    // Perilla de la cocina que controla el quemador de la olla. Se gira
    // acercando la mano y apretando (boton lateral, gatillo o pinza: ver
    // HandPress), igual que la llave del grifo; antes bastaba con tocarla.
    // Un punto dorado marca donde esta mientras el fuego esta prendido (solo
    // modo guiado). Apagar el fuego es lo primero ante una quemadura en la
    // cocina: se elimina el peligro.
    // Lo que se ve al apretarla, en los dos modos (tras la prueba en el
    // visor, 2026-11-17: en modo evaluacion no se notaba si se habia
    // apagado): la perilla se hunde un poco, gira despacio con una marca roja
    // que pasa del costado (encendida) a arriba (apagada), la llama se apaga
    // de a poco y aparece "Fuego apagado" junto a la perilla unos segundos.
    // Va en la pieza de la perilla del kit (Knob.00X) mas cercana al quemador.
    // Corre despues de PotFX, que ubica el quemador en su Start.
    [DefaultExecutionOrder(100)]
    public class StoveKnob : MonoBehaviour
    {
        public PotFX pot;
        public bool startOn = true;
        [Tooltip("Cuanto gira la perilla entre apagado y encendido (grados).")]
        public float turnAngle = 90f;
        [Tooltip("Grados por segundo al girar. Antes 360: el giro de 90 grados duraba un cuarto de segundo y no se notaba.")]
        public float turnSpeed = 180f;
        [Tooltip("Cuanto se hunde la perilla al apretarla (m).")]
        public float pushDepth = 0.006f;
        [Tooltip("Segundos que queda hundida antes de girar.")]
        public float pushSeconds = 0.15f;
        [Tooltip("Segundos que se ve el aviso de fuego apagado junto a la perilla.")]
        public float confirmSeconds = 2.5f;

        [Header("Indicador")]
        public Material indicatorMaterial;
        public float markerRadius = 0.018f;
        public Color onColor = new Color(1f, 0.78f, 0.15f, 0.65f);

        public float toggleCooldown = 0.8f;
        [Tooltip("Distancia de la mano al centro de la perilla para poder apretarla (m).")]
        public float pressReach = 0.1f;
        [Tooltip("Una vez apagada no se vuelve a prender al apretarla de nuevo (antes, un segundo toque la prendia y el paso ya contaba como hecho).")]
        public bool stayOffOnceOff = true;
        [Tooltip("Antes del accidente la paciente esta cocinando: apretar la perilla no hace nada.")]
        public bool onlyAfterBurn = true;

        public bool IsOn { get; private set; }

        private Quaternion offRotation;
        private Vector3 axisLocal;
        private float angle;
        private Renderer marker;
        private float nextToggleTime;
        private HandPress press;
        private Vector3 pressCenter;
        private Vector3 offPosition;
        private Vector3 outwardWorld;
        private float pushedAt = -10f;
        private Transform confirm;
        private float confirmUntil;

        private void Awake()
        {
            offRotation = transform.localRotation;
            offPosition = transform.localPosition;

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
            pressCenter = own != null ? own.bounds.center : transform.position;
            press = new HandPress(pressReach);
            SimpleGrab.AddGrabBlocker(BlocksGrab);
            Vector3 front = pressCenter + outward.normalized * 0.025f;
            marker = IndicatorUtil.CreateSphere("PuntoPerilla", transform.parent, front, markerRadius,
                indicatorMaterial, onColor);
            outwardWorld = outward.normalized;
            // La marca se pone con la perilla todavia en apagado: arriba.
            CreatePointer(own);

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
            // Primero se hunde y despues gira, como una perilla de gas real.
            float target = IsOn ? turnAngle : 0f;
            if (Time.time - pushedAt >= pushSeconds && !Mathf.Approximately(angle, target))
            {
                angle = Mathf.MoveTowards(angle, target, turnSpeed * Time.deltaTime);
            }

            ApplyRotation();
            UpdateConfirm();

            if (press != null)
            {
                press.reach = pressReach;
                if (press.Update(pressCenter) >= 0 && CanToggle() && Time.time >= nextToggleTime)
                {
                    nextToggleTime = Time.time + toggleCooldown;
                    SetOn(!IsOn);
                }
            }

            UpdateMarker();
            if (marker != null && marker.enabled)
            {
                // Con la mano al alcance, el punto crece: ya se puede apretar.
                float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 4f);
                float near = press != null && press.Near >= 0 ? 1.4f : 1f;
                IndicatorUtil.SetWorldDiameter(marker.transform, markerRadius * 2f * pulse * near);
            }
        }

        private void OnDestroy()
        {
            SimpleGrab.RemoveGrabBlocker(BlocksGrab);
            if (confirm != null)
            {
                Destroy(confirm.gameObject);
            }
        }

        // Apretar junto a la perilla no agarra otra cosa.
        private bool BlocksGrab(Vector3 point)
        {
            return press != null && isActiveAndEnabled && press.Contains(point, pressCenter) && CanToggle();
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.AngleAxis(angle, axisLocal) * offRotation;
            // Hundida mientras gira; vuelve a salir al terminar.
            float sincePush = Time.time - pushedAt;
            bool turning = !Mathf.Approximately(angle, IsOn ? turnAngle : 0f);
            float depth;
            if (sincePush < pushSeconds)
            {
                depth = Mathf.Clamp01(sincePush / (pushSeconds * 0.5f));
            }
            else if (turning)
            {
                depth = 1f;
            }
            else
            {
                depth = Mathf.Clamp01(1f - (sincePush - pushSeconds - TurnSeconds) / 0.12f);
            }

            transform.localPosition = offPosition - axisLocal * (pushDepth * depth / ParentScale);
        }

        private float TurnSeconds
        {
            get { return turnAngle / Mathf.Max(1f, turnSpeed); }
        }

        private float ParentScale
        {
            get { return transform.parent != null ? Mathf.Max(1e-4f, transform.parent.lossyScale.x) : 1f; }
        }

        // Raya roja en el frente de la manija de la perilla, del centro hacia
        // arriba (en apagado la manija del kit queda vertical): gira con ella.
        private void CreatePointer(Renderer own)
        {
            if (own == null)
            {
                return;
            }

            Bounds b = own.bounds;
            Vector3 n = outwardWorld;
            float depth = Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.y) * b.extents.y + Mathf.Abs(n.z) * b.extents.z;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            Vector3 at = b.center + n * (depth + 0.0008f) + up * (b.extents.y * 0.45f);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MarcaPerilla";
            Destroy(go.GetComponent<Collider>());
            go.AddComponent<NoAutoCollider>();
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(n, up));
            go.transform.localScale = new Vector3(0.0045f, 0.018f, 0.0015f);
            go.transform.SetParent(transform, true);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = PropKit.Mat("Perilla_Marca", new Color(0.8f, 0.08f, 0.05f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Aviso de fuego apagado junto a la perilla, mirando al jugador.
        private void ShowConfirm()
        {
            if (confirm == null)
            {
                GameObject go = new GameObject("AvisoPerilla");
                go.AddComponent<NoAutoCollider>();
                go.AddComponent<OverlayPanel>();
                VRPanelKit.CardPanel(go.transform, Vector2.zero, new Vector2(0.21f, 0.05f));
                VRPanelKit.Text(go.transform, "<b>Fuego apagado</b>", new Vector2(0f, 0.011f), 0.022f,
                    new Color(0.55f, 1f, 0.6f), TextAnchor.UpperCenter, TextAlignment.Center);
                confirm = go.transform;
            }

            confirm.gameObject.SetActive(true);
            confirmUntil = Time.time + TurnSeconds + pushSeconds + confirmSeconds;
            UpdateConfirm();
        }

        private void UpdateConfirm()
        {
            if (confirm == null || !confirm.gameObject.activeSelf)
            {
                return;
            }

            if (Time.time > confirmUntil)
            {
                confirm.gameObject.SetActive(false);
                return;
            }

            Transform head = VRPanelKit.Head();
            Vector3 pos = pressCenter + Vector3.up * 0.1f + outwardWorld * 0.05f;
            confirm.position = pos;
            if (head != null)
            {
                confirm.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
            }
        }

        private bool CanToggle()
        {
            if (stayOffOnceOff && !IsOn)
            {
                return false;
            }

            if (onlyAfterBurn)
            {
                if (burnSequence == null)
                {
                    burnSequence = FindObjectOfType<BurnSequenceManager>();
                }

                if (burnSequence != null && burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle)
                {
                    return false;
                }
            }

            return true;
        }

        private BurnSequenceManager burnSequence;

        public void SetOn(bool on)
        {
            IsOn = on;
            pushedAt = Time.time;
            Hito1FX.PlayAt("perilla_clic", transform.position, 0.8f);
            if (!on)
            {
                ShowConfirm();
            }

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

        // El punto dorado solo se ve mientras hay fuego (invita a apagarlo) y
        // en modo guiado.
        private void UpdateMarker()
        {
            if (marker != null)
            {
                marker.enabled = IsOn && IndicatorUtil.HintsVisible;
            }
        }
    }
}
