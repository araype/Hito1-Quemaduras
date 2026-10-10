using UnityEngine;

namespace SojaExiles
{
    // Agarre simple e independiente del Interaction SDK de Meta: usa la pose
    // de la mano (ancla del OVRCameraRig, que sigue al control o a la mano
    // trackeada) y el gatillo / agarre para detectar y sostener cualquier
    // Rigidbody cercano (la olla, el panuelo, la mano quemada).
    //
    //  - Con controles (o manos conducidas por control): gatillo o agarre.
    //  - Solo manos: pellizco del indice.
    //
    // Al soltar devuelve el Rigidbody a su estado original (si era cinematico,
    // como el objetivo del IK, sigue siendolo) y, si era dinamico, le da la
    // velocidad de la mano para que se pueda lanzar.
    public class SimpleGrab : MonoBehaviour
    {
        // Se mantiene por compatibilidad con la escena. Solo se usa como
        // respaldo si no se encuentra un OVRCameraRig.
        public Transform rigRoot;
        public float grabRadius = 0.15f;
        public float holdDistance = 0.25f;
        public float pinchStrengthThreshold = 0.8f;
        public bool allowHandPinch = true;

        private class Hand
        {
            public OVRInput.Controller controller;
            public OVRPlugin.Hand handType;
            public OVRHand ovrHand;
            public Rigidbody held;
            public bool heldWasKinematic;
            public Vector3 previousPosition;
            public Vector3 velocity;
            public bool hasPrevious;
        }

        private readonly Hand left = new Hand
        {
            controller = OVRInput.Controller.LTouch,
            handType = OVRPlugin.Hand.HandLeft
        };

        private readonly Hand right = new Hand
        {
            controller = OVRInput.Controller.RTouch,
            handType = OVRPlugin.Hand.HandRight
        };

        private OVRCameraRig rig;
        private float nextLookup;

        private void Update()
        {
            ResolveReferences();
            UpdateHand(left);
            UpdateHand(right);
        }

        private void OnDisable()
        {
            Release(left);
            Release(right);
        }

        private void ResolveReferences()
        {
            if (Time.unscaledTime < nextLookup)
            {
                return;
            }

            if (rig != null && left.ovrHand != null && right.ovrHand != null)
            {
                return;
            }

            nextLookup = Time.unscaledTime + 0.5f;

            if (rig == null)
            {
                rig = GetComponentInChildren<OVRCameraRig>(true);
                if (rig == null && rigRoot != null)
                {
                    rig = rigRoot.GetComponentInChildren<OVRCameraRig>(true);
                }
            }

            Transform searchRoot = rig != null ? rig.transform.root : transform.root;
            OVRHand[] hands = searchRoot.GetComponentsInChildren<OVRHand>(true);
            for (int i = 0; i < hands.Length; i++)
            {
                OVRPlugin.Hand type = hands[i].GetHand();
                if (type == left.handType)
                {
                    left.ovrHand = hands[i];
                }
                else if (type == right.handType)
                {
                    right.ovrHand = hands[i];
                }
            }
        }

        private bool TryGetPose(Hand hand, out Vector3 position, out Quaternion rotation)
        {
            if (rig != null)
            {
                Transform anchor = hand == left ? rig.leftHandAnchor : rig.rightHandAnchor;
                if (anchor != null)
                {
                    position = anchor.position;
                    rotation = anchor.rotation;
                    return true;
                }
            }

            if (rigRoot != null)
            {
                position = rigRoot.TransformPoint(OVRInput.GetLocalControllerPosition(hand.controller));
                rotation = rigRoot.rotation * OVRInput.GetLocalControllerRotation(hand.controller);
                return true;
            }

            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        private bool IsPressed(Hand hand)
        {
            bool controllerPressed =
                OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, hand.controller) ||
                OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, hand.controller);

            if (controllerPressed)
            {
                return true;
            }

            if (!allowHandPinch || hand.ovrHand == null || !hand.ovrHand.IsTracked)
            {
                return false;
            }

            // El pellizco solo cuenta cuando no hay controles en las manos;
            // si no, sostener un control se confundiria con un agarre.
            bool handsActive = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
            if (!handsActive)
            {
                return false;
            }

            return hand.ovrHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= pinchStrengthThreshold;
        }

        private void UpdateHand(Hand hand)
        {
            Vector3 worldPos;
            Quaternion worldRot;
            if (!TryGetPose(hand, out worldPos, out worldRot))
            {
                return;
            }

            if (hand.hasPrevious && Time.deltaTime > 0f)
            {
                Vector3 instant = (worldPos - hand.previousPosition) / Time.deltaTime;
                hand.velocity = Vector3.Lerp(hand.velocity, instant, 0.35f);
            }
            hand.previousPosition = worldPos;
            hand.hasPrevious = true;

            bool pressed = IsPressed(hand);

            if (hand.held != null)
            {
                if (!pressed)
                {
                    Release(hand);
                }
                else
                {
                    hand.held.MovePosition(worldPos + worldRot * Vector3.forward * holdDistance);
                    hand.held.MoveRotation(worldRot);
                }
                return;
            }

            if (!pressed)
            {
                return;
            }

            Collider[] hits = Physics.OverlapSphere(worldPos, grabRadius, ~0, QueryTriggerInteraction.Ignore);
            Rigidbody closest = null;
            float closestSqrDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb == null || IsHeldByOtherHand(hand, rb))
                {
                    continue;
                }

                float sqrDistance = (rb.worldCenterOfMass - worldPos).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = rb;
                    closestSqrDistance = sqrDistance;
                }
            }

            if (closest != null)
            {
                hand.held = closest;
                hand.heldWasKinematic = closest.isKinematic;
                closest.isKinematic = true;
            }
        }

        private bool IsHeldByOtherHand(Hand hand, Rigidbody rb)
        {
            Hand other = hand == left ? right : left;
            return other.held == rb;
        }

        private void Release(Hand hand)
        {
            if (hand.held == null)
            {
                return;
            }

            Rigidbody rb = hand.held;
            hand.held = null;
            rb.isKinematic = hand.heldWasKinematic;

            if (!rb.isKinematic)
            {
                rb.velocity = hand.velocity;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }
        }
    }
}
