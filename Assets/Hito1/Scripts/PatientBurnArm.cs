using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Que hace la paciente con su mano quemada cuando el jugador no se la
    // sostiene (antes la mano quedaba flotando donde se la soltaba, o
    // estirada a la altura de la cara):
    //   - antes del accidente revuelve la olla con una cuchara de palo y mira
    //     lo que cocina;
    //   - al quemarse suelta la cuchara y protege la mano contra el pecho,
    //     sosteniendola con la otra mano;
    //   - si el jugador la suelta cerca del chorro (y el agua corre), ella
    //     misma la pone bajo el agua y la deja ahi;
    //   - si la suelta en otro lado, la deja un momento y vuelve a
    //     protegerla;
    //   - si se cierra el agua, o cuando ya termino de enfriar, la saca del
    //     agua y la vuelve a proteger (asi es facil cubrirla);
    //   - tras el sobresalto, le muestra al jugador la mano quemada unos
    //     segundos (dorso hacia el), para que vea donde y como se quemo;
    //   - aparta la mano de lo que no quiere que le pongan (Avoid);
    //   - puede recibir un objeto que le alcanza el jugador (ReceiveObject).
    // Mueve el objetivo de la mano (kinematico) que usa PatientReach; si el
    // objetivo esta lejos, PatientReach la hace caminar.
    // Va en la paciente (el objeto con PatientBody y PatientReach).
    [RequireComponent(typeof(PatientBody))]
    [DefaultExecutionOrder(150)]
    public class PatientBurnArm : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        public bool rightArm = true;

        [Header("Cocinar (antes del accidente)")]
        public bool cookBeforeAccident = true;
        public float stirRadius = 0.03f;
        public float stirSeconds = 2.4f;
        [Tooltip("Altura de la muneca sobre el borde de la olla al revolver (m).")]
        public float stirHeight = 0.09f;
        public bool spoon = true;

        [Header("Proteger la mano")]
        [Tooltip("Muneca respecto del pecho: adelante, al costado de la mano, abajo (m).")]
        public Vector3 guardOffset = new Vector3(0.15f, 0.05f, 0.2f);
        public bool cradleWithOtherHand = true;
        [Tooltip("Al quemarse se aparta de la cocina: cuanto retrocede y cuanto se corre hacia el lavadero (m).")]
        public float stepAwayDistance = 0.45f;
        public float stepAwaySide = 0.35f;
        public float stepAwayDelay = 0.6f;

        [Header("Mostrar la mano quemada")]
        public bool showHandAfterBurn = true;
        [Tooltip("Segundos despues de quemarse en que la muestra, y cuanto la deja a la vista.")]
        public float showAfterBurn = 2.6f;
        public float showSeconds = 3.2f;
        [Tooltip("Cuanto adelanta la mano desde el pecho hacia el jugador (m); menos si esta muy cerca.")]
        public float showDistance = 0.33f;
        [Tooltip("Segundos que deja la mano ofrecida para que le saquen el anillo.")]
        public float ringOfferSeconds = 8f;
        [Tooltip("Segundos que deja la mano donde la soltaron antes de volver a protegerla.")]
        public float holdAfterRelease = 1.2f;

        [Header("Agua")]
        [Tooltip("Si la sueltan a esta distancia horizontal del chorro, ella pone la mano bajo el agua.")]
        public float assistRadius = 0.42f;
        [Tooltip("Altura de la palma bajo la boca del grifo (m).")]
        public float palmBelowNozzle = 0.11f;
        [Tooltip("Segundos para girar la mano (dorso arriba) al ponerla bajo el chorro.")]
        public float palmTurnSeconds = 0.4f;
        [Tooltip("Segundos despues de enfriar hasta que saca la mano del agua.")]
        public float withdrawAfterCooled = 3.5f;

        [Header("Movimiento")]
        public float smoothTime = 0.22f;
        public float maxHandSpeed = 1.6f;

        public enum Mode
        {
            Cooking,
            Guard,
            Placed,
            InWater,
            Show
        }

        public Mode Current { get; private set; } = Mode.Cooking;

        private PatientBody body;
        private PatientReach reach;
        private Rigidbody target;
        private PotFX pot;
        private WaterStreamFX stream;
        private Transform cradle;
        private Transform spoonRoot;
        private Vector3 spoonTip;
        private Vector3 velocity;
        private Vector3 placedAt;
        private float placedTime;
        private bool wasHeld;
        private float waterLostTime = -1f;
        private float cooledTime = -1f;
        private Vector3 avoidOffset;
        private float avoidUntil;
        private float hesitateLift;
        private float lookAtPlayerUntil;
        private Transform viewer;
        private bool receiving;
        private bool facingForReceive;
        private float attendUntil;
        // Hacia donde mira al empezar (la cocina): vuelve ahi tras atender.
        private Vector3 homeForward = Vector3.forward;
        private float palmDown;
        private float showUntil;
        private float heldNearStream;

        [Tooltip("Si la llevan hasta el chorro y la sostienen ahi este tiempo, ella pone la mano bajo el agua.")]
        public float placeInWaterAfter = 0.8f;

        [Tooltip("La otra mano sostiene la quemada: cuanto por debajo de la muneca quemada (m).")]
        public float cradleBelow = 0.105f;
        [Tooltip("La otra mano sostiene la quemada: cuanto desde la muneca hacia el codo (m).")]
        public float cradleTowardElbow = 0.04f;

        private void Awake()
        {
            body = GetComponent<PatientBody>();
            reach = GetComponent<PatientReach>();
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            target = reach != null ? reach.target : null;
            pot = FindObjectOfType<PotFX>();
            stream = FindObjectOfType<WaterStreamFX>();
            viewer = VRPanelKit.Head();
            homeForward = PatientBody.Flat(transform.forward).normalized;
            if (target == null || !body.IsReady)
            {
                enabled = false;
                return;
            }

            GameObject helper = new GameObject("ApoyoManoQuemada");
            helper.AddComponent<NoAutoCollider>();
            cradle = helper.transform;

            if (burnSequence != null)
            {
                burnSequence.OnStateChanged += HandleState;
            }

            SimpleGrab.OnPulledAway += HandlePulledAway;

            bool idle = burnSequence == null || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle;
            Current = idle && cookBeforeAccident && pot != null ? Mode.Cooking : Mode.Guard;
            if (Current == Mode.Cooking && spoon)
            {
                BuildSpoon();
            }

            // Empieza ya en su lugar, sin cruzar la cocina.
            Vector3 start = Goal();
            target.position = start;
            target.transform.position = start;
            Application.onBeforeRender += UpdateSpoon;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= UpdateSpoon;
            SimpleGrab.OnPulledAway -= HandlePulledAway;
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged -= HandleState;
            }

            if (cradle != null)
            {
                Destroy(cradle.gameObject);
            }
        }

        // ---------------- API ----------------

        // Aparta la mano de algo que le acercan. Devuelve false si el jugador
        // se la tiene agarrada (entonces solo se sobresalta).
        public bool Avoid(Vector3 from)
        {
            if (target == null || SimpleGrab.IsHeldObject(target))
            {
                return false;
            }

            Vector3 away = Vector3.ProjectOnPlane(target.position - from, Vector3.up);
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -body.TorsoForward;
            }

            avoidOffset = away.normalized * 0.14f + Vector3.up * 0.04f;
            avoidUntil = Time.time + 1.4f;

            // Si la tenia bajo el agua, igual la aparta (y deja de enfriar).
            if (Current == Mode.InWater)
            {
                SetPlaced(target.position);
            }

            return true;
        }

        // Duda bajo el agua (quiere sacar la mano por el frio): la levanta un
        // poco y tiembla, sin salir del chorro. 0 la deja quieta otra vez.
        public void Hesitate(float lift)
        {
            hesitateLift = Mathf.Max(0f, lift);
        }

        // Saca la mano del agua y la vuelve a proteger contra el pecho (lo
        // decide ella; PatientDialog). Si el jugador se la sostenia, se suelta.
        public void Withdraw()
        {
            hesitateLift = 0f;
            if (target != null && SimpleGrab.IsHeldObject(target))
            {
                SimpleGrab.ForceRelease(target);
                wasHeld = false;
            }

            if (Current == Mode.InWater || Current == Mode.Placed)
            {
                Current = Mode.Guard;
            }
        }

        // Mira al jugador un rato (por ejemplo, al pedirle algo).
        public void LookAtPlayer(float seconds)
        {
            lookAtPlayerUntil = Time.time + seconds;
        }

        // Recibe un objeto con la mano libre: se voltea hacia el jugador, lo
        // toma, vuelve a mirar la cocina y lo deja sobre el meson ('putDown'
        // se pide recien entonces, ya mirando la cocina).
        public void ReceiveObject(Rigidbody rb, System.Func<Vector3> putDown)
        {
            if (!receiving && rb != null)
            {
                StartCoroutine(Receive(rb, putDown));
            }
        }

        public void ReceiveObject(Rigidbody rb, Vector3 putDown)
        {
            ReceiveObject(rb, () => putDown);
        }

        // El jugador se acerca con algo para darle: deja de revolver y se
        // voltea hacia el (llamar cada cuadro mientras dure).
        public void AttendPlayer(float seconds = 0.5f)
        {
            attendUntil = Mathf.Max(attendUntil, Time.time + seconds);
        }

        public bool IsReceiving
        {
            get { return receiving; }
        }

        // ---------------- estados ----------------

        private void HandleState(BurnSequenceManager.BurnState state)
        {
            switch (state)
            {
                case BurnSequenceManager.BurnState.Burned:
                    if (Current == Mode.Cooking)
                    {
                        DropSpoon();
                        Current = Mode.Guard;
                        // Retira la mano de golpe.
                        velocity = (Goal() - target.position).normalized * 1.2f;
                        StartCoroutine(StepAwayFromStove());
                    }

                    cooledTime = -1f;
                    break;

                case BurnSequenceManager.BurnState.Cooled:
                    cooledTime = Time.time;
                    break;
            }
        }

        // Tras el sobresalto se aparta de la cocina, corriendose hacia el
        // lavadero. Antes se quedaba frente a la hornilla: su cuerpo tapaba la
        // perilla y la mano protegida quedaba sobre el fuego.
        private IEnumerator StepAwayFromStove()
        {
            yield return new WaitForSeconds(stepAwayDelay);
            if (reach == null || pot == null || SimpleGrab.IsHeldObject(target))
            {
                yield break;
            }

            Vector3 her = transform.position;
            Vector3 away = PatientBody.Flat(her - pot.BottomCenter);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -PatientBody.Flat(transform.forward).normalized;
            Vector3 goal = her + away * stepAwayDistance;
            if (stream != null)
            {
                // Solo la parte de costado del camino al lavadero.
                Vector3 toSink = PatientBody.Flat(stream.NozzlePosition - her);
                Vector3 side = toSink - away * Vector3.Dot(toSink, away);
                goal += Vector3.ClampMagnitude(side, stepAwaySide);
            }

            reach.WalkTo(goal, 0.6f);

            if (showHandAfterBurn)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, showAfterBurn - stepAwayDelay));
                // Con el anillo puesto deja la mano a la vista mas tiempo y pide
                // que se lo saquen: es el momento facil (antes se pedia con la
                // mano bajo el chorro y costaba mucho). Las frases esperan a que
                // haya silencio; la mano se ofrece desde ya.
                BurnPatientReactions reactions = GetComponent<BurnPatientReactions>();
                PatientRing ring = GetComponent<PatientRing>();
                bool askRing = ring != null && !ring.Removed && !ring.Stuck;
                if (ShowHand(askRing ? showSeconds + ringOfferSeconds : showSeconds) && reactions != null)
                {
                    yield return SayWhenQuiet(reactions, "voz_mira_mano", 3f);
                    if (askRing && !ring.Removed && !ring.Stuck && Current == Mode.Show)
                    {
                        yield return SayWhenQuiet(reactions, "voz_anillo_pide", 3f);
                    }
                }
            }
        }

        // Dice la frase cuando ni ella ni el narrador estan hablando (justo
        // despues del accidente suenan el grito y la narracion), esperando a
        // lo sumo 'maxWait' segundos.
        private IEnumerator SayWhenQuiet(BurnPatientReactions reactions, string line, float maxWait)
        {
            float limit = Time.time + maxWait;
            while (Time.time < limit && (reactions.IsSpeaking
                || (BurnGuide.Instance != null && BurnGuide.Instance.Speaking)))
            {
                yield return null;
            }

            reactions.Say(line, true);
            AudioClip clip = Hito1FX.Clip(line);
            yield return new WaitForSeconds(clip != null ? clip.length : 0.5f);
        }

        // Deja de mostrar la mano en un momento (al sacarle el anillo).
        public void EndShow()
        {
            if (Current == Mode.Show)
            {
                showUntil = Mathf.Min(showUntil, Time.time + 0.6f);
            }
        }

        // Le muestra la mano quemada al jugador unos segundos (no si el
        // jugador la tiene tomada o si la mano esta bajo el agua: ahi el
        // reloj se pausaria). Devuelve false si no la muestra.
        public bool ShowHand(float seconds)
        {
            if (target == null || SimpleGrab.IsHeldObject(target) || Current == Mode.InWater
                || Current == Mode.Cooking)
            {
                return false;
            }

            Current = Mode.Show;
            showUntil = Time.time + seconds;
            return true;
        }

        // La mano hacia el jugador, a la altura del pecho y con el dorso
        // arriba (UpdatePalm), sin estirar del todo el brazo. Si el jugador
        // esta muy cerca, la adelanta menos para no meterla en el.
        private Vector3 ShowGoal()
        {
            Vector3 chest = body.Chest.position;
            Vector3 toViewer = viewer != null ? PatientBody.Flat(viewer.position - chest) : Vector3.zero;
            float distance = toViewer.magnitude;
            Vector3 dir = distance > 0.05f ? toViewer / distance : PatientBody.Flat(body.TorsoForward).normalized;
            float reach = Mathf.Min(showDistance, Mathf.Max(0.12f, distance - 0.35f));
            Vector3 side = PatientBody.Flat(transform.right).normalized * (rightArm ? 1f : -1f);
            return WithinStep(chest + dir * reach + side * 0.06f + Vector3.down * 0.1f);
        }

        // Deja un objetivo de la mano dentro de lo que alcanza sin dar un paso
        // (PatientReach camina si la mano pasa de stepAtFraction del brazo).
        // Al ofrecer la mano el objetivo se mueve con ella: si quedaba fuera,
        // daba un paso, la mano se adelantaba otra vez y seguia al jugador
        // aunque este retrocediera.
        private Vector3 WithinStep(Vector3 goal)
        {
            if (reach == null)
            {
                return goal;
            }

            Vector3 shoulder = body.ShoulderPosition(rightArm);
            Vector3 flat = PatientBody.Flat(goal - shoulder);
            float max = body.ArmLength(rightArm) * reach.stepAtFraction * 0.9f;
            return flat.magnitude > max ? goal - flat + flat.normalized * max : goal;
        }

        // El jugador tiro demasiado fuerte de su mano y se solto: le duele.
        private void HandlePulledAway(Rigidbody rb)
        {
            if (rb != target)
            {
                return;
            }

            BurnPatientReactions reactions = GetComponent<BurnPatientReactions>();
            if (reactions != null && Current != Mode.Cooking)
            {
                reactions.Say("voz_dolor_1", true);
            }
        }

        private void SetPlaced(Vector3 at)
        {
            Current = Mode.Placed;
            placedAt = at;
            placedTime = Time.time;
        }

        private bool WaterRunning
        {
            get
            {
                if (stream == null || stream.tap == null || !stream.tap.Flowing)
                {
                    return false;
                }

                // Con el agua caliente sola no la pone (le quema).
                return stream.tap.Heat < 0.9f;
            }
        }

        private bool NearStream(Vector3 wrist)
        {
            if (stream == null)
            {
                return false;
            }

            // Palma que corresponde a esa muneca (la mano puede ir un poco
            // atrasada respecto del objetivo).
            Transform hand = body.HandBone(rightArm);
            Vector3 palm = wrist + (stream.PalmOf(hand) - hand.position);
            Vector3 nozzle = stream.NozzlePosition;
            Vector3 flat = PatientBody.Flat(palm - nozzle);
            float below = nozzle.y - palm.y;
            return flat.magnitude < assistRadius && below > -0.45f && below < 0.5f;
        }

        // La palma (donde esta ahora) queda bajo el chorro, a menos de
        // 'radius' en horizontal.
        private bool PalmUnderStream(float radius)
        {
            if (stream == null)
            {
                return false;
            }

            Vector3 palm = stream.PalmOf(body.HandBone(rightArm));
            Vector3 nozzle = stream.NozzlePosition;
            return PatientBody.Flat(palm - nozzle).magnitude < radius && palm.y < nozzle.y + 0.02f
                && palm.y > nozzle.y - 0.45f;
        }

        // Muneca tal que la palma quede bajo el chorro con la mano plana y el
        // dorso (donde esta la quemadura) hacia arriba: los dedos apuntan
        // desde el hombro hacia el grifo (ver PatientBody.LayHandFlat).
        private Vector3 WaterGoal()
        {
            Transform hand = body.HandBone(rightArm);
            float palmDistance = Vector3.Distance(stream.PalmOf(hand), hand.position);
            Vector3 nozzle = stream.NozzlePosition;
            Vector3 along = PatientBody.Flat(nozzle - body.ShoulderPosition(rightArm));
            along = along.sqrMagnitude > 1e-4f ? along.normalized : PatientBody.Flat(body.TorsoForward).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Vector3 fingers = Quaternion.AngleAxis(body.palmDownTiltDegrees, side) * along;
            Vector3 wantedPalm = nozzle + Vector3.down * palmBelowNozzle;
            return wantedPalm - fingers * palmDistance;
        }

        private Vector3 GuardGoal()
        {
            Vector3 up = Vector3.up;
            Vector3 fwd = PatientBody.Flat(body.TorsoForward).normalized;
            Vector3 side = PatientBody.Flat(transform.right).normalized * (rightArm ? 1f : -1f);
            return body.Chest.position + fwd * guardOffset.x + side * guardOffset.y - up * guardOffset.z;
        }

        private Vector3 CookGoal()
        {
            Vector3 toHer = PatientBody.Flat(transform.position - pot.BottomCenter);
            toHer = toHer.sqrMagnitude > 1e-4f ? toHer.normalized : -transform.forward;
            float a = Time.time / Mathf.Max(0.5f, stirSeconds) * Mathf.PI * 2f;
            Vector3 circle = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * stirRadius;
            Vector3 center = pot.BottomCenter + Vector3.up * (pot.Height + stirHeight) + toHer * (pot.Radius * 0.35f);
            spoonTip = pot.BottomCenter + Vector3.up * (pot.Height * 0.45f)
                + Vector3.ClampMagnitude(circle * 2.2f, pot.Radius * 0.55f);
            return center + circle;
        }

        private Vector3 Goal()
        {
            Vector3 goal;
            switch (Current)
            {
                case Mode.Cooking:
                    goal = FacingPlayer || receiving ? AttendGoal() : CookGoal();
                    break;
                case Mode.Placed:
                    goal = placedAt;
                    break;
                case Mode.InWater:
                    goal = WaterGoal();
                    break;
                case Mode.Show:
                    goal = ShowGoal();
                    break;
                default:
                    goal = GuardGoal();
                    break;
            }

            if (Time.time < avoidUntil)
            {
                goal += avoidOffset * Mathf.Clamp01((avoidUntil - Time.time) / 0.5f);
            }

            if (hesitateLift > 0f && Current == Mode.InWater)
            {
                goal += Vector3.up * hesitateLift * (0.75f + 0.25f * Mathf.Sin(Time.time * 9f));
            }

            return goal;
        }

        // ---------------- cada cuadro ----------------

        private void FixedUpdate()
        {
            if (target == null)
            {
                return;
            }

            bool held = SimpleGrab.IsHeldObject(target);
            if (held)
            {
                wasHeld = true;
                velocity = Vector3.zero;
                if (Current != Mode.Cooking)
                {
                    Current = Mode.Guard;
                }

                // La llevaron hasta el chorro: ella misma pone la mano bajo el
                // agua (si no, queda donde la sostiene el jugador, a veces
                // por encima del lavadero).
                bool nearWater = Current != Mode.Cooking && WaterRunning && NearStream(target.position);
                heldNearStream = nearWater ? heldNearStream + Time.fixedDeltaTime : 0f;
                if (heldNearStream > placeInWaterAfter)
                {
                    heldNearStream = 0f;
                    SimpleGrab.ForceRelease(target);
                    wasHeld = false;
                    Current = Mode.InWater;
                    waterLostTime = -1f;
                }

                return;
            }

            heldNearStream = 0f;

            if (wasHeld)
            {
                // Lo acaban de soltar: si quedo cerca del chorro, ella pone la
                // mano bajo el agua; si no, la deja un momento ahi.
                wasHeld = false;
                if (WaterRunning && NearStream(target.position))
                {
                    Current = Mode.InWater;
                    waterLostTime = -1f;
                }
                else
                {
                    SetPlaced(target.position);
                }
            }

            UpdateMode();
            UpdateFacing();

            // Protegiendo la mano, mira al jugador: si no, casi siempre queda
            // de espaldas y no se ve la quemadura.
            if (Current == Mode.Guard && viewer != null && reach != null && burnSequence != null
                && burnSequence.CurrentState != BurnSequenceManager.BurnState.Idle)
            {
                reach.FaceTo(viewer.position);
            }

            if (!target.isKinematic)
            {
                return;
            }

            Vector3 goal = Goal();
            Vector3 next = Vector3.SmoothDamp(target.position, goal, ref velocity, smoothTime, maxHandSpeed,
                Time.fixedDeltaTime);
            target.MovePosition(next);
        }

        private void UpdateMode()
        {
            BurnSequenceManager.BurnState state = burnSequence != null
                ? burnSequence.CurrentState
                : BurnSequenceManager.BurnState.Burned;

            switch (Current)
            {
                case Mode.Placed:
                    // Soltada justo bajo el agua (o el agua se abrio despues):
                    // la deja ahi.
                    if (state == BurnSequenceManager.BurnState.Cooling && WaterRunning)
                    {
                        Current = Mode.InWater;
                        waterLostTime = -1f;
                    }
                    else if (Time.time - placedTime > holdAfterRelease)
                    {
                        Current = Mode.Guard;
                    }

                    break;

                case Mode.Show:
                    if (Time.time > showUntil)
                    {
                        Current = Mode.Guard;
                    }

                    break;

                case Mode.InWater:
                    if (!WaterRunning)
                    {
                        if (waterLostTime < 0f)
                        {
                            waterLostTime = Time.time;
                        }
                        else if (Time.time - waterLostTime > 1.5f)
                        {
                            Current = Mode.Guard;
                        }
                    }
                    else
                    {
                        waterLostTime = -1f;
                    }

                    // Durante la llamada al 106 la deja en el agua (si la
                    // sacara, el jugador no podria atenderla con el celular
                    // en la mano y ella hablaria encima del operador).
                    if (cooledTime >= 0f && Time.time - cooledTime > withdrawAfterCooled && !EmergencyCall.InCall)
                    {
                        // Ya enfriada: la saca del agua y la ofrece para que se la
                        // cubran (protegida contra el pecho costaba alcanzarla).
                        Current = Mode.Guard;
                        ShowHand(6f);
                    }

                    break;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            UpdateLeftHand();
            UpdateHead();
            UpdatePalm();
        }

        // Bajo el chorro (puesta por ella o sostenida ahi por el jugador) la
        // mano queda plana con el dorso hacia arriba, para que se vea que el
        // agua cae sobre la quemadura.
        private void UpdatePalm()
        {
            bool held = SimpleGrab.IsHeldObject(target);
            bool underWater = Current == Mode.InWater || Current == Mode.Show
                || (held && Current != Mode.Cooking && WaterRunning && PalmUnderStream(0.1f));
            float speed = Time.deltaTime / Mathf.Max(0.05f, palmTurnSeconds);
            palmDown = Mathf.MoveTowards(palmDown, underWater ? 1f : 0f, speed);
            if (rightArm)
            {
                body.rightPalmDown = palmDown;
            }
            else
            {
                body.leftPalmDown = palmDown;
            }
        }

        // La otra mano sostiene la quemada por debajo de la muneca, salvo
        // mientras cocina, mientras el jugador la lleva o mientras esta bajo
        // el agua (ahi se apoya en el meson).
        private void UpdateLeftHand()
        {
            bool held = SimpleGrab.IsHeldObject(target);
            bool cradling = cradleWithOtherHand && Current == Mode.Guard && !held && !receiving;
            Transform hand = body.HandBone(rightArm);
            Transform fore = body.ForeArm(rightArm);
            if (cradling && hand != null && fore != null)
            {
                // Por debajo del antebrazo quemado, sosteniendolo: antes iba a
                // 3,5 cm de la muneca y los dedos atravesaban el antebrazo.
                Vector3 along = (hand.position - fore.position).normalized;
                cradle.position = body.WristPosition(rightArm) - along * cradleTowardElbow
                    + Vector3.down * cradleBelow;
                SetOtherArm(PatientBody.ArmMode.Reach, cradle);
            }
            else if (!receiving)
            {
                SetOtherArm(PatientBody.ArmMode.Rest, null);
            }
        }

        private void SetOtherGrip(float weight)
        {
            if (rightArm)
            {
                body.leftGrip = weight;
            }
            else
            {
                body.rightGrip = weight;
            }
        }

        private void SetOtherArm(PatientBody.ArmMode mode, Transform t)
        {
            if (rightArm)
            {
                body.leftMode = mode;
                body.leftTarget = t;
            }
            else
            {
                body.rightMode = mode;
                body.rightTarget = t;
            }
        }

        // Mientras cocina mira la olla (salvo que le hable al jugador).
        private void UpdateHead()
        {
            if (Current != Mode.Cooking || pot == null)
            {
                return;
            }

            bool atPlayer = (Time.time < lookAtPlayerUntil || FacingPlayer) && viewer != null;
            Vector3 look = atPlayer ? viewer.position : pot.BottomCenter;
            Vector3 toLook = look - body.Head.position;
            Vector3 flat = Vector3.ProjectOnPlane(toLook, Vector3.up);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(PatientBody.Flat(transform.forward), flat, Vector3.up), -60f, 60f);
            float pitch = Mathf.Clamp(Mathf.Atan2(-toLook.y, Mathf.Max(0.05f, flat.magnitude)) * Mathf.Rad2Deg, -15f, 40f);
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            body.headYaw = Mathf.Lerp(body.headYaw, yaw, k);
            body.headPitch = Mathf.Lerp(body.headPitch, pitch * 0.8f, k);
        }

        // ---------------- recibir un objeto ----------------

        private IEnumerator Receive(Rigidbody rb, System.Func<Vector3> putDownSpot)
        {
            receiving = true;
            facingForReceive = true;
            GameObject point = new GameObject("PuntoRecibir");
            point.AddComponent<NoAutoCollider>();

            // 1. Se voltea hacia el jugador (la gira UpdateFacing) antes de
            // estirar la mano.
            float t = 0f;
            while (t < 1.5f && rb != null && viewer != null && !FacingToward(viewer.position, 25f))
            {
                t += Time.deltaTime;
                yield return null;
            }

            // 2. Estira la mano libre hacia el costado del objeto (no a su
            // centro: la mano quedaba metida en la taza) y la cierra.
            t = 0f;
            SetOtherArm(PatientBody.ArmMode.Reach, point.transform);
            while (t < 0.9f && rb != null)
            {
                t += Time.deltaTime;
                Vector3 toShoulder = PatientBody.Flat(body.ShoulderPosition(!rightArm) - rb.worldCenterOfMass);
                toShoulder = toShoulder.sqrMagnitude > 1e-6f ? toShoulder.normalized : -PatientBody.Flat(transform.forward).normalized;
                point.transform.position = rb.worldCenterOfMass + toShoulder * 0.05f;
                SetOtherGrip(Mathf.SmoothStep(0f, 1f, t / 0.9f));
                yield return null;
            }

            if (rb == null)
            {
                EndReceive(point);
                yield break;
            }

            // 3. Lo toma.
            SimpleGrab.ForceRelease(rb);
            bool wasKinematic = rb.isKinematic;
            RigidbodyInterpolation interpolation = rb.interpolation;
            rb.isKinematic = true;
            // Sin interpolacion: si no, el Rigidbody pisa la posicion que se
            // le da al Transform.
            rb.interpolation = RigidbodyInterpolation.None;
            Collider[] cols = rb.GetComponentsInChildren<Collider>();
            Bounds bounds = new Bounds(rb.position, Vector3.zero);
            foreach (Collider c in cols)
            {
                bounds.Encapsulate(c.bounds);
                c.enabled = false;
            }

            // El centro del objeto va frente a la palma, tocandola (antes
            // conservaba la distancia a la que estaba y flotaba al costado de
            // la mano). Siempre derecho: antes giraba con la muneca.
            float radius = Mathf.Max(0.02f, Mathf.Min(bounds.extents.x, bounds.extents.z));
            Vector3 centerOffset = new Vector3(0f, bounds.center.y - rb.position.y, 0f);
            Quaternion upright = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
            Vector3 startPos = rb.position;
            float held = 0f;
            const float settle = 0.25f;

            // 4. Vuelve a mirar la cocina con la taza en la mano.
            facingForReceive = false;
            Vector3 hold = point.transform.position;
            float back = 0f;
            while (back < 2f && rb != null && !FacingToward(transform.position + homeForward, 12f))
            {
                back += Time.deltaTime;
                held += Time.deltaTime;
                Vector3 side = PatientBody.Flat(transform.right).normalized * (rightArm ? -1f : 1f);
                Vector3 wanted = body.Chest.position + PatientBody.Flat(body.TorsoForward).normalized * 0.28f
                    + side * 0.12f + Vector3.down * 0.18f;
                hold = Vector3.Lerp(hold, wanted, 1f - Mathf.Exp(-6f * Time.deltaTime));
                point.transform.position = hold;
                HoldInPalm(rb, radius, centerOffset, upright, startPos, Mathf.Clamp01(held / settle));
                yield return null;
            }

            // 5. Lo lleva hasta el meson (el lugar se pide ya mirando la cocina).
            Vector3 putDown = putDownSpot != null ? putDownSpot() : (rb != null ? rb.position : transform.position);
            Vector3 from = point.transform.position;
            float carryT = 0f;
            const float carry = 1.2f;
            while (carryT < carry && rb != null)
            {
                carryT += Time.deltaTime;
                held += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, carryT / carry);
                point.transform.position = Vector3.Lerp(from, putDown + Vector3.up * 0.1f, k);
                HoldInPalm(rb, radius, centerOffset, upright, startPos, Mathf.Clamp01(held / settle));
                yield return null;
            }

            // 6. Lo apoya y abre la mano.
            if (rb != null)
            {
                rb.transform.SetPositionAndRotation(putDown + Vector3.up * 0.003f, upright);
                rb.position = rb.transform.position;
                rb.rotation = rb.transform.rotation;
            }

            float release = 0f;
            while (release < 0.35f)
            {
                release += Time.deltaTime;
                SetOtherGrip(1f - release / 0.35f);
                yield return null;
            }

            SetOtherGrip(0f);

            if (rb != null)
            {
                rb.interpolation = interpolation;
                foreach (Collider c in cols)
                {
                    c.enabled = true;
                }

                rb.isKinematic = wasKinematic;
                if (!rb.isKinematic)
                {
                    rb.velocity = Vector3.zero;
                }

                Grabbable g = rb.GetComponent<Grabbable>();
                if (g != null)
                {
                    g.SetHome();
                }
            }

            EndReceive(point);
        }

        // El objeto pegado a la palma; los primeros instantes pasa suave desde
        // donde lo solto el jugador hasta la palma.
        private void HoldInPalm(Rigidbody rb, float radius, Vector3 centerOffset, Quaternion upright, Vector3 startPos,
            float blend)
        {
            if (rb == null)
            {
                return;
            }

            Vector3 inPalm = body.PalmGripPoint(!rightArm, radius + 0.005f) - centerOffset;
            rb.transform.SetPositionAndRotation(Vector3.Lerp(startPos, inPalm, blend), upright);
        }

        private void EndReceive(GameObject point)
        {
            Destroy(point);
            SetOtherArm(PatientBody.ArmMode.Rest, null);
            facingForReceive = false;
            receiving = false;
        }

        // True si mira hacia ese punto (en horizontal) con a lo sumo 'tolerance' grados.
        private bool FacingToward(Vector3 point, float tolerance)
        {
            Vector3 to = PatientBody.Flat(point - transform.position);
            return to.sqrMagnitude < 1e-4f || Vector3.Angle(PatientBody.Flat(transform.forward), to) <= tolerance;
        }

        // Mientras cocina, se voltea hacia el jugador si se acerca con algo
        // para darle (BurnGuide llama a AttendPlayer) o mientras lo recibe;
        // despues vuelve a mirar la cocina.
        private bool FacingPlayer
        {
            get { return Current == Mode.Cooking && (Time.time < attendUntil || facingForReceive); }
        }

        private void UpdateFacing()
        {
            if (Current != Mode.Cooking || reach == null)
            {
                return;
            }

            if (FacingPlayer && viewer != null)
            {
                reach.FaceTo(viewer.position);
            }
            else if (!FacingToward(transform.position + homeForward, 8f))
            {
                reach.FaceTo(transform.position + homeForward * 2f);
            }
        }

        // Mientras atiende al jugador deja de revolver: la mano baja junto al
        // cuerpo. Cerca del pecho (menos de 15 cm en horizontal) para que
        // PatientReach no gire el cuerpo hacia ella y la deje voltearse.
        private Vector3 AttendGoal()
        {
            Vector3 fwd = PatientBody.Flat(body.TorsoForward).normalized;
            Vector3 side = PatientBody.Flat(transform.right).normalized * (rightArm ? 1f : -1f);
            return body.Chest.position + fwd * 0.04f + side * 0.11f + Vector3.down * 0.4f;
        }

        // ---------------- cuchara ----------------

        private void BuildSpoon()
        {
            GameObject root = new GameObject("CucharaDePalo");
            root.AddComponent<NoAutoCollider>();
            spoonRoot = root.transform;
            Color wood = new Color(0.62f, 0.43f, 0.24f);
            // Mango a lo largo del eje Z local (de la mano hacia la olla).
            PropKit.Part(spoonRoot, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.13f), new Vector3(0.012f, 0.13f, 0.012f),
                Quaternion.Euler(90f, 0f, 0f), "Madera", wood);
            PropKit.Part(spoonRoot, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.27f), new Vector3(0.04f, 0.012f, 0.055f),
                Quaternion.identity, "Madera", wood);
        }

        private void UpdateSpoon()
        {
            if (spoonRoot == null || Current != Mode.Cooking || pot == null)
            {
                return;
            }

            if (FacingPlayer || receiving)
            {
                // Atendiendo al jugador: la cuchara queda apoyada en la olla.
                Vector3 toHer = PatientBody.Flat(transform.position - pot.BottomCenter);
                toHer = toHer.sqrMagnitude > 1e-4f ? toHer.normalized : -transform.forward;
                Vector3 tip = pot.BottomCenter + Vector3.up * (pot.Height * 0.45f);
                Vector3 down = (Vector3.down - toHer * 0.45f).normalized;
                spoonRoot.SetPositionAndRotation(tip - down * 0.27f, Quaternion.LookRotation(down, toHer));
                return;
            }

            Transform hand = body.HandBone(rightArm);
            if (hand == null)
            {
                return;
            }

            Vector3 grip = stream != null ? stream.PalmOf(hand) : hand.position;
            Vector3 dir = spoonTip - grip;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            // La cuchara se desliza en la mano para que la paleta (a 0,27 m del
            // extremo) quede dentro de la olla y no salga por el fondo.
            float back = Mathf.Max(0.02f, 0.27f - dir.magnitude);
            spoonRoot.SetPositionAndRotation(grip - dir.normalized * back, Quaternion.LookRotation(dir, transform.forward));
        }

        private void DropSpoon()
        {
            if (spoonRoot == null)
            {
                return;
            }

            CapsuleCollider col = spoonRoot.gameObject.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.center = new Vector3(0f, 0f, 0.14f);
            col.radius = 0.015f;
            col.height = 0.3f;
            Rigidbody rb = spoonRoot.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.velocity = (-transform.forward + Vector3.up * 0.6f) * 1.2f;
            rb.angularVelocity = Random.insideUnitSphere * 6f;
            // Se le cayo de la mano: en el suelo es creible, no vuelve.
            spoonRoot.gameObject.AddComponent<Grabbable>().returnFromFloor = false;
            spoonRoot = null;
        }
    }
}
