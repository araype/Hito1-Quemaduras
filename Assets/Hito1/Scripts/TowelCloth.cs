using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Tela simulada para el pano (o cualquier objeto plano con BoxCollider y
    // Rigidbody). El objeto rigido sigue existiendo y es lo que se agarra (con
    // el Interaction SDK o con SimpleGrab); lo que se ve es una malla de tela
    // que se simula aparte (Verlet con restricciones de distancia):
    //   - apoyado: la tela queda extendida sobre la caja del objeto;
    //   - agarrado: la tela cuelga del punto por donde se tomo y se mueve con
    //     inercia;
    //   - soltado sobre la mano del paciente: queda apoyado en la mano y cae
    //     por los costados (el objeto rigido acompana a la mano, asi el
    //     contacto con la quemadura se mantiene sin sostenerlo).
    // La tela choca con la mano y el antebrazo del paciente (como esferas y
    // capsulas) y con el resto de la escena (rayos cortos por particula).
    // Ademas lleva un sensor (FirstAidItem del mismo tipo) en el centro de la
    // tela, para que cubrir la quemadura cuente aunque la caja rigida quede
    // un poco mas lejos.
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(250)]
    public class TowelCloth : MonoBehaviour
    {
        [Header("Tela")]
        [Tooltip("Particulas a lo largo del lado mas largo.")]
        [Range(6, 18)] public int resolution = 13;
        public float thickness = 0.006f;
        [Range(2, 16)] public int iterations = 10;
        [Range(0.9f, 1f)] public float damping = 0.985f;
        [Range(0f, 1f)] public float bendStiffness = 0.12f;
        [Range(0f, 1f)] public float friction = 0.6f;
        public string materialName = "Tela";

        [Header("Apoyar sobre la mano del paciente")]
        [Tooltip("Si se suelta a menos de esta distancia de la mano del paciente, queda apoyado en ella.")]
        public float attachDistance = 0.16f;
        public float palmRadius = 0.042f;
        public float forearmRadius = 0.033f;
        public float fingerRadius = 0.02f;

        [Header("Sensor de contacto")]
        public float coverProbeRadius = 0.06f;

        [Header("Sonido")]
        public float rustleVolume = 0.6f;

        private enum Mode
        {
            Free,
            Held,
            OnHand
        }

        private struct Link
        {
            public int a, b;
            public float rest;
            public float stiffness;
        }

        private const float Step = 1f / 90f;

        private Rigidbody body;
        private BoxCollider box;
        private Oculus.Interaction.Grabbable sdkGrabbable;
        private Grabbable simpleGrabbable;
        private Collider[] ownColliders;
        private OVRCameraRig rig;
        private PatientBody patient;
        private Transform patientHand, patientForeArm, patientKnuckle, patientFingertip;

        private int nx, ny, count;
        private Vector3[] pos, prev, restLocal, restFlat;
        private float[] invMass;
        private readonly List<Link> links = new List<Link>();

        private readonly List<int> pins = new List<int>();
        private readonly List<Vector3> pinLocal = new List<Vector3>();
        private int handAnchor = -1;
        private Vector3 handAnchorLocal;

        private Mode mode = Mode.Free;
        private float freeTime = 1f;
        private bool lockedOnHand;
        private float accumulator;
        private float nextRustle;

        private Mesh mesh;
        private Vector3[] meshVertices, meshNormals;
        private GameObject clothObject;
        private Rigidbody probe;
        private AudioSource rustle;
        private readonly RaycastHit[] hits = new RaycastHit[6];

        public bool IsOnPatientHand
        {
            get { return mode == Mode.OnHand; }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            sdkGrabbable = GetComponent<Oculus.Interaction.Grabbable>();
            simpleGrabbable = GetComponent<Grabbable>();
            ownColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Start()
        {
            if (box == null)
            {
                Debug.LogWarning("[TowelCloth] Hace falta un BoxCollider para saber el tamano de la tela.", this);
                enabled = false;
                return;
            }

            rig = FindObjectOfType<OVRCameraRig>();
            patient = FindObjectOfType<PatientBody>();
            if (patient != null && patient.IsReady)
            {
                patientHand = patient.HandBone(true);
                patientForeArm = patient.ForeArm(true);
                FindFingerBones();
            }

            BuildParticles();
            BuildMesh();
            BuildProbe();

            MeshRenderer original = GetComponent<MeshRenderer>();
            if (original != null)
            {
                original.enabled = false;
            }

            // Fuente aparte: la malla de la tela esta en coordenadas del mundo
            // y su objeto no se puede mover.
            GameObject sound = new GameObject(name + "_SonidoTela");
            sound.transform.SetParent(clothObject.transform, false);
            rustle = Hito1FX.CreateSource(sound, null, rustleVolume, false, 0.3f, 4f);
        }

        private void OnDestroy()
        {
            if (clothObject != null)
            {
                Destroy(clothObject);
            }

            if (probe != null)
            {
                Destroy(probe.gameObject);
            }

            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        private void OnDisable()
        {
            if (lockedOnHand)
            {
                lockedOnHand = false;
                Oculus.Interaction.RigidbodyKinematicLockerExtension.UnlockKinematic(body);
            }
        }

        // ---------------- construccion ----------------

        private void FindFingerBones()
        {
            if (patientHand == null)
            {
                return;
            }

            foreach (Transform child in patientHand)
            {
                if (child.name.Contains("Middle1"))
                {
                    patientKnuckle = child;
                }
            }

            Transform tip = patientKnuckle;
            while (tip != null && tip.childCount > 0)
            {
                tip = tip.GetChild(0);
            }

            patientFingertip = tip != patientKnuckle ? tip : null;
        }

        private void BuildParticles()
        {
            // La tela es el plano de los dos lados mas largos de la caja.
            Vector3 size = Vector3.Scale(box.size, Abs(transform.lossyScale));
            int thin = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] < size[thin])
                {
                    thin = i;
                }
            }

            int u = (thin + 1) % 3;
            int v = (thin + 2) % 3;
            if (size[u] < size[v])
            {
                int tmp = u;
                u = v;
                v = tmp;
            }

            nx = resolution;
            ny = Mathf.Max(4, Mathf.RoundToInt(resolution * size[v] / Mathf.Max(0.01f, size[u])));
            count = nx * ny;
            pos = new Vector3[count];
            prev = new Vector3[count];
            restLocal = new Vector3[count];
            restFlat = new Vector3[count];
            invMass = new float[count];

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    Vector3 local = box.center;
                    local[u] += (i / (float)(nx - 1) - 0.5f) * box.size[u];
                    local[v] += (j / (float)(ny - 1) - 0.5f) * box.size[v];
                    int k = j * nx + i;
                    restLocal[k] = local;
                    pos[k] = prev[k] = restFlat[k] = transform.TransformPoint(local);
                    invMass[k] = 1f;
                }
            }

            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    if (i + 1 < nx) AddLink(k, k + 1, 1f);
                    if (j + 1 < ny) AddLink(k, k + nx, 1f);
                    if (i + 1 < nx && j + 1 < ny)
                    {
                        AddLink(k, k + nx + 1, 0.7f);
                        AddLink(k + 1, k + nx, 0.7f);
                    }

                    if (i + 2 < nx) AddLink(k, k + 2, bendStiffness);
                    if (j + 2 < ny) AddLink(k, k + 2 * nx, bendStiffness);
                }
            }
        }

        private void AddLink(int a, int b, float stiffness)
        {
            if (stiffness <= 0f)
            {
                return;
            }

            links.Add(new Link { a = a, b = b, rest = Vector3.Distance(pos[a], pos[b]), stiffness = stiffness });
        }

        private void BuildMesh()
        {
            clothObject = new GameObject(name + "_Tela");
            clothObject.AddComponent<NoAutoCollider>();
            MeshFilter filter = clothObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = clothObject.AddComponent<MeshRenderer>();
            Material material = Hito1FX.Mat(materialName);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            // Dos capas (cara y dorso) porque el shader Standard no dibuja
            // las caras traseras.
            mesh = new Mesh { name = "Tela" };
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
                    uv[k] = uv[k + count] = new Vector2(i / (float)(nx - 1), j / (float)(ny - 1));
                    if (i + 1 >= nx || j + 1 >= ny)
                    {
                        continue;
                    }

                    int a = k, b = k + 1, c = k + nx, d = k + nx + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                    tris[t++] = a + count; tris[t++] = b + count; tris[t++] = c + count;
                    tris[t++] = b + count; tris[t++] = d + count; tris[t++] = c + count;
                }
            }

            mesh.vertices = meshVertices;
            mesh.normals = meshNormals;
            mesh.uv = uv;
            mesh.triangles = tris;
            filter.sharedMesh = mesh;
            UpdateMesh();
        }

        private void BuildProbe()
        {
            FirstAidItem item = GetComponent<FirstAidItem>();
            if (item == null)
            {
                return;
            }

            GameObject go = new GameObject(name + "_SensorTela");
            go.AddComponent<NoAutoCollider>();
            probe = go.AddComponent<Rigidbody>();
            probe.isKinematic = true;
            probe.useGravity = false;
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = coverProbeRadius;
            FirstAidItem copy = go.AddComponent<FirstAidItem>();
            copy.type = item.type;
            go.transform.position = transform.position;
        }

        // ---------------- estado ----------------

        private bool IsHeld()
        {
            if (sdkGrabbable != null && sdkGrabbable.SelectingPointsCount > 0)
            {
                return true;
            }

            if (simpleGrabbable != null && simpleGrabbable.IsHeld)
            {
                return true;
            }

            return SimpleGrab.IsHeldObject(body);
        }

        private void UpdateMode()
        {
            bool held = IsHeld();
            Mode next = mode;
            if (held)
            {
                next = Mode.Held;
            }
            else if (mode == Mode.Held)
            {
                next = NearPatientHand() ? Mode.OnHand : Mode.Free;
            }

            if (next == mode)
            {
                return;
            }

            Mode previous = mode;
            mode = next;
            pins.Clear();
            pinLocal.Clear();
            handAnchor = -1;
            for (int k = 0; k < count; k++)
            {
                invMass[k] = 1f;
            }

            if (previous == Mode.OnHand && lockedOnHand)
            {
                lockedOnHand = false;
                Oculus.Interaction.RigidbodyKinematicLockerExtension.UnlockKinematic(body);
            }

            if (mode == Mode.Held)
            {
                PinAtHand();
            }
            else if (mode == Mode.OnHand)
            {
                AttachToHand();
            }
            else
            {
                freeTime = 0f;
            }

            PlayRustle(0.8f);
        }

        private bool NearPatientHand()
        {
            if (patientHand == null)
            {
                return false;
            }

            Vector3 palm = PalmCenter();
            Vector3 centroid = Centroid();
            return Vector3.Distance(centroid, palm) < attachDistance
                || Vector3.Distance(body.position, palm) < attachDistance;
        }

        // La tela queda tomada de la zona que esta mas cerca de la mano que la
        // agarro (una pinza toma un poco de tela, no un solo punto).
        private void PinAtHand()
        {
            Vector3 grabPoint = NearestHandPoint();
            int nearest = 0;
            float best = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                float d = (pos[k] - grabPoint).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = k;
                }
            }

            float spacing = links.Count > 0 ? links[0].rest : 0.03f;
            for (int k = 0; k < count; k++)
            {
                if (Vector3.Distance(pos[k], pos[nearest]) <= spacing * 1.05f)
                {
                    pins.Add(k);
                    pinLocal.Add(transform.InverseTransformPoint(pos[k]));
                    invMass[k] = 0f;
                }
            }
        }

        private Vector3 NearestHandPoint()
        {
            if (rig == null)
            {
                rig = FindObjectOfType<OVRCameraRig>();
            }

            if (rig == null)
            {
                return body.position;
            }

            Vector3 center = Centroid();
            Transform left = rig.leftHandAnchor;
            Transform right = rig.rightHandAnchor;
            Vector3 l = left != null ? left.position + left.forward * 0.05f : center + Vector3.one * 100f;
            Vector3 r = right != null ? right.position + right.forward * 0.05f : center + Vector3.one * 100f;
            return (l - center).sqrMagnitude < (r - center).sqrMagnitude ? l : r;
        }

        // Apoya la tela en la mano: la particula mas cercana al dorso queda
        // sujeta (como si la tela descansara ahi) y el resto cae por su peso.
        private void AttachToHand()
        {
            Oculus.Interaction.RigidbodyKinematicLockerExtension.LockKinematic(body);
            lockedOnHand = true;

            Vector3 top = PalmCenter() + Vector3.up * (palmRadius + thickness);
            float best = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                float d = (pos[k] - top).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    handAnchor = k;
                }
            }

            handAnchorLocal = patientHand.InverseTransformPoint(top);
            invMass[handAnchor] = 0f;
            Debug.Log("[TowelCloth] Pano apoyado sobre la mano del paciente");
        }

        // ---------------- simulacion ----------------

        private void FixedUpdate()
        {
            if (mode == Mode.OnHand && patientHand != null)
            {
                // El objeto rigido acompana a la mano (sigue tocando la
                // quemadura y se puede volver a agarrar desde ahi).
                Vector3 target = PalmCenter() + Vector3.up * (palmRadius + 0.01f);
                body.MovePosition(target);
            }

            if (probe != null && count > 0)
            {
                probe.MovePosition(Centroid());
            }
        }

        private void LateUpdate()
        {
            if (count == 0)
            {
                return;
            }

            UpdateMode();

            accumulator = Mathf.Min(accumulator + Time.deltaTime, Step * 3f);
            while (accumulator >= Step)
            {
                accumulator -= Step;
                Simulate(Step);
            }

            UpdateMesh();
            UpdateRustle();
        }

        private void Simulate(float dt)
        {
            freeTime += dt;
            Vector3 gravity = Physics.gravity * dt * dt;

            // En el modo libre la tela se asienta sobre la caja del objeto: el
            // tiron hacia su forma extendida crece en el primer segundo.
            float restPull = mode == Mode.Free ? Mathf.Clamp01(freeTime) * 0.35f : 0f;

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

                if (restPull > 0f)
                {
                    pos[k] = Vector3.Lerp(pos[k], transform.TransformPoint(restLocal[k]), restPull);
                }
            }

            for (int i = 0; i < pins.Count; i++)
            {
                int k = pins[i];
                prev[k] = pos[k];
                pos[k] = transform.TransformPoint(pinLocal[i]);
            }

            if (handAnchor >= 0 && patientHand != null)
            {
                // Se desliza hasta el dorso de la mano y queda ahi.
                Vector3 anchor = patientHand.TransformPoint(handAnchorLocal);
                prev[handAnchor] = pos[handAnchor];
                pos[handAnchor] = Vector3.Lerp(pos[handAnchor], anchor, 0.35f);
            }

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

                CollideWithPatient();
            }

            ApplyTethers();

            if (mode != Mode.Free || restPull < 0.3f)
            {
                CollideWithScene();
            }
        }

        // Ninguna particula se aleja de un punto sujeto mas que su distancia
        // en la tela extendida (con un 2 % de holgura). Sin esto, la tela que
        // cuelga de una pinza se estira como un elastico.
        private void ApplyTethers()
        {
            for (int i = 0; i <= pins.Count; i++)
            {
                int anchor = i < pins.Count ? pins[i] : handAnchor;
                if (anchor < 0)
                {
                    continue;
                }

                for (int k = 0; k < count; k++)
                {
                    if (invMass[k] == 0f)
                    {
                        continue;
                    }

                    float limit = Vector3.Distance(restFlat[k], restFlat[anchor]) * 1.02f;
                    Vector3 d = pos[k] - pos[anchor];
                    float sq = d.sqrMagnitude;
                    if (sq > limit * limit)
                    {
                        pos[k] = pos[anchor] + d * (limit / Mathf.Sqrt(sq));
                    }
                }
            }
        }

        private void CollideWithPatient()
        {
            if (patientHand == null)
            {
                return;
            }

            Vector3 palm = PalmCenter();
            Vector3 wrist = patientHand.position;
            Vector3 elbow = patientForeArm != null ? patientForeArm.position : wrist;
            for (int k = 0; k < count; k++)
            {
                if (invMass[k] == 0f)
                {
                    continue;
                }

                bool touched = PushOutOfSphere(k, palm, palmRadius + thickness);
                touched |= PushOutOfCapsule(k, elbow, wrist, forearmRadius + thickness);
                if (patientKnuckle != null && patientFingertip != null)
                {
                    touched |= PushOutOfCapsule(k, patientKnuckle.position, patientFingertip.position,
                        fingerRadius + thickness);
                }

                if (touched)
                {
                    // Roce: la tela no resbala sola sobre la piel.
                    prev[k] = Vector3.Lerp(prev[k], pos[k], friction);
                }
            }
        }

        private bool PushOutOfSphere(int k, Vector3 center, float radius)
        {
            Vector3 d = pos[k] - center;
            float sq = d.sqrMagnitude;
            if (sq >= radius * radius)
            {
                return false;
            }

            float length = Mathf.Sqrt(sq);
            Vector3 n = length > 1e-6f ? d / length : Vector3.up;
            pos[k] = center + n * radius;
            return true;
        }

        private bool PushOutOfCapsule(int k, Vector3 a, Vector3 b, float radius)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(pos[k] - a, ab) / ab.sqrMagnitude) : 0f;
            return PushOutOfSphere(k, a + ab * t, radius);
        }

        // Rayo corto desde la posicion anterior: si la particula atraveso una
        // superficie (meson, lavadero, piso), queda apoyada sobre ella.
        private void CollideWithScene()
        {
            for (int k = 0; k < count; k++)
            {
                if (invMass[k] == 0f)
                {
                    continue;
                }

                Vector3 from = prev[k];
                Vector3 move = pos[k] - from;
                float distance = move.magnitude;
                if (distance < 0.0005f)
                {
                    continue;
                }

                Vector3 dir = move / distance;
                int n = Physics.RaycastNonAlloc(from - dir * thickness, dir, hits, distance + thickness * 2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                RaycastHit bestHit = default;
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].distance < best && !Ignored(hits[i].collider))
                    {
                        best = hits[i].distance;
                        bestHit = hits[i];
                    }
                }

                if (best == float.MaxValue)
                {
                    continue;
                }

                pos[k] = bestHit.point + bestHit.normal * thickness;
                prev[k] = Vector3.Lerp(prev[k], pos[k], friction);
            }
        }

        private bool Ignored(Collider col)
        {
            if (SimpleGrab.IsHandCollider(col))
            {
                return true;
            }

            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == col)
                {
                    return true;
                }
            }

            Rigidbody rb = col.attachedRigidbody;
            return rb != null && (rb == body || rb == probe || rb.GetComponent<ManoQuemadaVisibility>() != null);
        }

        // ---------------- malla y sonido ----------------

        private void UpdateMesh()
        {
            for (int j = 0; j < ny; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int k = j * nx + i;
                    Vector3 du = pos[j * nx + Mathf.Min(i + 1, nx - 1)] - pos[j * nx + Mathf.Max(i - 1, 0)];
                    Vector3 dv = pos[Mathf.Min(j + 1, ny - 1) * nx + i] - pos[Mathf.Max(j - 1, 0) * nx + i];
                    Vector3 n = Vector3.Cross(dv, du);
                    n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                    meshVertices[k] = pos[k] + n * (thickness * 0.25f);
                    meshVertices[k + count] = pos[k] - n * (thickness * 0.25f);
                    meshNormals[k] = n;
                    meshNormals[k + count] = -n;
                }
            }

            mesh.vertices = meshVertices;
            mesh.normals = meshNormals;
            mesh.RecalculateBounds();
        }

        private Vector3 Centroid()
        {
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < count; k++)
            {
                sum += pos[k];
            }

            return count > 0 ? sum / count : transform.position;
        }

        private Vector3 PalmCenter()
        {
            if (patientHand == null)
            {
                return transform.position;
            }

            return patientKnuckle != null
                ? Vector3.Lerp(patientHand.position, patientKnuckle.position, 0.6f)
                : patientHand.position;
        }

        private void UpdateRustle()
        {
            if (rustle == null || Time.time < nextRustle)
            {
                return;
            }

            // Velocidad media de la tela: si se sacude, suena.
            float speed = 0f;
            for (int k = 0; k < count; k += 3)
            {
                speed += (pos[k] - prev[k]).magnitude;
            }

            speed = speed / Mathf.Max(1, count / 3) / Step;
            if (speed > 0.7f)
            {
                PlayRustle(Mathf.InverseLerp(0.7f, 2.5f, speed));
            }
        }

        private void PlayRustle(float intensity)
        {
            if (rustle == null || Time.time < nextRustle)
            {
                return;
            }

            nextRustle = Time.time + Random.Range(0.25f, 0.45f);
            rustle.transform.position = Centroid();
            rustle.pitch = Random.Range(0.9f, 1.15f);
            AudioClip clip = Hito1FX.Clip("tela_roce_" + Random.Range(1, 4));
            if (clip != null)
            {
                rustle.PlayOneShot(clip, Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(intensity)) * rustleVolume);
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }
    }
}
