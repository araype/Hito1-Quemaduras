using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Guarda en Resources/Hito1FX/PalancaGrifo.prefab la malla y los
    // materiales de la palanca del grifo de la cocina (Faucet_Handle, dentro
    // de Kitchen_01.fbx). Al dar Play, FaucetLever pone una copia que se
    // mueve en lugar de la original, que es estatica: con el batching
    // estatico su malla queda unida a las demas y ya no se puede leer ni
    // mover. No cambia la escena.
    public static class FaucetLeverSetup
    {
        public const string PrefabPath = "Assets/Hito1/Resources/Hito1FX/PalancaGrifo.prefab";
        private const string ModelPath = "Assets/Hito1/Kits/Apartment Kit/Common/Models/Kitchen_01.fbx";

        [MenuItem("Hito1/Preparar palanca del grifo")]
        public static void Run()
        {
            Mesh mesh = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (o is Mesh m && m.name == "Faucet_Handle")
                {
                    mesh = m;
                    break;
                }
            }

            if (mesh == null)
            {
                Debug.LogError("[Hito1] No se encontro la malla Faucet_Handle en " + ModelPath);
                return;
            }

            Material chrome = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Hito1/Kits/Apartment Kit/Common/Materials/Surface/Brushed Metal_Chrome.mat");

            GameObject go = new GameObject("PalancaGrifo");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = chrome;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
            Debug.Log("[Hito1] Palanca del grifo guardada en " + PrefabPath);
        }
    }
}
