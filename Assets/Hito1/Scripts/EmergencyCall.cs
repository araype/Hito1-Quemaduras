using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Llamada al 106 (SAMU) con un celular que esta en el meson. Reemplaza al
    // panel de decision medica: en vez de dos botones al final, el jugador
    // pide ayuda como en la vida real, idealmente mientras la mano se enfria
    // (antes ese minuto quedaba sin nada que hacer).
    //   1. Despues del accidente, tomar el celular: en su pantalla aparece el
    //      boton "Llamar 106", que se toca con la otra mano (dedo y gatillo o
    //      pinza, o el rayo). Llevarlo a la oreja tambien llama. Suena el
    //      timbre y contesta el operador en altavoz: el celular se puede
    //      dejar en el meson y seguir atendiendola mientras se habla (antes
    //      habia que tenerlo en la oreja y en el simulador no se podia).
    //   2. El operador pregunta que paso, si la mano ya esta bajo agua fria y
    //      si la quemadura tiene ampollas. Las respuestas son botones frente al
    //      jugador (se tocan con la mano libre). Para contestar bien hay que
    //      mirar la quemadura: describirla mal cuenta como error.
    //   3. Recomienda segun lo que le dijeron (el operador no ve la cocina) y
    //      pregunta si pueden seguir esa recomendacion: el jugador decide si
    //      la lleva al centro de salud o la cuida en casa (BurnProtocol.Decide).
    // El panel queda abajo y a un costado (no tapa la mano) y acompana al
    // jugador si se da vuelta. Si se aleja mas de 'speakerDistance' del
    // celular, la llamada queda en espera hasta que vuelva. Mientras dura la
    // llamada la paciente no se queja ni saca la mano del agua.
    // Las frases del operador son op_*.wav de Resources/Hito1Audio; si falta
    // alguna, igual se lee en el panel (subtitulo).
    // Va en el objeto de BurnSequenceManager (lo instala Hito1RuntimeBootstrap).
    public class EmergencyCall : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Distancia del celular a la cabeza para que cuente como llevado a la oreja (m).")]
        public float earDistance = 0.25f;
        public float operatorVolume = 1f;
        [Tooltip("Segundos de timbre antes de que conteste el operador.")]
        public float ringSeconds = 3.5f;
        [Tooltip("En altavoz: distancia de la cabeza al celular a partir de la cual ya no se oye y la llamada queda en espera (m).")]
        public float speakerDistance = 3f;

        public static EmergencyCall Instance { get; private set; }

        // True mientras dura la llamada (la guia oculta su tarjeta y calla).
        public static bool InCall
        {
            get { return Instance != null && Instance.inCall; }
        }

        public Transform Phone
        {
            get { return phone != null ? phone.transform : null; }
        }

        // Ya llamo y decidio que hacer.
        public bool Decided { get; private set; }

        private Rigidbody phone;
        private AudioSource speaker;
        private Transform head;
        private bool started;
        private bool inCall;
        private Transform panel;
        private int answer = -1;

        // Llamada en espera: la llamada va en altavoz, asi que soltar el
        // celular no la corta; solo si el jugador se aleja demasiado (otro
        // cuarto) el operador se pausa y un aviso pide volver junto a el.
        [Tooltip("Segundos lejos del celular antes de poner la llamada en espera.")]
        public float holdAfterSeconds = 0.8f;
        private bool onHold;
        private float awayTime;
        private string lastText;
        private string[] lastOptions;
        private Renderer holdMarker;
        private float nextHoldBeep;

        // True mientras la llamada esta en espera.
        public static bool OnHold
        {
            get { return Instance != null && Instance.onHold; }
        }

        private static readonly Color Blue = new Color(0.22f, 0.48f, 0.95f);
        private static readonly Color Green = new Color(0.2f, 0.62f, 0.42f);

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
        }

        private IEnumerator Start()
        {
            if (burnSequence == null)
            {
                burnSequence = GetComponent<BurnSequenceManager>();
            }

            // Despues de BurnSceneProps (que ubica los objetos en su Start).
            yield return null;
            yield return null;
            phone = BuildPhone();
            if (phone == null)
            {
                Debug.LogWarning("[EmergencyCall] No hay lugar para el celular: se usa el panel de decision medica.");
                enabled = false;
                Instance = null;
            }
        }

        private void Update()
        {
            UpdateScreen();
            UpdateCallButton();
            if (inCall)
            {
                UpdateHold();
                if (!onHold)
                {
                    FollowCallPanel();
                }
            }

            // Si la tia esta preguntando algo (PatientDialog), la llamada
            // empieza cuando se le responde: los dos paneles se tapaban.
            if (started || phone == null || burnSequence == null
                || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle || PatientDialog.IsOpen)
            {
                return;
            }

            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            // Con una sola mano: el boton A o X del control que lo sostiene es
            // el pulgar tocando la pantalla (en el simulador mover las dos
            // manos a la vez cuesta mucho).
            if (SimpleGrab.IsHeldObject(phone) && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.Touch))
            {
                StartCall();
                return;
            }

            // Llevarlo a la oreja tambien llama (en el visor sale natural).
            if (head != null && SimpleGrab.IsHeldObject(phone)
                && Vector3.Distance(phone.worldCenterOfMass, head.position) < earDistance)
            {
                StartCall();
            }
        }

        // Lo llama el boton de la pantalla o llevarlo a la oreja.
        private void StartCall()
        {
            if (started || phone == null || burnSequence == null
                || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle || PatientDialog.IsOpen)
            {
                return;
            }

            started = true;
            UpdateCallButton();
            StartCoroutine(Call());
        }

        // El boton "Llamar 106" se ve en la pantalla solo con el celular en la
        // mano (asi apretar junto al celular en el meson lo agarra, no llama)
        // y despues del accidente.
        private void UpdateCallButton()
        {
            if (callButton == null)
            {
                return;
            }

            bool show = !started && phone != null && burnSequence != null
                && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle
                && SimpleGrab.IsHeldObject(phone) && !PatientDialog.IsOpen;
            if (callButton.activeSelf != show)
            {
                callButton.SetActive(show);
            }
        }

        // ---------------- llamada ----------------

        private IEnumerator Call()
        {
            inCall = true;
            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            PlacePanel();
            burnSequence.Report(BurnSequenceManager.BurnEvent.EmergencyCalled);
            // Altavoz: se oye bien a un par de metros.
            speaker.minDistance = 1.2f;
            speaker.clip = Ringback();
            speaker.loop = false;
            speaker.Play();
            yield return WaitCall(ringSeconds);
            speaker.Stop();

            BurnProtocol protocol = BurnProtocol.Instance;
            yield return Ask("op_saludo", "SAMU, 106. ¿Cuál es su emergencia?",
                "Mi tía se quemó la mano con agua hirviendo");

            // El operador no ve la cocina: responde a lo que le dicen. Decir
            // que esta bajo el agua cuando no lo esta es un error.
            yield return Ask("op_agua", "Entiendo. ¿Ya tiene la mano bajo agua fría corriente?",
                "Sí, está bajo el chorro", "Todavía no");
            bool saysWater = answer == 0;
            if (saysWater && !UnderWater())
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.WrongCallInfo, "SinAgua",
                    BurnSequenceManager.Severity.Error);
            }

            if (saysWater)
            {
                yield return Say("op_agua_si", "Muy bien. Manténgala bajo el agua unos veinte minutos.");
            }
            else
            {
                yield return Say("op_agua_no", "Póngala ya bajo agua fría corriente, sin hielo, y manténgala ahí.");
            }

            // Para responder hay que mirar la quemadura: ella se la muestra
            // (si esta bajo el agua, solo voltea; no la saca del chorro).
            BurnPatientReactions reactions = FindObjectOfType<BurnPatientReactions>();
            if (reactions != null)
            {
                reactions.Say("voz_aqui_mirala", true);
            }

            PatientBurnArm arm = FindObjectOfType<PatientBurnArm>();
            if (arm != null)
            {
                arm.ShowHand(6f);
            }

            // Que termine de hablar ella antes de que pregunte el operador.
            AudioClip showLine = Hito1FX.Clip("voz_aqui_mirala");
            yield return WaitCall(showLine != null ? showLine.length + 0.2f : 0.6f);

            yield return Ask("op_ampollas", "Mire bien la quemadura. ¿Tiene ampollas?",
                "Sí, tiene ampollas", "No, solo está roja");
            bool saysBlisters = answer == 0;
            // Lo correcto es lo que se ve: antes de que salgan, "no" no es error.
            BurnInjury injury = BurnInjury.Instance;
            bool hasBlisters = injury != null ? injury.BlistersVisible : protocol == null || protocol.NeedsMedicalCare;
            if (saysBlisters != hasBlisters)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.WrongCallInfo,
                    hasBlisters ? "TieneAmpollas" : "SinAmpollas", BurnSequenceManager.Severity.Error);
            }

            // Recomienda segun lo que le dijeron, y la pregunta final es si
            // pueden seguir esa recomendacion (decir que no es la decision).
            bool advisedCenter = saysBlisters;
            bool goToHealthCenter;
            if (advisedCenter)
            {
                yield return Say("op_centro", "Con ampollas en la mano, debe verla un médico. Cuando termine de " +
                    "enfriarla, cúbrala sin apretar con film de cocina o una bolsa plástica limpia.");
                yield return Ask("op_decidir_centro", "¿Puede llevarla al centro de salud?",
                    "Sí, la llevo", "Prefiero cuidarla en casa");
                goToHealthCenter = answer == 0;
            }
            else
            {
                yield return Say("op_casa", "Si solo está roja y es pequeña, puede cuidarla en casa. Si salen " +
                    "ampollas o empeora, llévela al centro de salud.");
                yield return Ask("op_decidir_casa", "¿Podrá cuidarla en casa?",
                    "Sí, la cuido en casa", "Prefiero llevarla al centro de salud");
                goToHealthCenter = answer == 1;
            }

            if (protocol != null)
            {
                protocol.Decide(goToHealthCenter, advisedCenter);
            }

            Decided = true;
            yield return Say("op_cierre", "De acuerdo. Si empeora o tiene dudas, vuelva a llamar. Cuídela mucho.");
            ClosePanel();
            onHold = false;
            if (holdMarker != null)
            {
                Destroy(holdMarker.gameObject);
                holdMarker = null;
            }

            Hito1FX.PlayAt("perilla_clic", phone.position, 0.5f);
            inCall = false;
        }

        // Corta la llamada sin terminarla (la practica termino desde el menu).
        public void HangUp()
        {
            if (!inCall)
            {
                return;
            }

            StopAllCoroutines();
            ClosePanel();
            if (speaker != null)
            {
                speaker.Stop();
            }

            if (holdMarker != null)
            {
                Destroy(holdMarker.gameObject);
                holdMarker = null;
            }

            onHold = false;
            inCall = false;
        }

        // ---------------- llamada en espera ----------------

        // El jugador esta a menos de 'distance' del celular (en la mano
        // siempre cuenta).
        private bool PhoneInEarshot(float distance)
        {
            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            if (head == null || phone == null)
            {
                return true;
            }

            return SimpleGrab.IsHeldObject(phone)
                || Vector3.Distance(phone.worldCenterOfMass, head.position) < distance;
        }

        private void UpdateHold()
        {
            if (!onHold)
            {
                awayTime = PhoneInEarshot(speakerDistance) ? 0f : awayTime + Time.deltaTime;
                if (awayTime > holdAfterSeconds)
                {
                    EnterHold();
                }

                return;
            }

            // Algo mas cerca para volver (no entra y sale en el borde).
            if (PhoneInEarshot(speakerDistance * 0.8f))
            {
                ExitHold();
                return;
            }

            FollowHoldPanel();

            if (holdMarker != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                IndicatorUtil.SetWorldDiameter(holdMarker.transform, Mathf.Lerp(0.12f, 0.17f, pulse));
                IndicatorUtil.SetColor(holdMarker, new Color(0.3f, 0.85f, 1f, Mathf.Lerp(0.2f, 0.5f, pulse)));
            }

            // Un tono suave cada tanto desde el celular, para ubicarlo.
            if (Time.time >= nextHoldBeep && phone != null)
            {
                nextHoldBeep = Time.time + 2.5f;
                Hito1FX.PlayAt("perilla_clic", phone.position, 0.5f, 0.02f);
            }
        }

        private void EnterHold()
        {
            onHold = true;
            awayTime = 0f;
            if (speaker != null)
            {
                speaker.Pause();
            }

            // Aviso al frente (el panel de la llamada queda al costado).
            ClosePanel();
            GameObject root = new GameObject("PanelLlamada");
            root.AddComponent<NoAutoCollider>();
            HoldPanelPose(out Vector3 position, out Quaternion rotation);
            root.transform.SetPositionAndRotation(position, rotation);
            root.AddComponent<OverlayPanel>();
            panel = root.transform;
            const float width = 0.5f, height = 0.15f;
            const float left = -width * 0.5f + 0.025f;
            VRPanelKit.CardPanel(panel, Vector2.zero, new Vector2(width, height));
            VRPanelKit.Text(panel, "Llamada al 106  ·  En espera", new Vector2(left, height * 0.5f - 0.025f), 0.016f,
                VRPanelKit.CardLabelColor);
            VRPanelKit.BoldText(panel, "Desde aquí no oyes el altavoz:\nvuelve junto al celular",
                new Vector2(left, height * 0.5f - 0.05f), 0.022f, Color.white);

            if (holdMarker == null && phone != null)
            {
                WaterTap tap = FindObjectOfType<WaterTap>();
                holdMarker = IndicatorUtil.CreateSphere("BrilloCelular", phone.transform, phone.worldCenterOfMass, 0.07f,
                    tap != null ? tap.indicatorMaterial : null, new Color(0.3f, 0.85f, 1f, 0.35f));
            }

            nextHoldBeep = Time.time + 0.5f;
        }

        // El aviso de espera va frente a la vista, un poco abajo.
        private void HoldPanelPose(out Vector3 position, out Quaternion rotation)
        {
            Transform eye = head != null ? head : VRPanelKit.Head();
            Vector3 eyePos = eye != null ? eye.position : transform.position + Vector3.up * 1.6f;
            Vector3 forward = eye != null ? Vector3.ProjectOnPlane(eye.forward, Vector3.up) : Vector3.forward;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            position = eyePos + forward * 0.6f + Vector3.down * 0.12f;
            rotation = Quaternion.LookRotation(position - eyePos, Vector3.up);
        }

        // Mientras la llamada esta en espera el aviso sigue la vista: si el
        // celular se cayo y el jugador gira para buscarlo, el aviso quedaba
        // atras y no se veia.
        private void FollowHoldPanel()
        {
            if (panel == null)
            {
                return;
            }

            HoldPanelPose(out Vector3 position, out Quaternion rotation);
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
            panel.SetPositionAndRotation(Vector3.Lerp(panel.position, position, k),
                Quaternion.Slerp(panel.rotation, rotation, k));
        }

        private void ExitHold()
        {
            onHold = false;
            awayTime = 0f;
            if (holdMarker != null)
            {
                Destroy(holdMarker.gameObject);
                holdMarker = null;
            }

            if (speaker != null)
            {
                speaker.UnPause();
            }

            // Vuelve el panel de la llamada tal como estaba (mientras timbra
            // todavia no hay panel).
            if (lastText != null)
            {
                ShowPanel(lastText, lastOptions);
            }
            else
            {
                ClosePanel();
            }
        }

        // Espera que no corre mientras la llamada esta en espera.
        private IEnumerator WaitCall(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (!onHold)
                {
                    t += Time.deltaTime;
                }

                yield return null;
            }
        }

        private bool UnderWater()
        {
            BurnSequenceManager.BurnState s = burnSequence.CurrentState;
            return s == BurnSequenceManager.BurnState.Cooling || s == BurnSequenceManager.BurnState.Cooled
                || s == BurnSequenceManager.BurnState.Covered;
        }

        // El operador habla y espera a que termine (o un tiempo de lectura).
        private IEnumerator Say(string clipName, string text)
        {
            ShowPanel(text, null);
            float wait = Speak(clipName, text);
            yield return WaitCall(wait);
        }

        // Pregunta y espera la respuesta (queda en 'answer').
        private IEnumerator Ask(string clipName, string text, params string[] options)
        {
            answer = -1;
            ShowPanel(text, options);
            Speak(clipName, text);
            while (answer < 0)
            {
                yield return null;
            }

            speaker.Stop();
            yield return WaitCall(0.4f);
        }

        private float Speak(string clipName, string text)
        {
            AudioClip clip = Hito1FX.Clip(clipName);
            if (clip == null)
            {
                // Sin la voz grabada: tiempo para leer el subtitulo.
                return Mathf.Max(2.5f, text.Length * 0.06f);
            }

            speaker.Stop();
            speaker.loop = false;
            speaker.clip = clip;
            speaker.Play();
            return clip.length + 0.3f;
        }

        // ---------------- panel ----------------

        private void ShowPanel(string text, string[] options)
        {
            lastText = text;
            lastOptions = options;
            if (onHold)
            {
                // En espera se queda el aviso; al volver se muestra este panel.
                return;
            }

            ClosePanel();
            GameObject root = new GameObject("PanelLlamada");
            root.AddComponent<NoAutoCollider>();
            root.transform.SetPositionAndRotation(panelPosition, panelRotation);
            root.AddComponent<OverlayPanel>();
            panel = root.transform;
            int lines = VRPanelKit.Wrap(text, 38).Split('\n').Length;
            float height = 0.1f + lines * 0.03f + (options != null ? 0.1f : 0f);
            float top = height * 0.5f;
            const float width = 0.56f;
            const float left = -width * 0.5f + 0.025f;
            VRPanelKit.CardPanel(panel, Vector2.zero, new Vector2(width, height));
            VRPanelKit.Text(panel, "Llamada al 106  ·  SAMU", new Vector2(left, top - 0.025f), 0.016f,
                VRPanelKit.CardLabelColor);
            VRPanelKit.BoldText(panel, VRPanelKit.Wrap(text, 38), new Vector2(left, top - 0.05f), 0.024f, Color.white);

            if (options == null)
            {
                return;
            }

            float y = -height * 0.5f + 0.06f;
            if (options.Length == 1)
            {
                VRPanelKit.RoundedButton(panel, options[0], new Vector2(0f, y), new Vector2(0.5f, 0.075f), Blue,
                    () => answer = 0);
                return;
            }

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                float x = (i == 0 ? -1f : 1f) * 0.13f;
                VRPanelKit.RoundedButton(panel, options[i], new Vector2(x, y), new Vector2(0.245f, 0.08f),
                    i == 0 ? Blue : Green, () => answer = index);
            }
        }

        // Donde va el panel durante toda la llamada: abajo y a un costado de la
        // vista, del lado contrario a la paciente. Antes quedaba al frente y
        // tapaba la mano justo cuando el operador pedia mirarla.
        private Vector3 panelPosition;
        private Quaternion panelRotation;

        private void PlacePanel()
        {
            Transform eye = head != null ? head : VRPanelKit.Head();
            Vector3 eyePos = eye != null ? eye.position : transform.position + Vector3.up * 1.6f;
            Vector3 forward = eye != null ? Vector3.ProjectOnPlane(eye.forward, Vector3.up) : Vector3.forward;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            PatientBody patient = FindObjectOfType<PatientBody>();
            float side = patient != null && Vector3.Dot(patient.transform.position - eyePos, right) > 0f ? -1f : 1f;
            Vector3 dir = Quaternion.AngleAxis(32f * side, Vector3.up) * forward;
            panelPosition = eyePos + dir * 0.45f + Vector3.down * 0.3f;
            panelRotation = Quaternion.LookRotation(panelPosition - eyePos, Vector3.up);
        }

        // El panel de la llamada se queda quieto mientras se ve (para tocar
        // los botones sin que se mueva), pero si el jugador se da vuelta o
        // camina con el altavoz, se reacomoda a su costado.
        private bool panelGliding;

        private void FollowCallPanel()
        {
            if (panel == null || head == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            Vector3 toPanel = Vector3.ProjectOnPlane(panel.position - head.position, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f || toPanel.sqrMagnitude < 1e-4f)
            {
                return;
            }

            if (!panelGliding && (Vector3.Angle(forward, toPanel) > 65f || toPanel.magnitude > 0.9f))
            {
                panelGliding = true;
            }

            if (!panelGliding)
            {
                return;
            }

            PlacePanel();
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            panel.SetPositionAndRotation(Vector3.Lerp(panel.position, panelPosition, k),
                Quaternion.Slerp(panel.rotation, panelRotation, k));
            if (Vector3.Distance(panel.position, panelPosition) < 0.02f)
            {
                panelGliding = false;
            }
        }

        private void ClosePanel()
        {
            if (panel != null)
            {
                Destroy(panel.gameObject);
                panel = null;
            }
        }

        // ---------------- celular ----------------

        // En modo evaluacion no hay brillo de la guia: la pantalla parpadea
        // unos segundos despues del accidente (y cada tanto) como si llegara
        // un aviso, para que el jugador sepa que el celular esta ahi.
        private void UpdateScreen()
        {
            if (screen == null || burnSequence == null)
            {
                return;
            }

            if (inCall)
            {
                // En llamada: pantalla verde (altavoz); azul en espera.
                IndicatorUtil.SetColor(screen, onHold ? ScreenIdle : Green);
                return;
            }

            bool hint = !started && BurnGuide.Instance != null && !BurnGuide.Instance.Guided
                && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle
                && !SimpleGrab.IsHeldObject(phone);
            float since = Time.time - burnSequence.BurnTime;
            bool blinking = hint && since > 8f && since % 30f > 8f && since % 30f < 12f;
            float k = blinking ? 0.5f + 0.5f * Mathf.Sin(Time.time * 9f) : 0f;
            IndicatorUtil.SetColor(screen, Color.Lerp(ScreenIdle, new Color(0.75f, 0.9f, 1f), k));
        }

        private static readonly Color ScreenIdle = new Color(0.16f, 0.42f, 0.78f);
        private Renderer screen;
        private GameObject callButton;

        private Rigidbody BuildPhone()
        {
            WaterTap tap = FindObjectOfType<WaterTap>();
            if (tap == null)
            {
                return null;
            }

            // En el meson, cerca del lavadero y lejos de la cocina.
            float counter = BurnStaging.MeasureCounter(tap);
            BurnStaging.StoveArea(out Vector3 stove, out float stoveRadius);
            List<Vector3> taken = new List<Vector3>();
            foreach (Rigidbody rb in FindObjectsOfType<Rigidbody>())
            {
                taken.Add(rb.position);
            }

            if (!PropKit.FindSurfaceSpot(tap.transform.position, counter, 0.35f, 1.1f, 0.09f, taken, stove,
                stoveRadius, out Vector3 spot))
            {
                return null;
            }

            GameObject go = PropKit.Grabbable("Celular", spot + Vector3.up * 0.002f,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(0.075f, 0.012f, 0.155f), 0.2f);
            Transform t = go.transform;
            PropKit.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.006f, 0f), new Vector3(0.075f, 0.011f, 0.155f),
                "Mango", new Color(0.06f, 0.06f, 0.065f));
            screen = PropKit.Part(t, PrimitiveType.Cube, new Vector3(0f, 0.0118f, 0f),
                new Vector3(0.066f, 0.001f, 0.14f), "Panel_Boton", ScreenIdle);
            go.GetComponent<Grabbable>().SetHome();

            // Boton de la pantalla. Los paneles miran hacia -Z local: girado
            // 90 grados en X, su frente queda hacia arriba (la pantalla).
            GameObject face = new GameObject("PantallaCelular");
            face.AddComponent<NoAutoCollider>();
            face.transform.SetParent(t, false);
            face.transform.localPosition = new Vector3(0f, 0.0128f, 0.02f);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            VRPanelKit.RoundedButton(face.transform, "Llamar 106", Vector2.zero, new Vector2(0.058f, 0.032f), Green,
                StartCall);
            callButton = face;
            face.SetActive(false);

            speaker = go.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 0.85f;
            speaker.minDistance = 0.3f;
            speaker.volume = operatorVolume;
            // El operador se oye desde el celular (al llevarlo a la oreja,
            // por ese lado).
            Hito1FX.Spatialize(speaker);
            Debug.Log("[EmergencyCall] Celular en " + spot.ToString("F2"));
            return go.GetComponent<Rigidbody>();
        }

        // Tono de llamada (425 Hz, como en Peru): 1 s sonando y 1,5 s en
        // silencio, sintetizado aqui (no hay archivo).
        private static AudioClip ringback;

        private static AudioClip Ringback()
        {
            if (ringback != null)
            {
                return ringback;
            }

            const int rate = 22050;
            int length = rate * 5;
            float[] data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float time = (float)i / rate;
                float inCycle = time % 2.5f;
                float envelope = inCycle < 1f ? Mathf.Clamp01(Mathf.Min(inCycle, 1f - inCycle) / 0.02f) : 0f;
                data[i] = Mathf.Sin(2f * Mathf.PI * 425f * time) * 0.25f * envelope;
            }

            ringback = AudioClip.Create("timbre_106", length, 1, rate, false);
            ringback.SetData(data, 0);
            return ringback;
        }
    }
}
