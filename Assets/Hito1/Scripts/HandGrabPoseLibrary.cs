using Oculus.Interaction.Input;
using UnityEngine;

namespace SojaExiles
{
    // Poses de mano para HandGrabPose, tomadas de los prefabs de ejemplo del
    // Interaction SDK de Meta (Runtime/Sample/Objects/Props: Torch = agarre
    // cilindrico, Key = pinza) y normalizadas para poder ubicarlas sobre
    // cualquier objeto. Archivo generado; las rotaciones de articulaciones
    // usan el esqueleto OpenXR (ISDK_OPENXR_HAND, siempre definido en el SDK 201).
    //
    //  - Cilindro: eje +Y en el origen, radio de referencia CylinderReferenceRadius.
    //  - Pinza (Pinch): punto entre las yemas de pulgar e indice en el
    //    origen; el objeto (plano, normal +Y) se extiende hacia +Z; el pulgar
    //    queda arriba y el indice abajo, pinzando el grosor.
    //  - Pinza de borde (PinchRim): pared vertical en el plano XY (borde de
    //    una olla); pulgar afuera (-Z), indice adentro (+Z), mano por encima.
    internal static class HandGrabPoseLibrary
    {
        internal const float CylinderReferenceRadius = 0.0201279f;

        internal class PoseData
        {
            public Handedness handedness;
            public Vector3 wristPosition;
            public Quaternion wristRotation;
            public JointFreedom[] fingersFreedom;
            public Quaternion[] jointRotations;
        }

        internal static readonly PoseData CylinderRight = new PoseData
        {
            handedness = Handedness.Right,
            wristPosition = new Vector3(0.0826f, 0.0001f, -0.002f),
            wristRotation = new Quaternion(-0.3330202f, 0.4390661f, 0.6611203f, -0.5091546f),
            fingersFreedom = new[] { (JointFreedom)1, (JointFreedom)1, (JointFreedom)1, (JointFreedom)1, (JointFreedom)1 },
            jointRotations = new[]
            {
                new Quaternion(-0.1002931f, 0.4120852f, -0.6619037f, -0.61807f),
                new Quaternion(0.1973789f, 0.0523804f, -0.0819952f, 0.9754869f),
                new Quaternion(0.3424687f, -0.0306228f, 0.0784472f, 0.9357476f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.4844997f, 5.72e-05f, 0.0403535f, 0.8738602f),
                new Quaternion(0.6872907f, -0.0123966f, -0.0225061f, 0.7259278f),
                new Quaternion(0.3201472f, 0.0193944f, -0.0171541f, 0.9470139f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.5740999f, 0.0438181f, -0.0621908f, 0.8152433f),
                new Quaternion(0.6797283f, -0.0044462f, -0.0126207f, 0.733342f),
                new Quaternion(0.3402795f, -0.0143404f, -0.0743375f, 0.9372716f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.5584079f, 0.0515284f, -0.0755188f, 0.8245134f),
                new Quaternion(-0.7003327f, 0.0222749f, 0.0405069f, -0.7123181f),
                new Quaternion(0.3676766f, -0.03022f, -0.0178223f, 0.9292917f),
                new Quaternion(-0.1244058f, 0.1232486f, -0.1798154f, 0.9679873f),
                new Quaternion(0.5030263f, -0.0081032f, 0.1682058f, 0.8477061f),
                new Quaternion(0.4914082f, 0.019268f, -0.0791684f, 0.8671096f),
                new Quaternion(0.4360686f, -0.0454928f, 0.0064258f, 0.8987398f),
            }
        };

        internal static readonly PoseData CylinderLeft = new PoseData
        {
            handedness = Handedness.Left,
            wristPosition = new Vector3(0.0826f, 0.0001f, -0.002f),
            wristRotation = new Quaternion(0.6528695f, 0.4983722f, -0.3489337f, -0.4512559f),
            fingersFreedom = new[] { (JointFreedom)1, (JointFreedom)1, (JointFreedom)1, (JointFreedom)1, (JointFreedom)1 },
            jointRotations = new[]
            {
                new Quaternion(0.1002931f, 0.4120852f, -0.6619038f, 0.61807f),
                new Quaternion(-0.1973789f, 0.0523804f, -0.0819952f, -0.9754869f),
                new Quaternion(0.3424687f, 0.0306228f, -0.0784471f, 0.9357476f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.4844997f, 5.72e-05f, 0.0403535f, -0.8738602f),
                new Quaternion(0.6872907f, 0.0123967f, 0.0225061f, 0.7259278f),
                new Quaternion(-0.3201472f, 0.0193944f, -0.0171541f, -0.9470139f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.5740999f, 0.0438181f, -0.0621908f, -0.8152433f),
                new Quaternion(0.6797283f, 0.0044462f, 0.0126207f, 0.7333419f),
                new Quaternion(0.3402795f, 0.0143405f, 0.0743376f, 0.9372716f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.5584079f, 0.0515284f, -0.0755188f, -0.8245134f),
                new Quaternion(-0.7003327f, -0.0222749f, -0.0405069f, -0.7123181f),
                new Quaternion(0.3676766f, 0.03022f, 0.0178223f, 0.9292917f),
                new Quaternion(0.1244058f, 0.1232486f, -0.1798154f, -0.9679873f),
                new Quaternion(0.5030263f, 0.0081032f, -0.1682058f, 0.8477061f),
                new Quaternion(-0.4914082f, 0.019268f, -0.0791684f, -0.8671096f),
                new Quaternion(0.4360686f, 0.0454928f, -0.0064258f, 0.8987398f),
            }
        };

        internal static readonly PoseData PinchRight = new PoseData
        {
            handedness = Handedness.Right,
            wristPosition = new Vector3(0.0024235f, -0.0391889f, -0.1123778f),
            wristRotation = new Quaternion(-0.3235843f, 0.1339737f, -0.4568705f, 0.8176879f),
            fingersFreedom = new[] { (JointFreedom)2, (JointFreedom)2, (JointFreedom)0, (JointFreedom)0, (JointFreedom)0 },
            jointRotations = new[]
            {
                new Quaternion(0.0143527f, 0.4076272f, -0.5995831f, -0.688574f),
                new Quaternion(0.2983479f, 0.0433654f, -0.0856636f, 0.9496156f),
                new Quaternion(0.3653268f, -0.0280497f, 0.0749517f, 0.9274329f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.2957443f, 0.0312164f, 0.0188794f, 0.9545703f),
                new Quaternion(0.669118f, -0.012174f, -0.0224732f, 0.7427165f),
                new Quaternion(0.3290088f, 0.0166164f, -0.013105f, 0.9440897f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.5874708f, 0.0756351f, -0.0577938f, 0.8036275f),
                new Quaternion(-0.7367155f, 0.0053364f, 0.0123438f, -0.6760691f),
                new Quaternion(0.5357882f, -0.0229318f, -0.0892555f, 0.8393084f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.6593635f, 0.0662717f, -0.1162915f, 0.7398136f),
                new Quaternion(-0.7087445f, 0.0216501f, 0.0415153f, -0.7039098f),
                new Quaternion(0.557729f, -0.026902f, -0.0300242f, 0.8290435f),
                new Quaternion(0.0183118f, 0.1403428f, -0.207036f, 0.9680417f),
                new Quaternion(0.6646412f, 0.0202108f, 0.0726852f, 0.7433441f),
                new Quaternion(0.6430303f, 0.0073546f, -0.0860377f, 0.7609569f),
                new Quaternion(0.4961347f, -0.0423285f, 0.002343f, 0.86721f),
            }
        };

        internal static readonly PoseData PinchLeft = new PoseData
        {
            handedness = Handedness.Left,
            wristPosition = new Vector3(0.0090846f, -0.0399171f, -0.1108514f),
            wristRotation = new Quaternion(0.2970878f, 0.1854579f, -0.5853526f, -0.7312363f),
            fingersFreedom = new[] { (JointFreedom)2, (JointFreedom)2, (JointFreedom)0, (JointFreedom)0, (JointFreedom)0 },
            jointRotations = new[]
            {
                new Quaternion(-0.0143527f, 0.4076272f, -0.5995831f, 0.688574f),
                new Quaternion(-0.2983479f, 0.0433654f, -0.0856636f, -0.9496156f),
                new Quaternion(0.3653269f, 0.0280497f, -0.0749517f, 0.9274329f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.2957443f, 0.0312164f, 0.0188794f, -0.9545703f),
                new Quaternion(0.6691179f, 0.012174f, 0.0224733f, 0.7427166f),
                new Quaternion(-0.3290088f, 0.0166164f, -0.013105f, -0.9440897f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.5874708f, 0.0756351f, -0.0577938f, -0.8036275f),
                new Quaternion(0.7367155f, 0.0053364f, 0.0123438f, 0.6760691f),
                new Quaternion(0.5357882f, 0.0229318f, 0.0892555f, 0.8393084f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.6593635f, 0.0662717f, -0.1162915f, -0.7398136f),
                new Quaternion(-0.7087445f, -0.0216501f, -0.0415153f, -0.7039098f),
                new Quaternion(0.557729f, 0.026902f, 0.0300242f, 0.8290435f),
                new Quaternion(-0.0183118f, 0.1403428f, -0.207036f, -0.9680417f),
                new Quaternion(0.6646412f, -0.0202108f, -0.0726851f, 0.7433441f),
                new Quaternion(-0.6430303f, 0.0073546f, -0.0860377f, -0.7609569f),
                new Quaternion(0.4961347f, 0.0423286f, -0.002343f, 0.86721f),
            }
        };

        internal static readonly PoseData PinchRimRight = new PoseData
        {
            handedness = Handedness.Right,
            wristPosition = new Vector3(0.0292005f, 0.0621959f, 0.0972081f),
            wristRotation = new Quaternion(-0.1297166f, 0.9316656f, -0.0069289f, -0.3393005f),
            fingersFreedom = new[] { (JointFreedom)2, (JointFreedom)2, (JointFreedom)0, (JointFreedom)0, (JointFreedom)0 },
            jointRotations = new[]
            {
                new Quaternion(0.0143527f, 0.4076272f, -0.5995831f, -0.688574f),
                new Quaternion(0.2983479f, 0.0433654f, -0.0856636f, 0.9496156f),
                new Quaternion(0.3653268f, -0.0280497f, 0.0749517f, 0.9274329f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.2957443f, 0.0312164f, 0.0188794f, 0.9545703f),
                new Quaternion(0.669118f, -0.012174f, -0.0224732f, 0.7427165f),
                new Quaternion(0.3290088f, 0.0166164f, -0.013105f, 0.9440897f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.5874708f, 0.0756351f, -0.0577938f, 0.8036275f),
                new Quaternion(-0.7367155f, 0.0053364f, 0.0123438f, -0.6760691f),
                new Quaternion(0.5357882f, -0.0229318f, -0.0892555f, 0.8393084f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(0.6593635f, 0.0662717f, -0.1162915f, 0.7398136f),
                new Quaternion(-0.7087445f, 0.0216501f, 0.0415153f, -0.7039098f),
                new Quaternion(0.557729f, -0.026902f, -0.0300242f, 0.8290435f),
                new Quaternion(0.0183118f, 0.1403428f, -0.207036f, 0.9680417f),
                new Quaternion(0.6646412f, 0.0202108f, 0.0726852f, 0.7433441f),
                new Quaternion(0.6430303f, 0.0073546f, -0.0860377f, 0.7609569f),
                new Quaternion(0.4961347f, -0.0423285f, 0.002343f, 0.86721f),
            }
        };

        internal static readonly PoseData PinchRimLeft = new PoseData
        {
            handedness = Handedness.Left,
            wristPosition = new Vector3(-0.0367279f, 0.0916405f, 0.0649387f),
            wristRotation = new Quaternion(-0.2326789f, -0.8003036f, 0.1430667f, -0.5337664f),
            fingersFreedom = new[] { (JointFreedom)2, (JointFreedom)2, (JointFreedom)0, (JointFreedom)0, (JointFreedom)0 },
            jointRotations = new[]
            {
                new Quaternion(-0.0143527f, 0.4076272f, -0.5995831f, 0.688574f),
                new Quaternion(-0.2983479f, 0.0433654f, -0.0856636f, -0.9496156f),
                new Quaternion(0.3653269f, 0.0280497f, -0.0749517f, 0.9274329f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.2957443f, 0.0312164f, 0.0188794f, -0.9545703f),
                new Quaternion(0.6691179f, 0.012174f, 0.0224733f, 0.7427166f),
                new Quaternion(-0.3290088f, 0.0166164f, -0.013105f, -0.9440897f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.5874708f, 0.0756351f, -0.0577938f, -0.8036275f),
                new Quaternion(0.7367155f, 0.0053364f, 0.0123438f, 0.6760691f),
                new Quaternion(0.5357882f, 0.0229318f, 0.0892555f, 0.8393084f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f),
                new Quaternion(-0.6593635f, 0.0662717f, -0.1162915f, -0.7398136f),
                new Quaternion(-0.7087445f, -0.0216501f, -0.0415153f, -0.7039098f),
                new Quaternion(0.557729f, 0.026902f, 0.0300242f, 0.8290435f),
                new Quaternion(-0.0183118f, 0.1403428f, -0.207036f, -0.9680417f),
                new Quaternion(0.6646412f, -0.0202108f, -0.0726851f, 0.7433441f),
                new Quaternion(-0.6430303f, 0.0073546f, -0.0860377f, -0.7609569f),
                new Quaternion(0.4961347f, 0.0423286f, -0.002343f, 0.86721f),
            }
        };
    }
}
