using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Resumen de la practica (RF-10), frente al jugador al terminar. Mismo
    // estilo que las tarjetas de la guia (panel de vidrio redondeado):
    //   - encabezado: modo, titulo, grado de la quemadura y el puntaje;
    //   - cuatro indicadores: primera accion, enfriamiento, errores y
    //     errores criticos;
    //   - dos columnas: los pasos del protocolo (hechos o faltantes) y la
    //     decision medica con que mejorar;
    //   - boton para repetir la practica.
    public class BurnSummaryPanel : MonoBehaviour
    {
        private static readonly Color Green = new Color(0.36f, 0.84f, 0.54f);
        private static readonly Color Red = new Color(0.94f, 0.44f, 0.4f);
        private static readonly Color Amber = new Color(0.96f, 0.76f, 0.42f);
        private static readonly Color Accent = new Color(0.62f, 0.8f, 1f);
        private static readonly Color Soft = new Color(0.8f, 0.85f, 0.93f);
        private static readonly Color Muted = new Color(0.6f, 0.66f, 0.76f);

        private const float Width = 1.0f;
        private const float Height = 0.8f;
        private const float Pad = 0.045f;

        public static BurnSummaryPanel Show(BurnProtocol protocol)
        {
            Transform root = VRPanelKit.CreateRoot("PanelResumen", 1.0f, 0.12f);
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

            // ---------------- encabezado ----------------
            bool guided = BurnGuide.Instance == null || BurnGuide.Instance.Guided;
            string degree = p.Degree == BurnInjury.BurnDegree.Primero ? "primer grado" : "segundo grado superficial";
            VRPanelKit.Text(t, "PRÁCTICA TERMINADA  ·  " + (guided ? "MODO GUIADO" : "MODO EVALUACIÓN"),
                new Vector2(left, top), 0.017f, Accent);
            VRPanelKit.Text(t, "<b>RESUMEN DE LA PRÁCTICA</b>", new Vector2(left, top - 0.03f), 0.046f, Color.white);
            VRPanelKit.Text(t, "Quemadura de " + degree, new Vector2(left, top - 0.09f), 0.02f, Soft);

            Color scoreColor = p.Score >= 80 ? Green : (p.Score >= 50 ? Amber : Red);
            Vector2 chipCenter = new Vector2(right - 0.1f, top - 0.055f);
            RoundedPanel chip = RoundedPanel.Create(t, "Puntaje", chipCenter, new Vector2(0.2f, 0.115f), 0.001f,
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

            // ---------------- indicadores ----------------
            float tileTop = top - 0.15f;
            const float tileH = 0.105f;
            const float gap = 0.02f;
            float tileW = (Width - 2f * Pad - 3f * gap) / 4f;
            bool fullCooling = p.SimulatedCoolingMinutes >= p.simulatedMinutes - 0.5f;
            Tile(t, 0, left, tileTop, tileW, tileH, gap, Seconds(p.FirstActionDelay), "Primera acción", Color.white);
            Tile(t, 1, left, tileTop, tileW, tileH, gap,
                $"{p.SimulatedCoolingMinutes:F0}/{p.simulatedMinutes:F0} min", "Enfriamiento",
                fullCooling ? Green : Amber);
            Tile(t, 2, left, tileTop, tileW, tileH, gap, burn.Errors.ToString(), "Errores",
                burn.Errors > 0 ? Amber : Green);
            Tile(t, 3, left, tileTop, tileW, tileH, gap, burn.CriticalErrors.ToString(), "Errores críticos",
                burn.CriticalErrors > 0 ? Red : Green);

            // ---------------- columnas ----------------
            float colTop = tileTop - tileH - 0.022f;
            float colBottom = -Height / 2f + Pad + 0.105f;
            float colW = (Width - 2f * Pad - gap) / 2f;
            float colH = colTop - colBottom;
            Card(t, "Pasos", new Vector2(left + colW / 2f, colTop - colH / 2f), new Vector2(colW, colH));
            Card(t, "Decision", new Vector2(right - colW / 2f, colTop - colH / 2f), new Vector2(colW, colH));

            // Pasos del protocolo: punto verde (hecho) o rojo (falto).
            float cx = left + 0.022f;
            float y = colTop - 0.022f;
            VRPanelKit.Text(t, "<b>PASOS DEL PROTOCOLO</b>", new Vector2(cx, y), 0.016f, Accent);
            y -= 0.038f;
            foreach (BurnProtocol.Step step in System.Enum.GetValues(typeof(BurnProtocol.Step)))
            {
                bool ok = p.IsDone(step);
                Color c = ok ? Green : Red;
                RoundedPanel dot = RoundedPanel.Create(t, "EstadoPaso", new Vector2(cx + 0.006f, y - 0.0095f),
                    Vector2.one * 0.012f, -0.0005f, 0.006f, 0f, 0.005f);
                dot.sheen = 0f;
                dot.SetSolid(c, 0.4f);
                VRPanelKit.Text(t, BurnProtocol.StepNames[(int)step], new Vector2(cx + 0.024f, y), 0.019f,
                    Color.white);
                VRPanelKit.Text(t, ok ? "Hecho" : "Faltó", new Vector2(left + colW - 0.022f, y - 0.002f), 0.016f, c,
                    TextAnchor.UpperRight, TextAlignment.Right);
                y -= 0.031f;
            }

            // Decision medica y que mejorar.
            float rx = right - colW + 0.022f;
            y = colTop - 0.022f;
            VRPanelKit.Text(t, "<b>DECISIÓN MÉDICA</b>", new Vector2(rx, y), 0.016f, Accent);
            y -= 0.036f;
            string decision = p.Decision == "CentroDeSalud" ? "Ir al centro de salud" : "Cuidarla en casa";
            VRPanelKit.Text(t, decision, new Vector2(rx, y), 0.021f, Color.white);
            VRPanelKit.Text(t, p.DecisionCorrect ? "Correcta" : "Incorrecta",
                new Vector2(right - 0.022f, y - 0.002f), 0.016f, p.DecisionCorrect ? Green : Red,
                TextAnchor.UpperRight, TextAlignment.Right);
            y -= 0.05f;

            VRPanelKit.Text(t, "<b>QUÉ MEJORAR</b>", new Vector2(rx, y), 0.016f, Accent);
            y -= 0.032f;
            VRPanelKit.Text(t, Tips(p), new Vector2(rx, y), 0.0175f, Soft);

            // ---------------- boton ----------------
            float buttonY = -Height / 2f + Pad + 0.045f;
            VRPanelKit.RoundedButton(t, "Repetir práctica", new Vector2(0f, buttonY), new Vector2(0.34f, 0.075f),
                new Color(0.22f, 0.48f, 0.95f), Restart);
            VRPanelKit.Text(t, "Deja la mano sobre el botón para repetir", new Vector2(0f, buttonY - 0.05f), 0.015f,
                Muted, TextAnchor.UpperCenter, TextAlignment.Center);

            // Credito que pide el plan gratis de ElevenLabs (docs/VOCES.md).
            VRPanelKit.Text(t, "Voces: elevenlabs.io", new Vector2(right, -Height / 2f + 0.03f), 0.012f, Muted,
                TextAnchor.UpperRight, TextAlignment.Right);
        }

        // Indicador: valor grande arriba y etiqueta chica abajo.
        private static void Tile(Transform t, int index, float left, float top, float w, float h, float gap,
            string value, string label, Color valueColor)
        {
            float cx = left + w / 2f + index * (w + gap);
            Vector2 center = new Vector2(cx, top - h / 2f);
            Card(t, "Indicador", center, new Vector2(w, h));
            VRPanelKit.Text(t, "<b>" + value + "</b>", new Vector2(cx, top - 0.02f), 0.036f, valueColor,
                TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(t, label, new Vector2(cx, top - h + 0.03f), 0.016f, Muted,
                TextAnchor.UpperCenter, TextAlignment.Center);
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

        // Hasta tres consejos (uno por tipo de error), sin pasar de 6 lineas.
        private static string Tips(BurnProtocol p)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (BurnProtocol.Mistake m in p.Mistakes)
            {
                if (!counts.ContainsKey(m.explanation))
                {
                    counts[m.explanation] = 0;
                    order.Add(m.explanation);
                }

                counts[m.explanation]++;
            }

            if (order.Count == 0)
            {
                return "Seguiste el protocolo sin errores.\n¡Muy bien!";
            }

            List<string> lines = new List<string>();
            int lineCount = 0;
            foreach (string explanation in order)
            {
                string times = counts[explanation] > 1 ? $" (x{counts[explanation]})" : "";
                string tip = VRPanelKit.Wrap("• " + explanation + times, 44);
                int n = tip.Split('\n').Length;
                if (lines.Count >= 3 || lineCount + n > 6)
                {
                    break;
                }

                lines.Add(tip);
                lineCount += n;
            }

            return VRPanelKit.Join(lines);
        }

        private static string Seconds(float s)
        {
            return s < 0f ? "-" : s.ToString("F1") + " s";
        }

        private static void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
