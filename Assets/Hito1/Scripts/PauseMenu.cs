using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Pausa y reinicio en cualquier momento (E.10, RNF-03). Se abre con el
    // boton de menu del control izquierdo o manteniendo B o Y un segundo;
    // con las manos, con el boton "Pausa" de la muneca izquierda
    // (WristPauseButton; el gesto de menu del sistema tambien llega como
    // Button.Start, pero no se confirmo en el visor). El tutorial ensena a
    // abrirla (BurnGuide.PauseTutorial). En el editor tambien con Escape o P y, en Windows, con F9
    // (pausa) y F8 (terminar y ver resumen) aunque el teclado lo tenga la
    // ventana del simulador de Meta.
    // Pausado: el tiempo se detiene (Time.timeScale = 0: el reloj, el
    // enfriamiento y la quemadura no avanzan) y se pausa el sonido. El panel
    // tiene siempre las mismas tres opciones: "Continuar" (la principal),
    // "Reiniciar" (al panel de modo guiado o evaluacion, con el mismo caso) y
    // "Terminar" (pasa al resumen: si el jugador se quedo atrapado, ve igual
    // que le falto; vale tambien antes del accidente). No hay pausa en el
    // panel de inicio ni en el resumen. Los botones funcionan sin fisica
    // (TouchButton mira la posicion de la mano si el tiempo esta detenido).
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

        [Tooltip("Segundos manteniendo B o Y para abrir o cerrar la pausa (el boton de menu no siempre llega desde el simulador).")]
        public float holdToPause = 1f;
        private float holdTime;
        private bool holdFired;

#if UNITY_EDITOR_WIN
        // Teclas leidas directo de Windows: llegan aunque la ventana activa
        // sea la del simulador de Meta (que se queda con el teclado y Unity
        // no ve Escape, P ni los atajos de su menu).
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        private const int KeyF8 = 0x77, KeyF9 = 0x78;
        private readonly bool[] keyWasDown = new bool[2];

        private bool KeyPressed(int index, int key)
        {
            bool down = (GetAsyncKeyState(key) & 0x8000) != 0;
            bool pressed = down && !keyWasDown[index];
            keyWasDown[index] = down;
            return pressed;
        }
#endif

        private void Update()
        {
            bool toggle = OVRInput.GetDown(OVRInput.Button.Start);

            // Mantener B o Y (cualquiera de los dos controles).
            if (OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.Touch))
            {
                holdTime += Time.unscaledDeltaTime;
                if (holdTime >= holdToPause && !holdFired)
                {
                    holdFired = true;
                    toggle = true;
                }
            }
            else
            {
                holdTime = 0f;
                holdFired = false;
            }
#if UNITY_EDITOR
            toggle |= Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
#endif
#if UNITY_EDITOR_WIN
            toggle |= KeyPressed(0, KeyF9);
            if (KeyPressed(1, KeyF8))
            {
                // F8: terminar y ver el resumen directamente.
                if (CanFinish)
                {
                    FinishNow();
                }

                return;
            }
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

        // Hay pausa desde que se elige el modo hasta el resumen. En el panel
        // de inicio todavia no empezo nada, y el resumen ya trae sus botones.
        public static bool CanPause
        {
            get
            {
                BurnGuide guide = BurnGuide.Instance;
                BurnProtocol protocol = BurnProtocol.Instance;
                return (guide == null || !guide.ChoosingMode) && (protocol == null || !protocol.Finished);
            }
        }

        public void Pause()
        {
            if (Paused || !CanPause)
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
            // Vuelve al inicio con el mismo caso.
            if (BurnCase.Current != null)
            {
                BurnCase.Next = BurnCase.Current.Copy();
            }

            Debug.Log("[PauseMenu] Volver al inicio");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }

        // Termina la practica aqui y muestra el resumen con lo que falto
        // (para no quedarse atrapado si una interaccion no sale).
        public void FinishNow()
        {
            Resume();
            if (BurnProtocol.Instance != null)
            {
                BurnProtocol.Instance.EndEarly("Menu");
            }
        }

        // Siempre que hay pausa se puede pasar al final (tambien antes del
        // accidente: todo lo que falte sale en el resumen).
        private static bool CanFinish
        {
            get
            {
                BurnProtocol protocol = BurnProtocol.Instance;
                return CanPause && protocol != null && !protocol.Finished;
            }
        }

        // Siempre las mismas tres opciones. "Continuar" es la principal:
        // grande, arriba y del color de accion. "Reiniciar" (volver a elegir
        // modo guiado o evaluacion, con el mismo caso) y "Terminar" (pasar al
        // resumen) quedan abajo, chicas y apagadas, para no invitar a saltarse
        // la practica. Sin textos chicos de ayuda: en el visor nadie los lee
        // (pedido tras la prueba en el visor, 2026-11-17).
        private void BuildPanel()
        {
            Vector2 size = new Vector2(0.44f, 0.34f);
            panel = VRPanelKit.CreateRoot("PanelPausa", 0.6f, 0.1f);
            RoundedPanel bg = RoundedPanel.Create(panel, "FondoPausa", Vector2.zero, size, 0.002f, 0.04f, 0.0025f,
                0.02f);
            bg.top = new Color(0.1f, 0.14f, 0.24f, 0.97f);
            bg.bottom = new Color(0.05f, 0.07f, 0.14f, 0.97f);
            bg.Push();
            VRPanelKit.Text(panel, "<b>Pausa</b>", new Vector2(0f, 0.145f), 0.042f, Color.white,
                TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(panel, "El tiempo está detenido", new Vector2(0f, 0.09f), 0.022f,
                VRPanelKit.CardHintColor, TextAnchor.UpperCenter, TextAlignment.Center);

            VRPanelKit.RoundedButton(panel, "Continuar", new Vector2(0f, -0.005f), new Vector2(0.36f, 0.11f),
                new Color(0.22f, 0.5f, 0.98f), Resume);

            // Secundarias: relleno gris azulado, sin brillo ni borde de color.
            Color muted = new Color(0.2f, 0.24f, 0.32f);
            Mute(VRPanelKit.RoundedButton(panel, "Reiniciar", new Vector2(-0.095f, -0.115f),
                new Vector2(0.18f, 0.068f), muted, Restart));
            // Lo que falte cuenta como paso no hecho en el resumen.
            Mute(VRPanelKit.RoundedButton(panel, "Terminar", new Vector2(0.095f, -0.115f),
                new Vector2(0.18f, 0.068f), muted, FinishNow));
            PanelFollow.Add(panel, 0.6f, 0.1f, size);

            // Recien abierto, la mano puede estar justo donde aparece: igual
            // espera un poco antes de aceptar un toque.
            foreach (TouchButton button in panel.GetComponentsInChildren<TouchButton>())
            {
                button.armDelay = 0.4f;
            }
        }

        // Boton secundario: sin el brillo ni el borde celeste a morado de los
        // botones principales.
        private static void Mute(TouchButton button)
        {
            RoundedPanel rounded = button != null ? button.GetComponentInChildren<RoundedPanel>() : null;
            if (rounded == null)
            {
                return;
            }

            Color edge = new Color(0.45f, 0.5f, 0.6f, 0.6f);
            rounded.borderLeft = edge;
            rounded.borderRight = edge;
            rounded.glowStrength = 0.08f;
            rounded.Push();
        }
    }
}
