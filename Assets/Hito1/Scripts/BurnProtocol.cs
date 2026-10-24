using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SojaExiles
{
    // Lleva la cuenta del protocolo de primeros auxilios ante la quemadura a
    // partir de lo que avisa BurnSequenceManager:
    //   pasos (apagar el fuego, agua fria, enfriar 20 min, anillo, abrigar,
    //   cubrir, decision medica), errores con su explicacion, tiempo de
    //   primera accion, tiempo de enfriamiento y puntaje.
    // El enfriamiento de 20 minutos se representa en 'coolingSeconds' reales
    // (el reloj de pared, CoolingClock). Si la mano sale del agua, el reloj se
    // pausa y cuenta como error.
    // Al cubrir la quemadura pregunta si hay que ir al centro de salud
    // (MedicalDecisionPanel); con esa respuesta revisa los pasos que
    // faltaron, termina (Success), muestra el resumen (BurnSummaryPanel) y
    // guarda la sesion en un JSON.
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
        }

        public BurnSequenceManager burnSequence;
        [Tooltip("Segundos reales que representan los 20 minutos de enfriamiento.")]
        public float coolingSeconds = 90f;
        public float simulatedMinutes = 20f;
        [Tooltip("Segundos despues de cubrir hasta que aparece la pregunta del centro de salud.")]
        public float decisionDelay = 5f;
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
            "Cubrir con film o gasa",
            "Decidir si necesita atención médica"
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
        private BurnContactDetector coolingDetector;
        private bool ringStuck;
        private bool decisionShown;

        public bool IsDone(Step step)
        {
            return done[(int)step];
        }

        public float DoneTime(Step step)
        {
            return doneTime[(int)step];
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
                if (s == BurnSequenceManager.BurnState.Cooled || s == BurnSequenceManager.BurnState.Covered
                    || s == BurnSequenceManager.BurnState.Success || IsDone(Step.Enfriar))
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

        // Segundo grado con ampollas en la mano: hay que ir al centro de
        // salud. Una de primer grado pequena se cuida en casa.
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
                    if (!decisionShown)
                    {
                        decisionShown = true;
                        StartCoroutine(ShowDecision());
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
                    explanation = Explain(e, detail)
                });
            }

            if (e == BurnSequenceManager.BurnEvent.MedicalDecision)
            {
                Finish(detail);
            }
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
            MedicalDecisionPanel.Show(this);
        }

        // La respuesta del panel de decision medica.
        public void Decide(bool goToHealthCenter)
        {
            if (Finished || burnSequence == null)
            {
                return;
            }

            bool correct = goToHealthCenter == NeedsMedicalCare;
            burnSequence.Report(BurnSequenceManager.BurnEvent.MedicalDecision,
                goToHealthCenter ? "CentroDeSalud" : "Casa",
                correct ? BurnSequenceManager.Severity.None : BurnSequenceManager.Severity.Error);
        }

        private void Finish(string decision)
        {
            Decision = decision;
            DecisionCorrect = (decision == "CentroDeSalud") == NeedsMedicalCare;
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
                    return "La mano se hinchó antes de sacar el anillo: retíralo apenas empieces a enfriar.";
                case BurnSequenceManager.BurnEvent.CoveredTooEarly:
                    return "Primero hay que terminar de enfriar la quemadura; después se cubre.";
                case BurnSequenceManager.BurnEvent.ClothOnBurn:
                    return "Un paño de cocina suelta pelusa que se pega a la herida: cubre con film o gasa.";
                case BurnSequenceManager.BurnEvent.BurnedHandGrabbed:
                    return "Sujeta a la persona del antebrazo: agarrar la mano quemada le duele y daña la piel.";
                case BurnSequenceManager.BurnEvent.RemedyNear:
                case BurnSequenceManager.BurnEvent.RemedyApplied:
                    return ExplainRemedy(detail);
                case BurnSequenceManager.BurnEvent.MedicalDecision:
                    return NeedsMedicalCare
                        ? "Una quemadura con ampollas en la mano necesita revisión médica."
                        : "Una quemadura de primer grado pequeña se cuida en casa; consulta si empeora.";
                case BurnSequenceManager.BurnEvent.StepMissed:
                    switch (detail)
                    {
                        case "ApagarFuego":
                            return "Apaga el fuego primero para que nadie más se queme.";
                        case "Anillo":
                            return "Retira anillos y pulseras antes de que la mano se hinche.";
                        case "Abrigar":
                            return "Abriga a la persona para que no se enfríe mientras enfrías la quemadura.";
                    }

                    break;
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
            public int puntaje;
            public int errores;
            public int erroresCriticos;
            public float primeraAccionSeg;
            public float enfriamientoMin;
            public string decisionMedica;
            public bool decisionCorrecta;
            public float duracionSeg;
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
                puntaje = Score,
                errores = burnSequence.Errors,
                erroresCriticos = burnSequence.CriticalErrors,
                primeraAccionSeg = FirstActionDelay,
                enfriamientoMin = SimulatedCoolingMinutes,
                decisionMedica = Decision,
                decisionCorrecta = DecisionCorrect,
                duracionSeg = FinishTime
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
