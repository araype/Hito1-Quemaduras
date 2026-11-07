using UnityEngine;

namespace SojaExiles
{
    // Indicacion junto a un objeto de la escena para una accion que se
    // sostiene: una etiqueta corta con el estilo de las tarjetas ("Manten
    // encima de la quemadura") y un circulo, mirando al jugador, que se llena
    // con el progreso. Se ve antes de empezar (circulo vacio), asi queda claro
    // que hay que sostener. La usa CoverItem (film y gasa).
    public class HoldHint
    {
        private readonly Transform label;
        private readonly TextMesh text;
        private readonly LineRenderer track, arc;
        private readonly float radius;
        private Transform head;

        private static readonly Color Empty = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color Full = new Color(0.35f, 0.95f, 0.45f, 0.95f);

        public HoldHint(string name, float ringRadius)
        {
            radius = ringRadius;
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            go.AddComponent<OverlayPanel>();
            VRPanelKit.CardPanel(go.transform, Vector2.zero, new Vector2(0.2f, 0.056f));
            text = VRPanelKit.Text(go.transform, "", new Vector2(0f, 0.017f), 0.017f, Color.white,
                TextAnchor.UpperCenter, TextAlignment.Center);
            label = go.transform;
            go.SetActive(false);
            track = Line(name + "Fondo", Empty, 0.004f);
            arc = Line(name + "Progreso", Full, 0.007f);
        }

        private static LineRenderer Line(string name, Color color, float width)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 0;
            line.enabled = false;
            return line;
        }

        // 'words' en dos lineas como mucho; 'progress' de 0 a 1.
        public void Show(Vector3 at, string words, float progress)
        {
            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            Vector3 toHead = head != null ? (head.position - at).normalized : Vector3.up;
            if (!label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(true);
            }

            if (text.text != words)
            {
                text.text = words;
            }

            Vector3 pos = at + Vector3.up * 0.09f + toHead * 0.03f;
            label.position = pos;
            if (head != null)
            {
                label.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
            }

            Vector3 side = Vector3.Cross(Vector3.up, toHead);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.Cross(Vector3.forward, toHead);
            }

            side.Normalize();
            Vector3 up = Vector3.Cross(toHead, side).normalized;
            Vector3 center = at + toHead * 0.01f;
            track.enabled = true;
            SetArc(track, center, side, up, 1f);
            arc.enabled = progress > 0.001f;
            Color c = Color.Lerp(new Color(1f, 0.78f, 0.15f, 0.95f), Full, progress);
            arc.startColor = c;
            arc.endColor = c;
            SetArc(arc, center, side, up, Mathf.Clamp01(progress));
        }

        public void Hide()
        {
            if (label != null && label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(false);
            }

            if (track != null)
            {
                track.enabled = false;
            }

            if (arc != null)
            {
                arc.enabled = false;
            }
        }

        public void Destroy()
        {
            foreach (Component c in new Component[] { label, track, arc })
            {
                if (c != null)
                {
                    Object.Destroy(c.gameObject);
                }
            }
        }

        private void SetArc(LineRenderer line, Vector3 center, Vector3 side, Vector3 up, float fraction)
        {
            const int segments = 40;
            int count = Mathf.Max(2, Mathf.CeilToInt(segments * fraction) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float a = fraction * (i / (float)(count - 1)) * Mathf.PI * 2f;
                line.SetPosition(i, center + (up * Mathf.Cos(a) + side * Mathf.Sin(a)) * radius);
            }
        }
    }
}
