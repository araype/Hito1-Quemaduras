using UnityEngine;

namespace SojaExiles
{
    // Mano mojada del paciente: brilla mientras esta bajo el chorro y un rato
    // despues; al sacarla gotea (con un "plic" suave de cada tanto) y se va
    // secando. Va en el paciente (el objeto con PatientBody).
    [RequireComponent(typeof(PatientBody))]
    public class PatientWetHand : MonoBehaviour
    {
        public bool rightHand = true;
        [Tooltip("Segundos que tarda en secarse del todo.")]
        public float dryingSeconds = 25f;
        [Tooltip("Gotas por segundo justo al sacar la mano del agua.")]
        public float dripRate = 10f;
        public float dripVolume = 0.25f;

        private PatientBody body;
        private Transform hand, fingertip;
        private SkinnedMeshRenderer wet;
        private MaterialPropertyBlock block;
        private Color baseColor = new Color(0.9f, 0.93f, 0.96f, 0.12f);
        private ParticleSystem drips;
        private WaterStreamFX[] streams;
        private float wetness;
        private float nextDripSound;

        public float Wetness
        {
            get { return wetness; }
        }

        private void Awake()
        {
            body = GetComponent<PatientBody>();
        }

        private void Start()
        {
            if (!body.IsReady)
            {
                enabled = false;
                return;
            }

            hand = body.HandBone(rightHand);
            fingertip = FindFingertip(hand);
            wet = HandSkinMesh.Build(this, rightHand, 0.5f, 0.002f, "Body", "ManoMojada", "Piel_Mojada");
            if (wet != null)
            {
                Material m = wet.sharedMaterial;
                if (m != null && m.HasProperty("_Color"))
                {
                    baseColor = m.color;
                }

                wet.enabled = false;
            }

            block = new MaterialPropertyBlock();
            BuildDrips();
            streams = FindObjectsOfType<WaterStreamFX>();
        }

        private void OnDestroy()
        {
            if (wet != null)
            {
                Destroy(wet.sharedMesh);
                Destroy(wet.gameObject);
            }

            if (drips != null)
            {
                Destroy(drips.gameObject);
            }
        }

        private void Update()
        {
            bool underWater = IsUnderWater();
            wetness = Mathf.MoveTowards(wetness, underWater ? 1f : 0f,
                Time.deltaTime / (underWater ? 0.6f : Mathf.Max(1f, dryingSeconds)));

            if (wet != null)
            {
                wet.enabled = wetness > 0.01f;
                Color c = baseColor;
                c.a *= wetness;
                block.SetColor("_Color", c);
                block.SetFloat("_Glossiness", Mathf.Lerp(0.6f, 0.95f, wetness));
                wet.SetPropertyBlock(block);
            }

            UpdateDrips(underWater);
        }

        private bool IsUnderWater()
        {
            if (streams == null)
            {
                return false;
            }

            for (int i = 0; i < streams.Length; i++)
            {
                WaterStreamFX s = streams[i];
                if (s == null || !s.IsFlowing || !s.HitOnSkin || s.HitTarget == null)
                {
                    continue;
                }

                Transform t = s.HitTarget;
                if (t == hand || t.IsChildOf(hand))
                {
                    return true;
                }

                // El objetivo agarrable de la mano quemada (esta en la mano
                // derecha del paciente).
                if (rightHand && t.GetComponentInParent<ManoQuemadaVisibility>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindFingertip(Transform hand)
        {
            if (hand == null)
            {
                return null;
            }

            foreach (Transform child in hand)
            {
                if (!child.name.Contains("Middle1"))
                {
                    continue;
                }

                Transform tip = child;
                while (tip.childCount > 0)
                {
                    tip = tip.GetChild(0);
                }

                return tip;
            }

            return null;
        }

        private void BuildDrips()
        {
            GameObject go = new GameObject("GotasMano");
            go.AddComponent<NoAutoCollider>();
            drips = go.AddComponent<ParticleSystem>();
            drips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = drips.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.006f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = drips.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = drips.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.02f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material drop = Hito1FX.Mat("Agua_Gota");
            if (drop != null)
            {
                renderer.sharedMaterial = drop;
            }

            drips.Play();
        }

        private void UpdateDrips(bool underWater)
        {
            if (drips == null || hand == null)
            {
                return;
            }

            // Gotea desde lo mas bajo de la mano (punta de los dedos o palma).
            Vector3 palm = hand.position;
            Vector3 tip = fingertip != null ? fingertip.position : palm;
            drips.transform.position = tip.y < palm.y ? tip : Vector3.Lerp(palm, tip, 0.5f);

            float rate = underWater ? 0f : dripRate * wetness * wetness;
            ParticleSystem.EmissionModule emission = drips.emission;
            emission.rateOverTime = rate;

            if (rate > 0.5f && Time.time >= nextDripSound)
            {
                nextDripSound = Time.time + Random.Range(0.6f, 1.4f) / Mathf.Max(0.3f, wetness);
                Hito1FX.PlayAt("gota_" + Random.Range(1, 4), drips.transform.position + Vector3.down * 0.3f,
                    dripVolume * wetness, 0.15f, 0.2f);
            }
        }
    }
}
