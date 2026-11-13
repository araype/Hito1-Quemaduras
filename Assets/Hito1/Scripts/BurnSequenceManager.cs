using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Maquina de estados del caso "quemadura de primer grado".
    // Orden del protocolo de primeros auxilios:
    //   Burned (la mano se quema: la olla se vuelca, PotAccident; sin el
    //   accidente se dispara tras un retraso) -> Cooling (la mano esta bajo
    //   el chorro de agua fria) -> Cooled (se mantuvo bajo el agua el tiempo
    //   necesario) -> Covered (se cubrio con tiras de film o una bolsa) ->
    //   Success (se llamo al 106 y se decidio si va al centro de salud,
    //   EmergencyCall; sin celular, MedicalDecisionPanel).
    // Los cambios Cooling/Cooled los hacen los BurnContactDetector del
    // trigger de la quemadura. Si se saca la mano del agua antes de tiempo,
    // vuelve a Burned y el enfriamiento empieza de cero.
    //
    // Ademas avisa lo que hace el jugador (Report/OnEvent): apagar el fuego,
    // abrir el agua caliente, sacar el anillo, acercar hielo... Cada aviso
    // dice si es un error y si es critico, para el resumen y el puntaje.
    public class BurnSequenceManager : MonoBehaviour
    {
        public enum BurnState
        {
            Idle,
            Burned,
            Cooling,
            Cooled,
            Success,
            Failed,
            Covered
        }

        // Los valores nuevos se agregan al final.
        public enum BurnEvent
        {
            PotSpilled,         // la olla se volco sobre la mano
            BurnerOff,          // se apago el fuego
            BurnerOn,           // se volvio a prender
            ColdWaterOn,        // se abrio la llave fria
            HotWaterOn,         // se abrio la llave caliente
            HotWaterOnBurn,     // agua caliente (sola) sobre la quemadura
            RingRemoved,        // se saco el anillo a tiempo
            RingStuck,          // la mano ya se hincho: el anillo no sale
            SweaterOn,          // se abrigo a la paciente
            Covered,            // quemadura cubierta (detalle: Film o Bolsa)
            CoveredTooEarly,    // se quiso cubrir antes de enfriar
            ClothOnBurn,        // se cubrio con el pano de cocina (suelta pelusa)
            RemedyNear,         // se acerco un remedio casero a la quemadura
            RemedyApplied,      // se aplico un remedio casero sobre la quemadura
            CoolingInterrupted, // se saco la mano del agua antes de terminar de enfriar
            MedicalDecision,    // se decidio si va al centro de salud (detalle: CentroDeSalud o Casa)
            StepMissed,         // al terminar, falto un paso del protocolo (detalle: el paso)
            BurnedHandGrabbed,  // se la tomo de la mano quemada en vez del antebrazo
            EmergencyCalled,    // se llamo al 106 (EmergencyCall)
            WrongCallInfo,      // al 106 se le describio mal la quemadura (detalle: TieneAmpollas o SinAmpollas)
            FilmWrapped,        // se puso el film atravesado, envolviendo la mano (aprieta al hincharse)
            HotPotTouched,      // el jugador toco la olla caliente (seguridad propia)
            HotSpillStepped,    // el jugador piso el agua derramada (solo aviso: resbala)
            HelpRequested,      // sin uso (la llamada usa EmergencyCalled)
            PatientReassured,   // se calmo a la paciente cuando se asusto
            PatientAlarmed,     // se la asusto mas en vez de calmarla
            BadAdvice,          // se le recomendo un remedio casero (detalle: cual)
            CoolingConvinced,   // siguio con la mano en el agua (detalle: null, o Templada si se templo el agua)
            CoolingGaveUp,      // saco la mano por el frio (detalle: Consejo, SinRespuesta o SinTemplar)
            GauzePressed,       // sin uso desde 2026-11-12 (se quito la gasa)
            GauzeTouchedCenter, // sin uso desde 2026-11-12
            GauzeOffCenter,     // sin uso desde 2026-11-12
            BandageApplied,     // sin uso desde 2026-11-12 (se quito la venda)
            SuppliesReady       // se saco del cajon con que cubrir (detalle: Film o Bolsa); sin puntaje
        }

        public enum Severity
        {
            None,
            Error,
            Critical
        }

        [Tooltip("Sin accidente de la olla (PotAccident), la mano se quema sola tras este retraso.")]
        public bool triggerBurnAfterDelay = true;
        public float burnDelaySeconds = 3f;

        public BurnState CurrentState { get; private set; } = BurnState.Idle;

        // Momento (Time.time) en que se quemo la mano.
        public float BurnTime { get; private set; } = float.NegativeInfinity;

        public int Errors { get; private set; }
        public int CriticalErrors { get; private set; }

        public event System.Action<BurnState> OnStateChanged;

        // Evento, detalle (por ejemplo, que remedio) y gravedad.
        public event System.Action<BurnEvent, string, Severity> OnEvent;

        private void Start()
        {
            if (triggerBurnAfterDelay)
            {
                StartCoroutine(TriggerBurnAfterDelay());
            }
        }

        private IEnumerator TriggerBurnAfterDelay()
        {
            yield return new WaitForSeconds(burnDelaySeconds);
            TriggerBurn();
        }

        public void TriggerBurn()
        {
            if (CurrentState == BurnState.Idle)
            {
                BurnTime = Time.time;
            }

            ChangeState(BurnState.Burned);
        }

        public void ChangeState(BurnState newState)
        {
            if (newState == CurrentState)
            {
                return;
            }

            CurrentState = newState;
            Debug.Log($"[BurnSequence] Estado -> {newState}");
            OnStateChanged?.Invoke(newState);
        }

        public void Report(BurnEvent burnEvent, string detail = null, Severity severity = Severity.None)
        {
            if (severity == Severity.Error)
            {
                Errors++;
            }
            else if (severity == Severity.Critical)
            {
                CriticalErrors++;
            }

            string suffix = severity == Severity.None ? "" : " (" + severity + ")";
            Debug.Log($"[BurnSequence] Evento {burnEvent}{(detail != null ? " " + detail : "")}{suffix}");
            OnEvent?.Invoke(burnEvent, detail, severity);
        }
    }
}
