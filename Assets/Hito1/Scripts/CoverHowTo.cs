using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Tarjeta "como hacerlo" para cubrir la quemadura (pedido tras la prueba
    // en el visor, 2026-11-17): la bolsa y el film piden un movimiento de
    // manos preciso y no se adivinaba. Al agarrar la bolsa, el rollo de film
    // o una tira cortada aparece una tarjeta con los pasos del movimiento,
    // el paso actual grande y resaltado, los hechos en verde y los que
    // faltan en gris. El texto cambia si esa mano es un control (boton
    // lateral) o la mano (pinza). Flechas amarillas animadas muestran el
    // movimiento: de los dedos a la muneca con la bolsa, hacia afuera del
    // rollo al tirar del film y a lo largo de su mano con la tira.
    // Explica solo como mover las manos (la interfaz), no que hacer ante la
    // quemadura, por eso se ve en los dos modos. Lo de "primero enfria" y "no
    // le des la vuelta" (errores que se califican) solo sale en modo guiado.
    // La tarjeta queda sobre la mano quemada si el jugador esta cerca de
    // ella; si no, sobre el objeto agarrado. Va en el objeto de
    // BurnSequenceManager (Hito1RuntimeBootstrap).
    public class CoverHowTo : MonoBehaviour
    {
        [Tooltip("Con la cabeza a menos de esto de la mano quemada (m), la tarjeta va sobre su mano.")]
        public float nearPatient = 1.3f;
        [Tooltip("Altura de la tarjeta sobre su mano o el objeto (m).")]
        public float cardHeight = 0.22f;
        [Tooltip("Con la tira a menos de esto de su mano (m) se muestra la flecha de apoyarla.")]
        public float stripArrowDistance = 0.5f;

        private const float CardWidth = 0.36f;
        private const int MaxRows = 4;

        private BurnSequenceManager burnSequence;
        private Transform card;
        private RoundedPanel background;
        private TextMesh title, warning;
        private readonly TextMesh[] rows = new TextMesh[MaxRows];
        private string shownKey;
        private GuideArrow arrow;
        private Transform head;
        private bool placed;

        private CoverItem[] bags = new CoverItem[0];
        private FilmRoll[] rolls = new FilmRoll[0];
        private FilmStrip[] strips = new FilmStrip[0];
        private float nextScan;

        private static readonly Color DoneColor = new Color(0.5f, 0.9f, 0.58f);
        private static readonly Color NextColor = new Color(0.55f, 0.6f, 0.7f);
        private static readonly Color NowNumber = new Color(1f, 0.8f, 0.25f);

        private void Start()
        {
            burnSequence = GetComponent<BurnSequenceManager>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            arrow = new GuideArrow("FlechaCubrir");
        }

        private void OnDestroy()
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }

            if (arrow != null)
            {
                arrow.Destroy();
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                // Las tiras se crean y se destruyen: se buscan cada tanto.
                nextScan = Time.unscaledTime + 0.25f;
                bags = FindObjectsOfType<CoverItem>();
                rolls = FindObjectsOfType<FilmRoll>();
                strips = FindObjectsOfType<FilmStrip>();
            }

            if (!Active() || !Build(out Steps steps))
            {
                Hide();
                return;
            }

            Show(steps);
        }

        // Despues del accidente y antes de quedar cubierta.
        private bool Active()
        {
            if (burnSequence == null || PauseMenu.Paused)
            {
                return false;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            return state == BurnSequenceManager.BurnState.Burned || state == BurnSequenceManager.BurnState.Cooling
                || state == BurnSequenceManager.BurnState.Cooled;
        }

        private bool Cooled
        {
            get { return burnSequence.CurrentState == BurnSequenceManager.BurnState.Cooled; }
        }

        // Lo que dice la tarjeta este cuadro.
        private struct Steps
        {
            public string title;
            public string[] lines;
            public int current;
            public float progress;
            public Vector3 anchor;
            public bool arrowOn;
            public Vector3 arrowFrom, arrowTo;
        }

        private bool Build(out Steps steps)
        {
            steps = default;
            // Prioridad: tira cortada en la mano, rollo, bolsa.
            foreach (FilmStrip strip in strips)
            {
                if (strip != null && strip.Torn && strip.IsHeld)
                {
                    steps = StripSteps(strip);
                    return true;
                }
            }

            foreach (FilmRoll roll in rolls)
            {
                if (roll != null && roll.IsHeld)
                {
                    steps = RollSteps(roll);
                    return true;
                }
            }

            foreach (CoverItem bag in bags)
            {
                if (bag != null && bag.kind == CoverItem.Kind.Bolsa && bag.IsHeld)
                {
                    steps = BagSteps(bag);
                    return true;
                }
            }

            return false;
        }

        // ---- Bolsa ----

        private Steps BagSteps(CoverItem bag)
        {
            bool control = Controller(bag.Body);
            Steps s = new Steps
            {
                title = "CÓMO PONER LA BOLSA",
                lines = new[]
                {
                    "Acércate a su mano quemada",
                    "Pon la boca de la bolsa en la punta de sus dedos",
                    control
                        ? "Desliza el control hacia su muñeca sin soltar el botón"
                        : "Desliza tu mano hacia su muñeca sin abrirla"
                },
                anchor = bag.transform.position
            };

            bool haveHand = bag.TryGetHandLine(out Vector3 tip, out Vector3 wrist);
            if (bag.Sliding)
            {
                s.current = 2;
                s.progress = bag.SlideProgress;
            }
            else
            {
                s.current = haveHand && bag.NearHand ? 1 : 0;
            }

            if (haveHand)
            {
                if (NearPatient(wrist))
                {
                    s.anchor = wrist;
                }

                // De los dedos a la muneca: el movimiento que hay que hacer.
                s.arrowOn = Cooled && s.current >= 1;
                s.arrowFrom = tip;
                s.arrowTo = wrist;
            }

            return s;
        }

        // ---- Film ----

        private Steps RollSteps(FilmRoll roll)
        {
            bool rollLeft = SimpleGrab.IsHeldBy(roll.Body, true);
            bool freeControl = SimpleGrab.UsesController(!rollLeft);
            Steps s = FilmBase(Controller(roll.Body), freeControl);
            s.anchor = roll.transform.position;
            if (roll.TryGetPull(out Vector3 edge, out Vector3 outward, out float fraction))
            {
                s.current = roll.Pulling ? 2 : 1;
                s.progress = roll.Pulling ? fraction : 0f;
                // Hacia afuera del rollo: de donde se tira.
                s.arrowOn = true;
                s.arrowFrom = edge;
                s.arrowTo = edge + outward * 0.16f;
            }
            else
            {
                s.current = 1;
            }

            if (PatientHand(out Vector3 wrist, out _) && NearPatient(wrist))
            {
                s.anchor = wrist;
            }

            return s;
        }

        private Steps StripSteps(FilmStrip strip)
        {
            Rigidbody body = strip.GetComponent<Rigidbody>();
            bool control = Controller(body);
            Steps s = FilmBase(control, control);
            s.current = 3;
            s.anchor = strip.transform.position;
            if (PatientHand(out Vector3 wrist, out Vector3 fingers))
            {
                if (NearPatient(wrist))
                {
                    s.anchor = wrist;
                }

                float distance = Vector3.Distance(strip.transform.position, (wrist + fingers) * 0.5f);
                s.arrowOn = Cooled && distance < stripArrowDistance;
                s.arrowFrom = wrist;
                s.arrowTo = fingers;
            }

            return s;
        }

        private static Steps FilmBase(bool rollControl, bool freeControl)
        {
            // Envolver la mano es un error que el modo evaluacion califica:
            // el aviso de no darle la vuelta sale solo en modo guiado.
            string noWrap = IndicatorUtil.HintsVisible ? ". No le des la vuelta" : "";
            return new Steps
            {
                title = "CÓMO USAR EL FILM",
                lines = new[]
                {
                    rollControl
                        ? "Sostén el rollo con un control (botón lateral)"
                        : "Sostén el rollo con una mano",
                    freeControl
                        ? "Acerca el otro control a la solapa del rollo y aprieta el botón lateral"
                        : "Con la otra mano, pellizca la solapa del rollo",
                    "Aleja esa mano del rollo hasta que la tira se corte",
                    freeControl
                        ? "Apoya la tira a lo largo de su mano y suelta el botón" + noWrap
                        : "Apoya la tira a lo largo de su mano y abre los dedos" + noWrap
                }
            };
        }

        // ---- Utilidades ----

        // La mano que sostiene 'body' es un control (no la mano real).
        private static bool Controller(Rigidbody body)
        {
            bool left = body != null && SimpleGrab.IsHeldBy(body, true);
            return SimpleGrab.UsesController(left);
        }

        // Muneca y punta de los dedos de la mano quemada.
        private static bool PatientHand(out Vector3 wrist, out Vector3 fingers)
        {
            wrist = fingers = Vector3.zero;
            FilmCoverage coverage = FilmCoverage.For(FindObjectOfType<PatientBody>());
            if (coverage == null || coverage.Hand == null
                || !coverage.WorldAxis(out Vector3 center, out Vector3 axis, out float half))
            {
                return false;
            }

            wrist = coverage.Hand.position;
            Vector3 a = center + axis * half;
            Vector3 b = center - axis * half;
            fingers = Vector3.Distance(a, wrist) > Vector3.Distance(b, wrist) ? a : b;
            return true;
        }

        private bool NearPatient(Vector3 point)
        {
            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            return head != null && Vector3.Distance(head.position, point) < nearPatient;
        }

        // ---- Tarjeta ----

        private void Show(Steps s)
        {
            if (card == null)
            {
                CreateCard();
            }

            if (!card.gameObject.activeSelf)
            {
                card.gameObject.SetActive(true);
                placed = false;
            }

            bool early = !Cooled && IndicatorUtil.HintsVisible;
            int percent = Mathf.RoundToInt(s.progress * 100f);
            string key = s.title + s.current + "|" + (percent / 10) + "|" + early + "|" + string.Join("|", s.lines);
            if (key != shownKey)
            {
                shownKey = key;
                Layout(s, early);
            }

            Place(s.anchor);
            if (s.arrowOn)
            {
                arrow.Show(s.arrowFrom, s.arrowTo);
            }
            else
            {
                arrow.Hide();
            }
        }

        private void Hide()
        {
            if (card != null && card.gameObject.activeSelf)
            {
                card.gameObject.SetActive(false);
            }

            shownKey = null;
            if (arrow != null)
            {
                arrow.Hide();
            }
        }

        private void CreateCard()
        {
            GameObject go = new GameObject("TarjetaComoCubrir");
            go.AddComponent<NoAutoCollider>();
            card = go.transform;
            background = VRPanelKit.CardPanel(card, Vector2.zero, new Vector2(CardWidth, 0.2f));
            title = VRPanelKit.Text(card, "", Vector2.zero, 0.017f, VRPanelKit.CardLabelColor);
            warning = VRPanelKit.Text(card, "", Vector2.zero, 0.019f, VRPanelKit.CardWarningColor);
            for (int i = 0; i < MaxRows; i++)
            {
                rows[i] = VRPanelKit.Text(card, "", Vector2.zero, 0.02f, Color.white);
            }

            go.AddComponent<OverlayPanel>();
        }

        // Arma el texto de arriba hacia abajo y ajusta el fondo a lo que ocupa.
        private void Layout(Steps s, bool early)
        {
            const float pad = 0.018f;
            float left = -CardWidth * 0.5f + pad;
            float y = 0f;

            title.text = "<b>" + s.title + "</b>";
            title.transform.localPosition = new Vector3(left, y, -0.003f);
            y -= 0.03f;

            if (early)
            {
                warning.gameObject.SetActive(true);
                SetLines(warning, "Primero termina de enfriar: todavía no la cubras.", 0.019f, 34);
                warning.transform.localPosition = new Vector3(left, y, -0.003f);
                y -= LineCount(warning.text) * 0.019f * 1.25f + 0.012f;
            }
            else
            {
                warning.gameObject.SetActive(false);
            }

            for (int i = 0; i < MaxRows; i++)
            {
                TextMesh row = rows[i];
                if (i >= s.lines.Length)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                row.gameObject.SetActive(true);
                bool now = i == s.current;
                bool done = i < s.current;
                float size = now ? 0.024f : 0.018f;
                string body = s.lines[i];
                if (now && s.progress > 0.01f)
                {
                    body += " (" + Mathf.RoundToInt(s.progress * 100f) + " %)";
                }

                string wrapped = VRPanelKit.Wrap(body, now ? 22 : 32);
                string number = (i + 1) + ".  ";
                string text;
                if (now)
                {
                    text = Hex(NowNumber, "<b>" + number + "</b>") + "<b>" + Indent(wrapped) + "</b>";
                    row.color = Color.white;
                }
                else
                {
                    text = number + Indent(wrapped);
                    row.color = done ? DoneColor : NextColor;
                }

                row.text = text;
                row.characterSize = size * 10f / 64f;
                row.transform.localPosition = new Vector3(left, y, -0.003f);
                y -= LineCount(wrapped) * size * 1.25f + (now ? 0.014f : 0.009f);
            }

            float height = -y + pad * 0.5f;
            // El texto empieza arriba a la izquierda: se sube todo para que la
            // tarjeta quede centrada en su ancla.
            float top = height * 0.5f - pad * 0.6f;
            foreach (TextMesh t in card.GetComponentsInChildren<TextMesh>(true))
            {
                Vector3 p = t.transform.localPosition;
                t.transform.localPosition = new Vector3(p.x, p.y + top, p.z);
            }

            background.SetRect(Vector2.zero, new Vector2(CardWidth, height + pad));
        }

        private static void SetLines(TextMesh mesh, string text, float size, int width)
        {
            mesh.text = VRPanelKit.Wrap(text, width);
            mesh.characterSize = size * 10f / 64f;
        }

        // Las lineas siguientes quedan alineadas despues del numero.
        private static string Indent(string wrapped)
        {
            return wrapped.Replace("\n", "\n     ");
        }

        private static int LineCount(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;
        }

        private static string Hex(Color c, string text)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";
        }

        // Sobre el ancla, un poco hacia el jugador, mirandolo. Sigue suave para
        // no temblar con la mano.
        private void Place(Vector3 anchor)
        {
            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            Vector3 toHead = head != null ? Vector3.ProjectOnPlane(head.position - anchor, Vector3.up) : Vector3.back;
            toHead = toHead.sqrMagnitude > 1e-4f ? toHead.normalized : Vector3.back;
            Vector3 target = anchor + Vector3.up * cardHeight + toHead * 0.08f;
            card.position = placed ? Vector3.Lerp(card.position, target, 1f - Mathf.Exp(-6f * Time.deltaTime)) : target;
            placed = true;
            if (head != null)
            {
                card.rotation = Quaternion.LookRotation(card.position - head.position, Vector3.up);
            }
        }
    }

    // Flecha de tres puntas (">") que avanzan en bucle de 'from' a 'to',
    // mirando al jugador: muestra hacia donde mover la mano.
    public class GuideArrow
    {
        private readonly List<LineRenderer> chevrons = new List<LineRenderer>();
        private Transform head;
        private static readonly Color ArrowColor = new Color(1f, 0.82f, 0.2f);

        public GuideArrow(string name)
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 3; i++)
            {
                GameObject go = new GameObject(name + i);
                go.AddComponent<NoAutoCollider>();
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 3;
                line.widthMultiplier = 0.008f;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sharedMaterial = material;
                line.enabled = false;
                chevrons.Add(line);
            }
        }

        public void Show(Vector3 from, Vector3 to)
        {
            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            Vector3 path = to - from;
            float length = path.magnitude;
            if (length < 0.01f)
            {
                Hide();
                return;
            }

            Vector3 dir = path / length;
            Vector3 toHead = head != null ? (head.position - (from + to) * 0.5f).normalized : Vector3.up;
            Vector3 side = Vector3.Cross(dir, toHead);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.Cross(dir, Vector3.up);
            }

            side.Normalize();
            // Despegada de la piel, hacia el jugador.
            Vector3 lift = toHead * 0.03f;
            float arm = Mathf.Clamp(length * 0.2f, 0.018f, 0.03f);
            for (int i = 0; i < chevrons.Count; i++)
            {
                float t = Mathf.Repeat(Time.time * 0.6f + i / (float)chevrons.Count, 1f);
                Vector3 p = from + path * t + lift;
                LineRenderer line = chevrons[i];
                line.SetPosition(0, p - dir * arm + side * arm);
                line.SetPosition(1, p);
                line.SetPosition(2, p - dir * arm - side * arm);
                // Aparece al salir y se desvanece al llegar.
                float alpha = Mathf.Sin(t * Mathf.PI);
                Color c = ArrowColor;
                c.a = 0.95f * alpha;
                line.startColor = c;
                line.endColor = c;
                line.enabled = true;
            }
        }

        public void Hide()
        {
            foreach (LineRenderer line in chevrons)
            {
                if (line != null && line.enabled)
                {
                    line.enabled = false;
                }
            }
        }

        public void Destroy()
        {
            foreach (LineRenderer line in chevrons)
            {
                if (line != null)
                {
                    Object.Destroy(line.gameObject);
                }
            }
        }
    }
}
