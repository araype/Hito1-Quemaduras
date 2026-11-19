using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Pone en la cocina los objetos del protocolo y los distractores, sin
    // tocar la escena:
    //   - guardados en el cajon junto al lavadero, como en una casa: el film
    //     transparente y la bolsa plastica, con lo que se cubre (NHS, Cruz
    //     Roja Britanica). El cajon se abre con la mano (StorageFront). Si la
    //     escena no tiene ese cajon, van sueltos en el meson;
    //   - en el meson, cerca de la cocina: mantequilla, pasta dental, aceite
    //     y algodon; el hielo, cerca del refrigerador.
    // Busca lugares libres sobre el meson (a la altura del pano) y evita el
    // quemador. Corre un frame despues de empezar, cuando AutoColliders ya
    // puso los colliders del meson. Va en el objeto de BurnSequenceManager.
    public class BurnSceneProps : MonoBehaviour
    {
        // Se quitaron la manta del sofa (abrigar) y el botiquin del mueble
        // alto (2026-11-19): abrigar no es un paso de las guias para esta
        // quemadura y en las pruebas con usuarios muchos creian que el
        // botiquin servia de algo (solo tenia curitas y pomada).
        public bool coverItems = true;
        public bool remedies = true;

        private readonly List<Vector3> taken = new List<Vector3>();
        private bool kitStored;
        private Vector3 kitCabinet;
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
                Vector3 room = patient != null ? patient.transform.position
                    : (Camera.main != null ? Camera.main.transform.position : sink);
                // El cajon se elige como cuando estaba el botiquin (junto a
                // su mueble alto), para que el film, la bolsa y el celular
                // queden en el mismo lugar que en las pruebas anteriores.
                FindWallCabinet(sink, counter, avoid, room);

                GameObject film = BuildFilm();
                GameObject bag = BuildBag();
                // Si se caen al cubrir, se quedan en el piso para recogerlos.
                film.GetComponent<Grabbable>().stayOnReachableFloor = true;
                bag.GetComponent<Grabbable>().stayOnReachableFloor = true;
                if (!StoreInDrawer(sink, counter, avoid, room, film, bag))
                {
                    Place(film, sink, counter, 0.3f, 0.9f, 0.09f, avoid);
                    Place(bag, sink, counter, 0.3f, 0.9f, 0.09f, avoid);
                }
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

        // ---------------- muebles de la cocina ----------------

        // Puertas (opencloseDoor de la cocina) de un mueble, agrupadas por mueble.
        private static Dictionary<Transform, List<Transform>> CabinetDoors()
        {
            Dictionary<Transform, List<Transform>> doors = new Dictionary<Transform, List<Transform>>();
            foreach (MonoBehaviour m in FindObjectsOfType<MonoBehaviour>())
            {
                if (!(m is opencloseDoor || m is opencloseDoor1) || m.transform.parent == null
                    || m.GetComponent<StorageFront>() != null)
                {
                    continue;
                }

                Transform cabinet = m.transform.parent;
                if (!doors.TryGetValue(cabinet, out List<Transform> list))
                {
                    list = new List<Transform>();
                    doors[cabinet] = list;
                }

                list.Add(m.transform);
            }

            return doors;
        }

        private static Bounds RendererBounds(Transform t)
        {
            Renderer own = t.GetComponent<Renderer>();
            if (own != null)
            {
                return own.bounds;
            }

            Bounds b = new Bounds(t.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return b;
        }

        // El mueble alto mas cercano al lavadero (no encima del lavadero,
        // donde hay repisas abiertas) con lugar adentro: ahi estaba el
        // botiquin. Solo se recuerda donde esta (kitCabinet) para elegir el
        // cajon del film; las puertas quedan como en la escena.
        private bool FindWallCabinet(Vector3 sink, float counter, Vector3 avoid, Vector3 room)
        {
            // Muebles altos cerca del lavadero, del mas cercano al mas lejano.
            Dictionary<Transform, List<Transform>> all = CabinetDoors();
            List<Transform> candidates = new List<Transform>();
            foreach (KeyValuePair<Transform, List<Transform>> pair in all)
            {
                Bounds b = RendererBounds(pair.Key);
                // Ni sobre la cocina (calor, vapor): ahi no se guarda un botiquin.
                if (b.min.y < counter + 0.25f || b.size.y < 0.3f || b.min.y > counter + 1.2f
                    || PropKit.Flat(b.center - sink).magnitude > 1.8f
                    || PropKit.Flat(b.center - avoid).magnitude < avoidRadius + 0.5f)
                {
                    continue;
                }

                candidates.Add(pair.Key);
            }

            candidates.Sort((x, y) => PropKit.Flat(RendererBounds(x).center - sink).magnitude
                .CompareTo(PropKit.Flat(RendererBounds(y).center - sink).magnitude));

            foreach (Transform cabinet in candidates)
            {
                Bounds cab = RendererBounds(cabinet);
                // El frente: el lado delgado del mueble que mira a la cocina.
                Vector3 across = cab.size.x < cab.size.z ? Vector3.right : Vector3.forward;
                Vector3 outward = across * Mathf.Sign(Vector3.Dot(room - cab.center, across) + 1e-4f);
                Vector3 side = Vector3.Cross(Vector3.up, outward);

                // La puerta mas cercana al lavadero y, detras de ella, el fondo.
                Transform door = null;
                float nearest = float.MaxValue;
                foreach (Transform d in all[cabinet])
                {
                    float dist = PropKit.Flat(RendererBounds(d).center - sink).magnitude;
                    if (dist < nearest)
                    {
                        nearest = dist;
                        door = d;
                    }
                }

                Vector3 doorCenter = RendererBounds(door).center;
                Vector3 inner = cab.center + Vector3.Project(doorCenter - cab.center, side);
                if (!InteriorFloor(cabinet, new Vector3(inner.x, cab.max.y - 0.05f, inner.z), cab.size.y,
                    outward, out Vector3 floorPoint))
                {
                    Debug.Log("[BurnSceneProps] El mueble " + cabinet.name + " no tiene lugar adentro; se prueba otro.");
                    continue;
                }

                kitStored = true;
                kitCabinet = cab.center;
                return true;
            }

            Debug.Log("[BurnSceneProps] No hay mueble alto con puertas cerca del lavadero; el cajon se elige junto al lavadero.");
            return false;
        }

        // Fondo del mueble bajo el punto, con lugar para la caja (sin repisa
        // ni otra cosa encima a menos de 20 cm).
        private static bool InteriorFloor(Transform cabinet, Vector3 probe, float height, Vector3 outward, out Vector3 point)
        {
            point = Vector3.zero;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(probe, Vector3.down, height, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.8f && hit.collider.transform.IsChildOf(cabinet) && (!found || hit.point.y > point.y))
                {
                    point = hit.point;
                    found = true;
                }
            }

            if (!found)
            {
                return false;
            }

            Vector3 half = new Vector3(0.15f, 0.09f, 0.1f);
            Quaternion rotation = Quaternion.LookRotation(outward, Vector3.up);
            return !Physics.CheckBox(point + Vector3.up * 0.105f, half, rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        // Film y bolsa, en el cajon de la cocina mas cercano al lavadero (bajo
        // el meson).
        private bool StoreInDrawer(Vector3 sink, float counter, Vector3 avoid, Vector3 room, GameObject film, GameObject bag)
        {
            Transform best = null;
            float bestDistance = 1.8f;
            foreach (Drawer_Pull_X d in FindObjectsOfType<Drawer_Pull_X>())
            {
                if (d.GetComponent<StorageFront>() != null)
                {
                    continue;
                }

                Bounds b = RendererBounds(d.transform);
                if (b.max.y > counter + 0.02f || b.max.y < counter - 0.3f || b.size.y > 0.4f)
                {
                    continue;
                }

                // Junto al mueble alto donde estaba el botiquin (el mismo
                // cajon de siempre) y lejos de la cocina.
                Vector3 near = kitStored ? kitCabinet : sink;
                float dist = PropKit.Flat(b.center - near).magnitude;
                if (dist < bestDistance && PropKit.Flat(b.center - avoid).magnitude > avoidRadius + 0.3f)
                {
                    bestDistance = dist;
                    best = d.transform;
                }
            }

            if (best == null)
            {
                Debug.LogWarning("[BurnSceneProps] No hay cajon cerca del lavadero; el film y la bolsa quedan en el meson.");
                return false;
            }

            Bounds box = RendererBounds(best);
            // Primero se prepara (si es un bloque macizo, queda hueco).
            StorageFront drawer = StorageFront.Setup(best, StorageFront.Kind.Drawer, room, 0f);
            Physics.SyncTransforms();
            RaycastHit floor = default;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(box.center.x, box.max.y - 0.01f, box.center.z),
                Vector3.down, box.size.y + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.8f && hit.collider.transform.IsChildOf(best) && (!found || hit.point.y < floor.point.y))
                {
                    floor = hit;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning("[BurnSceneProps] No se encontro el fondo del cajon " + best.name + "; el film y la bolsa quedan en el meson.");
                return false;
            }

            Quaternion facing = Quaternion.LookRotation(drawer.Outward, Vector3.up);
            float depth = Mathf.Abs(Vector3.Dot(box.size, drawer.Outward));
            // Lado a lado, hacia el frente: la caja de film atravesada y la
            // bolsa a lo largo. Atras quedaban bajo el borde del meson y la
            // bolsa transparente no se veia al abrir el cajon.
            Vector3 side = Vector3.Cross(Vector3.up, drawer.Outward).normalized;
            float width = Mathf.Abs(Vector3.Dot(box.size, side));
            film.transform.SetPositionAndRotation(floor.point + drawer.Outward * (depth * 0.12f)
                - side * (width * 0.21f) + Vector3.up * 0.003f, facing);
            bag.transform.SetPositionAndRotation(floor.point + drawer.Outward * (depth * 0.05f)
                + side * (width * 0.22f) + Vector3.up * 0.003f, facing);
            Physics.SyncTransforms();
            foreach (GameObject item in new[] { film, bag })
            {
                item.GetComponent<Grabbable>().SetHome();
                drawer.Store(item.GetComponent<Rigidbody>());
            }

            if (PropKit.FindSurfaceSpot(sink, counter, 0.3f, 1f, 0.09f, taken, avoid, avoidRadius, out Vector3 spot))
            {
                drawer.OutsideHome = spot + Vector3.up * 0.01f;
                drawer.HasOutsideHome = true;
            }

            BurnSequenceManager sequence = GetComponent<BurnSequenceManager>();
            drawer.OnTaken += rb =>
            {
                if (sequence != null && sequence.CurrentState != BurnSequenceManager.BurnState.Idle)
                {
                    sequence.Report(BurnSequenceManager.BurnEvent.SuppliesReady,
                        rb.GetComponent<FilmRoll>() != null ? "Film" : "Bolsa");
                }
            };

            Debug.Log("[BurnSceneProps] Film y bolsa guardados en el cajon " + best.parent.name + "/" + best.name);
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

        // Film y bolsa en amarillo: en blanco se perdian sobre el meson. Solo
        // para las piezas de respaldo (si faltan los modelos importados).
        private static readonly Color Yellow = new Color(1f, 0.82f, 0.1f);

        private static void Amarillo(GameObject root, string part, float alpha)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name != part || r.sharedMaterial == null)
                {
                    continue;
                }

                Material m = new Material(r.sharedMaterial) { name = part + "_Amarillo" };
                if (m.HasProperty("_Color"))
                {
                    m.color = new Color(Yellow.r, Yellow.g, Yellow.b, alpha);
                }

                r.sharedMaterial = m;
            }
        }

        // Modelo importado (CoverModelsSetup, menu Hito1 > Preparar film y
        // bolsa importados): malla con la base en y = 0 y su material en
        // Resources/Hito1FX. Null si falta.
        private static GameObject FromModel(string name, string mesh, string material, float mass,
            float minHeight)
        {
            Mesh m = Resources.Load<Mesh>("Hito1FX/" + mesh);
            if (m == null || Resources.Load<Material>("Hito1FX/" + material) == null)
            {
                return null;
            }

            Vector3 size = m.bounds.size;
            size.y = Mathf.Max(size.y, minHeight);
            GameObject root = PropKit.Grabbable(name, Vector3.zero, Quaternion.identity, size, mass);
            Renderer r = PropKit.MeshPart(root.transform, m, Vector3.zero, Quaternion.identity, material, Color.white);
            r.gameObject.name = material;
            return root;
        }

        // Caja de film de cocina ("Aluminum Foil Box - Low Poly", de Ian
        // Valeta, CC BY 4.0, con etiqueta propia). La tira sale del borde de
        // la caja (FilmRoll con el radio de la caja).
        private static GameObject BuildFilm()
        {
            GameObject box = FromModel("FilmTransparente", "FilmCaja_Malla", "FilmCaja", 0.25f, 0f);
            if (box != null)
            {
                Bounds b = box.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                FilmRoll roll = box.AddComponent<FilmRoll>();
                roll.axisCenter = b.center;
                roll.halfLength = b.extents.x;
                roll.radius = Mathf.Max(b.extents.y, b.extents.z) + 0.002f;
                return box;
            }

            GameObject root = PropKit.Grabbable("FilmTransparente", Vector3.zero, Quaternion.identity,
                new Vector3(0.31f, 0.052f, 0.052f), 0.25f);
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.026f, 0f),
                new Vector3(0.05f, 0.15f, 0.05f), Lying, "Film_Rollo", new Color(0.9f, 0.95f, 1f, 0.5f));
            PropKit.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.026f, 0f),
                new Vector3(0.026f, 0.155f, 0.026f), Lying, "Carton", new Color(0.6f, 0.45f, 0.3f));
            // Se sostiene con una mano y se tiran tiras con la otra.
            root.AddComponent<FilmRoll>();
            Amarillo(root, "Film_Rollo", 0.9f);
            return root;
        }

        // Bolsa plastica limpia, doblada: para una mano quemada sirve como
        // cubierta holgada (Cruz Roja Britanica). Se pone deslizandola de los
        // dedos a la muneca (CoverItem).
        // Modelo: "Ziplock bag", de Phoenix_Connor (CC BY 4.0), transparente
        // y sin logo.
        private static GameObject BuildBag()
        {
            GameObject zip = FromModel("BolsaPlastica", "BolsaZip_Malla", "BolsaZip", 0.02f, 0.012f);
            if (zip != null)
            {
                zip.AddComponent<CoverItem>().kind = CoverItem.Kind.Bolsa;
                return zip;
            }

            GameObject root = PropKit.Grabbable("BolsaPlastica", Vector3.zero, Quaternion.identity,
                new Vector3(0.16f, 0.012f, 0.2f), 0.02f);
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.004f, 0f),
                new Vector3(0.16f, 0.006f, 0.2f), "Bolsa_Plastico", new Color(0.92f, 0.95f, 1f, 0.6f));
            PropKit.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.0085f, -0.03f),
                new Vector3(0.15f, 0.003f, 0.12f), "Bolsa_Plastico", new Color(0.92f, 0.95f, 1f, 0.6f));
            root.AddComponent<CoverItem>().kind = CoverItem.Kind.Bolsa;
            Amarillo(root, "Bolsa_Plastico", 0.75f);
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
