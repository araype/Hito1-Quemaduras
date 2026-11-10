using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Prepara la tia de Avaturn (Assets/Hito1/Personajes/Avaturn/TiaAvaturn.glb,
    // importado con glTFast) para reemplazar a la de Mixamo:
    //   1. Crea su avatar Humanoid (el GLB no trae uno) con los nombres de
    //      huesos de Avaturn, que son los de Mixamo sin prefijo.
    //   2. Guarda el prefab Resources/Hito1Avatar/TiaAvaturn con su Animator.
    //      PatientModelSwap lo pone en lugar del modelo de Mixamo al dar Play.
    //   3. Pasa las animaciones de Mixamo (y el FBX de la tia anterior) a
    //      Humanoid, para que sirvan en los dos esqueletos.
    //   4. La mascara de gestos pasa a partes del cuerpo Humanoid (columna y
    //      cabeza), porque las rutas de mixamorig8 no existen en Avaturn.
    public static class AvaturnSetup
    {
        private const string GlbPath = "Assets/Hito1/Personajes/Avaturn/TiaAvaturn.glb";
        private const string OutFolder = "Assets/Hito1/Resources/Hito1Avatar";
        private const string AvatarPath = OutFolder + "/TiaAvaturnAvatar.asset";
        private const string PrefabPath = OutFolder + "/TiaAvaturn.prefab";
        private const string AnimFolder = "Assets/Hito1/Personajes/Animaciones";
        private const string OldModelPath = "Assets/Hito1/Personajes/Familiarartefacto.fbx";
        private const string MaskPath = "Assets/Hito1/Resources/Hito1Anim/TiaGestosMascara.mask";

        // Hueso Humanoid de Unity -> nombre del hueso en Avaturn/Mixamo.
        private static readonly Dictionary<string, string> HumanMap = new Dictionary<string, string>
        {
            { "Hips", "Hips" }, { "Spine", "Spine" }, { "Chest", "Spine1" }, { "UpperChest", "Spine2" },
            { "Neck", "Neck" }, { "Head", "Head" }, { "LeftEye", "LeftEye" }, { "RightEye", "RightEye" },
            { "LeftShoulder", "LeftShoulder" }, { "LeftUpperArm", "LeftArm" },
            { "LeftLowerArm", "LeftForeArm" }, { "LeftHand", "LeftHand" },
            { "RightShoulder", "RightShoulder" }, { "RightUpperArm", "RightArm" },
            { "RightLowerArm", "RightForeArm" }, { "RightHand", "RightHand" },
            { "LeftUpperLeg", "LeftUpLeg" }, { "LeftLowerLeg", "LeftLeg" }, { "LeftFoot", "LeftFoot" },
            { "LeftToes", "LeftToeBase" },
            { "RightUpperLeg", "RightUpLeg" }, { "RightLowerLeg", "RightLeg" }, { "RightFoot", "RightFoot" },
            { "RightToes", "RightToeBase" }
        };

        private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        private static readonly string[] HumanFingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        private static readonly string[] Phalanges = { "Proximal", "Intermediate", "Distal" };

        [MenuItem("Hito1/Preparar tia de Avaturn")]
        public static void Run()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (source == null)
            {
                Debug.LogError("[AvaturnSetup] No esta " + GlbPath + " (o falta el paquete glTFast).");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OutFolder))
            {
                AssetDatabase.CreateFolder("Assets/Hito1/Resources", "Hito1Avatar");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "TiaAvaturn";
            try
            {
                Avatar avatar = BuildAvatar(instance);
                if (avatar == null)
                {
                    return;
                }

                AssetDatabase.DeleteAsset(AvatarPath);
                AssetDatabase.CreateAsset(avatar, AvatarPath);

                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                FixClothesNormals(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            int clips = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimFolder }))
            {
                if (MakeHumanoid(AssetDatabase.GUIDToAssetPath(guid), true))
                {
                    clips++;
                }
            }

            MakeHumanoid(OldModelPath, false);
            MakeMaskHumanoid();
            AssetDatabase.SaveAssets();
            Debug.Log("[AvaturnSetup] Listo: avatar Humanoid, prefab " + PrefabPath + " y " + clips
                + " animaciones en Humanoid.");
        }

        // La ropa ("avaturn_look_*") trae un mapa de normales que en Unity
        // oscurece media blusa (la mitad con las coordenadas de textura en
        // espejo; recalcular las tangentes no lo arregla). Se usa una copia
        // del material sin ese mapa: la tela se ve igual y cuesta menos en el
        // Quest.
        private static void FixClothesNormals(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("avaturn_look"))
                {
                    continue;
                }

                Material[] materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty("normalTexture"))
                    {
                        continue;
                    }

                    string path = OutFolder + "/" + materials[i].name + "_SinNormal.mat";
                    AssetDatabase.DeleteAsset(path);
                    Material copy = new Material(materials[i]);
                    copy.SetTexture("normalTexture", null);
                    copy.DisableKeyword("_NORMALMAP");
                    AssetDatabase.CreateAsset(copy, path);
                    materials[i] = copy;
                }

                r.sharedMaterials = materials;
            }
        }

        private static Avatar BuildAvatar(GameObject root)
        {
            Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(t.name))
                {
                    byName[t.name] = t;
                }
            }

            List<HumanBone> human = new List<HumanBone>();
            foreach (KeyValuePair<string, string> pair in HumanMap)
            {
                AddBone(human, byName, pair.Key, pair.Value);
            }

            foreach (string side in new[] { "Left", "Right" })
            {
                for (int f = 0; f < Fingers.Length; f++)
                {
                    for (int p = 0; p < Phalanges.Length; p++)
                    {
                        AddBone(human, byName, side + " " + HumanFingers[f] + " " + Phalanges[p],
                            side + "Hand" + Fingers[f] + (p + 1));
                    }
                }
            }

            List<SkeletonBone> skeleton = new List<SkeletonBone>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                skeleton.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }

            HumanDescription description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            avatar.name = "TiaAvaturnAvatar";
            if (!avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("[AvaturnSetup] El avatar Humanoid no es valido; revisa los nombres de huesos.");
                return null;
            }

            return avatar;
        }

        private static void AddBone(List<HumanBone> list, Dictionary<string, Transform> byName, string humanName,
            string boneName)
        {
            if (!byName.ContainsKey(boneName))
            {
                Debug.LogWarning("[AvaturnSetup] Falta el hueso " + boneName + " (" + humanName + ").");
                return;
            }

            HumanBone bone = new HumanBone { humanName = humanName, boneName = boneName };
            bone.limit.useDefaultValues = true;
            list.Add(bone);
        }

        // Pasa un FBX de Mixamo a Humanoid (avatar creado de su propio
        // esqueleto). En los clips, la raiz queda horneada en la pose: la tia
        // no avanza ni gira por la animacion (lo hace PatientReach).
        private static bool MakeHumanoid(string path, bool withClips)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            ModelImporterClipAnimation[] previous = importer.clipAnimations;
            if (previous == null || previous.Length == 0)
            {
                previous = importer.defaultClipAnimations;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            if (withClips && previous != null)
            {
                foreach (ModelImporterClipAnimation clip in previous)
                {
                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = previous;
            }

            importer.SaveAndReimport();
            return true;
        }

        private static void MakeMaskHumanoid()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                Debug.LogWarning("[AvaturnSetup] No esta la mascara " + MaskPath);
                return;
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                AvatarMaskBodyPart part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head);
            }

            mask.transformCount = 0;
            EditorUtility.SetDirty(mask);
        }
    }
}
