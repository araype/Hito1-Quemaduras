using UnityEngine;

namespace SojaExiles
{
    // Material para cubrir la quemadura despues de enfriarla: film
    // transparente de cocina o una gasa esteril. Como en la vida real, se
    // APOYA sobre la quemadura, sin envolver la mano (antes bastaba soltarlo
    // cerca y la mano entera quedaba envuelta al instante):
    //   - Gasa: se acerca a la quemadura y se mantiene encima 1 s. Mientras
    //     tanto se ve donde quedara (una silueta translucida) y una indicacion
    //     con un circulo de progreso (HoldHint).
    //   - Film: con el rollo en una mano, se aprieta junto a el con la otra y
    //     se tira: una lamina se estira entre las manos y, pasados unos 18 cm,
    //     se corta. Ese trozo se apoya igual que la gasa. Si se dan vueltas
    //     alrededor de la mano (envolverla), el narrador corrige: la mano se va
    //     a hinchar (BurnEvent.FilmWrapped).
    // Cubre solo la quemadura y un poco alrededor (HandSkinMesh.CopyWhere sobre
    // la mascara de BurnSkinPattern), no la mano entera. Al acercarlo, ella
    // ofrece la mano (PatientBurnArm.ShowHand). Si se intenta antes de terminar
    // de enfriar, no se pone: es un error y la paciente se queja. Al ponerlo
    // pasa a Covered. El rollo de film queda; la gasa se usa.
    [RequireComponent(typeof(Rigidbody))]
    public class CoverItem : MonoBehaviour
    {
        public enum Kind
        {
            Film,
            Gasa
        }

        public Kind kind;
        public BurnSequenceManager burnSequence;
        [Tooltip("Distancia al centro de la quemadura a la que se apoya (m).")]
        public float placeDistance = 0.1f;
        [Tooltip("Segundos que hay que mantenerlo apoyado.")]
        public float holdSeconds = 1f;
        [Tooltip("Distancia a la que ella ofrece la mano y aparece la indicacion (m).")]
        public float offerDistance = 0.4f;

        [Header("Film")]
        [Tooltip("Distancia de la otra mano al rollo para tirar del borde (m).")]
        public float pullZone = 0.12f;
        public float sheetWidth = 0.14f;
        [Tooltip("Largo de la lamina al cortarse (m).")]
        public float cutLength = 0.18f;
        [Tooltip("Grados de vuelta alrededor de la mano que cuentan como envolverla.")]
        public float wrapDegrees = 300f;

        // El material del film es casi invisible (16 %): la lamina y el trozo
        // puesto se ven un poco mas, con un tono celeste suave.
        private static readonly Color FilmTint = new Color(0.85f, 0.93f, 1f, 0.38f);

        private Rigidbody body;
        private Grabbable grabbable;
        private PatientBody patient;
        private PatientBurnArm arm;
        private BurnSkinOverlay burnOverlay;
        private SkinnedMeshRenderer layer;
        private Color layerColor = Color.white;
        private HoldHint hint;
        private float progress;
        private float nextEarlyReport;
        private bool placed;

        // Lamina de film entre las manos.
        private int sheetHand = -1;
        private bool sheetCut;
        private Transform sheet;
        private Mesh sheetMesh;
        private Vector3 sheetAlong = Vector3.forward;
        private float wrapAngle;
        private Vector3 lastWrapDir;
        private bool wrapTracking;

        private void Start()
        {
            body = GetComponent<Rigidbody>();
            grabbable = GetComponent<Grabbable>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            hint = new HoldHint("IndicacionCubrir", 0.05f);
            if (kind == Kind.Film)
            {
                SimpleGrab.AddGrabBlocker(ClaimsPull);
            }
        }

        private void OnDestroy()
        {
            SimpleGrab.RemoveGrabBlocker(ClaimsPull);
            if (hint != null)
            {
                hint.Destroy();
            }

            DestroySheet();
        }

        private void Update()
        {
            if (placed || burnSequence == null)
            {
                return;
            }

            if (patient == null)
            {
                patient = FindObjectOfType<PatientBody>();
                arm = patient != null ? patient.GetComponent<PatientBurnArm>() : null;
                burnOverlay = patient != null ? patient.GetComponent<BurnSkinOverlay>() : null;
            }

            Transform hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            if (hand == null)
            {
                return;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            if (kind == Kind.Film)
            {
                UpdateFilm(hand);
            }

            // Que se apoya sobre la quemadura: la gasa, o el trozo de film ya cortado.
            bool carrying = kind == Kind.Gasa ? grabbable != null && grabbable.IsHeld : sheetCut;
            Vector3 point = kind == Kind.Gasa ? body.worldCenterOfMass : (sheet != null ? sheet.position : hand.position);
            Vector3 burn = BurnCenter(hand);
            float distance = Vector3.Distance(point, burn);
            bool early = state == BurnSequenceManager.BurnState.Burned || state == BurnSequenceManager.BurnState.Cooling;
            bool ready = state == BurnSequenceManager.BurnState.Cooled;

            if (carrying && distance < placeDistance && early)
            {
                // Todavia no: primero hay que terminar de enfriar.
                if (Time.time >= nextEarlyReport)
                {
                    nextEarlyReport = Time.time + 8f;
                    burnSequence.Report(BurnSequenceManager.BurnEvent.CoveredTooEarly, kind.ToString(),
                        BurnSequenceManager.Severity.Error);
                }

                progress = 0f;
                HideGhost();
                hint.Hide();
                return;
            }

            if (carrying && ready && distance < offerDistance)
            {
                if (arm != null && arm.Current != PatientBurnArm.Mode.InWater)
                {
                    arm.ShowHand(1.5f);
                }

                bool near = distance < placeDistance;
                progress = near ? progress + Time.deltaTime / Mathf.Max(0.1f, holdSeconds)
                    : Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2f);
                hint.Show(burn, "Mantén encima\nde la quemadura", progress);
                ShowGhost(progress, near);
                if (progress >= 1f)
                {
                    Place(hand);
                }

                return;
            }

            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * 2f);
            HideGhost();

            // Con el rollo en la mano: como sacar un trozo.
            if (kind == Kind.Film && ready && sheetHand < 0 && RollHand() >= 0)
            {
                hint.Show(body.worldCenterOfMass, "Tira del borde\ncon la otra mano", 0f);
            }
            else
            {
                hint.Hide();
            }
        }

        // Centro de la quemadura (el de BurnSkinOverlay, en el hueso de la mano).
        private Vector3 BurnCenter(Transform hand)
        {
            return burnOverlay != null ? hand.position + hand.rotation * burnOverlay.burnCenter : hand.position;
        }

        // ---------------- poner ----------------

        private void Place(Transform hand)
        {
            placed = true;
            EnsureLayer();
            if (layer != null)
            {
                layer.enabled = true;
                layer.SetPropertyBlock(null);
                if (kind == Kind.Film)
                {
                    IndicatorUtil.SetColor(layer, FilmTint);
                }
            }

            hint.Hide();
            Hito1FX.PlayAt(kind == Kind.Film ? "film_estirar" : "tela_roce_2", hand.position, 0.7f);
            burnSequence.Report(BurnSequenceManager.BurnEvent.Covered, kind.ToString());
            burnSequence.ChangeState(BurnSequenceManager.BurnState.Covered);
            if (arm != null)
            {
                arm.EndShow();
            }

            if (kind == Kind.Gasa)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroySheet();
                SimpleGrab.RemoveGrabBlocker(ClaimsPull);
            }
        }

        // La capa solo sobre la quemadura (y un poco alrededor). Sin la capa de
        // la quemadura, como antes: sobre toda la mano.
        private void EnsureLayer()
        {
            if (layer != null || patient == null)
            {
                return;
            }

            string objectName = kind == Kind.Film ? "FilmMano" : "GasaMano";
            string material = kind == Kind.Film ? "Film_Mano" : "Gasa_Mano";
            SkinnedMeshRenderer source = burnOverlay != null ? burnOverlay.Overlay : null;
            layer = HandSkinMesh.CopyWhere(source, c => c.a > 0.3f || c.r > 0.1f,
                kind == Kind.Film ? 0.0025f : 0.002f, objectName, material);
            if (layer == null)
            {
                layer = HandSkinMesh.Build(patient, true, 0.45f, kind == Kind.Film ? 0.0035f : 0.003f, "Body",
                    objectName, material);
            }

            if (layer != null)
            {
                layerColor = layer.sharedMaterial != null && layer.sharedMaterial.HasProperty("_Color")
                    ? layer.sharedMaterial.color : Color.white;
                layer.enabled = false;
            }
        }

        // Silueta de donde quedara, mas visible mientras se mantiene encima.
        private void ShowGhost(float amount, bool near)
        {
            EnsureLayer();
            if (layer == null)
            {
                return;
            }

            layer.enabled = true;
            float pulse = near ? 1f : 0.7f + 0.3f * Mathf.Sin(Time.time * 4f);
            Color c = kind == Kind.Film ? FilmTint : layerColor;
            c.a = Mathf.Max(0.12f, c.a) * Mathf.Lerp(0.35f, 0.9f, amount) * pulse;
            IndicatorUtil.SetColor(layer, c);
        }

        private void HideGhost()
        {
            if (layer != null && layer.enabled)
            {
                layer.enabled = false;
            }
        }

        // ---------------- film ----------------

        // Mano que sostiene el rollo: 0 izquierda, 1 derecha, -1 ninguna.
        private int RollHand()
        {
            if (SimpleGrab.IsHeldBy(body, true))
            {
                return 0;
            }

            return SimpleGrab.IsHeldBy(body, false) ? 1 : -1;
        }

        // Apretar junto al rollo con la otra mano tira del borde (no se pasa el
        // rollo de una mano a la otra).
        private bool ClaimsPull(Vector3 point)
        {
            return !placed && sheetHand < 0 && RollHand() >= 0
                && Vector3.Distance(point, body.worldCenterOfMass) < pullZone;
        }

        private void UpdateFilm(Transform hand)
        {
            int roll = RollHand();
            if (sheetHand < 0)
            {
                if (roll < 0)
                {
                    return;
                }

                int other = 1 - roll;
                if (SimpleGrab.TryGetHandInput(other == 0, out Vector3 p, out bool pressed, out bool holding)
                    && pressed && !holding && Vector3.Distance(p, body.worldCenterOfMass) < pullZone)
                {
                    StartSheet(other);
                }

                return;
            }

            if (!SimpleGrab.TryGetHandInput(sheetHand == 0, out Vector3 at, out bool stillPressed, out bool _)
                || !stillPressed)
            {
                DestroySheet();
                return;
            }

            PullSheet(at, hand);
        }

        private void StartSheet(int handIndex)
        {
            sheetHand = handIndex;
            sheetCut = false;
            wrapTracking = false;
            wrapAngle = 0f;
            GameObject go = new GameObject("LaminaFilm");
            go.AddComponent<NoAutoCollider>();
            sheet = go.transform;
            sheetMesh = new Mesh { name = "LaminaFilm" };
            go.AddComponent<MeshFilter>().sharedMesh = sheetMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Hito1FX.Mat("Film_Mano");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            IndicatorUtil.SetColor(renderer, FilmTint);
            Hito1FX.PlayAt("film_estirar", body.worldCenterOfMass, 0.35f);
        }

        // Antes de cortarse, la lamina va del rollo a la mano que tira; despues,
        // es un trozo plano que la mano lleva por el medio.
        private void PullSheet(Vector3 at, Transform hand)
        {
            Vector3 edge = body.worldCenterOfMass;
            if (!sheetCut)
            {
                Vector3 along = at - edge;
                if (along.magnitude >= cutLength)
                {
                    sheetCut = true;
                    sheetAlong = PropKit.Flat(along).sqrMagnitude > 1e-4f ? PropKit.Flat(along).normalized : Vector3.forward;
                    Hito1FX.PlayAt("film_estirar", at, 0.6f);
                }
                else
                {
                    SetSheet((edge + at) * 0.5f, along, along.magnitude);
                    return;
                }
            }

            Vector3 center = at + Vector3.down * 0.02f;
            SetSheet(center, sheetAlong, cutLength);
            TrackWrap(at, hand);
        }

        private void SetSheet(Vector3 center, Vector3 along, float length)
        {
            if (sheet == null)
            {
                return;
            }

            sheet.position = center;
            sheet.rotation = Quaternion.identity;
            Vector3 dir = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, dir);
            if (across.sqrMagnitude < 1e-4f)
            {
                across = Vector3.right;
            }

            across = across.normalized * (sheetWidth * 0.5f);
            Vector3 half = dir * (length * 0.5f);
            // Dos caras con sus propios vertices y normales (compartidas, las
            // normales se anulaban y la lamina se veia negra).
            Vector3 a = -half - across, b = -half + across, c = half + across, d = half - across;
            Vector3 n = Vector3.Cross(across, dir).normalized;
            sheetMesh.vertices = new[] { a, b, c, d, a, b, c, d };
            sheetMesh.normals = new[] { n, n, n, n, -n, -n, -n, -n };
            sheetMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
            sheetMesh.RecalculateBounds();
        }

        // Vueltas de la mano que lleva el film alrededor de la mano de ella.
        private void TrackWrap(Vector3 at, Transform hand)
        {
            Transform middle = PropKit.FindChild(hand, "Middle1");
            Vector3 axis = middle != null ? (middle.position - hand.position).normalized : hand.up;
            Vector3 dir = Vector3.ProjectOnPlane(at - hand.position, axis);
            if (dir.magnitude > 0.2f || dir.sqrMagnitude < 1e-6f)
            {
                wrapTracking = false;
                wrapAngle = 0f;
                return;
            }

            if (wrapTracking)
            {
                wrapAngle += Vector3.SignedAngle(lastWrapDir, dir, axis);
                if (Mathf.Abs(wrapAngle) >= wrapDegrees)
                {
                    wrapAngle = 0f;
                    burnSequence.Report(BurnSequenceManager.BurnEvent.FilmWrapped);
                }
            }

            lastWrapDir = dir;
            wrapTracking = true;
        }

        private void DestroySheet()
        {
            if (sheet != null)
            {
                Destroy(sheet.gameObject);
            }

            if (sheetMesh != null)
            {
                Destroy(sheetMesh);
            }

            sheet = null;
            sheetMesh = null;
            sheetHand = -1;
            sheetCut = false;
            wrapTracking = false;
        }
    }
}
