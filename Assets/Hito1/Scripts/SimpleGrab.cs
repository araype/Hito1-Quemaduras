using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Agarre propio, independiente del Interaction SDK de Meta. Funciona con
    // controles (grip o gatillo) y con hand tracking (pinza pulgar-indice).
    //
    // El objeto sostenido sigue siendo un Rigidbody dinamico: en cada paso de
    // fisica se le asigna la velocidad necesaria para alcanzar la mano, asi
    // choca con el meson, las paredes, etc. en lugar de atravesarlos. Al
    // soltarlo conserva esa velocidad (se puede lanzar o dejar caer).
    //
    // Cada mano tiene ademas una esfera fisica kinematica que empuja los
    // objetos sueltos, para que las manos no los atraviesen como fantasmas.
    //
    // Que se puede agarrar: cualquier Rigidbody dinamico, cualquier Rigidbody
    // con Grabbable (aunque sea kinematico) y los colliders con GrabRedirect.
    public class SimpleGrab : MonoBehaviour
    {
        public OVRCameraRig cameraRig;
        public OVRHand leftTrackedHand;
        public OVRHand rightTrackedHand;

        [Header("Agarre")]
        public float grabRadius = 0.08f;
        // Punto de agarre relativo al anchor del control (en metros).
        public Vector3 controllerGrabOffset = new Vector3(0f, 0f, 0.03f);
        public float pinchGrabThreshold = 0.8f;
        public float pinchReleaseThreshold = 0.5f;
        // Si el objeto queda trabado (p.ej. contra el meson) y la mano se
        // aleja mas que esto, se suelta solo.
        public float breakDistance = 0.35f;
        public float maxLinearSpeed = 8f;
        public float maxAngularSpeed = 30f;

        [Header("Colision de las manos")]
        public bool handCollisions = true;
        public float handColliderRadius = 0.03f;

        private class Hand
        {
            public OVRInput.Controller controller;
            public OVRHand trackedHand;
            public OVRSkeleton skeleton;
            public Transform thumbTip;
            public Transform indexTip;
            public Transform anchor;

            public bool pressed;
            public bool poseValid;
            public Vector3 position;
            public Quaternion rotation;

            public Rigidbody body;
            public SphereCollider bodyCollider;
            public Held held;
        }

        private class Held
        {
            public Rigidbody rb;
            public Grabbable grabbable;
            public Collider[] colliders;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 worldOffset;

            public bool wasKinematic;
            public bool usedGravity;
            public RigidbodyInterpolation interpolation;
            public CollisionDetectionMode collisionMode;
            public float maxAngularVelocity;
        }

        // Objetos recien soltados cuyas colisiones con las manos / el jugador
        // se reactivan recien cuando dejan de superponerse (si no, saldrian
        // disparados al soltarlos).
        private class PendingCollision
        {
            public Rigidbody rb;
            public Collider[] colliders;
        }

        private readonly Hand left = new Hand { controller = OVRInput.Controller.LTouch };
        private readonly Hand right = new Hand { controller = OVRInput.Controller.RTouch };
        private readonly List<PendingCollision> pending = new List<PendingCollision>();
        private readonly List<Collider> playerColliders = new List<Collider>();

        private void Awake()
        {
            if (cameraRig == null)
            {
                cameraRig = GetComponentInChildren<OVRCameraRig>();
            }

            left.trackedHand = leftTrackedHand;
            right.trackedHand = rightTrackedHand;

            CharacterController characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                playerColliders.Add(characterController);
            }

            CreateHandBody(left, "ManoFisica_L");
            CreateHandBody(right, "ManoFisica_R");
        }

        private void OnDestroy()
        {
            DestroyHandBody(left);
            DestroyHandBody(right);
        }

        private void CreateHandBody(Hand hand, string bodyName)
        {
            GameObject go = new GameObject(bodyName);
            hand.body = go.AddComponent<Rigidbody>();
            hand.body.isKinematic = true;
            hand.body.interpolation = RigidbodyInterpolation.Interpolate;
            hand.bodyCollider = go.AddComponent<SphereCollider>();
            hand.bodyCollider.radius = handColliderRadius;
            hand.bodyCollider.enabled = false;

            // La esfera de la mano nunca debe empujar al propio jugador.
            foreach (Collider col in playerColliders)
            {
                Physics.IgnoreCollision(hand.bodyCollider, col, true);
            }

            playerColliders.Add(hand.bodyCollider);
        }

        private static void DestroyHandBody(Hand hand)
        {
            if (hand.body != null)
            {
                Destroy(hand.body.gameObject);
            }
        }

        private void Update()
        {
            UpdateInput(left, right);
            UpdateInput(right, left);
        }

        private void FixedUpdate()
        {
            UpdateHandPhysics(left);
            UpdateHandPhysics(right);
            UpdatePendingCollisions();
        }

        private void UpdateInput(Hand hand, Hand otherHand)
        {
            UpdatePose(hand);

            bool wasPressed = hand.pressed;
            hand.pressed = hand.poseValid && ReadPressed(hand);

            if (hand.pressed && !wasPressed && hand.held == null)
            {
                TryGrab(hand, otherHand);
            }
            else if (!hand.pressed && hand.held != null)
            {
                Release(hand);
            }
        }

        private void UpdatePose(Hand hand)
        {
            if (hand.anchor == null && cameraRig != null)
            {
                hand.anchor = hand == left ? cameraRig.leftHandAnchor : cameraRig.rightHandAnchor;
                if (hand.anchor != null && hand.trackedHand == null)
                {
                    hand.trackedHand = hand.anchor.GetComponentInChildren<OVRHand>(true);
                }
            }

            if (hand.skeleton == null && hand.trackedHand != null)
            {
                hand.skeleton = hand.trackedHand.GetComponent<OVRSkeleton>();
            }

            hand.poseValid = false;
            if (hand.anchor == null)
            {
                return;
            }

            hand.rotation = hand.anchor.rotation;

            if (hand.trackedHand != null && hand.trackedHand.IsTracked)
            {
                // Con hand tracking el agarre ocurre en la pinza: punto medio
                // entre la punta del pulgar y la del indice.
                hand.position = TryGetPinchPoint(hand, out Vector3 pinch)
                    ? pinch
                    : hand.anchor.position;
                hand.poseValid = true;
                return;
            }

            if (OVRInput.IsControllerConnected(hand.controller))
            {
                hand.position = hand.anchor.TransformPoint(controllerGrabOffset);
                hand.poseValid = true;
            }
        }

        private bool TryGetPinchPoint(Hand hand, out Vector3 point)
        {
            point = Vector3.zero;
            if (hand.skeleton == null || !hand.skeleton.IsInitialized || !hand.skeleton.IsDataValid)
            {
                return false;
            }

            if (hand.thumbTip == null || hand.indexTip == null)
            {
                // Se busca por nombre porque los BoneId cambian segun el tipo
                // de esqueleto (OVR "Hand_IndexTip" vs OpenXR "XRHand_IndexTip").
                foreach (OVRBone bone in hand.skeleton.Bones)
                {
                    if (bone.Transform == null)
                    {
                        continue;
                    }

                    if (bone.Transform.name.EndsWith("ThumbTip"))
                    {
                        hand.thumbTip = bone.Transform;
                    }
                    else if (bone.Transform.name.EndsWith("IndexTip"))
                    {
                        hand.indexTip = bone.Transform;
                    }
                }

                if (hand.thumbTip == null || hand.indexTip == null)
                {
                    return false;
                }
            }

            point = (hand.thumbTip.position + hand.indexTip.position) * 0.5f;
            return true;
        }

        private bool ReadPressed(Hand hand)
        {
            if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, hand.controller)
                || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, hand.controller))
            {
                return true;
            }

            if (hand.trackedHand != null && hand.trackedHand.IsTracked)
            {
                float strength = hand.trackedHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
                return hand.pressed ? strength > pinchReleaseThreshold : strength >= pinchGrabThreshold;
            }

            return false;
        }

        private void TryGrab(Hand hand, Hand otherHand)
        {
            Collider[] hits = Physics.OverlapSphere(hand.position, grabRadius, ~0, QueryTriggerInteraction.Ignore);
            Rigidbody closest = null;
            float closestSqrDistance = float.MaxValue;
            foreach (Collider col in hits)
            {
                Rigidbody rb = ResolveGrabTarget(col);
                if (rb == null)
                {
                    continue;
                }

                float sqrDistance = (ClosestPoint(col, hand.position) - hand.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = rb;
                    closestSqrDistance = sqrDistance;
                }
            }

            if (closest == null)
            {
                return;
            }

            // Pasar un objeto de una mano a la otra.
            if (otherHand.held != null && otherHand.held.rb == closest)
            {
                Release(otherHand);
            }

            Grab(hand, closest);
        }

        private Rigidbody ResolveGrabTarget(Collider col)
        {
            GrabRedirect redirect = col.GetComponent<GrabRedirect>();
            if (redirect != null)
            {
                return redirect.CanGrab ? redirect.target : null;
            }

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb == left.body || rb == right.body)
            {
                return null;
            }

            Grabbable grabbable = rb.GetComponent<Grabbable>();
            if (grabbable != null)
            {
                return grabbable.enabled ? rb : null;
            }

            return rb.isKinematic ? null : rb;
        }

        private static Vector3 ClosestPoint(Collider col, Vector3 point)
        {
            MeshCollider mesh = col as MeshCollider;
            if (mesh != null && !mesh.convex)
            {
                return col.bounds.ClosestPoint(point);
            }

            return col.ClosestPoint(point);
        }

        private void Grab(Hand hand, Rigidbody rb)
        {
            pending.RemoveAll(p => p.rb == rb);

            Held held = new Held
            {
                rb = rb,
                grabbable = rb.GetComponent<Grabbable>(),
                colliders = CollidersOf(rb),
                wasKinematic = rb.isKinematic,
                usedGravity = rb.useGravity,
                interpolation = rb.interpolation,
                collisionMode = rb.collisionDetectionMode,
                maxAngularVelocity = rb.maxAngularVelocity
            };

            Quaternion inverseHand = Quaternion.Inverse(hand.rotation);
            held.localPosition = inverseHand * (rb.position - hand.position);
            held.localRotation = inverseHand * rb.rotation;
            held.worldOffset = rb.position - hand.position;

            rb.isKinematic = false;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxAngularVelocity = maxAngularSpeed;

            SetIgnoreWithPlayer(held.colliders, true);

            if (held.grabbable != null)
            {
                held.grabbable.IsHeld = true;
            }

            hand.held = held;
        }

        private void Release(Hand hand)
        {
            Held held = hand.held;
            hand.held = null;
            if (held.rb == null)
            {
                return;
            }

            Rigidbody rb = held.rb;
            rb.useGravity = held.usedGravity;
            rb.interpolation = held.interpolation;
            rb.maxAngularVelocity = held.maxAngularVelocity;

            bool freeze = held.grabbable != null && held.grabbable.freezeOnRelease;
            if (freeze || held.wasKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.collisionDetectionMode = held.collisionMode;
                rb.isKinematic = true;
            }
            else
            {
                rb.collisionDetectionMode = held.collisionMode;
            }

            if (held.grabbable != null)
            {
                held.grabbable.IsHeld = false;
            }

            pending.Add(new PendingCollision { rb = rb, colliders = held.colliders });
        }

        private void UpdateHandPhysics(Hand hand)
        {
            if (hand.body != null)
            {
                bool active = handCollisions && hand.poseValid;
                hand.bodyCollider.enabled = active;
                if (active)
                {
                    hand.body.MovePosition(hand.position);
                }
            }

            Held held = hand.held;
            if (held == null)
            {
                return;
            }

            if (held.rb == null || !hand.poseValid)
            {
                Release(hand);
                return;
            }

            Rigidbody rb = held.rb;
            bool trackRotation = held.grabbable == null || held.grabbable.trackRotation;

            Vector3 targetPosition;
            Quaternion targetRotation;
            if (trackRotation)
            {
                targetPosition = hand.position + hand.rotation * held.localPosition;
                targetRotation = hand.rotation * held.localRotation;
            }
            else
            {
                targetPosition = hand.position + held.worldOffset;
                targetRotation = rb.rotation;
            }

            Vector3 delta = targetPosition - rb.position;
            if (delta.magnitude > breakDistance)
            {
                Release(hand);
                return;
            }

            float dt = Time.fixedDeltaTime;
            rb.velocity = Vector3.ClampMagnitude(delta / dt, maxLinearSpeed);

            Quaternion rotationDelta = targetRotation * Quaternion.Inverse(rb.rotation);
            rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (trackRotation && Mathf.Abs(angle) > 0.01f && !float.IsInfinity(axis.x) && !float.IsNaN(axis.x))
            {
                rb.angularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / dt), maxAngularSpeed);
            }
            else
            {
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void UpdatePendingCollisions()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCollision p = pending[i];
                if (p.rb == null)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (OverlapsPlayer(p.colliders))
                {
                    continue;
                }

                SetIgnoreWithPlayer(p.colliders, false);
                pending.RemoveAt(i);
            }
        }

        private bool OverlapsPlayer(Collider[] colliders)
        {
            foreach (Collider col in colliders)
            {
                if (col == null || !col.enabled || col.isTrigger)
                {
                    continue;
                }

                foreach (Collider player in playerColliders)
                {
                    if (!player.enabled)
                    {
                        continue;
                    }

                    if (Physics.ComputePenetration(
                            col, col.transform.position, col.transform.rotation,
                            player, player.transform.position, player.transform.rotation,
                            out _, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Mientras se sostiene un objeto, no debe chocar con las esferas de
        // las manos ni con la capsula del jugador (si no, no se podria
        // acercar al cuerpo).
        private void SetIgnoreWithPlayer(Collider[] colliders, bool ignore)
        {
            foreach (Collider col in colliders)
            {
                if (col == null)
                {
                    continue;
                }

                foreach (Collider player in playerColliders)
                {
                    Physics.IgnoreCollision(col, player, ignore);
                }
            }
        }

        private static Collider[] CollidersOf(Rigidbody rb)
        {
            List<Collider> result = new List<Collider>();
            foreach (Collider col in rb.GetComponentsInChildren<Collider>(true))
            {
                if (col.attachedRigidbody == rb || col.transform == rb.transform)
                {
                    result.Add(col);
                }
            }

            return result.ToArray();
        }
    }
}
