using UnityEngine;

namespace SojaExiles
{
    // Zona de la cocina donde puede caminar el jugador. Antes se podia salir
    // al comedor (y la paciente, llevada del brazo, quedaba en el marco de la
    // puerta). Se arma al dar Play con lo que importa de la escena (cocina,
    // lavadero, paciente y jugador) mas un margen; VRLocomotion no deja que la
    // capsula del jugador camine fuera de ella.
    //
    // El jugador (no la paciente) puede ademas ir por el camino de la cocina
    // hasta la manta de la sala (AllowPlayerPathTo): se busca sobre una
    // grilla del piso al dar Play, asi sirve aunque cambie la escala de la
    // casa. Las paredes las sigue frenando el CharacterController.
    public static class PlayArea
    {
        public static bool Active { get; private set; }
        public static Vector2 Min { get; private set; }
        public static Vector2 Max { get; private set; }

        public static float margin = 0.9f;

        private const float Cell = 0.25f;
        private static bool[] pathCells;
        private static bool[] freeCells;
        private static int pathWidth, pathHeight;
        private static Vector2 pathOrigin;
        private static float pathFloorY;

        public static bool HasPlayerPath
        {
            get { return pathCells != null; }
        }

        public static void FromKitchen()
        {
            pathCells = null;
            freeCells = null;
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;

            void Add(Vector3 p)
            {
                Vector2 q = new Vector2(p.x, p.z);
                if (!any)
                {
                    min = max = q;
                    any = true;
                }
                else
                {
                    min = Vector2.Min(min, q);
                    max = Vector2.Max(max, q);
                }
            }

            PotFX pot = Object.FindObjectOfType<PotFX>();
            if (pot != null)
            {
                Add(pot.transform.position);
            }

            foreach (WaterTap tap in Object.FindObjectsOfType<WaterTap>())
            {
                Add(tap.transform.position);
            }

            foreach (PatientBody patient in Object.FindObjectsOfType<PatientBody>())
            {
                Add(patient.transform.position);
            }

            OVRCameraRig rig = Object.FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                Add(rig.transform.position);
            }

            if (!any)
            {
                Active = false;
                return;
            }

            Min = min - Vector2.one * margin;
            Max = max + Vector2.one * margin;
            Active = true;
            Debug.Log("[PlayArea] Zona de juego x " + Min.x.ToString("F2") + ".." + Max.x.ToString("F2")
                + ", z " + Min.y.ToString("F2") + ".." + Max.y.ToString("F2"));
        }

        // Quita del paso la parte que sacaria al punto de la zona.
        public static Vector3 Clip(Vector3 position, Vector3 step)
        {
            if (!Active)
            {
                return step;
            }

            Vector3 next = position + step;
            if ((next.x < Min.x && step.x < 0f) || (next.x > Max.x && step.x > 0f))
            {
                step.x = 0f;
            }

            if ((next.z < Min.y && step.z < 0f) || (next.z > Max.y && step.z > 0f))
            {
                step.z = 0f;
            }

            return step;
        }

        // Como Clip, pero para el jugador: tambien puede andar por el camino
        // hasta la sala. Si un paso lo sacaria, se deja la parte (x o z) que
        // si se puede, para que resbale por el borde en vez de frenar en seco.
        public static Vector3 ClipPlayer(Vector3 position, Vector3 step)
        {
            if (!Active || pathCells == null)
            {
                return Clip(position, step);
            }

            if (Allowed(position + step))
            {
                return step;
            }

            Vector3 onlyX = new Vector3(step.x, step.y, 0f);
            if (Allowed(position + onlyX))
            {
                return onlyX;
            }

            Vector3 onlyZ = new Vector3(0f, step.y, step.z);
            if (Allowed(position + onlyZ))
            {
                return onlyZ;
            }

            // Ya estaba fuera de todo (p.ej. caminando en la habitacion real):
            // como antes, solo no se aleja mas de la cocina.
            if (!Allowed(position))
            {
                return Clip(position, step);
            }

            return new Vector3(0f, step.y, 0f);
        }

        public static bool Allowed(Vector3 p)
        {
            if (!Active)
            {
                return true;
            }

            if (p.x >= Min.x && p.x <= Max.x && p.z >= Min.y && p.z <= Max.y)
            {
                return true;
            }

            if (pathCells == null)
            {
                return false;
            }

            int i = Mathf.FloorToInt((p.x - pathOrigin.x) / Cell);
            int j = Mathf.FloorToInt((p.z - pathOrigin.y) / Cell);
            return i >= 0 && j >= 0 && i < pathWidth && j < pathHeight && pathCells[j * pathWidth + i];
        }

        // Busca por el piso el camino mas corto desde la cocina hasta cerca
        // de 'target' (la manta) y deja que el jugador ande por el, con un
        // poco de holgura, y alrededor del objetivo. Devuelve false si no hay
        // camino (queda solo la cocina).
        public static bool AllowPlayerPathTo(Vector3 target, float targetRadius = 1.4f)
        {
            if (!Active)
            {
                return false;
            }

            Vector2 kitchenCenter = (Min + Max) * 0.5f;
            Vector3 probe = new Vector3(kitchenCenter.x, target.y + 2f, kitchenCenter.y);
            float floorY = 0f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit floorHit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                floorY = floorHit.point.y;
            }

            Vector2 t = new Vector2(target.x, target.z);
            Vector2 lo = Vector2.Min(Min, t) - Vector2.one * 3f;
            Vector2 hi = Vector2.Max(Max, t) + Vector2.one * 3f;
            int w = Mathf.CeilToInt((hi.x - lo.x) / Cell);
            int h = Mathf.CeilToInt((hi.y - lo.y) / Cell);
            if (w <= 0 || h <= 0 || w * h > 40000)
            {
                return false;
            }

            bool[] free = new bool[w * h];
            Collider[] hits = new Collider[16];
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float x = lo.x + (i + 0.5f) * Cell;
                    float z = lo.y + (j + 0.5f) * Cell;
                    free[j * w + i] = Walkable(new Vector3(x, floorY, z), hits);
                }
            }

            // Busqueda en anchura desde el piso libre de la cocina.
            int[] parent = new int[w * h];
            for (int k = 0; k < parent.Length; k++)
            {
                parent[k] = -2;
            }

            System.Collections.Generic.Queue<int> queue = new System.Collections.Generic.Queue<int>();
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    int k = j * w + i;
                    Vector2 c = new Vector2(lo.x + (i + 0.5f) * Cell, lo.y + (j + 0.5f) * Cell);
                    if (free[k] && c.x >= Min.x && c.x <= Max.x && c.y >= Min.y && c.y <= Max.y)
                    {
                        parent[k] = -1;
                        queue.Enqueue(k);
                    }
                }
            }

            const float reachDistance = 1.0f;
            int goal = -1;
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                int ci = k % w, cj = k / w;
                Vector2 c = new Vector2(lo.x + (ci + 0.5f) * Cell, lo.y + (cj + 0.5f) * Cell);
                if (Vector2.Distance(c, t) <= reachDistance)
                {
                    goal = k;
                    break;
                }

                for (int d = 0; d < 4; d++)
                {
                    int ni = ci + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nj = cj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                    {
                        continue;
                    }

                    int n = nj * w + ni;
                    if (free[n] && parent[n] == -2)
                    {
                        parent[n] = k;
                        queue.Enqueue(n);
                    }
                }
            }

            if (goal < 0)
            {
                Debug.LogWarning("[PlayArea] No hay camino por el piso desde la cocina hasta " + target.ToString("F2")
                    + "; el jugador sigue solo en la cocina.");
                return false;
            }

            bool[] allowed = new bool[w * h];
            const int slack = 2; // medio metro a cada lado del camino
            int steps = 0;
            for (int k = goal; k >= 0; k = parent[k])
            {
                steps++;
                int ci = k % w, cj = k / w;
                for (int dj = -slack; dj <= slack; dj++)
                {
                    for (int di = -slack; di <= slack; di++)
                    {
                        int ni = ci + di, nj = cj + dj;
                        if (ni >= 0 && nj >= 0 && ni < w && nj < h)
                        {
                            allowed[nj * w + ni] = true;
                        }
                    }
                }
            }

            // Alrededor de la manta, para acercarse y agarrarla con comodidad.
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    Vector2 c = new Vector2(lo.x + (i + 0.5f) * Cell, lo.y + (j + 0.5f) * Cell);
                    if (Vector2.Distance(c, t) <= targetRadius)
                    {
                        allowed[j * w + i] = true;
                    }
                }
            }

            pathCells = allowed;
            freeCells = free;
            pathWidth = w;
            pathHeight = h;
            pathOrigin = lo;
            pathFloorY = floorY;
            Debug.Log("[PlayArea] Camino del jugador hasta " + target.ToString("F2") + ": " + (steps * Cell).ToString("F1")
                + " m");
            return true;
        }

        // Camino por el piso desde 'from' hasta quedar a 'reach' metros de
        // 'to', solo por donde el jugador puede andar y sin muebles. Lo usa
        // FloorPathGuide para dibujar flechas. Deja en 'points' las esquinas
        // del camino (a la altura del piso) y devuelve false si no hay.
        public static bool FindPath(Vector3 from, Vector3 to, float reach, System.Collections.Generic.List<Vector3> points)
        {
            points.Clear();
            if (freeCells == null)
            {
                return false;
            }

            int w = pathWidth, h = pathHeight;
            int start = NearestPassable(from, 3);
            if (start < 0)
            {
                return false;
            }

            Vector2 t = new Vector2(to.x, to.z);
            int[] parent = new int[w * h];
            for (int k = 0; k < parent.Length; k++)
            {
                parent[k] = -2;
            }

            System.Collections.Generic.Queue<int> queue = new System.Collections.Generic.Queue<int>();
            parent[start] = -1;
            queue.Enqueue(start);
            int goal = -1;
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                int ci = k % w, cj = k / w;
                if (Vector2.Distance(CellCenter(ci, cj), t) <= reach)
                {
                    goal = k;
                    break;
                }

                for (int d = 0; d < 4; d++)
                {
                    int ni = ci + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nj = cj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ni < 0 || nj < 0 || ni >= w || nj >= h)
                    {
                        continue;
                    }

                    int n = nj * w + ni;
                    if (parent[n] == -2 && Passable(ni, nj))
                    {
                        parent[n] = k;
                        queue.Enqueue(n);
                    }
                }
            }

            if (goal < 0)
            {
                return false;
            }

            System.Collections.Generic.List<Vector2> cells = new System.Collections.Generic.List<Vector2>();
            for (int k = goal; k >= 0; k = parent[k])
            {
                cells.Add(CellCenter(k % w, k / w));
            }

            cells.Reverse();
            cells[0] = new Vector2(from.x, from.z);

            // Se quitan las esquinas de la grilla: desde cada punto se va
            // directo al mas lejano que se ve sin pasar por un mueble.
            int i = 0;
            points.Add(new Vector3(cells[0].x, pathFloorY, cells[0].y));
            while (i < cells.Count - 1)
            {
                int next = i + 1;
                for (int j = cells.Count - 1; j > i + 1; j--)
                {
                    if (ClearLine(cells[i], cells[j]))
                    {
                        next = j;
                        break;
                    }
                }

                points.Add(new Vector3(cells[next].x, pathFloorY, cells[next].y));
                i = next;
            }

            return true;
        }

        private static Vector2 CellCenter(int i, int j)
        {
            return new Vector2(pathOrigin.x + (i + 0.5f) * Cell, pathOrigin.y + (j + 0.5f) * Cell);
        }

        // Celda libre de muebles y dentro de la zona del jugador.
        private static bool Passable(int i, int j)
        {
            if (!freeCells[j * pathWidth + i])
            {
                return false;
            }

            Vector2 c = CellCenter(i, j);
            return Allowed(new Vector3(c.x, 0f, c.y));
        }

        private static bool PassablePoint(Vector2 p)
        {
            int i = Mathf.FloorToInt((p.x - pathOrigin.x) / Cell);
            int j = Mathf.FloorToInt((p.y - pathOrigin.y) / Cell);
            return i >= 0 && j >= 0 && i < pathWidth && j < pathHeight && Passable(i, j);
        }

        private static bool ClearLine(Vector2 a, Vector2 b)
        {
            float length = Vector2.Distance(a, b);
            int samples = Mathf.CeilToInt(length / (Cell * 0.4f));
            for (int s = 1; s < samples; s++)
            {
                if (!PassablePoint(Vector2.Lerp(a, b, s / (float)samples)))
                {
                    return false;
                }
            }

            return true;
        }

        // La celda libre mas cercana a 'p' (hasta 'radius' celdas), -1 si no hay.
        private static int NearestPassable(Vector3 p, int radius)
        {
            int ci = Mathf.FloorToInt((p.x - pathOrigin.x) / Cell);
            int cj = Mathf.FloorToInt((p.z - pathOrigin.y) / Cell);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int dj = -radius; dj <= radius; dj++)
            {
                for (int di = -radius; di <= radius; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= pathWidth || j >= pathHeight || !Passable(i, j))
                    {
                        continue;
                    }

                    float d = di * di + dj * dj;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = j * pathWidth + i;
                    }
                }
            }

            return best;
        }

        // Piso a la altura de la cocina y sin muebles ni paredes donde iria
        // el cuerpo del jugador (no cuentan los objetos sueltos, el jugador ni
        // la paciente).
        private static bool Walkable(Vector3 floorPoint, Collider[] hits)
        {
            Vector3 from = floorPoint + Vector3.up * 2.2f;
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 2.6f, ~0, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(hit.point.y - floorPoint.y) > 0.15f)
            {
                return false;
            }

            int n = Physics.OverlapCapsuleNonAlloc(floorPoint + Vector3.up * 0.35f, floorPoint + Vector3.up * 1.1f,
                0.15f, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i];
                if (c is CharacterController || c.name.StartsWith("Fisica_")
                    || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                    || c.GetComponentInParent<OVRCameraRig>() != null
                    || c.GetComponentInParent<PatientBody>() != null)
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
