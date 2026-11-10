using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Cara de la tia de Avaturn (blendshapes ARKit y visemas del GLB):
    //   - Gesto de fondo segun como esta: tranquila cocinando, dolor (sigue
    //     el dolor de BurnPatientReactions), alivio con el agua, gracias al
    //     final.
    //   - Gestos cortos con su voz (ForVoice, lo llama BurnPatientReactions.Say):
    //     el golpe al quemarse, miedo, alivio, ceno al negar o si le aprietan
    //     la mano.
    //   - Boca al ritmo de su voz (volumen del AudioSource, con visemas que
    //     van cambiando), parpadeo y ojos que miran al jugador.
    // Lo instala Hito1RuntimeBootstrap solo si el modelo tiene expresiones.
    [DefaultExecutionOrder(320)]
    public class PatientFace : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;

        [Tooltip("Segundos para pasar de un gesto a otro.")]
        public float blendSeconds = 0.35f;
        [Tooltip("Cuanto abre la boca al hablar (0 a 1).")]
        public float talkOpen = 0.55f;
        [Tooltip("Volumen de la voz que ya abre la boca del todo.")]
        public float talkLoudness = 0.12f;
        [Tooltip("Grados maximos que giran los ojos para mirar al jugador.")]
        public float eyeMaxDegrees = 22f;

        // Gestos: blendshape -> peso (0 a 1).
        private static readonly Dictionary<string, Dictionary<string, float>> Expressions =
            new Dictionary<string, Dictionary<string, float>>
            {
                { "Tranquila", new Dictionary<string, float> { { "mouthSmileLeft", 0.12f }, { "mouthSmileRight", 0.12f } } },
                {
                    "Dolor", new Dictionary<string, float>
                    {
                        { "browDownLeft", 0.55f }, { "browDownRight", 0.55f }, { "browInnerUp", 0.6f },
                        { "eyeSquintLeft", 0.6f }, { "eyeSquintRight", 0.6f },
                        { "cheekSquintLeft", 0.35f }, { "cheekSquintRight", 0.35f },
                        { "noseSneerLeft", 0.4f }, { "noseSneerRight", 0.4f },
                        { "mouthStretchLeft", 0.45f }, { "mouthStretchRight", 0.45f },
                        { "mouthFrownLeft", 0.35f }, { "mouthFrownRight", 0.35f },
                        { "mouthUpperUpLeft", 0.25f }, { "mouthUpperUpRight", 0.25f }
                    }
                },
                {
                    "Golpe", new Dictionary<string, float>
                    {
                        { "browDownLeft", 0.8f }, { "browDownRight", 0.8f }, { "browInnerUp", 0.7f },
                        { "eyeBlinkLeft", 0.65f }, { "eyeBlinkRight", 0.65f },
                        { "eyeSquintLeft", 0.9f }, { "eyeSquintRight", 0.9f },
                        { "noseSneerLeft", 0.7f }, { "noseSneerRight", 0.7f },
                        { "mouthStretchLeft", 0.7f }, { "mouthStretchRight", 0.7f },
                        { "mouthUpperUpLeft", 0.5f }, { "mouthUpperUpRight", 0.5f }, { "jawOpen", 0.3f }
                    }
                },
                {
                    "Miedo", new Dictionary<string, float>
                    {
                        { "browInnerUp", 0.85f }, { "browOuterUpLeft", 0.45f }, { "browOuterUpRight", 0.45f },
                        { "eyeWideLeft", 0.6f }, { "eyeWideRight", 0.6f },
                        { "mouthStretchLeft", 0.3f }, { "mouthStretchRight", 0.3f },
                        { "mouthFrownLeft", 0.2f }, { "mouthFrownRight", 0.2f }, { "jawOpen", 0.1f }
                    }
                },
                {
                    "Alivio", new Dictionary<string, float>
                    {
                        { "browInnerUp", 0.3f }, { "eyeBlinkLeft", 0.3f }, { "eyeBlinkRight", 0.3f },
                        { "mouthSmileLeft", 0.2f }, { "mouthSmileRight", 0.2f }
                    }
                },
                {
                    "Gracias", new Dictionary<string, float>
                    {
                        { "mouthSmileLeft", 0.55f }, { "mouthSmileRight", 0.55f },
                        { "cheekSquintLeft", 0.3f }, { "cheekSquintRight", 0.3f },
                        { "eyeSquintLeft", 0.2f }, { "eyeSquintRight", 0.2f }, { "browInnerUp", 0.15f }
                    }
                },
                {
                    "Ceno", new Dictionary<string, float>
                    {
                        { "browDownLeft", 0.85f }, { "browDownRight", 0.85f },
                        { "eyeSquintLeft", 0.35f }, { "eyeSquintRight", 0.35f },
                        { "noseSneerLeft", 0.25f }, { "noseSneerRight", 0.25f },
                        { "mouthPressLeft", 0.5f }, { "mouthPressRight", 0.5f },
                        { "mouthFrownLeft", 0.3f }, { "mouthFrownRight", 0.3f }
                    }
                }
            };

        // Frase de la tia -> gesto corto que la acompana.
        private static readonly Dictionary<string, string> VoiceFaces = new Dictionary<string, string>
        {
            { "voz_quemadura_1", "Golpe" },
            { "voz_quemadura_2", "Golpe" },
            { "voz_dolor_1", "Golpe" },
            { "voz_dolor_2", "Golpe" },
            { "voz_hielo_duele", "Golpe" },
            { "voz_alivio_agua", "Alivio" },
            { "voz_calmada", "Alivio" },
            { "voz_templada", "Alivio" },
            { "voz_enfriada", "Alivio" },
            { "voz_aguanto", "Alivio" },
            { "voz_exito", "Gracias" },
            { "voz_gracias_taza", "Gracias" },
            { "voz_abrigo", "Gracias" },
            { "voz_anillo_sale", "Gracias" },
            { "voz_remedio_no", "Ceno" },
            { "voz_hielo_no", "Ceno" },
            { "voz_mano_no", "Ceno" },
            { "voz_aprieta", "Ceno" },
            { "voz_ansiosa", "Miedo" },
            { "voz_asustada", "Miedo" }
        };

        // Visemas para hablar (se van turnando mientras suena la voz).
        private static readonly string[] TalkVisemes = { "viseme_aa", "viseme_O", "viseme_E", "viseme_I", "viseme_U" };

        // Cada forma: renderer, indice y peso del gesto completo (glTFast
        // guarda 1, los FBX suelen traer 100; pasar de ahi lo exagera).
        private struct Shape
        {
            public SkinnedMeshRenderer renderer;
            public int index;
            public float full;
        }

        private readonly Dictionary<string, List<Shape>> shapes = new Dictionary<string, List<Shape>>();
        private readonly Dictionary<string, float> current = new Dictionary<string, float>();
        private readonly Dictionary<string, float> target = new Dictionary<string, float>();
        private readonly List<string> keys = new List<string>();

        private BurnPatientReactions reactions;
        private string pulse;
        private float pulseUntil;
        private float nextBlink;
        private float blinkStart = -1f;
        private float talk;
        private float visemeChange;
        private int viseme;
        private readonly float[] samples = new float[256];

        private Transform head, leftEye, rightEye;
        private Quaternion leftEyeRest, rightEyeRest;
        private Vector3 headForward;
        private Transform viewer;

        public static bool HasExpressions(GameObject patient)
        {
            foreach (SkinnedMeshRenderer r in patient.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("browInnerUp") >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Start()
        {
            reactions = GetComponent<BurnPatientReactions>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = r.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string n = mesh.GetBlendShapeName(i);
                    if (!shapes.TryGetValue(n, out List<Shape> list))
                    {
                        list = new List<Shape>();
                        shapes[n] = list;
                        current[n] = 0f;
                        keys.Add(n);
                    }

                    int frames = mesh.GetBlendShapeFrameCount(i);
                    float full = frames > 0 ? mesh.GetBlendShapeFrameWeight(i, frames - 1) : 100f;
                    list.Add(new Shape { renderer = r, index = i, full = full });
                }
            }

            if (shapes.Count == 0)
            {
                enabled = false;
                return;
            }

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head" || t.name.EndsWith(":Head"))
                {
                    head = t;
                }
                else if (t.name == "LeftEye" || t.name.EndsWith(":LeftEye"))
                {
                    leftEye = t;
                }
                else if (t.name == "RightEye" || t.name.EndsWith(":RightEye"))
                {
                    rightEye = t;
                }
            }

            if (head != null && leftEye != null && rightEye != null)
            {
                leftEyeRest = Quaternion.Inverse(head.rotation) * leftEye.rotation;
                rightEyeRest = Quaternion.Inverse(head.rotation) * rightEye.rotation;
                headForward = Quaternion.Inverse(head.rotation) * transform.forward;
            }

            nextBlink = Time.time + Random.Range(1.5f, 4f);
        }

        // Gesto corto que acompana una frase de la tia.
        public void ForVoice(string line)
        {
            if (VoiceFaces.TryGetValue(line, out string face))
            {
                Show(face, face == "Golpe" ? 1.6f : 2.6f);
            }
        }

        // Muestra un gesto durante 'seconds' segundos por encima del de fondo.
        public void Show(string expression, float seconds)
        {
            if (Expressions.ContainsKey(expression))
            {
                pulse = expression;
                pulseUntil = Time.time + seconds;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            target.Clear();

            AddMood();
            if (pulse != null && Time.time < pulseUntil)
            {
                Add(pulse, 1f);
            }

            AddBlink();
            AddTalk(dt);

            float step = blendSeconds > 0f ? dt / blendSeconds : 1f;
            for (int k = 0; k < keys.Count; k++)
            {
                string n = keys[k];
                target.TryGetValue(n, out float goal);
                // El parpadeo y la boca van rapido; lo demas, suave.
                float speed = n.StartsWith("eyeBlink") || n.StartsWith("viseme") || n == "jawOpen" ? step * 4f : step;
                float w = Mathf.MoveTowards(current[n], Mathf.Clamp01(goal), speed);
                if (!Mathf.Approximately(w, current[n]))
                {
                    current[n] = w;
                    foreach (Shape s in shapes[n])
                    {
                        s.renderer.SetBlendShapeWeight(s.index, w * s.full);
                    }
                }
            }

            LookAtViewer();
        }

        // Gesto de fondo segun el estado de la escena y el dolor.
        private void AddMood()
        {
            BurnSequenceManager.BurnState state = burnSequence != null
                ? burnSequence.CurrentState
                : BurnSequenceManager.BurnState.Idle;
            float pain = reactions != null ? reactions.Pain : 0f;

            switch (state)
            {
                case BurnSequenceManager.BurnState.Idle:
                    Add("Tranquila", 1f);
                    break;
                case BurnSequenceManager.BurnState.Success:
                    Add("Gracias", 0.8f);
                    break;
                case BurnSequenceManager.BurnState.Cooled:
                case BurnSequenceManager.BurnState.Covered:
                    Add("Alivio", 0.7f);
                    Add("Dolor", pain * 0.6f);
                    break;
                default:
                    Add("Dolor", Mathf.Max(0.35f, pain));
                    Add("Miedo", 0.25f * (1f - pain));
                    break;
            }
        }

        private void AddBlink()
        {
            if (blinkStart < 0f && Time.time >= nextBlink)
            {
                blinkStart = Time.time;
            }

            if (blinkStart >= 0f)
            {
                float t = (Time.time - blinkStart) / 0.16f;
                float w = t < 0.5f ? t * 2f : 2f - t * 2f;
                Add("eyeBlinkLeft", Mathf.Clamp01(w), true);
                Add("eyeBlinkRight", Mathf.Clamp01(w), true);
                if (t >= 1f)
                {
                    blinkStart = -1f;
                    nextBlink = Time.time + Random.Range(2f, 5.5f);
                }
            }
        }

        private void AddTalk(float dt)
        {
            AudioSource voice = reactions != null ? reactions.VoiceSource : null;
            float loud = 0f;
            if (voice != null && voice.isPlaying && voice.clip != null)
            {
                // Del clip mismo (sirve aunque el audio del editor este
                // apagado); si el clip no se puede leer, de la salida.
                AudioClip clip = voice.clip;
                int channels = Mathf.Max(1, clip.channels);
                int start = voice.timeSamples * channels;
                if (clip.loadType != AudioClipLoadType.DecompressOnLoad
                    || start + samples.Length > clip.samples * channels
                    || !clip.GetData(samples, voice.timeSamples))
                {
                    voice.GetOutputData(samples, 0);
                }

                float sum = 0f;
                for (int i = 0; i < samples.Length; i++)
                {
                    sum += samples[i] * samples[i];
                }

                loud = Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) / Mathf.Max(0.001f, talkLoudness));
            }

            talk = Mathf.MoveTowards(talk, loud, dt * 12f);
            if (talk < 0.02f)
            {
                return;
            }

            if (Time.time >= visemeChange)
            {
                viseme = (viseme + Random.Range(1, TalkVisemes.Length)) % TalkVisemes.Length;
                visemeChange = Time.time + Random.Range(0.08f, 0.16f);
            }

            Add(TalkVisemes[viseme], talk * talkOpen * 1.4f, true);
            Add("jawOpen", talk * talkOpen * 0.6f, true);
        }

        // Ojos al jugador (sin pasar de eyeMaxDegrees respecto a la cabeza).
        private void LookAtViewer()
        {
            if (head == null || leftEye == null || rightEye == null)
            {
                return;
            }

            if (viewer == null && Camera.main != null)
            {
                viewer = Camera.main.transform;
            }

            AimEye(leftEye, leftEyeRest);
            AimEye(rightEye, rightEyeRest);
        }

        private void AimEye(Transform eye, Quaternion rest)
        {
            Quaternion restWorld = head.rotation * rest;
            if (viewer == null)
            {
                eye.rotation = restWorld;
                return;
            }

            Vector3 forward = head.rotation * headForward;
            Vector3 toViewer = viewer.position - eye.position;
            if (Vector3.Angle(forward, toViewer) > 70f)
            {
                eye.rotation = restWorld;
                return;
            }

            Quaternion turn = Quaternion.FromToRotation(forward, toViewer);
            turn.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            angle = Mathf.Clamp(angle, -eyeMaxDegrees, eyeMaxDegrees);
            eye.rotation = Quaternion.AngleAxis(angle, axis) * restWorld;
        }

        private void Add(string expression, float amount)
        {
            if (amount <= 0f || !Expressions.TryGetValue(expression, out Dictionary<string, float> weights))
            {
                return;
            }

            foreach (KeyValuePair<string, float> pair in weights)
            {
                Add(pair.Key, pair.Value * amount, false);
            }
        }

        private void Add(string shape, float weight, bool exact)
        {
            target.TryGetValue(shape, out float w);
            target[shape] = exact ? Mathf.Max(w, weight) : w + weight;
        }
    }
}
