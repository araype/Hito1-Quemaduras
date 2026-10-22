using UnityEngine;
using UnityEngine.SceneManagement;

namespace SojaExiles
{
    // Instala en runtime las correcciones de manos/controles/colliders sin
    // tener que editar la escena a mano. Solo actua si la escena trae el
    // jugador con VRLocomotion (o sea, el escenario VR), y no duplica nada si
    // los componentes ya estan puestos en la escena. Se repite cada vez que
    // la escena se carga (por ejemplo, al reiniciar).
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

            InstallBurnScene();
        }

        // Escena de quemadura: aspecto, fisica de la tela, agua, reacciones
        // de la paciente y sonido; el accidente de la olla, la evolucion de la
        // quemadura, las dos llaves del grifo, los objetos del protocolo, los
        // remedios caseros y la vibracion. Cada componente se puede poner a
        // mano en la escena para ajustar sus valores; si ya esta, no se
        // duplica.
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

            if (Object.FindObjectOfType<BurnHaptics>() == null)
            {
                burn.gameObject.AddComponent<BurnHaptics>().burnSequence = burn;
            }

            if (Object.FindObjectOfType<BurnSceneProps>() == null)
            {
                burn.gameObject.AddComponent<BurnSceneProps>();
            }

            // Capa de secuencia: protocolo y puntaje, y el reloj de 20 min.
            if (Object.FindObjectOfType<BurnProtocol>() == null)
            {
                burn.gameObject.AddComponent<BurnProtocol>().burnSequence = burn;
            }

            if (Object.FindObjectOfType<CoolingClock>() == null)
            {
                burn.gameObject.AddComponent<CoolingClock>();
            }

            InstallHotTap();

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

                if (item.type == FirstAidItem.ItemType.Panuelo && item.GetComponent<TowelCloth>() != null
                    && item.GetComponent<TowelMisuse>() == null)
                {
                    item.gameObject.AddComponent<TowelMisuse>().burnSequence = burn;
                }
            }

            // La quemadura se cubre con film o gasa (CoverItem), no con el
            // pano de cocina: el detector del pano ya no la da por cubierta.
            foreach (BurnContactDetector detector in Object.FindObjectsOfType<BurnContactDetector>())
            {
                if (detector.requiredItem == FirstAidItem.ItemType.Panuelo)
                {
                    detector.enabled = false;
                }
            }

            foreach (Rigidbody rb in Object.FindObjectsOfType<Rigidbody>())
            {
                if (rb.name.Contains("Olla") && rb.GetComponent<PotFX>() == null)
                {
                    rb.gameObject.AddComponent<PotFX>();
                }

                if (rb.GetComponent<PotFX>() != null && rb.GetComponent<PotAccident>() == null)
                {
                    rb.gameObject.AddComponent<PotAccident>().burnSequence = burn;
                }
            }

            WaterTap anyTap = Object.FindObjectOfType<WaterTap>();
            foreach (PatientBody patient in Object.FindObjectsOfType<PatientBody>())
            {
                if (patient.GetComponent<BurnInjury>() == null)
                {
                    patient.gameObject.AddComponent<BurnInjury>().burnSequence = burn;
                }

                if (patient.GetComponent<PatientRing>() == null)
                {
                    PatientRing ring = patient.gameObject.AddComponent<PatientRing>();
                    ring.burnSequence = burn;
                    ring.indicatorMaterial = anyTap != null ? anyTap.indicatorMaterial : null;
                }

                if (patient.GetComponent<BurnSkinOverlay>() == null)
                {
                    patient.gameObject.AddComponent<BurnSkinOverlay>().burnSequence = burn;
                }

                if (patient.GetComponent<BurnPatientReactions>() == null)
                {
                    patient.gameObject.AddComponent<BurnPatientReactions>().burnSequence = burn;
                }

                if (patient.GetComponent<PatientWetHand>() == null)
                {
                    patient.gameObject.AddComponent<PatientWetHand>();
                }
            }

            InstallStoveKnob();
        }

        // Segunda llave del grifo, la del agua caliente, al lado de la que ya
        // esta (que queda como la fria). Comparten el chorro. Los puntos son
        // azul (fria) y rojo (caliente), como las marcas de una llave real.
        private static void InstallHotTap()
        {
            WaterTap cold = null;
            foreach (WaterTap tap in Object.FindObjectsOfType<WaterTap>())
            {
                if (tap.hot || tap.pair != null)
                {
                    return;
                }

                cold = cold != null ? cold : tap;
            }

            if (cold == null)
            {
                return;
            }

            cold.closedColor = new Color(0.25f, 0.55f, 1f, 0.7f);
            cold.openColor = new Color(0.55f, 0.85f, 1f, 0.45f);

            // Se arma desactivado para que el Awake de WaterTap ya tenga el
            // material del indicador.
            GameObject go = new GameObject("LlaveGrifoCaliente");
            go.SetActive(false);
            go.transform.SetParent(cold.transform.parent, false);
            go.transform.position = cold.transform.position - cold.transform.right * 0.09f;
            go.transform.rotation = cold.transform.rotation;
            SphereCollider own = cold.GetComponent<SphereCollider>();
            SphereCollider trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = own != null ? own.radius : 0.04f;

            WaterTap hot = go.AddComponent<WaterTap>();
            hot.hot = true;
            hot.pair = cold;
            hot.water = cold.water;
            hot.waterTrigger = cold.waterTrigger;
            hot.waterSound = cold.waterSound;
            hot.indicatorMaterial = cold.indicatorMaterial;
            hot.markerRadius = cold.markerRadius;
            hot.closedColor = new Color(1f, 0.28f, 0.2f, 0.7f);
            hot.openColor = new Color(1f, 0.55f, 0.35f, 0.45f);
            cold.pair = hot;
            go.SetActive(true);
        }

        // Perilla del kit (Knob.00X) mas cercana a la olla: la que apaga su
        // quemador.
        private static void InstallStoveKnob()
        {
            if (Object.FindObjectOfType<StoveKnob>() != null)
            {
                return;
            }

            PotFX pot = Object.FindObjectOfType<PotFX>();
            if (pot == null)
            {
                return;
            }

            Renderer best = null;
            float bestDistance = 0.9f;
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
            {
                if (!r.name.StartsWith("Knob"))
                {
                    continue;
                }

                float d = Vector3.Distance(r.bounds.center, pot.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = r;
                }
            }

            if (best == null)
            {
                Debug.LogWarning("[Hito1] No se encontro la perilla de la cocina cerca de la olla.");
                return;
            }

            StoveKnob knob = best.gameObject.AddComponent<StoveKnob>();
            knob.pot = pot;
            WaterTap tap = Object.FindObjectOfType<WaterTap>();
            if (tap != null)
            {
                knob.indicatorMaterial = tap.indicatorMaterial;
            }
        }
    }
}
