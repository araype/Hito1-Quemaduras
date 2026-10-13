using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Pasa al siguiente escenario del build (quemadura -> atragantamiento ->
    // corte -> quemadura) con el boton de menu del control izquierdo (o el
    // gesto de menu de la mano izquierda). Es provisional hasta que exista la
    // pantalla de inicio para elegir escenario (Hito 5). En el editor tambien
    // sirve la tecla N.
    public class SceneCycler : MonoBehaviour
    {
        public float cooldownSeconds = 1.5f;

        private float nextAllowedTime;

        private void Start()
        {
            nextAllowedTime = Time.unscaledTime + cooldownSeconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextAllowedTime)
            {
                return;
            }

            bool pressed = OVRInput.GetDown(OVRInput.Button.Start);
#if UNITY_EDITOR
            pressed |= Input.GetKeyDown(KeyCode.N);
#endif
            if (!pressed)
            {
                return;
            }

            int count = SceneManager.sceneCountInBuildSettings;
            if (count <= 1)
            {
                return;
            }

            nextAllowedTime = Time.unscaledTime + cooldownSeconds;
            int next = (SceneManager.GetActiveScene().buildIndex + 1) % count;
            Debug.Log("[Hito1] Cambiando al escenario " + next);
            SceneManager.LoadScene(next);
        }
    }
}
