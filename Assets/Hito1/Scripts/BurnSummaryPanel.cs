using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Resumen de la practica (RF-10), frente al jugador al terminar. Mismo
    // estilo que las tarjetas de la guia (panel de vidrio redondeado).
    // Rediseno del 2026-11-17 (prueba en el Quest Pro): el anterior estaba
    // cargado (cuatro indicadores y la barra de actos que no se entendian),
    // los textos se montaban y, con muchos errores, los cortaba ("Y 3
    // errores mas"). Ahora:
    //   - encabezado: una frase que dice como le fue, el caso y el puntaje;
    //   - izquierda: los pasos del protocolo, hechos o no (con el detalle
    //     que importa: minutos de enfriamiento si faltaron, que se decidio
    //     con el 106);
    //   - derecha: que mejorar, una tarjeta por error (titulo corto y que
    //     hacer), los graves primero. Los textos se ajustan a su ancho con
    //     TextMesh Pro y se miden, asi no se montan; si no caben todos, se
    //     pasa de pagina en vez de omitirlos;
    //   - botones para repetir el caso o probar otro.
    // Se quitaron el tiempo de la primera accion y el de enfriamiento
    // (siempre 20/20 salvo que falte el paso, y eso ya lo dicen los pasos),
    // los contadores de errores (los dice el encabezado) y la barra de actos.
    public class BurnSummaryPanel : MonoBehaviour
    {
        private static readonly Color Green = new Color(0.36f, 0.84f, 0.54f);
        private static readonly Color Red = new Color(0.94f, 0.44f, 0.4f);
        private static readonly Color Amber = new Color(0.96f, 0.76f, 0.42f);
        private static readonly Color Accent = new Color(0.62f, 0.8f, 1f);
        private static readonly Color Soft = new Color(0.82f, 0.87f, 0.95f);
        private static readonly Color Muted = new Color(0.6f, 0.66f, 0.76f);

        private const float Width = 1.0f;
        private const float Height = 0.92f;
        private const float Pad = 0.045f;
        private const float Gap = 0.02f;
        private const float LeftColumn = 0.31f;
        private const float Distance = 0.62f;
        private const float Scale = 0.72f;

        private readonly List<GameObject> pages = new List<GameObject>();
        private int page;
        private TextMesh pageLabel;
        private TouchButton previous, next;

        public static BurnSummaryPanel Show(BurnProtocol protocol)
        {
            // Al alcance de la mano (0,62 m, como las otras tarjetas) y mas
            // chico para que quepa en la vista.
            Transform root = VRPanelKit.CreateRoot("PanelResumen", Distance, 0.12f);
            root.localScale = Vector3.one * Scale;
            PanelFollow.Add(root, Distance, 0.12f, new Vector2(Width, Height) * Scale);
            BurnSummaryPanel panel = root.gameObject.AddComponent<BurnSummaryPanel>();
            panel.Build(protocol);
            return panel;
        }

        private void Build(BurnProtocol p)
        {
            Transform t = transform;
            BurnSequenceManager burn = p.burnSequence;
            float left = -Width / 2f + Pad;
            float right = Width / 2f - Pad;
            float top = Height / 2f - Pad;

            RoundedPanel bg = RoundedPanel.Create(t, "FondoResumen", Vector2.zero, new Vector2(Width, Height), 0.002f,
                0.04f, 0.0025f, 0.02f);
            bg.top = new Color(0.1f, 0.14f, 0.24f, 0.97f);
            bg.bottom = new Color(0.05f, 0.07f, 0.14f, 0.97f);
            bg.Push();

            List<Item> items = Group(p);
            int missing = 0;
            foreach (BurnProtocol.Step step in System.Enum.GetValues(typeof(BurnProtocol.Step)))
            {
                if (!p.IsDone(step))
                {
                    missing++;
                }
            }

            // ---------------- encabezado ----------------
            bool guided = BurnGuide.Instance == null || BurnGuide.Instance.Guided;
            VRPanelKit.Text(t, "PRÁCTICA TERMINADA  ·  " + (guided ? "MODO GUIADO" : "MODO EVALUACIÓN"),
                new Vector2(left, top), 0.017f, Accent);

            float chipW = 0.2f;
            float textW = Width - 2f * Pad - chipW - Gap;
            float y = top - 0.032f;
            y -= Wrapped(t, "<b>" + Verdict(burn, missing) + "</b>", new Vector2(left, y), 0.042f, Color.white, textW)
                + 0.012f;

            string degree = p.Case != null ? p.Case.Describe()
                : (p.Degree == BurnInjury.BurnDegree.Primero ? "primer grado" : "segundo grado superficial");
            string ended = p.EndedEarly == "Menu" ? "  ·  Terminada desde el menú"
                : p.EndedEarly == "Tiempo" ? "  ·  Se acabó el tiempo" : "";
            y -= Wrapped(t, "Quemadura de " + degree + ended, new Vector2(left, y), 0.019f, Soft, textW);

            Color scoreColor = p.Score >= 80 ? Green : (p.Score >= 50 ? Amber : Red);
            Vector2 chipCenter = new Vector2(right - chipW / 2f, top - 0.055f);
            RoundedPanel chip = RoundedPanel.Create(t, "Puntaje", chipCenter, new Vector2(chipW, 0.115f), 0.001f,
                0.03f, 0.002f, 0.012f);
            chip.top = new Color(0.12f, 0.17f, 0.28f, 1f);
            chip.bottom = new Color(0.08f, 0.11f, 0.2f, 1f);
            chip.borderLeft = scoreColor;
            chip.borderRight = scoreColor;
            chip.glowStrength = 0.35f;
            chip.Push();
            VRPanelKit.Text(t, $"<b>{p.Score}</b><size=55%> / 100</size>", chipCenter + new Vector2(0f, 0.045f), 0.06f,
                scoreColor, TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(t, "PUNTAJE", chipCenter + new Vector2(0f, -0.03f), 0.014f, Muted,
                TextAnchor.UpperCenter, TextAlignment.Center);

            // ---------------- columnas ----------------
            // Las columnas empiezan debajo del encabezado, mida lo que mida.
            float colTop = Mathf.Min(top - 0.15f, y - 0.022f);
            float colBottom = -Height / 2f + Pad + 0.105f;
            float colH = colTop - colBottom;
            float rightW = Width - 2f * Pad - LeftColumn - Gap;
            Card(t, "Pasos", new Vector2(left + LeftColumn / 2f, colTop - colH / 2f), new Vector2(LeftColumn, colH));
            Card(t, "Mejorar", new Vector2(right - rightW / 2f, colTop - colH / 2f), new Vector2(rightW, colH));

            BuildSteps(t, p, left, colTop);
            BuildImprove(t, items, burn, right - rightW, colTop, rightW, colH);

            // ---------------- botones ----------------
            float buttonY = -Height / 2f + Pad + 0.045f;
            BurnCase same = p.Case != null ? p.Case.Copy() : null;
            // Los dos vuelven al panel de inicio (modo guiado o evaluacion):
            // el mismo caso u otro al azar (grado, extension o zona distintos).
            VRPanelKit.RoundedButton(t, "Repetir este caso", new Vector2(-0.19f, buttonY), new Vector2(0.34f, 0.075f),
                new Color(0.22f, 0.48f, 0.95f), () => Restart(same));
            VRPanelKit.RoundedButton(t, "Otro caso al azar", new Vector2(0.19f, buttonY), new Vector2(0.34f, 0.075f),
                new Color(0.45f, 0.3f, 0.62f), () => Restart(BurnCase.RandomOtherThan(same)));
            // El credito que pide el plan gratis de ElevenLabs (docs/VOCES.md).
            VRPanelKit.Text(t, "Voces: elevenlabs.io", new Vector2(0f, buttonY - 0.05f), 0.013f, Muted,
                TextAnchor.UpperCenter, TextAlignment.Center);
        }

        // Una frase que dice como le fue, sin numeros.
        private static string Verdict(BurnSequenceManager burn, int missing)
        {
            if (burn.CriticalErrors > 0)
            {
                return "Hubo errores graves";
            }

            if (missing > 0)
            {
                return missing == 1 ? "Faltó un paso del protocolo" : "Faltaron " + missing + " pasos del protocolo";
            }

            if (burn.Errors > 0)
            {
                return "Bien hecho, con algunas cosas por mejorar";
            }

            return "¡Protocolo completo y sin errores!";
        }

        // ---------------- pasos ----------------

        private static void BuildSteps(Transform t, BurnProtocol p, float left, float colTop)
        {
            float cx = left + 0.022f;
            float rightEdge = left + LeftColumn - 0.022f;
            float nameW = LeftColumn - 0.044f - 0.024f - 0.07f;
            float y = colTop - 0.024f;
            VRPanelKit.Text(t, "<b>PASOS DEL PROTOCOLO</b>", new Vector2(cx, y), 0.016f, Accent);
            y -= 0.042f;
            foreach (BurnProtocol.Step step in System.Enum.GetValues(typeof(BurnProtocol.Step)))
            {
                StepStatus(p, step, out string status, out Color c, out string detail);
                RoundedPanel dot = RoundedPanel.Create(t, "EstadoPaso", new Vector2(cx + 0.006f, y - 0.0105f),
                    Vector2.one * 0.012f, -0.0005f, 0.006f, 0f, 0.005f);
                dot.sheen = 0f;
                dot.SetSolid(c, 0.4f);
                float h = Wrapped(t, ShortStepName(step), new Vector2(cx + 0.024f, y), 0.019f, Color.white, nameW);
                VRPanelKit.Text(t, status, new Vector2(rightEdge, y - 0.002f), 0.016f, c,
                    TextAnchor.UpperRight, TextAlignment.Right);
                y -= h + 0.006f;
                if (!string.IsNullOrEmpty(detail))
                {
                    y -= Wrapped(t, detail, new Vector2(cx + 0.024f, y), 0.0155f, Muted,
                        LeftColumn - 0.044f - 0.024f) + 0.006f;
                }

                y -= 0.012f;
            }
        }

        private static void StepStatus(BurnProtocol p, BurnProtocol.Step step, out string status, out Color color,
            out string detail)
        {
            bool ok = p.IsDone(step);
            status = ok ? "Hecho" : "Faltó";
            color = ok ? Green : Red;
            detail = null;
            switch (step)
            {
                case BurnProtocol.Step.Enfriar:
                    if (!ok)
                    {
                        status = $"{p.SimulatedCoolingMinutes:F0}/{p.simulatedMinutes:F0} min";
                        color = p.SimulatedCoolingMinutes > 0.5f ? Amber : Red;
                    }

                    break;
                case BurnProtocol.Step.DecisionMedica:
                    if (p.Decision == null)
                    {
                        detail = "No se llamó al 106.";
                    }
                    else
                    {
                        status = p.DecisionCorrect ? "Correcta" : "Incorrecta";
                        detail = "Decidiste: " + (p.Decision == "CentroDeSalud" ? "ir al centro de salud." : "cuidarla en casa.");
                    }

                    break;
            }
        }

        // Nombre corto de cada paso (BurnProtocol.StepNames queda para el
        // registro).
        private static string ShortStepName(BurnProtocol.Step step)
        {
            switch (step)
            {
                case BurnProtocol.Step.ApagarFuego: return "Apagar el fuego";
                case BurnProtocol.Step.AguaFria: return "Abrir el agua fría";
                case BurnProtocol.Step.Enfriar: return "Enfriar 20 minutos";
                case BurnProtocol.Step.Anillo: return "Retirar el anillo";
                case BurnProtocol.Step.Cubrir: return "Cubrir sin apretar";
                case BurnProtocol.Step.DecisionMedica: return "Llamar al 106";
                default: return BurnProtocol.StepNames[(int)step];
            }
        }

        // ---------------- que mejorar ----------------

        private class Item
        {
            public string title;
            public string explanation;
            public BurnSequenceManager.Severity severity;
            public int count;
            public float time;
        }

        // Un item por explicacion distinta (si se repite, "2 veces"); los
        // graves primero y, entre los del mismo tipo, en el orden en que
        // pasaron.
        private static List<Item> Group(BurnProtocol p)
        {
            Dictionary<string, Item> byText = new Dictionary<string, Item>();
            List<Item> items = new List<Item>();
            foreach (BurnProtocol.Mistake m in p.Mistakes)
            {
                if (byText.TryGetValue(m.explanation, out Item item))
                {
                    item.count++;
                    if (m.severity > item.severity)
                    {
                        item.severity = m.severity;
                    }

                    continue;
                }

                item = new Item
                {
                    title = MistakeTitle(m),
                    explanation = m.explanation,
                    severity = m.severity,
                    count = 1,
                    time = m.time
                };
                byText[m.explanation] = item;
                items.Add(item);
            }

            items.Sort((a, b) => a.severity != b.severity ? b.severity.CompareTo(a.severity) : a.time.CompareTo(b.time));
            return items;
        }

        private void BuildImprove(Transform t, List<Item> items, BurnSequenceManager burn, float x, float colTop,
            float w, float h)
        {
            float inner = 0.022f;
            float y = colTop - 0.024f;
            VRPanelKit.Text(t, "<b>QUÉ MEJORAR</b>", new Vector2(x + inner, y), 0.016f, Accent);
            string counts = burn.Errors + burn.CriticalErrors == 0 ? "sin errores"
                : Plural(burn.Errors, "error", "errores")
                  + (burn.CriticalErrors > 0 ? "  ·  " + Plural(burn.CriticalErrors, "grave", "graves") : "");
            VRPanelKit.Text(t, counts, new Vector2(x + w - inner, y - 0.001f), 0.015f,
                burn.CriticalErrors > 0 ? Red : burn.Errors > 0 ? Amber : Green, TextAnchor.UpperRight,
                TextAlignment.Right);

            float listTop = y - 0.04f;
            float listBottom = colTop - h + 0.018f;
            float cardW = w - 2f * inner;
            float cardLeft = x + inner;

            if (items.Count == 0)
            {
                GameObject only = Page(t);
                BuildItem(only.transform, new Item
                {
                    title = "Seguiste el protocolo",
                    explanation = "No hubo errores. Repite el caso en modo evaluación, sin la guía, o prueba otro caso.",
                    severity = BurnSequenceManager.Severity.None,
                    count = 1
                }, cardLeft, listTop, cardW);
                return;
            }

            // Primero sin espacio para los botones de pagina; si no entran
            // todos, se reserva ese espacio y se reparte en paginas.
            if (!Layout(t, items, cardLeft, listTop, listBottom, cardW))
            {
                ClearPages();
                const float navH = 0.06f;
                Layout(t, items, cardLeft, listTop, listBottom + navH, cardW);
                BuildPager(t, x + w / 2f, listBottom + navH / 2f - 0.006f);
            }

            ShowPage(0);
        }

        // Reparte los items en paginas entre 'top' y 'bottom'. Devuelve true
        // si entraron todos en una.
        private bool Layout(Transform t, List<Item> items, float left, float top, float bottom, float w)
        {
            const float spacing = 0.012f;
            GameObject current = Page(t);
            float y = top;
            foreach (Item item in items)
            {
                GameObject built = BuildItem(current.transform, item, left, y, w, out float height);
                if (y - height < bottom && y < top - 0.001f)
                {
                    // No entra: a la pagina siguiente.
                    Destroy(built);
                    current = Page(t);
                    y = top;
                    built = BuildItem(current.transform, item, left, y, w, out height);
                }

                y -= height + spacing;
            }

            return pages.Count == 1;
        }

        private GameObject Page(Transform t)
        {
            GameObject go = new GameObject("PaginaMejorar" + pages.Count);
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(t, false);
            pages.Add(go);
            return go;
        }

        private void ClearPages()
        {
            foreach (GameObject go in pages)
            {
                Destroy(go);
            }

            pages.Clear();
        }

        private GameObject BuildItem(Transform parent, Item item, float left, float top, float w)
        {
            return BuildItem(parent, item, left, top, w, out float _);
        }

        // Tarjeta de un error: franja de color (rojo grave, ambar error, verde
        // sin errores), titulo y que hacer. Devuelve el alto.
        private static GameObject BuildItem(Transform parent, Item item, float left, float top, float w,
            out float height)
        {
            GameObject go = new GameObject("Mejora");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, false);
            Transform t = go.transform;

            Color color = item.severity == BurnSequenceManager.Severity.Critical ? Red
                : item.severity == BurnSequenceManager.Severity.Error ? Amber : Green;
            const float pad = 0.014f;
            float textLeft = left + 0.026f;
            float textW = w - 0.026f - pad;

            string tag = item.severity == BurnSequenceManager.Severity.Critical ? "GRAVE  ·  " : "";
            string times = item.count > 1 ? $"  <color=#9AA6BA>({item.count} veces)</color>" : "";
            float y = top - pad;
            y -= Wrapped(t, $"<b>{tag}{item.title}</b>{times}", new Vector2(textLeft, y), 0.019f, color, textW) + 0.006f;
            y -= Wrapped(t, item.explanation, new Vector2(textLeft, y), 0.016f, Soft, textW);
            height = top - y + pad;

            RoundedPanel card = RoundedPanel.Create(t, "FondoMejora", new Vector2(left + w / 2f, top - height / 2f),
                new Vector2(w, height), 0.0005f, 0.014f, 0.001f, 0f);
            card.top = new Color(0.19f, 0.24f, 0.37f, 0.9f);
            card.bottom = new Color(0.15f, 0.19f, 0.31f, 0.9f);
            card.borderLeft = new Color(color.r, color.g, color.b, 0.5f);
            card.borderRight = new Color(0.45f, 0.55f, 0.8f, 0.3f);
            card.sheen = 0.02f;
            card.Push();

            RoundedPanel stripe = RoundedPanel.Create(t, "Franja", new Vector2(left + 0.011f, top - height / 2f),
                new Vector2(0.006f, height - 0.024f), -0.0003f, 0.003f, 0f, 0.004f);
            stripe.sheen = 0f;
            stripe.SetSolid(color, 0.3f);
            return go;
        }

        private void BuildPager(Transform t, float centerX, float y)
        {
            Vector2 size = new Vector2(0.13f, 0.045f);
            Color color = new Color(0.25f, 0.33f, 0.5f);
            previous = VRPanelKit.RoundedButton(t, "Anterior", new Vector2(centerX - 0.15f, y), size, color,
                () => ShowPage(page - 1));
            next = VRPanelKit.RoundedButton(t, "Siguiente", new Vector2(centerX + 0.15f, y), size, color,
                () => ShowPage(page + 1));
            pageLabel = VRPanelKit.Text(t, "", new Vector2(centerX, y + 0.011f), 0.017f, Soft,
                TextAnchor.UpperCenter, TextAlignment.Center);
        }

        private void ShowPage(int index)
        {
            if (pages.Count == 0)
            {
                return;
            }

            page = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == page);
            }

            if (pageLabel != null)
            {
                pageLabel.text = $"{page + 1} de {pages.Count}";
            }

            if (previous != null)
            {
                previous.gameObject.SetActive(page > 0);
            }

            if (next != null)
            {
                next.gameObject.SetActive(page < pages.Count - 1);
            }
        }

        // Titulo corto de cada error: que paso, en pocas palabras.
        private static string MistakeTitle(BurnProtocol.Mistake m)
        {
            switch (m.burnEvent)
            {
                case BurnSequenceManager.BurnEvent.HotWaterOnBurn: return "Agua caliente sobre la quemadura";
                case BurnSequenceManager.BurnEvent.CoolingInterrupted: return "Sacó la mano del agua antes de tiempo";
                case BurnSequenceManager.BurnEvent.RingStuck: return "El anillo se trabó";
                case BurnSequenceManager.BurnEvent.CoveredTooEarly: return "Cubriste antes de terminar de enfriar";
                case BurnSequenceManager.BurnEvent.ClothOnBurn: return "Paño de cocina sobre la quemadura";
                case BurnSequenceManager.BurnEvent.FilmWrapped: return "Envolviste la mano con el film";
                case BurnSequenceManager.BurnEvent.BurnedHandGrabbed: return "La tomaste de la mano quemada";
                case BurnSequenceManager.BurnEvent.HotPotTouched: return "Tocaste la olla caliente";
                case BurnSequenceManager.BurnEvent.PatientAlarmed: return "La asustaste";
                case BurnSequenceManager.BurnEvent.HotSpillStepped: return "Pisaste el agua derramada";
                case BurnSequenceManager.BurnEvent.BadAdvice: return "Le recomendaste " + RemedyName(m.detail);
                case BurnSequenceManager.BurnEvent.RemedyNear: return "Acercaste " + RemedyName(m.detail);
                case BurnSequenceManager.BurnEvent.RemedyApplied: return "Le pusiste " + RemedyName(m.detail);
                case BurnSequenceManager.BurnEvent.MedicalDecision: return "Decisión médica equivocada";
                case BurnSequenceManager.BurnEvent.WrongCallInfo: return "Dato equivocado al 106";
                case BurnSequenceManager.BurnEvent.StepMissed:
                    if (System.Enum.TryParse(m.detail, out BurnProtocol.Step step))
                    {
                        return "Faltó: " + ShortStepName(step).ToLowerInvariant();
                    }

                    return "Faltó un paso";
            }

            return "Error";
        }

        private static string RemedyName(string remedy)
        {
            switch (remedy)
            {
                case "Hielo": return "hielo";
                case "Mantequilla": return "mantequilla";
                case "Aceite": return "aceite";
                case "PastaDental": return "pasta dental";
                case "Algodon": return "algodón";
                case "Pomada": return "pomada";
                case "Curita": return "una curita";
            }

            return "un remedio casero";
        }

        // ---------------- utilidades ----------------

        // Texto que se ajusta a 'width' (TextMesh Pro) con la esquina de
        // arriba a la izquierda en 'position'. Devuelve su alto.
        private static float Wrapped(Transform parent, string text, Vector2 position, float lineHeight, Color color,
            float width)
        {
            TextMesh mesh = VRPanelKit.Text(parent, text, position, lineHeight, color);
            PanelText panelText = mesh.GetComponent<PanelText>();
            if (panelText == null)
            {
                return lineHeight;
            }

            panelText.Init();
            TextMeshPro tmp = panelText.Tmp;
            tmp.enableWordWrapping = true;
            tmp.rectTransform.sizeDelta = new Vector2(width, 0f);
            Vector2 preferred = tmp.GetPreferredValues(text, width, 0f);
            return Mathf.Max(lineHeight, preferred.y);
        }

        private static string Plural(int n, string one, string many)
        {
            return n + " " + (n == 1 ? one : many);
        }

        // Tarjeta interior, un poco mas clara que el fondo y sin brillo.
        private static void Card(Transform t, string name, Vector2 center, Vector2 size)
        {
            RoundedPanel card = RoundedPanel.Create(t, name, center, size, 0.001f, 0.02f, 0.0012f, 0f);
            card.top = new Color(0.15f, 0.2f, 0.32f, 0.85f);
            card.bottom = new Color(0.11f, 0.15f, 0.26f, 0.85f);
            card.borderLeft = new Color(0.45f, 0.6f, 0.9f, 0.45f);
            card.borderRight = new Color(0.6f, 0.5f, 0.95f, 0.45f);
            card.sheen = 0.03f;
            card.Push();
        }

        private static void Restart(BurnCase next)
        {
            BurnCase.Next = next;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
