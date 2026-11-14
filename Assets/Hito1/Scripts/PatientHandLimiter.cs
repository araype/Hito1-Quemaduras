using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace SojaExiles
{
    // Las manos que dibuja el SDK de Meta (manos sinteticas, con hand
    // tracking o con controles) no entran en el cuerpo de la paciente: si la
    // mano real la atraviesa, la mano visible se queda en su superficie, como
    // el SDK la frena en los botones (HandPokeLimiterVisual). Antes las manos
    // la atravesaban como a un fantasma.
    //
    // Solo cambia lo que se ve: el agarre (SimpleGrab), el anillo y la
    // vibracion al tocarla siguen usando la mano real. No actua mientras la
    // mano sostiene algo, para que la mano siga al objeto (y al brazo de la
    // paciente cuando se la lleva al agua).
    // Va en la paciente (lo instala Hito1RuntimeBootstrap).
    [RequireComponent(typeof(PatientPhysics))]
    public class PatientHandLimiter : MonoBehaviour
    {
        [Tooltip("Radio de la palma (m).")]
        public float palmRadius = 0.035f;
        [Tooltip("Radio de las puntas del indice y del medio (m).")]
        public float fingertipRadius = 0.01f;

        private class Limited
        {
            public SyntheticHand synthetic;
            public IHand source;
            public bool locked;
        }

        private PatientPhysics physics;
        private readonly List<Limited> hands = new List<Limited>();
        private HandGrabInteractor[] interactors = new HandGrabInteractor[0];
        private float nextLookup;

        private void Awake()
        {
            physics = GetComponent<PatientPhysics>();
        }

        private void OnDisable()
        {
            foreach (Limited hand in hands)
            {
                Unlock(hand);
            }
        }

        private void LateUpdate()
        {
            Lookup();
            foreach (Limited hand in hands)
            {
                if (hand.synthetic == null || hand.source == null)
                {
                    continue;
                }

                Vector3 push = Busy(hand) ? Vector3.zero : Push(hand);
                if (push.sqrMagnitude < 1e-8f || !hand.source.GetRootPose(out Pose wrist))
                {
                    Unlock(hand);
                    continue;
                }

                hand.synthetic.LockWristPose(new Pose(wrist.position + push, wrist.rotation), 1f,
                    SyntheticHand.WristLockMode.Position, true, true);
                hand.synthetic.MarkInputDataRequiresUpdate();
                hand.locked = true;
            }
        }

        // Cuanto hay que correr la mano para que la palma y las puntas de
        // los dedos queden fuera del cuerpo.
        private Vector3 Push(Limited hand)
        {
            IHand source = hand.source;
            if (!source.IsTrackedDataValid || !source.GetRootPose(out Pose wrist))
            {
                return Vector3.zero;
            }

            Vector3 total = Vector3.zero;
            if (source.GetJointPose(HandJointId.HandMiddle1, out Pose knuckle))
            {
                Vector3 palm = Vector3.Lerp(wrist.position, knuckle.position, 0.6f);
                total += physics.PushOut(palm + total, palmRadius);
            }

            foreach (HandJointId tip in new[] { HandJointId.HandIndexTip, HandJointId.HandMiddleTip })
            {
                if (source.GetJointPose(tip, out Pose pose))
                {
                    total += physics.PushOut(pose.position + total, fingertipRadius);
                }
            }

            return total;
        }

        // La mano sostiene algo (con SimpleGrab o con el SDK).
        private bool Busy(Limited hand)
        {
            bool left = hand.source.Handedness == Handedness.Left;
            if (SimpleGrab.TryGetHand(left, out Vector3 _, out bool holding) && holding)
            {
                return true;
            }

            foreach (HandGrabInteractor interactor in interactors)
            {
                if (interactor != null && interactor.HasSelectedInteractable && interactor.Hand != null
                    && interactor.Hand.Handedness == hand.source.Handedness)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Unlock(Limited hand)
        {
            if (!hand.locked)
            {
                return;
            }

            hand.locked = false;
            if (hand.synthetic != null)
            {
                hand.synthetic.FreeWrist(SyntheticHand.WristLockMode.Position);
                hand.synthetic.MarkInputDataRequiresUpdate();
            }
        }

        // El rig de Meta arma sus manos en runtime: se buscan con calma.
        private void Lookup()
        {
            if (Time.unscaledTime < nextLookup)
            {
                return;
            }

            nextLookup = Time.unscaledTime + (hands.Count >= 2 ? 5f : 0.5f);
            interactors = FindObjectsOfType<HandGrabInteractor>();
            foreach (SyntheticHand synthetic in FindObjectsOfType<SyntheticHand>())
            {
                // Las manos fantasma de la reticula de agarre a distancia
                // tienen su propio manejo de la muneca.
                if (synthetic.GetComponentInParent<Oculus.Interaction.DistanceReticles.ReticleGhostDrawer>() != null
                    || hands.Exists(h => h.synthetic == synthetic))
                {
                    continue;
                }

                IHand source = synthetic.ModifyDataFromSource as IHand;
                if (source != null)
                {
                    hands.Add(new Limited { synthetic = synthetic, source = source });
                }
            }
        }
    }
}
