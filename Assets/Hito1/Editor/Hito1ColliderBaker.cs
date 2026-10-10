using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SojaExiles
{
    // Hornea en la escena abierta los colliders que le faltan a las mallas
    // (muebles, paredes, piso). Es la version permanente de lo que
    // AutoColliders hace en runtime; usa MeshCollider para mallas grandes
    // aunque no tengan Read/Write, porque el editor las cocina en el build.
    public static class Hito1ColliderBaker
    {
        [MenuItem("Hito1/Agregar colliders faltantes (escena abierta)")]
        private static void BakeColliders()
        {
            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();

            int boxes = 0;
            int meshes = 0;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Hito1: agregar colliders faltantes");

            for (int i = 0; i < renderers.Length; i++)
            {
                switch (ColliderUtil.AddIfMissing(renderers[i], true))
                {
                    case ColliderUtil.Result.Box:
                        boxes++;
                        break;
                    case ColliderUtil.Result.Mesh:
                        meshes++;
                        break;
                }
            }

            if (boxes + meshes > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
            }

            Debug.Log(string.Format(
                "[Hito1] Colliders agregados: {0} cajas, {1} mallas. Guarda la escena (Ctrl+S) para conservarlos.",
                boxes,
                meshes));
        }
    }
}
