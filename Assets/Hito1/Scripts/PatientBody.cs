using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Mueve el esqueleto del paciente (rig de Mixamo sin animaciones) con
    // poses calculadas por codigo:
    //   - postura del torso y la cabeza (inclinarse, girar, ladearse),
    //   - brazos con IK de dos huesos: el codo dobla como una bisagra real y
    //     la muneca sigue al antebrazo (no queda fija en el mundo),
    //   - brazos en reposo (colgando a los costados, ya no en T-pose),
    //   - rodillas (para desplomarse de rodillas),
    //   - reacciones con resorte (compresiones, golpes, tos).
    //
    // Cada frame parte de la pose de bind (la T-pose guardada en la malla),
    // asi nada se acumula ni se tuerce con el tiempo. Otros scripts solo
    // cambian los parametros publicos (modo de cada brazo, inclinacion...).
    [DefaultExecutionOrder(300)]
    public class PatientBody : MonoBehaviour
    {
        public enum ArmMode
        {
            Rest,   // colgando al costado del cuerpo
            Reach,  // la muneca va al objetivo (rightTarget / leftTarget)
            Neck    // senal universal de atragantamiento: manos al cuello
        }

        public string bonePrefix = "mixamorig8:";

        [Header("Brazos")]
        public ArmMode rightMode = ArmMode.Rest;
        public ArmMode leftMode = ArmMode.Rest;
        public Transform rightTarget;
        public Transform leftTarget;
        [Tooltip("Segundos para pasar de un modo de brazo a otro sin saltos.")]
        public float modeBlendSeconds = 0.45f;

        [Header("Postura (grados)")]
        [Tooltip("Positivo: el torso se inclina hacia adelante.")]
        public float lean;
        [Tooltip("Positivo: el torso se ladea hacia la derecha.")]
        public float sideBend;
        [Tooltip("Positivo: el pecho gira hacia la derecha.")]
        public float twist;
        [Tooltip("Positivo: la cabeza mira hacia abajo.")]
        public float headPitch;
        public float headYaw;
        [Range(0f, 1f)] public float kneel;

        [Header("Manos al cuello (relativas al hueso del cuello)")]
        public float neckWristForward = 0.13f;
        public float neckWristDown = 0.08f;
        public float neckWristSpread = 0.05f;
        public float neckFingersSpread = 0.06f;
        public float neckFingersUp = 0.03f;

        [Header("Respiracion y reflejos")]
        [Tooltip("Cuanto se expande el pecho al inspirar (grados).")]
        public float breathChestDegrees = 1.2f;
        [Tooltip("Cuanto suben los hombros al inspirar (grados).")]
        public float breathShoulderDegrees = 1.8f;

        // Los anima otro componente (p.ej. BurnPatientReactions):
        // breath va de 0 (exhalado) a 1 (inhalado, puede pasar de 1 si agita);
        // *HandShake es un giro de la muneca en grados (sacudir la mano).
        [HideInInspector] public float breath;
        [HideInInspector] public float rightHandShake;
        [HideInInspector] public float leftHandShake;

        [Header("Reacciones (resorte)")]
        public float springStiffness = 160f;
        public float springDamping = 15f;

        // --- huesos ---
        private Transform hips, spine, spine1, spine2, neck, head;
        private Arm right, left;
        private Leg rightLeg, leftLeg;

        // Pose de bind (T-pose) por hueso.
        private Transform[] resetBones;
        private Quaternion[] bindLocalRotations;
        private Vector3 hipsBindLocalPosition;
        private float kneelDrop;

        // Resortes de reaccion.
        private float reactPitch, reactPitchVelocity;
        private float reactLift, reactLiftVelocity;

        private bool ready;

        private class Arm
        {
            public float side;              // +1 derecha, -1 izquierda
            public Transform shoulder, upper, fore, hand, middle1, thumb1;
            public float upperLength, foreLength;
            public Vector3 upperAxisLocal, upperHingeLocal;
            public Vector3 foreAxisLocal, foreHingeLocal;

            public ArmMode currentMode;
            public ArmMode previousMode;
            public float blendStartTime = float.NegativeInfinity;
            public Vector3 blendFromLocal;  // muneca al empezar la transicion (espacio del paciente)
            public Vector3 lastWrist;
            public bool hasLastWrist;
        }

        private class Leg
        {
            public Transform upLeg, leg, foot, toe;
        }

        // ---------------- API para otros scripts ----------------

        public bool IsReady
        {
            get { return ready; }
        }

        public Transform Spine
        {
            get { return spine; }
        }

        public Transform Chest
        {
            get { return spine2; }
        }

        public Transform Neck
        {
            get { return neck; }
        }

        public Transform Head
        {
            get { return head; }
        }

        public Transform Hips
        {
            get { return hips; }
        }

        public Transform UpperArm(bool rightSide)
        {
            return rightSide ? right.upper : left.upper;
        }

        public Transform ForeArm(bool rightSide)
        {
            return rightSide ? right.fore : left.fore;
        }

        public Transform HandBone(bool rightSide)
        {
            return rightSide ? right.hand : left.hand;
        }

        public Vector3 ShoulderPosition(bool rightSide)
        {
            return (rightSide ? right : left).upper.position;
        }

        public float ArmLength(bool rightSide)
        {
            Arm arm = rightSide ? right : left;
            return arm.upperLength + arm.foreLength;
        }

        // Ultima posicion resuelta de la muneca (sirve para que un "asa" de
        // agarre quede donde esta la mano).
        public Vector3 WristPosition(bool rightSide)
        {
            Arm arm = rightSide ? right : left;
            return arm.hasLastWrist ? arm.lastWrist : arm.hand.position;
        }

        // Frente del torso (ya inclinado), proyectado para ser perpendicular
        // a la columna.
        public Vector3 TorsoForward
        {
            get
            {
                Vector3 up = TorsoUp;
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up);
                return fwd.sqrMagnitude > 1e-6f ? fwd.normalized : transform.forward;
            }
        }

        public Vector3 TorsoUp
        {
            get
            {
                Vector3 up = neck.position - spine.position;
                return up.sqrMagnitude > 1e-6f ? up.normalized : transform.up;
            }
        }

        // Empujon al cuerpo: velocidad angular de inclinacion (grados/s,
        // positivo hacia adelante) y de levantamiento (m/s).
        public void Impulse(float pitchDegreesPerSecond, float liftMetersPerSecond)
        {
            reactPitchVelocity += pitchDegreesPerSecond;
            reactLiftVelocity += liftMetersPerSecond;
        }

        // Lleva un punto al espacio que la mano puede alcanzar de verdad:
        // dentro del largo del brazo, no detras de la espalda y no dentro del
        // torso. Lo usan los scripts que dejan al jugador arrastrar la mano.
        public Vector3 ConstrainReach(bool rightSide, Vector3 point, float reachFraction, float behindLimit,
            float torsoClearance)
        {
            if (!ready)
            {
                return point;
            }

            Vector3 shoulderPos = ShoulderPosition(rightSide);
            float reach = ArmLength(rightSide) * reachFraction;
            Vector3 flatForward = Flat(transform.forward);

            for (int i = 0; i < 2; i++)
            {
                Vector3 d = point - shoulderPos;
                if (d.magnitude > reach)
                {
                    point = shoulderPos + d.normalized * reach;
                }

                // No mas atras que behindLimit respecto del plano de los hombros.
                float behind = Vector3.Dot(point - shoulderPos, flatForward);
                if (behind < -behindLimit)
                {
                    point += flatForward * (-behindLimit - behind);
                }

                // Fuera del torso (cilindro alrededor de la columna).
                Vector3 a = hips.position;
                Vector3 b = neck.position;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                if (t > 0f && t < 1f)
                {
                    Vector3 closest = a + ab * t;
                    Vector3 radial = Flat(point - closest);
                    float distance = radial.magnitude;
                    if (distance < torsoClearance)
                    {
                        Vector3 dir = distance > 1e-4f ? radial / distance : flatForward;
                        point += dir * (torsoClearance - distance);
                    }
                }
            }

            return point;
        }

        // ---------------- Unity ----------------

        private void Awake()
        {
            ready = FindBones() && CaptureBindPose();
            if (!ready)
            {
                Debug.LogWarning("[PatientBody] No se encontraron los huesos con prefijo " + bonePrefix, this);
                enabled = false;
                return;
            }

            DisableLegacyPosers();
            ResetToBind();
            SetupArm(right);
            SetupArm(left);
            ComputeKneelDrop();

            right.currentMode = right.previousMode = rightMode;
            left.currentMode = left.previousMode = leftMode;
        }

        private void LateUpdate()
        {
            if (!ready)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateSprings(dt);

            ResetToBind();
            ApplyLegs();
            ApplySpine();
            ApplyHead();
            ApplyArm(right, rightMode, rightTarget);
            ApplyArm(left, leftMode, leftTarget);
        }

        // ---------------- preparacion ----------------

        private bool FindBones()
        {
            Dictionary<string, Transform> map = new Dictionary<string, Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(bonePrefix) && !map.ContainsKey(t.name))
                {
                    map[t.name] = t;
                }
            }

            Transform Get(string n)
            {
                map.TryGetValue(bonePrefix + n, out Transform t);
                return t;
            }

            hips = Get("Hips");
            spine = Get("Spine");
            spine1 = Get("Spine1");
            spine2 = Get("Spine2");
            neck = Get("Neck");
            head = Get("Head");

            right = new Arm
            {
                side = 1f,
                shoulder = Get("RightShoulder"),
                upper = Get("RightArm"),
                fore = Get("RightForeArm"),
                hand = Get("RightHand"),
                middle1 = Get("RightHandMiddle1"),
                thumb1 = Get("RightHandThumb1")
            };
            left = new Arm
            {
                side = -1f,
                shoulder = Get("LeftShoulder"),
                upper = Get("LeftArm"),
                fore = Get("LeftForeArm"),
                hand = Get("LeftHand"),
                middle1 = Get("LeftHandMiddle1"),
                thumb1 = Get("LeftHandThumb1")
            };
            rightLeg = new Leg
            {
                upLeg = Get("RightUpLeg"), leg = Get("RightLeg"), foot = Get("RightFoot"), toe = Get("RightToeBase")
            };
            leftLeg = new Leg
            {
                upLeg = Get("LeftUpLeg"), leg = Get("LeftLeg"), foot = Get("LeftFoot"), toe = Get("LeftToeBase")
            };

            return hips != null && spine != null && spine1 != null && spine2 != null && neck != null && head != null
                && right.upper != null && right.fore != null && right.hand != null
                && left.upper != null && left.fore != null && left.hand != null
                && rightLeg.upLeg != null && rightLeg.leg != null && rightLeg.foot != null
                && leftLeg.upLeg != null && leftLeg.leg != null && leftLeg.foot != null;
        }

        // Lee la T-pose de las matrices de bind de la malla. No depende de la
        // pose en que quedo guardada la escena (p.ej. la del atragantamiento).
        private bool CaptureBindPose()
        {
            Dictionary<Transform, Matrix4x4> bindWorld = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null)
                {
                    continue;
                }

                Matrix4x4[] bindposes = smr.sharedMesh.bindposes;
                Transform[] bones = smr.bones;
                Matrix4x4 meshToWorld = smr.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length && i < bindposes.Length; i++)
                {
                    if (bones[i] != null && !bindWorld.ContainsKey(bones[i]))
                    {
                        bindWorld[bones[i]] = meshToWorld * bindposes[i].inverse;
                    }
                }
            }

            if (!bindWorld.ContainsKey(hips))
            {
                return false;
            }

            List<Transform> bonesList = new List<Transform>();
            List<Quaternion> rotations = new List<Quaternion>();
            foreach (KeyValuePair<Transform, Matrix4x4> pair in bindWorld)
            {
                Transform bone = pair.Key;
                if (bone == transform || !bone.IsChildOf(transform))
                {
                    continue;
                }

                Quaternion parentRotation = bindWorld.TryGetValue(bone.parent, out Matrix4x4 parentMatrix)
                    ? parentMatrix.rotation
                    : bone.parent.rotation;
                bonesList.Add(bone);
                rotations.Add(Quaternion.Inverse(parentRotation) * pair.Value.rotation);

                if (bone == hips)
                {
                    Matrix4x4 parentWorld = bindWorld.TryGetValue(bone.parent, out Matrix4x4 pm)
                        ? pm
                        : bone.parent.localToWorldMatrix;
                    hipsBindLocalPosition = parentWorld.inverse.MultiplyPoint3x4(pair.Value.GetColumn(3));
                }
            }

            resetBones = bonesList.ToArray();
            bindLocalRotations = rotations.ToArray();
            return true;
        }

        // El IK de Animation Rigging y BodyReachFollow pelearian con este
        // script por los mismos huesos.
        private void DisableLegacyPosers()
        {
            foreach (Behaviour b in GetComponents<Behaviour>())
            {
                string n = b.GetType().Name;
                if (n == "RigBuilder" || n == "BodyReachFollow")
                {
                    b.enabled = false;
                }
            }
        }

        private void ResetToBind()
        {
            for (int i = 0; i < resetBones.Length; i++)
            {
                resetBones[i].localRotation = bindLocalRotations[i];
            }

            hips.localPosition = hipsBindLocalPosition;
        }

        // Ejes locales del brazo, medidos en la T-pose: hacia donde apunta
        // cada hueso y el eje de bisagra del codo (en T-pose con las palmas
        // hacia abajo, el codo dobla llevando el antebrazo hacia adelante).
        private void SetupArm(Arm arm)
        {
            Vector3 upperDir = arm.fore.position - arm.upper.position;
            Vector3 foreDir = arm.hand.position - arm.fore.position;
            arm.upperLength = upperDir.magnitude;
            arm.foreLength = foreDir.magnitude;
            upperDir.Normalize();
            foreDir.Normalize();

            Vector3 hinge = Vector3.Cross(upperDir, transform.forward).normalized;

            arm.upperAxisLocal = Quaternion.Inverse(arm.upper.rotation) * upperDir;
            arm.upperHingeLocal = Orthogonal(Quaternion.Inverse(arm.upper.rotation) * hinge, arm.upperAxisLocal);
            arm.foreAxisLocal = Quaternion.Inverse(arm.fore.rotation) * foreDir;
            arm.foreHingeLocal = Orthogonal(Quaternion.Inverse(arm.fore.rotation) * hinge, arm.foreAxisLocal);
        }

        private void ComputeKneelDrop()
        {
            float floorY = transform.position.y;
            float hipHeight = hips.position.y - floorY;
            float thigh = Vector3.Distance(rightLeg.upLeg.position, rightLeg.leg.position);
            float kneeling = thigh * Mathf.Cos(15f * Mathf.Deg2Rad) + 0.06f;
            kneelDrop = Mathf.Max(0f, hipHeight - kneeling);
        }

        // ---------------- pose por frame ----------------

        private void UpdateSprings(float dt)
        {
            float accel = -springStiffness * reactPitch - springDamping * reactPitchVelocity;
            reactPitchVelocity += accel * dt;
            reactPitch += reactPitchVelocity * dt;
            reactPitch = Mathf.Clamp(reactPitch, -25f, 35f);

            float liftAccel = -springStiffness * reactLift - springDamping * reactLiftVelocity;
            reactLiftVelocity += liftAccel * dt;
            reactLift += reactLiftVelocity * dt;
            reactLift = Mathf.Clamp(reactLift, -0.05f, 0.06f);
        }

        private void ApplyLegs()
        {
            Vector3 up = transform.up;
            Vector3 fwd = transform.forward;
            hips.position += up * (reactLift - kneelDrop * kneel);

            if (kneel <= 0.001f)
            {
                return;
            }

            Vector3 thighDir = (-up + fwd * 0.27f).normalized;
            Vector3 shinDir = (-fwd - up * 0.12f).normalized;
            Vector3 footDir = (-fwd - up * 0.5f).normalized;
            foreach (Leg leg in new[] { rightLeg, leftLeg })
            {
                AimToward(leg.upLeg, leg.leg.position, thighDir, kneel);
                AimToward(leg.leg, leg.foot.position, shinDir, kneel);
                if (leg.toe != null)
                {
                    AimToward(leg.foot, leg.toe.position, footDir, kneel);
                }
            }
        }

        private void ApplySpine()
        {
            Vector3 up = transform.up;
            Vector3 fwd = transform.forward;
            Vector3 rightAxis = transform.right;

            float pitch = lean + reactPitch;
            Quaternion q = Quaternion.AngleAxis(twist, up)
                * Quaternion.AngleAxis(-sideBend, fwd)
                * Quaternion.AngleAxis(pitch, rightAxis);

            spine.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, q, 0.3f) * spine.rotation;
            spine1.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, q, 0.35f) * spine1.rotation;
            spine2.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, q, 0.35f) * spine2.rotation;

            // Al inspirar el pecho se abre (va un poco hacia atras).
            spine2.rotation = Quaternion.AngleAxis(-breath * breathChestDegrees, rightAxis) * spine2.rotation;
        }

        private void ApplyHead()
        {
            Vector3 up = TorsoUp;
            Vector3 rightAxis = transform.right;
            Quaternion q = Quaternion.AngleAxis(headYaw, up) * Quaternion.AngleAxis(headPitch, rightAxis);
            neck.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, q, 0.4f) * neck.rotation;
            head.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, q, 0.6f) * head.rotation;
        }

        private void ApplyArm(Arm arm, ArmMode requestedMode, Transform target)
        {
            if (requestedMode == ArmMode.Reach && target == null)
            {
                requestedMode = ArmMode.Rest;
            }

            if (requestedMode != arm.currentMode)
            {
                arm.previousMode = arm.currentMode;
                arm.currentMode = requestedMode;
                arm.blendStartTime = Time.time;
                arm.blendFromLocal = transform.InverseTransformPoint(arm.hasLastWrist ? arm.lastWrist : arm.hand.position);
            }

            Vector3 torsoUp = TorsoUp;
            Vector3 torsoFwd = TorsoForward;
            Vector3 torsoRight = Vector3.Cross(torsoUp, torsoFwd).normalized;
            Vector3 outward = torsoRight * arm.side;

            float blend = modeBlendSeconds > 0f
                ? Mathf.SmoothStep(0f, 1f, (Time.time - arm.blendStartTime) / modeBlendSeconds)
                : 1f;

            // Hombro: se eleva un poco al subir la mano y se adelanta al estirarse.
            Vector3 desired = DesiredWrist(arm, arm.currentMode, target, torsoUp, torsoFwd, outward);
            if (arm.shoulder != null)
            {
                Vector3 s = arm.upper.position;
                float raise = Mathf.Clamp01((desired.y - s.y) / 0.4f);
                float forward = Mathf.Clamp01((Vector3.Dot(desired - s, torsoFwd) - 0.2f) / 0.3f);
                Quaternion q = Quaternion.AngleAxis(-arm.side * 12f * forward, torsoUp)
                    * Quaternion.AngleAxis(arm.side * (15f * raise + breathShoulderDegrees * breath), torsoFwd);
                arm.shoulder.rotation = q * arm.shoulder.rotation;
                desired = DesiredWrist(arm, arm.currentMode, target, torsoUp, torsoFwd, outward);
            }

            Vector3 pole = Pole(arm, arm.currentMode, desired, torsoUp, torsoFwd, outward);
            if (blend < 1f)
            {
                Vector3 from = transform.TransformPoint(arm.blendFromLocal);
                Vector3 previousPole = Pole(arm, arm.previousMode, from, torsoUp, torsoFwd, outward);
                desired = Vector3.Lerp(from, desired, blend);
                pole = Vector3.Slerp(previousPole, pole, blend);
            }

            Vector3 wrist = SolveArm(arm, desired, pole);
            arm.lastWrist = wrist;
            arm.hasLastWrist = true;

            // Manos al cuello: los dedos apuntan a los costados del cuello.
            float neckWeight = arm.currentMode == ArmMode.Neck ? blend
                : arm.previousMode == ArmMode.Neck ? 1f - blend : 0f;
            if (neckWeight > 0.001f)
            {
                AimHandAtNeck(arm, torsoUp, torsoFwd, outward, neckWeight);
            }

            // Sacudir la mano (reflejo al quemarse): flexion de la muneca.
            float shake = arm.side > 0f ? rightHandShake : leftHandShake;
            if (Mathf.Abs(shake) > 0.01f)
            {
                Vector3 foreDir = arm.hand.position - arm.fore.position;
                Vector3 flexAxis = Vector3.Cross(foreDir, torsoUp);
                if (flexAxis.sqrMagnitude > 1e-8f)
                {
                    arm.hand.rotation = Quaternion.AngleAxis(shake, flexAxis.normalized) * arm.hand.rotation;
                }
            }
        }

        private Vector3 DesiredWrist(Arm arm, ArmMode mode, Transform target, Vector3 torsoUp, Vector3 torsoFwd,
            Vector3 outward)
        {
            switch (mode)
            {
                case ArmMode.Reach:
                    return target != null ? target.position : RestWrist(arm, torsoFwd, outward);
                case ArmMode.Neck:
                    return neck.position + torsoFwd * neckWristForward - torsoUp * neckWristDown
                        + outward * neckWristSpread;
                default:
                    return RestWrist(arm, torsoFwd, outward);
            }
        }

        private Vector3 RestWrist(Arm arm, Vector3 torsoFwd, Vector3 outward)
        {
            float length = arm.upperLength + arm.foreLength;
            return arm.upper.position + Vector3.down * (length * 0.93f) + torsoFwd * 0.07f + outward * 0.05f;
        }

        // Hacia donde apunta el codo.
        private Vector3 Pole(Arm arm, ArmMode mode, Vector3 wrist, Vector3 torsoUp, Vector3 torsoFwd, Vector3 outward)
        {
            if (mode == ArmMode.Neck)
            {
                return -torsoUp + outward * 0.5f + torsoFwd * 0.3f;
            }

            if (mode == ArmMode.Rest)
            {
                return -torsoFwd + outward * 0.3f - torsoUp * 0.2f;
            }

            // Alcanzar: codo abajo y algo afuera; al subir la mano, mas afuera.
            float raise = Mathf.Clamp01((wrist.y - arm.upper.position.y + 0.1f) / 0.35f);
            Vector3 low = -torsoUp + outward * 0.45f - torsoFwd * 0.15f;
            Vector3 high = outward + torsoUp * 0.1f - torsoFwd * 0.1f;
            return Vector3.Lerp(low, high, raise);
        }

        // IK de dos huesos con bisagra: el plano del brazo lo fija el polo y
        // cada hueso se orienta con su eje y su bisagra medidos en la T-pose,
        // asi el codo dobla en su sentido natural y el antebrazo no se retuerce.
        private Vector3 SolveArm(Arm arm, Vector3 desired, Vector3 pole)
        {
            float a = arm.upperLength;
            float b = arm.foreLength;
            Vector3 s = arm.upper.position;
            Vector3 toTarget = desired - s;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 0.01f, (a + b) * 0.999f);
            Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : -transform.up;

            Vector3 perp = Vector3.ProjectOnPlane(pole, dir);
            if (perp.sqrMagnitude < 1e-6f)
            {
                perp = Vector3.ProjectOnPlane(-transform.up + transform.forward * 0.1f, dir);
            }

            perp.Normalize();

            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = s + dir * (a * cosA) + perp * (a * sinA);
            Vector3 wrist = s + dir * d;

            Vector3 hinge = -Vector3.Cross(dir, perp).normalized;
            Vector3 upperDir = (elbow - s).normalized;
            Vector3 foreDir = (wrist - elbow).normalized;

            arm.upper.rotation = Quaternion.LookRotation(upperDir, hinge)
                * Quaternion.Inverse(Quaternion.LookRotation(arm.upperAxisLocal, arm.upperHingeLocal));
            arm.fore.rotation = Quaternion.LookRotation(foreDir, hinge)
                * Quaternion.Inverse(Quaternion.LookRotation(arm.foreAxisLocal, arm.foreHingeLocal));

            return wrist;
        }

        private void AimHandAtNeck(Arm arm, Vector3 torsoUp, Vector3 torsoFwd, Vector3 outward, float weight)
        {
            if (arm.middle1 == null)
            {
                return;
            }

            Quaternion before = arm.hand.rotation;
            Vector3 fingersTarget = neck.position + torsoUp * neckFingersUp + outward * neckFingersSpread;
            Aim(arm.hand, arm.middle1.position, fingersTarget - arm.hand.position);

            if (arm.thumb1 != null)
            {
                Vector3 axis = (arm.middle1.position - arm.hand.position).normalized;
                Vector3 thumbNow = Vector3.ProjectOnPlane(arm.thumb1.position - arm.hand.position, axis);
                Vector3 thumbWanted = Vector3.ProjectOnPlane(outward + torsoFwd * 0.5f, axis);
                if (thumbNow.sqrMagnitude > 1e-8f && thumbWanted.sqrMagnitude > 1e-8f)
                {
                    arm.hand.rotation = Quaternion.FromToRotation(thumbNow, thumbWanted) * arm.hand.rotation;
                }
            }

            if (weight < 1f)
            {
                arm.hand.rotation = Quaternion.Slerp(before, arm.hand.rotation, weight);
            }
        }

        // ---------------- utilidades ----------------

        private static void Aim(Transform bone, Vector3 childPosition, Vector3 wantedDirection)
        {
            Vector3 current = childPosition - bone.position;
            if (current.sqrMagnitude < 1e-8f || wantedDirection.sqrMagnitude < 1e-8f)
            {
                return;
            }

            bone.rotation = Quaternion.FromToRotation(current, wantedDirection) * bone.rotation;
        }

        private static void AimToward(Transform bone, Vector3 childPosition, Vector3 wantedDirection, float weight)
        {
            Vector3 current = childPosition - bone.position;
            if (current.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector3 blended = Vector3.Slerp(current.normalized, wantedDirection.normalized, weight);
            bone.rotation = Quaternion.FromToRotation(current, blended) * bone.rotation;
        }

        private static Vector3 Orthogonal(Vector3 v, Vector3 axis)
        {
            Vector3 o = Vector3.ProjectOnPlane(v, axis);
            return o.sqrMagnitude > 1e-8f ? o.normalized : Vector3.Cross(axis, Vector3.right).normalized;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
