using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // El accidente que abre la escena: la paciente esta cocinando y, sin
    // aviso, la olla de agua hirviendo se vuelca hacia su mano. El agua cae
    // sobre la mano, se derrama por el meson y gotea al suelo, y sale una
    // nube de vapor. En ese momento la mano se quema (BurnSequenceManager).
    // Va en la olla (el objeto con PotFX). Reemplaza el retraso fijo con que
    // antes se quemaba la mano.
    [RequireComponent(typeof(PotFX))]
    public class PotAccident : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Segundos de cocina tranquila antes del accidente.")]
        public float accidentDelay = 6f;
        public float tipAngle = 90f;
        public float tipSeconds = 0.55f;

        [Header("Agua derramada")]
        public float counterPuddleRadius = 0.24f;
        public float floorPuddleRadius = 0.42f;
        [Tooltip("Segundos que el agua derramada sigue echando vapor.")]
        public float steamSeconds = 30f;
        public float pourVolume = 0.9f;

        private PotFX pot;
        private Rigidbody body;
        private ParticleSystem pour;

        private void Awake()
        {
            pot = GetComponent<PotFX>();
            body = GetComponent<Rigidbody>();

            // Antes del Start de BurnSequenceManager: la mano se quema con el
            // accidente, no con el retraso fijo.
            BurnSequenceManager burn = FindObjectOfType<BurnSequenceManager>();
            if (burn != null)
            {
                burn.triggerBurnAfterDelay = false;
            }
        }

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            StartCoroutine(Accident());
        }

        private IEnumerator Accident()
        {
            yield return new WaitForSeconds(accidentDelay);

            // Si el jugador ya tiene la olla en la mano, no se vuelca: la
            // paciente se quema igual al tocarla.
            if (body != null && SimpleGrab.IsHeldObject(body))
            {
                Burn();
                yield break;
            }

            // Se vuelca hacia la paciente (su mano esta junto a la olla).
            PatientBody patient = FindObjectOfType<PatientBody>();
            Transform hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            Vector3 bottom = pot.BottomCenter;
            Vector3 toPatient = patient != null ? PropKit.Flat(patient.transform.position - bottom)
                : -PropKit.Flat(transform.forward);
            Vector3 dir = toPatient.sqrMagnitude > 1e-4f ? toPatient.normalized : Vector3.forward;
            Vector3 pivot = bottom + dir * pot.Radius;
            Vector3 axis = Vector3.Cross(Vector3.up, dir);

            bool wasKinematic = body != null && body.isKinematic;
            if (body != null)
            {
                body.isKinematic = true;
            }

            Hito1FX.PlayAt("agua_derrame", pot.BottomCenter + Vector3.up * pot.Height, pourVolume, 0.02f, 0.6f);

            // Se vuelca acelerando, como al caer.
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            bool pouring = false, burned = false;
            float t = 0f;
            while (t < tipSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / tipSeconds);
                float angle = tipAngle * k * k;
                Quaternion q = Quaternion.AngleAxis(angle, axis);
                Vector3 position = pivot + q * (startPosition - pivot);
                if (body != null)
                {
                    body.MovePosition(position);
                    body.MoveRotation(q * startRotation);
                }
                else
                {
                    transform.SetPositionAndRotation(position, q * startRotation);
                }

                if (!pouring && angle > 45f)
                {
                    pouring = true;
                    pot.Spill();
                    StartPour(pivot + Vector3.up * pot.Height * 0.9f, dir);
                }

                if (!burned && angle > 70f)
                {
                    burned = true;
                    Burn();
                }

                yield return null;
            }

            if (!burned)
            {
                Burn();
            }

            if (body != null)
            {
                body.isKinematic = wasKinematic;
                if (!body.isKinematic)
                {
                    body.velocity = dir * 0.15f;
                    body.angularVelocity = Vector3.zero;
                }
            }

            // El golpe de la olla y el grito los pone BurnPatientReactions.
            StartCoroutine(Spread(pivot, dir, hand));
        }

        private void Burn()
        {
            if (burnSequence == null)
            {
                return;
            }

            burnSequence.TriggerBurn();
            burnSequence.Report(BurnSequenceManager.BurnEvent.PotSpilled);
        }

        // Chorro de agua hirviendo que sale por el borde hacia la mano.
        private void StartPour(Vector3 rim, Vector3 dir)
        {
            GameObject go = new GameObject("DerrameOlla");
            go.AddComponent<NoAutoCollider>();
            go.transform.position = rim;
            go.transform.rotation = Quaternion.LookRotation((dir + Vector3.down * 0.4f).normalized);

            pour = go.AddComponent<ParticleSystem>();
            pour.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = pour.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.9f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = pour.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 450f, 1f, 60f));

            ParticleSystem.ShapeModule shape = pour.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = pot.Radius * 0.6f;

            ParticleSystem.CollisionModule collision = pour.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.dampen = 0.6f;
            collision.bounce = 0.15f;
            collision.lifetimeLoss = 0.4f;
            collision.quality = ParticleSystemCollisionQuality.Medium;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material drop = Hito1FX.Mat("Agua_Gota");
            if (drop != null)
            {
                renderer.sharedMaterial = drop;
            }

            pour.Play();
            Destroy(go, 3f);
        }

        // El agua se extiende por el meson, cae al suelo y echa vapor.
        private IEnumerator Spread(Vector3 pivot, Vector3 dir, Transform hand)
        {
            // Nube de vapor sobre la mano al quemarse.
            Vector3 burst = hand != null ? hand.position : pivot + dir * 0.1f;
            ParticleSystem cloud = Hito1FX.CreateSteam("VaporQuemadura", null, burst, 0.06f, 40f, 120);
            Destroy(cloud.gameObject, 6f);
            StartCoroutine(FadeSteam(cloud, 2.5f, 0f));

            Puddle counter = null, floor = null;
            if (GroundHit(pivot + dir * 0.12f + Vector3.up * 0.2f, 0.5f, out RaycastHit top))
            {
                counter = new Puddle(top.point, top.normal, counterPuddleRadius, 1);
            }

            // Borde del meson: el primer punto en esa direccion donde la
            // superficie baja mucho (el suelo).
            float counterHeight = counter != null ? counter.Point.y : pivot.y;
            for (float d = 0.15f; d < 1f; d += 0.05f)
            {
                Vector3 probe = pivot + dir * d + Vector3.up * 0.1f;
                if (GroundHit(probe, 2.5f, out RaycastHit hit) && counterHeight - hit.point.y > 0.4f
                    && hit.normal.y > 0.8f)
                {
                    floor = new Puddle(hit.point + dir * 0.1f, hit.normal, floorPuddleRadius, 2);
                    break;
                }
            }

            float elapsed = 0f;
            while (elapsed < steamSeconds)
            {
                elapsed += Time.deltaTime;
                counter?.Update(Mathf.Clamp01(elapsed / 1.2f), elapsed, steamSeconds);
                floor?.Update(Mathf.Clamp01((elapsed - 0.6f) / 2.5f), elapsed, steamSeconds);
                yield return null;
            }

            counter?.StopSteam();
            floor?.StopSteam();
        }

        // Primera superficie fija hacia abajo: ignora la olla, la paciente
        // (su cuerpo fisico), el jugador y los objetos sueltos.
        private bool GroundHit(Vector3 from, float distance, out RaycastHit result)
        {
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider col = hit.collider;
                if (col.transform.IsChildOf(transform) || col is CharacterController
                    || col.GetComponentInParent<PatientBody>() != null || col.GetComponentInParent<OVRCameraRig>() != null
                    || (hit.rigidbody != null && !hit.rigidbody.isKinematic) || SimpleGrab.IsHandCollider(col))
                {
                    continue;
                }

                result = hit;
                return true;
            }

            result = default;
            return false;
        }

        private static IEnumerator FadeSteam(ParticleSystem steam, float seconds, float to)
        {
            ParticleSystem.EmissionModule emission = steam.emission;
            float from = emission.rateOverTime.constant;
            float t = 0f;
            while (t < seconds && steam != null)
            {
                t += Time.deltaTime;
                emission.rateOverTime = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
        }

        // Charco de agua caliente: crece, queda y echa vapor que se apaga.
        private class Puddle
        {
            public Vector3 Point;
            private readonly Transform transform;
            private readonly ParticleSystem steam;
            private readonly float radius;

            public Puddle(Vector3 point, Vector3 normal, float radius, int seed)
            {
                Point = point;
                this.radius = radius;
                GameObject go = new GameObject("CharcoDerrame");
                go.AddComponent<NoAutoCollider>();
                transform = go.transform;
                transform.position = point + normal * 0.002f;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, normal)
                    * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
                transform.localScale = Vector3.zero;
                go.AddComponent<MeshFilter>().sharedMesh = Hito1FX.PuddleMesh(seed);
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Material material = Hito1FX.Mat("Agua_Superficie");
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }

                // Sin padre: el charco se escala al crecer.
                steam = Hito1FX.CreateSteam("VaporCharco", null, point, radius * 0.6f, 0f);
            }

            public void Update(float grow, float elapsed, float steamSeconds)
            {
                float r = radius * Mathf.Sqrt(grow);
                transform.localScale = new Vector3(r, 1f, r);
                ParticleSystem.EmissionModule emission = steam.emission;
                emission.rateOverTime = grow > 0f ? 9f * (1f - elapsed / steamSeconds) : 0f;
            }

            public void StopSteam()
            {
                if (steam != null)
                {
                    steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
