using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Menu Hito1 > Practica del editor, para probar con el simulador de Meta
    // (alli el boton de menu del control y las teclas Escape/P no siempre
    // llegan al juego: el simulador se queda con el teclado). Con el mouse en
    // la barra de menus de Unity siempre funciona. Las teclas F9 (pausa) y F8
    // (terminar) las lee PauseMenu directo de Windows.
    public static class PracticeMenu
    {
        [MenuItem("Hito1/Práctica/Pausar o continuar")]
        private static void TogglePause()
        {
            PauseMenu menu = Object.FindObjectOfType<PauseMenu>();
            if (menu == null)
            {
                Debug.LogWarning("[Hito1] No hay menú de pausa en la escena.");
                return;
            }

            if (PauseMenu.Paused)
            {
                menu.Resume();
            }
            else
            {
                menu.Pause();
            }
        }

        [MenuItem("Hito1/Práctica/Terminar y ver resumen")]
        private static void FinishNow()
        {
            if (BurnProtocol.Instance == null)
            {
                Debug.LogWarning("[Hito1] No hay práctica en curso.");
                return;
            }

            PauseMenu menu = Object.FindObjectOfType<PauseMenu>();
            if (menu != null)
            {
                menu.FinishNow();
            }
            else
            {
                BurnProtocol.Instance.EndEarly("Menu");
            }
        }

        [MenuItem("Hito1/Práctica/Pausar o continuar", true)]
        [MenuItem("Hito1/Práctica/Terminar y ver resumen", true)]
        private static bool InPlayMode()
        {
            return Application.isPlaying;
        }
    }
}
