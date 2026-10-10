using UnityEngine;

namespace SojaExiles
{
    // Instala en runtime las correcciones de manos/controles/colliders sin
    // tener que editar la escena a mano. Solo actua si la escena trae el
    // jugador con VRLocomotion (o sea, el escenario VR), y no duplica nada si
    // los componentes ya estan puestos en la escena.
    public static class Hito1RuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindObjectOfType<VRLocomotion>() == null)
            {
                return;
            }

            if (Object.FindObjectOfType<HandsControllersUnifier>() != null)
            {
                return;
            }

            GameObject go = new GameObject("Hito1Runtime");
            go.AddComponent<HandsControllersUnifier>();

            if (Object.FindObjectOfType<AutoColliders>() == null)
            {
                go.AddComponent<AutoColliders>();
            }
        }
    }
}
