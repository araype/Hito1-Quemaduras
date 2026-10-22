using UnityEngine;

namespace SojaExiles
{
    // Logica compartida para agregar colliders a mallas que no tienen
    // ninguno. La usan el componente de runtime (AutoColliders) y la
    // herramienta de editor (Hito1 > Agregar colliders faltantes).
    //
    // Regla: siempre que se pueda, MeshCollider con la forma real de la
    // malla, porque una caja tapa los huecos (puertas, ventanas, el espacio
    // bajo una mesa). En el editor siempre se puede (la malla se cocina en el
    // build); en runtime solo si la malla tiene Read/Write. Si no se puede, se
    // usa una caja solo para objetos de tamano de mueble que no parezcan una
    // puerta o un marco.
    public static class ColliderUtil
    {
        public const float MinSize = 0.03f;
        public const float MinThickness = 0.05f;
        public const float BoxMaxSize = 2.5f;

        private static readonly string[] OpeningNames = { "door", "puerta", "frame", "marco", "window", "ventana", "arch" };

        public enum Result
        {
            Skipped,
            Box,
            Mesh,
            NotReadable
        }

        public static Result AddIfMissing(MeshRenderer renderer, bool editorTime)
        {
            if (renderer == null || !renderer.enabled)
            {
                return Result.Skipped;
            }

            // Lo que cuelga del rig (manos, controles, camara) nunca debe
            // chocar con el propio jugador.
            if (renderer.GetComponentInParent<OVRCameraRig>() != null)
            {
                return Result.Skipped;
            }

            // Indicadores visuales (zonas, marcadores) creados por los scripts.
            if (renderer.GetComponentInParent<NoAutoCollider>() != null)
            {
                return Result.Skipped;
            }

            // Ya tiene collider propio.
            if (renderer.GetComponent<Collider>() != null)
            {
                return Result.Skipped;
            }

            // Objetos con fisica (agarrables, el objetivo IK): sus colliders
            // los configura quien los armo; agregar otros cambiaria su masa
            // y como se agarran.
            if (renderer.GetComponentInParent<Rigidbody>() != null)
            {
                return Result.Skipped;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return Result.Skipped;
            }

            Mesh mesh = filter.sharedMesh;
            Bounds bounds = mesh.bounds;
            Vector3 scale = renderer.transform.lossyScale;
            Vector3 worldSize = new Vector3(
                Mathf.Abs(bounds.size.x * scale.x),
                Mathf.Abs(bounds.size.y * scale.y),
                Mathf.Abs(bounds.size.z * scale.z));

            if (worldSize.x < MinSize && worldSize.y < MinSize && worldSize.z < MinSize)
            {
                return Result.Skipped;
            }

            if (editorTime || mesh.isReadable)
            {
                MeshCollider meshCollider = AddComponent<MeshCollider>(renderer.gameObject, editorTime);
                meshCollider.sharedMesh = mesh;
                return Result.Mesh;
            }

            float biggest = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));
            if (biggest > BoxMaxSize || LooksLikeOpening(renderer, mesh))
            {
                // Una caja aqui encerraria al jugador o taparia una puerta.
                return Result.NotReadable;
            }

            BoxCollider box = AddComponent<BoxCollider>(renderer.gameObject, editorTime);
            box.center = bounds.center;
            box.size = new Vector3(
                MinLocal(bounds.size.x, scale.x),
                MinLocal(bounds.size.y, scale.y),
                MinLocal(bounds.size.z, scale.z));
            return Result.Box;
        }

        private static bool LooksLikeOpening(MeshRenderer renderer, Mesh mesh)
        {
            string names = (renderer.name + " " + mesh.name).ToLowerInvariant();
            for (int i = 0; i < OpeningNames.Length; i++)
            {
                if (names.Contains(OpeningNames[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Evita cajas de grosor cero (pisos y paredes modelados como planos).
        private static float MinLocal(float localSize, float scale)
        {
            float absScale = Mathf.Max(0.0001f, Mathf.Abs(scale));
            return Mathf.Max(localSize, MinThickness / absScale);
        }

        private static T AddComponent<T>(GameObject target, bool editorTime) where T : Component
        {
#if UNITY_EDITOR
            if (editorTime)
            {
                return UnityEditor.Undo.AddComponent<T>(target);
            }
#endif
            return target.AddComponent<T>();
        }
    }

    // Rellena en runtime los colliders que le faltan al apartamento (muebles,
    // paredes, piso) para que el CharacterController choque y el jugador no
    // atraviese todo ni caiga al vacio. Es un respaldo: la solucion definitiva
    // es hornearlos en la escena con Hito1 > Agregar colliders faltantes.
    public class AutoColliders : MonoBehaviour
    {
        private void Start()
        {
            MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();

            int boxes = 0;
            int meshes = 0;
            System.Text.StringBuilder missing = new System.Text.StringBuilder();
            int missingCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                switch (ColliderUtil.AddIfMissing(renderers[i], false))
                {
                    case ColliderUtil.Result.Box:
                        boxes++;
                        break;
                    case ColliderUtil.Result.Mesh:
                        meshes++;
                        break;
                    case ColliderUtil.Result.NotReadable:
                        missingCount++;
                        if (missingCount <= 15)
                        {
                            missing.Append(missingCount > 1 ? ", " : string.Empty).Append(renderers[i].name);
                        }
                        break;
                }
            }

            Debug.Log(string.Format(
                "[Hito1] Colliders automaticos: {0} cajas, {1} mallas. Sin collider (malla no legible grande o con hueco): {2}.",
                boxes,
                meshes,
                missingCount));

            if (missingCount > 0)
            {
                Debug.LogWarning(
                    "[Hito1] Mallas sin collider porque no tienen Read/Write habilitado: " + missing +
                    (missingCount > 15 ? "..." : string.Empty) +
                    ". Ejecuta Hito1 > Agregar colliders faltantes en el editor para hornearlos.");
            }
        }
    }
}
