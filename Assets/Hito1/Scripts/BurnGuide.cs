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
        [Tooltip("Segundos para agarrar la taza antes de seguir sin ella (0: espera al jugador y repite la indicacion). Antes 45: si el jugador no hacia nada, el accidente pasaba igual.")]
        public float grabTimeout = 0f;
        [Tooltip("Segundos para llevarle la taza (0: espera al jugador).")]
        public float bringTimeout = 0f;
        [Tooltip("Cada cuanto se repite la indicacion del tutorial mientras se espera (s).")]
        public float tutorialReminder = 25f;
        [Tooltip("Segundos que espera el paso de la pausa del tutorial antes de seguir sin el.")]
        public float pauseTutorialTimeout = 45f;

        // El panel de inicio (modo guiado o evaluacion) esta abierto: todavia
        // no empezo nada, asi que no hay pausa.
        public bool ChoosingMode { get; private set; }

        // La practica termino (por ejemplo, desde el menu de pausa antes del
        // accidente): el tutorial deja de esperar y no hay accidente.
        private bool Over
        {
            get { return protocol != null && protocol.Finished; }
        }
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
            Cubrir,
            Preparar
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
                hint = "Acerca la mano a la perilla que brilla y apriétala (botón lateral o pinza). Así nadie más se quema.",
                clip = "guia_apagar" },
            new StepInfo { id = StepId.AbrirAgua, title = "Abre el agua fría",
                hint = "Es la llave del punto azul: acerca la mano y apriétala (botón lateral o pinza).", clip = "guia_agua" },
            // El anillo va antes del chorro: ella ofrece la mano a la altura del
            // pecho y se alcanza facil (bajo el agua costaba mucho). Si se lo
            // salta, se puede sacar despues, tambien bajo el chorro.
            new StepInfo { id = StepId.Anillo, title = "Quítale el anillo",
                hint = "Antes de que se hinche. Pellizca su anillo y deslízalo hacia la punta del dedo.",
                clip = "guia_anillo" },
            new StepInfo { id = StepId.LlevarAlAgua, title = "Lleva su mano al chorro",
                hint = "Tómala del antebrazo, no de la mano quemada, y llévala al chorro: ella la pondrá bajo el agua.",
                clip = "guia_llevar" },
            // La manta esta en el sofa de la sala: hay que ir a buscarla y ella
            // se queda con la mano bajo el agua.
            new StepInfo { id = StepId.Abrigar, title = "Abrígala con una manta",
                hint = "El agua fría también le enfría el cuerpo. Sal de la cocina hacia la sala: la manta está doblada en el sofá, detrás de la mesa de centro. Sigue las flechas del piso y suéltala sobre sus hombros.",
                clip = "guia_abrigo" },
            // Mientras se enfria (antes ese minuto quedaba sin nada que hacer).
            // Sin celular en la escena se decide al final con el panel
            // (ver ShowStep).
            new StepInfo { id = StepId.Llamar, title = "Llama al 106",
                hint = "Toma el celular del mesón y toca «Llamar 106» en su pantalla con la otra mano (o aprieta A/X en el control que lo sostiene). Sale en altavoz: puedes dejarlo en el mesón y seguir atendiéndola.",
                clip = "guia_llamar" },
            // Los 20 minutos de agua dan tiempo de buscar con que cubrir: el
            // film y la bolsa estan guardados en el cajon, como en una casa
            // (el botiquin del mueble alto solo tiene curitas y pomada, que no
            // sirven). No da ni quita puntos (ninguna guia lo pide); solo
            // evita que al terminar haya que salir a buscarlo.
            new StepInfo { id = StepId.Preparar, title = "Busca con qué cubrirla",
                hint = "Mientras se enfría, abre el cajón bajo el mesón junto al lavadero y saca el film o la bolsa plástica.",
                clip = "guia_preparar" },
            new StepInfo { id = StepId.Esperar, title = "Enfría 20 minutos",
                hint = "Mantén su mano bajo el agua hasta que el reloj complete las 20 marcas.", clip = "guia_esperar" },
            // Cruz Roja Britanica: "loosely cover it with cling film or a
            // clean plastic bag"; NHS: el film encima, sin envolver.
            new StepInfo { id = StepId.Cubrir, title = "Cúbrela sin apretar",
                hint = "Con la bolsa: pasa la boca de la bolsa por sus dedos y deslízala hasta la muñeca. Con film: sostén el rollo, tira del borde que brilla con la otra mano y apoya cada tira a lo largo de su mano, sin envolverla.",
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
        private float coolingStartTime;

        // Busqueda de la manta (ver UpdateBlanketGuidance).
        [Tooltip("Modo evaluacion: segundos desde que empieza a enfriar hasta que la tia comenta que tiene frio, si el jugador no fue a la sala.")]
        public float blanketHintDelay = 20f;
        [Tooltip("Modo evaluacion: segundos entre la primera y la segunda vez que lo comenta.")]
        public float blanketHintRepeat = 45f;
        private FloorPathGuide pathGuide;
        private BlanketItem blanketItem;
        private int blanketHintsSaid;
        private float nextBlanketHint;

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
                ChoosingMode = true;
                yield return ChooseMode();
                ChoosingMode = false;

                // Si mientras estaba el menu el jugador boto o movio algo,
                // abrio una llave, giro la perilla o volco la olla, la
                // practica no empezaria con la cocina inicial: se recarga la
                // escena con el modo ya elegido.
                if (SceneDisturbed())
                {
                    pendingMode = guided;
                    // Con el mismo caso de quemadura.
                    if (BurnCase.Current != null)
                    {
                        BurnCase.Next = BurnCase.Current.Copy();
                    }

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
            while (burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle && !Over)
            {
                yield return null;
            }

            yield return new WaitForSeconds(2.5f);
            if (Over)
            {
                yield break;
            }
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
            VRPanelKit.Text(root, "Apunta a un botón y aprieta el gatillo (o haz la pinza):", new Vector2(left, 0.03f), 0.018f,
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
                ShowCard("Tutorial 1 de 3", "Agarra la taza del mesón",
                    "Acerca la mano y aprieta el botón lateral del control, o junta el pulgar y el índice.");
                Say("guia_agarrar");
                HighlightFollow(mug.transform, 0.07f);
                float t = 0f;
                float remind = tutorialReminder;
                while ((grabTimeout <= 0f || t < grabTimeout) && !SimpleGrab.IsHeldObject(mug) && !Over)
                {
                    t += Time.deltaTime;
                    if (t >= remind && !Speaking)
                    {
                        remind = t + tutorialReminder;
                        Say("guia_agarrar");
                    }

                    yield return null;
                }

                ClearHighlights();

                // 2. Caminar y entregar.
                ShowCard("Tutorial 2 de 3", "Llévale la taza a tu tía",
                    "Camina con el joystick izquierdo (sin controles: pellizca con el dedo medio izquierdo).");
                StopNarration();
                Say("guia_caminar");
                Transform herHand = patient != null && patient.IsReady ? patient.HandBone(false) : null;
                if (herHand != null)
                {
                    HighlightFollow(herHand, 0.06f);
                }

                t = 0f;
                remind = tutorialReminder;
                bool delivered = false;
                while ((bringTimeout <= 0f || t < bringTimeout) && mug != null && !Over)
                {
                    t += Time.deltaTime;
                    if (t >= remind && !Speaking)
                    {
                        remind = t + tutorialReminder;
                        Say("guia_caminar");
                    }

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

            // 3. Pausa: un jugador nuevo no sabe que existe ni como se abre.
            if (!Over)
            {
                yield return PauseTutorial();
            }

            StopNarration();
            if (Over)
            {
                // Termino desde el menu de pausa antes del accidente.
                ClearHighlights();
                HideCard();
                yield break;
            }

            Say("guia_bien");
            yield return WaitNarration();
            if (accident != null && !Over)
            {
                accident.Trigger(accidentAfterTutorial);
            }
        }

        // Ensena a abrir y cerrar la pausa, segun como juega: con controles
        // el boton de menu del control izquierdo (o mantener B/Y); con las
        // manos, el boton de la muneca izquierda (WristPauseButton). Si no lo
        // hace en 'pauseTutorialTimeout' segundos sigue igual (la tarjeta de
        // la pausa no debe trabar la historia).
        private IEnumerator PauseTutorial()
        {
            float waited = 0f;
            string shownHint = null;
            bool highlighted = false;
            while (!PauseMenu.Paused && !Over && waited < pauseTutorialTimeout)
            {
                waited += Time.unscaledDeltaTime;
                string hint = PauseHint();
                if (hint != shownHint)
                {
                    shownHint = hint;
                    ShowCard("Tutorial 3 de 3", "Abre la pausa", hint);
                }

                WristPauseButton wrist = WristPauseButton.Instance;
                bool wristVisible = wrist != null && wrist.Visible;
                if (wristVisible && !highlighted)
                {
                    HighlightFollow(wrist.ButtonTransform, 0.05f);
                    highlighted = true;
                }
                else if (!wristVisible && highlighted)
                {
                    ClearHighlights();
                    highlighted = false;
                }

                yield return null;
            }

            ClearHighlights();
            if (PauseMenu.Paused)
            {
                ShowCard("Tutorial 3 de 3", "Así se pausa la práctica",
                    "El reloj se detiene. Toca \"Continuar\" para seguir.");
                while (PauseMenu.Paused && !Over)
                {
                    yield return null;
                }
            }

            HideCard();
        }

        // Indicacion para abrir la pausa con lo que esta usando el jugador.
        private static string PauseHint()
        {
            bool hands = SimpleGrab.TryGetPalm(true, out Vector3 _, out Vector3 _, out Vector3 _);
            return hands
                ? "Mira la palma de tu mano izquierda: aparece \"Pausa\" en la muñeca. Tócalo con la otra mano y haz la pinza."
                : "Aprieta el botón de menú del control izquierdo (el de tres rayas), o mantén B o Y un segundo.";
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
            if (state == BurnSequenceManager.BurnState.Cooling && !coolingStarted)
            {
                coolingStarted = true;
                coolingStartTime = Time.time;
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
                case StepId.Preparar:
                    return SuppliesOut();
            }

            return true;
        }

        // Ya hay con que cubrir a mano: el film o la bolsa fuera del cajon
        // (o no hay nada guardado).
        private static bool SuppliesOut()
        {
            bool any = false;
            foreach (FilmRoll roll in FindObjectsOfType<FilmRoll>())
            {
                any = true;
                if (!StorageFront.IsStored(roll.GetComponent<Rigidbody>()))
                {
                    return true;
                }
            }

            foreach (CoverItem item in FindObjectsOfType<CoverItem>())
            {
                any = true;
                if (!StorageFront.IsStored(item.GetComponent<Rigidbody>()))
                {
                    return true;
                }
            }

            return !any;
        }

        // El tirador del cajon si esta cerrado; abierto, el film y la bolsa.
        private static void AddSupplies(List<Vector3> points)
        {
            foreach (CoverItem item in FindObjectsOfType<CoverItem>())
            {
                AddSupply(points, item.GetComponent<Rigidbody>(), item.GetComponentInChildren<Renderer>());
            }

            foreach (FilmRoll roll in FindObjectsOfType<FilmRoll>())
            {
                AddSupply(points, roll.GetComponent<Rigidbody>(), roll.GetComponentInChildren<Renderer>());
            }
        }

        private static void AddSupply(List<Vector3> points, Rigidbody body, Renderer renderer)
        {
            StorageFront drawer = StorageFront.Holder(body);
            if (drawer != null && !drawer.IsOpen)
            {
                AddOnce(points, drawer.HandlePosition);
            }
            else if (drawer != null && renderer != null)
            {
                points.Add(renderer.bounds.center);
            }
        }

        // Hacia donde apuntan los dedos de la mano quemada (del hueso de la
        // mano al nudillo del medio).
        private static Vector3 FingerDirection(Transform hand)
        {
            foreach (Transform t in hand.GetComponentsInChildren<Transform>())
            {
                if (t.name.EndsWith("Middle1"))
                {
                    Vector3 d = t.position - hand.position;
                    if (d.sqrMagnitude > 1e-6f)
                    {
                        return d.normalized;
                    }
                }
            }

            return hand.up;
        }

        // ---------------- orden flexible ----------------
        //
        // Cada paso tiene su momento, pero ninguno bloquea a los demas (antes
        // el guia mostraba siempre el primer paso pendiente de la lista: sin
        // llamar al 106 nunca llegaba a cubrir, y el anillo tapaba llevar la
        // mano al agua). Cuando pasa el momento de un paso que todavia se
        // puede hacer, la tarjeta lo deja abajo como "Cuando puedas" y sigue
        // con lo que toca:
        //   - Anillo: antes del chorro, por 'ringFocusSeconds'. Despues se
        //     puede sacar bajo el agua (hasta que la mano se hinche).
        //   - Abrigar, llamar y buscar con que cubrir: durante el
        //     enfriamiento, en cualquier orden. Abrigar y llamar siguen
        //     valiendo mientras se cubre y despues.
        //   - Cubrir: al terminar de enfriar pasa primero, aunque falte
        //     llamar o abrigar.

        [Tooltip("Segundos que quitar el anillo es el paso principal antes de pasar al chorro (se puede sacar despues, bajo el agua).")]
        public float ringFocusSeconds = 25f;
        private float ringShownTime = -1f;

        private static readonly StepId[] OrderCooling =
        {
            StepId.ApagarFuego, StepId.AbrirAgua, StepId.Anillo, StepId.LlevarAlAgua, StepId.Abrigar, StepId.Llamar,
            StepId.Preparar, StepId.Esperar, StepId.Cubrir
        };

        // Ya enfriada: cubrir antes que lo que quedo pendiente.
        private static readonly StepId[] OrderCooled =
        {
            StepId.ApagarFuego, StepId.AbrirAgua, StepId.LlevarAlAgua, StepId.Cubrir, StepId.Abrigar, StepId.Llamar,
            StepId.Anillo
        };

        // Su momento ya paso, pero todavia se puede hacer.
        private bool Deferred(StepId id)
        {
            switch (id)
            {
                case StepId.Anillo:
                    // No cambia de paso mientras lo esta sacando.
                    if (ring != null && ring.Progress > 0.02f)
                    {
                        return false;
                    }

                    return coolingStarted || (ringShownTime >= 0f && Time.time - ringShownTime > ringFocusSeconds);
            }

            return false;
        }

        // Pasos que se pueden hacer en paralelo con el principal.
        private static bool CanWait(StepId id)
        {
            return id == StepId.Anillo || id == StepId.Abrigar || id == StepId.Llamar || id == StepId.Preparar;
        }

        private static int IndexOf(StepId id)
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                if (Steps[i].id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool Pending(StepId id)
        {
            return Available(id) && !IsDone(id);
        }

        // Los que quedan para "Cuando puedas" (sin el principal).
        private readonly List<int> sideSteps = new List<int>();

        private void PickSide(int main)
        {
            sideSteps.Clear();
            if (main < 0)
            {
                return;
            }

            // Mientras se apaga el fuego, se abre el agua o se lleva la mano al
            // chorro no se distrae con lo demas (salvo el anillo ya pasado).
            StepId mainId = Steps[main].id;
            bool urgent = !CanWait(mainId) && mainId != StepId.Esperar && mainId != StepId.Cubrir;
            StepId[] order = IsDone(StepId.Esperar) ? OrderCooled : OrderCooling;
            foreach (StepId id in order)
            {
                int index = IndexOf(id);
                if (index == main || !CanWait(id) || !Pending(id))
                {
                    continue;
                }

                if (id == StepId.Anillo ? Deferred(id) : !urgent)
                {
                    sideSteps.Add(index);
                }
            }
        }

        private static string SideLabel(StepId id)
        {
            switch (id)
            {
                case StepId.Anillo:
                    return "quitarle el anillo (también bajo el agua)";
                case StepId.Abrigar:
                    return "abrigarla con la manta del sofá";
                case StepId.Llamar:
                    return EmergencyCall.Instance != null ? "llamar al 106 con el celular" : null;
                case StepId.Preparar:
                    return "sacar el film o la bolsa del cajón";
            }

            return null;
        }

        private string SideText()
        {
            List<string> labels = new List<string>();
            foreach (int i in sideSteps)
            {
                string label = SideLabel(Steps[i].id);
                if (label != null)
                {
                    labels.Add(label);
                }
            }

            return labels.Count > 0 ? "Cuando puedas: " + string.Join(" · ", labels) + "." : null;
        }

        private string SideSignature()
        {
            return string.Join(",", sideSteps);
        }

        private string shownSide = "";
        private bool warnedColdAway;
        private int reminders;

        // Frase del primer pendiente al lado que todavia dice lo correcto
        // (la de llamar dice "mientras se enfria").
        private string SideClip()
        {
            foreach (int i in sideSteps)
            {
                StepId id = Steps[i].id;
                if (id == StepId.Llamar && (EmergencyCall.Instance == null || IsDone(StepId.Esperar)))
                {
                    continue;
                }

                return Steps[i].clip;
            }

            return null;
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
                case StepId.Preparar:
                    // Si ya termino de enfriar, lo dice el paso de cubrir.
                    return coolingStarted && !IsDone(StepId.Esperar);
                case StepId.Llamar:
                    return EmergencyCall.Instance != null ? coolingStarted : IsDone(StepId.Cubrir);
            }

            return s != BurnSequenceManager.BurnState.Idle;
        }

        // El paso principal: el primero pendiente cuyo momento no paso; si
        // solo quedan pasos atrasados, el primero de ellos.
        private int PickStep()
        {
            StepId[] order = IsDone(StepId.Esperar) ? OrderCooled : OrderCooling;
            int late = -1;
            foreach (StepId id in order)
            {
                if (!Pending(id))
                {
                    continue;
                }

                if (!Deferred(id))
                {
                    return IndexOf(id);
                }

                if (late < 0)
                {
                    late = IndexOf(id);
                }
            }

            return late;
        }

        private void Update()
        {
            UpdateNarration();
            UpdateCard();
            UpdateHighlights();
            UpdateBlanketGuidance();

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

            // La tia se quejo del frio y el jugador no esta con ella (por
            // ejemplo, fue por la manta): tiene un margen para volver antes
            // de que saque la mano.
            if (PatientDialog.WaitingForPlayer && !warnedColdAway)
            {
                warnedColdAway = true;
                Correct("guia_frio_lejos", "Tu tía se queja de que el agua está muy fría. Vuelve con ella antes de que saque la mano.");
            }

            int step = PickStep();
            if (step >= 0 && Steps[step].id == StepId.Anillo && ringShownTime < 0f)
            {
                ringShownTime = Time.time;
            }

            PickSide(step);
            string side = SideSignature();
            bool isReturn = step >= 0 && Steps[step].id == StepId.LlevarAlAgua && coolingStarted;
            if (step != currentStep || isReturn != currentIsReturn)
            {
                currentStep = step;
                currentIsReturn = isReturn;
                shownSide = side;
                reminders = 0;
                ShowStep(step, isReturn, true);
            }
            else if (side != shownSide)
            {
                // Cambio lo que queda pendiente al lado: solo la tarjeta.
                shownSide = side;
                ShowStep(step, isReturn, false);
            }
            else if (step >= 0 && Time.time - stepShownTime > reminderSeconds && !Speaking)
            {
                // Recordatorio si no avanza. Uno de cada dos recuerda algo de
                // lo pendiente al lado (si tiene una frase que sirva ahora).
                reminders++;
                string sideClip = reminders % 2 == 0 ? SideClip() : null;
                ShowStep(step, isReturn, sideClip == null);
                if (sideClip != null)
                {
                    StopNarration();
                    Say(sideClip);
                }
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

        // La manta esta en otro cuarto (el sofa de la sala) y costaba
        // encontrarla. Modo guiado: flechas en el piso hasta ella y, con la
        // manta en la mano, de vuelta a la paciente. Modo evaluacion: sin
        // flechas; si el jugador no va a la sala, la tia comenta que tiene
        // frio y que hay una manta en el sofa (algo que diria cualquiera con
        // la mano en agua fria; no le dice que hacer).
        private void UpdateBlanketGuidance()
        {
            bool pending = coolingStarted && protocol != null && !protocol.Finished
                && !protocol.IsDone(BurnProtocol.Step.Abrigar)
                && burnSequence.CurrentState != BurnSequenceManager.BurnState.Success
                && !EmergencyCall.InCall && !MedicalDecisionPanel.IsOpen && !PauseMenu.Paused;
            if (pending && blanketItem == null)
            {
                blanketItem = FindObjectOfType<BlanketItem>();
            }

            if (!pending || blanketItem == null)
            {
                if (pathGuide != null)
                {
                    pathGuide.Hide();
                }

                return;
            }

            Grabbable grab = blanketItem.GetComponent<Grabbable>();
            bool held = grab != null && grab.IsHeld;
            Vector3 blanketPosition = blanketItem.transform.position;

            if (guided)
            {
                // Paso principal, o pendiente al lado y ya con la manta en la
                // mano (flechas de vuelta hacia ella).
                bool onStep = stepsActive && currentStep >= 0 && currentStep < Steps.Length
                    && ((Steps[currentStep].id == StepId.Abrigar && !currentIsReturn)
                        || (held && sideSteps.Contains(IndexOf(StepId.Abrigar))));
                if (!onStep)
                {
                    if (pathGuide != null)
                    {
                        pathGuide.Hide();
                    }

                    return;
                }

                if (pathGuide == null)
                {
                    pathGuide = FloorPathGuide.Ensure();
                }

                if (held && patient != null)
                {
                    pathGuide.SetTarget(patient.transform.position, 0.9f);
                }
                else
                {
                    pathGuide.SetTarget(blanketPosition, 0.9f);
                }

                return;
            }

            // Modo evaluacion.
            if (held || reactions == null || blanketHintsSaid >= 2)
            {
                return;
            }

            float due = blanketHintsSaid == 0 ? coolingStartTime + blanketHintDelay : nextBlanketHint;
            if (Time.time < due)
            {
                return;
            }

            // Ya esta en la sala o yendo: no hace falta.
            Transform eye = head != null ? head : VRPanelKit.Head();
            if (eye != null)
            {
                Vector3 d = eye.position - blanketPosition;
                d.y = 0f;
                if (d.magnitude < 2.5f)
                {
                    return;
                }
            }

            if (reactions.IsSpeaking || Speaking)
            {
                return;
            }

            reactions.Say("voz_pide_manta", false);
            blanketHintsSaid++;
            nextBlanketHint = Time.time + blanketHintRepeat;
        }

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
            if (!isReturn && info.id == StepId.Cubrir && cover < 0 && !SuppliesOut())
            {
                hint = "El film y la bolsa están en el cajón bajo el mesón junto al lavadero. " + hint;
            }

            string sideText = SideText();
            if (sideText != null)
            {
                hint = hint + "\n" + sideText;
            }

            // Con el orden flexible el numero es cuantos van hechos, y los
            // puntitos marcan cada paso hecho, el actual y los que faltan.
            bool[] doneSteps = new bool[Steps.Length];
            int doneCount = 0;
            for (int i = 0; i < Steps.Length; i++)
            {
                doneSteps[i] = IsDone(Steps[i].id);
                if (doneSteps[i])
                {
                    doneCount++;
                }
            }

            dotDone = doneSteps;
            dotCurrent = index;
            ShowCard($"Paso {Mathf.Min(doneCount + 1, Steps.Length)} de {Steps.Length}", title, hint);
            dotDone = null;
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
                    BlanketItem blanket = FindObjectOfType<BlanketItem>();
                    if (blanket != null)
                    {
                        Grabbable held = blanket.GetComponent<Grabbable>();
                        PatientBody body = held != null && held.IsHeld ? FindObjectOfType<PatientBody>() : null;
                        if (body != null && body.IsReady && body.Chest != null)
                        {
                            // Ya la lleva: donde soltarla (la espalda, bajo la nuca).
                            points.Add(body.Chest.position - body.TorsoForward * 0.16f + body.TorsoUp * 0.08f);
                        }
                        else
                        {
                            Renderer rr = blanket.GetComponentInChildren<Renderer>();
                            points.Add(rr != null ? rr.bounds.center : blanket.transform.position);
                        }

                        radius = 0.14f;
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
                case StepId.Preparar:
                    // Cajon cerrado: su tirador. Abierto: el film y la bolsa.
                    AddSupplies(points);
                    radius = 0.07f;
                    break;
                case StepId.Cubrir:
                    foreach (CoverItem item in FindObjectsOfType<CoverItem>())
                    {
                        // Lo que esta en un cajon cerrado: el tirador del cajon.
                        StorageFront closed = StorageFront.Holder(item.GetComponent<Rigidbody>());
                        if (closed != null && !closed.IsOpen)
                        {
                            AddOnce(points, closed.HandlePosition);
                            continue;
                        }

                        // Con la bolsa en la mano: la punta de sus dedos, por
                        // donde entra.
                        if (item.GetComponent<Grabbable>().IsHeld)
                        {
                            PatientBody body = FindObjectOfType<PatientBody>();
                            Transform burned = body != null && body.IsReady ? body.HandBone(true) : null;
                            if (burned != null)
                            {
                                points.Add(burned.position + FingerDirection(burned) * 0.17f);
                            }

                            continue;
                        }

                        Renderer rr = item.GetComponentInChildren<Renderer>();
                        if (rr != null && rr.enabled)
                        {
                            points.Add(rr.bounds.center);
                        }
                    }

                    foreach (FilmRoll roll in FindObjectsOfType<FilmRoll>())
                    {
                        StorageFront drawer = StorageFront.Holder(roll.GetComponent<Rigidbody>());
                        if (drawer != null && !drawer.IsOpen)
                        {
                            AddOnce(points, drawer.HandlePosition);
                        }
                        else if (!roll.GetComponent<Grabbable>().IsHeld)
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

        private static void AddOnce(List<Vector3> points, Vector3 p)
        {
            foreach (Vector3 q in points)
            {
                if ((q - p).sqrMagnitude < 1e-4f)
                {
                    return;
                }
            }

            points.Add(p);
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
                    if (detail == HomeRemedy.Remedy.Curita.ToString())
                    {
                        Correct("guia_err_curita", "Las curitas se pegan a la piel quemada. Cúbrela con film o una bolsa limpia.");
                    }
                    else
                    {
                        Correct("guia_err_remedio", "No le pongas nada en la quemadura: solo agua fría corriente.");
                    }

                    break;
                case BurnSequenceManager.BurnEvent.BurnedHandGrabbed:
                    Correct("guia_err_mano", "Sujétala del antebrazo: la mano quemada le duele.");
                    break;
                case BurnSequenceManager.BurnEvent.ClothOnBurn:
                    Correct("guia_err_pano", "El paño de cocina suelta pelusa. Usa film o una bolsa limpia.");
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
        // Estado de cada paso para los puntitos (solo mientras ShowStep arma
        // la tarjeta; null para el tutorial).
        private bool[] dotDone;
        private int dotCurrent = -1;

        private void UpdateDots(string title)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(title ?? "",
                @"(\d+)\s+de\s+(\d+)");
            int current = m.Success ? int.Parse(m.Groups[1].Value) : 0;
            int total = m.Success ? Mathf.Clamp(int.Parse(m.Groups[2].Value), 0, 12) : 0;
            bool[] stepDone = dotDone != null && dotDone.Length == total ? dotDone : null;

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
                bool now = stepDone != null ? i == dotCurrent : step == current;
                bool finished = stepDone != null ? stepDone[i] : step < current;
                float width = now ? DotSize * 2.6f : DotSize;
                dot.SetRect(new Vector2(x - width * 0.5f, y), new Vector2(width, DotSize));
                x -= width + gap;
                Color c = now ? new Color(0.45f, 0.86f, 1f, 1f)
                    : finished ? new Color(0.5f, 0.72f, 1f, 0.75f)
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
