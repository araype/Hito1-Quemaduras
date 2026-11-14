using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Acomoda el cuarto real del jugador dentro de la cocina. Antes
    // BurnStaging movia el rig al punto de inicio sin mirar donde estaba el
    // jugador en su cuarto: la cocina podia quedar contra una pared real y el
    // celular o la palanca del caño solo se alcanzaban saliendo del limite.
    //
    //  - Con el limite del Quest (guardian, area de juego rectangular): el
    //    rectangulo se pone frente al meson, con su lado largo a lo largo del
    //    meson y su borde 'counterInset' metido en el (para poder estirar el
    //    brazo sobre el meson sin salir del limite), centrado entre la tia,
    //    el grifo y el punto de inicio. Asi todo lo de la cocina se alcanza
    //    caminando de verdad; para la manta (en la sala) igual hay que usar
    //    la locomocion.
    //  - Sin limite (o si no se puede leer): la cabeza real queda en el punto
    //    de inicio, mirando a la tia, este donde este el jugador.
    // Si el jugador recentra la vista (boton de Meta), se vuelve a acomodar
    // el rectangulo. Lo instala Hito1RuntimeBootstrap despues de BurnStaging.
    public class PlaySpaceFit : MonoBehaviour
    {
        public bool fitToBoundary = true;
        [Tooltip("Cuanto entra el borde del area de juego dentro del meson (m).")]
        public float counterInset = 0.3f;
        [Tooltip("Segundos maximos esperando a que el visor entregue la pose de la cabeza.")]
        public float waitForHeadSeconds = 4f;

        public static bool UsedBoundary { get; private set; }

        private VRLocomotion player;
        private OVRCameraRig rig;
        private Vector3 startFeet;
        private Vector3 startForward;
        private bool hasStart;

        private void Start()
        {
            StartCoroutine(FitWhenReady());
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose += OnRecentered;
            }
        }

        private void OnDestroy()
        {
            if (OVRManager.display != null)
            {
                OVRManager.display.RecenteredPose -= OnRecentered;
            }
        }

        private void OnRecentered()
        {
            if (hasStart && UsedBoundary)
            {
                Fit();
            }
        }

        private IEnumerator FitWhenReady()
        {
            // BurnStaging ya dejo al jugador en el punto de inicio.
            player = FindObjectOfType<VRLocomotion>();
            if (player == null)
            {
                yield break;
            }

            startFeet = player.transform.position;
            startForward = PropKit.Flat(player.transform.forward).normalized;
            hasStart = true;

            float t = 0f;
            while (t < waitForHeadSeconds)
            {
                rig = player.GetComponentInChildren<OVRCameraRig>();
                if (rig != null && rig.trackingSpace != null && rig.centerEyeAnchor != null && OVRManager.isHmdPresent
                    && rig.centerEyeAnchor.localPosition.sqrMagnitude > 0.01f)
                {
                    break;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (rig == null || rig.trackingSpace == null || !OVRManager.isHmdPresent)
            {
                Debug.Log("[PlaySpaceFit] Sin visor: el jugador queda donde lo dejo BurnStaging.");
                yield break;
            }

            // Un cuadro mas para que la pose sea la de este cuadro.
            yield return null;
            Fit();
        }

        private void Fit()
        {
            if (player == null || rig == null || rig.trackingSpace == null)
            {
                return;
            }

            Transform space = rig.trackingSpace;
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            UsedBoundary = fitToBoundary && FitBoundary(space, out targetPos, out targetRot);
            if (!UsedBoundary && !FitHead(space, out targetPos, out targetRot))
            {
                return;
            }

            // Mover todo el jugador (capsula y rig) para que el espacio de
            // seguimiento quede en targetPos / targetRot. Solo giro horizontal;
            // la altura no cambia (la maneja VRLocomotion.AnchorFloor).
            Quaternion delta = targetRot * Quaternion.Inverse(Yaw(space.rotation));
            Vector3 pivot = space.position;
            targetPos.y = pivot.y;

            CharacterController capsule = player.GetComponent<CharacterController>();
            if (capsule != null)
            {
                capsule.enabled = false;
            }

            Transform root = player.transform;
            root.position = targetPos + delta * (root.position - pivot);
            root.rotation = delta * root.rotation;

            // La capsula, bajo la cabeza (si ahi hay lugar): el rig se corre al
            // reves para que la vista no se mueva.
            Vector3 head = rig.centerEyeAnchor.position;
            Vector3 feet = new Vector3(head.x, root.position.y, head.z);
            if (Free(feet, capsule))
            {
                Vector3 shift = feet - root.position;
                root.position = feet;
                rig.transform.position -= shift;
            }

            if (capsule != null)
            {
                capsule.enabled = true;
            }

            Physics.SyncTransforms();
            Debug.Log("[PlaySpaceFit] " + (UsedBoundary ? "Area de juego acomodada frente al meson" : "Cabeza en el punto de inicio")
                + "; cabeza en " + rig.centerEyeAnchor.position.ToString("F2"));
        }

        // Rectangulo del area de juego frente al meson.
        private bool FitBoundary(Transform space, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            PatientBody patient = FindObjectOfType<PatientBody>();
            WaterTap tap = FindObjectOfType<WaterTap>();
            if (patient == null || tap == null || OVRManager.boundary == null || !OVRManager.boundary.GetConfigured())
            {
                return false;
            }

            Vector3[] points = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
            if (points == null || points.Length < 4)
            {
                return false;
            }

            // Centro y lados (espacio de seguimiento).
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in points)
            {
                center += p;
            }

            center /= points.Length;
            center.y = 0f;
            Vector3 edgeA = PropKit.Flat(points[1] - points[0]);
            Vector3 edgeB = PropKit.Flat(points[2] - points[1]);
            Vector3 longAxis = edgeA.magnitude >= edgeB.magnitude ? edgeA : edgeB;
            float depth = Mathf.Min(edgeA.magnitude, edgeB.magnitude);
            if (longAxis.sqrMagnitude < 0.25f || depth < 0.5f)
            {
                return false;
            }

            // El meson: frente a la tia. 'outward' sale del meson hacia el cuarto.
            Vector3 her = patient.transform.position;
            Vector3 toCounter = PropKit.Flat(patient.transform.forward).normalized;
            float front = 0.35f;
            if (Physics.Raycast(her + Vector3.up * 0.8f, toCounter, out RaycastHit hit, 1.2f, ~0,
                    QueryTriggerInteraction.Ignore) && hit.rigidbody == null)
            {
                front = hit.distance;
            }

            Vector3 outward = -toCounter;
            Vector3 along = Vector3.Cross(Vector3.up, outward).normalized;
            Vector3 counterFront = her + toCounter * front;
            float lateral = (Vector3.Dot(her, along) + Vector3.Dot(tap.transform.position, along)
                + Vector3.Dot(startFeet, along)) / 3f;
            float inset = Mathf.Min(counterInset, depth * 0.3f);
            Vector3 worldCenter = counterFront + along * (lateral - Vector3.Dot(counterFront, along))
                + outward * (depth * 0.5f - inset);

            // Lado largo a lo largo del meson; de los dos sentidos, el que menos
            // gira respecto de como esta ahora.
            Quaternion current = Yaw(space.rotation);
            Vector3 longWorldNow = current * longAxis.normalized;
            Vector3 wanted = Vector3.Dot(longWorldNow, along) >= 0f ? along : -along;
            Quaternion fix = Quaternion.FromToRotation(PropKit.Flat(longWorldNow).normalized, wanted);
            rotation = Yaw(fix * current);
            position = worldCenter - rotation * center;
            return true;
        }

        // Sin area de juego: la cabeza real va al punto de inicio, mirando a la tia.
        private bool FitHead(Transform space, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Transform head = rig.centerEyeAnchor;
            Vector3 local = space.InverseTransformPoint(head.position);
            local.y = 0f;
            Vector3 localForward = PropKit.Flat(space.InverseTransformDirection(head.forward));
            if (localForward.sqrMagnitude < 1e-4f || startForward.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            rotation = Quaternion.FromToRotation(localForward.normalized, startForward);
            rotation = Yaw(rotation);
            position = startFeet - rotation * local;
            return true;
        }

        private static Quaternion Yaw(Quaternion q)
        {
            return Quaternion.Euler(0f, q.eulerAngles.y, 0f);
        }

        private static bool Free(Vector3 feet, CharacterController own)
        {
            Collider[] hits = Physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.6f, 0.22f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                if (col == own || col.attachedRigidbody != null)
                {
                    continue;
                }

                if (col.GetComponentInParent<VRLocomotion>() != null || col.GetComponentInParent<PatientBody>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
