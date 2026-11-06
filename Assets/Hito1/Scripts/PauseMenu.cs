using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Pausa y reinicio en cualquier momento (E.10, RNF-03). Se abre con el
    // boton de menu del control izquierdo (con las manos, el gesto de menu
    // de la mano izquierda: pellizco mirando la palma). En el editor tambien
    // con Escape o P.
    // Pausado: el tiempo se detiene (Time.timeScale = 0: el reloj, el
    // enfriamiento y la quemadura no avanzan) y se pausa el sonido. El panel
    // tiene "Continuar" y "Reiniciar práctica" (vuelve al menu de inicio con
    // el mismo caso). Los botones funcionan sin fisica (TouchButton mira la
    // posicion de la mano si el tiempo esta detenido).
    // Va en el objeto de BurnSequenceManager.
    public class PauseMenu : MonoBehaviour
    {
        public static bool Paused { get; private set; }

        private Transform panel;
        private float pausedAt;

        private void OnDestroy()
        {
            // Al recargar la escena no puede quedar el tiempo detenido.
            if (Paused)
            {
                SetPaused(false);
            }
        }

        private void Update()
        {
            bool toggle = OVRInput.GetDown(OVRInput.Button.Start);
#if UNITY_EDITOR
            toggle |= Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
            if (!toggle)
            {
                return;
            }

            if (Paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        public void Pause()
        {
            if (Paused)
            {
                return;
            }

            SetPaused(true);
            pausedAt = Time.unscaledTime;
            BuildPanel();
            Debug.Log("[PauseMenu] Pausa");
        }

        public void Resume()
        {
            if (!Paused)
            {
                return;
            }

            SetPaused(false);
            if (panel != null)
            {
                Destroy(panel.gameObject);
                panel = null;
            }

            Debug.Log($"[PauseMenu] Continuar ({Time.unscaledTime - pausedAt:F1} s en pausa)");
        }

        public void Restart()
        {
            SetPaused(false);
            if (BurnInjury.Instance != null)
            {
                BurnInjury.NextDegree = BurnInjury.Instance.degree;
            }

            Debug.Log("[PauseMenu] Reiniciar");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        private void BuildPanel()
        {
            Vector2 size = new Vector2(0.5f, 0.3f);
            panel = VRPanelKit.CreateRoot("PanelPausa", 0.6f, 0.1f);
            RoundedPanel bg = RoundedPanel.Create(panel, "FondoPausa", Vector2.zero, size, 0.002f, 0.04f, 0.0025f,
                0.02f);
            bg.top = new Color(0.1f, 0.14f, 0.24f, 0.97f);
            bg.bottom = new Color(0.05f, 0.07f, 0.14f, 0.97f);
            bg.Push();
            VRPanelKit.Text(panel, "<b>Práctica en pausa</b>", new Vector2(0f, 0.115f), 0.032f, Color.white,
                TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(panel, "El reloj y la quemadura están detenidos.", new Vector2(0f, 0.06f), 0.018f,
                new Color(0.8f, 0.85f, 0.93f), TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.RoundedButton(panel, "Continuar", new Vector2(-0.115f, -0.03f), new Vector2(0.21f, 0.08f),
                new Color(0.22f, 0.48f, 0.95f), Resume);
            VRPanelKit.RoundedButton(panel, "Reiniciar práctica", new Vector2(0.115f, -0.03f),
                new Vector2(0.21f, 0.08f), new Color(0.45f, 0.3f, 0.62f), Restart);
            VRPanelKit.Text(panel, "Deja la mano sobre un botón. Menú izquierdo: continuar.",
                new Vector2(0f, -0.095f), 0.014f, new Color(0.6f, 0.66f, 0.76f), TextAnchor.UpperCenter,
                TextAlignment.Center);
            PanelFollow.Add(panel, 0.6f, 0.1f, size);

            // Recien abierto, la mano puede estar justo donde aparece: igual
            // espera un poco antes de aceptar un toque.
            foreach (TouchButton button in panel.GetComponentsInChildren<TouchButton>())
            {
                button.armDelay = 0.4f;
            }
        }
    }
}
