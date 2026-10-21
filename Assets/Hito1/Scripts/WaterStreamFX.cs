using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Chorro de agua del grifo con aspecto y sonido creibles. Reemplaza lo
    // visual del ParticleSystem original (que sigue existiendo porque
    // WaterTap lo enciende y apaga) por:
    //   - un chorro continuo (tubo transparente que se angosta al caer, con
    //     vetas que bajan y una leve ondulacion);
    //   - el chorro se corta donde choca: contra el lavadero, la mano del
    //     paciente o la mano del jugador, y ahi salpica gotas;
    //   - al abrir, el agua tarda en llegar abajo; al cerrar, el ultimo tramo
    //     cae y se corta;
    //   - sonido del chorro en el punto de impacto, mas fuerte y brillante
    //     cuando cae sobre una mano, y el clic de la llave al abrir o cerrar;
    //   - en el lavadero se junta un charco que crece mientras corre el agua
    //     y se va por el desague (con su gorgoteo) al cerrar.
    // Va en el objeto del agua del grifo (Aguadegrifo); busca su WaterTap en
    // los hijos.
    [DefaultExecutionOrder(150)]
    public class WaterStreamFX : MonoBehaviour
    {
        public WaterTap tap;

        [Header("Chorro")]
        [Tooltip("Boca del grifo, en coordenadas locales de este objeto.")]
        public Vector3 nozzleOffset = Vector3.zero;
        public float maxLength = 0.6f;
        public float topRadius = 0.0075f;
        public float bottomRadius = 0.0052f;
        public float scrollSpeed = 1.8f;
        public float wobble = 0.0012f;

        [Header("Manos bajo el chorro")]
        [Tooltip("Partes del cuerpo que cortan el chorro aunque no tengan collider (se agregan solas las manos del paciente).")]
        public List<Transform> extraTargets = new List<Transform>();
        public float extraTargetRadius = 0.045f;

        [Header("Lavadero")]
        public bool sinkPuddle = true;
        public float puddleMaxRadius = 0.11f;
        public float puddleFillSeconds = 6f;
        public float puddleDrainSeconds = 9f;
        public float drainVolume = 0.4f;

        [Header("Sonido")]
        public float streamVolume = 0.55f;
        public float splashVolume = 0.8f;
        public float tapVolume = 0.8f;

        private const int Sides = 10;
        private const int Rings = 14;

        private Mesh mesh;
        private Transform streamTransform;
        private Vector3[] vertices;
        private Vector3[] normals;
        private Vector2[] uvs;
        private Material streamMaterial;
        private ParticleSystem splash;
        private ParticleSystem hotSteam;
        private AudioSource streamSource;
        private AudioSource splashSource;

        private bool wasOpen;
        private float head;     // distancia del borde superior del agua a la boca
        private float front;    // distancia del frente que cae
        private float frontSpeed;
        private float headSpeed;
        private float hitDistance;
        private Vector3 hitNormal = Vector3.up;
        private bool hitOnSkin;
        private Transform hitTarget;
        private Transform puddle;
        private Vector3 puddlePoint;
        private Vector3 puddleNormal = Vector3.up;
        private bool hasPuddlePoint;
        private float puddleAmount;
        private AudioSource drainSource;
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private readonly Dictionary<Transform, Transform> palmKnuckles = new Dictionary<Transform, Transform>();

        public bool IsFlowing { get; private set; }
        public Vector3 ImpactPoint { get; private set; }

        // El chorro cae sobre piel (mano del paciente o del jugador) y que
        // cosa toca (el hueso de la mano o el collider).
        public bool HitOnSkin
        {
            get { return hitOnSkin; }
        }

        public Transform HitTarget
        {
            get { return hitTarget; }
        }

        private Vector3 Nozzle
        {
            get { return transform.TransformPoint(nozzleOffset); }
        }

        private void Awake()
        {
            if (tap == null)
            {
                tap = GetComponentInChildren<WaterTap>(true);
            }

            ParticleSystemRenderer legacy = GetComponent<ParticleSystemRenderer>();
            if (legacy != null)
            {
                legacy.enabled = false;
            }

            BuildStream();
            BuildSplash();
            if (sinkPuddle)
            {
                BuildPuddle();
            }

            GameObject audio = new GameObject("SonidoChorro");
            audio.transform.SetParent(transform, false);
            streamSource = Hito1FX.CreateSource(audio, "agua_chorro_loop", 0f, true, 0.4f, 7f);
            splashSource = Hito1FX.CreateSource(audio, "agua_salpica_loop", 0f, true, 0.4f, 7f);

            hitDistance = maxLength;
        }

        private void Start()
        {
            if (extraTargets.Count == 0)
            {
                foreach (PatientBody body in FindObjectsOfType<PatientBody>())
                {
                    if (body.HandBone(true) != null)
                    {
                        extraTargets.Add(body.HandBone(true));
                    }

                    if (body.HandBone(false) != null)
                    {
                        extraTargets.Add(body.HandBone(false));
                    }
                }
            }

            wasOpen = tap != null && tap.Flowing;
            if (wasOpen)
            {
                front = maxLength;
            }
            else
            {
                head = front = 0f;
            }
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }

            if (streamMaterial != null)
            {
                Destroy(streamMaterial);
            }
        }

        private void Update()
        {
            bool open = tap != null && tap.Flowing;
            if (open != wasOpen)
            {
                wasOpen = open;
                Vector3 tapPos = tap != null ? tap.transform.position : transform.position;
                Hito1FX.PlayAt(open ? "grifo_abrir" : "grifo_cerrar", tapPos, tapVolume);
                if (open)
                {
                    head = 0f;
                    front = 0f;
                    frontSpeed = 0.6f;
                }
                else
                {
                    headSpeed = 0.6f;
                }
            }

            UpdateHit();

            // El agua cae acelerando: el frente al abrir y el corte al cerrar.
            float dt = Time.deltaTime;
            frontSpeed = Mathf.Min(frontSpeed + 9.81f * dt, 4f);
            headSpeed = Mathf.Min(headSpeed + 9.81f * dt, 4f);
            if (open)
            {
                head = 0f;
                front = Mathf.Min(front + frontSpeed * dt, hitDistance);
            }
            else if (head < front)
            {
                head += headSpeed * dt;
                front = Mathf.Min(front + frontSpeed * dt, hitDistance);
            }

            // Si algo se mete en el chorro, el frente se corta ahi mismo.
            front = Mathf.Min(front, hitDistance);
            head = Mathf.Min(head, front);

            bool visible = front - head > 0.002f;
            IsFlowing = visible && front >= hitDistance - 0.01f;
            ImpactPoint = Nozzle + Vector3.down * hitDistance;

            UpdateMesh(visible);
            UpdateSplash();
            UpdatePuddle(dt);
            UpdateSound();
            UpdateHotSteam();
        }

        private void UpdateHit()
        {
            Vector3 origin = Nozzle;
            float best = maxLength;
            Vector3 normal = Vector3.up;
            bool skin = false;
            Transform target = null;

            int count = Physics.SphereCastNonAlloc(origin, topRadius, Vector3.down, hits, maxLength,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance <= 0.0001f || hit.distance >= best)
                {
                    continue;
                }

                best = hit.distance;
                normal = hit.normal;
                skin = IsSkin(hit.collider);
                target = hit.collider.transform;
            }

            // Manos sin collider (los huesos del paciente): se tratan como
            // esferas. La punta de los dedos queda dentro del radio.
            for (int i = 0; i < extraTargets.Count; i++)
            {
                Transform extra = extraTargets[i];
                if (extra == null)
                {
                    continue;
                }

                Vector3 center = PalmCenter(extra);
                Vector3 rel = center - origin;
                float along = -rel.y;
                if (along <= 0f || along >= best + extraTargetRadius)
                {
                    continue;
                }

                float lateral = new Vector2(rel.x, rel.z).magnitude;
                float r = extraTargetRadius + topRadius;
                if (lateral >= r)
                {
                    continue;
                }

                float top = along - Mathf.Sqrt(r * r - lateral * lateral);
                if (top > 0f && top < best)
                {
                    best = top;
                    normal = (origin + Vector3.down * top - center).normalized;
                    skin = true;
                    target = extra;
                }
            }

            hitDistance = best;
            hitNormal = normal;
            hitOnSkin = skin && best < maxLength;
            hitTarget = best < maxLength ? target : null;
        }

        // Centro de la palma: entre la muneca y el nudillo del dedo medio.
        private Vector3 PalmCenter(Transform hand)
        {
            if (!palmKnuckles.TryGetValue(hand, out Transform knuckle))
            {
                knuckle = null;
                foreach (Transform child in hand)
                {
                    if (child.name.Contains("Middle1"))
                    {
                        knuckle = child;
                        break;
                    }
                }

                palmKnuckles[hand] = knuckle;
            }

            return knuckle != null ? Vector3.Lerp(hand.position, knuckle.position, 0.6f) : hand.position;
        }

        private static bool IsSkin(Collider col)
        {
            if (SimpleGrab.IsHandCollider(col))
            {
                return true;
            }

            if (col.GetComponentInParent<PatientBody>() != null)
            {
                return true;
            }

            return col.GetComponentInParent<ManoQuemadaVisibility>() != null;
        }

        // ---------------- malla ----------------

        private void BuildStream()
        {
            GameObject go = new GameObject("ChorroAgua");
            go.transform.SetParent(transform, false);
            streamTransform = go.transform;
            go.AddComponent<NoAutoCollider>();
            MeshFilter filter = go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material source = Hito1FX.Mat("Agua");
            if (source != null)
            {
                streamMaterial = new Material(source);
                renderer.sharedMaterial = streamMaterial;
            }

            mesh = new Mesh { name = "ChorroAgua" };
            mesh.MarkDynamic();
            int vCount = (Sides + 1) * Rings;
            vertices = new Vector3[vCount];
            normals = new Vector3[vCount];
            uvs = new Vector2[vCount];
            int[] tris = new int[Sides * (Rings - 1) * 6];
            int t = 0;
            for (int r = 0; r < Rings - 1; r++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * (Sides + 1) + s;
                    int b = a + Sides + 1;
                    tris[t++] = a;
                    tris[t++] = a + 1;
                    tris[t++] = b;
                    tris[t++] = a + 1;
                    tris[t++] = b + 1;
                    tris[t++] = b;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            filter.sharedMesh = mesh;
        }

        private void UpdateMesh(bool visible)
        {
            Transform meshTransform = streamTransform;
            if (meshTransform == null)
            {
                return;
            }

            meshTransform.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            // La malla se arma en coordenadas del mundo y se pasa a locales
            // (el grifo puede tener rotacion).
            Vector3 nozzle = Nozzle;
            float time = Time.time;
            for (int r = 0; r < Rings; r++)
            {
                float k = r / (float)(Rings - 1);
                float d = Mathf.Lerp(head, front, k);
                float fall = Mathf.Clamp01(d / maxLength);
                float radius = Mathf.Lerp(topRadius, bottomRadius, Mathf.Sqrt(fall));
                if (front >= hitDistance - 0.005f && r == Rings - 1)
                {
                    radius *= 1.35f; // se abre al chocar
                }

                Vector3 center = nozzle + Vector3.down * d;
                float w = wobble * fall;
                center.x += Mathf.Sin(time * 23f + d * 41f) * w;
                center.z += Mathf.Cos(time * 19f + d * 37f) * w;

                for (int s = 0; s <= Sides; s++)
                {
                    float angle = s / (float)Sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    int i = r * (Sides + 1) + s;
                    vertices[i] = meshTransform.InverseTransformPoint(center + n * radius);
                    normals[i] = meshTransform.InverseTransformDirection(n);
                    uvs[i] = new Vector2(s / (float)Sides, d * 2.5f - time * scrollSpeed);
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.RecalculateBounds();
        }

        // ---------------- salpicaduras ----------------

        private void BuildSplash()
        {
            GameObject go = new GameObject("Salpicaduras");
            go.transform.SetParent(transform, false);
            go.AddComponent<NoAutoCollider>();
            splash = go.AddComponent<ParticleSystem>();
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = splash.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.009f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.startColor = new Color(0.9f, 0.96f, 1f, 0.85f);

            ParticleSystem.EmissionModule emission = splash.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = splash.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 65f;
            shape.radius = 0.006f;

            ParticleSystem.SizeOverLifetimeModule size = splash.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.025f;
            renderer.lengthScale = 1.2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material drop = Hito1FX.Mat("Agua_Gota");
            if (drop != null)
            {
                renderer.sharedMaterial = drop;
            }

            splash.Play();
        }

        private void UpdateSplash()
        {
            if (splash == null)
            {
                return;
            }

            Transform t = splash.transform;
            t.position = ImpactPoint + hitNormal * 0.003f;
            t.rotation = Quaternion.LookRotation(hitNormal.sqrMagnitude > 0.5f ? hitNormal : Vector3.up);

            ParticleSystem.EmissionModule emission = splash.emission;
            bool landing = IsFlowing && hitDistance < maxLength;
            emission.rateOverTime = landing ? (hitOnSkin ? 260f : 150f) : 0f;
        }

        // ---------------- charco del lavadero ----------------

        private void BuildPuddle()
        {
            GameObject go = new GameObject("CharcoLavadero");
            go.AddComponent<NoAutoCollider>();
            puddle = go.transform;

            // Borde algo irregular, como el agua que se junta.
            go.AddComponent<MeshFilter>().sharedMesh = Hito1FX.PuddleMesh();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material material = Hito1FX.Mat("Agua_Superficie");
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            go.SetActive(false);
            // Objeto propio: la fuente se mueve al desague.
            GameObject drain = new GameObject("SonidoDesague");
            drain.transform.SetParent(transform, false);
            drainSource = Hito1FX.CreateSource(drain, "desague_loop", 0f, true, 0.3f, 5f);
        }

        private void UpdatePuddle(float dt)
        {
            if (puddle == null)
            {
                return;
            }

            // Solo se junta agua sobre una superficie horizontal (el fondo
            // del lavadero), no sobre una mano.
            bool filling = IsFlowing && hitDistance < maxLength && !hitOnSkin && hitNormal.y > 0.7f;
            if (filling)
            {
                if (!hasPuddlePoint || Vector3.Distance(ImpactPoint, puddlePoint) > 0.15f)
                {
                    puddlePoint = ImpactPoint;
                    puddleNormal = hitNormal;
                    hasPuddlePoint = true;
                }

                puddleAmount = Mathf.MoveTowards(puddleAmount, 1f, dt / Mathf.Max(0.5f, puddleFillSeconds));
            }
            else
            {
                puddleAmount = Mathf.MoveTowards(puddleAmount, 0f, dt / Mathf.Max(0.5f, puddleDrainSeconds));
            }

            bool visible = hasPuddlePoint && puddleAmount > 0.01f;
            puddle.gameObject.SetActive(visible);
            if (visible)
            {
                float radius = Mathf.Lerp(0.025f, puddleMaxRadius, Mathf.Sqrt(puddleAmount));
                radius *= 1f + 0.03f * Mathf.Sin(Time.time * 9f);
                puddle.position = puddlePoint + puddleNormal * 0.0015f;
                puddle.rotation = Quaternion.FromToRotation(Vector3.up, puddleNormal);
                puddle.localScale = new Vector3(radius, 1f, radius);
            }

            if (drainSource != null)
            {
                drainSource.transform.position = hasPuddlePoint ? puddlePoint : transform.position;
            }
        }

        // Con agua caliente sale vapor donde cae el chorro (aviso visual de
        // que esa llave no es la correcta).
        private void UpdateHotSteam()
        {
            float heat = tap != null ? tap.Heat : 0f;
            if (hotSteam == null)
            {
                if (heat < 0.5f)
                {
                    return;
                }

                hotSteam = Hito1FX.CreateSteam("VaporAguaCaliente", null, ImpactPoint, 0.03f, 0f, 50);
            }

            hotSteam.transform.position = ImpactPoint;
            ParticleSystem.EmissionModule emission = hotSteam.emission;
            bool landing = IsFlowing && hitDistance < maxLength;
            emission.rateOverTime = landing ? (heat > 0.9f ? 14f : 0f) : 0f;
        }

        private void UpdateSound()
        {
            Transform audio = streamSource != null ? streamSource.transform : null;
            if (audio != null)
            {
                audio.position = ImpactPoint;
            }

            bool landing = IsFlowing && hitDistance < maxLength;
            float onSkin = landing && hitOnSkin ? 1f : 0f;
            float stream = landing ? (1f - onSkin * 0.6f) : (IsFlowing ? 0.4f : 0f);
            Hito1FX.FadeLoop(streamSource, stream * streamVolume, 3f);
            Hito1FX.FadeLoop(splashSource, onSkin * splashVolume, 3f);
            Hito1FX.FadeLoop(drainSource, puddleAmount * drainVolume, 1f);
        }
    }
}
