using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Resumen de la practica (RF-10), frente al jugador al terminar: puntaje,
    // pasos del protocolo hechos y faltantes, errores y errores criticos,
    // tiempo de primera accion, tiempo de enfriamiento, la decision medica y
    // que mejorar (una explicacion breve por cada tipo de error). Un boton
    // vuelve a empezar la practica.
    public class BurnSummaryPanel : MonoBehaviour
    {
        private const string Green = "#5BD68A";
        private const string Red = "#F07167";
        private const string Amber = "#F4C26B";

        public static BurnSummaryPanel Show(BurnProtocol protocol)
        {
            Transform root = VRPanelKit.CreateRoot("PanelResumen", 1.0f, 0.15f);
            BurnSummaryPanel panel = root.gameObject.AddComponent<BurnSummaryPanel>();
            panel.Build(protocol);
            return panel;
        }

        private void Build(BurnProtocol p)
        {
            Transform t = transform;
            BurnSequenceManager burn = p.burnSequence;
            const float width = 0.92f;
            const float height = 1.1f;
            const float left = -width / 2f + 0.05f;
            VRPanelKit.Background(t, Vector2.zero, new Vector2(width, height), "Panel_Fondo",
                new Color(0.07f, 0.1f, 0.16f), 0.001f);

            string scoreColor = p.Score >= 80 ? Green : (p.Score >= 50 ? Amber : Red);
            VRPanelKit.Text(t, "Resumen de la práctica", new Vector2(left, height / 2f - 0.045f), 0.042f, Color.white);
            VRPanelKit.Text(t, $"<color={scoreColor}><b>{p.Score}</b></color> / 100",
                new Vector2(width / 2f - 0.05f, height / 2f - 0.04f), 0.06f, Color.white, TextAnchor.UpperRight,
                TextAlignment.Right);

            string degree = p.Degree == BurnInjury.BurnDegree.Primero ? "primer grado" : "segundo grado superficial";
            List<string> facts = new List<string>
            {
                $"Quemadura de {degree}",
                $"Primera acción: {Seconds(p.FirstActionDelay)} después de la quemadura",
                $"Enfriamiento: {p.SimulatedCoolingMinutes:F0} de {p.simulatedMinutes:F0} min bajo el agua",
                $"Errores: <color={(burn.Errors > 0 ? Amber : Green)}>{burn.Errors}</color>    " +
                $"Errores críticos: <color={(burn.CriticalErrors > 0 ? Red : Green)}>{burn.CriticalErrors}</color>",
                "Decisión médica: " + (p.Decision == "CentroDeSalud" ? "ir al centro de salud" : "cuidarla en casa") +
                (p.DecisionCorrect ? $" <color={Green}>(correcta)</color>" : $" <color={Red}>(incorrecta)</color>")
            };
            VRPanelKit.Text(t, VRPanelKit.Join(facts), new Vector2(left, height / 2f - 0.125f), 0.026f,
                new Color(0.85f, 0.88f, 0.93f));

            List<string> steps = new List<string> { "<b>Pasos del protocolo</b>" };
            foreach (BurnProtocol.Step step in System.Enum.GetValues(typeof(BurnProtocol.Step)))
            {
                bool ok = p.IsDone(step);
                steps.Add($"<color={(ok ? Green : Red)}>•</color> {BurnProtocol.StepNames[(int)step]}" +
                    (ok ? "" : $" <color={Red}>(faltó)</color>"));
            }

            VRPanelKit.Text(t, VRPanelKit.Join(steps), new Vector2(left, height / 2f - 0.33f), 0.026f, Color.white);

            List<string> tips = new List<string> { "<b>Qué mejorar</b>" };
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

            int shown = 0;
            foreach (string explanation in order)
            {
                if (shown++ >= 4)
                {
                    break;
                }

                string times = counts[explanation] > 1 ? $" (x{counts[explanation]})" : "";
                tips.Add(VRPanelKit.Wrap("• " + explanation + times, 62));
            }

            if (order.Count == 0)
            {
                tips.Add($"<color={Green}>Seguiste el protocolo sin errores. ¡Muy bien!</color>");
            }

            VRPanelKit.Text(t, VRPanelKit.Join(tips), new Vector2(left, height / 2f - 0.6f), 0.022f,
                new Color(0.85f, 0.88f, 0.93f));

            VRPanelKit.Button(t, "Repetir práctica", new Vector2(0f, -height / 2f + 0.075f), new Vector2(0.3f, 0.09f),
                new Color(0.16f, 0.42f, 0.78f), Restart);
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
