using UnityEngine;

namespace SojaExiles
{
    // Logica compartida para agregar colliders a mallas que no tienen
    // ninguno. La usan el componente de runtime (AutoColliders) y la
    // herramienta de editor (Hito1 > Agregar colliders faltantes).
    public static class ColliderUtil
    {
        public const float MinSize = 0.03f;
        public const float MinThickness = 0.05f;
        public const float BigMeshSize = 6f;

        public enum Result
        {
            Skipped,
            Box,
            Mesh,
            TooBigNotReadable
        }

        // meshColliderAlways: en el editor un MeshCollider se "cocina" en el
        // build, asi que no hace falta que la malla sea legible. En runtime
        // solo se puede si la malla tiene Read/Write habilitado.
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

            // Ya tiene collider propio o heredado de un padre compuesto.
            if (renderer.GetComponentInParent<Collider>() != null)
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

            float biggest = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));
            if (biggest > BigMeshSize)
            {
                // Una caja gigante dejaria al jugador encerrado dentro de un
                // collider solido; hace falta la forma real de la malla.
                if (editorTime || mesh.isReadable)
                {
                    MeshCollider meshCollider = AddComponent<MeshCollider>(renderer.gameObject, editorTime);
                    meshCollider.sharedMesh = mesh;
                    return Result.Mesh;
                }

                return Result.TooBigNotReadable;
            }

            BoxCollider box = AddComponent<BoxCollider>(renderer.gameObject, editorTime);
            box.center = bounds.center;
            box.size = new Vector3(
                MinLocal(bounds.size.x, scale.x),
                MinLocal(bounds.size.y, scale.y),
                MinLocal(bounds.size.z, scale.z));
            return Result.Box;
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
            int tooBig = 0;

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
                    case ColliderUtil.Result.TooBigNotReadable:
                        tooBig++;
                        break;
                }
            }

            Debug.Log(string.Format(
                "[Hito1] Colliders automaticos: {0} cajas, {1} mallas. Sin collider (malla grande no legible): {2}.",
                boxes,
                meshes,
                tooBig));

            if (tooBig > 0)
            {
                Debug.LogWarning(
                    "[Hito1] Hay mallas grandes sin collider porque no tienen Read/Write habilitado. " +
                    "Ejecuta Hito1 > Agregar colliders faltantes en el editor para hornearlos.");
            }
        }
    }
}
