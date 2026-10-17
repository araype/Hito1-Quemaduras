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

            InstallBurnScene();
        }

        // Escena de quemadura: aspecto, fisica de la tela, agua, reacciones
        // de la paciente y sonido. Cada componente se puede poner a mano en la
        // escena para ajustar sus valores; si ya esta, no se duplica.
        private static void InstallBurnScene()
        {
            BurnSequenceManager burn = Object.FindObjectOfType<BurnSequenceManager>();
            if (burn == null)
            {
                return;
            }

            if (Object.FindObjectOfType<KitchenAmbience>() == null)
            {
                burn.gameObject.AddComponent<KitchenAmbience>();
            }

            foreach (WaterTap tap in Object.FindObjectsOfType<WaterTap>())
            {
                Transform parent = tap.transform.parent;
                GameObject water = tap.water != null ? tap.water.gameObject : (parent != null ? parent.gameObject : null);
                if (water != null && water.GetComponent<WaterStreamFX>() == null)
                {
                    water.AddComponent<WaterStreamFX>().tap = tap;
                }
            }

            foreach (FirstAidItem item in Object.FindObjectsOfType<FirstAidItem>())
            {
                if (item.type == FirstAidItem.ItemType.Panuelo && item.GetComponent<Rigidbody>() != null
                    && item.GetComponent<BoxCollider>() != null && item.GetComponent<TowelCloth>() == null)
                {
                    item.gameObject.AddComponent<TowelCloth>();
                }
            }

            foreach (Rigidbody rb in Object.FindObjectsOfType<Rigidbody>())
            {
                if (rb.name.Contains("Olla") && rb.GetComponent<PotFX>() == null)
                {
                    rb.gameObject.AddComponent<PotFX>();
                }
            }

            foreach (PatientBody patient in Object.FindObjectsOfType<PatientBody>())
            {
                if (patient.GetComponent<BurnSkinOverlay>() == null)
                {
                    patient.gameObject.AddComponent<BurnSkinOverlay>().burnSequence = burn;
                }

                if (patient.GetComponent<BurnPatientReactions>() == null)
                {
                    patient.gameObject.AddComponent<BurnPatientReactions>().burnSequence = burn;
                }
            }
        }
    }
}
