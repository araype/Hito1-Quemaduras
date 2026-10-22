using UnityEngine;

namespace SojaExiles
{
    // Hace interactuable al paciente de la escena de atragantamiento:
    //   - Cuerpo con colliders en los huesos (torso, hombros, cabeza, brazos):
    //     el jugador no lo atraviesa caminando y puede tocarlo y agarrarlo.
    //   - Brazos agarrables: se pueden tomar y mover con limites humanos.
    //     Al soltarlos, el paciente vuelve a llevarse las manos al cuello.
    //   - Hombros agarrables: tomandolo por los hombros o la espalda alta se
    //     lo puede inclinar hacia adelante (posicion correcta para ayudarlo)
    //     o hacia un costado. Al soltarlo, recupera la postura.
    //   - Golpes en la espalda (entre los omoplatos): el paciente reacciona y
    //     se cuentan en ChokingSequenceManager.
    //   - Reacciones: se agita y tose mientras se ahoga, se dobla con cada
    //     compresion abdominal, se endereza aliviado al expulsar el objeto y
    //     se desploma de rodillas si se acaba el tiempo (Failed).
    //
    // Mientras las manos del jugador estan en la zona del abdomen (maniobra
    // de Heimlich) no se pueden agarrar los brazos ni los hombros, para que
    // apretar el agarre en las compresiones no tome un brazo por error.
    [RequireComponent(typeof(PatientBody))]
    [DefaultExecutionOrder(200)]
    public class ChokingPatientInteraction : MonoBehaviour
    {
        public ChokingSequenceManager sequence;

        [Header("Postura mientras se ahoga")]
        public float chokingLean = 18f;
        public float chokingHeadPitch = 10f;
        [Range(0f, 2f)] public float distressAmount = 1f;

        [Header("Brazos agarrables")]
        [Range(0.8f, 1f)] public float reachFraction = 0.97f;
        public float behindLimit = 0.15f;
        public float torsoClearance = 0.16f;

        [Header("Hombros agarrables (inclinar al paciente)")]
        public float maxPullLean = 40f;
        public float maxPullBack = 10f;
        public float maxPullSide = 20f;
        [Tooltip("Cuanto hay que mover la mano (m) para la inclinacion maxima.")]
        public float pullForFullLean = 0.25f;
        [Tooltip("Mas alla de esto el paciente no se deja llevar y la mano se suelta.")]
        public float maxHandleTravel = 0.3f;

        [Header("Golpes en la espalda")]
        public float backBlowRadius = 0.15f;
        public float backBlowSpeed = 0.9f;
        public float backBlowCooldown = 0.4f;

        [Header("Reacciones (impulsos al resorte del cuerpo)")]
        public float thrustPitchImpulse = 160f;
        public float thrustLiftImpulse = 0.35f;
        public float backBlowPitchImpulse = 110f;
        public float coughPitchImpulse = 55f;

        [Header("Desplome (Failed)")]
        public float collapseSeconds = 1.6f;

        private PatientBody body;
        private OVRCameraRig rig;

        private Rigidbody rightHandle, leftHandle, torsoHandle;
        private Grabbable rightGrab, leftGrab, torsoGrab;
        private Collider rightHandleCol, leftHandleCol, torsoHandleCol;

        private bool torsoWasHeld;
        private bool torsoGrabStarted;
        private Vector3 torsoGrabStart;
        private float pullLean, pullSide, pullLeanVelocity, pullSideVelocity;

        private float baseLean, baseSide, baseHead;
        private float distress;
        private float nextCough;

        private Vector3 previousLeftHand, previousRightHand;
        private Vector3 leftHandVelocity, rightHandVelocity;
        private bool hasPreviousHands;
        private float lastBackBlowTime = float.NegativeInfinity;

        private void Awake()
        {
            body = GetComponent<PatientBody>();
            if (sequence == null)
            {
                sequence = FindObjectOfType<ChokingSequenceManager>();
            }

            body.rightMode = PatientBody.ArmMode.Rest;
            body.leftMode = PatientBody.ArmMode.Rest;
        }

        private void Start()
        {
            if (!body.IsReady)
            {
                enabled = false;
                return;
            }

            CreateBodyColliders();

            rightHandle = CreateHandle("AsaBrazoDer", body.WristPosition(true), out rightGrab, out rightHandleCol);
            leftHandle = CreateHandle("AsaBrazoIzq", body.WristPosition(false), out leftGrab, out leftHandleCol);
            torsoHandle = CreateHandle("AsaHombros", ShouldersPoint(), out torsoGrab, out torsoHandleCol);

            AddRedirect(body.UpperArm(true), rightHandle);
            AddRedirect(body.ForeArm(true), rightHandle);
            AddRedirect(body.HandBone(true), rightHandle);
            AddRedirect(body.UpperArm(false), leftHandle);
            AddRedirect(body.ForeArm(false), leftHandle);
            AddRedirect(body.HandBone(false), leftHandle);
            AddRedirect(body.Chest, torsoHandle, "Col_Hombros");

            IgnoreHandleCollisions();

            if (sequence != null)
            {
                sequence.OnThrust += HandleThrust;
                sequence.OnBackBlow += HandleBackBlow;
            }

            baseLean = 2f;
            nextCough = Time.time + 2.5f;
        }

        private void OnDestroy()
        {
            if (sequence != null)
            {
                sequence.OnThrust -= HandleThrust;
                sequence.OnBackBlow -= HandleBackBlow;
            }
        }

        private void HandleThrust(int count)
        {
            body.Impulse(thrustPitchImpulse, thrustLiftImpulse);
        }

        private void HandleBackBlow(int count)
        {
            body.Impulse(backBlowPitchImpulse, 0.05f);
        }

        // ---------------- por frame ----------------

        private void Update()
        {
            float dt = Time.deltaTime;
            ChokingSequenceManager.ChokingState state = sequence != null
                ? sequence.CurrentState
                : ChokingSequenceManager.ChokingState.Idle;
            bool active = sequence != null && sequence.IsActive;
            bool failed = state == ChokingSequenceManager.ChokingState.Failed;

            UpdateHandsVelocity(dt);
            UpdateHandleAvailability(failed);
            UpdateArms(active, failed);
            UpdateTorsoPull();
            UpdatePosture(state, active, failed, dt);

            if (active)
            {
                DetectBackBlow();
            }
        }

        private void FixedUpdate()
        {
            // Mano del paciente: no mas lejos de lo que un brazo alcanza.
            ConstrainArmHandle(rightHandle, rightGrab, true);
            ConstrainArmHandle(leftHandle, leftGrab, false);

            // Hombros: el paciente se deja inclinar, no arrastrar por la sala.
            if (torsoGrab.IsHeld && !torsoHandle.isKinematic)
            {
                BeginTorsoGrabIfNeeded();

                float dt = Time.fixedDeltaTime;
                Vector3 next = torsoHandle.position + torsoHandle.velocity * dt;
                Vector3 offset = next - torsoGrabStart;
                if (offset.magnitude > maxHandleTravel)
                {
                    Vector3 allowed = torsoGrabStart + offset.normalized * maxHandleTravel;
                    torsoHandle.velocity = (allowed - torsoHandle.position) / dt;
                }
            }
        }

        private void ConstrainArmHandle(Rigidbody handle, Grabbable grab, bool rightSide)
        {
            if (!grab.IsHeld || handle.isKinematic)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 next = handle.position + handle.velocity * dt;
            Vector3 allowed = body.ConstrainReach(rightSide, next, reachFraction, behindLimit, torsoClearance);
            if ((allowed - next).sqrMagnitude > 1e-8f)
            {
                handle.velocity = (allowed - handle.position) / dt;
            }
        }

        private void UpdateArms(bool active, bool failed)
        {
            PatientBody.ArmMode free = active ? PatientBody.ArmMode.Neck : PatientBody.ArmMode.Rest;

            if (rightGrab.IsHeld && !failed)
            {
                body.rightMode = PatientBody.ArmMode.Reach;
                body.rightTarget = rightHandle.transform;
            }
            else
            {
                body.rightMode = free;
                SyncHandle(rightHandle, body.WristPosition(true));
            }

            if (leftGrab.IsHeld && !failed)
            {
                body.leftMode = PatientBody.ArmMode.Reach;
                body.leftTarget = leftHandle.transform;
            }
            else
            {
                body.leftMode = free;
                SyncHandle(leftHandle, body.WristPosition(false));
            }
        }

        private void UpdateTorsoPull()
        {
            float wantedLean = 0f;
            float wantedSide = 0f;

            if (torsoGrab.IsHeld)
            {
                BeginTorsoGrabIfNeeded();
                Vector3 offset = torsoHandle.position - torsoGrabStart;
                Vector3 fwd = PatientBody.Flat(transform.forward).normalized;
                Vector3 right = PatientBody.Flat(transform.right).normalized;
                wantedLean = Mathf.Clamp(Vector3.Dot(offset, fwd) / pullForFullLean * maxPullLean,
                    -maxPullBack, maxPullLean);
                wantedSide = Mathf.Clamp(Vector3.Dot(offset, right) / pullForFullLean * maxPullSide,
                    -maxPullSide, maxPullSide);
            }
            else
            {
                SyncHandle(torsoHandle, ShouldersPoint());
            }

            torsoWasHeld = torsoGrab.IsHeld;
            if (!torsoWasHeld)
            {
                torsoGrabStarted = false;
            }

            pullLean = Mathf.SmoothDamp(pullLean, wantedLean, ref pullLeanVelocity, torsoGrab.IsHeld ? 0.1f : 0.35f);
            pullSide = Mathf.SmoothDamp(pullSide, wantedSide, ref pullSideVelocity, torsoGrab.IsHeld ? 0.1f : 0.35f);
        }

        // Guarda donde se tomo a la persona por los hombros (la primera vez
        // que se ve agarrada, sea en Update o en FixedUpdate).
        private void BeginTorsoGrabIfNeeded()
        {
            if (!torsoGrabStarted)
            {
                torsoGrabStart = torsoHandle.position;
                torsoGrabStarted = true;
            }
        }

        private void UpdatePosture(ChokingSequenceManager.ChokingState state, bool active, bool failed, float dt)
        {
            float targetLean, targetSide, targetHead;
            if (failed)
            {
                targetLean = 45f;
                targetSide = 6f;
                targetHead = 35f;
            }
            else if (state == ChokingSequenceManager.ChokingState.Success)
            {
                targetLean = 3f;
                targetSide = 0f;
                targetHead = -4f;
            }
            else if (active)
            {
                targetLean = chokingLean;
                targetSide = 0f;
                targetHead = chokingHeadPitch;
            }
            else
            {
                targetLean = 2f;
                targetSide = 0f;
                targetHead = 0f;
            }

            float k = 1f - Mathf.Exp(-3f * dt);
            baseLean = Mathf.Lerp(baseLean, targetLean, k);
            baseSide = Mathf.Lerp(baseSide, targetSide, k);
            baseHead = Mathf.Lerp(baseHead, targetHead, k);
            distress = Mathf.MoveTowards(distress, active ? distressAmount : 0f, dt * 1.5f);

            float t = Time.time;
            float breathing = state == ChokingSequenceManager.ChokingState.Success ? Mathf.Sin(t * 1.6f) * 1.5f : 0f;

            body.lean = baseLean + breathing + pullLean
                + distress * (Mathf.Sin(t * 1.7f) * 3f + Mathf.Sin(t * 4.3f) * 1f);
            body.sideBend = baseSide + pullSide + distress * Mathf.Sin(t * 0.9f) * 3f;
            body.twist = distress * Mathf.Sin(t * 1.1f) * 6f;
            body.headPitch = baseHead + distress * Mathf.Sin(t * 3.1f) * 3f;
            body.headYaw = distress * Mathf.Sin(t * 2.4f) * 8f;
            body.kneel = Mathf.MoveTowards(body.kneel, failed ? 1f : 0f, dt / Mathf.Max(0.1f, collapseSeconds));

            // Tos: espasmos cortos hacia adelante mientras se ahoga.
            if (active && t >= nextCough)
            {
                body.Impulse(coughPitchImpulse * distress, 0.08f * distress);
                nextCough = t + Random.Range(1.8f, 3.5f);
            }
        }

        // ---------------- golpes en la espalda ----------------

        private void UpdateHandsVelocity(float dt)
        {
            if (rig == null)
            {
                rig = FindObjectOfType<OVRCameraRig>();
            }

            if (rig == null || rig.leftHandAnchor == null || rig.rightHandAnchor == null || dt <= 0f)
            {
                hasPreviousHands = false;
                return;
            }

            Vector3 l = rig.leftHandAnchor.position;
            Vector3 r = rig.rightHandAnchor.position;
            if (hasPreviousHands)
            {
                leftHandVelocity = Vector3.Lerp(leftHandVelocity, (l - previousLeftHand) / dt, 0.5f);
                rightHandVelocity = Vector3.Lerp(rightHandVelocity, (r - previousRightHand) / dt, 0.5f);
            }

            previousLeftHand = l;
            previousRightHand = r;
            hasPreviousHands = true;
        }

        private void DetectBackBlow()
        {
            if (!hasPreviousHands || Time.time - lastBackBlowTime < backBlowCooldown)
            {
                return;
            }

            Vector3 fwd = body.TorsoForward;
            Vector3 back = body.Chest.position - fwd * 0.12f + body.TorsoUp * 0.03f;
            if (IsBlow(previousLeftHand, leftHandVelocity, back, fwd)
                || IsBlow(previousRightHand, rightHandVelocity, back, fwd))
            {
                lastBackBlowTime = Time.time;
                sequence.RegisterBackBlow();
            }
        }

        private bool IsBlow(Vector3 hand, Vector3 velocity, Vector3 back, Vector3 fwd)
        {
            return (hand - back).sqrMagnitude <= backBlowRadius * backBlowRadius
                && Vector3.Dot(velocity, fwd) >= backBlowSpeed;
        }

        // ---------------- agarre ----------------

        // No agarrar brazos ni hombros mientras las manos hacen la maniobra
        // de Heimlich, ni cuando el paciente ya se desplomo.
        private void UpdateHandleAvailability(bool failed)
        {
            bool handsAtAbdomen = false;
            if (sequence != null && sequence.isActiveAndEnabled && rig != null && rig.leftHandAnchor != null
                && rig.rightHandAnchor != null)
            {
                Vector3 abdomen = sequence.AbdomenPoint();
                float r = sequence.zoneRadius * 1.6f;
                handsAtAbdomen = (rig.leftHandAnchor.position - abdomen).sqrMagnitude < r * r
                    || (rig.rightHandAnchor.position - abdomen).sqrMagnitude < r * r;
            }

            bool available = !failed && !handsAtAbdomen;
            SetGrabbable(rightHandleCol, rightGrab, available);
            SetGrabbable(leftHandleCol, leftGrab, available);
            SetGrabbable(torsoHandleCol, torsoGrab, available);
        }

        private static void SetGrabbable(Collider col, Grabbable grab, bool available)
        {
            // Lo que ya esta agarrado no se suelta por esto.
            if (!grab.IsHeld)
            {
                col.enabled = available;
            }
        }

        private static void SyncHandle(Rigidbody handle, Vector3 position)
        {
            if (!handle.isKinematic)
            {
                return;
            }

            handle.position = position;
            handle.transform.position = position;
        }

        private Vector3 ShouldersPoint()
        {
            return body.Chest.position + body.TorsoUp * 0.08f;
        }

        private Rigidbody CreateHandle(string handleName, Vector3 position, out Grabbable grab, out Collider col)
        {
            GameObject go = new GameObject(handleName);
            go.transform.SetParent(transform.parent, true);
            go.transform.position = position;
            go.AddComponent<NoAutoCollider>();

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = 0.015f;
            col = sphere;

            grab = go.AddComponent<Grabbable>();
            grab.trackRotation = false;
            grab.respawnIfLost = false;
            return rb;
        }

        // Colliders del cuerpo. Si el modelo ya trae alguno en ese hueso, se respeta.
        private void CreateBodyColliders()
        {
            AddCapsule(body.Spine, "Col_Torso", 0.15f, 0.5f, 1, new Vector3(0f, 0.12f, 0f));
            AddCapsule(body.Chest, "Col_Hombros", 0.08f, 0.42f, 0, new Vector3(0f, 0.1f, -0.02f));
            AddSphere(body.Head, "Col_Cabeza", 0.1f, new Vector3(0f, 0.09f, 0.02f));

            foreach (bool rightSide in new[] { true, false })
            {
                Transform upper = body.UpperArm(rightSide);
                Transform fore = body.ForeArm(rightSide);
                Transform hand = body.HandBone(rightSide);
                float upperLength = Vector3.Distance(upper.position, fore.position);
                float foreLength = Vector3.Distance(fore.position, hand.position);
                AddCapsule(upper, rightSide ? "Col_BrazoDer" : "Col_BrazoIzq", 0.05f, upperLength + 0.04f, 1,
                    new Vector3(0f, upperLength * 0.5f, 0f));
                AddCapsule(fore, rightSide ? "Col_AntebrazoDer" : "Col_AntebrazoIzq", 0.04f, foreLength + 0.04f, 1,
                    new Vector3(0f, foreLength * 0.5f, 0f));
                AddSphere(hand, rightSide ? "Col_ManoDer" : "Col_ManoIzq", 0.05f, new Vector3(0f, 0.07f, 0f));
            }
        }

        // Las medidas se dan en metros del mundo; se convierten a la escala del hueso.
        private static void AddCapsule(Transform bone, string colliderName, float radius, float height, int direction,
            Vector3 center)
        {
            if (bone == null || bone.Find(colliderName) != null)
            {
                return;
            }

            float scale = Mathf.Max(1e-5f, bone.lossyScale.x);
            GameObject go = new GameObject(colliderName);
            go.transform.SetParent(bone, false);
            go.AddComponent<NoAutoCollider>();
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = direction;
            capsule.radius = radius / scale;
            capsule.height = height / scale;
            capsule.center = center / scale;
        }

        private static void AddSphere(Transform bone, string colliderName, float radius, Vector3 center)
        {
            if (bone == null || bone.Find(colliderName) != null)
            {
                return;
            }

            float scale = Mathf.Max(1e-5f, bone.lossyScale.x);
            GameObject go = new GameObject(colliderName);
            go.transform.SetParent(bone, false);
            go.AddComponent<NoAutoCollider>();
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = radius / scale;
            sphere.center = center / scale;
        }

        private static void AddRedirect(Transform bone, Rigidbody target, string colliderName = null)
        {
            if (bone == null)
            {
                return;
            }

            foreach (Transform child in bone)
            {
                if (child.GetComponent<Collider>() == null)
                {
                    continue;
                }

                if (colliderName != null && child.name != colliderName)
                {
                    continue;
                }

                if (colliderName == null && !child.name.StartsWith("Col_"))
                {
                    continue;
                }

                GrabRedirect redirect = child.GetComponent<GrabRedirect>();
                if (redirect == null)
                {
                    redirect = child.gameObject.AddComponent<GrabRedirect>();
                }

                redirect.target = target;
            }
        }

        // Las asas no deben chocar con el propio cuerpo del paciente.
        private void IgnoreHandleCollisions()
        {
            Collider[] patientColliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider handle in new[] { rightHandleCol, leftHandleCol, torsoHandleCol })
            {
                foreach (Collider col in patientColliders)
                {
                    Physics.IgnoreCollision(handle, col, true);
                }

                foreach (Collider other in new[] { rightHandleCol, leftHandleCol, torsoHandleCol })
                {
                    if (other != handle)
                    {
                        Physics.IgnoreCollision(handle, other, true);
                    }
                }
            }
        }
    }
}
