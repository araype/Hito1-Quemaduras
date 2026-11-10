using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Guia de la practica: arma el arco de la historia y le dice al jugador
    // que hacer en cada momento (antes solo se quejaba la paciente y no se
    // entendia el objetivo).
    //
    //   Planteamiento: panel de inicio (aviso de contenido y eleccion de modo)
    //     y tutorial dentro de la historia: la tia esta cocinando y le pide al
    //     jugador una taza (agarrar y caminar). Al entregarla, la olla se
    //     vuelca (PotAccident.Trigger).
    //   Desarrollo, climax y resolucion (modo guiado): un narrador indica cada
    //     paso del protocolo con voz y una tarjeta que acompana la vista, y un
    //     brillo marca el objeto que hay que usar. Si el jugador se equivoca,
    //     el narrador lo corrige.
    //   Modo evaluacion: mismo tutorial; despues del accidente no hay tarjeta,
    //     ni voz de guia, ni texto en el reloj.
    //
    // Las frases del narrador son los archivos guia_*.wav de
    // Resources/Hito1Audio (voz de sintesis; se reemplazan con el mismo
    // nombre). Si falta un archivo, igual se muestra la tarjeta.
    // Va en el objeto de BurnSequenceManager.
    public class BurnGuide : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;

        [Header("Modo")]
        [Tooltip("Muestra el panel para elegir entre modo guiado y modo evaluacion.")]
        public bool askMode = true;
        [Tooltip("Modo si no se pregunta (o hasta que se elija).")]
        public bool guided = true;
        public bool tutorial = true;

        [Header("Voz")]
        public float narratorVolume = 1f;
        [Tooltip("Segundos sin avanzar hasta repetir la indicacion.")]
        public float reminderSeconds = 30f;

        [Header("Tarjeta")]
        [Tooltip("Mas lejos y a la altura de los ojos: la tarea (meson, lavadero, manos) queda debajo y no se tapa.")]
        public float cardDistance = 0.9f;
        [Tooltip("Cuanto mas abajo de los ojos va la tarjeta (negativo: mas arriba).")]
        public float cardDrop = 0.1f;
        [Tooltip("Escala de la tarjeta (al estar mas lejos se agranda para leerse igual).")]
        public float cardScale = 1.25f;
        [Tooltip("Angulo de la mirada a partir del cual la tarjeta se reacomoda.")]
        public float cardFollowAngle = 28f;
        [Tooltip("Segundos despues de que calla el narrador hasta que la tarjeta se achica a solo el paso.")]
        public float cardCompactDelay = 3f;
        [Tooltip("Opacidad del fondo: completa y compacta.")]
        public float cardAlphaFull = 0.92f;
        public float cardAlphaCompact = 0.8f;

        [Header("Tutorial")]
        public float grabTimeout = 45f;
        public float bringTimeout = 60f;
        public float handOverDistance = 0.4f;
        [Tooltip("Distancia del jugador (con la taza en la mano) a la que la tia se voltea para recibirla (m).")]
        public float attendDistance = 2.2f;
        [Tooltip("Segundos entre la entrega de la taza y el accidente.")]
        public float accidentAfterTutorial = 4f;

        public static BurnGuide Instance { get; private set; }

        public bool Guided
        {
            get { return guided; }
        }

        // La guia decide cuando se vuelca la olla (despues del menu y del
        // tutorial).
        public bool ControlsAccident
        {
            get { return enabled && (askMode || tutorial); }
        }

        // El narrador esta hablando (o tiene algo por decir).
        public bool Speaking
        {
            get { return narrator != null && (narrator.isPlaying || queue.Count > 0); }
        }

        private enum StepId
        {
            ApagarFuego,
            AbrirAgua,
            LlevarAlAgua,
            Anillo,
            Abrigar,
            Llamar,
            Esperar,
            Cubrir
        }

        private class StepInfo
        {
            public StepId id;
            public string title;
            public string hint;
            public string clip;
        }

        private static readonly StepInfo[] Steps =
        {
            new StepInfo { id = StepId.ApagarFuego, title = "Apaga la hornilla",
                hint = "Toca la perilla que brilla. Así nadie más se quema.", clip = "guia_apagar" },
            new StepInfo { id = StepId.AbrirAgua, title = "Abre el agua fría",
                hint = "Es la llave del punto azul. Tócala con la mano.", clip = "guia_agua" },
            // El anillo va antes del chorro: ella ofrece la mano a la altura del
            // pecho y se alcanza facil (bajo el agua costaba mucho). Si se lo
            // salta, se puede sacar despues, tambien bajo el chorro.
            new StepInfo { id = StepId.Anillo, title = "Quítale el anillo",
                hint = "Antes de que se hinche. Pellizca su anillo y deslízalo hacia la punta del dedo.",
                clip = "guia_anillo" },
            new StepInfo { id = StepId.LlevarAlAgua, title = "Lleva su mano al chorro",
                hint = "Tómala del antebrazo, no de la mano quemada, y llévala al chorro: ella la pondrá bajo el agua.",
                clip = "guia_llevar" },
            new StepInfo { id = StepId.Abrigar, title = "Abrígala con la chompa",
                hint = "El agua fría también le enfría el cuerpo. Suelta la chompa sobre sus hombros.", clip = "guia_abrigo" },
            // Mientras se enfria (antes ese minuto quedaba sin nada que hacer).
            // Sin celular en la escena se decide al final con el panel
            // (ver ShowStep).
            new StepInfo { id = StepId.Llamar, title = "Llama al 106",
                hint = "Mientras se enfría, toma el celular del mesón y llévalo a tu oreja. Responde lo que te pregunten.",
                clip = "guia_llamar" },
            new StepInfo { id = StepId.Esperar, title = "Enfría 20 minutos",
                hint = "Mantén su mano bajo el agua hasta que el reloj complete las 20 marcas.", clip = "guia_esperar" },
            new StepInfo { id = StepId.Cubrir, title = "Cubre con una gasa limpia",
                hint = "Abre el botiquín, toma un sobre de gasa y rásgalo separando las manos. Toma la gasa de los bordes y apóyala plana y centrada sobre la quemadura 1 segundo, sin apretar. Si quieres, fíjala con la venda dando una vuelta suelta. Si no hay gasa, usa film: sostén el rollo, toma el borde que brilla con la otra mano y pon las tiras a lo largo de su mano.",
                clip = "guia_cubrir" }
        };

        // Paso de decision sin celular (panel de decision medica al final).
        private static readonly StepInfo DecideWithoutPhone = new StepInfo { id = StepId.Llamar,
            title = "¿Necesita atención médica?",
            hint = "Mira la quemadura: si tiene ampollas, debe verla un médico.", clip = "guia_decidir" };

        // La tarjeta se oculta mientras hay un panel con la pregunta (la
        // tapaba) o durante la llamada al 106.
        private static bool PanelOpen
        {
            get { return MedicalDecisionPanel.IsOpen || EmergencyCall.InCall; }
        }

        private BurnProtocol protocol;
        private PatientBody patient;
        private PatientBurnArm arm;
        private BurnPatientReactions reactions;
        private PotAccident accident;
        private StoveKnob knob;
        private WaterTap coldTap;
        private WaterStreamFX stream;
        private PatientRing ring;
        private Transform head;

        private AudioSource narrator;
        private readonly List<string> queue = new List<string>();
        private float nextNarration;

        private Transform card;
        private TextMesh cardTitle, cardText, cardHint, cardWarning;
        private Vector3 cardVelocity;
        private Vector3 cardAnchor;
        private int cardChoice = -1;
        private float warningUntil;
        private Renderer cardBackground, cardAccent;
        private RoundedPanel cardPanel;
        private readonly List<RoundedPanel> cardDots = new List<RoundedPanel>();
        private float cardExpandUntil;
        private float cardGazeTime;
        private float cardOpen = 1f;     // 0 compacta, 1 completa (animado)
        private bool hiddenForDialog;

        private readonly List<Renderer> highlights = new List<Renderer>();
        private Material highlightMaterial;

        // Como estaba la cocina al abrir el menu de inicio (ver SceneDisturbed).
        private struct PropPose
        {
            public Rigidbody rb;
            public Vector3 position;
            public Quaternion rotation;
        }

        private readonly List<PropPose> menuProps = new List<PropPose>();
        private readonly Dictionary<WaterTap, bool> menuTaps = new Dictionary<WaterTap, bool>();
        private StoveKnob menuKnob;
        private bool menuKnobOn;

        // Modo elegido antes de recargar la escena: al volver no se pregunta
        // de nuevo.
        private static bool? pendingMode;

        private bool stepsActive;
        private int currentStep = -1;
        private bool currentIsReturn;
        private float stepShownTime;
        private bool coolingStarted;

        // Donde estaba la taza del tutorial (respaldo para dejarla).
        private Vector3 mugSpot;
        private bool hasMugSpot;
        private bool wasInCall;
        private readonly Dictionary<string, float> lastCorrection = new Dictionary<string, float>();

        private void Awake()
        {
            Instance = this;
            PanelPlacement.ExtraFocus.Add(AddHighlightFocus);
        }

        // La tarjeta no tapa lo que se esta resaltando (los brillos pegados a
        // un panel no cuentan: se mueven con el).
        private void AddHighlightFocus(System.Collections.Generic.List<Vector3> points)
        {
            foreach (Renderer r in highlights)
            {
                if (r != null && r.enabled && r.GetComponentInParent<PanelFollow>() == null)
                {
                    points.Add(r.transform.position);
                }
            }
        }

        // Sin recarga de dominio entre dos Play del editor, el modo pendiente
        // no debe pasar de una prueba a la siguiente.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearPendingMode()
        {
            pendingMode = null;
        }

        private void OnDestroy()
        {
            PanelPlacement.ExtraFocus.Remove(AddHighlightFocus);
            if (Instance == this)
            {
                Instance = null;
            }

            if (burnSequence != null)
            {
                burnSequence.OnEvent -= HandleEvent;
                burnSequence.OnStateChanged -= HandleState;
            }
        }

        private IEnumerator Start()
        {
            if (burnSequence == null)
            {
                burnSequence = GetComponent<BurnSequenceManager>();
            }

            if (burnSequence == null)
            {
                enabled = false;
                yield break;
            }

            protocol = FindObjectOfType<BurnProtocol>();
            patient = FindObjectOfType<PatientBody>();
            arm = patient != null ? patient.GetComponent<PatientBurnArm>() : null;
            reactions = patient != null ? patient.GetComponent<BurnPatientReactions>() : null;
            accident = FindObjectOfType<PotAccident>();
            stream = FindObjectOfType<WaterStreamFX>();
            foreach (WaterTap tap in FindObjectsOfType<WaterTap>())
            {
                if (!tap.hot)
                {
                    coldTap = tap;
                }
            }

            WaterTap anyTap = coldTap != null ? coldTap : FindObjectOfType<WaterTap>();
            highlightMaterial = anyTap != null ? anyTap.indicatorMaterial : null;

            burnSequence.OnEvent += HandleEvent;
            burnSequence.OnStateChanged += HandleState;

            // Las pistas de la paciente ("¿la pongo en agua fria?") las
            // reemplaza el narrador.
            if (reactions != null)
            {
                reactions.voiceHints = false;
            }

            // Un momento para que el visor entregue la pose de la cabeza.
            yield return new WaitForSeconds(0.8f);
            head = VRPanelKit.Head();
            CreateNarrator();
            CreateCard();

            if (pendingMode.HasValue)
            {
                // Escena recargada al elegir el modo (ver abajo).
                guided = pendingMode.Value;
                pendingMode = null;
            }
            else if (askMode)
            {
                TakeMenuSnapshot();
                yield return ChooseMode();

                // Si mientras estaba el menu el jugador boto o movio algo,
                // abrio una llave, giro la perilla o volco la olla, la
                // practica no empezaria con la cocina inicial: se recarga la
                // escena con el modo ya elegido.
                if (SceneDisturbed())
                {
                    pendingMode = guided;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    yield break;
                }
            }

            ApplyMode();

            if (tutorial && burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle)
            {
                yield return Tutorial();
            }
            else if (accident != null)
            {
                accident.Trigger(accidentAfterTutorial);
            }

            // Espera el accidente.
            while (burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle)
            {
                yield return null;
            }

            yield return new WaitForSeconds(2.5f);
            if (guided)
            {
                Say("guia_accidente");
                stepsActive = true;
            }
            else
            {
                Say("guia_evaluacion");
                ShowCard("Modo evaluación", "Atiende la quemadura como aprendiste. No habrá indicaciones.", null);
                yield return new WaitForSeconds(7f);
                HideCard();
            }
        }

        // ---------------- modo ----------------

        private IEnumerator ChooseMode()
        {
            bool chosen = false;
            Transform root = VRPanelKit.CreateRoot("PanelInicio", 0.6f, 0.15f);
            // Mismo estilo que la tarjeta del narrador (VRPanelKit.CardPanel).
            // Los dos botones son pildoras del mismo tamano y brillo: ninguna
            // opcion se sugiere sobre la otra.
            const float left = -0.28f;
            Vector2 menuSize = new Vector2(0.62f, 0.4f);
            VRPanelKit.CardPanel(root, Vector2.zero, menuSize);
            VRPanelKit.Text(root, "Primeros auxilios  ·  Antes de empezar", new Vector2(left, 0.17f), 0.016f,
                VRPanelKit.CardLabelColor);
            VRPanelKit.BoldText(root, "Quemadura en la cocina", new Vector2(left, 0.145f), 0.034f, Color.white);
            VRPanelKit.Text(root, VRPanelKit.Wrap("Aviso: esta práctica incluye gritos de dolor y una quemadura visible. " +
                    "Puedes quitarte el visor cuando quieras.", 60), new Vector2(left, 0.09f), 0.018f,
                VRPanelKit.CardWarningColor);
            VRPanelKit.Text(root, "Deja la mano sobre un botón hasta que se llene:", new Vector2(left, 0.03f), 0.018f,
                VRPanelKit.CardHintColor);
            VRPanelKit.RoundedButton(root, "Modo guiado", new Vector2(-0.14f, -0.05f), new Vector2(0.26f, 0.08f),
                new Color(0.22f, 0.48f, 0.95f), () => { guided = true; chosen = true; });
            VRPanelKit.RoundedButton(root, "Modo evaluación", new Vector2(0.14f, -0.05f), new Vector2(0.26f, 0.08f),
                new Color(0.52f, 0.36f, 0.9f), () => { guided = false; chosen = true; });
            VRPanelKit.Text(root, VRPanelKit.Wrap("Guiado: te indica cada paso. Evaluación: lo haces solo y se mide.", 64),
                new Vector2(left, -0.11f), 0.016f, VRPanelKit.CardHintColor);


            Say("guia_bienvenida");
            // De frente, sin inclinarse y sin tapar a la tia. Algo mas chico:
            // a 60 cm (al alcance de la mano) el tamano completo la tapaba
            // desde cualquier lado (PanelPlacement, rama corregir-bugs).
            const float menuScale = 0.8f;
            root.localScale = Vector3.one * menuScale;
            PanelFollow.Add(root, 0.6f, 0.08f, menuSize * menuScale);

            // El mismo brillo en las dos opciones: ninguna se sugiere sobre la
            // otra. Van pegados al panel, que se mueve.
            Highlight(root.TransformPoint(new Vector3(-0.14f, -0.05f, -0.01f)), 0f).transform.SetParent(root, true);
            Highlight(root.TransformPoint(new Vector3(0.14f, -0.05f, -0.01f)), 0f).transform.SetParent(root, true);

            while (!chosen)
            {
                yield return null;
            }

            Destroy(root.gameObject);
            StopNarration();
        }

        private void ApplyMode()
        {
            foreach (CoolingClock clock in FindObjectsOfType<CoolingClock>())
            {
                clock.showLabel = guided;
            }

            Debug.Log("[BurnGuide] Modo " + (guided ? "guiado" : "evaluacion"));
        }

        // Guarda donde estan los objetos sueltos, las llaves y la perilla al
        // abrir el menu. No cuenta la paciente (su mano agarrable se mueve
        // con ella) ni el jugador.
        private void TakeMenuSnapshot()
        {
            menuProps.Clear();
            menuTaps.Clear();
            PatientReach reach = patient != null ? patient.GetComponent<PatientReach>() : null;
            foreach (Rigidbody rb in FindObjectsOfType<Rigidbody>())
            {
                bool grabbable = rb.GetComponent<Grabbable>() != null
                    || rb.GetComponentInChildren<Oculus.Interaction.Grabbable>() != null;
                if ((rb.isKinematic && !grabbable)
                    || (patient != null && rb.transform.IsChildOf(patient.transform))
                    || (reach != null && rb == reach.target)
                    || rb.GetComponentInParent<VRLocomotion>() != null
                    || rb.GetComponentInParent<OVRCameraRig>() != null)
                {
                    continue;
                }

                menuProps.Add(new PropPose { rb = rb, position = rb.position, rotation = rb.rotation });
            }

            foreach (WaterTap tap in FindObjectsOfType<WaterTap>())
            {
                menuTaps[tap] = tap.IsOpen;
            }

            menuKnob = FindObjectOfType<StoveKnob>();
            menuKnobOn = menuKnob != null && menuKnob.IsOn;
        }

        private bool SceneDisturbed()
        {
            if (burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle)
            {
                Debug.Log("[BurnGuide] La quemadura ocurrio durante el menu: se reinicia la escena.");
                return true;
            }

            foreach (PropPose prop in menuProps)
            {
                // Destruido o muy lejos / girado de donde estaba.
                if (prop.rb == null || Vector3.Distance(prop.rb.position, prop.position) > 0.05f
                    || Quaternion.Angle(prop.rb.rotation, prop.rotation) > 30f)
                {
                    Debug.Log("[BurnGuide] Se movio " + (prop.rb != null ? prop.rb.name : "un objeto")
                        + " durante el menu: se reinicia la escena.");
                    return true;
                }
            }

            foreach (KeyValuePair<WaterTap, bool> tap in menuTaps)
            {
                if (tap.Key != null && tap.Key.IsOpen != tap.Value)
                {
                    Debug.Log("[BurnGuide] Se abrio o cerro una llave durante el menu: se reinicia la escena.");
                    return true;
                }
            }

            if (menuKnob != null && menuKnob.IsOn != menuKnobOn)
            {
                Debug.Log("[BurnGuide] Se giro la perilla durante el menu: se reinicia la escena.");
                return true;
            }

            return false;
        }

        // ---------------- tutorial ----------------

        private IEnumerator Tutorial()
        {
            if (accident != null)
            {
                accident.autoStart = false;
            }

            Rigidbody mug = BuildMug();
            Say("guia_intro");
            yield return WaitNarration();

            if (reactions != null)
            {
                reactions.Say("voz_pide_taza", true);
            }

            if (arm != null)
            {
                arm.LookAtPlayer(4f);
            }

            yield return new WaitForSeconds(2.2f);

            // 1. Agarrar.
            if (mug != null)
            {
                ShowCard("Tutorial 1 de 2", "Agarra la taza del mesón",
                    "Acerca la mano y aprieta el botón lateral del control, o junta el pulgar y el índice.");
                Say("guia_agarrar");
                HighlightFollow(mug.transform, 0.07f);
                float t = 0f;
                while (t < grabTimeout && !SimpleGrab.IsHeldObject(mug))
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                ClearHighlights();

                // 2. Caminar y entregar.
                ShowCard("Tutorial 2 de 2", "Llévale la taza a tu tía",
                    "Camina con el joystick izquierdo (sin controles: pellizca con el dedo medio izquierdo).");
                StopNarration();
                Say("guia_caminar");
                Transform herHand = patient != null && patient.IsReady ? patient.HandBone(false) : null;
                if (herHand != null)
                {
                    HighlightFollow(herHand, 0.06f);
                }

                t = 0f;
                bool delivered = false;
                while (t < bringTimeout && mug != null)
                {
                    t += Time.deltaTime;
                    if (patient != null && NearPatient(mug.worldCenterOfMass))
                    {
                        delivered = true;
                        break;
                    }

                    // Al acercarse con la taza en la mano, ella deja de
                    // revolver y se voltea para recibirla.
                    if (arm != null && head != null && patient.Chest != null && SimpleGrab.IsHeldObject(mug)
                        && PatientBody.Flat(head.position - patient.Chest.position).magnitude < attendDistance)
                    {
                        arm.AttendPlayer();
                    }

                    yield return null;
                }

                ClearHighlights();
                HideCard();
                if (delivered && arm != null)
                {
                    // El lugar se pide cuando ya volvio a mirar la cocina.
                    arm.ReceiveObject(mug, PutDownSpot);
                }

                if (delivered && reactions != null)
                {
                    reactions.Say("voz_gracias_taza", true);
                }

                yield return new WaitForSeconds(1.8f);
            }

            StopNarration();
            Say("guia_bien");
            yield return WaitNarration();

            if (accident != null)
            {
                accident.Trigger(accidentAfterTutorial);
            }
        }

        private bool NearPatient(Vector3 point)
        {
            Transform left = patient.HandBone(false);
            float toHand = left != null ? Vector3.Distance(point, left.position) : float.MaxValue;
            float toChest = patient.Chest != null ? Vector3.Distance(point, patient.Chest.position) : float.MaxValue;
            return Mathf.Min(toHand, toChest) < handOverDistance;
        }

        private Rigidbody BuildMug()
        {
            VRLocomotion player = FindObjectOfType<VRLocomotion>();
            if (player == null || coldTap == null)
            {
                return null;
            }

            float counter = BurnStaging.MeasureCounter(coldTap);
            BurnStaging.StoveArea(out Vector3 stove, out float stoveRadius);
            Vector3 near = head != null ? head.position : player.transform.position;
            if (!PropKit.FindSurfaceSpot(near, counter, 0.3f, 1.4f, 0.06f, null, stove, stoveRadius, out Vector3 spot))
            {
                Debug.LogWarning("[BurnGuide] No hay lugar en el meson para la taza; se salta esa parte del tutorial.");
                return null;
            }

            GameObject mug = PropKit.Grabbable("Taza", spot + Vector3.up * 0.003f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                new Vector3(0.085f, 0.095f, 0.085f), 0.3f);
            Transform t = mug.transform;
            PropKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.0475f, 0f), new Vector3(0.08f, 0.0475f, 0.08f),
                "Plato", new Color(0.93f, 0.93f, 0.9f));
            // Boca de la taza: apenas por encima de la tapa del cuerpo. Antes
            // su cara de arriba coincidia con la del cuerpo y parpadeaba (se
            // veian los triangulos como aspas negras).
            PropKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.0955f, 0f), new Vector3(0.07f, 0.001f, 0.07f),
                "Mango", new Color(0.25f, 0.15f, 0.08f));
            PropKit.MeshPart(t, PropKit.Torus("AsaTaza", 0.022f, 0.005f), new Vector3(0.045f, 0.05f, 0f),
                Quaternion.Euler(90f, 0f, 0f), "Plato", new Color(0.93f, 0.93f, 0.9f));
            mug.GetComponent<Grabbable>().SetHome();
            mugSpot = spot;
            hasMugSpot = true;
            Debug.Log("[BurnGuide] Taza del tutorial en " + spot.ToString("F2"));
            return mug.GetComponent<Rigidbody>();
        }

        // Donde la tia deja la taza: el meson a su izquierda.
        private Vector3 PutDownSpot()
        {
            Vector3 her = patient.transform.position;
            float counter = coldTap != null ? BurnStaging.MeasureCounter(coldTap) : her.y + 0.92f;
            BurnStaging.StoveArea(out Vector3 stove, out float stoveRadius);
            Vector3 left = her - patient.transform.right * 0.4f + patient.transform.forward * 0.3f;
            if (PropKit.FindSurfaceSpot(left, counter, 0.05f, 0.7f, 0.06f, null, stove, stoveRadius, out Vector3 spot))
            {
                return spot;
            }

            // Sin meson cerca (ella se movio): antes la soltaba en el aire a la
            // altura del meson, la taza caia al piso y volvia a su lugar
            // inicial, como si apareciera otra. Mejor un poco mas lejos o,
            // si no, donde estaba la taza.
            if (PropKit.FindSurfaceSpot(her, counter, 0.05f, 2f, 0.06f, null, stove, stoveRadius, out spot))
            {
                return spot;
            }

            return hasMugSpot ? mugSpot : new Vector3(left.x, counter, left.z);
        }

        // ---------------- pasos del protocolo ----------------

        private void HandleState(BurnSequenceManager.BurnState state)
        {
            if (state == BurnSequenceManager.BurnState.Cooling)
            {
                coolingStarted = true;
            }

            if (state == BurnSequenceManager.BurnState.Success)
            {
                stepsActive = false;
                ClearHighlights();
                HideCard();
            }
        }

        private bool IsDone(StepId id)
        {
            BurnSequenceManager.BurnState s = burnSequence.CurrentState;
            bool cooled = s == BurnSequenceManager.BurnState.Cooled || s == BurnSequenceManager.BurnState.Covered
                || s == BurnSequenceManager.BurnState.Success;
            switch (id)
            {
                case StepId.ApagarFuego:
                    return (protocol != null && protocol.IsDone(BurnProtocol.Step.ApagarFuego))
                        || (knob != null && !knob.IsOn);
                case StepId.AbrirAgua:
                    return (coldTap != null && coldTap.IsOpen) || s == BurnSequenceManager.BurnState.Cooling || cooled;
                case StepId.LlevarAlAgua:
                    return s == BurnSequenceManager.BurnState.Cooling || cooled;
                case StepId.Anillo:
                    return ring == null || ring.Removed || ring.Stuck
                        || (protocol != null && protocol.IsDone(BurnProtocol.Step.Anillo));
                case StepId.Abrigar:
                    return protocol == null || protocol.IsDone(BurnProtocol.Step.Abrigar);
                case StepId.Esperar:
                    return cooled;
                case StepId.Cubrir:
                    return s == BurnSequenceManager.BurnState.Covered || s == BurnSequenceManager.BurnState.Success;
                case StepId.Llamar:
                    return EmergencyCall.Instance != null
                        ? EmergencyCall.Instance.Decided
                        : protocol == null || protocol.Finished;
            }

            return true;
        }

        private bool Available(StepId id)
        {
            BurnSequenceManager.BurnState s = burnSequence.CurrentState;
            switch (id)
            {
                case StepId.Abrigar:
                case StepId.Esperar:
                    return coolingStarted;
                case StepId.Cubrir:
                    return IsDone(StepId.Esperar);
                case StepId.Llamar:
                    return EmergencyCall.Instance != null ? coolingStarted : IsDone(StepId.Cubrir);
            }

            return s != BurnSequenceManager.BurnState.Idle;
        }

        private int PickStep()
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                if (Available(Steps[i].id) && !IsDone(Steps[i].id))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Update()
        {
            UpdateNarration();
            UpdateCard();
            UpdateHighlights();

            if (!stepsActive)
            {
                return;
            }

            // Termino la practica (aunque falten pasos): sale el resumen y la
            // tarjeta del paso no debe quedar encima de el.
            if (protocol != null && protocol.Finished)
            {
                stepsActive = false;
                ClearHighlights();
                HideCard();
                return;
            }

            if (knob == null)
            {
                knob = FindObjectOfType<StoveKnob>();
            }

            if (ring == null && patient != null)
            {
                ring = patient.GetComponent<PatientRing>();
            }

            // Durante la llamada habla el operador: el narrador calla y no
            // recuerda pasos. Al colgar se muestra el paso que sigue.
            if (EmergencyCall.InCall)
            {
                if (!wasInCall)
                {
                    wasInCall = true;
                    StopNarration();
                    ClearHighlights();
                }

                return;
            }

            if (wasInCall)
            {
                wasInCall = false;
                currentStep = -2;
            }

            int step = PickStep();
            bool isReturn = step >= 0 && Steps[step].id == StepId.LlevarAlAgua && coolingStarted;
            if (step != currentStep || isReturn != currentIsReturn)
            {
                currentStep = step;
                currentIsReturn = isReturn;
                ShowStep(step, isReturn, true);
            }
            else if (step >= 0 && Time.time - stepShownTime > reminderSeconds && !Speaking)
            {
                // Recordatorio si no avanza.
                ShowStep(step, isReturn, true);
            }

            if (step >= 0)
            {
                UpdateStepHighlight(Steps[step].id);

                // Con cada tira de film, la tarjeta dice cuanto va cubierto.
                if (Steps[step].id == StepId.Cubrir)
                {
                    int cover = CoverPercent();
                    if (cover != shownCover)
                    {
                        shownCover = cover;
                        if (cover > 0)
                        {
                            ShowStep(step, false, false);
                        }
                    }
                }
            }
        }

        private int shownCover = -1;

        // Porcentaje de la quemadura cubierto con film (-1 si aun no hay tiras).
        private static int CoverPercent()
        {
            FilmCoverage coverage = FindObjectOfType<FilmCoverage>();
            return coverage != null && coverage.Strips > 0 ? Mathf.RoundToInt(coverage.Coverage * 100f) : -1;
        }

        private void ShowStep(int index, bool isReturn, bool speak)
        {
            stepShownTime = Time.time;
            ClearHighlights();
            if (index < 0)
            {
                HideCard();
                return;
            }

            StepInfo info = Steps[index];
            if (info.id == StepId.Llamar && EmergencyCall.Instance == null)
            {
                info = DecideWithoutPhone;
            }

            string title = isReturn ? "Vuelve a poner su mano bajo el agua" : info.title;
            string hint = isReturn ? "El reloj se pausó. Tómala del antebrazo y llévala al chorro." : info.hint;
            int cover = CoverPercent();
            if (!isReturn && info.id == StepId.Cubrir && cover >= 0)
            {
                FilmCoverage coverage = FindObjectOfType<FilmCoverage>();
                hint = $"Cubierto con film: {cover} % (hace falta {Mathf.RoundToInt(coverage.coverageNeeded * 100f)} %). "
                    + "Pon otra tira al lado, a lo largo de su mano.";
            }
            ShowCard($"Paso {index + 1} de {Steps.Length}", title, hint);
            if (speak)
            {
                StopNarration();
                Say(isReturn ? "guia_volver" : info.clip);
            }
        }

        // El brillo sigue al objeto del paso (algunos se mueven).
        private void UpdateStepHighlight(StepId id)
        {
            List<Vector3> points = new List<Vector3>();
            float radius = 0.06f;
            switch (id)
            {
                case StepId.ApagarFuego:
                    if (knob != null)
                    {
                        points.Add(knob.transform.position);
                        radius = 0.045f;
                    }

                    break;
                case StepId.AbrirAgua:
                    if (coldTap != null)
                    {
                        points.Add(coldTap.transform.position);
                        radius = 0.045f;
                    }

                    break;
                case StepId.LlevarAlAgua:
                    if (patient != null && patient.IsReady)
                    {
                        PatientReach reach = patient.GetComponent<PatientReach>();
                        bool led = reach != null && reach.IsLed;
                        if (led && stream != null)
                        {
                            points.Add(stream.NozzlePosition + Vector3.down * 0.1f);
                        }
                        else
                        {
                            Transform fore = patient.ForeArm(true), hand = patient.HandBone(true);
                            points.Add(Vector3.Lerp(fore.position, hand.position, 0.55f));
                        }
                    }

                    break;
                case StepId.Anillo:
                    // Sin brillo propio: el anillo ya tiene su punto dorado, y la
                    // esfera de la guia tapaba el anillo y su indicacion.
                    break;
                case StepId.Abrigar:
                    SweaterItem sweater = FindObjectOfType<SweaterItem>();
                    if (sweater != null)
                    {
                        Renderer rr = sweater.GetComponentInChildren<Renderer>();
                        points.Add(rr != null ? rr.bounds.center : sweater.transform.position);
                        radius = 0.12f;
                    }

                    break;
                case StepId.Llamar:
                    Transform phone = EmergencyCall.Instance != null ? EmergencyCall.Instance.Phone : null;
                    if (phone != null && !SimpleGrab.IsHeldObject(phone.GetComponent<Rigidbody>()))
                    {
                        points.Add(phone.position + Vector3.up * 0.01f);
                        radius = 0.06f;
                    }

                    break;
                case StepId.Esperar:
                    CoolingClock clock = FindObjectOfType<CoolingClock>();
                    if (clock != null && clock.Face != null)
                    {
                        points.Add(clock.Face.position);
                        radius = 0.2f;
                    }

                    break;
                case StepId.Cubrir:
                    foreach (CoverItem item in FindObjectsOfType<CoverItem>())
                    {
                        Renderer rr = item.GetComponentInChildren<Renderer>();
                        points.Add(rr != null ? rr.bounds.center : item.transform.position);
                    }

                    // La gasa esta en el botiquin: primero el tirador de la
                    // tapa; abierto, los sobres.
                    if (FirstAidKit.Current != null && !FirstAidKit.Current.IsOpen)
                    {
                        points.Add(FirstAidKit.Current.HandlePosition);
                    }
                    else
                    {
                        foreach (GauzePacket packet in FindObjectsOfType<GauzePacket>())
                        {
                            points.Add(packet.transform.position);
                        }
                    }

                    foreach (FilmRoll roll in FindObjectsOfType<FilmRoll>())
                    {
                        if (!roll.GetComponent<Grabbable>().IsHeld)
                        {
                            points.Add(roll.transform.TransformPoint(roll.axisCenter));
                        }
                        else if (roll.EdgeWaiting(out Vector3 edge))
                        {
                            // Con el rollo en una mano: el borde de donde se tira.
                            points.Add(edge);
                        }
                    }

                    radius = 0.08f;
                    break;
            }

            while (highlights.Count < points.Count)
            {
                Highlight(Vector3.zero, radius);
            }

            for (int i = 0; i < highlights.Count; i++)
            {
                bool on = i < points.Count;
                highlights[i].enabled = on;
                if (on)
                {
                    highlights[i].transform.position = points[i];
                    IndicatorUtil.SetWorldDiameter(highlights[i].transform, radius * 2f);
                }
            }
        }

        private Transform FindRing()
        {
            Transform hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            if (hand == null)
            {
                return null;
            }

            foreach (Transform t in hand.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Anillo")
                {
                    return t;
                }
            }

            return null;
        }

        // ---------------- correcciones ----------------

        private void HandleEvent(BurnSequenceManager.BurnEvent e, string detail, BurnSequenceManager.Severity severity)
        {
            if (!guided || !stepsActive)
            {
                return;
            }

            switch (e)
            {
                case BurnSequenceManager.BurnEvent.HotWaterOn:
                    // Con la fria abierta, la caliente solo la templa (sale
                    // tibia): esta bien, no se corrige.
                    if (coldTap != null && coldTap.IsOpen)
                    {
                        break;
                    }

                    Correct("guia_err_caliente", "Esa es la llave caliente. Ciérrala y usa la del punto azul.");
                    break;
                case BurnSequenceManager.BurnEvent.HotWaterOnBurn:
                    Correct("guia_err_caliente", "Esa es la llave caliente. Ciérrala y usa la del punto azul.");
                    break;
                case BurnSequenceManager.BurnEvent.RemedyNear:
                case BurnSequenceManager.BurnEvent.RemedyApplied:
                    Correct("guia_err_remedio", "No le pongas nada en la quemadura: solo agua fría corriente.");
                    break;
                case BurnSequenceManager.BurnEvent.BurnedHandGrabbed:
                    Correct("guia_err_mano", "Sujétala del antebrazo: la mano quemada le duele.");
                    break;
                case BurnSequenceManager.BurnEvent.ClothOnBurn:
                    Correct("guia_err_pano", "El paño de cocina suelta pelusa. Usa una gasa limpia o, si no hay, film.");
                    break;
                case BurnSequenceManager.BurnEvent.CoveredTooEarly:
                    Correct("guia_err_temprano", "Todavía no: primero termina de enfriar la quemadura.");
                    break;
                case BurnSequenceManager.BurnEvent.PatientAlarmed:
                    Correct("guia_err_calma", "Háblale con calma: dile que el agua fría alivia.");
                    break;
                case BurnSequenceManager.BurnEvent.BadAdvice:
                    Correct("guia_err_remedio", "No le pongas nada en la quemadura: solo agua fría corriente.");
                    break;
                case BurnSequenceManager.BurnEvent.CoolingGaveUp:
                    Correct("guia_err_aguantar", "Anímala a aguantar o témplale un poco el agua: hay que completar los 20 minutos.");
                    break;
                case BurnSequenceManager.BurnEvent.HotPotTouched:
                    Correct("guia_err_olla", "No toques la olla: sigue caliente y también te quemarías.");
                    break;
                case BurnSequenceManager.BurnEvent.HotSpillStepped:
                    Correct("guia_err_charco", "El piso mojado resbala: rodea el agua derramada.");
                    break;
                case BurnSequenceManager.BurnEvent.FilmWrapped:
                    Correct("guia_err_envolver", "No la envuelvas: apoya el film encima. La mano se va a hinchar.");
                    break;
                case BurnSequenceManager.BurnEvent.GauzePressed:
                    Correct("guia_err_apretar", "Apóyala sin apretar: solo tiene que quedar encima.");
                    break;
                case BurnSequenceManager.BurnEvent.GauzeTouchedCenter:
                    Correct("guia_err_centro_gasa", "Tómala de los bordes: el centro va sobre la herida y debe quedar limpio.");
                    break;
                case BurnSequenceManager.BurnEvent.GauzeOffCenter:
                    Correct("guia_err_gasa_corrida", "La gasa quedó corrida: la próxima vez céntrala para cubrir toda la quemadura.");
                    break;
                case BurnSequenceManager.BurnEvent.BandageApplied:
                    if (severity != BurnSequenceManager.Severity.None)
                    {
                        Correct("guia_err_venda", "La venda va suelta: al hincharse la mano, una venda apretada le corta la circulación.");
                    }

                    break;
                case BurnSequenceManager.BurnEvent.RingStuck:
                    // Antes el punto del anillo desaparecia sin explicacion y
                    // parecia que no funcionaba.
                    Correct("guia_err_anillo",
                        "Su mano ya se hinchó: no fuerces el anillo. Sigue con el agua fría; en el centro de salud se lo sacarán.");
                    break;
            }
        }

        private void Correct(string clip, string text)
        {
            if (lastCorrection.TryGetValue(clip, out float last) && Time.time - last < 10f)
            {
                return;
            }

            lastCorrection[clip] = Time.time;
            if (cardWarning != null)
            {
                cardWarning.text = VRPanelKit.Wrap("¡Cuidado! " + text, 44);
                LayoutCard();
                warningUntil = Time.time + 6f;
                ExpandCard(6f);
                if (card != null && !card.gameObject.activeSelf)
                {
                    card.gameObject.SetActive(true);
                }
            }

            // Despues del quejido de la paciente.
            StopNarration();
            nextNarration = Time.time + 1.2f;
            Say(clip);
            stepShownTime = Time.time;
        }

        // ---------------- narrador ----------------

        private void CreateNarrator()
        {
            GameObject go = new GameObject("VozNarrador");
            if (head != null)
            {
                go.transform.SetParent(head, false);
            }

            narrator = go.AddComponent<AudioSource>();
            narrator.playOnAwake = false;
            narrator.spatialBlend = 0f;
            narrator.volume = narratorVolume;
        }

        private void Say(string clip)
        {
            if (!string.IsNullOrEmpty(clip))
            {
                queue.Add(clip);
            }
        }

        private void StopNarration()
        {
            queue.Clear();
            if (narrator != null)
            {
                narrator.Stop();
            }
        }

        private IEnumerator WaitNarration()
        {
            float limit = Time.time + 20f;
            while (Speaking && Time.time < limit)
            {
                yield return null;
            }
        }

        private void UpdateNarration()
        {
            if (narrator == null || narrator.isPlaying || queue.Count == 0 || Time.time < nextNarration)
            {
                return;
            }

            // No pisa a la paciente (espera hasta 3 s a que termine).
            if (reactions != null && reactions.IsSpeaking && Time.time - stepShownTime < 3f)
            {
                return;
            }

            string name = queue[0];
            queue.RemoveAt(0);
            AudioClip clip = Hito1FX.Clip(name);
            if (clip == null)
            {
                return;
            }

            narrator.clip = clip;
            narrator.Play();
            nextNarration = Time.time + clip.length + 0.35f;
        }

        // ---------------- tarjeta ----------------

        private const float CardHeight = 0.23f;
        private const float CardWidth = 0.44f;
        private const float CardPad = 0.022f;
        private const float CardMinHeight = 0.12f;
        private const float TitleLine = 0.014f;
        private const float TextLine = 0.026f;
        private const float HintLine = 0.017f;
        private const float DotSize = 0.007f;

        private void CreateCard()
        {
            GameObject go = new GameObject("TarjetaGuia");
            go.AddComponent<NoAutoCollider>();
            card = go.transform;
            go.transform.localScale = Vector3.one * cardScale;
            // Panel de vidrio oscuro: esquinas redondeadas, borde azul a morado
            // y un brillo suave alrededor.
            cardPanel = RoundedPanel.Create(card, "FondoTarjeta", Vector2.zero, new Vector2(CardWidth, CardHeight),
                0.001f, 0.022f, 0.0018f, 0.012f);
            cardPanel.top = new Color(0.1f, 0.14f, 0.24f, 1f);
            cardPanel.bottom = new Color(0.05f, 0.07f, 0.14f, 1f);
            cardPanel.Push();
            cardBackground = cardPanel.Renderer;
            cardAccent = null;

            float left = -CardWidth * 0.5f + CardPad;
            float top = CardHeight * 0.5f - CardPad;
            // Jerarquia: "Paso 3 de 8" chico, la accion en grande y negrita,
            // y la explicacion debajo.
            cardTitle = VRPanelKit.Text(card, "", new Vector2(left, top), TitleLine, new Color(0.62f, 0.8f, 1f));
            cardText = VRPanelKit.Text(card, "", new Vector2(left, top - 0.024f), TextLine, Color.white);
            PanelText bold = cardText.GetComponent<PanelText>();
            if (bold != null && bold.Tmp != null)
            {
                bold.Tmp.fontStyle = TMPro.FontStyles.Bold;
            }

            cardHint = VRPanelKit.Text(card, "", new Vector2(left, 0f), HintLine, new Color(0.82f, 0.87f, 0.95f));
            cardWarning = VRPanelKit.Text(card, "", new Vector2(left, 0f), HintLine, new Color(1f, 0.66f, 0.45f));
            // Siempre encima: no se mete en el lavadero, la pared ni la paciente.
            go.AddComponent<OverlayPanel>();
            go.SetActive(false);
        }

        private static int Lines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int n = 1;
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    n++;
                }
            }

            return n;
        }

        // Ubica la pista y el aviso debajo del texto principal.
        private void LayoutCard()
        {
            float y = cardText.transform.localPosition.y - Lines(cardText.text) * TextLine * 1.2f - 0.01f;
            Vector3 p = cardHint.transform.localPosition;
            cardHint.transform.localPosition = new Vector3(p.x, y, p.z);
            y -= Lines(cardHint.text) * HintLine * 1.2f + 0.008f;
            p = cardWarning.transform.localPosition;
            cardWarning.transform.localPosition = new Vector3(p.x, y, p.z);
        }

        private void ShowCard(string title, string text, string hint)
        {
            if (card == null)
            {
                return;
            }

            bool wasHidden = !card.gameObject.activeSelf;
            cardTitle.text = title ?? "";
            UpdateDots(cardTitle.text);
            cardText.text = VRPanelKit.Wrap(text ?? "", 28);
            cardHint.text = VRPanelKit.Wrap(hint ?? "", 44);
            LayoutCard();
            if (PanelOpen)
            {
                // El panel de decision ya trae la pregunta: la tarjeta lo tapaba.
                card.gameObject.SetActive(false);
                return;
            }

            card.gameObject.SetActive(true);
            ExpandCard(4f);
            if (wasHidden)
            {
                cardOpen = 1f;
                PlaceCard(true);
            }

            Hito1FX.PlayAt("logro_paso", card.position, 0.15f, 0f);
        }

        private void HideCard()
        {
            if (card != null)
            {
                card.gameObject.SetActive(false);
            }
        }

        // Acompana la vista sin pegarse a ella: se reacomoda solo cuando la
        // mirada se aleja mucho.
        private void UpdateCard()
        {
            if (card == null)
            {
                return;
            }

            // Mientras la paciente pregunta algo (PatientDialog) la tarjeta se
            // esconde y vuelve al cerrarse el panel.
            if (hiddenForDialog && !PatientDialog.IsOpen)
            {
                hiddenForDialog = false;
                card.gameObject.SetActive(true);
                PlaceCard(true);
            }

            if (!card.gameObject.activeSelf)
            {
                return;
            }

            if (PatientDialog.IsOpen)
            {
                hiddenForDialog = true;
                card.gameObject.SetActive(false);
                return;
            }

            if (PanelOpen)
            {
                card.gameObject.SetActive(false);
                return;
            }

            if (cardWarning != null && cardWarning.text.Length > 0 && Time.time > warningUntil)
            {
                cardWarning.text = "";
                LayoutCard();
            }

            PlaceCard(false);
            UpdateCardSize();
        }

        // Mantiene la tarjeta completa al menos 'seconds' desde ahora.
        private void ExpandCard(float seconds)
        {
            cardExpandUntil = Mathf.Max(cardExpandUntil, Time.time + seconds);
        }

        // La tarjeta completa (titulo, paso, pista y aviso) solo se muestra
        // mientras hace falta: al aparecer un paso, mientras habla el
        // narrador, con un aviso, o si el jugador la mira. Si no, se achica al
        // paso actual y el fondo se vuelve translucido: tapa menos la cocina.
        private void UpdateCardSize()
        {
            if (head != null)
            {
                Vector3 toCard = card.position - head.position;
                bool looking = Vector3.Angle(head.forward, toCard) < 14f;
                cardGazeTime = looking ? cardGazeTime + Time.deltaTime : 0f;
                if (cardGazeTime > 0.35f)
                {
                    ExpandCard(1.5f);
                }
            }

            if (Speaking || Time.time < warningUntil)
            {
                ExpandCard(cardCompactDelay);
            }

            float wanted = Time.time < cardExpandUntil ? 1f : 0f;
            cardOpen = Mathf.MoveTowards(cardOpen, wanted, Time.deltaTime / 0.3f);
            float k = Mathf.SmoothStep(0f, 1f, cardOpen);

            float top = CardHeight * 0.5f;
            float compact = CardPad + 0.024f + Lines(cardText.text) * TextLine * 1.2f + 0.012f;
            float full = Mathf.Max(CardMinHeight, top - CardBottom() + CardPad);
            float height = Mathf.Lerp(compact, full, k);
            if (cardPanel != null)
            {
                cardPanel.SetRect(new Vector2(0f, top - height * 0.5f), new Vector2(CardWidth, height));
                cardPanel.SetFillAlpha(Mathf.Lerp(cardAlphaCompact, cardAlphaFull, k));
            }
            SetAlpha(cardHint, k);
            SetAlpha(cardWarning, k);
        }

        // Borde inferior del texto (coordenadas de la tarjeta).
        private float CardBottom()
        {
            float y = cardWarning.text.Length > 0 ? cardWarning.transform.localPosition.y
                - Lines(cardWarning.text) * HintLine * 1.2f
                : cardHint.transform.localPosition.y - Lines(cardHint.text) * HintLine * 1.2f;
            return y;
        }

        // Puntitos de progreso a la derecha de "Paso 3 de 8": los hechos en
        // azul suave, el actual alargado y brillante, los que faltan tenues.
        private void UpdateDots(string title)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(title ?? "",
                @"(\d+)\s+de\s+(\d+)");
            int current = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            int total = m.Success ? Mathf.Clamp(int.Parse(m.Groups[2].Value), 0, 12) : 0;

            while (cardDots.Count < total)
            {
                RoundedPanel dot = RoundedPanel.Create(card, "Punto", Vector2.zero, Vector2.one * DotSize, -0.001f,
                    DotSize * 0.5f, 0f, 0.004f);
                dot.sheen = 0f;
                cardDots.Add(dot);
            }

            float right = CardWidth * 0.5f - CardPad;
            float y = CardHeight * 0.5f - CardPad - TitleLine * 0.55f;
            const float gap = 0.0055f;
            float x = right;
            for (int i = cardDots.Count - 1; i >= 0; i--)
            {
                RoundedPanel dot = cardDots[i];
                bool show = i < total;
                dot.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                int step = i + 1;
                bool now = step == current;
                float width = now ? DotSize * 2.6f : DotSize;
                dot.SetRect(new Vector2(x - width * 0.5f, y), new Vector2(width, DotSize));
                x -= width + gap;
                Color c = now ? new Color(0.45f, 0.86f, 1f, 1f)
                    : step < current ? new Color(0.5f, 0.72f, 1f, 0.75f)
                    : new Color(1f, 1f, 1f, 0.25f);
                dot.SetSolid(c, now ? 0.6f : 0f);
            }
        }

        private static void SetAlpha(TextMesh text, float alpha)
        {
            if (text == null)
            {
                return;
            }

            Color c = text.color;
            c.a = alpha;
            text.color = c;
            text.GetComponent<Renderer>().enabled = alpha > 0.02f;
        }

        private void PlaceCard(bool snap)
        {
            if (head == null)
            {
                head = VRPanelKit.Head();
                if (head == null)
                {
                    return;
                }
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            forward.Normalize();

            // La base sigue la mirada sin prisa; sobre ella se elige el lugar
            // que no tapa a la paciente, el reloj ni el objeto del paso.
            if (snap || cardAnchor.sqrMagnitude < 0.5f || Vector3.Angle(forward, cardAnchor) > cardFollowAngle)
            {
                cardAnchor = forward;
            }

            Vector2 half = new Vector2(CardWidth, CardHeight) * (cardScale * 0.5f);
            // Al mirar el meson la tarjeta baja con la vista (antes quedaba
            // arriba, sobre los gabinetes).
            float lookDown = Mathf.Clamp(-head.forward.y, 0f, 0.6f);
            float drop = cardDrop + lookDown * cardDistance * 0.5f;
            Vector3 wanted = PanelPlacement.Choose(head.position, cardAnchor, cardDistance, drop, half, ref cardChoice);
            if (snap)
            {
                card.position = wanted;
                cardVelocity = Vector3.zero;
            }
            else
            {
                card.position = Vector3.SmoothDamp(card.position, wanted, ref cardVelocity, 0.35f);
            }

            card.rotation = PanelPlacement.Facing(card.position, head.position);
        }

        // ---------------- brillo ----------------

        private Renderer Highlight(Vector3 position, float radius)
        {
            Renderer r = IndicatorUtil.CreateSphere("BrilloGuia", null, position, Mathf.Max(0.02f, radius),
                highlightMaterial, new Color(0.3f, 0.85f, 1f, 0.2f));
            highlights.Add(r);
            return r;
        }

        private void HighlightFollow(Transform target, float radius)
        {
            Renderer r = Highlight(target.position, radius);
            r.gameObject.AddComponent<FollowTarget>().target = target;
        }

        private void ClearHighlights()
        {
            foreach (Renderer r in highlights)
            {
                if (r != null)
                {
                    Destroy(r.gameObject);
                }
            }

            highlights.Clear();
        }

        private void UpdateHighlights()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f);
            Color color = new Color(0.3f, 0.85f, 1f, Mathf.Lerp(0.06f, 0.24f, pulse));
            foreach (Renderer r in highlights)
            {
                if (r != null && r.enabled)
                {
                    IndicatorUtil.SetColor(r, color);
                }
            }
        }
    }
}
