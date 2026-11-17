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
    //
    // Los objetos con Grabbable de Meta (HandGrabPoseSetup: olla, panuelo,
    // gasas...) los agarra el Interaction SDK, que pone la mano en su pose;
    // SimpleGrab los ignora para no pelear por el mismo objeto, salvo que se
    // active grabInteractionSdkObjects (respaldo si el SDK no respondiera).
    public class SimpleGrab : MonoBehaviour
    {
        public OVRCameraRig cameraRig;
        public OVRHand leftTrackedHand;
        public OVRHand rightTrackedHand;

        [Header("Agarre")]
        public float grabRadius = 0.08f;
        // Punto de agarre relativo al anchor del control (en metros).
        public Vector3 controllerGrabOffset = new Vector3(0f, 0f, 0.03f);
        [Tooltip("Punta del indice relativa al anchor del control, si no hay esqueleto de mano (m). La usan los botones de los paneles.")]
        public Vector3 controllerFingertipOffset = new Vector3(0f, -0.015f, 0.09f);
        public float pinchGrabThreshold = 0.8f;
        public float pinchReleaseThreshold = 0.5f;
        // Si el objeto queda trabado (p.ej. contra el meson) y la mano se
        // aleja mas que esto, se suelta solo.
        public float breakDistance = 0.35f;
        public float maxLinearSpeed = 8f;
        public float maxAngularSpeed = 30f;

        [Tooltip("Respaldo: agarrar tambien los objetos que maneja el Interaction SDK de Meta.")]
        public bool grabInteractionSdkObjects;

        [Tooltip("Apaga el agarre a distancia de Meta: tomaba objetos lejos de la mano (la olla desde el otro lado del meson).")]
        public bool disableDistanceGrab = true;

        [Tooltip("Al agarrar, el objeto se acerca hasta tocar la mano en vez de quedar flotando a la distancia a la que estaba.")]
        public bool snapToHand = true;

        [Header("Manos (hand tracking)")]
        [Tooltip("Radio de agarre con las manos seguidas por las camaras (m). Mas grande que con controles: el punto de la pinza tiembla y la mano que se ve no siempre coincide con la real.")]
        public float handGrabRadius = 0.1f;
        [Tooltip("Agarrar cerrando la mano entera (puno), ademas de la pinza. Es lo que hace la gente sin pensarlo con la manta, la bolsa o el celular.")]
        public bool palmGrab = true;
        [Tooltip("Dedos medio y anular a menos de esta parte del largo de la palma, desde su centro: la mano cuenta como cerrada.")]
        public float palmCloseRatio = 0.8f;
        [Tooltip("Por encima de esta parte, la mano cerrada se abre (suelta).")]
        public float palmOpenRatio = 1.0f;
        [Tooltip("Segundos despues de empezar a apretar en los que se sigue intentando agarrar: la gente cierra la mano mientras llega al objeto, no despues.")]
        public float grabWindowSeconds = 0.4f;
        [Tooltip("Segundos que la mano debe estar abierta para soltar (las camaras pierden la pinza un instante y el objeto se caia).")]
        public float releaseGraceSeconds = 0.12f;
        [Tooltip("Si las camaras dejan de ver la mano con algo agarrado (al caminar con la mano al costado), el objeto se sigue llevando con la cabeza hasta este tiempo (s).")]
        public float trackingLossHoldSeconds = 8f;
        [Tooltip("Con la mano cerca del piso, tambien se toma lo que esta hasta esto mas abajo (m): el piso real puede no coincidir con el del juego.")]
        public float floorReachDown = 0.25f;

        [Header("Colision de las manos")]
        public bool handCollisions = true;
        public float handColliderRadius = 0.03f;

        [Tooltip("Los objetos sueltos (taza, celular, pano...) no chocan con el cuerpo ni con las esferas de las manos "
            + "del jugador: las manos los hacian saltar en el meson o salir disparados al ir a agarrarlos, y en el piso "
            + "frenaban al jugador. Se siguen agarrando igual.")]
        public bool loosePropsIgnorePlayer = true;
        private readonly HashSet<Collider> looseProps = new HashSet<Collider>();
        private float nextLooseScan;

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
            public Vector3 fingertip;
            public Quaternion rotation;

            public Rigidbody body;
            public SphereCollider bodyCollider;
            public Held held;

            // Mano cerrada (puno) con hand tracking y el punto delante de la
            // palma donde queda lo que se agarra asi.
            public bool palmValid;
            public bool palmClosed;
            public Vector3 palmPoint;
            public bool pinchPressed;
            public bool heldByPalm;

            public float pressStart = -10f;
            public float openSince = -1f;

            // Mano perdida por las camaras con algo agarrado: pose guardada
            // respecto de la cabeza (solo el giro horizontal).
            public float lostSince = -1f;
            public Vector3 lostOffset;
            public Quaternion lostRotation;

            // Agarres de Meta de esta mano: se apagan mientras SimpleGrab
            // sostiene algo, para no tomar dos objetos con la misma mano.
            public readonly List<MonoBehaviour> metaGrab = new List<MonoBehaviour>();
            public bool metaEnabled = true;
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

        private static readonly List<SimpleGrab> Instances = new List<SimpleGrab>();

        // Aviso al agarrar: collider tocado, objeto tomado y si fue la mano
        // izquierda (la paciente lo usa para saber de donde la tomaron).
        public static event System.Action<Collider, Rigidbody, bool> OnGrabbed;

        // Aviso cuando un objeto se suelta solo porque la mano se alejo
        // demasiado (se tiro muy fuerte o muy rapido).
        public static event System.Action<Rigidbody> OnPulledAway;

        // Suelta el objeto si alguna mano lo tiene (por ejemplo, cuando la
        // paciente recibe la taza).
        public static void ForceRelease(Rigidbody rb)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                if (grab.left.held != null && grab.left.held.rb == rb)
                {
                    grab.Release(grab.left);
                }

                if (grab.right.held != null && grab.right.held.rb == rb)
                {
                    grab.Release(grab.right);
                }
            }
        }

#if UNITY_EDITOR
        // Mano simulada, solo en el editor: para probar sin visor (desde el MCP
        // de Unity) agarrar, tirar de puertas, etc., pasando por el mismo
        // codigo que las manos reales. Indice 0 = izquierda, 1 = derecha.
        public static readonly bool[] SimActive = new bool[2];
        public static readonly Vector3[] SimPosition = new Vector3[2];
        public static readonly Quaternion[] SimRotation = { Quaternion.identity, Quaternion.identity };
        public static readonly bool[] SimPressed = new bool[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSimulatedHands()
        {
            for (int i = 0; i < 2; i++)
            {
                SimActive[i] = false;
                SimPressed[i] = false;
            }
        }

        private bool Simulated(Hand hand)
        {
            return SimActive[hand == left ? 0 : 1];
        }
#endif

        // Pone 'rb' en esa mano del jugador como si la hubiera agarrado (la
        // gasa que sale del sobre queda en la mano que tenia el sobre). Falso
        // si esa mano ya tiene algo o no se esta siguiendo.
        public static bool ForceGrab(Rigidbody rb, bool leftHand)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                Hand hand = leftHand ? grab.left : grab.right;
                if (hand == null || hand.held != null || !hand.poseValid)
                {
                    continue;
                }

                hand.heldByPalm = false;
                grab.Grab(hand, rb, Vector3.zero);
                return true;
            }

            return false;
        }

        // Posicion del punto de agarre de una mano del jugador.
        public static bool TryGetHand(bool leftHand, out Vector3 position, out bool holding)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Hand hand = leftHand ? Instances[i].left : Instances[i].right;
                if (hand.poseValid)
                {
                    position = hand.position;
                    holding = hand.held != null;
                    return true;
                }
            }

            position = Vector3.zero;
            holding = false;
            return false;
        }

        // Punta del indice de la mano visible (con manos o con controles, que
        // tambien dibujan una mano). Con eso se aprietan los botones de los
        // paneles, como se tocaria una pantalla.
        public static bool TryGetFingertip(bool leftHand, out Vector3 position)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Hand hand = leftHand ? Instances[i].left : Instances[i].right;
                if (hand.poseValid)
                {
                    position = hand.fingertip;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }

        // Posicion del punto de agarre, si esa mano aprieta el boton (o hace
        // la pinza) y si sostiene algo. Lo usan las acciones que se hacen
        // apretando sin tomar un objeto (sacar el anillo).
        public static bool TryGetHandInput(bool leftHand, out Vector3 position, out bool pressed, out bool holding)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Hand hand = leftHand ? Instances[i].left : Instances[i].right;
                if (hand.poseValid)
                {
                    position = hand.position;
                    pressed = hand.pressed;
                    holding = hand.held != null;
                    return true;
                }
            }

            position = Vector3.zero;
            pressed = false;
            holding = false;
            return false;
        }

        // Punto de la mano real, sin el freno visual de PatientHandLimiter
        // (la mano que se ve se queda en la superficie de la paciente). Con
        // su mano bajo el chorro, la mano visible chocaba con la de ella
        // antes de llegar a la llave del grifo, que esta a 3 cm.
        public static bool TryGetRawHandPoint(bool leftHand, out Vector3 position)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                Hand hand = leftHand ? grab.left : grab.right;
                if (!hand.poseValid)
                {
                    continue;
                }

                position = hand.position;
#if UNITY_EDITOR
                if (grab.Simulated(hand))
                {
                    return true;
                }
#endif
                if (hand.trackedHand != null && hand.trackedHand.IsTracked && grab.TryGetPinchPoint(hand, out Vector3 pinch))
                {
                    position = pinch;
                }
                else if (hand.anchor != null && OVRInput.IsControllerConnected(hand.controller))
                {
                    position = hand.anchor.TransformPoint(grab.controllerGrabOffset);
                }

                return true;
            }

            position = Vector3.zero;
            return false;
        }

        // Muneca, normal de la palma (hacia afuera de la palma) y direccion de
        // los dedos de una mano seguida con hand tracking. Falso con
        // controles (tienen el boton de menu) o si la mano no se ve. La usa
        // WristPauseButton. En el editor, con la mano simulada: muneca en
        // SimPosition, palma hacia SimRotation * atras y dedos hacia arriba.
        public static bool TryGetPalm(bool leftHand, out Vector3 wrist, out Vector3 palmNormal, out Vector3 toFingers)
        {
            wrist = Vector3.zero;
            palmNormal = Vector3.forward;
            toFingers = Vector3.up;
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                Hand hand = leftHand ? grab.left : grab.right;
#if UNITY_EDITOR
                if (grab.Simulated(hand))
                {
                    int s = leftHand ? 0 : 1;
                    wrist = SimPosition[s];
                    palmNormal = SimRotation[s] * Vector3.back;
                    toFingers = SimRotation[s] * Vector3.up;
                    return true;
                }
#endif
                if (hand.trackedHand == null || !hand.trackedHand.IsTracked)
                {
                    continue;
                }

                if (TryVisibleJoints(leftHand, out Vector3 w, out Vector3 index, out Vector3 pinky))
                {
                    Vector3 a = index - w;
                    Vector3 b = pinky - w;
                    // Mano izquierda con la palma hacia la cara y los dedos
                    // arriba: el indice queda a la derecha y el menique a la
                    // izquierda; Cross(menique, indice) sale de la palma. En
                    // la derecha es al reves.
                    Vector3 n = leftHand ? Vector3.Cross(b, a) : Vector3.Cross(a, b);
                    if (n.sqrMagnitude < 1e-8f)
                    {
                        return false;
                    }

                    wrist = w;
                    palmNormal = n.normalized;
                    toFingers = ((a + b) * 0.5f).normalized;
                    return true;
                }
            }

            return false;
        }

        // Muneca y nudillos del indice y del menique de la mano visible.
        private static bool TryVisibleJoints(bool leftHand, out Vector3 wrist, out Vector3 index, out Vector3 pinky)
        {
            wrist = index = pinky = Vector3.zero;
            if (Time.unscaledTime >= nextVisualLookup)
            {
                nextVisualLookup = Time.unscaledTime + 2f;
                handVisuals = FindObjectsOfType<Oculus.Interaction.HandVisual>();
            }

            Oculus.Interaction.Input.Handedness side = leftHand
                ? Oculus.Interaction.Input.Handedness.Left
                : Oculus.Interaction.Input.Handedness.Right;
            foreach (Oculus.Interaction.HandVisual visual in handVisuals)
            {
                if (visual == null || !visual.isActiveAndEnabled || !visual.IsVisible)
                {
                    continue;
                }

                Oculus.Interaction.Input.IHand h = visual.Hand;
                if (h == null || !h.IsTrackedDataValid || h.Handedness != side)
                {
                    continue;
                }

                if (h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandWristRoot, out Pose pw)
                    && h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandIndex1, out Pose pi)
                    && h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandPinky1, out Pose pp))
                {
                    wrist = pw.position;
                    index = pi.position;
                    pinky = pp.position;
                    return true;
                }
            }

            return false;
        }

        // Rayo para apuntar a los botones de los paneles (ButtonPointer) y si
        // esa mano aprieta (gatillo, boton lateral o pinza). Con manos, el
        // rayo del sistema (OVRHand.PointerPose); con controles, hacia donde
        // apunta el control. Se lee aqui y no del estado de agarre porque en
        // pausa los botones tambien deben responder.
        public static bool TryGetPointer(bool leftHand, out Ray ray, out bool pressed, out bool holding)
        {
            ray = default;
            pressed = false;
            holding = false;
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                Hand hand = leftHand ? grab.left : grab.right;
                holding = hand.held != null;
#if UNITY_EDITOR
                if (grab.Simulated(hand))
                {
                    int s = leftHand ? 0 : 1;
                    ray = new Ray(SimPosition[s], SimRotation[s] * Vector3.forward);
                    pressed = SimPressed[s];
                    return true;
                }
#endif
                if (hand.anchor == null)
                {
                    continue;
                }

                if (hand.trackedHand != null && hand.trackedHand.IsTracked)
                {
                    if (!hand.trackedHand.IsPointerPoseValid || hand.trackedHand.PointerPose == null)
                    {
                        return false;
                    }

                    // PointerPose queda en la raiz de la escena con valores del
                    // espacio de seguimiento: si el rig se movio (inicio,
                    // teletransporte), el rayo salia lejos de la mano. Se pasa
                    // por el TrackingSpace (padre de los anchors).
                    Transform pose = hand.trackedHand.PointerPose;
                    Transform space = pose.parent == null ? hand.anchor.parent : null;
                    ray = space != null
                        ? new Ray(space.TransformPoint(pose.position), space.TransformDirection(pose.forward))
                        : new Ray(pose.position, pose.forward);
                    pressed = hand.trackedHand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= grab.pinchGrabThreshold;
                    return true;
                }

                if (OVRInput.IsControllerConnected(hand.controller))
                {
                    ray = new Ray(hand.anchor.position, hand.anchor.forward);
                    pressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, hand.controller)
                        || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, hand.controller);
                    return true;
                }
            }

            return false;
        }

        // Zonas donde apretar no agarra nada (la accion la maneja otro
        // script). Cada funcion recibe el punto de agarre y devuelve true si
        // lo reclama.
        private static readonly List<System.Func<Vector3, bool>> GrabBlockers = new List<System.Func<Vector3, bool>>();

        public static void AddGrabBlocker(System.Func<Vector3, bool> blocker)
        {
            if (blocker != null && !GrabBlockers.Contains(blocker))
            {
                GrabBlockers.Add(blocker);
            }
        }

        public static void RemoveGrabBlocker(System.Func<Vector3, bool> blocker)
        {
            GrabBlockers.Remove(blocker);
        }

        private static bool Blocked(Vector3 point)
        {
            for (int i = 0; i < GrabBlockers.Count; i++)
            {
                if (GrabBlockers[i](point))
                {
                    return true;
                }
            }

            return false;
        }

        // True si la mano indicada sostiene algo con SimpleGrab.
        public bool IsHolding(bool leftHand)
        {
            return (leftHand ? left : right).held != null;
        }

        // True si alguna mano del jugador sostiene ese Rigidbody con
        // SimpleGrab (lo usa la tela del pano para saber si cuelga de la mano).
        public static bool IsHeldObject(Rigidbody rb)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                if ((grab.left.held != null && grab.left.held.rb == rb)
                    || (grab.right.held != null && grab.right.held.rb == rb))
                {
                    return true;
                }
            }

            return false;
        }

        // True si esa mano del jugador (izquierda o derecha) sostiene ese
        // Rigidbody (la usa la vibracion de los controles).
        public static bool IsHeldBy(Rigidbody rb, bool leftHand)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Hand hand = leftHand ? Instances[i].left : Instances[i].right;
                if (hand.held != null && hand.held.rb == rb)
                {
                    return true;
                }
            }

            return false;
        }

        // Lo que sostiene esa mano del jugador (null si nada). Lo usa
        // HandTeleport: no se salta con esa mano ni llevando a la paciente.
        public static Rigidbody HeldBody(bool leftHand)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Hand hand = leftHand ? Instances[i].left : Instances[i].right;
                if (hand.held != null && hand.held.rb != null)
                {
                    return hand.held.rb;
                }
            }

            return null;
        }

        // True si es la esfera fisica de una mano del jugador.
        public static bool IsHandBody(Rigidbody rb)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                if (Instances[i].left.body == rb || Instances[i].right.body == rb)
                {
                    return true;
                }
            }

            return false;
        }

        // El jugador se teletransporto: lo que sostiene viaja con el. Si no,
        // quedaba lejos de la mano y se soltaba como si se tirara fuerte
        // (breakDistance).
        public static void ShiftHeld(Vector3 offset)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Instances[i].Shift(Instances[i].left, offset);
                Instances[i].Shift(Instances[i].right, offset);
            }

#if UNITY_EDITOR
            // Las manos simuladas estan en el mundo; las reales, en el rig:
            // viajan con el jugador como las reales.
            for (int s = 0; s < 2; s++)
            {
                if (SimActive[s])
                {
                    SimPosition[s] += offset;
                }
            }
#endif
            Physics.SyncTransforms();
        }

        private void Shift(Hand hand, Vector3 offset)
        {
            // La pose de la mano se lee en Update: hasta el proximo, el
            // FixedUpdate usaria la de antes del salto y soltaria el objeto.
            hand.position += offset;
            hand.fingertip += offset;
            hand.palmPoint += offset;
            if (hand.body != null)
            {
                hand.body.position += offset;
                hand.body.transform.position += offset;
            }

            if (hand.held == null || hand.held.rb == null)
            {
                return;
            }

            Rigidbody rb = hand.held.rb;
            rb.position += offset;
            rb.transform.position += offset;
            rb.velocity = Vector3.zero;
        }

        // True si es la esfera de una mano del jugador que no sostiene nada
        // (para sacar el anillo sin confundirse con la mano que sujeta la de
        // la paciente).
        public static bool IsFreeHandCollider(Collider col)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                if (col == grab.left.bodyCollider)
                {
                    return grab.left.held == null;
                }

                if (col == grab.right.bodyCollider)
                {
                    return grab.right.held == null;
                }
            }

            return false;
        }

        // True si el collider es la esfera fisica de la mano izquierda.
        public static bool IsLeftHandCollider(Collider col)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                if (col == Instances[i].left.bodyCollider)
                {
                    return true;
                }
            }

            return false;
        }

        // True si el collider es la esfera fisica de alguna mano del jugador
        // (la usan el grifo y otros objetos que se activan al tocarlos).
        public static bool IsHandCollider(Collider col)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                SimpleGrab grab = Instances[i];
                if (col == grab.left.bodyCollider || col == grab.right.bodyCollider)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEnable()
        {
            Instances.Add(this);
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            if (left.held != null)
            {
                Release(left);
            }

            if (right.held != null)
            {
                Release(right);
            }
        }

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

        private void Start()
        {
            CollectMetaInteractors();

            if (grabInteractionSdkObjects)
            {
                return;
            }

            // Las esferas de las manos no deben empujar los objetos que agarra
            // el Interaction SDK: al acercar la mano para pinzar una gasa, la
            // esfera la alejaria antes de que el SDK la tome.
            foreach (Oculus.Interaction.Grabbable grabbable in FindObjectsOfType<Oculus.Interaction.Grabbable>())
            {
                foreach (Collider col in grabbable.GetComponentsInChildren<Collider>(true))
                {
                    Physics.IgnoreCollision(col, left.bodyCollider, true);
                    Physics.IgnoreCollision(col, right.bodyCollider, true);
                }
            }
        }

        // Busca los agarres del Interaction SDK de cada mano (manos y
        // controles). Los de distancia se apagan.
        private void CollectMetaInteractors()
        {
            foreach (MonoBehaviour mb in FindObjectsOfType<MonoBehaviour>(true))
            {
                if (!(mb is Oculus.Interaction.IInteractorView))
                {
                    continue;
                }

                string type = mb.GetType().Name;
                if (!type.Contains("Grab") || type.Contains("Use"))
                {
                    continue;
                }

                if (type.StartsWith("Distance"))
                {
                    if (disableDistanceGrab)
                    {
                        mb.enabled = false;
                    }

                    continue;
                }

                int side = SideOf(mb.transform);
                if (side == 0 || left.metaGrab.Contains(mb) || right.metaGrab.Contains(mb))
                {
                    continue;
                }

                // Si la mano ya sostiene algo, el agarre nuevo nace apagado.
                Hand owner = side < 0 ? left : right;
                if (mb.enabled)
                {
                    mb.enabled = owner.metaEnabled;
                    owner.metaGrab.Add(mb);
                }
            }
        }

        // -1 izquierda, 1 derecha, 0 si no se sabe (por los nombres del rig).
        private static int SideOf(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.Contains("Left"))
                {
                    return -1;
                }

                if (t.name.Contains("Right"))
                {
                    return 1;
                }
            }

            return 0;
        }

        private static bool MetaHolding(Hand hand)
        {
            foreach (MonoBehaviour mb in hand.metaGrab)
            {
                if (mb != null && mb.enabled && ((Oculus.Interaction.IInteractorView)mb).HasSelectedInteractable)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SyncMetaGrab(Hand hand)
        {
            bool wanted = hand.held == null;
            if (wanted == hand.metaEnabled)
            {
                return;
            }

            hand.metaEnabled = wanted;
            foreach (MonoBehaviour mb in hand.metaGrab)
            {
                if (mb != null)
                {
                    mb.enabled = wanted;
                }
            }
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

        // El rig se arma en runtime: si en Start todavia no estaban los
        // agarres de Meta, se vuelven a buscar cada segundo durante un rato.
        private float nextMetaLookup;

        private void Update()
        {
            if ((left.metaGrab.Count == 0 || right.metaGrab.Count == 0)
                && Time.unscaledTime >= nextMetaLookup && Time.unscaledTime < 30f)
            {
                nextMetaLookup = Time.unscaledTime + 1f;
                CollectMetaInteractors();
            }

            UpdateInput(left, right);
            UpdateInput(right, left);
            SyncMetaGrab(left);
            SyncMetaGrab(right);

            if (loosePropsIgnorePlayer && Time.unscaledTime >= nextLooseScan)
            {
                // Muchos se crean en runtime (BurnSceneProps, la taza, el
                // celular, las tiras de film): se revisa cada segundo.
                nextLooseScan = Time.unscaledTime + 1f;
                IgnoreLooseProps();
            }
        }

        private void IgnoreLooseProps()
        {
            // Tampoco chocan con el cuerpo de la paciente: al voltearse o mover
            // los brazos junto al meson lanzaba lo que habia encima. (Agarrar,
            // cubrir y sacar el anillo no usan esas colisiones.)
            PatientBody patient = FindObjectOfType<PatientBody>();
            List<Collider> patientCols = new List<Collider>();
            if (patient != null)
            {
                foreach (Collider c in patient.GetComponentsInChildren<Collider>())
                {
                    if (!c.isTrigger)
                    {
                        patientCols.Add(c);
                    }
                }
            }

            if (patientCols.Count != patientColliderCount)
            {
                patientColliderCount = patientCols.Count;
                foreach (Collider col in looseProps)
                {
                    IgnoreAll(col, patientCols);
                }
            }

            PatientReach reach = patient != null ? patient.GetComponent<PatientReach>() : null;
            Rigidbody patientHand = reach != null ? reach.target : null;
            foreach (Grabbable g in FindObjectsOfType<Grabbable>())
            {
                Rigidbody rb = g.GetComponent<Rigidbody>();
                // La mano de la paciente (su objetivo, fuera de su jerarquia) y
                // lo que es parte de ella se siguen tocando (sacar el anillo,
                // llevarla del brazo).
                if (rb == null || rb == patientHand || g.GetComponentInParent<PatientBody>() != null)
                {
                    continue;
                }

                foreach (Collider col in CollidersOf(rb))
                {
                    if (col == null || col.isTrigger || !looseProps.Add(col))
                    {
                        continue;
                    }

                    IgnoreAll(col, playerColliders);
                    IgnoreAll(col, patientCols);
                }
            }

            looseProps.RemoveWhere(c => c == null);
        }

        private int patientColliderCount = -1;

        private static void IgnoreAll(Collider col, List<Collider> others)
        {
            if (col == null)
            {
                return;
            }

            foreach (Collider other in others)
            {
                if (other != null)
                {
                    Physics.IgnoreCollision(col, other, true);
                }
            }
        }

        private void FixedUpdate()
        {
            UpdateHandPhysics(left);
            UpdateHandPhysics(right);
            UpdatePendingCollisions();
        }

        // True si esa mano esta cerrada (puno) con hand tracking. Lo usa
        // VRLocomotion: con la mano cerrada los dedos medio y anular tambien
        // "pellizcan" y el jugador caminaba o giraba sin querer al agarrar.
        public static bool IsGripping(bool leftHand)
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                Hand hand = leftHand ? Instances[i].left : Instances[i].right;
                if (hand.palmClosed || hand.heldByPalm)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateInput(Hand hand, Hand otherHand)
        {
            UpdatePose(hand);
            UpdatePalm(hand);
            float now = Time.unscaledTime;

            bool wasPressed = hand.pressed;
            bool raw = hand.poseValid && ReadPressed(hand);

            // Mano perdida con algo agarrado: se sigue llevando un rato.
            if (!hand.poseValid && hand.held != null && CarryWhileLost(hand))
            {
                hand.pressed = true;
                return;
            }

            hand.lostSince = -1f;
            if (hand.held != null && !raw && hand.poseValid && HandTracked(hand))
            {
                // Un instante abierta no suelta: las camaras pierden la pinza
                // o confunden los dedos al mover la mano.
                if (hand.openSince < 0f)
                {
                    hand.openSince = now;
                }

                raw = now - hand.openSince < releaseGraceSeconds;
            }
            else
            {
                hand.openSince = -1f;
            }

            hand.pressed = raw;
            if (hand.pressed && !wasPressed)
            {
                hand.pressStart = now;
            }

            // Se intenta agarrar al apretar y durante un momento despues (la
            // mano suele cerrarse un poco antes de llegar al objeto).
            bool inWindow = hand.pressed && now - hand.pressStart <= grabWindowSeconds;
            if (inWindow && hand.held == null && !Blocked(hand.position) && !MetaHolding(hand))
            {
                TryGrab(hand, otherHand);
            }
            else if (!hand.pressed && hand.held != null)
            {
                Release(hand);
            }
        }

        private bool HandTracked(Hand hand)
        {
            return hand.trackedHand != null && hand.trackedHand.IsTracked
                && !OVRInput.IsControllerConnected(hand.controller);
        }

        // Las camaras dejaron de ver la mano con algo agarrado: el objeto queda
        // donde estaba respecto de la cabeza (al caminar con la manta, la mano
        // cuelga al costado, fuera de la vista). False si ya paso el tiempo o
        // no se usa hand tracking (con controles, perder el control suelta).
        private bool CarryWhileLost(Hand hand)
        {
            Transform head = cameraRig != null ? cameraRig.centerEyeAnchor : null;
            if (head == null || hand.trackedHand == null || trackingLossHoldSeconds <= 0f)
            {
                return false;
            }

            Quaternion yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            float now = Time.unscaledTime;
            if (hand.lostSince < 0f)
            {
                hand.lostSince = now;
                hand.lostOffset = Quaternion.Inverse(yaw) * (GrabPoint(hand) - head.position);
                hand.lostRotation = Quaternion.Inverse(yaw) * hand.rotation;
            }

            if (now - hand.lostSince > trackingLossHoldSeconds)
            {
                return false;
            }

            Vector3 point = head.position + yaw * hand.lostOffset;
            hand.position = point;
            hand.palmPoint = point;
            hand.rotation = yaw * hand.lostRotation;
            return true;
        }

        // Punto donde queda lo agarrado: la pinza o, si se agarro con la mano
        // cerrada, delante de la palma.
        private static Vector3 GrabPoint(Hand hand)
        {
            return hand.heldByPalm ? hand.palmPoint : hand.position;
        }

        private void UpdatePose(Hand hand)
        {
#if UNITY_EDITOR
            if (Simulated(hand))
            {
                int i = hand == left ? 0 : 1;
                hand.position = SimPosition[i];
                hand.fingertip = SimPosition[i];
                hand.rotation = SimRotation[i];
                hand.poseValid = true;
                return;
            }
#endif
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

            // La mano que se ve (HandVisual del SDK de Meta, con manos o con
            // controles): se agarra en la pinza de sus dedos. Antes el punto
            // salia de OVRSkeleton o de un desfase fijo del control y no
            // coincidia con la mano dibujada: el objeto quedaba flotando a
            // varios centimetros de los dedos.
            bool usable = (hand.trackedHand != null && hand.trackedHand.IsTracked)
                || OVRInput.IsControllerConnected(hand.controller);
            if (usable && TryVisiblePinch(hand == left, out Vector3 visiblePinch, out Vector3 visibleTip))
            {
                hand.position = visiblePinch;
                hand.fingertip = visibleTip;
                hand.poseValid = true;
                return;
            }

            if (hand.trackedHand != null && hand.trackedHand.IsTracked)
            {
                // Con hand tracking el agarre ocurre en la pinza: punto medio
                // entre la punta del pulgar y la del indice.
                bool skeletonOk = TryGetPinchPoint(hand, out Vector3 pinch);
                hand.position = skeletonOk ? pinch : hand.anchor.position;
                hand.fingertip = skeletonOk ? hand.indexTip.position
                    : hand.anchor.TransformPoint(controllerFingertipOffset);
                hand.poseValid = true;
                return;
            }

            if (OVRInput.IsControllerConnected(hand.controller))
            {
                hand.position = hand.anchor.TransformPoint(controllerGrabOffset);
                hand.fingertip = hand.anchor.TransformPoint(controllerFingertipOffset);
                hand.poseValid = true;
            }
        }

        // Mano cerrada: los dedos medio y anular se doblan hacia la palma. Se
        // mide con las articulaciones de la mano que se ve (relativo al largo
        // de la palma, asi vale para manos grandes y chicas). Solo con hand
        // tracking: con controles agarra el boton lateral.
        private void UpdatePalm(Hand hand)
        {
            hand.palmValid = false;
#if UNITY_EDITOR
            if (Simulated(hand))
            {
                hand.palmClosed = false;
                hand.palmPoint = hand.position;
                return;
            }
#endif
            if (!palmGrab || !hand.poseValid || !HandTracked(hand) || !TryVisibleHand(hand == left, out Oculus.Interaction.Input.IHand h))
            {
                hand.palmClosed = false;
                return;
            }

            if (!h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandWristRoot, out Pose wrist)
                || !h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandIndex1, out Pose index1)
                || !h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandMiddle1, out Pose middle1)
                || !h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandPinky1, out Pose pinky1)
                || !h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandMiddleTip, out Pose middleTip)
                || !h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandRingTip, out Pose ringTip))
            {
                hand.palmClosed = false;
                return;
            }

            float palmLength = Vector3.Distance(wrist.position, middle1.position);
            if (palmLength < 0.03f)
            {
                hand.palmClosed = false;
                return;
            }

            Vector3 center = Vector3.Lerp(wrist.position, middle1.position, 0.6f);
            Vector3 a = index1.position - wrist.position;
            Vector3 b = pinky1.position - wrist.position;
            // Igual que TryGetPalm: sale del lado de la palma.
            Vector3 normal = hand == left ? Vector3.Cross(b, a) : Vector3.Cross(a, b);
            if (normal.sqrMagnitude < 1e-8f)
            {
                hand.palmClosed = false;
                return;
            }

            normal.Normalize();
            float curl = (Vector3.Distance(middleTip.position, center) + Vector3.Distance(ringTip.position, center))
                * 0.5f / palmLength;
            hand.palmValid = true;
            hand.palmPoint = center + normal * 0.035f;
            hand.palmClosed = hand.palmClosed ? curl < palmOpenRatio : curl < palmCloseRatio;
        }

        private static bool TryVisibleHand(bool leftHand, out Oculus.Interaction.Input.IHand hand)
        {
            hand = null;
            if (Time.unscaledTime >= nextVisualLookup)
            {
                nextVisualLookup = Time.unscaledTime + 2f;
                handVisuals = FindObjectsOfType<Oculus.Interaction.HandVisual>();
            }

            Oculus.Interaction.Input.Handedness side = leftHand
                ? Oculus.Interaction.Input.Handedness.Left
                : Oculus.Interaction.Input.Handedness.Right;
            foreach (Oculus.Interaction.HandVisual visual in handVisuals)
            {
                if (visual == null || !visual.isActiveAndEnabled || !visual.IsVisible)
                {
                    continue;
                }

                Oculus.Interaction.Input.IHand h = visual.Hand;
                if (h != null && h.IsTrackedDataValid && h.Handedness == side)
                {
                    hand = h;
                    return true;
                }
            }

            return false;
        }

        private static Oculus.Interaction.HandVisual[] handVisuals = new Oculus.Interaction.HandVisual[0];
        private static float nextVisualLookup;

        // Pinza (punto medio entre las puntas del pulgar y del indice) y punta
        // del indice de la mano visible de ese lado, si se esta dibujando.
        private static bool TryVisiblePinch(bool leftHand, out Vector3 pinch, out Vector3 indexTip)
        {
            pinch = Vector3.zero;
            indexTip = Vector3.zero;
            if (Time.unscaledTime >= nextVisualLookup)
            {
                // El rig se arma en runtime: se buscan cada 2 s.
                nextVisualLookup = Time.unscaledTime + 2f;
                handVisuals = FindObjectsOfType<Oculus.Interaction.HandVisual>();
            }

            Oculus.Interaction.Input.Handedness side = leftHand
                ? Oculus.Interaction.Input.Handedness.Left
                : Oculus.Interaction.Input.Handedness.Right;
            foreach (Oculus.Interaction.HandVisual visual in handVisuals)
            {
                if (visual == null || !visual.isActiveAndEnabled || !visual.IsVisible)
                {
                    continue;
                }

                Oculus.Interaction.Input.IHand h = visual.Hand;
                if (h == null || !h.IsTrackedDataValid || h.Handedness != side)
                {
                    continue;
                }

                if (h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandThumbTip, out Pose thumb)
                    && h.GetJointPose(Oculus.Interaction.Input.HandJointId.HandIndexTip, out Pose index))
                {
                    pinch = (thumb.position + index.position) * 0.5f;
                    indexTip = index.position;
                    return true;
                }
            }

            return false;
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
#if UNITY_EDITOR
            if (Simulated(hand))
            {
                return SimPressed[hand == left ? 0 : 1];
            }
#endif
            if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, hand.controller)
                || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, hand.controller))
            {
                return true;
            }

            if (hand.trackedHand != null && hand.trackedHand.IsTracked)
            {
                // Mano vacia en la L del teletransporte (HandTeleport): los
                // dedos doblados no son un puno ni cerrar el pulgar una pinza.
                if (hand.held == null && HandTeleport.Aiming(hand == left))
                {
                    hand.pinchPressed = false;
                    return false;
                }

                // Con poca confianza (dedos tapados por la otra mano o por el
                // objeto) se mantiene lo que habia: antes la pinza "se abria"
                // sola y el objeto se caia.
                if (hand.trackedHand.IsDataHighConfidence)
                {
                    float strength = hand.trackedHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
                    hand.pinchPressed = hand.pinchPressed ? strength > pinchReleaseThreshold : strength >= pinchGrabThreshold;
                }

                // Pinza o mano cerrada: suelta cuando no queda ninguna de las
                // dos (al pasar de la pinza a cerrar la mano no se cae).
                return hand.pinchPressed || hand.palmClosed;
            }

            hand.pinchPressed = false;
            return false;
        }

        private void TryGrab(Hand hand, Hand otherHand)
        {
            bool tracked = HandTracked(hand);
            // Con la mano cerrada (sin pinza) se agarra delante de la palma.
            bool byPalm = tracked && hand.palmClosed && !hand.pinchPressed && hand.palmValid;
            Vector3 point = byPalm ? hand.palmPoint : hand.position;
            float radius = tracked ? Mathf.Max(grabRadius, handGrabRadius) : grabRadius;

            Collider[] hits = Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore);
            if (!Closest(hits, point, out Rigidbody closest, out Collider closestCollider) && LowNearFloor(point))
            {
                // Cerca del piso: tambien lo que queda un poco mas abajo (la
                // manta caida). Si el piso real esta mas alto que el del
                // juego, la mano no llegaba a tocarla.
                hits = Physics.OverlapCapsule(point, point + Vector3.down * floorReachDown, radius, ~0,
                    QueryTriggerInteraction.Ignore);
                Closest(hits, point, out closest, out closestCollider);
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

            hand.heldByPalm = byPalm;
            // Lo que falta para que el objeto toque la mano.
            Vector3 snap = snapToHand ? point - ClosestPoint(closestCollider, point) : Vector3.zero;
            Grab(hand, closest, snap);
            OnGrabbed?.Invoke(closestCollider, closest, hand == left);
        }

        private bool Closest(Collider[] hits, Vector3 point, out Rigidbody closest, out Collider closestCollider)
        {
            closest = null;
            closestCollider = null;
            float closestSqrDistance = float.MaxValue;
            foreach (Collider col in hits)
            {
                Rigidbody rb = ResolveGrabTarget(col);
                if (rb == null)
                {
                    continue;
                }

                float sqrDistance = (ClosestPoint(col, point) - point).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = rb;
                    closestCollider = col;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closest != null;
        }

        // La mano esta a menos de 45 cm del piso (de lo que hay debajo sin
        // fisica, a la altura de los pies del jugador).
        private bool LowNearFloor(Vector3 point)
        {
            if (floorReachDown <= 0f)
            {
                return false;
            }

            float feet = transform.position.y;
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.02f, Vector3.down, 0.6f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.rigidbody == null && hit.point.y < feet + 0.15f)
                {
                    return point.y - hit.point.y < 0.45f;
                }
            }

            return false;
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

            if (!grabInteractionSdkObjects && rb.GetComponent<Oculus.Interaction.Grabbable>() != null)
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

        private void Grab(Hand hand, Rigidbody rb, Vector3 snap)
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

            // Solo se acercan a la mano los objetos que giran con ella (la mano
            // de la paciente no: la mueve su brazo).
            if (held.grabbable != null && !held.grabbable.trackRotation)
            {
                snap = Vector3.zero;
            }

            Vector3 grip = GrabPoint(hand);
            Quaternion inverseHand = Quaternion.Inverse(hand.rotation);
            held.localPosition = inverseHand * (rb.position + snap - grip);
            held.localRotation = inverseHand * rb.rotation;
            held.worldOffset = rb.position + snap - grip;

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
            hand.heldByPalm = false;
            hand.lostSince = -1f;
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

            // Sin pose: suelta, salvo que se este llevando con la mano perdida
            // por las camaras (CarryWhileLost).
            if (held.rb == null || (!hand.poseValid && hand.lostSince < 0f))
            {
                Release(hand);
                return;
            }

            Rigidbody rb = held.rb;
            bool trackRotation = held.grabbable == null || held.grabbable.trackRotation;

            Vector3 targetPosition;
            Quaternion targetRotation;
            Vector3 grip = GrabPoint(hand);
            if (trackRotation)
            {
                targetPosition = grip + hand.rotation * held.localPosition;
                targetRotation = hand.rotation * held.localRotation;
            }
            else
            {
                targetPosition = grip + held.worldOffset;
                targetRotation = rb.rotation;
            }

            Vector3 delta = targetPosition - rb.position;
            float limit = held.grabbable != null && held.grabbable.breakDistance > 0f
                ? held.grabbable.breakDistance
                : breakDistance;
            if (delta.magnitude > limit)
            {
                Release(hand);
                OnPulledAway?.Invoke(rb);
                return;
            }

            float dt = Time.fixedDeltaTime;
            // Con followSharpness el objeto sigue a la mano como con un
            // resorte (la mano de la paciente); si no, al instante.
            float sharpness = held.grabbable != null ? held.grabbable.followSharpness : 0f;
            Vector3 wanted = sharpness > 0f ? delta * Mathf.Min(sharpness, 1f / dt) : delta / dt;
            float maxSpeed = held.grabbable != null && held.grabbable.maxFollowSpeed > 0f
                ? Mathf.Min(maxLinearSpeed, held.grabbable.maxFollowSpeed)
                : maxLinearSpeed;
            rb.velocity = Vector3.ClampMagnitude(wanted, maxSpeed);

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

                // Los objetos sueltos siguen sin chocar con el jugador al
                // soltarlos (ver loosePropsIgnorePlayer).
                bool keep = !ignore && looseProps.Contains(col);
                foreach (Collider player in playerColliders)
                {
                    Physics.IgnoreCollision(col, player, ignore || keep);
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
