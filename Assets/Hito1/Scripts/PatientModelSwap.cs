using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Cambia el modelo de la tia (FBX de Mixamo, huesos mixamorig8:) por la
    // tia de Avaturn (Resources/Hito1Avatar/TiaAvaturn, la arma el menu
    // Hito1 > Preparar tia de Avaturn). Lo llama PatientBody.Awake antes de
    // buscar los huesos, asi todo lo demas (brazos, anillo, chompa, capsulas,
    // capas de la quemadura) ya encuentra los huesos y la piel nuevos.
    //
    // Se queda el objeto de la tia con sus componentes; solo se cambian los
    // hijos (mallas y esqueleto) y el avatar del Animator. Los huesos de
    // Avaturn se llaman como los de Mixamo sin prefijo, y su piel es
    // "Body_Mesh" (las capas buscan "Body"). Si falta el prefab, no hace nada
    // y queda la tia de Mixamo.
    public static class PatientModelSwap
    {
        public const string PrefabPath = "Hito1Avatar/TiaAvaturn";
        private const string Marker = "ModeloAvaturn";

        public static bool Apply(PatientBody body)
        {
            Transform root = body.transform;
            if (root.Find(Marker) != null)
            {
                return true;
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            Animator source = prefab != null ? prefab.GetComponent<Animator>() : null;
            if (source == null || source.avatar == null || !source.avatar.isHuman)
            {
                Debug.LogWarning("[PatientModelSwap] Falta Resources/" + PrefabPath + " con avatar Humanoid "
                    + "(menu Hito1 > Preparar tia de Avaturn): queda la tia de Mixamo.", body);
                return false;
            }

            // Fuera el modelo anterior: mallas, esqueleto y el IK viejo (ya
            // apagado) que apuntaba a sus huesos.
            List<GameObject> old = new List<GameObject>();
            foreach (Transform child in root)
            {
                if (child.GetComponent<SkinnedMeshRenderer>() != null
                    || (body.bonePrefix.Length > 0 && child.name.StartsWith(body.bonePrefix))
                    || child.name == "IK_Rig")
                {
                    old.Add(child.gameObject);
                }
            }

            foreach (Behaviour b in root.GetComponents<Behaviour>())
            {
                string n = b.GetType().Name;
                if (n == "RigBuilder" || n == "BodyReachFollow")
                {
                    Object.DestroyImmediate(b);
                }
            }

            foreach (GameObject go in old)
            {
                Object.DestroyImmediate(go);
            }

            // El modelo nuevo, con sus hijos directamente bajo la tia (como
            // estaba el de Mixamo).
            GameObject model = Object.Instantiate(prefab, root, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            List<Transform> parts = new List<Transform>();
            foreach (Transform child in model.transform)
            {
                parts.Add(child);
            }

            foreach (Transform part in parts)
            {
                part.SetParent(root, false);
            }

            Object.DestroyImmediate(model);
            new GameObject(Marker).transform.SetParent(root, false);

            Animator animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.gameObject.AddComponent<Animator>();
            }

            animator.avatar = source.avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();

            body.bonePrefix = "";
            return true;
        }
    }
}
