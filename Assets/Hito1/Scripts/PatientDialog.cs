using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Lo que pasa con la paciente durante los 20 minutos de agua (E.3 y E.6):
    //   - Hacia el minuto 3 se angustia ("¿me va a quedar la marca?"). Hay que
    //     calmarla: tranquilizarla es lo correcto; asustarla o recomendar un
    //     remedio casero es error. Apagado por defecto (worryQuestion): la
    //     tarjeta salia mientras el narrador pedia llamar al 106 y aportaba
    //     poco (prueba en el Quest, 2026-11-09).
    //   - Hacia el minuto 9 el agua ya le parece muy fria y empieza a sacar
    //     la mano. Se queja este donde este el jugador, pero las opciones
    //     salen solo con el a su lado; si esta lejos (la manta), aguanta
    //     'awayGraceSeconds' y, si no vuelve, la saca (error). Hay dos respuestas correctas: convencerla de aguantar o
    //     templar un poco el agua (abrir tambien la llave caliente: con las
    //     dos abiertas sale tibia; NHS y Cruz Roja: agua fresca o tibia, no
    //     helada). Templarla se puede elegir en el panel (y luego hay que
    //     abrir la llave) o hacer directamente con el panel abierto. Si se
    //     le dice que la saque, se le recomienda hielo, no se responde o no
    //     se templa a tiempo, la saca (y el reloj se pausa:
    //     CoolingInterrupted). Despues hay que volver a llevarla. Si el agua
    //     ya sale tibia al llegar el minuto 9, no se queja.
    // Cada momento abre un panel con las respuestas en orden al azar (no se
    // puede memorizar el lugar de la correcta) y la paciente lo dice en voz.
    // Aparece en los dos modos: es la paciente hablando, no la guia.
    // Avisa por BurnSequenceManager.Report. Va en la paciente.
    [RequireComponent(typeof(PatientBody))]
    public class PatientDialog : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Hacer la pregunta del minuto 3 (se angustia y hay que calmarla).")]
        public bool worryQuestion;
        [Tooltip("Minuto simulado en que se angustia y hay que calmarla.")]
        public float worryMinute = 3f;
        [Tooltip("Minuto simulado en que quiere sacar la mano por el frio.")]
        public float resistMinute = 9f;
        [Tooltip("Segundos para responder antes de que ella decida sola.")]
        public float answerSeconds = 14f;
        [Tooltip("Cuanto sube la mano mientras duda, antes de sacarla (m).")]
        public float hesitateLift = 0.02f;
        [Tooltip("Segundos que espera a que se temple el agua si se le ofrecio.")]
        public float temperSeconds = 12f;
        [Tooltip("Las opciones de la queja del frio salen con el jugador a esta distancia de ella (m).")]
        public float resistNearDistance = 2.5f;
        [Tooltip("Si el jugador esta lejos al quejarse del frio (por ejemplo, en la sala por la manta), segundos reales que ella aguanta antes de sacar la mano.")]
        public float awayGraceSeconds = 30f;

        // Ir por la manta (2026-11-17): la queja del minuto 9 caia mientras el
        // jugador volvia con la manta y se sentia a contrarreloj. Ahora se
        // estima cuanto toma la ida y vuelta (por el camino del piso, a paso
        // tranquilo, mas tomarla y ponersela) y, mientras el jugador esta en
        // eso, ella espera a que vuelva para quejarse.
        [Tooltip("Velocidad que se estima para ir y volver con la manta (m/s), contando orientarse y rodear muebles.")]
        public float fetchSpeed = 0.9f;
        [Tooltip("Segundos que se estiman para tomar la manta del sofa y echarsela encima.")]
        public float fetchHandlingSeconds = 15f;
        [Tooltip("Margen sobre el tiempo estimado (1,5 = 50 % mas).")]
        public float fetchMargin = 1.5f;

        private float fetchBudget = -1f;
        private float errandStart = -1f;

        public static bool IsOpen
        {
            get { return open != null; }
        }

        private static Transform open;

        private BurnProtocol protocol;
        private PatientBurnArm arm;
        private BurnPatientReactions reactions;
        private bool worryDone, resistDone;
        private int answer = -1;
        private WaterTap[] taps;

        private struct Option
        {
            public string text;
            public int id;
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            arm = GetComponent<PatientBurnArm>();
            reactions = GetComponent<BurnPatientReactions>();
            taps = FindObjectsOfType<WaterTap>();
        }

        // El agua sale tibia: las dos llaves abiertas (WaterTap.Heat = 0,5).
        private bool WaterTempered()
        {
            if (taps == null)
            {
                return false;
            }

            foreach (WaterTap tap in taps)
            {
                if (tap != null && tap.Flowing && tap.Heat > 0.3f && tap.Heat < 0.7f)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            WaitingForPlayer = false;
            if (open != null)
            {
                Destroy(open.gameObject);
                open = null;
            }
        }

        private void Update()
        {
            if (protocol == null)
            {
                protocol = BurnProtocol.Instance;
                if (protocol == null)
                {
                    return;
                }
            }

            // Durante la llamada al 106 espera: la pregunta sale al colgar.
            if (IsOpen || protocol.Finished || !protocol.IsCooling || MedicalDecisionPanel.IsOpen
                || EmergencyCall.InCall)
            {
                return;
            }

            float minute = protocol.SimulatedCoolingMinutes;
            TrackErrand();
            if (!worryDone && minute >= worryMinute)
            {
                worryDone = true;
                if (worryQuestion)
                {
                    StartCoroutine(Worry());
                }
            }
            else if (!resistDone && minute >= resistMinute && !FetchingBlanket())
            {
                resistDone = true;
                StartCoroutine(Resist());
            }
        }

        // Se quejo del frio y espera que el jugador vuelva junto a ella para
        // preguntarle (BurnGuide lo avisa en modo guiado).
        public static bool WaitingForPlayer { get; private set; }

        private BlanketItem blanket;

        // El jugador esta junto a ella y con las manos libres de la manta (si
        // la trae, primero se la pone). Antes la pregunta salia mientras el
        // jugador estaba en la sala buscando la manta.
        private bool PlayerAttending()
        {
            Transform head = VRPanelKit.Head();
            if (head != null && PatientBody.Flat(head.position - transform.position).magnitude > resistNearDistance)
            {
                return false;
            }

            if (blanket == null)
            {
                blanket = FindObjectOfType<BlanketItem>();
            }

            Grabbable grab = blanket != null ? blanket.GetComponent<Grabbable>() : null;
            return grab == null || !grab.IsHeld;
        }

        // ---------------- ir por la manta ----------------

        // Segundos que se estiman para ir por la manta y volver: el camino
        // del piso (el mismo de las flechas) ida y vuelta a 'fetchSpeed', mas
        // tomarla y ponersela, por 'fetchMargin'. Nunca menos que
        // 'awayGraceSeconds'.
        public float FetchBudget
        {
            get
            {
                if (fetchBudget < 0f)
                {
                    fetchBudget = EstimateFetch();
                }

                return fetchBudget;
            }
        }

        private float EstimateFetch()
        {
            if (blanket == null)
            {
                blanket = FindObjectOfType<BlanketItem>();
            }

            if (blanket == null)
            {
                return awayGraceSeconds;
            }

            Vector3 from = transform.position;
            Vector3 to = blanket.transform.position;
            List<Vector3> path = new List<Vector3>();
            float length = 0f;
            if (PlayArea.FindPath(from, to, 0.9f, path) && path.Count > 1)
            {
                for (int i = 1; i < path.Count; i++)
                {
                    length += Vector3.Distance(path[i - 1], path[i]);
                }
            }
            else
            {
                // Sin camino calculado: en linea recta, con un rodeo.
                length = PatientBody.Flat(to - from).magnitude * 1.4f;
            }

            float seconds = (2f * length / Mathf.Max(0.1f, fetchSpeed) + fetchHandlingSeconds) * fetchMargin;
            seconds = Mathf.Max(awayGraceSeconds, seconds);
            Debug.Log($"[PatientDialog] Ir por la manta: {length:F1} m de camino, se estiman {seconds:F0} s");
            return seconds;
        }

        // El jugador esta yendo o volviendo con la manta: falta abrigarla y
        // esta lejos de ella o con la manta en la mano.
        private bool OnBlanketErrand()
        {
            if (protocol == null || protocol.IsDone(BurnProtocol.Step.Abrigar))
            {
                return false;
            }

            if (blanket == null)
            {
                blanket = FindObjectOfType<BlanketItem>();
                if (blanket == null)
                {
                    return false;
                }
            }

            Grabbable grab = blanket.GetComponent<Grabbable>();
            if (grab != null && grab.IsHeld)
            {
                return true;
            }

            Transform head = VRPanelKit.Head();
            return head != null && PatientBody.Flat(head.position - transform.position).magnitude > resistNearDistance;
        }

        // Desde cuando esta en el mandado (se reinicia al volver sin la manta
        // o al abrigarla).
        private void TrackErrand()
        {
            if (!OnBlanketErrand())
            {
                errandStart = -1f;
                return;
            }

            if (errandStart < 0f)
            {
                errandStart = Time.time;
            }
        }

        // Ella espera para quejarse del frio mientras el jugador va por la
        // manta, hasta el tiempo estimado. Si se pasa, se queja igual (y
        // tiene 'awayGraceSeconds' para volver).
        private bool FetchingBlanket()
        {
            return errandStart >= 0f && Time.time - errandStart < FetchBudget;
        }

        private bool HoldingBlanket()
        {
            Grabbable grab = blanket != null ? blanket.GetComponent<Grabbable>() : null;
            return grab != null && grab.IsHeld;
        }

        // ---------------- minuto 3: calmarla ----------------

        private IEnumerator Worry()
        {
            Say("voz_ansiosa");
            yield return new WaitForSeconds(1.2f);
            Ask("Está asustada. ¿Qué le dices?",
                new Option { id = 0, text = "Tranquila, el agua fría calma el dolor y ayuda a que sane." },
                new Option { id = 1, text = "No sé… se ve muy mal." },
                new Option { id = 2, text = "Te pongo pasta dental, dicen que sirve." });

            float limit = Time.time + answerSeconds;
            while (answer < 0 && Time.time < limit && protocol.IsCooling)
            {
                yield return null;
            }

            Close();
            switch (answer)
            {
                case 0:
                    Say("voz_calmada");
                    burnSequence.Report(BurnSequenceManager.BurnEvent.PatientReassured);
                    break;
                case 1:
                    Say("voz_asustada");
                    burnSequence.Report(BurnSequenceManager.BurnEvent.PatientAlarmed, null,
                        BurnSequenceManager.Severity.Error);
                    break;
                case 2:
                    Say("voz_remedio_no");
                    burnSequence.Report(BurnSequenceManager.BurnEvent.BadAdvice,
                        HomeRemedy.Remedy.PastaDental.ToString(), BurnSequenceManager.Severity.Error);
                    break;
            }
        }

        // ---------------- minuto 9: quiere sacar la mano ----------------

        private IEnumerator Resist()
        {
            if (WaterTempered())
            {
                yield break;
            }

            Say("voz_frio");
            if (arm != null)
            {
                arm.Hesitate(hesitateLift);
            }

            // Ella reacciona al frio este donde este el jugador, pero las
            // opciones solo salen con el jugador a su lado (antes salian
            // mientras buscaba la manta en la sala). Si no vuelve en
            // 'awayGraceSeconds', saca la mano: es la consecuencia de dejarla
            // sola, con margen para ir por la manta y volver. Durante la
            // llamada al 106 el plazo no corre (ella espera).
            answer = -1;
            bool tempered = false;
            bool gaveUp = false;
            float waited = 0f;
            bool complainedAgain = false;
            WaitingForPlayer = true;
            while (!PlayerAttending() && protocol.IsCooling)
            {
                // Templar el agua (desde el lavadero) tambien la calma.
                if (WaterTempered())
                {
                    tempered = true;
                    answer = 3;
                    break;
                }

                // Durante la llamada el plazo no corre; mientras vuelve con la
                // manta en la mano, corre a un tercio (ya viene).
                if (!EmergencyCall.InCall)
                {
                    waited += HoldingBlanket() ? Time.deltaTime / 3f : Time.deltaTime;
                }

                if (!complainedAgain && waited > awayGraceSeconds * 0.5f)
                {
                    complainedAgain = true;
                    Say("voz_frio");
                }

                if (waited >= awayGraceSeconds)
                {
                    gaveUp = true;
                    break;
                }

                yield return null;
            }

            WaitingForPlayer = false;
            if (!tempered && !gaveUp && protocol.IsCooling)
            {
                // Durante la llamada la pregunta espera a que se cuelgue.
                while (EmergencyCall.InCall && protocol.IsCooling)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(waited > 0.5f ? 0.4f : 1.2f);
                int left = Mathf.Max(1, Mathf.CeilToInt(protocol.simulatedMinutes - protocol.SimulatedCoolingMinutes));
                Ask("Quiere sacar la mano del agua. ¿Qué le dices?",
                    new Option { id = 0, text = $"Aguanta un poco más: faltan {left} minutos y así no se hace más profunda." },
                    new Option { id = 3, text = FaucetLever.Current != null ? "Espera, te templo un poco el agua." : "Espera, te templo un poco el agua con la llave caliente." },
                    new Option { id = 1, text = "Sácala un rato y luego la volvemos a poner." },
                    new Option { id = 2, text = "Mejor te pongo hielo, enfría más rápido." });

                float limit = Time.time + answerSeconds;
                while (answer < 0 && Time.time < limit && protocol.IsCooling)
                {
                    // Templar el agua sin tocar el panel tambien vale.
                    if (WaterTempered())
                    {
                        tempered = true;
                        answer = 3;
                        break;
                    }

                    yield return null;
                }

                Close();
            }

            // Se le ofrecio templar el agua: espera con la mano en el agua a
            // que se abra tambien la llave caliente.
            if (answer == 3 && !tempered && protocol.IsCooling)
            {
                Say("voz_templar_pide");
                float wait = Time.time + temperSeconds;
                while (!tempered && Time.time < wait && protocol.IsCooling)
                {
                    tempered = WaterTempered();
                    yield return null;
                }
            }

            if (arm != null)
            {
                arm.Hesitate(0f);
            }

            if (!protocol.IsCooling)
            {
                yield break;
            }

            switch (answer)
            {
                case 0:
                    Say("voz_aguanto");
                    burnSequence.Report(BurnSequenceManager.BurnEvent.CoolingConvinced);
                    yield break;
                case 3:
                    if (tempered)
                    {
                        Say("voz_templada");
                        burnSequence.Report(BurnSequenceManager.BurnEvent.CoolingConvinced, "Templada");
                        yield break;
                    }

                    // Se le prometio y no se templo: la saca.
                    burnSequence.Report(BurnSequenceManager.BurnEvent.CoolingGaveUp, "SinTemplar");
                    break;
                case 2:
                    Say("voz_hielo_no");
                    burnSequence.Report(BurnSequenceManager.BurnEvent.BadAdvice, HomeRemedy.Remedy.Hielo.ToString(),
                        BurnSequenceManager.Severity.Error);
                    yield return new WaitForSeconds(1.5f);
                    break;
                default:
                    // Sin respuesta, o se le dijo que la saque: el error es
                    // la interrupcion (CoolingInterrupted, lo avisa el
                    // detector del agua).
                    burnSequence.Report(BurnSequenceManager.BurnEvent.CoolingGaveUp, answer == 1 ? "Consejo" : "SinRespuesta");
                    break;
            }

            if (arm != null)
            {
                arm.Withdraw();
            }
        }

        // ---------------- panel ----------------

        private void Ask(string question, params Option[] options)
        {
            answer = -1;
            List<Option> shuffled = new List<Option>(options);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                Option tmp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = tmp;
            }

            // Alto segun la cantidad de respuestas (3 o 4).
            Vector2 size = new Vector2(0.56f, 0.08f + 0.1f * shuffled.Count);
            Transform root = VRPanelKit.CreateRoot("PanelPaciente", 0.6f, 0.2f);
            RoundedPanel bg = RoundedPanel.Create(root, "FondoPaciente", Vector2.zero, size, 0.002f, 0.035f,
                0.0025f, 0.02f);
            bg.top = new Color(0.1f, 0.14f, 0.24f, 0.96f);
            bg.bottom = new Color(0.05f, 0.07f, 0.14f, 0.96f);
            bg.Push();
            float top = size.y * 0.5f;
            VRPanelKit.Text(root, question, new Vector2(0f, top - 0.03f), 0.026f, Color.white, TextAnchor.UpperCenter,
                TextAlignment.Center);

            float y = top - 0.115f;
            foreach (Option option in shuffled)
            {
                int id = option.id;
                VRPanelKit.RoundedButton(root, option.text, new Vector2(0f, y),
                    new Vector2(0.5f, 0.08f), new Color(0.2f, 0.34f, 0.6f), () => answer = id);
                y -= 0.1f;
            }

            // Hay que poder ver su mano y su cara mientras se le responde.
            PanelFollow.Add(root, 0.6f, 0.2f, size);
            open = root;
            if (arm != null)
            {
                arm.LookAtPlayer(answerSeconds);
            }

            Hito1FX.PlayAt("logro_paso", root.position, 0.25f, 0f);
        }

        private void Close()
        {
            if (open != null)
            {
                Destroy(open.gameObject);
                open = null;
            }
        }

        private void Say(string line)
        {
            if (reactions != null)
            {
                reactions.Say(line, true);
            }
        }
    }
}
