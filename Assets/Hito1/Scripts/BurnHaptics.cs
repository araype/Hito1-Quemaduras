using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Vibracion de los controles en la escena de quemadura, con patrones
    // distintos para que se aprendan sin mirar:
    //   - suave y continua mientras la mano de la paciente esta bajo el
    //     chorro y el jugador la sostiene (mas fuerte si el agua es caliente);
    //   - pulsos cada vez mas seguidos al acercar la mano al fuego prendido;
    //   - un golpe corto y fuerte ante un error (dos ante un error critico);
    //   - un toque leve al hacer algo bien;
    //   - un toque corto en la mano que agarra algo (como sentir el objeto);
    //   - aspera en las dos manos al cortar una tira de film (FilmStrip);
    //   - suave mientras se desliza la bolsa por la mano quemada (CoverItem).
    // Con seguimiento de manos (sin controles) no hace nada.
    public class BurnHaptics : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Range(0f, 1f)] public float intensity = 1f;

        [Header("Fuego")]
        public float burnerWarnDistance = 0.25f;

        private class Pulse
        {
            public float start, end, amplitude, frequency;
        }

        private readonly List<Pulse>[] pulses = { new List<Pulse>(), new List<Pulse>() };
        private readonly float[] lastAmplitude = new float[2];
        private readonly float[] nextBurnerPulse = new float[2];
        private OVRCameraRig rig;
        private PotFX pot;
        private Rigidbody patientHand;
        private WaterStreamFX[] streams;

        // La de la escena (la usan FilmStrip y CoverItem).
        public static BurnHaptics Current { get; private set; }

        private void Awake()
        {
            Current = this;
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            if (burnSequence != null)
            {
                burnSequence.OnEvent += HandleEvent;
                burnSequence.OnStateChanged += HandleState;
            }

            rig = FindObjectOfType<OVRCameraRig>();
            pot = FindObjectOfType<PotFX>();
            PatientReach reach = FindObjectOfType<PatientReach>();
            patientHand = reach != null ? reach.target : null;
            streams = FindObjectsOfType<WaterStreamFX>();
            SimpleGrab.OnGrabbed += HandleGrab;
        }

        private void HandleGrab(Collider touched, Rigidbody body, bool left)
        {
            Buzz(left ? 0 : 1, 0.3f, 0.04f, 0f, 0.8f);
        }

        private void OnDestroy()
        {
            if (burnSequence != null)
            {
                burnSequence.OnEvent -= HandleEvent;
                burnSequence.OnStateChanged -= HandleState;
            }

            SimpleGrab.OnGrabbed -= HandleGrab;
            if (Current == this)
            {
                Current = null;
            }

            Stop(0);
            Stop(1);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Stop(0);
                Stop(1);
            }
        }

        // Vibracion por un tiempo en una mano (0 = izquierda, 1 = derecha) o
        // en las dos (-1).
        public void Buzz(int hand, float amplitude, float seconds, float delay = 0f, float frequency = 1f)
        {
            for (int h = 0; h < 2; h++)
            {
                if (hand >= 0 && hand != h)
                {
                    continue;
                }

                pulses[h].Add(new Pulse
                {
                    start = Time.unscaledTime + delay,
                    end = Time.unscaledTime + delay + seconds,
                    amplitude = amplitude,
                    frequency = frequency
                });
            }
        }

        private void HandleEvent(BurnSequenceManager.BurnEvent e, string detail, BurnSequenceManager.Severity severity)
        {
            if (severity == BurnSequenceManager.Severity.Critical)
            {
                Buzz(-1, 1f, 0.22f);
                Buzz(-1, 1f, 0.22f, 0.34f);
                return;
            }

            if (severity == BurnSequenceManager.Severity.Error)
            {
                Buzz(-1, 1f, 0.25f);
                return;
            }

            switch (e)
            {
                case BurnSequenceManager.BurnEvent.BurnerOff:
                case BurnSequenceManager.BurnEvent.ColdWaterOn:
                case BurnSequenceManager.BurnEvent.RingRemoved:
                case BurnSequenceManager.BurnEvent.SweaterOn:
                case BurnSequenceManager.BurnEvent.Covered:
                    Buzz(-1, 0.35f, 0.08f);
                    break;
            }
        }

        private void HandleState(BurnSequenceManager.BurnState state)
        {
            if (state == BurnSequenceManager.BurnState.Cooled || state == BurnSequenceManager.BurnState.Covered
                || state == BurnSequenceManager.BurnState.Success)
            {
                Buzz(-1, 0.4f, 0.08f);
                Buzz(-1, 0.4f, 0.08f, 0.16f);
            }
        }

        private void Update()
        {
            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                float amplitude = 0f;
                float frequency = 1f;

                List<Pulse> list = pulses[h];
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Pulse p = list[i];
                    if (Time.unscaledTime >= p.end)
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    if (Time.unscaledTime >= p.start && p.amplitude > amplitude)
                    {
                        amplitude = p.amplitude;
                        frequency = p.frequency;
                    }
                }

                // En pausa solo vibran los botones del menu (tiempo sin
                // escalar), no el agua ni el fuego.
                if (!PauseMenu.Paused)
                {
                    float water = WaterOnHeldHand(left);
                    if (water > amplitude)
                    {
                        amplitude = water;
                        frequency = 0.3f;
                    }

                    BurnerWarning(h);
                }

                amplitude *= intensity;
                OVRInput.Controller controller = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                if (amplitude > 0.001f || lastAmplitude[h] > 0.001f)
                {
                    OVRInput.SetControllerVibration(frequency, amplitude, controller);
                }

                lastAmplitude[h] = amplitude;
            }
        }

        // Vibracion continua si esta mano sostiene la de la paciente y la
        // mano esta bajo el chorro.
        private float WaterOnHeldHand(bool left)
        {
            if (patientHand == null || !SimpleGrab.IsHeldBy(patientHand, left) || streams == null)
            {
                return 0f;
            }

            foreach (WaterStreamFX stream in streams)
            {
                if (stream != null && stream.IsFlowing && stream.HitOnSkin)
                {
                    float heat = stream.tap != null ? stream.tap.Heat : 0f;
                    return heat > 0.9f ? 0.5f : 0.12f;
                }
            }

            return 0f;
        }

        private void BurnerWarning(int h)
        {
            if (pot == null || !pot.BurnerOn || rig == null || Time.unscaledTime < nextBurnerPulse[h])
            {
                return;
            }

            Transform anchor = h == 0 ? rig.leftHandAnchor : rig.rightHandAnchor;
            if (anchor == null)
            {
                return;
            }

            float d = Vector3.Distance(anchor.position, pot.BurnerPosition);
            if (d > burnerWarnDistance)
            {
                return;
            }

            float k = 1f - d / burnerWarnDistance;
            Buzz(h, Mathf.Lerp(0.25f, 0.7f, k), 0.05f);
            nextBurnerPulse[h] = Time.unscaledTime + Mathf.Lerp(0.5f, 0.12f, k);
        }

        private void Stop(int h)
        {
            OVRInput.SetControllerVibration(0f, 0f, h == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
            lastAmplitude[h] = 0f;
        }
    }
}
