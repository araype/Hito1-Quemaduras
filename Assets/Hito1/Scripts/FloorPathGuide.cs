using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Flechas tenues en el piso que marcan el camino desde el jugador hasta
    // un objetivo (en el modo guiado: hasta la manta del sofa de la sala y,
    // con la manta en la mano, de vuelta a la paciente). El camino es el
    // mismo por el que PlayArea deja caminar al jugador, asi lo que se ve
    // coincide con por donde se puede ir. Una onda de brillo recorre las
    // flechas hacia el objetivo para que se lea la direccion de un vistazo.
    // Lo maneja BurnGuide con SetTarget / Hide.
    public class FloorPathGuide : MonoBehaviour
    {
        [Tooltip("Distancia entre flechas (m).")]
        public float spacing = 0.45f;
        [Tooltip("Las flechas empiezan a esta distancia del jugador (m).")]
        public float startOffset = 0.6f;
        [Tooltip("Sin flechas si el objetivo esta a menos de esto por el camino (m).")]
        public float hideWithin = 1.3f;
        // Azul mas saturado que el brillo de la guia: se lee tanto en el piso
        // de madera como en la alfombra clara de la sala.
        public Color color = new Color(0.1f, 0.55f, 1f, 1f);
        public float minAlpha = 0.45f;
        public float maxAlpha = 0.9f;
        [Tooltip("Velocidad de la onda de brillo que avanza hacia el objetivo (m/s).")]
        public float waveSpeed = 1.6f;

        private const int MaxArrows = 48;
        private const float Recompute = 0.4f;

        private static FloorPathGuide instance;
        private static Mesh arrowMesh;

        private readonly List<Vector3> corners = new List<Vector3>();
        private readonly List<Renderer> arrows = new List<Renderer>();
        private readonly List<float> arrowDistance = new List<float>();
        private MaterialPropertyBlock block;
        private Material material;
        private bool hasTarget;
        private Vector3 target;
        private float reach;
        private float nextRecompute;
        private Vector3 lastFrom;
        private Vector3 lastTarget;
        private Transform head;

        public static FloorPathGuide Ensure()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("CaminoEnElPiso");
                go.AddComponent<NoAutoCollider>();
                instance = go.AddComponent<FloorPathGuide>();
            }

            return instance;
        }

        // Muestra el camino hasta 'point'; 'stopAt' es a cuanto del objetivo
        // termina (la manta esta sobre el sofa, no se puede llegar encima).
        public void SetTarget(Vector3 point, float stopAt)
        {
            if (!hasTarget || (point - target).sqrMagnitude > 0.09f)
            {
                nextRecompute = 0f;
            }

            hasTarget = true;
            target = point;
            reach = stopAt;
        }

        public void Hide()
        {
            if (!hasTarget)
            {
                return;
            }

            hasTarget = false;
            corners.Clear();
            for (int i = 0; i < arrows.Count; i++)
            {
                arrows[i].enabled = false;
            }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            material = new Material(Shader.Find("Sprites/Default"));
            // Sobre el piso y la alfombra, antes de los paneles.
            material.renderQueue = 3001;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (material != null)
            {
                Destroy(material);
            }
        }

        private void Update()
        {
            if (!hasTarget)
            {
                return;
            }

            if (head == null)
            {
                OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
                head = rig != null ? rig.centerEyeAnchor : (Camera.main != null ? Camera.main.transform : null);
                if (head == null)
                {
                    return;
                }
            }

            Vector3 from = new Vector3(head.position.x, 0f, head.position.z);
            float now = Time.time;
            if (now >= nextRecompute || (from - lastFrom).sqrMagnitude > 0.09f
                || (target - lastTarget).sqrMagnitude > 0.09f)
            {
                nextRecompute = now + Recompute;
                lastFrom = from;
                lastTarget = target;
                Rebuild(from);
            }

            float wave = now * waveSpeed;
            for (int i = 0; i < arrows.Count; i++)
            {
                if (!arrows[i].enabled)
                {
                    continue;
                }

                // Una cresta cada 2,5 m que avanza hacia el objetivo.
                float phase = Mathf.Repeat(arrowDistance[i] - wave, 2.5f) / 2.5f;
                float crest = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(phase * Mathf.PI * 2f)), 6f);
                Color c = color;
                c.a = Mathf.Lerp(minAlpha, maxAlpha, crest);
                block.SetColor("_Color", c);
                arrows[i].SetPropertyBlock(block);
            }
        }

        private void Rebuild(Vector3 from)
        {
            if (!PlayArea.FindPath(from, target, reach, corners))
            {
                SetVisible(0);
                return;
            }

            float total = 0f;
            for (int i = 1; i < corners.Count; i++)
            {
                total += Vector3.Distance(corners[i - 1], corners[i]);
            }

            if (total < hideWithin)
            {
                SetVisible(0);
                return;
            }

            int count = 0;
            float along = 0f;
            float next = startOffset;
            for (int i = 1; i < corners.Count && count < MaxArrows; i++)
            {
                Vector3 a = corners[i - 1], b = corners[i];
                float length = Vector3.Distance(a, b);
                Vector3 dir = length > 1e-4f ? (b - a) / length : Vector3.forward;
                while (next <= along + length && count < MaxArrows)
                {
                    Vector3 p = a + dir * (next - along);
                    Renderer arrow = Arrow(count);
                    arrow.transform.SetPositionAndRotation(OnFloor(p), Quaternion.LookRotation(dir, Vector3.up));
                    arrow.enabled = true;
                    arrowDistance[count] = next;
                    count++;
                    next += spacing;
                }

                along += length;
            }

            SetVisible(count);
        }

        private void SetVisible(int count)
        {
            for (int i = count; i < arrows.Count; i++)
            {
                arrows[i].enabled = false;
            }
        }

        // Altura del piso (o la alfombra) en ese punto, sin contar objetos
        // sueltos, al jugador ni a la paciente.
        private static Vector3 OnFloor(Vector3 p)
        {
            float y = p.y;
            float best = float.MinValue;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(p.x, p.y + 0.6f, p.z), Vector3.down, 1.2f, ~0,
                         QueryTriggerInteraction.Ignore))
            {
                Collider c = hit.collider;
                if (c is CharacterController || c.attachedRigidbody != null
                    || c.GetComponentInParent<PatientBody>() != null)
                {
                    continue;
                }

                if (hit.point.y > best && hit.point.y < p.y + 0.2f)
                {
                    best = hit.point.y;
                }
            }

            if (best > float.MinValue)
            {
                y = best;
            }

            return new Vector3(p.x, y + 0.012f, p.z);
        }

        private Renderer Arrow(int index)
        {
            while (arrows.Count <= index)
            {
                GameObject go = new GameObject("Flecha");
                go.AddComponent<NoAutoCollider>();
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                arrows.Add(r);
                arrowDistance.Add(0f);
            }

            return arrows[index];
        }

        // Flecha en "V" plana sobre el piso, apuntando a +Z.
        private static Mesh ArrowMesh()
        {
            if (arrowMesh != null)
            {
                return arrowMesh;
            }

            const float halfWidth = 0.15f, depth = 0.14f, thick = 0.05f;
            Vector3 tip = new Vector3(0f, 0f, depth * 0.5f);
            Vector3 tipBack = tip - new Vector3(0f, 0f, thick * 1.4f);
            Vector3 leftOuter = new Vector3(-halfWidth, 0f, -depth * 0.5f);
            Vector3 rightOuter = new Vector3(halfWidth, 0f, -depth * 0.5f);
            Vector3 leftInner = new Vector3(leftOuter.x + thick * 0.7f, 0f, leftOuter.z - thick);
            Vector3 rightInner = new Vector3(rightOuter.x - thick * 0.7f, 0f, rightOuter.z - thick);

            arrowMesh = new Mesh { name = "FlechaPiso" };
            arrowMesh.vertices = new[] { tip, rightOuter, rightInner, tipBack, leftInner, leftOuter };
            // Hacia arriba (se ve desde encima).
            arrowMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 };
            arrowMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            arrowMesh.RecalculateBounds();
            return arrowMesh;
        }
    }
}
