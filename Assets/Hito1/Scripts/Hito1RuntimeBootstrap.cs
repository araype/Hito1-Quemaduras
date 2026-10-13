using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Instala en runtime las correcciones de manos/controles/colliders sin
    // tener que editar la escena a mano. Solo actua si la escena trae el
    // jugador con VRLocomotion (o sea, el escenario VR), y no duplica nada si
    // los componentes ya estan puestos en la escena. Se repite en cada escena
    // que se carga (SceneCycler pasa de un escenario a otro).
    public static class Hito1RuntimeBootstrap
    {
        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += (scene, mode) => InstallInCurrentScene();
            }

            InstallInCurrentScene();
        }

        private static void InstallInCurrentScene()
        {
            if (Object.FindObjectOfType<VRLocomotion>() == null)
            {
                return;
            }

            GameObject go = null;

            if (Object.FindObjectOfType<HandsControllersUnifier>() == null)
            {
                go = go != null ? go : new GameObject("Hito1Runtime");
                go.AddComponent<HandsControllersUnifier>();
            }

            if (Object.FindObjectOfType<AutoColliders>() == null)
            {
                go = go != null ? go : new GameObject("Hito1Runtime");
                go.AddComponent<AutoColliders>();
            }

            if (Object.FindObjectOfType<SceneCycler>() == null)
            {
                go = go != null ? go : new GameObject("Hito1Runtime");
                go.AddComponent<SceneCycler>();
            }
        }
    }
}
