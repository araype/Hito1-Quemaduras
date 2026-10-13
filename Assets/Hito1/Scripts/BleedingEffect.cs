using UnityEngine;

namespace SojaExiles
{
    // Ajusta la emision de un ParticleSystem de goteo de sangre segun el
    // estado del corte: fuerte sin atender, leve con presion, nulo despues.
    [RequireComponent(typeof(ParticleSystem))]
    public class BleedingEffect : MonoBehaviour
    {
        public CutSequenceManager cutSequence;
        public float rateUncontrolled = 25f;
        public float rateWithPressure = 3f;

        private ParticleSystem particles;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            SetRate(0f);
        }

        private void OnEnable()
        {
            if (cutSequence != null)
            {
                cutSequence.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (cutSequence != null)
            {
                cutSequence.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(CutSequenceManager.CutState newState)
        {
            switch (newState)
            {
                case CutSequenceManager.CutState.Cut:
                    SetRate(rateUncontrolled);
                    break;
                case CutSequenceManager.CutState.PressureApplied:
                    SetRate(rateWithPressure);
                    break;
                default:
                    SetRate(0f);
                    break;
            }
        }

        private void SetRate(float rate)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            if (rate > 0f && !particles.isPlaying)
            {
                particles.Play();
            }
        }
    }
}
