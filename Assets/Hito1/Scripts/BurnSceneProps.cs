using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Pone en la cocina los objetos del protocolo y los distractores, sin
    // tocar la escena:
    //   - junto al lavadero: film transparente y gasa (para cubrir);
    //   - en la silla mas cercana a la paciente: una chompa (para abrigarla);
    //   - en el meson, cerca de la cocina: mantequilla, pasta dental, aceite
    //     y algodon; el hielo, cerca del refrigerador.
    // Busca lugares libres sobre el meson (a la altura del pano) y evita el
    // quemador. Corre un frame despues de empezar, cuando AutoColliders ya
    // puso los colliders del meson. Va en el objeto de BurnSequenceManager.
    public class BurnSceneProps : MonoBehaviour
    {
        public bool coverItems = true;
        public bool sweater = true;
        public bool remedies = true;

        private readonly List<Vector3> taken = new List<Vector3>();
        private float avoidRadius = 0.3f;

        private IEnumerator Start()
        {
            yield return null;
            Physics.SyncTransforms();

            PotFX pot = FindObjectOfType<PotFX>();
            PatientBody patient = FindObjectOfType<PatientBody>();
            Transform towel = null;
            foreach (FirstAidItem item in FindObjectsOfType<FirstAidItem>())
            {
                if (item.type == FirstAidItem.ItemType.Panuelo)
                {
                    towel = item.transform;
                    break;
                }
            }

            Vector3 stove = pot != null ? pot.BurnerPosition : (towel != null ? towel.position : transform.position);
            float counter = CounterHeight(towel, pot);
            Vector3 avoid = pot != null ? pot.BurnerPosition : new Vector3(0f, -100f, 0f);

            // Toda la cocina (las cuatro hornillas), no solo el quemador de la
            // olla: antes los remedios quedaban sobre las otras hornillas.
            if (BurnStaging.StoveArea(out Vector3 stoveCenter, out float stoveRadius))
            {
                avoid = stoveCenter;
                avoidRadius = stoveRadius;
            }

            if (towel != null)
            {
                taken.Add(towel.position);
            }

            if (pot != null)
            {
                taken.Add(pot.BottomCenter);
            }

            if (coverItems)
            {
                WaterTap tap = FindObjectOfType<WaterTap>();
                Vector3 sink = tap != null ? tap.transform.position : stove;
                Place(BuildFilm(), sink, counter, 0.3f, 0.9f, 0.09f, avoid);
                Place(BuildGauze(), sink, counter, 0.3f, 0.9f, 0.07f, avoid);
            }

            if (remedies)
            {
                Place(BuildButter(), stove, counter, 0.35f, 1f, 0.08f, avoid);
                Place(BuildToothpaste(), stove, counter, 0.35f, 1f, 0.07f, avoid);
                Place(BuildOil(), stove, counter, 0.35f, 1f, 0.06f, avoid);
                Place(BuildCotton(), stove, counter, 0.35f, 1.1f, 0.06f, avoid);

                Renderer fridge = FindByName("Fridge");
                Vector3 iceNear = fridge != null ? fridge.bounds.center : stove;
                Place(BuildIce(), iceNear, counter, fridge != null ? 0.2f : 0.35f, 1.2f, 0.09f, avoid, stove);
            }

            if (sweater)
            {
                GameObject chompa = BuildSweater();
                if (!PlaceOnChair(chompa, patient != null ? patient.transform.position : stove))
                {
                    Place(chompa, patient != null ? patient.transform.position : stove, counter, 0.4f, 1.4f, 0.15f, avoid);
                }
            }
        }

        // Altura del meson: la base del pano (que esta apoyado en el), o la
        // superficie bajo la olla.
        private static float CounterHeight(Transform towel, PotFX pot)
        {
            Vector3 from = towel != null ? towel.position : (pot != null ? pot.BottomCenter : Vector3.zero);
            Collider own = towel != null ? towel.GetComponent<Collider>() : null;
            if (Physics.Raycast(from + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 0.6f, ~0,
                QueryTriggerInteraction.Ignore) && hit.collider != own)
            {
                return hit.point.y;
            }

            return own != null ? own.bounds.min.y : from.y;
        }

        private void Place(GameObject prop, Vector3 near, float counter, float minRadius, float maxRadius,
            float clearance, Vector3 avoid, Vector3? fallback = null)
        {
            Vector3 spot;
            if (!PropKit.FindSurfaceSpot(near, counter, minRadius, maxRadius, clearance, taken, avoid, avoidRadius, out spot)
                && !(fallback.HasValue && PropKit.FindSurfaceSpot(fallback.Value, counter, 0.35f, 1.2f, clearance,
                    taken, avoid, avoidRadius, out spot)))
            {
                Debug.LogWarning("[BurnSceneProps] No hay lugar libre en el meson para " + prop.name
                    + "; queda sobre el meson cerca de la cocina.");
                spot = new Vector3(near.x, counter, near.z);
            }

            prop.transform.position = spot + Vector3.up * 0.002f;
            prop.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            prop.GetComponent<Grabbable>().SetHome();
            Debug.Log("[BurnSceneProps] " + prop.name + " en " + spot.ToString("F2"));
        }

        // Asiento de la silla mas cercana a la paciente.
        private bool PlaceOnChair(GameObject prop, Vector3 near)
        {
            Renderer best = null;
            float bestDistance = 4f;
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                if (!r.name.StartsWith("Chair"))
                {
                    continue;
                }

                float d = Vector3.Distance(r.bounds.center, near);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = r;
                }
            }

            if (best == null)
            {
                return false;
            }

            Bounds b = best.bounds;
            Vector3 probe = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, b.size.y + 0.1f, ~0,
                QueryTriggerInteraction.Ignore) || hit.point.y > b.center.y + 0.2f || hit.normal.y < 0.8f)
            {
                return false;
            }

            prop.transform.position = hit.point + Vector3.up * 0.002f;
            prop.transform.rotation = Quaternion.Euler(0f, best.transform.eulerAngles.y, 0f);
            prop.GetComponent<Grabbable>().SetHome();
            Debug.Log("[BurnSceneProps] Chompa sobre " + best.name);
            return true;
        }

        private static Renderer FindByName(string contains)
        {
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                if (r.name.Contains(contains))
                {
                    return r;
                }
            }

            return null;
        }

        // ---------------- objetos ----------------

        private static readonly Quaternion Lying = Quaternion.Euler(0f, 0f, 90f);

        private static GameObject BuildFilm()
        {
            GameObject root = PropKit.Grabbable("FilmTransparente", Vector3.zero, Quaternion.identity,
                new Vector3(0.31f, 0.052f, 0.052f), 0.25f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.026f, 0f),
                new Vector3(0.05f, 0.15f, 0.05f), Lying, "Film_Rollo", new Color(0.9f, 0.95f, 1f, 0.5f));
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.026f, 0f),
                new Vector3(0.026f, 0.155f, 0.026f), Lying, "Carton", new Color(0.6f, 0.45f, 0.3f));
            root.AddComponent<CoverItem>().kind = CoverItem.Kind.Film;
            return root;
        }

        private static GameObject BuildGauze()
        {
            GameObject root = PropKit.Grabbable("GasaEsteril", Vector3.zero, Quaternion.identity,
                new Vector3(0.1f, 0.014f, 0.08f), 0.03f);
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.007f, 0f),
                new Vector3(0.1f, 0.012f, 0.08f), "Gasa_Paquete", new Color(0.96f, 0.96f, 0.94f));
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.0135f, 0.022f),
                new Vector3(0.1f, 0.002f, 0.016f), "Plastico_Azul", new Color(0.2f, 0.45f, 0.85f));
            root.AddComponent<CoverItem>().kind = CoverItem.Kind.Gasa;
            return root;
        }

        private static GameObject BuildSweater()
        {
            GameObject root = PropKit.Grabbable("Chompa", Vector3.zero, Quaternion.identity,
                new Vector3(0.3f, 0.075f, 0.24f), 0.4f);
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.028f, 0f),
                new Vector3(0.3f, 0.055f, 0.24f), "Lana", new Color(0.55f, 0.18f, 0.2f));
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.064f, -0.05f),
                new Vector3(0.28f, 0.02f, 0.11f), "Lana", new Color(0.55f, 0.18f, 0.2f));
            root.AddComponent<SweaterItem>();
            return root;
        }

        private static GameObject BuildIce()
        {
            GameObject root = PropKit.Grabbable("Hielo", Vector3.zero, Quaternion.identity,
                new Vector3(0.15f, 0.07f, 0.15f), 0.4f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.022f, 0f),
                new Vector3(0.15f, 0.022f, 0.15f), "Plato", new Color(0.95f, 0.95f, 0.95f));
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                float r = i == 0 ? 0f : 0.035f;
                PropKit.Part(root.transform, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(a) * r, 0.055f + (i % 2) * 0.006f, Mathf.Sin(a) * r),
                    Vector3.one * 0.026f, Quaternion.Euler(Random.Range(0f, 40f), Random.Range(0f, 90f), 0f),
                    "Hielo", new Color(0.85f, 0.93f, 1f, 0.6f));
            }

            root.AddComponent<HomeRemedy>().remedy = HomeRemedy.Remedy.Hielo;
            return root;
        }

        private static GameObject BuildButter()
        {
            GameObject root = PropKit.Grabbable("Mantequilla", Vector3.zero, Quaternion.identity,
                new Vector3(0.15f, 0.045f, 0.1f), 0.25f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.004f, 0f),
                new Vector3(0.15f, 0.004f, 0.1f), "Plato", new Color(0.95f, 0.95f, 0.95f));
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f),
                new Vector3(0.09f, 0.034f, 0.05f), "Mantequilla", new Color(0.98f, 0.88f, 0.45f));
            root.AddComponent<HomeRemedy>().remedy = HomeRemedy.Remedy.Mantequilla;
            return root;
        }

        private static GameObject BuildToothpaste()
        {
            GameObject root = PropKit.Grabbable("PastaDental", Vector3.zero, Quaternion.identity,
                new Vector3(0.18f, 0.036f, 0.045f), 0.12f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.018f, 0f),
                new Vector3(0.034f, 0.075f, 0.034f), Lying, "Pasta_Tubo", new Color(0.95f, 0.97f, 1f));
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0.083f, 0.018f, 0f),
                new Vector3(0.022f, 0.01f, 0.022f), Lying, "Plastico_Azul", new Color(0.2f, 0.45f, 0.85f));
            root.AddComponent<HomeRemedy>().remedy = HomeRemedy.Remedy.PastaDental;
            return root;
        }

        private static GameObject BuildOil()
        {
            GameObject root = PropKit.Grabbable("Aceite", Vector3.zero, Quaternion.identity,
                new Vector3(0.068f, 0.24f, 0.068f), 0.6f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f),
                new Vector3(0.066f, 0.08f, 0.066f), "Aceite_Botella", new Color(0.95f, 0.75f, 0.2f, 0.7f));
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f),
                new Vector3(0.03f, 0.03f, 0.03f), "Aceite_Botella", new Color(0.95f, 0.75f, 0.2f, 0.7f));
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.227f, 0f),
                new Vector3(0.034f, 0.012f, 0.034f), "Plastico_Rojo", new Color(0.8f, 0.12f, 0.1f));
            root.AddComponent<HomeRemedy>().remedy = HomeRemedy.Remedy.Aceite;
            return root;
        }

        private static GameObject BuildCotton()
        {
            GameObject root = PropKit.Grabbable("Algodon", Vector3.zero, Quaternion.identity,
                new Vector3(0.09f, 0.05f, 0.09f), 0.02f);
            Vector3[] balls =
            {
                new Vector3(0f, 0.022f, 0f), new Vector3(0.025f, 0.02f, 0.012f),
                new Vector3(-0.02f, 0.02f, 0.018f), new Vector3(0.006f, 0.019f, -0.025f)
            };
            foreach (Vector3 p in balls)
            {
                PropKit.Part(root.transform, PrimitiveType.Sphere, p, Vector3.one * Random.Range(0.036f, 0.046f),
                    "Algodon", new Color(0.98f, 0.98f, 0.98f));
            }

            root.AddComponent<HomeRemedy>().remedy = HomeRemedy.Remedy.Algodon;
            return root;
        }
    }
}
