using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        public float cardDistance = 0.7f;
        public float cardDrop = 0.3f;
        [Tooltip("Angulo de la mirada a partir del cual la tarjeta se reacomoda.")]
        public float cardFollowAngle = 28f;

        [Header("Tutorial")]
        public float grabTimeout = 45f;
        public float bringTimeout = 60f;
        public float handOverDistance = 0.4f;
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
            Esperar,
            Cubrir,
            Decidir
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
            new StepInfo { id = StepId.LlevarAlAgua, title = "Lleva su mano al chorro",
                hint = "Tómala del antebrazo, no de la mano quemada. Suéltala cerca del chorro y ella la pondrá bajo el agua.",
                clip = "guia_llevar" },
            new StepInfo { id = StepId.Anillo, title = "Quítale el anillo",
                hint = "Antes de que la mano se hinche. Deja tu mano libre sobre el anillo un momento.", clip = "guia_anillo" },
            new StepInfo { id = StepId.Abrigar, title = "Abrígala con la chompa",
                hint = "El agua fría también le enfría el cuerpo. Suelta la chompa sobre sus hombros.", clip = "guia_abrigo" },
            new StepInfo { id = StepId.Esperar, title = "Enfría 20 minutos",
                hint = "Mantén su mano bajo el agua hasta que el reloj complete las 20 marcas.", clip = "guia_esperar" },
            new StepInfo { id = StepId.Cubrir, title = "Cubre la quemadura",
                hint = "Con film o una gasa, sin apretar. Suéltalo sobre su mano.", clip = "guia_cubrir" },
            new StepInfo { id = StepId.Decidir, title = "¿Necesita atención médica?",
                hint = "Mira la quemadura: si tiene ampollas, debe verla un médico.", clip = "guia_decidir" }
        };

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
        private bool cardMoving;
        private float warningUntil;

        private readonly List<Renderer> highlights = new List<Renderer>();
        private Material highlightMaterial;

        private bool stepsActive;
        private int currentStep = -1;
        private bool currentIsReturn;
        private float stepShownTime;
        private bool coolingStarted;
        private readonly Dictionary<string, float> lastCorrection = new Dictionary<string, float>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
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

            if (askMode)
            {
                yield return ChooseMode();
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
            VRPanelKit.Background(root, Vector2.zero, new Vector2(0.62f, 0.42f), "Panel_Fondo",
                new Color(0.07f, 0.1f, 0.16f), 0.001f);
            VRPanelKit.Text(root, "Primeros auxilios: quemadura en la cocina", new Vector2(0f, 0.18f), 0.03f,
                Color.white, TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(root, VRPanelKit.Wrap("Aviso: esta práctica incluye gritos de dolor y una quemadura visible. " +
                    "Puedes quitarte el visor cuando quieras.", 52), new Vector2(0f, 0.125f), 0.019f,
                new Color(1f, 0.82f, 0.55f), TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(root, "Toca un botón con el dedo para elegir:", new Vector2(0f, 0.045f), 0.02f,
                new Color(0.8f, 0.85f, 0.92f), TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Button(root, "Modo guiado", new Vector2(-0.145f, -0.06f), new Vector2(0.26f, 0.1f),
                new Color(0.16f, 0.42f, 0.78f), () => { guided = true; chosen = true; });
            VRPanelKit.Button(root, "Modo evaluación", new Vector2(0.145f, -0.06f), new Vector2(0.26f, 0.1f),
                new Color(0.45f, 0.3f, 0.62f), () => { guided = false; chosen = true; });
            VRPanelKit.Text(root, VRPanelKit.Wrap("Guiado: te indica cada paso. Evaluación: lo haces solo y se mide.", 56),
                new Vector2(0f, -0.13f), 0.017f, new Color(0.7f, 0.75f, 0.82f), TextAnchor.UpperCenter,
                TextAlignment.Center);

            Say("guia_bienvenida");
            Highlight(root.TransformPoint(new Vector3(-0.145f, -0.06f, -0.01f)), 0f);

            while (!chosen)
            {
                yield return null;
            }

            ClearHighlights();
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

                    yield return null;
                }

                ClearHighlights();
                HideCard();
                if (delivered && arm != null)
                {
                    arm.ReceiveObject(mug, PutDownSpot());
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
            PropKit.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.093f, 0f), new Vector3(0.07f, 0.002f, 0.07f),
                "Mango", new Color(0.25f, 0.15f, 0.08f));
            PropKit.MeshPart(t, PropKit.Torus("AsaTaza", 0.022f, 0.005f), new Vector3(0.045f, 0.05f, 0f),
                Quaternion.Euler(90f, 0f, 0f), "Plato", new Color(0.93f, 0.93f, 0.9f));
            mug.GetComponent<Grabbable>().SetHome();
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

            return new Vector3(left.x, counter, left.z);
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
                case StepId.Decidir:
                    return protocol == null || protocol.Finished;
            }

            return true;
        }

        private bool Available(StepId id)
        {
            BurnSequenceManager.BurnState s = burnSequence.CurrentState;
            switch (id)
            {
                case StepId.Anillo:
                case StepId.Abrigar:
                case StepId.Esperar:
                    return coolingStarted;
                case StepId.Cubrir:
                    return IsDone(StepId.Esperar);
                case StepId.Decidir:
                    return IsDone(StepId.Cubrir);
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

            if (knob == null)
            {
                knob = FindObjectOfType<StoveKnob>();
            }

            if (ring == null && patient != null)
            {
                ring = patient.GetComponent<PatientRing>();
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
            }
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
            string title = isReturn ? "Vuelve a poner su mano bajo el agua" : info.title;
            string hint = isReturn ? "El reloj se pausó. Tómala del antebrazo y llévala al chorro." : info.hint;
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
                    Transform r = ring != null ? FindRing() : null;
                    if (r != null)
                    {
                        points.Add(r.position);
                        radius = 0.03f;
                    }

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
                    Correct("guia_err_pano", "El paño de cocina suelta pelusa. Usa film o gasa.");
                    break;
                case BurnSequenceManager.BurnEvent.CoveredTooEarly:
                    Correct("guia_err_temprano", "Todavía no: primero termina de enfriar la quemadura.");
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
        private const float TextLine = 0.024f;
        private const float HintLine = 0.017f;

        private void CreateCard()
        {
            GameObject go = new GameObject("TarjetaGuia");
            go.AddComponent<NoAutoCollider>();
            card = go.transform;
            VRPanelKit.Background(card, new Vector2(0f, 0f), new Vector2(0.44f, CardHeight), "Panel_Fondo",
                new Color(0.07f, 0.1f, 0.16f), 0.001f);
            VRPanelKit.Background(card, new Vector2(-0.214f, 0f), new Vector2(0.012f, CardHeight), "Panel_Boton",
                new Color(0.25f, 0.6f, 1f), 0f);
            float top = CardHeight * 0.5f - 0.016f;
            cardTitle = VRPanelKit.Text(card, "", new Vector2(-0.195f, top), 0.016f, new Color(0.55f, 0.78f, 1f));
            cardText = VRPanelKit.Text(card, "", new Vector2(-0.195f, top - 0.026f), TextLine, Color.white);
            cardHint = VRPanelKit.Text(card, "", new Vector2(-0.195f, 0f), HintLine, new Color(0.8f, 0.85f, 0.92f));
            cardWarning = VRPanelKit.Text(card, "", new Vector2(-0.195f, 0f), HintLine, new Color(1f, 0.6f, 0.45f));
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
            float y = cardText.transform.localPosition.y - Lines(cardText.text) * TextLine * 1.2f - 0.008f;
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
            cardText.text = VRPanelKit.Wrap(text ?? "", 30);
            cardHint.text = VRPanelKit.Wrap(hint ?? "", 44);
            LayoutCard();
            card.gameObject.SetActive(true);
            if (wasHidden)
            {
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
            if (card == null || !card.gameObject.activeSelf)
            {
                return;
            }

            if (cardWarning != null && cardWarning.text.Length > 0 && Time.time > warningUntil)
            {
                cardWarning.text = "";
            }

            PlaceCard(false);
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
            Vector3 wanted = head.position + forward * cardDistance + Vector3.down * cardDrop;
            Vector3 toCard = Vector3.ProjectOnPlane(card.position - head.position, Vector3.up);
            float angle = toCard.sqrMagnitude > 1e-4f ? Vector3.Angle(toCard, forward) : 180f;
            float distanceError = Mathf.Abs(toCard.magnitude - cardDistance);

            if (snap)
            {
                card.position = wanted;
                cardVelocity = Vector3.zero;
            }
            else
            {
                if (angle > cardFollowAngle || distanceError > 0.3f)
                {
                    cardMoving = true;
                }

                if (cardMoving)
                {
                    card.position = Vector3.SmoothDamp(card.position, wanted, ref cardVelocity, 0.35f);
                    if ((card.position - wanted).sqrMagnitude < 0.0004f)
                    {
                        cardMoving = false;
                    }
                }
                else
                {
                    // Sigue la altura de la cabeza (si se agacha, por ejemplo).
                    Vector3 p = card.position;
                    p.y = Mathf.Lerp(p.y, wanted.y, 1f - Mathf.Exp(-3f * Time.deltaTime));
                    card.position = p;
                }
            }

            Vector3 look = card.position - head.position;
            look.y *= 0.5f;
            if (look.sqrMagnitude > 1e-4f)
            {
                card.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
        }

        // ---------------- brillo ----------------

        private Renderer Highlight(Vector3 position, float radius)
        {
            Renderer r = IndicatorUtil.CreateSphere("BrilloGuia", null, position, Mathf.Max(0.02f, radius),
                highlightMaterial, new Color(0.3f, 0.85f, 1f, 0.4f));
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
            Color color = new Color(0.3f, 0.85f, 1f, Mathf.Lerp(0.18f, 0.5f, pulse));
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
