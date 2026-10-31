using UnityEngine;

namespace SojaExiles
{
    // Brazo del paciente que el jugador puede arrastrar (la mano quemada o la
    // mano herida). Reemplaza al IK de Animation Rigging y a BodyReachFollow:
    //   - la mano sigue al objetivo agarrable, pero el objetivo no puede
    //     salir del espacio que un brazo alcanza (ni mas lejos que el largo
    //     del brazo, ni detras de la espalda, ni dentro del torso). Si el
    //     jugador tira de mas, la mano se le escapa (SimpleGrab la suelta),
    //     como pasaria con una persona real;
    //   - si la mano queda lejos, el paciente da pasos y gira hacia ella sin
    //     atravesar muebles ni paredes;
    //   - el torso acompana: se inclina para llegar abajo y gira hacia la mano.
    //
    // Corre despues de SimpleGrab (que fija la velocidad del objetivo en su
    // FixedUpdate) para poder corregir esa velocidad.
    [RequireComponent(typeof(PatientBody))]
    [DefaultExecutionOrder(200)]
    public class PatientReach : MonoBehaviour
    {
        public Rigidbody target;
        public bool rightArm = true;

        [Header("Limites del brazo")]
        [Range(0.8f, 1f)] public float reachFraction = 0.97f;
        [Tooltip("Cuanto puede ir la mano detras del plano de los hombros (m).")]
        public float behindLimit = 0.12f;
        [Tooltip("Distancia minima entre la mano y la columna (m).")]
        public float torsoClearance = 0.17f;

        [Header("Seguir con el cuerpo")]
        public bool followBody = true;
        [Tooltip("Fraccion del largo del brazo a partir de la cual el cuerpo se acerca.")]
        public float startFollowFraction = 0.85f;
        public float stepSpeed = 0.6f;
        [Tooltip("Mientras el jugador la lleva del brazo: empieza a caminar antes y puede ir mas rapido.")]
        public float ledFollowFraction = 0.55f;
        public float ledMaxSpeed = 1.5f;
        public float turnSpeed = 80f;
        [Tooltip("Rango comodo del angulo de la mano respecto del frente (grados). Fuera de el, el cuerpo gira.")]
        public float comfortInner = 20f;
        public float comfortOuter = 55f;
        [Tooltip("Radio de la capsula de piernas y cadera que no atraviesa muebles.")]
        public float bodyRadius = 0.14f;
        [Tooltip("Altura de la capsula: el torso puede inclinarse por encima de un meson.")]
        public float bodyHeight = 1.0f;

        [Header("Postura")]
        public float maxLean = 35f;
        [Tooltip("Inclinacion extra si aun asi el brazo no llega (p.ej. al chorro del grifo, al fondo del meson).")]
        public float reachLeanExtra = 15f;
        public float maxTwist = 25f;
        public float postureSpeed = 4f;

        private PatientBody body;
        private Collider[] ownColliders;
        private Collider[] targetColliders;
        private Vector3 desiredHand;
        private bool hasDesired;
        private CharacterController mover;
        private Vector3 lastRootPosition;
        private float smoothedSpeed;

        // Capsula de piernas y cadera con que se desplaza (la usa
        // PatientPhysics para que sus propios colliders no la frenen).
        public CharacterController Mover
        {
            get { return mover; }
        }

        // True si el jugador la lleva del brazo.
        public bool IsLed
        {
            get { return target != null && SimpleGrab.IsHeldObject(target); }
        }

        private void Awake()
        {
            body = GetComponent<PatientBody>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            targetColliders = target != null ? target.GetComponentsInChildren<Collider>(true) : new Collider[0];

            foreach (Behaviour b in GetComponents<Behaviour>())
            {
                if (b.GetType().Name == "RigBuilder" || b.GetType().Name == "BodyReachFollow")
                {
                    b.enabled = false;
                }
            }

            if (target == null)
            {
                Debug.LogWarning("[PatientReach] Falta asignar el objetivo de la mano.", this);
                return;
            }

            if (rightArm)
            {
                body.rightMode = PatientBody.ArmMode.Reach;
                body.rightTarget = target.transform;
            }
            else
            {
                body.leftMode = PatientBody.ArmMode.Reach;
                body.leftTarget = target.transform;
            }
        }

        private void Start()
        {
            lastRootPosition = transform.position;
            if (followBody)
            {
                CreateMover();
            }
        }

        // Ubica a la paciente en otro lugar (al armar la escena): mueve
        // tambien la capsula con que camina.
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            lastRootPosition = position;
            if (mover != null)
            {
                mover.enabled = false;
                mover.transform.position = position;
                mover.enabled = true;
            }
        }

        private void OnDestroy()
        {
            if (mover != null)
            {
                Destroy(mover.gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (target == null || !body.IsReady)
            {
                return;
            }

            Vector3 position = target.position;
            if (target.isKinematic)
            {
                desiredHand = position;
                hasDesired = true;
                return;
            }

            // A donde iria el objetivo este paso si nadie lo limitara.
            float dt = Time.fixedDeltaTime;
            Vector3 next = position + target.velocity * dt;
            desiredHand = next;
            hasDesired = true;

            Vector3 allowed = body.ConstrainReach(rightArm, next, reachFraction, behindLimit, torsoClearance);
            if ((allowed - next).sqrMagnitude > 1e-8f)
            {
                target.velocity = (allowed - position) / dt;
            }
        }

        private void Update()
        {
            if (target == null || !body.IsReady || !hasDesired)
            {
                return;
            }

            float dt = Time.deltaTime;
            Vector3 shoulder = body.ShoulderPosition(rightArm);
            Vector3 chest = body.Chest.position;
            Vector3 toHand = PatientBody.Flat(desiredHand - chest);
            float armLength = body.ArmLength(rightArm);

            if (followBody)
            {
                TurnTowardHand(toHand, dt);

                // Si la llevan del brazo camina antes y, cuanto mas tiran,
                // mas rapido (hasta ledMaxSpeed): asi no se suelta.
                bool led = IsLed;
                Vector3 fromShoulder = PatientBody.Flat(desiredHand - shoulder);
                float excess = fromShoulder.magnitude - armLength * (led ? ledFollowFraction : startFollowFraction);
                if (excess > 0.005f)
                {
                    float speed = led ? Mathf.Clamp(excess * 5f, stepSpeed, ledMaxSpeed) : stepSpeed;
                    Vector3 move = fromShoulder.normalized * Mathf.Min(excess, speed * dt);
                    MoveBody(move);
                }
            }

            // Velocidad real del cuerpo, para que las piernas den pasos.
            Vector3 moved = PatientBody.Flat(transform.position - lastRootPosition);
            lastRootPosition = transform.position;
            float instant = dt > 0f ? moved.magnitude / dt : 0f;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, Mathf.Min(instant, 3f), 1f - Mathf.Exp(-8f * dt));
            body.walkSpeed = smoothedSpeed;

            UpdatePosture(shoulder, toHand, armLength, dt);
        }

        // Gira el cuerpo si la mano queda muy al costado o detras.
        private void TurnTowardHand(Vector3 toHand, float dt)
        {
            if (toHand.magnitude < 0.15f)
            {
                return;
            }

            float angle = Vector3.SignedAngle(PatientBody.Flat(transform.forward), toHand, Vector3.up);
            // Rango comodo: hacia el lado del brazo hasta comfortOuter, hacia
            // el otro lado hasta comfortInner.
            float min = rightArm ? -comfortInner : -comfortOuter;
            float max = rightArm ? comfortOuter : comfortInner;
            float correction = 0f;
            if (angle < min)
            {
                correction = angle - min;
            }
            else if (angle > max)
            {
                correction = angle - max;
            }

            if (Mathf.Abs(correction) < 0.5f)
            {
                return;
            }

            float step = Mathf.Sign(correction) * Mathf.Min(Mathf.Abs(correction), turnSpeed * dt);
            transform.Rotate(0f, step, 0f, Space.World);
        }

        private void UpdatePosture(Vector3 shoulder, Vector3 toHand, float armLength, float dt)
        {
            // Se usa a donde quiere llevar la mano el jugador (aunque el brazo
            // no llegue): asi el torso se inclina para acercarse.
            Vector3 hand = desiredHand;
            float below = shoulder.y - hand.y;
            float fromHips = PatientBody.Flat(hand - body.Hips.position).magnitude;

            float leanDown = Mathf.Clamp01((below - 0.3f) / 0.45f) * maxLean;
            float leanFar = Mathf.Clamp01((fromHips - armLength * 0.7f) / 0.35f) * maxLean;
            // Si el brazo no llega a donde quiere ir la mano, se inclina mas.
            float deficit = Vector3.Distance(shoulder, hand) - armLength * reachFraction;
            float leanReach = Mathf.Clamp01((deficit + 0.04f) / 0.14f) * (maxLean + reachLeanExtra);
            float wantedLean = Mathf.Max(Mathf.Max(leanDown, leanFar), leanReach);

            float angle = toHand.magnitude > 0.1f
                ? Vector3.SignedAngle(PatientBody.Flat(transform.forward), toHand, Vector3.up)
                : 0f;
            float wantedTwist = Mathf.Clamp(angle * 0.35f, -maxTwist, maxTwist);

            float k = 1f - Mathf.Exp(-postureSpeed * dt);
            body.lean = Mathf.Lerp(body.lean, wantedLean, k);
            body.twist = Mathf.Lerp(body.twist, wantedTwist, k);
            body.headPitch = Mathf.Lerp(body.headPitch, Mathf.Clamp(below * 30f, -10f, 25f), k);
        }

        // El cuerpo se mueve con un CharacterController (piernas y cadera):
        // se desliza por muebles y paredes y nunca queda metido en ellos. El
        // torso queda libre para inclinarse por encima de un meson. Va en un
        // objeto aparte para no mezclarse con el Rigidbody del paciente.
        private void CreateMover()
        {
            GameObject go = new GameObject(name + "_CuerpoFisico");
            go.layer = 2; // Ignore Raycast: no estorba rayos ni el teletransporte.
            go.transform.position = transform.position;
            go.AddComponent<NoAutoCollider>();

            mover = go.AddComponent<CharacterController>();
            mover.radius = bodyRadius;
            mover.height = Mathf.Max(bodyHeight - 0.2f, bodyRadius * 2f + 0.05f);
            mover.center = new Vector3(0f, 0.2f + mover.height * 0.5f, 0f);
            mover.skinWidth = 0.02f;
            mover.stepOffset = 0.05f;
            mover.minMoveDistance = 0f;

            // Ni el propio cuerpo, ni la mano agarrable, ni el jugador, ni los
            // objetos sueltos frenan al paciente.
            foreach (Collider col in ownColliders)
            {
                IgnoreWithMover(col);
            }

            foreach (Collider col in targetColliders)
            {
                IgnoreWithMover(col);
            }

            foreach (Collider col in FindObjectsOfType<Collider>())
            {
                Rigidbody rb = col.attachedRigidbody;
                if ((col is CharacterController && col != mover) || SimpleGrab.IsHandCollider(col)
                    || col.GetComponentInParent<OVRCameraRig>() != null || (rb != null && !rb.isKinematic))
                {
                    IgnoreWithMover(col);
                }
            }
        }

        private void IgnoreWithMover(Collider col)
        {
            if (col != null && col != mover)
            {
                Physics.IgnoreCollision(mover, col, true);
            }
        }

        private void MoveBody(Vector3 move)
        {
            if (mover == null)
            {
                transform.position += move;
                return;
            }

            mover.Move(PatientBody.Flat(move));
            Vector3 p = mover.transform.position;
            transform.position = new Vector3(p.x, transform.position.y, p.z);
        }
    }
}
