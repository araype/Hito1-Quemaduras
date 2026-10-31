using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Reloj de pared junto al lavadero que muestra el enfriamiento de 20
    // minutos en tiempo acelerado (RF-07), sin pantallas ni textos flotantes:
    //   - el minutero avanza los 20 minutos y el segundero gira rapido
    //     mientras la mano esta bajo el agua fria; si sale del agua, se
    //     detienen (el tiempo no vuelve a cero);
    //   - 20 marcas alrededor de la esfera se encienden en verde, una por
    //     minuto cumplido;
    //   - debajo dice cuantos minutos van.
    // Se cuelga en la pared mas cercana al grifo. Va en el objeto de
    // BurnSequenceManager (lo instala Hito1RuntimeBootstrap).
    public class CoolingClock : MonoBehaviour
    {
        public float diameter = 0.3f;
        public float heightAboveTap = 0.42f;
        [Tooltip("Muestra el texto de minutos debajo del reloj.")]
        public bool showLabel = true;

        private BurnProtocol protocol;
        private Transform root, minuteHand, secondHand;
        private readonly List<Renderer> marks = new List<Renderer>();
        private TextMesh label;
        private Renderer labelBackground;
        private int lastLit = -1;
        private float simulatedSeconds;
        private float lastProgress;

        private static readonly Color MarkOff = new Color(0.55f, 0.58f, 0.62f);
        private static readonly Color MarkOn = new Color(0.25f, 0.85f, 0.45f);

        private IEnumerator Start()
        {
            // Los colliders de las paredes los pone AutoColliders en su Start.
            yield return null;
            Physics.SyncTransforms();
            protocol = BurnProtocol.Instance != null ? BurnProtocol.Instance : FindObjectOfType<BurnProtocol>();
            WaterTap tap = null;
            foreach (WaterTap t in FindObjectsOfType<WaterTap>())
            {
                if (!t.hot)
                {
                    tap = t;
                    break;
                }
            }

            Vector3 near = tap != null ? tap.transform.position : transform.position;
            Build(near);
        }

        private void Build(Vector3 tapPosition)
        {
            Vector3 from = tapPosition + Vector3.up * heightAboveTap;
            Vector3 position, outward;
            if (!FindWall(from, out position, out outward))
            {
                // Sin pared cerca: flota sobre el grifo mirando al centro de
                // la cocina.
                Transform head = VRPanelKit.Head();
                outward = head != null ? PropKit.Flat(head.position - from).normalized : Vector3.back;
                position = from;
                Debug.LogWarning("[CoolingClock] No se encontro una pared junto al grifo; el reloj queda sobre el.");
            }

            GameObject go = new GameObject("RelojEnfriamiento");
            go.AddComponent<NoAutoCollider>();
            root = go.transform;
            root.position = position + outward * 0.025f;
            // El frente (eje -Z local) mira hacia afuera de la pared.
            root.rotation = Quaternion.LookRotation(-outward, Vector3.up);

            float r = diameter * 0.5f;
            PropKit.Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(diameter, 0.005f, diameter),
                Quaternion.Euler(90f, 0f, 0f), "Reloj_Cara", new Color(0.96f, 0.95f, 0.92f));
            PropKit.MeshPart(root, PropKit.Torus("RelojAro", r, 0.012f, 40, 10), Vector3.zero,
                Quaternion.Euler(90f, 0f, 0f), "Acero", new Color(0.7f, 0.72f, 0.75f));

            // Marcas de las horas (12) y, por dentro, las 20 de los minutos
            // de enfriamiento.
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * r * 0.86f + Vector3.back * 0.007f;
                PropKit.Part(root, PrimitiveType.Cube, p, new Vector3(0.006f, i % 3 == 0 ? 0.03f : 0.018f, 0.003f),
                    Quaternion.Euler(0f, 0f, -i * 30f), "Reloj_Aguja", new Color(0.1f, 0.1f, 0.12f));
            }

            for (int i = 0; i < 20; i++)
            {
                // Los 20 minutos ocupan un tercio de la esfera (de las 12 a
                // las 4), como en un reloj real.
                float a = (i + 0.5f) / 60f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * r * 0.7f + Vector3.back * 0.007f;
                Renderer mark = PropKit.Part(root, PrimitiveType.Sphere, p, Vector3.one * 0.012f, "Reloj_Marca", MarkOff);
                IndicatorUtil.SetColor(mark, MarkOff);
                marks.Add(mark);
            }

            minuteHand = Hand("Minutero", r * 0.78f, 0.009f, -0.015f, new Color(0.1f, 0.1f, 0.12f));
            secondHand = Hand("Segundero", r * 0.85f, 0.003f, -0.018f, new Color(0.85f, 0.15f, 0.12f));
            PropKit.Part(root, PrimitiveType.Cylinder, Vector3.back * 0.02f, new Vector3(0.016f, 0.004f, 0.016f),
                Quaternion.Euler(90f, 0f, 0f), "Reloj_Aguja", new Color(0.1f, 0.1f, 0.12f));

            // El texto se arma siempre y se muestra segun showLabel (el modo
            // evaluacion lo apaga despues de empezar).
            label = VRPanelKit.Text(root, "", new Vector2(0f, -r - 0.032f), 0.026f, Color.white, TextAnchor.UpperCenter,
                TextAlignment.Center);
            labelBackground = VRPanelKit.Background(root, new Vector2(0f, -r - 0.058f), new Vector2(0.34f, 0.056f),
                "Panel_Fondo", new Color(0.07f, 0.1f, 0.16f), 0.001f);

            UpdateClock(true);
        }

        private Transform Hand(string name, float length, float width, float z, Color color)
        {
            GameObject pivot = new GameObject(name);
            pivot.AddComponent<NoAutoCollider>();
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(0f, 0f, z);
            PropKit.Part(pivot.transform, PrimitiveType.Cube, new Vector3(0f, length * 0.4f, 0f),
                new Vector3(width, length, 0.003f), "Reloj_Aguja", color);
            return pivot.transform;
        }

        // Pared vertical a menos de 1,5 m del punto, mirando hacia el.
        private static bool FindWall(Vector3 from, out Vector3 position, out Vector3 normal)
        {
            position = from;
            normal = Vector3.back;
            float best = 1.5f;
            bool found = false;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (!Physics.Raycast(from, dir, out RaycastHit hit, best, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (Mathf.Abs(hit.normal.y) > 0.2f || (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    || hit.collider.GetComponentInParent<PatientBody>() != null
                    || hit.collider.GetComponentInParent<OVRCameraRig>() != null)
                {
                    continue;
                }

                best = hit.distance;
                position = hit.point;
                normal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                found = true;
            }

            return found;
        }

        private void Update()
        {
            if (label != null && label.gameObject.activeSelf != showLabel)
            {
                label.gameObject.SetActive(showLabel);
                labelBackground.gameObject.SetActive(showLabel);
            }

            UpdateClock(false);
        }

        // Centro del reloj (para que la guia lo senale).
        public Transform Face
        {
            get { return root; }
        }

        private void UpdateClock(bool force)
        {
            if (root == null)
            {
                return;
            }

            float progress = protocol != null ? protocol.CoolingProgress : 0f;
            bool running = protocol != null && protocol.IsCooling;
            float minutes = progress * (protocol != null ? protocol.simulatedMinutes : 20f);

            // El segundero da una vuelta por cada minuto simulado.
            simulatedSeconds = Mathf.Max(simulatedSeconds, minutes * 60f);
            if (running || progress != lastProgress)
            {
                minuteHand.localRotation = Quaternion.Euler(0f, 0f, -minutes / 60f * 360f);
                secondHand.localRotation = Quaternion.Euler(0f, 0f, -(simulatedSeconds % 60f) * 6f);
                lastProgress = progress;
            }

            int lit = Mathf.FloorToInt(minutes + 1e-3f);
            if (lit != lastLit || force)
            {
                for (int i = 0; i < marks.Count; i++)
                {
                    IndicatorUtil.SetColor(marks[i], i < lit ? MarkOn : MarkOff);
                }

                lastLit = lit;
            }

            if (label != null)
            {
                string text = progress >= 1f ? "20 min listos" : $"{minutes:F0} / 20 min";
                if (!running && progress > 0f && progress < 1f)
                {
                    text += "  (pausa)";
                }

                if (label.text != text)
                {
                    label.text = text;
                    label.color = progress >= 1f ? MarkOn : (running ? Color.white : new Color(1f, 0.75f, 0.4f));
                }
            }
        }
    }
}
