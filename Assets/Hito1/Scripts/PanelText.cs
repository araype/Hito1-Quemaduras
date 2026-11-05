using TMPro;
using UnityEngine;

namespace SojaExiles
{
    // Dibuja con TextMesh Pro el texto de un TextMesh. El TextMesh se mantiene
    // (el resto del codigo sigue leyendo y escribiendo .text y .color), pero
    // ya no se dibuja: lo reemplaza un TextMeshPro hijo, nitido a cualquier
    // distancia y con tildes y enes. Liberation Sans mide igual que Arial,
    // asi que los calculos de ancho de VRPanelKit siguen valiendo.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh))]
    public class PanelText : MonoBehaviour
    {
        private static TMP_FontAsset font;

        private TextMesh source;
        private MeshRenderer sourceRenderer;
        private TextMeshPro tmp;
        private MeshRenderer tmpRenderer;

        private string lastText;
        private Color lastColor;
        private float lastSize = -1f;
        private TextAnchor lastAnchor;
        private bool lastVisible;

        public TextMeshPro Tmp
        {
            get { return tmp; }
        }

        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                    if (font == null)
                    {
                        font = TMP_Settings.defaultFontAsset;
                    }
                }

                return font;
            }
        }

        private void Awake()
        {
            Init();
        }

        public void Init()
        {
            if (tmp != null)
            {
                return;
            }

            source = GetComponent<TextMesh>();
            sourceRenderer = GetComponent<MeshRenderer>();

            GameObject go = new GameObject("TextoTMP");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(transform, false);
            tmp = go.AddComponent<TextMeshPro>();
            if (Font != null)
            {
                tmp.font = Font;
            }

            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.rectTransform.sizeDelta = Vector2.zero;
            tmpRenderer = tmp.GetComponent<MeshRenderer>();
            tmpRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tmpRenderer.receiveShadows = false;

            if (sourceRenderer != null)
            {
                // Sigue activo (el codigo lo prende y apaga para mostrar u
                // ocultar), pero no se dibuja.
                sourceRenderer.forceRenderingOff = true;
            }

            Sync(true);
        }

        private void LateUpdate()
        {
            Sync(false);
        }

        private void Sync(bool force)
        {
            if (source == null || tmp == null)
            {
                return;
            }

            string text = source.text ?? "";
            if (force || text != lastText)
            {
                lastText = text;
                tmp.text = text;
            }

            if (force || source.color != lastColor)
            {
                lastColor = source.color;
                tmp.color = lastColor;
            }

            // TextMesh: 10 puntos con characterSize 1 miden una unidad (em).
            // TextMeshPro: el em mide fontSize / 10 unidades.
            float size = source.characterSize * source.fontSize;
            if (force || !Mathf.Approximately(size, lastSize))
            {
                lastSize = size;
                tmp.fontSize = size;
            }

            if (force || source.anchor != lastAnchor)
            {
                lastAnchor = source.anchor;
                ApplyAnchor(lastAnchor);
            }

            bool visible = sourceRenderer == null || sourceRenderer.enabled;
            if (force || visible != lastVisible)
            {
                lastVisible = visible;
                tmpRenderer.enabled = visible;
            }
        }

        private void ApplyAnchor(TextAnchor anchor)
        {
            Vector2 pivot;
            TextAlignmentOptions alignment;
            switch (anchor)
            {
                case TextAnchor.UpperLeft: pivot = new Vector2(0f, 1f); alignment = TextAlignmentOptions.TopLeft; break;
                case TextAnchor.UpperCenter: pivot = new Vector2(0.5f, 1f); alignment = TextAlignmentOptions.Top; break;
                case TextAnchor.UpperRight: pivot = new Vector2(1f, 1f); alignment = TextAlignmentOptions.TopRight; break;
                case TextAnchor.MiddleLeft: pivot = new Vector2(0f, 0.5f); alignment = TextAlignmentOptions.Left; break;
                case TextAnchor.MiddleRight: pivot = new Vector2(1f, 0.5f); alignment = TextAlignmentOptions.Right; break;
                case TextAnchor.LowerLeft: pivot = new Vector2(0f, 0f); alignment = TextAlignmentOptions.BottomLeft; break;
                case TextAnchor.LowerCenter: pivot = new Vector2(0.5f, 0f); alignment = TextAlignmentOptions.Bottom; break;
                case TextAnchor.LowerRight: pivot = new Vector2(1f, 0f); alignment = TextAlignmentOptions.BottomRight; break;
                default: pivot = new Vector2(0.5f, 0.5f); alignment = TextAlignmentOptions.Center; break;
            }

            tmp.rectTransform.pivot = pivot;
            tmp.rectTransform.anchoredPosition = Vector2.zero;
            tmp.rectTransform.localPosition = Vector3.zero;
            tmp.alignment = alignment;
        }
    }
}
