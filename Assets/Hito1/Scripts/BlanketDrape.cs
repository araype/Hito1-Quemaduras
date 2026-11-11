using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Manta echada sobre los hombros de la paciente, con tela simulada
    // (Verlet con restricciones de distancia, como TowelCloth). El borde de
    // arriba queda sujeto en la parte alta de la espalda y sobre los hombros
    // (sigue al pecho); el resto cae por su peso: por la espalda y, por los
    // costados, sobre los brazos y por delante. Choca con las capsulas y la
    // esfera del cuerpo (PatientPhysics), asi que se amolda a la postura y no
    // la atraviesa al mover los brazos.
    //
    // Reemplaza a la chompa (SweaterItem + SweaterDrape), que era una media
    // capa rigida con mangas de capsula y no se acoplaba al cuerpo.
    [DefaultExecutionOrder(260)]
    public class BlanketDrape : MonoBehaviour
    {
        [Header("Tela")]
        public float width = 1.35f;
        public float height = 0.85f;
        [Range(10, 30)] public int columns = 22;
        [Range(6, 20)] public int rows = 13;
        [Range(2, 12)] public int iterations = 6;
        [Range(0.9f, 1f)] public float damping = 0.97f;
        [Range(0f, 1f)] public float bendStiffness = 0.25f;
        [Tooltip("Cuanto frena el roce con el cuerpo (0 = resbala, 1 = se queda pegada).")]
        [Range(0f, 1f)] public float friction = 0.55f;
        [Tooltip("Separacion entre la tela y el cuerpo (grosor de la ropa).")]
        public float skin = 0.018f;
        public float thickness = 0.01f;
        [Tooltip("Cuanto del borde pasa por encima del hombro hacia adelante antes de caer (m).")]
        public float overShoulder = 0.08f;
        [Tooltip("Filas sujetas a la espalda debajo del borde (para que no se voltee al girar).")]
        [Range(0, 5)] public int backPinRows = 2;
        public string materialName = "Manta";
        public Color fallbackColor = new Color(0.62f, 0.2f, 0.22f);

        private struct Link
        {
            public int a, b;
            public float rest;
            public float stiffness;
        }

        private struct Shape
        {
            public Vector3 a, b;
            public float radius;
            public Vector3 center;
            public float reach;
        }

        // 72 pasos por segundo: un paso por cuadro en el Quest.
        private const float Step = 1f / 72f;

        private PatientBody patient;
        private Transform chest;
        private int nx, ny, count;
        private Vector3[] pos, prev;
        private float[] invMass;
        private readonly List<Link> links = new List<Link>();
        private readonly List<int> pins = new List<int>();
        private readonly List<Vector3> pinLocal = new List<Vector3>();
        private readonly List<Collider> bodyColliders = new List<Collider>();
        private Shape[] shapes = new Shape[0];
        private float floorY = float.NegativeInfinity;
        private float accumulator;

        private Mesh mesh;
        private Vector3[] meshVertices, meshNormals;

        [Header("Reposo (rendimiento)")]
        [Tooltip("Si ninguna parte de la tela se mueve mas que esto por paso (m) durante 'sleepAfter' s, deja de simularse y sigue al pecho.")]
        public float sleepSpeed = 0.0006f;
        public float sleepAfter = 1f;
        [Tooltip("Vuelve a simularse si alguna parte del cuerpo se mueve mas que esto desde que se durmio (m).")]
        public float wakeDistance = 0.02f;
        private bool sleeping;
        private float quietTime;
        private float lastStepMove;
        private Vector3[] sleepLocal;
        private Vector3[] sleepShapes;
        private Vector3 sleepChest;

        public bool Sleeping
        {
            get { return sleeping; }
        }

        // Crea la manta puesta sobre la paciente.
        public static BlanketDrape PutOn(PatientBody patient, string materialName)
        {
            GameObject go = new GameObject("MantaPuesta");
            go.AddComponent<NoAutoCollider>();
            BlanketDrape drape = go.AddComponent<BlanketDrape>();
            if (!string.IsNullOrEmpty(materialName))
            {
                drape.materialName = materialName;
            }

            drape.Init(patient);
            return drape;
        }

        private void Init(PatientBody body)
        {
            patient = body;
            chest = body.Chest;

            PatientPhysics physics = PatientPhysics.Current;
            if (physics != null)
            {
                foreach (Collider c in physics.BodyColliders)
                {
                    if (c != null)
                    {
                        bodyColliders.Add(c);
                    }
                }
            }

            if (Physics.Raycast(body.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                floorY = hit.point.y;
            }

            BuildParticles();
            BuildMesh();
            Hito1FX.PlayAt("tela_roce_1", chest.position, 0.8f);
        }

        // El borde de arriba va de un costado al otro pasando por los hombros
        // y por detras del cuello. La parte que cae sobre la espalda queda
        // sujeta; los extremos (sobre los brazos y por delante) cuelgan
        // libres. Las filas de abajo empiezan colgando derecho y caen por su
        // peso: la tela se acomoda sola en el primer segundo.
        private void BuildParticles()
        {
            nx = columns;
            ny = rows;
            count = nx * ny;
            pos = new Vector3[count];
            prev = new Vector3[count];
            invMass = new float[count];

            Vector3 up = patient.TorsoUp;
            Vector3 forward = patient.TorsoForward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            Vector3 leftShoulder = patient.ShoulderPosition(false);
            Vector3 rightShoulder = patient.ShoulderPosition(true);
            Vector3 center = (leftShoulder + rightShoulder) * 0.5f;
            float shoulderHalf = Mathf.Max(0.12f, Vector3.Distance(leftShoulder, rightShoulder) * 0.5f);
            Vector3 neck = patient.Neck != null ? patient.Neck.position : center + up * 0.08f;

            // Borde de arriba: medio ovalo por detras, a la altura de la parte
            // alta de los hombros (el trapecio), un poco por fuera del cuerpo.
            float halfWidth = shoulderHalf + 0.05f;
            float depth = 0.13f;
            Vector3 top = center + up * 0.07f;
            float arc = HalfEllipse(halfWidth, depth);
            float dx = width / (nx - 1);
            float dy = height / (ny - 1);

            for (int i = 0; i < nx; i++)
            {
                // Distancia a lo largo del borde, desde el centro de la espalda.
                float s = (i - (nx - 1) * 0.5f) * dx;
                float side = Mathf.Sign(s);
                Vector3 edge;
                bool onBack = Mathf.Abs(s) <= arc * 0.5f;
                if (onBack)
                {
                    float angle = EllipseAngle(Mathf.Abs(s), halfWidth, depth) * side;
                    // Los hombros bajan hacia afuera.
                    float drop = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle)), 2f) * 0.04f;
                    edge = top + right * Mathf.Sin(angle) * halfWidth - forward * Mathf.Cos(angle) * depth - up * drop;
                }
                else
                {
                    // Pasa por encima del hombro hacia adelante y cae.
                    float extra = Mathf.Abs(s) - arc * 0.5f;
                    Vector3 shoulderTop = top + right * side * halfWidth - up * 0.04f;
                    float over = Mathf.Min(extra, overShoulder + 0.02f);
                    edge = shoulderTop + forward * over - up * Mathf.Max(0f, extra - over);
                }

                for (int j = 0; j < ny; j++)
                {
                    int k = j * nx + i;
                    Vector3 p = edge - up * (j * dy);
                    if (!onBack)
                    {
                        // Los costados caen por fuera del brazo.
                        p += right * side * Mathf.Min(j * dy, 0.06f);
                    }

                    pos[k] = prev[k] = p;
                    invMass[k] = 1f;

                    // Se sujeta el borde de arriba de la espalda y el que pasa
                    // por encima de los hombros (como lo retiene el roce en
                    // una manta real; sin esto los extremos resbalaban hacia
                    // atras y quedaban detras de los brazos). En la espalda
                    // tambien las filas de abajo hasta los omoplatos: si solo
                    // se sujetaba el borde, al girar la paciente la manta se
                    // volteaba por encima de los hombros y quedaba delante.
                    bool pinned = (j == 0 && Mathf.Abs(s) <= arc * 0.5f + overShoulder)
                        || (onBack && j <= backPinRows && Mathf.Abs(s) <= arc * 0.42f);
                    if (pinned)
                    {
                        invMass[k] = 0f;
                        pins.Add(k);
                    }
                }
            }

            // Tambien la nuca, para que no resbale hacia atras.
            if (neck != Vector3.zero)
            {
                int mid = (nx - 1) / 2;
                pos[mid] = prev[mid] = Vector3.Lerp(pos[mid], neck - forward * 0.07f + up * 0.01f, 0.5f);
            }

            // Fuera del cuerpo desde el principio (los puntos sujetos no
            // chocan despues: si quedaban adentro, la tela salia del pecho).
            UpdateShapes();
            for (int k = 0; k < count; k++)
            {
                pos[k] = prev[k] = PushOut(pos[k]);
            }

            foreach (int k in pins)
            {
                pinLocal.Add(chest.InverseTransformPoint(pos[k]));
            }

            // Largos de reposo de la tela extendida (no de la forma inicial).
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    if (i + 1 < nx) AddLink(k, k + 1, dx, 1f);
                    if (j + 1 < ny) AddLink(k, k + nx, dy, 1f);
                    if (i + 1 < nx && j + 1 < ny)
                    {
                        float diagonal = Mathf.Sqrt(dx * dx + dy * dy);
                        AddLink(k, k + nx + 1, diagonal, 0.6f);
                        AddLink(k + 1, k + nx, diagonal, 0.6f);
                    }

                    if (i + 2 < nx) AddLink(k, k + 2, dx * 2f, bendStiffness);
                    if (j + 2 < ny) AddLink(k, k + 2 * nx, dy * 2f, bendStiffness);
                }
            }
        }

        private void AddLink(int a, int b, float rest, float stiffness)
        {
            if (stiffness > 0f)
            {
                links.Add(new Link { a = a, b = b, rest = rest, stiffness = stiffness });
            }
        }

        // Largo de medio ovalo (de -90 a 90 grados) con semiejes a (costado)
        // y b (fondo).
        private static float HalfEllipse(float a, float b)
        {
            float length = 0f;
            Vector2 last = new Vector2(-a, 0f);
            for (int i = 1; i <= 32; i++)
            {
                float t = Mathf.Lerp(-90f, 90f, i / 32f) * Mathf.Deg2Rad;
                Vector2 p = new Vector2(Mathf.Sin(t) * a, Mathf.Cos(t) * b);
                length += Vector2.Distance(last, p);
                last = p;
            }

            return length;
        }

        // Angulo (0 = centro de la espalda) al que se llega recorriendo
        // 'distance' por el ovalo desde el centro.
        private static float EllipseAngle(float distance, float a, float b)
        {
            float walked = 0f;
            Vector2 last = new Vector2(0f, b);
            for (int i = 1; i <= 64; i++)
            {
                float t = i / 64f * 90f * Mathf.Deg2Rad;
                Vector2 p = new Vector2(Mathf.Sin(t) * a, Mathf.Cos(t) * b);
                float d = Vector2.Distance(last, p);
                if (walked + d >= distance)
                {
                    float k = d > 1e-6f ? (distance - walked) / d : 0f;
                    return Mathf.Lerp((i - 1) / 64f, i / 64f, k) * 90f * Mathf.Deg2Rad;
                }

                walked += d;
                last = p;
            }

            return 90f * Mathf.Deg2Rad;
        }

        private void BuildMesh()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PropKit.Mat(materialName, fallbackColor);

            // Dos capas (cara y dorso): el shader Standard no dibuja las caras
            // traseras.
            mesh = new Mesh { name = "Manta" };
            mesh.MarkDynamic();
            meshVertices = new Vector3[count * 2];
            meshNormals = new Vector3[count * 2];
            Vector2[] uv = new Vector2[count * 2];
            int[] tris = new int[(nx - 1) * (ny - 1) * 12];
            int t = 0;
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    // Un cuadro de la textura cada ~35 cm.
                    uv[k] = uv[k + count] = new Vector2(i / (float)(nx - 1) * width / 0.7f,
                        j / (float)(ny - 1) * height / 0.7f);
                    if (i + 1 >= nx || j + 1 >= ny)
                    {
                        continue;
                    }

                    int a = k, b = k + 1, c = k + nx, d = k + nx + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = b; tris[t++] = d; tris[t++] = c;
                    tris[t++] = a + count; tris[t++] = c + count; tris[t++] = b + count;
                    tris[t++] = b + count; tris[t++] = c + count; tris[t++] = d + count;
                }
            }

            mesh.vertices = meshVertices;
            mesh.normals = meshNormals;
            mesh.uv = uv;
            mesh.triangles = tris;
            filter.sharedMesh = mesh;
            UpdateMesh();
        }

        private void LateUpdate()
        {
            if (count == 0 || chest == null)
            {
                return;
            }

            if (sleeping)
            {
                if (!ShouldWake())
                {
                    // Quieta: acompana al pecho (la respiracion, un giro leve)
                    // sin simular.
                    for (int k = 0; k < count; k++)
                    {
                        pos[k] = prev[k] = chest.TransformPoint(sleepLocal[k]);
                    }

                    UpdateMesh();
                    return;
                }

                sleeping = false;
                quietTime = 0f;
                accumulator = 0f;
            }

            accumulator = Mathf.Min(accumulator + Time.deltaTime, Step * 3f);
            while (accumulator >= Step)
            {
                accumulator -= Step;
                Simulate(Step);
                quietTime = lastStepMove < sleepSpeed ? quietTime + Step : 0f;
            }

            if (quietTime >= sleepAfter)
            {
                Sleep();
            }

            UpdateMesh();
        }

        // La simulacion cuesta ~1,5 ms por cuadro (medido en el editor del
        // PC). La mayor parte del tiempo la paciente esta quieta con la mano
        // en el agua: ahi la tela se "duerme" y solo sigue al pecho.
        private void Sleep()
        {
            sleeping = true;
            sleepChest = chest.position;
            if (sleepLocal == null)
            {
                sleepLocal = new Vector3[count];
            }

            for (int k = 0; k < count; k++)
            {
                sleepLocal[k] = chest.InverseTransformPoint(pos[k]);
            }

            UpdateShapes();
            sleepShapes = new Vector3[shapes.Length * 2];
            for (int i = 0; i < shapes.Length; i++)
            {
                sleepShapes[i * 2] = chest.InverseTransformPoint(shapes[i].a);
                sleepShapes[i * 2 + 1] = chest.InverseTransformPoint(shapes[i].b);
            }
        }

        // Despierta si una parte del cuerpo se movio respecto del pecho (un
        // brazo que sube) o si el pecho mismo se desplazo (camina, se gira).
        private bool ShouldWake()
        {
            UpdateShapes();
            if (sleepShapes == null || sleepShapes.Length != shapes.Length * 2)
            {
                return true;
            }

            float limit = wakeDistance * wakeDistance;
            for (int i = 0; i < shapes.Length; i++)
            {
                if (shapes[i].radius <= 0f)
                {
                    continue;
                }

                if ((chest.InverseTransformPoint(shapes[i].a) - sleepShapes[i * 2]).sqrMagnitude > limit
                    || (chest.InverseTransformPoint(shapes[i].b) - sleepShapes[i * 2 + 1]).sqrMagnitude > limit)
                {
                    return true;
                }
            }

            // Desplazamiento del pecho en el mundo (caminar): la tela debe
            // balancearse.
            Vector3 now = chest.position;
            bool moved = (now - sleepChest).sqrMagnitude > limit;
            return moved;
        }

        private void Simulate(float dt)
        {
            SimulateStep(dt);

            // Cuanto se movio la parte mas rapida de la tela en este paso
            // (para saber si ya se asento).
            float most = 0f;
            for (int k = 0; k < count; k++)
            {
                if (invMass[k] != 0f)
                {
                    most = Mathf.Max(most, (pos[k] - prev[k]).sqrMagnitude);
                }
            }

            lastStepMove = Mathf.Sqrt(most);
        }

        private void SimulateStep(float dt)
        {
            Vector3 gravity = Physics.gravity * dt * dt;
            for (int k = 0; k < count; k++)
            {
                if (invMass[k] == 0f)
                {
                    continue;
                }

                Vector3 p = pos[k];
                Vector3 velocity = (p - prev[k]) * damping;
                prev[k] = p;
                pos[k] = p + velocity + gravity;
            }

            for (int i = 0; i < pins.Count; i++)
            {
                int k = pins[i];
                prev[k] = pos[k];
                pos[k] = chest.TransformPoint(pinLocal[i]);
            }

            UpdateShapes();
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < links.Count; i++)
                {
                    Link link = links[i];
                    float wa = invMass[link.a];
                    float wb = invMass[link.b];
                    float w = wa + wb;
                    if (w == 0f)
                    {
                        continue;
                    }

                    Vector3 delta = pos[link.b] - pos[link.a];
                    float length = delta.magnitude;
                    if (length < 1e-6f)
                    {
                        continue;
                    }

                    Vector3 correction = delta * ((length - link.rest) / (length * w) * link.stiffness);
                    pos[link.a] += correction * wa;
                    pos[link.b] -= correction * wb;
                }

                // Choque con el cuerpo en la mitad de las vueltas y en la
                // ultima: alcanza para que no lo atraviese y cuesta la mitad.
                if ((it & 1) == 1 || it == iterations - 1)
                {
                    Collide(it == iterations - 1);
                }
            }
        }

        // Capsulas y esferas del cuerpo en coordenadas del mundo, una vez por
        // paso.
        private void UpdateShapes()
        {
            if (shapes.Length != bodyColliders.Count)
            {
                shapes = new Shape[bodyColliders.Count];
            }

            for (int i = 0; i < bodyColliders.Count; i++)
            {
                Collider col = bodyColliders[i];
                Shape shape = new Shape { radius = -1f };
                if (col != null && col.enabled && col.gameObject.activeInHierarchy)
                {
                    Transform t = col.transform;
                    Vector3 s = t.lossyScale;
                    float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                    if (col is CapsuleCollider capsule)
                    {
                        Vector3 axis = capsule.direction == 0 ? Vector3.right
                            : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                        float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                        shape.a = t.TransformPoint(capsule.center + axis * half);
                        shape.b = t.TransformPoint(capsule.center - axis * half);
                        shape.radius = capsule.radius * scale + skin;
                    }
                    else if (col is SphereCollider sphere)
                    {
                        shape.a = shape.b = t.TransformPoint(sphere.center);
                        shape.radius = sphere.radius * scale + skin;
                    }

                    shape.center = (shape.a + shape.b) * 0.5f;
                    shape.reach = Vector3.Distance(shape.a, shape.b) * 0.5f + shape.radius;
                }

                shapes[i] = shape;
            }
        }

        private void Collide(bool applyFriction)
        {
            for (int k = 0; k < count; k++)
            {
                if (invMass[k] == 0f)
                {
                    continue;
                }

                Vector3 p = PushOut(pos[k], out bool touched);
                if (p.y < floorY + 0.01f)
                {
                    p.y = floorY + 0.01f;
                    touched = true;
                }

                pos[k] = p;
                if (touched && applyFriction)
                {
                    // Roce: la tela apoyada pierde parte de su velocidad y no
                    // resbala por los hombros.
                    prev[k] = Vector3.Lerp(prev[k], p, friction);
                }
            }
        }

        private Vector3 PushOut(Vector3 p)
        {
            return PushOut(p, out _);
        }

        // Saca el punto de las capsulas y la esfera del cuerpo.
        private Vector3 PushOut(Vector3 p, out bool touched)
        {
            touched = false;
            for (int i = 0; i < shapes.Length; i++)
            {
                Shape s = shapes[i];
                if (s.radius <= 0f)
                {
                    continue;
                }

                Vector3 toCenter = p - s.center;
                if (toCenter.sqrMagnitude > s.reach * s.reach)
                {
                    continue;
                }

                Vector3 ab = s.b - s.a;
                float lengthSq = ab.sqrMagnitude;
                float along = lengthSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - s.a, ab) / lengthSq) : 0f;
                Vector3 closest = s.a + ab * along;
                Vector3 away = p - closest;
                float distance = away.magnitude;
                if (distance >= s.radius)
                {
                    continue;
                }

                if (distance < 1e-5f)
                {
                    away = -patient.TorsoForward;
                    distance = 1f;
                }

                p = closest + away / distance * s.radius;
                touched = true;
            }

            return p;
        }

        private void UpdateMesh()
        {
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    Vector3 du = pos[j * nx + Mathf.Min(i + 1, nx - 1)] - pos[j * nx + Mathf.Max(i - 1, 0)];
                    Vector3 dv = pos[Mathf.Min(j + 1, ny - 1) * nx + i] - pos[Mathf.Max(j - 1, 0) * nx + i];
                    Vector3 n = Vector3.Cross(du, dv);
                    n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                    meshVertices[k] = pos[k] + n * (thickness * 0.5f);
                    meshVertices[k + count] = pos[k] - n * (thickness * 0.5f);
                    meshNormals[k] = n;
                    meshNormals[k + count] = -n;
                }
            }

            mesh.vertices = meshVertices;
            mesh.normals = meshNormals;
            mesh.RecalculateBounds();
        }
    }
}
