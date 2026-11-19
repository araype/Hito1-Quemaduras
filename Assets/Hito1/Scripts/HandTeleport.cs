using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;

namespace SojaExiles
{
    // Teletransporte con las manos (sin controles) para ir lejos, por ejemplo
    // de la cocina a la sala por la manta. Caminar en el cuarto real sirve
    // para lo cercano, pero al salir del limite del Quest el visor oculta el
    // juego. Al teletransportarse, el limite viaja con el jugador.
    //
    // Gesto (2026-11-17): el mismo del Interaction SDK de Meta (pose "LShape"
    // del paquete y TeleportHandInteractor): la mano en "L", como una
    // pistola, y juntar el pulgar al indice para saltar.
    //  - Apuntar: indice y pulgar estirados, medio y anular doblados, la
    //    mano adelante con la palma hacia abajo o de costado (no hacia
    //    arriba ni hacia la cara: eso es la pausa de la muneca). Sale un arco
    //    punteado hasta el piso y un circulo donde se llegaria (azul si se
    //    puede, gris con una X si no) con una flecha hacia donde se quedara
    //    mirando.
    //  - Saltar: juntar el pulgar al costado del indice (cerrar la L). El
    //    circulo se va llenando mientras el pulgar se acerca. Para volver a
    //    saltar hay que abrir la L otra vez.
    //  - Se diferencia de los demas gestos: la pinza de botones y de agarrar
    //    junta las puntas del pulgar y el indice con los demas dedos sueltos;
    //    agarrar con la mano entera dobla tambien el indice; la pausa es con
    //    la palma hacia la cara; tocar botones con el dedo es con el pulgar
    //    recogido (el arco solo sale si la L empieza con el pulgar abierto).
    //  - La direccion sale del hombro hacia los nudillos (como el SDK de
    //    Meta), mezclada con la del indice: no tiembla al cerrar el pulgar.
    //    Ademas se salta al destino de un instante antes de cerrar.
    //  - Solo se llega al piso por donde se puede caminar (PlayArea), sin
    //    muebles encima y a no mas de 'maxDistance'.
    //  - No con la mano que sostiene algo, ni mientras se lleva a la paciente
    //    del antebrazo. Con la otra mano si: lo que se sostiene viaja con el
    //    jugador.
    // Antes (hasta 2026-11-17) se senalaba con el indice y se bajaba el
    // pulgar sobre el medio: con medio y anular doblados SimpleGrab tomaba la
    // mano por cerrada (puno) y el arco no salia casi nunca en el visor.
    // VRLocomotion ignora los gestos de caminar de una mano mientras apunta
    // (Busy) y SimpleGrab no agarra con ella (Aiming).
    //
    // Con controles (2026-11-17), el estandar de Meta: empujar el joystick
    // derecho hacia adelante saca el arco desde el control; soltarlo salta;
    // tirarlo hacia atras cancela. El joystick izquierdo sigue caminando y
    // el derecho a los costados gira. Con control se puede saltar aunque esa
    // mano sostenga algo (no hay conflicto de gestos).
    //
    // Si el arco cae sobre un mueble (el meson, una mesa) o en el piso
    // pegado a el, se aterriza en el piso libre mas cercano al pie del
    // mueble, del lado del jugador: apuntar al celular lleva frente al meson.
    // Asi tambien sirve para acomodarse cerca (desde 'minDistance', 0,4 m).
    [RequireComponent(typeof(VRLocomotion))]
    public class HandTeleport : MonoBehaviour
    {
        [Tooltip("Distancia maxima del salto (m, en horizontal).")]
        public float maxDistance = 5f;
        [Tooltip("Distancia minima del salto (m): mas cerca se camina.")]
        public float minDistance = 0.4f;
        [Tooltip("Segundos que hay que mantener la L antes de que salga el arco.")]
        public float holdSeconds = 0.15f;
        [Tooltip("Velocidad inicial del arco (m/s): mas alta, llega mas lejos.")]
        public float launchSpeed = 7f;
        [Tooltip("Indice estirado: distancia de la punta al centro de la palma, en largos de palma.")]
        public float extendedRatio = 1.1f;
        [Tooltip("Medio y anular doblados: distancia de la punta al centro de la palma, en largos de palma.")]
        public float curledRatio = 1.0f;
        [Tooltip("Pulgar abierto (L) si su punta queda a mas de esto del indice, en largos de palma.")]
        public float thumbOpenRatio = 0.55f;
        [Tooltip("Pulgar junto al indice (salta) si su punta queda a menos de esto, en largos de palma.")]
        public float thumbClosedRatio = 0.3f;
        [Tooltip("Coseno maximo entre la palma y 'arriba' (0,5 = 60 grados): con la palma hacia arriba no apunta.")]
        public float maxPalmUp = 0.5f;
        [Tooltip("Coseno maximo entre la palma y la direccion a los ojos: mirandose la palma no apunta (pausa).")]
        public float maxPalmToEyes = 0.6f;
        [Tooltip("La mano debe estar al menos a esta distancia de la cabeza (m): pegada al cuerpo no apunta.")]
        public float minReach = 0.2f;
        [Tooltip("Sin arco si la punta del indice esta a menos de esto de un boton (m): se esta por tocarlo.")]
        public float buttonClearance = 0.15f;
        [Tooltip("Segundos que se tolera perder la pose (temblor, mano tapada) sin apagar el arco.")]
        public float lostGrace = 0.2f;
        [Tooltip("Peso de la direccion del indice frente a la del brazo (hombro a nudillos).")]
        [Range(0f, 1f)] public float fingerWeight = 0.35f;
        [Tooltip("Se salta al destino de este tiempo antes (s): al cerrar el pulgar la mano se mueve un poco.")]
        public float targetLag = 0.12f;
        [Tooltip("Muestra el texto de ayuda junto a la mano durante los primeros saltos (solo en modo guiado; en evaluacion no hay ayudas).")]
        public int hintTeleports = 3;
        public float cooldownSeconds = 0.6f;
        [Tooltip("Hasta que distancia (m) se busca piso libre al pie de un mueble o junto a un obstaculo.")]
        public float nudgeRadius = 0.6f;
        [Tooltip("Joystick hacia adelante a partir de esto (0-1) saca el arco con control.")]
        public float stickAim = 0.6f;
        [Tooltip("Joystick por debajo de esto (0-1): suelto, salta.")]
        public float stickRelease = 0.25f;

        public Color validColor = new Color(0.25f, 0.7f, 1f, 0.95f);
        public Color invalidColor = new Color(0.62f, 0.64f, 0.7f, 0.6f);

        private const float ArcStep = 0.03f;
        private const int ArcMaxSteps = 60;
        private const float BusyAfterSeconds = 0.5f;
        private const float RingRadius = 0.28f;

        private static readonly float[] busyUntil = { -1f, -1f };
        private static readonly bool[] aiming = new bool[2];

        // True si esa mano esta apuntando para teletransportarse (o lo acaba
        // de hacer): sus gestos de caminar no cuentan.
        public static bool Busy(bool leftHand)
        {
            return Time.unscaledTime < busyUntil[leftHand ? 0 : 1];
        }

        // True si esa mano esta en la L (aunque el arco todavia no salga):
        // SimpleGrab no agarra con ella (cerrar el pulgar no es una pinza).
        public static bool Aiming(bool leftHand)
        {
            return aiming[leftHand ? 0 : 1];
        }

        // Veces que se teletransporto en la partida (para el registro y el
        // tutorial).
        public static int Count { get; private set; }

        // El tutorial lo pone en true: el texto de ayuda sale siempre.
        public static bool ForceHints;

        public static HandTeleport Instance { get; private set; }

#if UNITY_EDITOR
        // Mano simulada (con SimpleGrab.SimActive / SimPosition / SimRotation):
        // SimAim hace la L apuntando hacia SimRotation * adelante; SimPinch
        // junta el pulgar al indice. Para probar desde el MCP de Unity sin
        // visor.
        public static readonly bool[] SimAim = new bool[2];
        public static readonly bool[] SimPinch = new bool[2];
        // Joystick de la mano simulada cuando hace de control
        // (SimpleGrab.SimController).
        public static readonly Vector2[] SimStick = new Vector2[2];
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics()
        {
            for (int i = 0; i < 2; i++)
            {
#if UNITY_EDITOR
                SimAim[i] = false;
                SimPinch[i] = false;
                SimStick[i] = Vector2.zero;
#endif
                busyUntil[i] = -1f;
                aiming[i] = false;
            }

            Count = 0;
            ForceHints = false;
            Instance = null;
        }

        private struct Sample
        {
            public float time;
            public bool valid;
            public Vector3 point;
        }

        private class HandState
        {
            public float poseSince = -1f;
            public float lostSince = -1f;
            public bool active;
            public bool armed;
            public bool controller;
            public float progress;
            public bool hasTarget;
            public bool targetValid;
            public Vector3 target;
            public Vector3 hintAnchor;
            public readonly List<Sample> history = new List<Sample>();
            public LineRenderer arc;
            public LineRenderer ring;
            public LineRenderer arrow;
            public LineRenderer cross;
            public MeshRenderer fill;
            public TextMeshPro hint;
        }

        private readonly HandState[] hands = { new HandState(), new HandState() };
        private readonly List<Vector3> arcPoints = new List<Vector3>();
        private VRLocomotion locomotion;
        private Material lineMaterial;
        private Material dotMaterial;
        private Texture2D dotTexture;
        private Mesh discMesh;
        private float nextTeleport;
        private float nextLookup;
        private bool jumping;
        private Oculus.Interaction.HandVisual[] visuals = new Oculus.Interaction.HandVisual[0];
        private TouchButton[] buttons = new TouchButton[0];
        private Renderer blink;
        private OVRCameraRig rig;
        private AudioClip jumpClip;

        // Meta del tutorial (circulo en el piso con un haz de luz).
        private Transform goal;
        private LineRenderer goalRing;
        private LineRenderer goalBeam;

        private void Awake()
        {
            Instance = this;
            locomotion = GetComponent<VRLocomotion>();
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lineMaterial.renderQueue = 3002;
            dotTexture = BuildDotTexture();
            dotMaterial = new Material(lineMaterial);
            dotMaterial.mainTexture = dotTexture;
            dotMaterial.mainTextureScale = new Vector2(14f, 1f);
            discMesh = BuildDisc(48);
            for (int i = 0; i < 2; i++)
            {
                HandState s = hands[i];
                s.arc = CreateLine("ArcoTeleport" + i, false, 0.02f, dotMaterial);
                s.arc.textureMode = LineTextureMode.Tile;
                s.ring = CreateLine("DestinoTeleport" + i, true, 0.02f, lineMaterial);
                s.arrow = CreateLine("FlechaTeleport" + i, false, 0.025f, lineMaterial);
                s.cross = CreateLine("NoTeleport" + i, false, 0.02f, lineMaterial);
                s.fill = CreateDisc("RellenoTeleport" + i);
                s.hint = CreateHint("AyudaTeleport" + i);
            }

            jumpClip = Hito1FX.Clip("teleport_salto");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Destroy(lineMaterial);
            Destroy(dotMaterial);
            Destroy(dotTexture);
            Destroy(discMesh);
            HideGoal();
        }

        // ---------------- construccion ----------------

        private static Texture2D BuildDotTexture()
        {
            // Un punto redondo por tramo: el arco se ve punteado.
            const int w = 32, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - w * 0.5f) / (w * 0.32f);
                    float dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Mesh BuildDisc(int segments)
        {
            Mesh mesh = new Mesh { name = "DiscoTeleport" };
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % segments;
                triangles[i * 3 + 2] = 1 + i;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private LineRenderer CreateLine(string name, bool loop, float width, Material material)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = loop;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private MeshRenderer CreateDisc(string name)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            go.AddComponent<MeshFilter>().sharedMesh = discMesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.material = new Material(lineMaterial) { renderQueue = 3001 };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        private static TextMeshPro CreateHint(string name)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            if (PanelText.Font != null)
            {
                tmp.font = PanelText.Font;
            }

            tmp.fontSize = 0.2f;
            tmp.alignment = TextAlignmentOptions.Bottom;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(0.3f, 0.05f);
            tmp.rectTransform.pivot = new Vector2(0.5f, 0f);
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(10, 20, 40, 255);
            MeshRenderer r = tmp.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return tmp;
        }

        // ---------------- ciclo ----------------

        private void Update()
        {
            if (Time.unscaledTime >= nextLookup)
            {
                // El rig y los paneles se arman en runtime: se buscan cada 2 s.
                nextLookup = Time.unscaledTime + 2f;
                visuals = FindObjectsOfType<Oculus.Interaction.HandVisual>();
                buttons = FindObjectsOfType<TouchButton>();
                if (rig == null)
                {
                    rig = FindObjectOfType<OVRCameraRig>();
                }
            }

            for (int i = 0; i < 2; i++)
            {
                UpdateHand(i == 0, hands[i]);
            }

            UpdateGoal();
        }

        private void UpdateHand(bool isLeft, HandState state)
        {
            int side = isLeft ? 0 : 1;
            float now = Time.unscaledTime;
            Transform head = VRPanelKit.Head();

            bool controller = SimpleGrab.UsesController(isLeft);
            if (controller != state.controller)
            {
                Deactivate(state);
                state.controller = controller;
            }

            if (controller)
            {
                aiming[side] = false;
                UpdateController(isLeft, state, head, now);
                return;
            }

            bool pose = false;
            float thumb = 1f;
            Vector3 origin = Vector3.zero, direction = Vector3.forward, knuckle = Vector3.zero;
            if (CanTeleport(isLeft) && head != null)
            {
                pose = ReadPose(isLeft, head, out origin, out direction, out knuckle, out thumb);
            }

            aiming[side] = pose && (state.active || thumb > thumbClosedRatio);
            if (pose)
            {
                state.lostSince = -1f;
                if (!state.active)
                {
                    // La L tiene que empezar con el pulgar abierto: senalar
                    // un boton con el pulgar recogido no saca el arco.
                    if (thumb < thumbOpenRatio)
                    {
                        state.poseSince = -1f;
                    }
                    else if (state.poseSince < 0f)
                    {
                        state.poseSince = now;
                    }

                    if (state.poseSince >= 0f && now - state.poseSince >= holdSeconds)
                    {
                        state.active = true;
                        state.armed = true;
                        state.history.Clear();
                    }
                }
            }
            else if (state.active)
            {
                // Un temblor o la mano tapada un instante no apagan el arco.
                if (state.lostSince < 0f)
                {
                    state.lostSince = now;
                }

                if (now - state.lostSince > lostGrace)
                {
                    Deactivate(state);
                }
            }
            else
            {
                state.poseSince = -1f;
            }

            if (pose || state.active)
            {
                busyUntil[side] = now + BusyAfterSeconds;
            }

            if (!state.active)
            {
                Show(state, false);
                return;
            }

            if (!pose)
            {
                // Dentro del margen: se deja lo ultimo que se mostro.
                return;
            }

            state.hasTarget = TraceArc(origin, direction, head, out state.target, out state.targetValid);
            state.history.Add(new Sample { time = now, valid = state.hasTarget && state.targetValid, point = state.target });
            while (state.history.Count > 0 && now - state.history[0].time > 0.5f)
            {
                state.history.RemoveAt(0);
            }

            if (thumb > thumbOpenRatio)
            {
                state.armed = true;
            }

            state.progress = state.armed
                ? Mathf.Clamp01(Mathf.InverseLerp(thumbOpenRatio, thumbClosedRatio, thumb))
                : 0f;
            state.hintAnchor = knuckle;
            Draw(state, head);

            if (state.armed && thumb < thumbClosedRatio && now >= nextTeleport && !jumping)
            {
                state.armed = false;
                if (LaggedTarget(state, now, out Vector3 point))
                {
                    nextTeleport = now + cooldownSeconds;
                    StartCoroutine(Jump(point));
                    // Tras el salto el arco se apaga: para el siguiente hay
                    // que volver a abrir la L.
                    Deactivate(state);
                }
            }
        }

        private static void Deactivate(HandState state)
        {
            state.active = false;
            state.armed = false;
            state.poseSince = -1f;
            state.lostSince = -1f;
            state.progress = 0f;
            state.hasTarget = false;
            state.history.Clear();
            Show(state, false);
        }

        // Destino de hace 'targetLag' segundos (si era valido); si no, el de
        // ahora.
        private bool LaggedTarget(HandState state, float now, out Vector3 point)
        {
            point = Vector3.zero;
            for (int i = state.history.Count - 1; i >= 0; i--)
            {
                Sample s = state.history[i];
                if (now - s.time >= targetLag)
                {
                    if (s.valid)
                    {
                        point = s.point;
                        return true;
                    }

                    break;
                }
            }

            if (state.hasTarget && state.targetValid)
            {
                point = state.target;
                return true;
            }

            return false;
        }

        private IEnumerator Jump(Vector3 point)
        {
            jumping = true;
            // Parpadeo corto: saltar sin el mueve el mundo de golpe y marea.
            yield return Fade(0f, 1f, 0.07f);
            Count++;
            locomotion.TeleportTo(point);
            Debug.Log("[HandTeleport] Salto a " + point.ToString("F2") + " (" + Count + ")");
            if (jumpClip != null)
            {
                AudioSource.PlayClipAtPoint(jumpClip, point + Vector3.up * 1.2f, 0.6f);
            }

            yield return null;
            yield return Fade(1f, 0f, 0.15f);
            jumping = false;
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            Renderer r = BlinkRenderer();
            if (r == null)
            {
                yield break;
            }

            r.enabled = true;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(r, Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }

            SetAlpha(r, to);
            r.enabled = to > 0.001f;
        }

        private static void SetAlpha(Renderer r, float a)
        {
            r.material.color = new Color(0.02f, 0.03f, 0.05f, a);
        }

        // Esfera oscura alrededor de la cabeza para el parpadeo.
        private Renderer BlinkRenderer()
        {
            if (blink != null)
            {
                return blink;
            }

            Transform head = VRPanelKit.Head();
            if (head == null)
            {
                return null;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ParpadeoTeleport";
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            go.transform.SetParent(head, false);
            go.transform.localScale = Vector3.one * 0.4f;
            blink = go.GetComponent<Renderer>();
            blink.material = new Material(lineMaterial) { renderQueue = 4000 };
            blink.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blink.receiveShadows = false;
            blink.enabled = false;
            return blink;
        }

        // Solo con hand tracking (con controles se camina con el joystick),
        // fuera del menu de pausa, con esa mano libre y sin llevar a la
        // paciente.
        private bool CanTeleport(bool isLeft)
        {
            if (!CanTeleportAtAll())
            {
                return false;
            }

#if UNITY_EDITOR
            bool simulated = SimpleGrab.SimActive[isLeft ? 0 : 1];
#else
            const bool simulated = false;
#endif
            if (!simulated && (OVRInput.GetActiveController() & OVRInput.Controller.Hands) == 0)
            {
                return false;
            }

            // Con algo en esa mano no. No se mira si la mano esta "cerrada"
            // (SimpleGrab.IsGripping): la L tiene medio y anular doblados y
            // contaba como puno.
            if (SimpleGrab.HeldBody(isLeft) != null)
            {
                return false;
            }

            return !HoldingPatient(true) && !HoldingPatient(false);
        }

        private bool CanTeleportAtAll()
        {
            return !PauseMenu.Paused && locomotion != null && locomotion.enabled && !HoldingPatient(true)
                && !HoldingPatient(false);
        }

        // ---------------- control ----------------

        // Joystick derecho: adelante saca el arco desde el control, soltarlo
        // salta, atras cancela. El izquierdo es para caminar.
        private void UpdateController(bool isLeft, HandState state, Transform head, float now)
        {
            int side = isLeft ? 0 : 1;
            Vector2 stick = Vector2.zero;
            Vector3 origin = Vector3.zero, direction = Vector3.forward;
            bool tracked = false;
            if (!isLeft && head != null && CanTeleportAtAll())
            {
                tracked = ReadController(out stick, out origin, out direction);
            }

            if (!tracked)
            {
                if (state.active)
                {
                    Deactivate(state);
                }

                return;
            }

            if (!state.active)
            {
                bool pushed = stick.y >= stickAim && Mathf.Abs(stick.x) < stick.y;
                if (!pushed)
                {
                    state.poseSince = -1f;
                    Show(state, false);
                    return;
                }

                if (state.poseSince < 0f)
                {
                    state.poseSince = now;
                }

                if (now - state.poseSince < 0.05f)
                {
                    return;
                }

                state.active = true;
                state.armed = true;
                state.history.Clear();
            }

            busyUntil[side] = now + BusyAfterSeconds;
            if (stick.y < -0.5f)
            {
                // Hacia atras: cancelar.
                Deactivate(state);
                return;
            }

            if (stick.magnitude < stickRelease)
            {
                bool ok = state.hasTarget && state.targetValid;
                Vector3 point = state.target;
                Deactivate(state);
                if (ok && now >= nextTeleport && !jumping)
                {
                    nextTeleport = now + cooldownSeconds;
                    StartCoroutine(Jump(point));
                }

                return;
            }

            state.hasTarget = TraceArc(origin, direction, head, out state.target, out state.targetValid);
            state.progress = 0f;
            state.hintAnchor = origin;
            Draw(state, head);
        }

        private bool ReadController(out Vector2 stick, out Vector3 origin, out Vector3 direction)
        {
            stick = Vector2.zero;
            origin = Vector3.zero;
            direction = Vector3.forward;
#if UNITY_EDITOR
            if (SimpleGrab.SimActive[1])
            {
                stick = SimStick[1];
                origin = SimpleGrab.SimPosition[1];
                direction = SimpleGrab.SimRotation[1] * Vector3.forward;
                return true;
            }
#endif
            if (rig == null || rig.rightHandAnchor == null
                || !OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
            {
                return false;
            }

            stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            origin = rig.rightHandAnchor.position;
            direction = rig.rightHandAnchor.forward;
            return true;
        }

        private static bool HoldingPatient(bool leftHand)
        {
            Rigidbody rb = SimpleGrab.HeldBody(leftHand);
            return rb != null && (rb.GetComponentInParent<PatientBody>() != null
                || (PatientPhysics.Current != null && rb.transform.IsChildOf(PatientPhysics.Current.transform)));
        }

        private bool NearButton(Vector3 tip)
        {
            float limit = buttonClearance * buttonClearance;
            foreach (TouchButton button in buttons)
            {
                if (button != null && button.isActiveAndEnabled
                    && (button.transform.position - tip).sqrMagnitude < limit)
                {
                    return true;
                }
            }

            return false;
        }

        // La L con la mano que se ve. Devuelve el origen del arco (nudillo
        // del indice), hacia donde apunta y que tan abierto esta el pulgar
        // (distancia de su punta al indice, en largos de palma).
        private bool ReadPose(bool isLeft, Transform head, out Vector3 origin, out Vector3 direction,
            out Vector3 knuckle, out float thumb)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;
            knuckle = Vector3.zero;
            thumb = 1f;
#if UNITY_EDITOR
            int s = isLeft ? 0 : 1;
            if (SimpleGrab.SimActive[s])
            {
                if (!SimAim[s])
                {
                    return false;
                }

                knuckle = SimpleGrab.SimPosition[s];
                origin = knuckle;
                direction = SimpleGrab.SimRotation[s] * Vector3.forward;
                thumb = SimPinch[s] ? 0.1f : 0.9f;
                return Reach(head, origin);
            }
#endif
            IHand hand = VisibleHand(isLeft);
            if (hand == null)
            {
                return false;
            }

            if (!hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist)
                || !hand.GetJointPose(HandJointId.HandMiddle1, out Pose middle1)
                || !hand.GetJointPose(HandJointId.HandIndex1, out Pose index1)
                || !hand.GetJointPose(HandJointId.HandIndex3, out Pose index3)
                || !hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexTip)
                || !hand.GetJointPose(HandJointId.HandPinky1, out Pose pinky1)
                || !hand.GetJointPose(HandJointId.HandMiddleTip, out Pose middleTip)
                || !hand.GetJointPose(HandJointId.HandRingTip, out Pose ringTip)
                || !hand.GetJointPose(HandJointId.HandThumbTip, out Pose thumbTip))
            {
                return false;
            }

            float palm = Vector3.Distance(wrist.position, middle1.position);
            if (palm < 0.03f)
            {
                return false;
            }

            // Mismo centro de la palma que SimpleGrab.UpdatePalm.
            Vector3 center = Vector3.Lerp(wrist.position, middle1.position, 0.6f);
            float index = Vector3.Distance(indexTip.position, center) / palm;
            float middle = Vector3.Distance(middleTip.position, center) / palm;
            float ring = Vector3.Distance(ringTip.position, center) / palm;
            if (index < extendedRatio || middle > curledRatio || ring > curledRatio)
            {
                return false;
            }

            // Normal de la palma, igual que SimpleGrab.TryGetPalm.
            Vector3 a = index1.position - wrist.position;
            Vector3 b = pinky1.position - wrist.position;
            Vector3 normal = isLeft ? Vector3.Cross(b, a) : Vector3.Cross(a, b);
            if (normal.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            normal.Normalize();
            Vector3 toEyes = (head.position - wrist.position).normalized;
            if (Vector3.Dot(normal, Vector3.up) > maxPalmUp || Vector3.Dot(normal, toEyes) > maxPalmToEyes)
            {
                return false;
            }

            thumb = DistanceToSegment(thumbTip.position, index1.position, index3.position) / palm;

            // Del hombro (estimado) a los nudillos, mezclado con el indice.
            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f)
            {
                flatForward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            Vector3 right = Vector3.Cross(Vector3.up, flatForward.normalized);
            Vector3 shoulder = head.position + Vector3.down * 0.15f + right * (isLeft ? -0.17f : 0.17f);
            Vector3 arm = (index1.position - shoulder).normalized;
            Vector3 finger = (indexTip.position - index1.position).normalized;
            direction = Vector3.Slerp(arm, finger, fingerWeight).normalized;
            knuckle = index1.position;
            origin = index1.position;
            return Reach(head, indexTip.position) && !NearButton(indexTip.position);
        }

        // Pegada al cuerpo no es apuntar lejos.
        private bool Reach(Transform head, Vector3 point)
        {
            Vector3 reach = point - head.position;
            reach.y = 0f;
            return reach.magnitude >= minReach;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        // Distancia de la punta del pulgar a la del indice, el medio y el
        // anular, en largos de palma, con la mano que se ve. La usa
        // VRLocomotion para los gestos de caminar (mas fiable que la "fuerza
        // de pinza" de OVRHand, que en el Quest Pro no los reconocia).
        public static bool TryFingerGaps(bool isLeft, out float index, out float middle, out float ring,
            out Vector3 indexTip)
        {
            index = middle = ring = 10f;
            indexTip = Vector3.zero;
            IHand hand = Instance != null ? Instance.VisibleHand(isLeft) : null;
            if (hand == null
                || !hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist)
                || !hand.GetJointPose(HandJointId.HandMiddle1, out Pose middle1)
                || !hand.GetJointPose(HandJointId.HandThumbTip, out Pose thumbTip)
                || !hand.GetJointPose(HandJointId.HandIndexTip, out Pose iTip)
                || !hand.GetJointPose(HandJointId.HandMiddleTip, out Pose mTip)
                || !hand.GetJointPose(HandJointId.HandRingTip, out Pose rTip))
            {
                return false;
            }

            float palm = Vector3.Distance(wrist.position, middle1.position);
            if (palm < 0.03f)
            {
                return false;
            }

            index = Vector3.Distance(thumbTip.position, iTip.position) / palm;
            middle = Vector3.Distance(thumbTip.position, mTip.position) / palm;
            ring = Vector3.Distance(thumbTip.position, rTip.position) / palm;
            indexTip = iTip.position;
            return true;
        }

        // Hay un boton a menos de 'buttonClearance' de ese punto.
        public static bool NearAnyButton(Vector3 point)
        {
            return Instance != null && Instance.NearButton(point);
        }

        private IHand VisibleHand(bool isLeft)
        {
            Handedness side = isLeft ? Handedness.Left : Handedness.Right;
            foreach (Oculus.Interaction.HandVisual visual in visuals)
            {
                if (visual == null || !visual.isActiveAndEnabled || !visual.IsVisible)
                {
                    continue;
                }

                IHand h = visual.Hand;
                if (h != null && h.IsTrackedDataValid && h.Handedness == side && h.IsHighConfidence)
                {
                    return h;
                }
            }

            return null;
        }

        // ---------------- arco y destino ----------------

        // Arco de un tiro parabolico. Devuelve false si no toca nada; si
        // toca, 'valid' dice si se puede llegar ahi.
        private bool TraceArc(Vector3 origin, Vector3 direction, Transform head, out Vector3 point, out bool valid)
        {
            arcPoints.Clear();
            arcPoints.Add(origin);
            point = Vector3.zero;
            valid = false;

            Vector3 p = origin;
            Vector3 v = direction * launchSpeed;
            for (int i = 0; i < ArcMaxSteps; i++)
            {
                Vector3 next = p + v * ArcStep;
                v += Physics.gravity * ArcStep;
                if (Cast(p, next, out RaycastHit hit))
                {
                    arcPoints.Add(hit.point);
                    point = hit.point;
                    valid = ValidTarget(hit, head);
                    if (!valid && Nudge(hit, head, out Vector3 floor))
                    {
                        // El arco baja hasta el piso elegido.
                        arcPoints.Add(floor);
                        point = floor;
                        valid = true;
                    }

                    return true;
                }

                arcPoints.Add(next);
                p = next;
                if (p.y < transform.position.y - 2f)
                {
                    break;
                }
            }

            return false;
        }

        // Lo primero que toca el arco, sin contar el cuerpo del jugador, sus
        // manos ni lo que sostiene.
        private bool Cast(Vector3 from, Vector3 to, out RaycastHit best)
        {
            best = default;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-5f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.RaycastAll(from, delta / length, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance >= nearest || IsPlayer(hit.collider))
                {
                    continue;
                }

                nearest = hit.distance;
                best = hit;
                found = true;
            }

            return found;
        }

        private bool IsPlayer(Collider col)
        {
            if (col.transform.IsChildOf(transform))
            {
                return true;
            }

            Rigidbody rb = col.attachedRigidbody;
            return rb != null && (rb.transform.IsChildOf(transform) || SimpleGrab.IsHeldObject(rb)
                || SimpleGrab.IsHandBody(rb));
        }

        // Piso libre cerca de donde cayo el arco, empezando por el lado del
        // jugador: al pie del meson si se apunto a su cubierta, o un poco
        // separado de un mueble si se apunto al piso pegado a el. No si el
        // arco pego en algo lejos del piso (un estante alto, el techo) ni en
        // la paciente.
        private bool Nudge(RaycastHit hit, Transform head, out Vector3 floor)
        {
            floor = Vector3.zero;
            if (head == null || hit.point.y > transform.position.y + 1.3f
                || hit.collider.GetComponentInParent<PatientBody>() != null
                || (PatientPhysics.Current != null && hit.collider.transform.IsChildOf(PatientPhysics.Current.transform)))
            {
                return false;
            }

            Vector3 basePoint = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            Vector3 toPlayer = head.position - basePoint;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            toPlayer.Normalize();
            for (float r = 0.1f; r <= nudgeRadius + 1e-3f; r += 0.15f)
            {
                foreach (float a in NudgeAngles)
                {
                    Vector3 candidate = basePoint + Quaternion.Euler(0f, a, 0f) * toPlayer * r;
                    if (LandingAt(candidate, head, out Vector3 landing) && LineOfSight(hit.point, landing))
                    {
                        floor = landing;
                        return true;
                    }
                }
            }

            return false;
        }

        private static readonly float[] NudgeAngles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f };

        // Sin una pared entre donde cayo el arco y el piso elegido (para no
        // aparecer del otro lado de un tabique).
        private bool LineOfSight(Vector3 from, Vector3 floor)
        {
            Vector3 a = from + Vector3.up * 0.05f;
            Vector3 b = floor + Vector3.up * 0.3f;
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 1e-3f)
            {
                return true;
            }

            foreach (RaycastHit h in Physics.RaycastAll(a, delta / length, length, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsPlayer(h.collider) && Mathf.Abs(h.normal.y) < 0.5f
                    && (h.rigidbody == null || h.rigidbody.isKinematic))
                {
                    return false;
                }
            }

            return true;
        }

        private bool LandingAt(Vector3 floorPoint, Transform head, out Vector3 landing)
        {
            landing = Vector3.zero;
            Vector3 from = floorPoint + Vector3.up * 1.5f;
            if (!Cast(from, floorPoint + Vector3.down * 0.5f, out RaycastHit hit) || !ValidTarget(hit, head))
            {
                return false;
            }

            landing = hit.point;
            return true;
        }

        // True si se puede saltar a ese punto del piso (lo usa el tutorial
        // para poner la meta en un lugar alcanzable).
        public bool CanLandAt(Vector3 floorPoint)
        {
            Vector3 from = floorPoint + Vector3.up * 1.5f;
            if (!Cast(from, floorPoint + Vector3.down * 0.5f, out RaycastHit hit))
            {
                return false;
            }

            return ValidTarget(hit, VRPanelKit.Head());
        }

        // Piso (casi horizontal, a la altura de donde esta parado el
        // jugador), dentro de lo caminable, a una distancia razonable y con
        // lugar para el cuerpo.
        private bool ValidTarget(RaycastHit hit, Transform head)
        {
            if (hit.normal.y < 0.75f || Mathf.Abs(hit.point.y - transform.position.y) > 0.2f)
            {
                return false;
            }

            if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)
            {
                // Un objeto suelto en el piso (la olla, la manta caida).
                return false;
            }

            Vector3 from = head != null ? head.position : transform.position;
            Vector3 flat = hit.point - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < minDistance || distance > maxDistance || !PlayArea.Allowed(hit.point))
            {
                return false;
            }

            return Clear(hit.point);
        }

        private readonly Collider[] overlap = new Collider[16];

        private bool Clear(Vector3 floor)
        {
            const float radius = 0.22f;
            Vector3 bottom = floor + Vector3.up * (0.25f + radius);
            Vector3 top = floor + Vector3.up * 1.6f;
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = overlap[i];
                if (col == null || IsPlayer(col))
                {
                    continue;
                }

                Rigidbody rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    // Objetos sueltos: se los empuja al caminar.
                    continue;
                }

                return false;
            }

            return true;
        }

        private void Draw(HandState state, Transform head)
        {
            bool ok = state.hasTarget && state.targetValid;
            Color color = ok ? validColor : invalidColor;

            LineRenderer arc = state.arc;
            arc.positionCount = arcPoints.Count;
            for (int i = 0; i < arcPoints.Count; i++)
            {
                arc.SetPosition(i, arcPoints[i]);
            }

            Color start = color;
            start.a *= 0.35f;
            arc.startColor = start;
            arc.endColor = color;
            arc.enabled = true;
            // Los puntos avanzan hacia el destino.
            dotMaterial.mainTextureOffset = new Vector2(-Time.unscaledTime * 2.5f, 0f);

            DrawHint(state, head, ok);

            if (!state.hasTarget)
            {
                state.ring.enabled = false;
                state.arrow.enabled = false;
                state.cross.enabled = false;
                state.fill.enabled = false;
                return;
            }

            Vector3 c = state.target + Vector3.up * 0.015f;
            const int segments = 40;
            LineRenderer ring = state.ring;
            ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius);
            }

            ring.startColor = color;
            ring.endColor = color;
            ring.enabled = true;

            if (ok)
            {
                state.cross.enabled = false;

                // Relleno: tenue al apuntar, se llena al juntar el pulgar.
                float k = state.progress;
                Transform fill = state.fill.transform;
                fill.position = c + Vector3.up * 0.002f;
                fill.rotation = Quaternion.identity;
                float radius = Mathf.Lerp(RingRadius * 0.35f, RingRadius, k);
                fill.localScale = new Vector3(radius, 1f, radius);
                Color fc = color;
                fc.a = Mathf.Lerp(0.18f, 0.55f, k);
                state.fill.material.color = fc;
                state.fill.enabled = true;

                // Flecha: hacia donde se quedara mirando (se llega mirando
                // hacia donde se mira ahora).
                Vector3 look = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
                if (look.sqrMagnitude < 1e-4f)
                {
                    look = Vector3.forward;
                }

                look.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, look);
                Vector3 tip = c + look * (RingRadius + 0.13f);
                Vector3 baseCenter = c + look * (RingRadius + 0.03f);
                LineRenderer arrow = state.arrow;
                arrow.positionCount = 3;
                arrow.SetPosition(0, baseCenter - side * 0.09f);
                arrow.SetPosition(1, tip);
                arrow.SetPosition(2, baseCenter + side * 0.09f);
                arrow.startColor = color;
                arrow.endColor = color;
                arrow.enabled = true;
            }
            else
            {
                state.fill.enabled = false;
                state.arrow.enabled = false;
                // X: ahi no se puede.
                const float x = 0.12f;
                LineRenderer cross = state.cross;
                cross.positionCount = 5;
                cross.SetPosition(0, c + new Vector3(-x, 0f, -x));
                cross.SetPosition(1, c + new Vector3(x, 0f, x));
                cross.SetPosition(2, c);
                cross.SetPosition(3, c + new Vector3(-x, 0f, x));
                cross.SetPosition(4, c + new Vector3(x, 0f, -x));
                cross.startColor = color;
                cross.endColor = color;
                cross.enabled = true;
            }
        }

        // Texto junto a la mano durante los primeros saltos (o en el
        // tutorial): que hacer ahora.
        private void DrawHint(HandState state, Transform head, bool ok)
        {
            TextMeshPro hint = state.hint;
            bool show = head != null && (ForceHints
                || (Count < hintTeleports && (BurnGuide.Instance == null || BurnGuide.Instance.Guided)));
            if (!show)
            {
                hint.gameObject.SetActive(false);
                return;
            }

            hint.text = !ok ? "Apunta a un lugar libre del piso"
                : state.controller ? "Suelta el joystick para ir"
                : "Junta el pulgar al índice para ir";
            hint.color = ok ? new Color(0.85f, 0.95f, 1f) : new Color(0.85f, 0.85f, 0.88f);
            Vector3 p = state.hintAnchor + Vector3.up * 0.07f;
            hint.transform.SetPositionAndRotation(p, Quaternion.LookRotation(p - head.position, Vector3.up));
            hint.gameObject.SetActive(true);
        }

        private static void Show(HandState state, bool visible)
        {
            state.arc.enabled = visible;
            state.ring.enabled = visible;
            state.arrow.enabled = visible;
            state.cross.enabled = visible;
            state.fill.enabled = visible;
            if (state.hint.gameObject.activeSelf != visible)
            {
                state.hint.gameObject.SetActive(visible);
            }
        }

        // ---------------- meta del tutorial ----------------

        // Circulo en el piso con un haz de luz, visible desde lejos.
        public void ShowGoal(Vector3 floorPoint)
        {
            if (goal == null)
            {
                GameObject go = new GameObject("MetaTeleport");
                go.AddComponent<NoAutoCollider>();
                goal = go.transform;
                goalRing = CreateLine("AnilloMeta", true, 0.03f, lineMaterial);
                goalRing.transform.SetParent(goal, false);
                goalRing.useWorldSpace = false;
                goalBeam = CreateLine("HazMeta", false, 0.12f, lineMaterial);
                goalBeam.transform.SetParent(goal, false);
                goalBeam.useWorldSpace = false;
                goalBeam.positionCount = 2;
                goalBeam.SetPosition(0, Vector3.zero);
                goalBeam.SetPosition(1, Vector3.up * 1.6f);
                goalBeam.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f));
                goalRing.enabled = true;
                goalBeam.enabled = true;
            }

            goal.position = floorPoint + Vector3.up * 0.01f;
            goal.gameObject.SetActive(true);
        }

        public void HideGoal()
        {
            if (goal != null)
            {
                Destroy(goal.gameObject);
                goal = null;
            }
        }

        private void UpdateGoal()
        {
            if (goal == null)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            const int segments = 40;
            float radius = Mathf.Lerp(0.3f, 0.36f, pulse);
            goalRing.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                goalRing.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
            }

            Color ring = new Color(0.35f, 0.9f, 0.6f, Mathf.Lerp(0.6f, 1f, pulse));
            goalRing.startColor = ring;
            goalRing.endColor = ring;
            goalBeam.startColor = new Color(0.35f, 0.9f, 0.6f, Mathf.Lerp(0.18f, 0.32f, pulse));
            goalBeam.endColor = new Color(0.35f, 0.9f, 0.6f, 0f);
        }
    }
}
