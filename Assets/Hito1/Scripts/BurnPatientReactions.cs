using UnityEngine;

namespace SojaExiles
{
    // Reacciones de la paciente en el caso de quemadura, para que se comporte
    // como una persona y no como un muneco:
    //   - al quemarse: siseo, golpe de la olla, grito y un respingo hacia
    //     atras; mientras le arde, se queja cada tanto y tiembla un poco;
    //   - mira su mano mientras le duele y mira al jugador cuando le habla;
    //   - bajo el agua se alivia; si se saca la mano antes de tiempo, protesta;
    //   - enfriada y cubierta: lo agradece.
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

        [Header("Cuerpo")]
        public float flinchImpulse = -110f;
        public float trembleImpulse = 14f;
        public float maxHeadYaw = 65f;
        public float headTurnSpeed = 3f;

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
        private string pendingLine;
        private float pendingTime;

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

            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            viewer = rig != null && rig.centerEyeAnchor != null
                ? rig.centerEyeAnchor
                : (Camera.main != null ? Camera.main.transform : null);

            if (burnSequence != null)
            {
                burnSequence.OnStateChanged += HandleState;
                lastState = burnSequence.CurrentState;
            }
        }

        private void OnDestroy()
        {
            if (burnSequence != null)
            {
                burnSequence.OnStateChanged -= HandleState;
            }
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

                case BurnSequenceManager.BurnState.Success:
                    Hito1FX.PlayAt("logro_final", hand, 0.7f, 0f);
                    SayLater("voz_exito", 0.9f);
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

            if (pendingLine != null && Time.time >= pendingTime)
            {
                Say(pendingLine, true);
                pendingLine = null;
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
        }

        private bool IsSpeaking
        {
            get { return voice != null && voice.isPlaying; }
        }

        private void Say(string line, bool interrupt)
        {
            if (voice == null || (IsSpeaking && !interrupt))
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

        private void SayLater(string line, float delay)
        {
            pendingLine = line;
            pendingTime = Time.time + delay;
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
