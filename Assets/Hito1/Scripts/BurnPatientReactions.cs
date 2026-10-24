using UnityEngine;

namespace SojaExiles
{
    // Reacciones de la paciente en el caso de quemadura, para que se comporte
    // como una persona y no como un muneco:
    //   - al quemarse: siseo, golpe de la olla, grito y un respingo hacia
    //     atras; mientras le arde, se queja cada tanto y tiembla un poco;
    //   - mira su mano mientras le duele y mira al jugador cuando le habla;
    //   - bajo el agua se alivia; si se saca la mano antes de tiempo, protesta;
    //   - enfriada y cubierta: lo agradece;
    //   - respira siempre (el pecho y los hombros suben y bajan); con dolor
    //     respira rapido y se la oye jadear, y se calma al enfriar la mano;
    //   - al quemarse sacude la mano por reflejo;
    //   - reacciona a lo que hace el jugador (BurnSequenceManager.OnEvent):
    //     grita con el agua caliente, aparta la mano del hielo o la
    //     mantequilla, agradece el anillo y la chompa.
    // Las pistas de voz (pedir agua o algo para cubrirse) se pueden apagar
    // para una evaluacion sin ayudas.
    // Corre despues de PatientReach (que fija la inclinacion de la cabeza) y
    // antes de PatientBody (que arma la pose en LateUpdate).
    [RequireComponent(typeof(PatientBody))]
    [DefaultExecutionOrder(260)]
    public class BurnPatientReactions : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;

        [Header("Voz")]
        public float voiceVolume = 1f;
        [Tooltip("Pistas habladas si el jugador no sabe que hacer.")]
        public bool voiceHints = true;
        public float waterHintDelay = 18f;
        public float clothHintDelay = 12f;
        public Vector2 painLineInterval = new Vector2(7f, 12f);

        [Header("Respiracion")]
        public float calmBreathsPerMinute = 15f;
        public float painBreathsPerMinute = 36f;
        public float painBreathDepth = 1.6f;
        public float painBreathVolume = 0.35f;

        [Header("Cuerpo")]
        public float flinchImpulse = -110f;
        public float trembleImpulse = 14f;
        public float maxHeadYaw = 65f;
        public float headTurnSpeed = 3f;
        [Tooltip("Sacudida de la mano al quemarse: amplitud (grados) y duracion (s).")]
        public float handShakeDegrees = 32f;
        public float handShakeSeconds = 1.6f;

        private static readonly string[] PainLines = { "voz_dolor_1", "voz_dolor_2", "voz_quemadura_2" };

        private PatientBody body;
        private AudioSource voice;
        private Transform viewer;
        private BurnSequenceManager.BurnState lastState = BurnSequenceManager.BurnState.Idle;
        private float stateTime;
        private float burnedWithoutWater;
        private float nextPainLine;
        private float nextTremble;
        private float nextProtest;
        private bool waterHinted, clothHinted, relieved;
        // Frases en cola: cada una espera a que termine la anterior.
        private readonly System.Collections.Generic.List<string> queuedLines =
            new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<float> queuedGaps =
            new System.Collections.Generic.List<float>();
        private float nextQueuedTime;
        private AudioSource breathing;
        private float breathPhase;
        private float pain;
        private float shakeUntil = float.NegativeInfinity;
        private float painBoostUntil = float.NegativeInfinity;
        private PatientReach reach;
        private Coroutine recoil;

        private void Awake()
        {
            body = GetComponent<PatientBody>();
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            Transform head = body.IsReady ? body.Head : transform;
            GameObject go = new GameObject("VozPaciente");
            go.transform.SetParent(head, false);
            voice = Hito1FX.CreateSource(go, null, voiceVolume, false, 0.6f, 9f);
            voice.spread = 60f;
            breathing = Hito1FX.CreateSource(go, "respiracion_dolor_loop", 0f, true, 0.4f, 5f);

            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            viewer = rig != null && rig.centerEyeAnchor != null
                ? rig.centerEyeAnchor
                : (Camera.main != null ? Camera.main.transform : null);

            if (burnSequence != null)
            {
                burnSequence.OnStateChanged += HandleState;
                burnSequence.OnEvent += HandleEvent;
                lastState = burnSequence.CurrentState;
            }

            reach = GetComponent<PatientReach>();
        }

        private void OnDestroy()
        {
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged -= HandleState;
                burnSequence.OnEvent -= HandleEvent;
            }
        }

        private void HandleEvent(BurnSequenceManager.BurnEvent e, string detail, BurnSequenceManager.Severity severity)
        {
            Vector3 hand = HandPosition();
            switch (e)
            {
                case BurnSequenceManager.BurnEvent.HotWaterOnBurn:
                    Say("voz_agua_caliente", true);
                    Hurt(1f);
                    break;

                case BurnSequenceManager.BurnEvent.RemedyNear:
                    // El algodon no le parece mal (el error es que suelta
                    // pelusa); de lo demas aparta la mano.
                    if (detail == HomeRemedy.Remedy.Algodon.ToString())
                    {
                        break;
                    }

                    Say(detail == HomeRemedy.Remedy.Hielo.ToString() ? "voz_hielo_no" : "voz_remedio_no", true);
                    PullHandAway(HomeRemedy.LastNear != null ? HomeRemedy.LastNear.transform.position : hand);
                    break;

                case BurnSequenceManager.BurnEvent.RemedyApplied:
                    if (severity != BurnSequenceManager.Severity.Critical)
                    {
                        break;
                    }

                    Say(detail == HomeRemedy.Remedy.Hielo.ToString() ? "voz_hielo_duele" : "voz_remedio_arde", true);
                    Hurt(1f);
                    break;

                case BurnSequenceManager.BurnEvent.CoveredTooEarly:
                    Say("voz_saco_agua", true);
                    break;

                case BurnSequenceManager.BurnEvent.BurnedHandGrabbed:
                    Say("voz_mano_no", true);
                    Hurt(0.6f);
                    break;

                case BurnSequenceManager.BurnEvent.RingRemoved:
                    SayLater("voz_anillo_sale", 0.6f);
                    break;

                case BurnSequenceManager.BurnEvent.RingStuck:
                    Say("voz_anillo_atorado", true);
                    Hurt(0.4f);
                    break;

                case BurnSequenceManager.BurnEvent.SweaterOn:
                    SayLater("voz_abrigo", 0.6f);
                    break;

                case BurnSequenceManager.BurnEvent.MedicalDecision:
                    SayLater(detail == "CentroDeSalud" ? "voz_decision_ir" : "voz_decision_casa", 0.4f);
                    break;
            }
        }

        // Le dolio algo: respingo, sacude la mano y respira agitada un rato.
        private void Hurt(float strength)
        {
            body.Impulse(flinchImpulse * strength, 0.08f * strength);
            shakeUntil = Time.time + handShakeSeconds * Mathf.Max(0.5f, strength);
            painBoostUntil = Time.time + 6f * strength;
        }

        // Aparta la mano de algo que le acercan (si el jugador no la tiene
        // agarrada; si la tiene, solo se sobresalta).
        private void PullHandAway(Vector3 from)
        {
            // Con PatientBurnArm la mano la maneja ese componente.
            PatientBurnArm arm = GetComponent<PatientBurnArm>();
            if (arm != null && arm.enabled)
            {
                if (!arm.Avoid(from))
                {
                    body.Impulse(flinchImpulse * 0.5f, 0.04f);
                    shakeUntil = Time.time + 0.6f;
                }

                return;
            }

            Rigidbody target = reach != null ? reach.target : null;
            if (target == null || SimpleGrab.IsHeldObject(target))
            {
                body.Impulse(flinchImpulse * 0.5f, 0.04f);
                shakeUntil = Time.time + 0.6f;
                return;
            }

            if (recoil != null)
            {
                StopCoroutine(recoil);
            }

            recoil = StartCoroutine(MoveHand(target, from));
        }

        private System.Collections.IEnumerator MoveHand(Rigidbody target, Vector3 from)
        {
            Vector3 away = Vector3.ProjectOnPlane(target.position - from, Vector3.up);
            if (away.sqrMagnitude < 1e-4f)
            {
                away = -body.TorsoForward;
            }

            Vector3 start = target.position;
            Vector3 end = start + away.normalized * 0.14f + Vector3.up * 0.03f;
            float t = 0f;
            while (t < 0.3f && target != null && !SimpleGrab.IsHeldObject(target))
            {
                t += Time.deltaTime;
                target.MovePosition(Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / 0.3f)));
                yield return null;
            }

            recoil = null;
        }

        private void HandleState(BurnSequenceManager.BurnState state)
        {
            BurnSequenceManager.BurnState previous = lastState;
            lastState = state;
            stateTime = 0f;
            Vector3 hand = HandPosition();

            switch (state)
            {
                case BurnSequenceManager.BurnState.Burned:
                    if (previous == BurnSequenceManager.BurnState.Idle)
                    {
                        Hito1FX.PlayAt("quemadura_siseo", hand, 0.9f);
                        PotFX pot = FindObjectOfType<PotFX>();
                        Hito1FX.PlayAt("olla_golpe", pot != null ? pot.transform.position : hand, 0.8f);
                        Say("voz_quemadura_1", true);
                        body.Impulse(flinchImpulse, 0.12f);
                        shakeUntil = Time.time + handShakeSeconds;
                        nextPainLine = Time.time + Random.Range(painLineInterval.x, painLineInterval.y) + 2f;
                    }
                    else if (previous == BurnSequenceManager.BurnState.Cooling && Time.time >= nextProtest)
                    {
                        // Saco la mano del agua antes de tiempo.
                        nextProtest = Time.time + 6f;
                        Say("voz_saco_agua", true);
                        body.Impulse(flinchImpulse * 0.4f, 0.04f);
                    }

                    break;

                case BurnSequenceManager.BurnState.Cooling:
                    if (!relieved)
                    {
                        relieved = true;
                        SayLater("voz_alivio_agua", 1.2f);
                    }

                    break;

                case BurnSequenceManager.BurnState.Cooled:
                    Hito1FX.PlayAt("logro_paso", hand, 0.6f, 0f);
                    SayLater("voz_enfriada", 0.7f);
                    break;

                case BurnSequenceManager.BurnState.Covered:
                    Hito1FX.PlayAt("logro_paso", hand, 0.6f, 0f);
                    SayLater("voz_exito", 0.9f);
                    SayLater("voz_pregunta_medico", 0.6f);
                    break;

                case BurnSequenceManager.BurnState.Success:
                    Hito1FX.PlayAt("logro_final", hand, 0.7f, 0f);
                    break;
            }
        }

        private void Update()
        {
            if (burnSequence == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            stateTime += dt;
            BurnSequenceManager.BurnState state = burnSequence.CurrentState;

            if (queuedLines.Count > 0 && Time.time >= nextQueuedTime && !IsSpeaking)
            {
                Say(queuedLines[0], true);
                queuedLines.RemoveAt(0);
                queuedGaps.RemoveAt(0);
                if (queuedLines.Count > 0)
                {
                    float length = voice != null && voice.clip != null ? voice.clip.length : 0f;
                    nextQueuedTime = Time.time + length + queuedGaps[0];
                }
            }

            if (state == BurnSequenceManager.BurnState.Burned)
            {
                burnedWithoutWater += dt;
                if (Time.time >= nextPainLine && !IsSpeaking)
                {
                    nextPainLine = Time.time + Random.Range(painLineInterval.x, painLineInterval.y);
                    Say(PainLines[Random.Range(0, PainLines.Length)], false);
                }

                if (voiceHints && !waterHinted && burnedWithoutWater > waterHintDelay && !relieved && !IsSpeaking)
                {
                    waterHinted = true;
                    Say("voz_pista_agua", false);
                }

                // Le duele: pequenos estremecimientos.
                if (Time.time >= nextTremble)
                {
                    nextTremble = Time.time + Random.Range(0.35f, 0.8f);
                    body.Impulse(Random.Range(-trembleImpulse, trembleImpulse), 0f);
                }
            }

            if (state == BurnSequenceManager.BurnState.Cooled && voiceHints && !clothHinted
                && stateTime > clothHintDelay && !IsSpeaking)
            {
                clothHinted = true;
                Say("voz_pista_pano", false);
            }

            UpdateHead(state, dt);
            UpdateBreathing(state, dt);
            UpdateHandShake();
        }

        // Dolor (0 a 1) segun la etapa; se suaviza para que la respiracion
        // se calme de a poco.
        private void UpdateBreathing(BurnSequenceManager.BurnState state, float dt)
        {
            float wanted;
            switch (state)
            {
                case BurnSequenceManager.BurnState.Burned:
                    wanted = 1f;
                    break;
                case BurnSequenceManager.BurnState.Cooling:
                    wanted = 0.55f;
                    break;
                case BurnSequenceManager.BurnState.Cooled:
                    wanted = 0.2f;
                    break;
                default:
                    wanted = 0f;
                    break;
            }

            if (Time.time < painBoostUntil)
            {
                wanted = 1f;
            }

            pain = Mathf.MoveTowards(pain, wanted, dt / (wanted > pain ? 0.5f : 6f));

            float perMinute = Mathf.Lerp(calmBreathsPerMinute, painBreathsPerMinute, pain);
            breathPhase = Mathf.Repeat(breathPhase + dt * perMinute / 60f * Mathf.PI * 2f, Mathf.PI * 2f);
            float depth = Mathf.Lerp(1f, painBreathDepth, pain);
            body.breath = depth * (1f - Mathf.Cos(breathPhase)) * 0.5f;

            // Mientras habla no se oye el jadeo.
            float volume = IsSpeaking ? 0f : painBreathVolume * Mathf.Clamp01((pain - 0.15f) / 0.85f);
            Hito1FX.FadeLoop(breathing, volume, 1f);
        }

        private void UpdateHandShake()
        {
            float left = shakeUntil - Time.time;
            if (left <= 0f)
            {
                body.rightHandShake = 0f;
                return;
            }

            float k = Mathf.Clamp01(left / Mathf.Max(0.1f, handShakeSeconds));
            body.rightHandShake = handShakeDegrees * k * Mathf.Sin(Time.time * Mathf.PI * 2f * 7f);
        }

        public bool IsSpeaking
        {
            get { return voice != null && voice.isPlaying; }
        }

        public void Say(string line, bool interrupt)
        {
            if (voice == null || (IsSpeaking && !interrupt))
            {
                return;
            }

            // Las quejas y pistas no tapan al narrador; los gritos si.
            if (!interrupt && BurnGuide.Instance != null && BurnGuide.Instance.Speaking)
            {
                return;
            }

            AudioClip clip = Hito1FX.Clip(line);
            if (clip == null)
            {
                return;
            }

            voice.Stop();
            voice.clip = clip;
            voice.volume = voiceVolume;
            voice.pitch = Random.Range(0.97f, 1.03f);
            voice.Play();
        }

        // Dice la frase despues de 'delay' segundos (o despues de las que ya
        // estan en cola).
        private void SayLater(string line, float delay)
        {
            if (queuedLines.Count == 0)
            {
                nextQueuedTime = Time.time + delay;
            }

            queuedLines.Add(line);
            queuedGaps.Add(delay);
        }

        // Mientras le duele mira su mano; cuando habla, o cuando ya paso todo,
        // mira al jugador.
        private void UpdateHead(BurnSequenceManager.BurnState state, float dt)
        {
            if (!body.IsReady || body.Head == null)
            {
                return;
            }

            bool hurting = state == BurnSequenceManager.BurnState.Burned
                || state == BurnSequenceManager.BurnState.Cooling;
            Vector3 lookAt;
            if (hurting && !IsSpeaking)
            {
                lookAt = HandPosition();
            }
            else if (viewer != null)
            {
                lookAt = viewer.position;
            }
            else
            {
                return;
            }

            Vector3 up = body.TorsoUp;
            Vector3 forward = body.TorsoForward;
            Vector3 toTarget = Vector3.ProjectOnPlane(lookAt - body.Head.position, up);
            if (toTarget.sqrMagnitude < 1e-4f)
            {
                return;
            }

            float yaw = Mathf.Clamp(Vector3.SignedAngle(forward, toTarget, up), -maxHeadYaw, maxHeadYaw);
            body.headYaw = Mathf.Lerp(body.headYaw, yaw, 1f - Mathf.Exp(-headTurnSpeed * dt));
        }

        private Vector3 HandPosition()
        {
            Transform hand = body.IsReady ? body.HandBone(true) : null;
            return hand != null ? hand.position : transform.position;
        }
    }
}
