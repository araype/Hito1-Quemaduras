using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Arma la escena de la quemadura antes de que empiece, sin tocar el
    // archivo de la escena:
    //   1. Escala de la cocina: el kit del departamento esta a ~1,4 veces el
    //      tamano real (meson de 1,37 m, refrigerador de 2,46 m), y la
    //      paciente mide 1,65 m. Se achica la cocina alrededor de la paciente
    //      hasta que el meson quede a 'counterHeight' sobre el piso. Sin esto
    //      ella tiene que levantar la mano a la cara para revolver la olla y
    //      casi al hombro para llegar al chorro.
    //   2. La paciente queda de frente a la cocina, con la olla delante de su
    //      mano derecha (asi puede revolver).
    //   3. El jugador queda a un costado, mirandola, con la cocina a la vista.
    // Lo llama Hito1RuntimeBootstrap antes de instalar lo demas (los objetos
    // nuevos se ubican ya con la cocina a escala).
    public static class BurnStaging
    {
        // Altura real de un meson de cocina (m).
        public const float CounterHeight = 0.92f;

        public static bool Applied { get; private set; }
        public static float Scale { get; private set; } = 1f;

        public static void Apply()
        {
            Applied = false;
            Scale = 1f;

            PatientBody patient = Object.FindObjectOfType<PatientBody>();
            WaterTap tap = Object.FindObjectOfType<WaterTap>();
            if (patient == null || tap == null)
            {
                return;
            }

            // Raiz del ambiente: el ancestro mas alto del grifo.
            Transform environment = tap.transform;
            while (environment.parent != null)
            {
                environment = environment.parent;
            }

            // La paciente no se achica: se saca del ambiente antes de escalar.
            if (patient.transform.IsChildOf(environment))
            {
                patient.transform.SetParent(null, true);
            }

            float floor = FloorBelow(patient.transform.position);
            float counter = MeasureCounter(tap);
            float height = counter - floor;
            if (height > CounterHeight * 1.12f && height < CounterHeight * 2.2f)
            {
                Scale = CounterHeight / height;
                Vector3 pivot = new Vector3(patient.transform.position.x, floor, patient.transform.position.z);
                environment.position = pivot + (environment.position - pivot) * Scale;
                environment.localScale *= Scale;
                foreach (Light light in environment.GetComponentsInChildren<Light>(true))
                {
                    light.range *= Scale;
                }

                Physics.SyncTransforms();

                // Los objetos agarrables vuelven aqui si se pierden.
                foreach (Grabbable g in Object.FindObjectsOfType<Grabbable>())
                {
                    g.SetHome();
                }

                Debug.Log($"[BurnStaging] Cocina escalada a {Scale:F2} (meson de {height:F2} m a {CounterHeight:F2} m)");
            }

            PlacePatient(patient, floor);
            PlacePlayer(patient, tap, floor);
            Physics.SyncTransforms();
            Applied = true;
        }

        // Zona de la cocina (las hornillas): centro y radio en planta. Ahi no
        // se dejan objetos.
        public static bool StoveArea(out Vector3 center, out float radius)
        {
            foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
            {
                if (r.name == "Stove_Top")
                {
                    center = r.bounds.center;
                    radius = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) + 0.08f;
                    return true;
                }
            }

            PotFX pot = Object.FindObjectOfType<PotFX>();
            center = pot != null ? pot.BurnerPosition : new Vector3(0f, -100f, 0f);
            radius = 0.3f;
            return pot != null;
        }

        private static float FloorBelow(Vector3 point)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 0.6f, Vector3.down, 3f, ~0,
                QueryTriggerInteraction.Ignore);
            float best = point.y;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<PatientBody>() != null || hit.normal.y < 0.8f)
                {
                    continue;
                }

                float d = Mathf.Abs(hit.point.y - point.y);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = hit.point.y;
                }
            }

            return best;
        }

        // Altura de la superficie del meson junto al lavadero (o bajo la olla).
        public static float MeasureCounter(WaterTap tap)
        {
            Transform towel = null;
            foreach (FirstAidItem item in Object.FindObjectsOfType<FirstAidItem>())
            {
                if (item.type == FirstAidItem.ItemType.Panuelo)
                {
                    towel = item.transform;
                    break;
                }
            }

            if (towel != null)
            {
                Collider own = towel.GetComponent<Collider>();
                RaycastHit[] hits = Physics.RaycastAll(towel.position + Vector3.up * 0.05f, Vector3.down, 0.6f, ~0,
                    QueryTriggerInteraction.Ignore);
                float best = float.NegativeInfinity;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider != own && hit.point.y > best)
                    {
                        best = hit.point.y;
                    }
                }

                if (!float.IsNegativeInfinity(best))
                {
                    return best;
                }
            }

            // Sin pano: el borde del lavadero, un poco bajo la llave.
            return tap.transform.position.y - 0.27f;
        }

        // De frente a la cocina, con la olla delante de la mano derecha.
        private static void PlacePatient(PatientBody patient, float floor)
        {
            Rigidbody potBody = null;
            foreach (Rigidbody rb in Object.FindObjectsOfType<Rigidbody>())
            {
                if (rb.name.Contains("Olla"))
                {
                    potBody = rb;
                    break;
                }
            }

            if (potBody == null)
            {
                return;
            }

            Renderer potRenderer = potBody.GetComponentInChildren<Renderer>();
            Vector3 pot = potRenderer != null ? potRenderer.bounds.center : potBody.position;
            Vector3 away = PropKit.Flat(patient.transform.position - pot);
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -patient.transform.forward;
            }

            away = SnapToAxis(away.normalized, 35f);
            Vector3 forward = -away;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 position = new Vector3(pot.x, floor, pot.z) + away * 0.33f - right * 0.1f;

            PatientReach reach = patient.GetComponent<PatientReach>();
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            if (reach != null)
            {
                reach.Teleport(position, rotation);
            }
            else
            {
                patient.transform.SetPositionAndRotation(position, rotation);
            }
        }

        // A un costado de la paciente (del lado del lavadero), mirandola.
        private static void PlacePlayer(PatientBody patient, WaterTap tap, float floor)
        {
            VRLocomotion player = Object.FindObjectOfType<VRLocomotion>();
            if (player == null)
            {
                return;
            }

            Vector3 her = patient.transform.position;
            Vector3 back = -patient.transform.forward;
            Vector3 right = patient.transform.right;
            float sinkSide = Vector3.Dot(tap.transform.position - her, right) >= 0f ? 1f : -1f;

            List<Vector3> candidates = new List<Vector3>
            {
                her + back * 0.95f + right * sinkSide * 0.75f,
                her + back * 1.2f + right * sinkSide * 0.3f,
                her + back * 0.95f - right * sinkSide * 0.75f,
                her + back * 1.4f
            };

            CharacterController capsule = player.GetComponent<CharacterController>();
            foreach (Vector3 c in candidates)
            {
                Vector3 feet = new Vector3(c.x, floor, c.z);
                if (Blocked(feet, capsule))
                {
                    continue;
                }

                Vector3 look = PropKit.Flat(her - feet);
                if (capsule != null)
                {
                    capsule.enabled = false;
                }

                player.transform.position = feet + Vector3.up * 0.02f;
                if (look.sqrMagnitude > 1e-4f)
                {
                    player.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                }

                if (capsule != null)
                {
                    capsule.enabled = true;
                }

                return;
            }

            Debug.LogWarning("[BurnStaging] No hay lugar libre junto a la paciente para el jugador; queda donde estaba.");
        }

        private static bool Blocked(Vector3 feet, CharacterController own)
        {
            Collider[] hits = Physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 1.6f, 0.25f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                if (col == own || col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic)
                {
                    continue;
                }

                if (col.GetComponentInParent<OVRCameraRig>() != null || col.GetComponentInParent<VRLocomotion>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // Si la direccion esta cerca de un eje del mundo (las cocinas del kit
        // estan alineadas a los ejes), la endereza.
        private static Vector3 SnapToAxis(Vector3 dir, float maxDegrees)
        {
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (Vector3 axis in axes)
            {
                if (Vector3.Angle(dir, axis) <= maxDegrees)
                {
                    return axis;
                }
            }

            return dir;
        }
    }
}
