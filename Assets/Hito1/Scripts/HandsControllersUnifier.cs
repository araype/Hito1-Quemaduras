using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Une manos y controles en un solo esquema y quita lo que estorba:
    //
    //  1. Manos conducidas por el control (OVRManager.controllerDrivenHandPosesType):
    //     el sistema entrega poses de mano mientras se sostienen los
    //     controles. Resultado: se ven manos, los dedos siguen los botones y
    //     los joysticks siguen funcionando. Ya no hay que "elegir" entre
    //     manos o controles.
    //  2. Fuerza que OVRHand se muestre siempre y oculta el modelo del
    //     control cuando hay mano, en vez de alternar uno u otro.
    //  3. Apaga la locomocion propia del rig de Meta (teletransporte con
    //     joystick hacia adelante, paso hacia atras, giro) y su
    //     CharacterController duplicado: eran la causa de que el joystick
    //     solo "hiciera algo" hacia atras y de que la capsula se trabara.
    //     VRLocomotion queda como unico sistema de movimiento. Solo se tocan
    //     componentes dentro del jugador (el objeto con VRLocomotion), nunca
    //     los de otros personajes de la escena.
    //  4. Apaga el agarre a distancia del SDK (objetos flotando lejos de la
    //     mano).
    //
    // Se reaplica unos segundos porque el rig se arma en runtime.
    public class HandsControllersUnifier : MonoBehaviour
    {
        public bool showHandsWhileHoldingControllers = true;
        public bool hideControllerModels = true;
        public bool disableMetaLocomotion = true;
        public bool disableDistanceGrab = true;
        public OVRManager.ControllerDrivenHandPosesType handPosesType = OVRManager.ControllerDrivenHandPosesType.Natural;

        private static readonly float[] ApplyDelays = { 0f, 0.5f, 1.5f, 4f, 10f };
        private const string MetaLocomotionNamespace = "Oculus.Interaction.Locomotion";

        private int nextApplyIndex;
        private float startTime;
        private int disabledLocomotionComponents;
        private int disabledCharacterControllers;
        private int disabledDistanceGrab;

        private void Start()
        {
            startTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (nextApplyIndex >= ApplyDelays.Length)
            {
                return;
            }

            if (Time.unscaledTime - startTime < ApplyDelays[nextApplyIndex])
            {
                return;
            }

            nextApplyIndex++;
            Apply();

            if (nextApplyIndex >= ApplyDelays.Length)
            {
                Debug.Log(string.Format(
                    "[Hito1] Manos+controles unificados. Componentes de locomocion Meta apagados: {0}, CharacterController extra apagados: {1}, agarre a distancia apagado: {2}.",
                    disabledLocomotionComponents,
                    disabledCharacterControllers,
                    disabledDistanceGrab));
            }
        }

        private void Apply()
        {
            ApplyHandVisibility();

            if (disableMetaLocomotion)
            {
                DisableMetaLocomotion();
            }

            if (disableDistanceGrab)
            {
                DisableDistanceGrab();
            }
        }

        // El agarre a distancia del SDK tomaba objetos lejanos y los dejaba
        // flotando lejos de la mano (y sumaba un segundo objeto a la mano).
        // En una cocina todo se alcanza caminando: solo agarre de cerca.
        private void DisableDistanceGrab()
        {
            VRLocomotion player = FindObjectOfType<VRLocomotion>();
            if (player == null)
            {
                return;
            }

            List<Behaviour> interactors = new List<Behaviour>();
            interactors.AddRange(player.GetComponentsInChildren<Oculus.Interaction.HandGrab.DistanceHandGrabInteractor>(true));
            interactors.AddRange(player.GetComponentsInChildren<Oculus.Interaction.DistanceGrabInteractor>(true));
            foreach (Behaviour interactor in interactors)
            {
                if (interactor.enabled)
                {
                    interactor.enabled = false;
                    disabledDistanceGrab++;
                }
            }
        }

        private void ApplyHandVisibility()
        {
            if (showHandsWhileHoldingControllers && OVRManager.instance != null)
            {
                OVRManager.instance.controllerDrivenHandPosesType = handPosesType;
            }

            OVRHand[] hands = FindObjectsOfType<OVRHand>(true);
            for (int i = 0; i < hands.Length; i++)
            {
                if (showHandsWhileHoldingControllers)
                {
                    hands[i].m_showState = OVRInput.InputDeviceShowState.Always;
                }
            }

            OVRControllerHelper[] helpers = FindObjectsOfType<OVRControllerHelper>(true);
            for (int i = 0; i < helpers.Length; i++)
            {
                if (hideControllerModels && showHandsWhileHoldingControllers)
                {
                    helpers[i].m_showState = OVRInput.InputDeviceShowState.NoHand;
                }
            }
        }

        private void DisableMetaLocomotion()
        {
            VRLocomotion player = FindObjectOfType<VRLocomotion>();
            if (player == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = player.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                string ns = behaviour.GetType().Namespace;
                if (ns != null && ns.StartsWith(MetaLocomotionNamespace))
                {
                    behaviour.enabled = false;
                    disabledLocomotionComponents++;
                }
            }

            CharacterController[] controllers = player.GetComponentsInChildren<CharacterController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                CharacterController cc = controllers[i];
                if (cc.enabled && cc.GetComponent<VRLocomotion>() == null)
                {
                    cc.enabled = false;
                    disabledCharacterControllers++;
                }
            }
        }
    }
}
