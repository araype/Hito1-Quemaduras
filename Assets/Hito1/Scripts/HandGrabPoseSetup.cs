using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.Grab.GrabSurfaces;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;
// SojaExiles.Grabbable (de la escena de cortes) tapa al de Meta dentro de
// este namespace.
using MetaGrabbable = Oculus.Interaction.Grabbable;

namespace SojaExiles
{
    // Agarre con poses de mano (Hand Grab Pose) del Interaction SDK de Meta,
    // armado en runtime para no tener que grabar poses a mano en el editor.
    //
    // Al iniciar, sobre el Rigidbody de este objeto:
    //  1. Asegura un Grabbable de Meta (lo que mueve el objeto al agarrarlo).
    //  2. Asegura un GrabInteractable para agarrar con los controles.
    //  3. Crea, por cada mano, varios HandGrabInteractable con una
    //     HandGrabPose cada uno (el SDK elige el mas cercano a la mano real):
    //       - Pinza: pulgar e indice pinzando el grosor en cada borde de un
    //         objeto plano, desde arriba y desde abajo (gasa, guantes, panuelo).
    //       - PinzaBorde: pulgar por fuera e indice por dentro de la pared,
    //         con la mano encima, en cada lado del borde (olla).
    //       - Cilindro: mano envolviendo el objeto alrededor de un eje, en
    //         los dos sentidos (frasco, rollo de venda).
    //     Las posiciones se calculan con el tamano real del objeto (sus
    //     colliders), y los dedos salen de poses grabadas por Meta
    //     (HandGrabPoseLibrary).
    //  4. Apaga los HandGrabInteractable que ya existian sin poses, para que
    //     no compitan con los nuevos.
    [RequireComponent(typeof(Rigidbody))]
    public class HandGrabPoseSetup : MonoBehaviour
    {
        // Los valores nuevos van al final: las escenas guardan el numero.
        public enum GripType
        {
            Pinza,
            Cilindro,
            PinzaBorde
        }

        public enum CylinderAxis
        {
            MasLargo,
            MasCorto,
            X,
            Y,
            Z
        }

        public GripType grip = GripType.Pinza;

        [Tooltip("Solo para Cilindro: eje local alrededor del cual se envuelve la mano.")]
        public CylinderAxis cylinderAxis = CylinderAxis.MasLargo;

        [Tooltip("Distancia desde el borde hacia adentro donde se ubica la pinza (m).")]
        public float pinchInset = 0.012f;

        public bool allowControllerGrab = true;
        public bool throwOnRelease = true;

        private const float NormalSideMargin = 0.01f;
        private const float CylinderEndMargin = 0.03f;
        private const float MinCylinderWristDistance = 0.03f;

        private Rigidbody body;
        private MetaGrabbable grabbable;

        public int InteractableCount { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();

            Bounds localBounds;
            if (!TryGetLocalBounds(out localBounds))
            {
                Debug.LogWarning("[Hito1] HandGrabPoseSetup: " + name + " no tiene colliders ni malla para calcular las poses.", this);
                return;
            }

            grabbable = EnsureGrabbable();
            DisableInteractablesWithoutPoses();

            if (allowControllerGrab)
            {
                EnsureControllerGrab();
            }

            switch (grip)
            {
                case GripType.Cilindro:
                    BuildCylinder(localBounds);
                    break;
                case GripType.PinzaBorde:
                    BuildPinch(localBounds, true);
                    break;
                default:
                    BuildPinch(localBounds, false);
                    break;
            }
        }

        private MetaGrabbable EnsureGrabbable()
        {
            MetaGrabbable existing = body.GetComponent<MetaGrabbable>();
            if (existing != null)
            {
                return existing;
            }

            MetaGrabbable created = body.gameObject.AddComponent<MetaGrabbable>();
            created.InjectOptionalRigidbody(body);
            created.InjectOptionalTargetTransform(body.transform);
            created.InjectOptionalKinematicWhileSelected(true);
            created.InjectOptionalThrowWhenUnselected(throwOnRelease);
            return created;
        }

        private void DisableInteractablesWithoutPoses()
        {
            HandGrabInteractable[] existing = body.GetComponentsInChildren<HandGrabInteractable>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].HandGrabPoses.Count == 0)
                {
                    existing[i].enabled = false;
                }
            }
        }

        private void EnsureControllerGrab()
        {
            GrabInteractable[] existing = body.GetComponentsInChildren<GrabInteractable>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].Rigidbody == body || existing[i].Rigidbody == null)
                {
                    return;
                }
            }

            GameObject go = new GameObject("Agarre_Control");
            go.transform.SetParent(body.transform, false);
            GrabInteractable interactable = go.AddComponent<GrabInteractable>();
            interactable.InjectAllGrabInteractable(body);
            interactable.InjectOptionalPointableElement(grabbable);
        }

        // ---------------------------------------------------------------
        // Pinza: una pose por borde (4 bordes) y por lado (arriba/abajo).
        // En el borde de un recipiente (rim) solo desde arriba.
        // ---------------------------------------------------------------
        private void BuildPinch(Bounds local, bool rim)
        {
            HandGrabPoseLibrary.PoseData left = rim ? HandGrabPoseLibrary.PinchRimLeft : HandGrabPoseLibrary.PinchLeft;
            HandGrabPoseLibrary.PoseData right = rim ? HandGrabPoseLibrary.PinchRimRight : HandGrabPoseLibrary.PinchRight;

            Vector3 scale = AbsScale();
            Vector3 worldExtents = Vector3.Scale(local.extents, scale);
            int normalAxis = SmallestAxis(worldExtents);

            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == normalAxis)
                {
                    continue;
                }

                for (int edgeSign = -1; edgeSign <= 1; edgeSign += 2)
                {
                    for (int sideSign = rim ? 1 : -1; sideSign <= 1; sideSign += 2)
                    {
                        Vector3 point = local.center;

                        // Hacia el borde, metido un poco hacia adentro.
                        float inset = Mathf.Min(pinchInset, worldExtents[axis] * 0.25f) / Mathf.Max(scale[axis], 1e-5f);
                        point[axis] += edgeSign * (local.extents[axis] - inset);

                        // Hacia la cara de arriba (o de abajo) en objetos gruesos
                        // como la olla; en los planos queda practicamente al centro.
                        float sideOffset = Mathf.Max(0f, worldExtents[normalAxis] - NormalSideMargin) / Mathf.Max(scale[normalAxis], 1e-5f);
                        point[normalAxis] += sideSign * sideOffset;

                        Vector3 worldPoint = transform.TransformPoint(point);
                        Vector3 inward = -edgeSign * AxisDirection(axis);
                        Vector3 up = sideSign * AxisDirection(normalAxis);
                        Quaternion frame = Quaternion.LookRotation(inward, up);

                        string label = (rim ? "PinzaBorde_" : "Pinza_") + "XYZ"[axis] + (edgeSign > 0 ? "+" : "-")
                            + (sideSign > 0 ? "_arriba" : "_abajo");
                        CreateInteractable(label, left, worldPoint, frame, null);
                        CreateInteractable(label, right, worldPoint, frame, null);
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // Cilindro: la mano envuelve el eje; dos sentidos (pulgar hacia
        // cada extremo). La superficie cilindrica deja girar la mano
        // alrededor del eje y deslizarla a lo largo.
        // ---------------------------------------------------------------
        private void BuildCylinder(Bounds local)
        {
            Vector3 scale = AbsScale();
            Vector3 worldExtents = Vector3.Scale(local.extents, scale);
            int axis = ResolveCylinderAxis(worldExtents);

            float radius = 0f;
            int count = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i != axis)
                {
                    radius += worldExtents[i];
                    count++;
                }
            }
            radius /= count;

            Vector3 center = transform.TransformPoint(local.center);
            Vector3 axisDir = AxisDirection(axis);
            float slide = Mathf.Max(0.0005f, worldExtents[axis] - CylinderEndMargin);

            for (int sign = -1; sign <= 1; sign += 2)
            {
                Quaternion frame = Quaternion.FromToRotation(Vector3.up, sign * axisDir);
                string label = "Cilindro_" + "XYZ"[axis] + (sign > 0 ? "+" : "-");
                CreateInteractable(label, HandGrabPoseLibrary.CylinderLeft, center, frame, radius);
                CreateInteractable(label, HandGrabPoseLibrary.CylinderRight, center, frame, radius);
            }

            // Superficies: se agregan despues porque dependen de cada pose.
            foreach (CylinderGrabSurface surface in GetComponentsInChildren<CylinderGrabSurface>(true))
            {
                Vector3 dir = surface.transform.parent.rotation * Vector3.up;
                CylinderSurfaceData data = new CylinderSurfaceData();
                data.startPoint = transform.InverseTransformPoint(center + dir * slide);
                data.endPoint = transform.InverseTransformPoint(center - dir * slide);
                data.arcOffset = 0f;
                data.arcLength = 360f;
                surface.InjectAllCylinderSurface(data, transform);
            }
        }

        // frame: orientacion del marco normalizado de la pose en el mundo
        // (ver HandGrabPoseLibrary). cylinderRadius != null para cilindros.
        private void CreateInteractable(string label, HandGrabPoseLibrary.PoseData data, Vector3 origin,
            Quaternion frame, float? cylinderRadius)
        {
            string hand = data.handedness == Handedness.Left ? "_L" : "_R";

            GameObject interactableGo = new GameObject("PoseDeMano_" + label + hand);
            interactableGo.transform.SetParent(body.transform, false);
            interactableGo.transform.SetPositionAndRotation(origin, frame);

            Vector3 wristLocal = data.wristPosition;
            if (cylinderRadius.HasValue)
            {
                // Correr la muneca para que los dedos queden sobre este radio
                // y no sobre el de la linterna con que se grabo la pose.
                Vector3 radial = new Vector3(wristLocal.x, 0f, wristLocal.z);
                float distance = Mathf.Max(MinCylinderWristDistance,
                    radial.magnitude - HandGrabPoseLibrary.CylinderReferenceRadius + cylinderRadius.Value);
                wristLocal = radial.normalized * distance + new Vector3(0f, wristLocal.y, 0f);
            }

            GameObject poseGo = new GameObject("HandGrabPose");
            poseGo.transform.SetParent(interactableGo.transform, false);
            poseGo.transform.SetPositionAndRotation(origin + frame * wristLocal, frame * data.wristRotation);

            HandPose handPose = new HandPose(data.handedness);
            for (int i = 0; i < data.jointRotations.Length && i < handPose.JointRotations.Length; i++)
            {
                handPose.JointRotations[i] = data.jointRotations[i];
            }
            for (int i = 0; i < data.fingersFreedom.Length && i < handPose.FingersFreedom.Length; i++)
            {
                handPose.FingersFreedom[i] = data.fingersFreedom[i];
            }

            HandGrabPose pose = poseGo.AddComponent<HandGrabPose>();
            pose.InjectAllHandGrabPose(body.transform);
            pose.InjectOptionalHandPose(handPose);

            if (cylinderRadius.HasValue)
            {
                CylinderGrabSurface surface = poseGo.AddComponent<CylinderGrabSurface>();
                pose.InjectOptionalSurface(surface);
            }

            HandGrabInteractable interactable = interactableGo.AddComponent<HandGrabInteractable>();
            interactable.InjectAllHandGrabInteractable(GrabTypeFlags.All, body,
                GrabbingRule.DefaultPinchRule, GrabbingRule.DefaultPalmRule);
            interactable.InjectOptionalPointableElement(grabbable);
            interactable.InjectOptionalHandGrabPoses(new List<HandGrabPose> { pose });

            InteractableCount++;
        }

        private int ResolveCylinderAxis(Vector3 worldExtents)
        {
            switch (cylinderAxis)
            {
                case CylinderAxis.X:
                    return 0;
                case CylinderAxis.Y:
                    return 1;
                case CylinderAxis.Z:
                    return 2;
                case CylinderAxis.MasCorto:
                    return SmallestAxis(worldExtents);
                default:
                    return LargestAxis(worldExtents);
            }
        }

        private Vector3 AxisDirection(int axis)
        {
            Vector3 local = Vector3.zero;
            local[axis] = 1f;
            return transform.rotation * local;
        }

        private Vector3 AbsScale()
        {
            Vector3 s = transform.lossyScale;
            return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        private static int SmallestAxis(Vector3 v)
        {
            return v.x <= v.y && v.x <= v.z ? 0 : (v.y <= v.z ? 1 : 2);
        }

        private static int LargestAxis(Vector3 v)
        {
            return v.x >= v.y && v.x >= v.z ? 0 : (v.y >= v.z ? 1 : 2);
        }

        // Caja que contiene los colliders del objeto, en el espacio local de
        // este transform (sin escala aplicada).
        private bool TryGetLocalBounds(out Bounds result)
        {
            result = new Bounds();
            bool any = false;

            foreach (Collider col in GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger || col.attachedRigidbody != body)
                {
                    continue;
                }

                Bounds colliderLocal;
                if (!TryGetColliderLocalBounds(col, out colliderLocal))
                {
                    continue;
                }

                Encapsulate(ref result, ref any, col.transform, colliderLocal);
            }

            if (!any)
            {
                MeshFilter filter = GetComponentInChildren<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Encapsulate(ref result, ref any, filter.transform, filter.sharedMesh.bounds);
                }
            }

            return any;
        }

        private void Encapsulate(ref Bounds result, ref bool any, Transform source, Bounds sourceBounds)
        {
            Vector3 min = sourceBounds.min;
            Vector3 max = sourceBounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 p = transform.InverseTransformPoint(source.TransformPoint(corner));
                if (!any)
                {
                    result = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    result.Encapsulate(p);
                }
            }
        }

        private static bool TryGetColliderLocalBounds(Collider col, out Bounds bounds)
        {
            BoxCollider box = col as BoxCollider;
            if (box != null)
            {
                bounds = new Bounds(box.center, box.size);
                return true;
            }

            SphereCollider sphere = col as SphereCollider;
            if (sphere != null)
            {
                bounds = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                return true;
            }

            CapsuleCollider capsule = col as CapsuleCollider;
            if (capsule != null)
            {
                Vector3 size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                bounds = new Bounds(capsule.center, size);
                return true;
            }

            MeshCollider mesh = col as MeshCollider;
            if (mesh != null && mesh.sharedMesh != null)
            {
                bounds = mesh.sharedMesh.bounds;
                return true;
            }

            bounds = new Bounds();
            return false;
        }
    }
}
