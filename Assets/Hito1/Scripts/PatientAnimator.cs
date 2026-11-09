using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Animaciones de Mixamo de la paciente (Assets/Hito1/Personajes/Animaciones,
    // mismo esqueleto mixamorig8): respirar quieta, caminar, quedarse
    // adolorida y gestos de torso y cabeza (asentir, negar, alivio,
    // agradecer, el golpe al quemarse, ansiedad).
    // Pone el controlador Resources/Hito1Anim/TiaAnimator en su Animator y le
    // pasa cada cuadro:
    //   - Speed: la velocidad del cuerpo (PatientBody.walkSpeed, la fija
    //     PatientReach; al girar en el lugar tambien da pasos);
    //   - Dolor: 0 antes del accidente, 1 despues (respirar quieta -> encorvada).
    // Los gestos se disparan con la voz de la paciente (ForVoice, lo llama
    // BurnPatientReactions) y van en una capa con mascara de torso y cabeza:
    // los brazos los sigue manejando PatientBody (mano quemada, taza, anillo).
    // Va en el paciente (lo instala Hito1RuntimeBootstrap).
    public class PatientAnimator : MonoBehaviour
    {
        public static PatientAnimator Instance { get; private set; }

        public BurnSequenceManager burnSequence;
        [Tooltip("Segundos para pasar de tranquila a adolorida despues del accidente.")]
        public float painBlendSeconds = 1.5f;

        // Voz de la paciente -> gesto (trigger del controlador).
        private static readonly Dictionary<string, string> VoiceGestures = new Dictionary<string, string>
        {
            { "voz_quemadura_1", "Golpe" },
            { "voz_quemadura_2", "Golpe" },
            { "voz_alivio_agua", "Alivio" },
            { "voz_calmada", "Alivio" },
            { "voz_templada", "Alivio" },
            { "voz_enfriada", "Alivio" },
            { "voz_aguanto", "Alivio" },
            { "voz_exito", "Gracias" },
            { "voz_gracias_taza", "Gracias" },
            { "voz_abrigo", "Gracias" },
            { "voz_anillo_sale", "Gracias" },
            { "voz_remedio_no", "Negar" },
            { "voz_hielo_no", "Negar" },
            { "voz_mano_no", "Negar" },
            { "voz_aprieta", "Negar" },
            { "voz_decision_ir", "Asentir" },
            { "voz_decision_casa", "Asentir" },
            { "voz_ansiosa", "Nervios" },
            { "voz_asustada", "Nervios" }
        };

        private Animator animator;
        private PatientBody body;
        private float pain;
        private float speed;
        private readonly HashSet<string> triggers = new HashSet<string>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            body = GetComponent<PatientBody>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("Hito1Anim/TiaAnimator");
            animator = GetComponent<Animator>();
            if (controller == null || body == null)
            {
                Debug.LogWarning("[PatientAnimator] Falta Resources/Hito1Anim/TiaAnimator o PatientBody: la paciente "
                    + "queda sin animaciones (como antes).", this);
                enabled = false;
                return;
            }

            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            // Quien mueve y gira a la paciente es PatientReach, no la animacion.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private void Update()
        {
            if (animator == null || body == null)
            {
                return;
            }

            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            speed = Mathf.Lerp(speed, body.walkSpeed, k);
            animator.SetFloat("Speed", speed);

            BurnSequenceManager.BurnState state = burnSequence != null
                ? burnSequence.CurrentState
                : BurnSequenceManager.BurnState.Idle;
            bool hurt = state != BurnSequenceManager.BurnState.Idle && state != BurnSequenceManager.BurnState.Success;
            pain = Mathf.MoveTowards(pain, hurt ? 1f : 0f, Time.deltaTime / Mathf.Max(0.1f, painBlendSeconds));
            animator.SetFloat("Dolor", pain);
        }

        // Gesto por nombre del trigger (Asentir, Negar, Alivio, Gracias, Golpe, Nervios).
        public void Gesture(string trigger)
        {
            if (animator == null || !enabled)
            {
                return;
            }

            // Los parametros recien estan cuando el Animator ya corrio un cuadro
            // con el controlador (en Start todavia vienen vacios).
            if (triggers.Count == 0)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger)
                    {
                        triggers.Add(p.name);
                    }
                }
            }

            if (triggers.Contains(trigger))
            {
                animator.SetTrigger(trigger);
            }
        }

        // La paciente dice una frase: el gesto que la acompana, si hay.
        public void ForVoice(string line)
        {
            if (!string.IsNullOrEmpty(line) && VoiceGestures.TryGetValue(line, out string trigger))
            {
                Gesture(trigger);
            }
        }
    }
}
