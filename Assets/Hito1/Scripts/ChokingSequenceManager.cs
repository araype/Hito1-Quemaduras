using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Maquina de estados del caso "atragantamiento" (maniobra de Heimlich),
    // analoga a BurnSequenceManager y CutSequenceManager:
    //   Choking (el paciente se lleva las manos al cuello) ->
    //   RescuerBehind (el jugador se ubico detras del paciente) ->
    //   HandsPlaced (ambas manos en la zona del abdomen, entre el ombligo y
    //   el esternon) -> Success (tras requiredThrusts compresiones rapidas
    //   hacia adentro y hacia arriba, el objeto sale expulsado).
    // Si se aleja o saca las manos, vuelve al estado anterior (las
    // compresiones ya hechas se conservan). Si pasa timeLimitSeconds sin
    // lograrlo, Failed (el paciente pierde el conocimiento).
    //
    // La zona del abdomen y la marca en el piso se calculan en runtime con
    // los huesos del personaje, asi no dependen de como se pose el modelo.
    public class ChokingSequenceManager : MonoBehaviour
    {
        public enum ChokingState
        {
            Idle,
            Choking,
            RescuerBehind,
            HandsPlaced,
            Success,
            Failed
        }

        [Header("Paciente")]
        public PoseAtragantamiento patient;
        public Transform rescuerSpot;
        public float startDelaySeconds = 2f;
        public float timeLimitSeconds = 120f;

        [Header("Posicion del rescatista")]
        public float maxDistanceBehind = 0.9f;
        public float minBehindDot = 0.2f;

        [Header("Zona del abdomen")]
        public float abdomenForwardOffset = 0.13f;
        public float zoneRadius = 0.14f;
        public bool requireGripWithControllers = true;

        [Header("Compresiones")]
        public int requiredThrusts = 5;
        public float thrustSpeed = 0.4f;
        public float rearmSpeed = 0.1f;
        public float minSecondsBetweenThrusts = 0.3f;

        [Header("Indicadores")]
        public Material indicatorMaterial;
        public Color zoneIdleColor = new Color(1f, 0.78f, 0.15f, 0.35f);
        public Color zoneReadyColor = new Color(0.3f, 0.85f, 0.35f, 0.4f);
        public Color zoneFailColor = new Color(0.9f, 0.15f, 0.1f, 0.4f);

        public ChokingState CurrentState { get; private set; } = ChokingState.Idle;
        public int Thrusts { get; private set; }

        // Golpes en la espalda (entre los omoplatos). No son obligatorios
        // para el exito; se registran para el resumen y el paciente reacciona.
        public int BackBlows { get; private set; }

        public event System.Action<ChokingState> OnStateChanged;
        public event System.Action<int> OnThrust;
        public event System.Action<int> OnBackBlow;

        // Si el paciente tiene PatientBody, sus reacciones (inclinarse, toser,
        // desplomarse) las maneja ChokingPatientInteraction.
        private PatientBody body;

        public bool IsActive
        {
            get
            {
                return CurrentState == ChokingState.Choking || CurrentState == ChokingState.RescuerBehind
                    || CurrentState == ChokingState.HandsPlaced;
            }
        }

        public void RegisterBackBlow()
        {
            if (!IsActive)
            {
                return;
            }

            BackBlows++;
            Debug.Log($"[Choking] Golpe en la espalda {BackBlows}");
            OnBackBlow?.Invoke(BackBlows);
        }

        private OVRCameraRig rig;
        private Transform spine;
        private Transform spineUpper;
        private Transform head;
        private Renderer zone;
        private Renderer floorMarker;
        private float chokingStartTime;
        private float lastThrustTime = float.NegativeInfinity;
        private bool thrustArmed = true;
        private Vector3 previousHandsCenter;
        private bool hasPreviousHands;
        private Vector3 handsVelocity;

        private void Start()
        {
            if (patient == null)
            {
                Debug.LogWarning("[Choking] Falta asignar el paciente (PoseAtragantamiento).", this);
                enabled = false;
                return;
            }

            body = patient.GetComponent<PatientBody>();
            spine = FindBone("Spine");
            spineUpper = FindBone("Spine1");
            head = FindBone("Head");
            if (spine == null || spineUpper == null)
            {
                Debug.LogWarning("[Choking] No se encontraron los huesos Spine/Spine1 del paciente.", this);
                enabled = false;
                return;
            }

            zone = IndicatorUtil.CreateSphere("ZonaAbdomen", transform, AbdomenPoint(), zoneRadius,
                indicatorMaterial, zoneIdleColor);
            zone.enabled = false;

            if (rescuerSpot != null)
            {
                floorMarker = IndicatorUtil.CreateSphere("MarcaRescatista", transform, rescuerSpot.position, 0.25f,
                    indicatorMaterial, zoneIdleColor);
                Vector3 s = floorMarker.transform.localScale;
                floorMarker.transform.localScale = new Vector3(s.x, s.y * 0.04f, s.z);
                floorMarker.enabled = false;
            }

            StartCoroutine(StartChokingAfterDelay());
        }

        private IEnumerator StartChokingAfterDelay()
        {
            yield return new WaitForSeconds(startDelaySeconds);
            chokingStartTime = Time.time;
            ChangeState(ChokingState.Choking);
        }

        private void Update()
        {
            // Si el paciente se desploma, la esfera roja lo acompana.
            if (CurrentState == ChokingState.Failed && zone != null)
            {
                zone.transform.position = AbdomenPoint();
            }

            if (CurrentState == ChokingState.Idle || CurrentState == ChokingState.Success
                || CurrentState == ChokingState.Failed)
            {
                return;
            }

            if (rig == null)
            {
                rig = FindObjectOfType<OVRCameraRig>();
                if (rig == null)
                {
                    return;
                }
            }

            if (timeLimitSeconds > 0f && Time.time - chokingStartTime > timeLimitSeconds)
            {
                ChangeState(ChokingState.Failed);
                return;
            }

            Vector3 abdomen = AbdomenPoint();
            zone.transform.position = abdomen;

            bool behind = IsRescuerBehind();
            bool handsInZone = behind && AreHandsInZone(abdomen);
            UpdateHandsVelocity();

            ChokingState target = handsInZone ? ChokingState.HandsPlaced
                : behind ? ChokingState.RescuerBehind
                : ChokingState.Choking;
            ChangeState(target);

            if (CurrentState == ChokingState.HandsPlaced)
            {
                DetectThrust();
            }
        }

        // Punto entre el ombligo y el esternon, delante del torso.
        public Vector3 AbdomenPoint()
        {
            Vector3 up = (spineUpper.position - spine.position).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(patient.transform.forward, up).normalized;
            Vector3 middle = Vector3.Lerp(spine.position, spineUpper.position, 0.5f);
            return middle + forward * abdomenForwardOffset;
        }

        private bool IsRescuerBehind()
        {
            Transform eye = rig.centerEyeAnchor;
            if (eye == null)
            {
                return false;
            }

            Vector3 toRescuer = eye.position - patient.transform.position;
            toRescuer.y = 0f;
            if (toRescuer.magnitude > maxDistanceBehind)
            {
                return false;
            }

            Vector3 forward = patient.transform.forward;
            forward.y = 0f;
            return Vector3.Dot(toRescuer.normalized, -forward.normalized) >= minBehindDot;
        }

        private bool AreHandsInZone(Vector3 abdomen)
        {
            if (rig.leftHandAnchor == null || rig.rightHandAnchor == null)
            {
                return false;
            }

            float r2 = zoneRadius * zoneRadius;
            if ((rig.leftHandAnchor.position - abdomen).sqrMagnitude > r2
                || (rig.rightHandAnchor.position - abdomen).sqrMagnitude > r2)
            {
                return false;
            }

            // Con controles, el puno cerrado es el boton de agarre. Con manos
            // trackeadas basta con que esten en la zona.
            if (requireGripWithControllers)
            {
                bool leftController = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
                bool rightController = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
                bool handsActive = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
                if (!handsActive && leftController && rightController)
                {
                    return OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch)
                        || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
                }
            }

            return true;
        }

        private void UpdateHandsVelocity()
        {
            if (rig.leftHandAnchor == null || rig.rightHandAnchor == null || Time.deltaTime <= 0f)
            {
                hasPreviousHands = false;
                return;
            }

            Vector3 center = (rig.leftHandAnchor.position + rig.rightHandAnchor.position) * 0.5f;
            if (hasPreviousHands)
            {
                Vector3 instant = (center - previousHandsCenter) / Time.deltaTime;
                handsVelocity = Vector3.Lerp(handsVelocity, instant, 0.5f);
            }

            previousHandsCenter = center;
            hasPreviousHands = true;
        }

        private void DetectThrust()
        {
            // Direccion de la compresion: hacia la espalda del paciente y
            // hacia arriba (en forma de "J").
            Vector3 up = (spineUpper.position - spine.position).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(patient.transform.forward, up).normalized;
            Vector3 thrustDir = (-forward + up).normalized;
            float speed = Vector3.Dot(handsVelocity, thrustDir);

            if (!thrustArmed)
            {
                if (speed < rearmSpeed)
                {
                    thrustArmed = true;
                }
                return;
            }

            if (speed >= thrustSpeed && Time.time - lastThrustTime >= minSecondsBetweenThrusts)
            {
                thrustArmed = false;
                lastThrustTime = Time.time;
                Thrusts++;
                Debug.Log($"[Choking] Compresion {Thrusts}/{requiredThrusts}");
                if (body == null)
                {
                    StartCoroutine(Jolt(thrustDir));
                }

                OnThrust?.Invoke(Thrusts);

                if (Thrusts >= requiredThrusts)
                {
                    ChangeState(ChokingState.Success);
                }
            }
        }

        // Pequeno empujon del paciente con cada compresion.
        private IEnumerator Jolt(Vector3 direction)
        {
            Transform body = patient.transform;
            Vector3 offset = direction * 0.015f;
            body.position += offset;
            yield return new WaitForSeconds(0.08f);
            body.position -= offset;
        }

        public void ChangeState(ChokingState newState)
        {
            if (newState == CurrentState)
            {
                return;
            }

            CurrentState = newState;
            Debug.Log($"[Choking] Estado -> {newState} (compresiones: {Thrusts})");

            if (zone != null)
            {
                zone.enabled = newState == ChokingState.Choking || newState == ChokingState.RescuerBehind
                    || newState == ChokingState.HandsPlaced || newState == ChokingState.Failed;
                IndicatorUtil.SetColor(zone, newState == ChokingState.HandsPlaced ? zoneReadyColor
                    : newState == ChokingState.Failed ? zoneFailColor : zoneIdleColor);
            }

            if (floorMarker != null)
            {
                floorMarker.enabled = newState == ChokingState.Choking;
            }

            if (newState == ChokingState.Success)
            {
                ExpelObject();
                if (body == null)
                {
                    patient.inclinacionTorso = 4f;
                    patient.inclinacionCabeza = 0f;
                    patient.Aplicar();
                }
            }

            OnStateChanged?.Invoke(newState);
        }

        private void ExpelObject()
        {
            Vector3 mouth = head != null ? head.position : AbdomenPoint() + Vector3.up * 0.45f;
            Vector3 forward = patient.transform.forward;
            Renderer piece = IndicatorUtil.CreateSphere("ObjetoExpulsado", null, mouth + forward * 0.12f, 0.018f,
                null, new Color(0.55f, 0.3f, 0.2f, 1f));
            Rigidbody rb = piece.gameObject.AddComponent<Rigidbody>();
            SphereCollider col = piece.gameObject.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            rb.mass = 0.02f;
            rb.velocity = forward * 1.6f + Vector3.up * 0.8f;
        }

        private Transform FindBone(string bone)
        {
            return FindRecursive(patient.transform, patient.prefijoHuesos + bone);
        }

        private static Transform FindRecursive(Transform root, string boneName)
        {
            if (root.name == boneName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindRecursive(root.GetChild(i), boneName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
