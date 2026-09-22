using UnityEngine;

namespace SojaExiles
{
    // Agarre simple e independiente del Interaction SDK de Meta: usa
    // directamente la posicion del control (OVRInput) y el gatillo para
    // detectar y sostener cualquier Rigidbody cercano (la olla, el panuelo).
    // Sirve de alternativa mientras se revisa por que el Grabbable /
    // HandGrabInteractable del Interaction SDK no responde.
    public class SimpleGrab : MonoBehaviour
    {
        public Transform rigRoot;
        public float grabRadius = 0.15f;
        public float holdDistance = 0.25f;

        private class Hand
        {
            public OVRInput.Controller controller;
            public Rigidbody held;
        }

        private readonly Hand left = new Hand { controller = OVRInput.Controller.LTouch };
        private readonly Hand right = new Hand { controller = OVRInput.Controller.RTouch };

        private void Update()
        {
            UpdateHand(left);
            UpdateHand(right);
        }

        private void UpdateHand(Hand hand)
        {
            if (rigRoot == null)
            {
                return;
            }

            Vector3 worldPos = rigRoot.TransformPoint(OVRInput.GetLocalControllerPosition(hand.controller));
            Quaternion worldRot = rigRoot.rotation * OVRInput.GetLocalControllerRotation(hand.controller);

            bool pressed = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, hand.controller)
                        || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, hand.controller);

            if (hand.held != null)
            {
                if (!pressed)
                {
                    hand.held.isKinematic = false;
                    hand.held = null;
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

            Collider[] hits = Physics.OverlapSphere(worldPos, grabRadius);
            foreach (Collider col in hits)
            {
                Rigidbody rb = col.attachedRigidbody;
                if (rb == null)
                {
                    continue;
                }

                hand.held = rb;
                rb.isKinematic = true;
                break;
            }
        }
    }
}
