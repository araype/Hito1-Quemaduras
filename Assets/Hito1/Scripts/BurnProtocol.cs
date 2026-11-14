using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SojaExiles
{
    // Lleva la cuenta del protocolo de primeros auxilios ante la quemadura a
    // partir de lo que avisa BurnSequenceManager:
    //   pasos (apagar el fuego, agua fria, enfriar 20 min, anillo, abrigar,
    //   cubrir, decision medica, pedir ayuda), errores con su explicacion, tiempo de
    //   primera accion, tiempo de enfriamiento y puntaje.
    // El enfriamiento de 20 minutos se representa en 'coolingSeconds' reales
    // (el reloj de pared, CoolingClock). Si la mano sale del agua, el reloj se
    // pausa y cuenta como error.
    // La decision de ir al centro de salud se toma en la llamada al 106
    // (EmergencyCall), en cualquier momento despues del accidente. La
    // practica termina cuando la quemadura esta cubierta, ya se llamo y esta
    // abrigada: revisa los pasos que faltaron, termina (Success), muestra el
    // resumen (BurnSummaryPanel) y guarda la sesion en un JSON. Si se cubre
    // sin haber llamado o sin abrigarla, espera un rato ('callGraceSeconds',
    // 'blanketGraceSeconds') y, si no llega, termina con ese paso faltante. Sin celular en la escena, al cubrir
    // pregunta con el panel de antes (MedicalDecisionPanel).
    // Va en el objeto de BurnSequenceManager.
    public class BurnProtocol : MonoBehaviour
    {
        public enum Step
        {
            ApagarFuego,
            AguaFria,
            Enfriar,
            Anillo,
            Abrigar,
            Cubrir,
            DecisionMedica
        }

        public enum Act
        {
            Planteamiento,
            Desarrollo,
            Climax,
            Resolucion
        }

        public class Mistake
        {
            public BurnSequenceManager.BurnEvent burnEvent;
            public string detail;
            public BurnSequenceManager.Severity severity;
            public float time;
            public string explanation;
            public Act act;
        }

        public BurnSequenceManager burnSequence;
        [Tooltip("Segundos reales que representan los 20 minutos de enfriamiento.")]
        public float coolingSeconds = 90f;
        public float simulatedMinutes = 20f;
        [Tooltip("Segundos de cierre (la tia agradece) entre completar todo y el resumen.")]
        public float decisionDelay = 5f;
        [Tooltip("Si se cubre sin haber llamado al 106: segundos que espera la llamada (modo guiado y evaluacion).")]
        public float callGraceSecondsGuided = 90f;
        public float callGraceSeconds = 40f;
        [Tooltip("Ya se llamo, se cubrio y falta abrigarla: segundos que espera la manta (modo guiado y evaluacion).")]
        public float blanketGraceSecondsGuided = 60f;
        public float blanketGraceSeconds = 20f;
        [Tooltip("Modo evaluacion: segundos reales desde el accidente hasta que la practica termina sola (0 sin limite). En modo guiado no hay limite: para no quedarse atrapado esta 'Terminar y ver resumen' en el menu de pausa.")]
        public float evaluationTimeLimit = 600f;

        // Por que termino antes de completar todo: "Menu", "Tiempo" o null.
        public string EndedEarly { get; private set; }
        public bool saveSessionLog = true;

        [Header("Puntaje")]
        public int errorPenalty = 10;
        public int criticalPenalty = 25;

        public static readonly string[] StepNames =
        {
            "Apagar el fuego",
            "Abrir el agua fría",
            "Enfriar 20 minutos bajo el agua",
            "Retirar el anillo",
            "Abrigar a la persona",
            "Cubrir sin apretar (film o bolsa limpia)",
            "Llamar al 106 y decidir si necesita atención médica"
        };

        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }
        public float FirstActionDelay { get; private set; } = -1f;
        public string Decision { get; private set; }
        public bool DecisionCorrect { get; private set; }
        public int Score { get; private set; }
        public readonly List<Mistake> Mistakes = new List<Mistake>();

        public event System.Action OnFinished;

        public static BurnProtocol Instance { get; private set; }

        private readonly bool[] done = new bool[System.Enum.GetValues(typeof(Step)).Length];
        private readonly float[] doneTime = new float[System.Enum.GetValues(typeof(Step)).Length];
        private readonly Act[] doneAct = new Act[System.Enum.GetValues(typeof(Step)).Length];
        private BurnContactDetector coolingDetector;
        private bool ringStuck;
        private bool decisionShown;
        private string pendingDecision;
        private bool decisionCorrect;
        private bool finishing;

        public bool IsDone(Step step)
        {
            return done[(int)step];
        }

        public float DoneTime(Step step)
        {
            return doneTime[(int)step];
        }

        // Pasos hechos y errores de un acto (la barra de actos del resumen).
        // Un paso que falto cuenta en el acto en que tocaba hacerlo.
        public void CountAct(Act act, out int hits, out int errors, out int critical)
        {
            hits = 0;
            errors = 0;
            critical = 0;
            foreach (Step step in System.Enum.GetValues(typeof(Step)))
            {
                if (IsDone(step) && doneAct[(int)step] == act)
                {
                    hits++;
                }
            }

            foreach (Mistake m in Mistakes)
            {
                if (m.act != act)
                {
                    continue;
                }

                if (m.severity == BurnSequenceManager.Severity.Critical)
                {
                    critical++;
                }
                else
                {
                    errors++;
                }
            }
        }

        // Acto en que se espera cada paso.
        public static Act ExpectedAct(Step step)
        {
            switch (step)
            {
                case Step.ApagarFuego:
                case Step.AguaFria:
                    return Act.Desarrollo;
                case Step.Enfriar:
                case Step.Anillo:
                case Step.Abrigar:
                    return Act.Climax;
            }

            return Act.Resolucion;
        }

        // 0 a 1: cuanto del enfriamiento se completo.
        public float CoolingProgress
        {
            get
            {
                if (burnSequence == null)
                {
                    return 0f;
                }

                BurnSequenceManager.BurnState s = burnSequence.CurrentState;
                // Success no cuenta: la practica puede terminar desde el menu
                // o por tiempo sin haber enfriado.
                if (s == BurnSequenceManager.BurnState.Cooled || s == BurnSequenceManager.BurnState.Covered
                    || IsDone(Step.Enfriar))
                {
                    return 1f;
                }

                return coolingDetector != null ? coolingDetector.Progress : 0f;
            }
        }

        public float SimulatedCoolingMinutes
        {
            get { return CoolingProgress * simulatedMinutes; }
        }

        public bool IsCooling
        {
            get { return burnSequence != null && burnSequence.CurrentState == BurnSequenceManager.BurnState.Cooling; }
        }

        public BurnInjury.BurnDegree Degree
        {
            get { return BurnInjury.Instance != null ? BurnInjury.Instance.degree : BurnInjury.BurnDegree.SegundoSuperficial; }
        }

        // Caso de la partida (grado, extension y zona); null sin paciente.
        public BurnCase Case
        {
            get { return BurnInjury.Instance != null ? BurnInjury.Instance.Case : BurnCase.Current; }
        }

        // Segundo grado con ampollas en la mano: hay que ir al centro de
        // salud. Primer grado (solo roja) se cuida en casa aunque sea
        // grande: la extension no cambia la decision mientras sea en la mano
        // (guia LSEBN: el enrojecimiento no cuenta como superficie quemada).
        public bool NeedsMedicalCare
        {
            get { return Degree == BurnInjury.BurnDegree.SegundoSuperficial; }
        }

        public Act CurrentAct
        {
            get
            {
                if (burnSequence == null || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle)
                {
                    return Act.Planteamiento;
                }

                switch (burnSequence.CurrentState)
                {
                    case BurnSequenceManager.BurnState.Cooled:
                    case BurnSequenceManager.BurnState.Covered:
                    case BurnSequenceManager.BurnState.Success:
                        return Act.Resolucion;
                    case BurnSequenceManager.BurnState.Cooling:
                        return Act.Climax;
                    default:
                        return CoolingProgress > 0f ? Act.Climax : Act.Desarrollo;
                }
            }
        }

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

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = GetComponent<BurnSequenceManager>();
            }

            if (burnSequence == null)
            {
                enabled = false;
                return;
            }

            burnSequence.OnEvent += HandleEvent;
            burnSequence.OnStateChanged += HandleState;
            ConfigureCooling();
        }

        // El detector del chorro pide 'coolingSeconds' de agua y, si se
        // interrumpe, se pausa en vez de volver a cero.
        private void ConfigureCooling()
        {
            foreach (BurnContactDetector detector in FindObjectsOfType<BurnContactDetector>())
            {
                if (detector.requiredItem == FirstAidItem.ItemType.Agua)
                {
                    coolingDetector = detector;
                    detector.requiredSeconds = coolingSeconds;
                    detector.resetOnInterrupt = false;
                }
            }

            foreach (BurnSkinOverlay overlay in FindObjectsOfType<BurnSkinOverlay>())
            {
                overlay.coolingSeconds = coolingSeconds;
            }

            if (coolingDetector == null)
            {
                Debug.LogWarning("[BurnProtocol] No se encontro el detector del agua sobre la quemadura.", this);
            }
        }

        private void Mark(Step step)
        {
            if (done[(int)step] || Finished)
            {
                return;
            }

            done[(int)step] = true;
            doneTime[(int)step] = SinceBurn();
            doneAct[(int)step] = CurrentAct;
            Debug.Log($"[BurnProtocol] Paso completado: {StepNames[(int)step]} ({doneTime[(int)step]:F1} s)");
        }

        private float SinceBurn()
        {
            return burnSequence.BurnTime > 0f ? Time.time - burnSequence.BurnTime : 0f;
        }

        private void HandleState(BurnSequenceManager.BurnState state)
        {
            switch (state)
            {
                case BurnSequenceManager.BurnState.Cooling:
                    Mark(Step.AguaFria);
                    FirstAction();
                    break;
                case BurnSequenceManager.BurnState.Cooled:
                    Mark(Step.Enfriar);
                    break;
                case BurnSequenceManager.BurnState.Covered:
                    Mark(Step.Cubrir);
                    if (EmergencyCall.Instance == null && pendingDecision == null)
                    {
                        if (!decisionShown)
                        {
                            decisionShown = true;
                            StartCoroutine(ShowDecision());
                        }
                    }
                    else
                    {
                        StartCoroutine(WaitForRest());
                    }

                    break;
            }
        }

        private void HandleEvent(BurnSequenceManager.BurnEvent e, string detail, BurnSequenceManager.Severity severity)
        {
            if (Finished && e != BurnSequenceManager.BurnEvent.StepMissed)
            {
                return;
            }

            bool burned = burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle;
            switch (e)
            {
                case BurnSequenceManager.BurnEvent.BurnerOff:
                    if (burned)
                    {
                        Mark(Step.ApagarFuego);
                        FirstAction();
                    }

                    break;
                case BurnSequenceManager.BurnEvent.ColdWaterOn:
                    if (burned)
                    {
                        Mark(Step.AguaFria);
                        FirstAction();
                    }

                    break;
                case BurnSequenceManager.BurnEvent.RingRemoved:
                    Mark(Step.Anillo);
                    break;
                case BurnSequenceManager.BurnEvent.RingStuck:
                    ringStuck = true;
                    break;
                case BurnSequenceManager.BurnEvent.SweaterOn:
                    if (burned)
                    {
                        Mark(Step.Abrigar);
                    }

                    break;
                case BurnSequenceManager.BurnEvent.PotSpilled:
                    break;
                default:
                    if (burned)
                    {
                        FirstAction();
                    }

                    break;
            }

            if (severity != BurnSequenceManager.Severity.None)
            {
                Mistakes.Add(new Mistake
                {
                    burnEvent = e,
                    detail = detail,
                    severity = severity,
                    time = SinceBurn(),
                    explanation = Explain(e, detail),
                    act = MistakeAct(e, detail)
                });
            }

            if (e == BurnSequenceManager.BurnEvent.MedicalDecision && pendingDecision == null)
            {
                // Se termina recien con la quemadura cubierta (WaitForRest).
                // Sin celular, el panel se abre al cubrir y termina aqui.
                pendingDecision = detail;
                if (burnSequence.CurrentState == BurnSequenceManager.BurnState.Covered && EmergencyCall.Instance == null)
                {
                    StartCoroutine(FinishSoon(detail));
                }
            }
        }

        // Todo listo: un momento de cierre (la tia agradece) y el resumen.
        private IEnumerator FinishSoon(string decision)
        {
            if (finishing)
            {
                yield break;
            }

            finishing = true;
            while (EmergencyCall.InCall)
            {
                yield return null;
            }

            yield return new WaitForSeconds(decisionDelay);
            Finish(decision);
        }

        // Ya cubierta. Llamar al 106 y abrigarla se pueden hacer en cualquier
        // momento, tambien despues de cubrir: si falta alguno se espera un
        // rato (mas si falta la llamada, que es mas larga) y se termina con
        // lo que haya. Si ya esta todo, termina enseguida.
        private IEnumerator WaitForRest()
        {
            bool guided = BurnGuide.Instance == null || BurnGuide.Instance.Guided;
            float start = Time.time;
            while (!finishing)
            {
                bool called = pendingDecision != null;
                bool warm = IsDone(Step.Abrigar);
                if (called && warm)
                {
                    break;
                }

                float grace = !called
                    ? (guided ? callGraceSecondsGuided : callGraceSeconds)
                    : (guided ? blanketGraceSecondsGuided : blanketGraceSeconds);
                if (Time.time - start >= grace && !EmergencyCall.InCall)
                {
                    break;
                }

                yield return null;
            }

            if (finishing)
            {
                yield break;
            }

            if (pendingDecision != null)
            {
                StartCoroutine(FinishSoon(pendingDecision));
            }
            else
            {
                finishing = true;
                Finish(null);
            }
        }

        private Act MistakeAct(BurnSequenceManager.BurnEvent e, string detail)
        {
            if (e == BurnSequenceManager.BurnEvent.StepMissed && System.Enum.TryParse(detail, out Step step))
            {
                return ExpectedAct(step);
            }

            // La decision medica se toma al final aunque el estado ya cambio.
            if (e == BurnSequenceManager.BurnEvent.MedicalDecision)
            {
                return Act.Resolucion;
            }

            return CurrentAct;
        }

        private void FirstAction()
        {
            if (FirstActionDelay < 0f && burnSequence.BurnTime > 0f)
            {
                FirstActionDelay = SinceBurn();
            }
        }

        private IEnumerator ShowDecision()
        {
            yield return new WaitForSeconds(decisionDelay);
            if (!Finished)
            {
                MedicalDecisionPanel.Show(this);
            }
        }

        // La respuesta del panel de decision medica.
        // 'advised': lo que recomendo el operador del 106 (null sin llamada).
        // Con llamada, lo correcto es seguir su consejo: si el jugador le
        // describio mal la quemadura, ese error ya se conto (WrongCallInfo) y
        // seguir el consejo que recibio no se castiga otra vez.
        public void Decide(bool goToHealthCenter, bool? advised = null)
        {
            if (Finished || burnSequence == null)
            {
                return;
            }

            bool correct = advised.HasValue ? goToHealthCenter == advised.Value : goToHealthCenter == NeedsMedicalCare;
            decisionCorrect = correct;
            burnSequence.Report(BurnSequenceManager.BurnEvent.MedicalDecision,
                goToHealthCenter ? "CentroDeSalud" : "Casa",
                correct ? BurnSequenceManager.Severity.None : BurnSequenceManager.Severity.Error);
        }

        // Modo evaluacion: al pasar el limite termina solo (la quemadura no
        // espera). En la vida real seria mucho mas que esto: el limite solo
        // evita una practica sin fin.
        private void Update()
        {
            if (Finished || finishing || evaluationTimeLimit <= 0f || burnSequence == null
                || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle || EmergencyCall.InCall
                || BurnGuide.Instance == null || BurnGuide.Instance.Guided)
            {
                return;
            }

            if (SinceBurn() >= evaluationTimeLimit)
            {
                EndEarly("Tiempo");
            }
        }

        // Termina ya (menu de pausa o limite de tiempo) y muestra el resumen:
        // lo que falto cuenta como paso no hecho. Desde el menu tambien vale
        // antes del accidente (la opcion de pasar al final esta siempre).
        public void EndEarly(string reason)
        {
            if (Finished || burnSequence == null)
            {
                return;
            }

            EndedEarly = reason;
            finishing = true;
            if (EmergencyCall.Instance != null)
            {
                EmergencyCall.Instance.HangUp();
            }

            Debug.Log("[BurnProtocol] Termina antes: " + reason);
            if (!IsDone(Step.AguaFria))
            {
                Missed(Step.AguaFria);
            }

            if (!IsDone(Step.Enfriar))
            {
                Missed(Step.Enfriar);
            }

            if (!IsDone(Step.Cubrir))
            {
                Missed(Step.Cubrir);
            }

            Finish(pendingDecision);
        }

        private void Finish(string decision)
        {
            if (Finished)
            {
                return;
            }

            Decision = decision;
            DecisionCorrect = decision != null && decisionCorrect;
            if (decision == null)
            {
                // Nunca llamo al 106.
                Missed(Step.DecisionMedica);
            }
            if (DecisionCorrect)
            {
                Mark(Step.DecisionMedica);
            }

            Finished = true;
            FinishTime = SinceBurn();

            // Pasos que nunca se hicieron.
            if (!IsDone(Step.ApagarFuego))
            {
                Missed(Step.ApagarFuego);
            }

            if (!IsDone(Step.Anillo) && !ringStuck)
            {
                Missed(Step.Anillo);
            }

            if (!IsDone(Step.Abrigar))
            {
                Missed(Step.Abrigar);
            }

            Score = Mathf.Max(0, 100 - burnSequence.Errors * errorPenalty - burnSequence.CriticalErrors * criticalPenalty);
            Debug.Log($"[BurnProtocol] Fin: puntaje {Score}, errores {burnSequence.Errors}, criticos {burnSequence.CriticalErrors}");

            burnSequence.ChangeState(BurnSequenceManager.BurnState.Success);
            if (saveSessionLog)
            {
                SaveLog();
            }

            OnFinished?.Invoke();
            BurnSummaryPanel.Show(this);
        }

        private void Missed(Step step)
        {
            burnSequence.Report(BurnSequenceManager.BurnEvent.StepMissed, step.ToString(), BurnSequenceManager.Severity.Error);
        }

        // ---------------- explicaciones ----------------

        public string Explain(BurnSequenceManager.BurnEvent e, string detail)
        {
            switch (e)
            {
                case BurnSequenceManager.BurnEvent.HotWaterOnBurn:
                    return "El agua caliente vuelve a quemar: usa siempre agua fría o tibia.";
                case BurnSequenceManager.BurnEvent.CoolingInterrupted:
                    return "Sacaste la mano del agua antes de tiempo: hay que enfriar 20 minutos seguidos.";
                case BurnSequenceManager.BurnEvent.RingStuck:
                    return "La mano se hinchó antes de sacar el anillo: retíralo lo antes posible, apenas empieces a atenderla. Si ya no sale, no lo fuerces.";
                case BurnSequenceManager.BurnEvent.CoveredTooEarly:
                    return "Primero hay que terminar de enfriar la quemadura; después se cubre.";
                case BurnSequenceManager.BurnEvent.ClothOnBurn:
                    return "Un paño de cocina suelta pelusa que se pega a la herida: cubre con film o una bolsa plástica limpia.";
                case BurnSequenceManager.BurnEvent.FilmWrapped:
                    return "No envuelvas la mano con el film: al hincharse aprieta. Pon las tiras a lo largo, sueltas.";
                case BurnSequenceManager.BurnEvent.BurnedHandGrabbed:
                    return "Sujeta a la persona del antebrazo: agarrar la mano quemada le duele y daña la piel.";
                case BurnSequenceManager.BurnEvent.HotPotTouched:
                    return "No toques la olla: sigue caliente y también te quemarías. Primero cuídate tú.";
                case BurnSequenceManager.BurnEvent.PatientAlarmed:
                    return "Háblale con calma: dile que el agua fría alivia y ayuda a sanar. El miedo aumenta el dolor.";
                case BurnSequenceManager.BurnEvent.BadAdvice:
                    return ExplainRemedy(detail);
                case BurnSequenceManager.BurnEvent.HotSpillStepped:
                    return "Rodea el agua derramada: el piso mojado resbala. Tu seguridad va primero.";
                case BurnSequenceManager.BurnEvent.RemedyNear:
                case BurnSequenceManager.BurnEvent.RemedyApplied:
                    return ExplainRemedy(detail);
                case BurnSequenceManager.BurnEvent.MedicalDecision when EmergencyCall.Instance != null:
                    return "Sigue lo que recomienda el 106: no decidas lo contrario sin una buena razón.";
                case BurnSequenceManager.BurnEvent.MedicalDecision:
                    return NeedsMedicalCare
                        ? "Una quemadura con ampollas en la mano necesita revisión médica."
                        : "Si solo está roja, sin ampollas, se cuida en casa aunque sea grande; consulta si salen ampollas o empeora.";
                case BurnSequenceManager.BurnEvent.StepMissed:
                    switch (detail)
                    {
                        case "ApagarFuego":
                            return "Apaga el fuego primero para que nadie más se queme.";
                        case "Anillo":
                            return "Retira anillos y pulseras antes de que la mano se hinche.";
                        case "Abrigar":
                            return "Abriga a la persona para que no se enfríe mientras enfrías la quemadura.";
                        case "DecisionMedica":
                            return "Pide ayuda: llama al 106 mientras enfrías la quemadura.";
                        case "AguaFria":
                            return "Lo primero después de apagar el fuego: agua fría corriente sobre la quemadura.";
                        case "Enfriar":
                            return "La quemadura se enfría 20 minutos seguidos bajo agua fría o tibia.";
                        case "Cubrir":
                            return "Al terminar de enfriar, cúbrela sin apretar con film o una bolsa limpia.";
                    }

                    break;
                case BurnSequenceManager.BurnEvent.WrongCallInfo when detail == "SinAgua":
                    return "Al 106 dijiste que la mano estaba bajo el agua y no lo estaba: informa lo que pasa de verdad.";
                case BurnSequenceManager.BurnEvent.WrongCallInfo:
                    return detail == "TieneAmpollas"
                        ? "Mira bien la quemadura antes de describirla: tenía ampollas (segundo grado)."
                        : "Mira bien la quemadura antes de describirla: no tenía ampollas.";
            }

            return e.ToString();
        }

        private static string ExplainRemedy(string remedy)
        {
            switch (remedy)
            {
                case "Hielo":
                    return "El hielo daña todavía más la piel quemada: usa agua corriente.";
                case "Mantequilla":
                case "Aceite":
                    return "La grasa guarda el calor en la piel y aumenta el riesgo de infección.";
                case "PastaDental":
                    return "La pasta dental irrita la piel y no enfría la quemadura.";
                case "Algodon":
                    return "El algodón suelta fibras que se pegan a la quemadura.";
                case "Pomada":
                    return "Ninguna crema ni pomada va sobre una quemadura recién hecha: guarda el calor. Solo agua fría y después film o una bolsa limpia.";
                case "Curita":
                    return "Las curitas y apósitos adhesivos se pegan a la piel quemada y la dañan al quitarlos: cubre con film o una bolsa limpia.";
            }

            return "No se usan remedios caseros sobre una quemadura.";
        }

        // ---------------- registro de la sesion ----------------

        [System.Serializable]
        private class SessionLog
        {
            public string fecha;
            public string modo;
            public string grado;
            public string extension;
            public string zona;
            public int semillaCaso;
            public int puntaje;
            public int errores;
            public int erroresCriticos;
            public float primeraAccionSeg;
            public float enfriamientoMin;
            public string decisionMedica;
            public bool decisionCorrecta;
            public float duracionSeg;
            public string terminoAntes;
            public List<string> pasosCompletados = new List<string>();
            public List<string> pasosFaltantes = new List<string>();
            public List<string> errorDetalle = new List<string>();
        }

        private void SaveLog()
        {
            SessionLog log = new SessionLog
            {
                fecha = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                modo = BurnGuide.Instance != null ? (BurnGuide.Instance.Guided ? "Guiado" : "Evaluacion") : "SinGuia",
                grado = Degree.ToString(),
                extension = Case != null ? Case.size.ToString() : "",
                zona = Case != null ? Case.zone.ToString() : "",
                semillaCaso = Case != null ? Case.seed : 0,
                puntaje = Score,
                errores = burnSequence.Errors,
                erroresCriticos = burnSequence.CriticalErrors,
                primeraAccionSeg = FirstActionDelay,
                enfriamientoMin = SimulatedCoolingMinutes,
                decisionMedica = Decision,
                decisionCorrecta = DecisionCorrect,
                duracionSeg = FinishTime,
                terminoAntes = EndedEarly
            };

            foreach (Step step in System.Enum.GetValues(typeof(Step)))
            {
                (IsDone(step) ? log.pasosCompletados : log.pasosFaltantes).Add(step.ToString());
            }

            foreach (Mistake m in Mistakes)
            {
                log.errorDetalle.Add($"{m.time:F1}s {m.severity} {m.burnEvent} {m.detail}");
            }

            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "sesiones_quemadura");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "sesion_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(log, true));
                Debug.Log("[BurnProtocol] Sesion guardada en " + path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[BurnProtocol] No se pudo guardar la sesion: " + ex.Message);
            }
        }
    }
}
