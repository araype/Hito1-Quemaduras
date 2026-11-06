using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Lo que pasa con la paciente durante los 20 minutos de agua (E.3 y E.6):
    //   - Hacia el minuto 3 se angustia ("¿me va a quedar la marca?"). Hay que
    //     calmarla: tranquilizarla es lo correcto; asustarla o recomendar un
    //     remedio casero es error.
    //   - Hacia el minuto 9 el agua ya le parece muy fria y empieza a sacar
    //     la mano. Hay que convencerla de aguantar: si se le dice que la saque,
    //     se le recomienda hielo o no se le responde, la saca (y el reloj se
    //     pausa: CoolingInterrupted). Despues hay que volver a llevarla.
    // Cada momento abre un panel con tres respuestas en orden al azar (no se
    // puede memorizar el lugar de la correcta) y la paciente lo dice en voz.
    // Aparece en los dos modos: es la paciente hablando, no la guia.
    // Avisa por BurnSequenceManager.Report. Va en la paciente.
    [RequireComponent(typeof(PatientBody))]
    public class PatientDialog : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Minuto simulado en que se angustia y hay que calmarla.")]
        public float worryMinute = 3f;
        [Tooltip("Minuto simulado en que quiere sacar la mano por el frio.")]
        public float resistMinute = 9f;
        [Tooltip("Segundos para responder antes de que ella decida sola.")]
        public float answerSeconds = 14f;
        [Tooltip("Cuanto sube la mano mientras duda, antes de sacarla (m).")]
        public float hesitateLift = 0.02f;

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
        }

        private void OnDestroy()
        {
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

            if (IsOpen || protocol.Finished || !protocol.IsCooling || MedicalDecisionPanel.IsOpen)
            {
                return;
            }

            float minute = protocol.SimulatedCoolingMinutes;
            if (!worryDone && minute >= worryMinute)
            {
                worryDone = true;
                StartCoroutine(Worry());
            }
            else if (!resistDone && minute >= resistMinute)
            {
                resistDone = true;
                StartCoroutine(Resist());
            }
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
            Say("voz_frio");
            if (arm != null)
            {
                arm.Hesitate(hesitateLift);
            }

            yield return new WaitForSeconds(1.2f);
            int left = Mathf.Max(1, Mathf.CeilToInt(protocol.simulatedMinutes - protocol.SimulatedCoolingMinutes));
            Ask("Quiere sacar la mano del agua. ¿Qué le dices?",
                new Option { id = 0, text = $"Aguanta un poco más: faltan {left} minutos y así no se hace más profunda." },
                new Option { id = 1, text = "Sácala un rato y luego la volvemos a poner." },
                new Option { id = 2, text = "Mejor te pongo hielo, enfría más rápido." });

            float limit = Time.time + answerSeconds;
            while (answer < 0 && Time.time < limit && protocol.IsCooling)
            {
                yield return null;
            }

            Close();
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

            Vector2 size = new Vector2(0.56f, 0.38f);
            Transform root = VRPanelKit.CreateRoot("PanelPaciente", 0.6f, 0.2f);
            RoundedPanel bg = RoundedPanel.Create(root, "FondoPaciente", Vector2.zero, size, 0.002f, 0.035f,
                0.0025f, 0.02f);
            bg.top = new Color(0.1f, 0.14f, 0.24f, 0.96f);
            bg.bottom = new Color(0.05f, 0.07f, 0.14f, 0.96f);
            bg.Push();
            VRPanelKit.Text(root, question, new Vector2(0f, 0.16f), 0.026f, Color.white, TextAnchor.UpperCenter,
                TextAlignment.Center);

            float y = 0.075f;
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
