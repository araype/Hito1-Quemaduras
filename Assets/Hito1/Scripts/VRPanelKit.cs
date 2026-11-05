using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SojaExiles
{
    // Paneles de texto en el mundo (decision medica, resumen) sin TextMesh Pro
    // ni Canvas: un fondo plano, textos con TextMesh y botones que se tocan
    // con la mano (TouchButton). El eje Z del panel apunta en contra del
    // jugador: el frente (texto y fondo) mira hacia el.
    public static class VRPanelKit
    {
        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return font;
            }
        }

        // Cabeza del jugador (camara del visor).
        public static Transform Head()
        {
            OVRCameraRig rig = Object.FindObjectOfType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null)
            {
                return rig.centerEyeAnchor;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        // Raiz del panel frente al jugador, a la distancia y altura dadas
        // (relativas a los ojos), girada solo en horizontal para mirarlo.
        public static Transform CreateRoot(string name, float distance, float drop)
        {
            Transform head = Head();
            Vector3 eye = head != null ? head.position : new Vector3(0f, 1.6f, 0f);
            Vector3 forward = head != null ? Vector3.ProjectOnPlane(head.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            go.transform.position = eye + forward * distance + Vector3.down * drop;
            go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            return go.transform;
        }

        // Fondo rectangular centrado en (center), del tamano dado.
        public static Renderer Background(Transform parent, Vector2 center, Vector2 size, string materialName,
            Color fallback, float depth = 0f)
        {
            Renderer quad = PropKit.Part(parent, PrimitiveType.Quad, new Vector3(center.x, center.y, depth),
                new Vector3(size.x, size.y, 1f), materialName, fallback);
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.receiveShadows = false;
            return quad;
        }

        // Estilo comun de las tarjetas (narrador, inicio, decision medica y
        // resumen): fondo oscuro translucido, franja azul a la izquierda,
        // etiqueta celeste arriba, texto principal blanco y pistas en gris
        // azulado.
        public static readonly Color CardColor = new Color(0.07f, 0.1f, 0.16f);
        public static readonly Color CardLabelColor = new Color(0.55f, 0.78f, 1f);
        public static readonly Color CardHintColor = new Color(0.8f, 0.85f, 0.92f);
        public static readonly Color CardWarningColor = new Color(1f, 0.6f, 0.45f);
        public const float CardAlpha = 0.92f;
        public const float CardAccentWidth = 0.012f;

        // Fondo de tarjeta centrado en (center) con su franja de color en el
        // borde izquierdo.
        public static Renderer CardBackground(Transform parent, Vector2 center, Vector2 size, out Renderer accent)
        {
            Renderer background = Background(parent, center, size, "Panel_Fondo_Translucido", CardColor, 0.001f);
            IndicatorUtil.SetColor(background, new Color(CardColor.r, CardColor.g, CardColor.b, CardAlpha));
            float accentX = center.x - size.x * 0.5f + CardAccentWidth * 0.5f;
            accent = Background(parent, new Vector2(accentX, center.y), new Vector2(CardAccentWidth, size.y),
                "Panel_Boton", new Color(0.25f, 0.6f, 1f), 0f);
            return background;
        }

        public static Renderer CardBackground(Transform parent, Vector2 center, Vector2 size)
        {
            return CardBackground(parent, center, size, out Renderer _);
        }

        // Texto con alto de linea aproximado 'lineHeight' (m). TextMesh: con
        // characterSize 1, 10 puntos de fuente miden 1 unidad.
        public static TextMesh Text(Transform parent, string text, Vector2 position, float lineHeight, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, TextAlignment alignment = TextAlignment.Left)
        {
            GameObject go = new GameObject("Texto");
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, -0.003f);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.font = Font;
            mesh.fontSize = 64;
            mesh.characterSize = lineHeight * 10f / 64f;
            mesh.anchor = anchor;
            mesh.alignment = alignment;
            mesh.color = color;
            mesh.richText = true;
            mesh.text = text;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        public static TouchButton Button(Transform parent, string label, Vector2 center, Vector2 size, Color color,
            System.Action onPressed)
        {
            GameObject go = new GameObject("Boton_" + label);
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, center.y, -0.004f);

            // Zona de toque un poco mas honda que el boton, hacia el jugador.
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, size.y, 0.08f);
            box.center = new Vector3(0f, 0f, -0.03f);

            Renderer background = Background(go.transform, Vector2.zero, size, "Panel_Boton", color);
            FitLabel(label, size, out string text, out float lineHeight);
            Text(go.transform, text, Vector2.zero, lineHeight, Color.white, TextAnchor.MiddleCenter,
                TextAlignment.Center);

            TouchButton button = go.AddComponent<TouchButton>();
            button.background = background;
            button.idleColor = color;
            button.pressedColor = Color.Lerp(color, Color.white, 0.45f);
            button.OnPressed = onPressed;
            return button;
        }

        // Ancho aproximado de un caracter de la fuente integrada, en
        // fracciones del alto de linea.
        private const float CharWidth = 0.5f;

        // Que la etiqueta quepa en el boton: si en una linea queda ancha, se
        // parte en dos; si aun asi no cabe, se achica la letra.
        private static void FitLabel(string label, Vector2 size, out string text, out float lineHeight)
        {
            float usable = size.x * 0.88f;
            lineHeight = size.y * 0.32f;
            text = label;
            if (label.Length * lineHeight * CharWidth <= usable)
            {
                return;
            }

            text = SplitInTwo(label);
            int longest = 0;
            int lines = 0;
            foreach (string line in text.Split('\n'))
            {
                longest = Mathf.Max(longest, line.Length);
                lines++;
            }

            lineHeight = Mathf.Min(size.y * 0.8f / Mathf.Max(1, lines), size.y * 0.3f);
            lineHeight = Mathf.Min(lineHeight, usable / Mathf.Max(1, longest * CharWidth));
        }

        // Parte la etiqueta en dos lineas lo mas parejas posible, en un
        // espacio. Antes se cortaba con Wrap a la mitad del largo y "Ir al
        // centro de salud" quedaba en tres lineas apretadas.
        private static string SplitInTwo(string label)
        {
            int best = -1;
            int bestLongest = int.MaxValue;
            for (int i = 0; i < label.Length; i++)
            {
                if (label[i] != ' ')
                {
                    continue;
                }

                int longest = Mathf.Max(i, label.Length - i - 1);
                if (longest < bestLongest)
                {
                    bestLongest = longest;
                    best = i;
                }
            }

            return best < 0 ? label : label.Substring(0, best) + "\n" + label.Substring(best + 1);
        }

        // Corta el texto en lineas de hasta 'width' caracteres (TextMesh no
        // ajusta solo).
        public static string Wrap(string text, int width)
        {
            StringBuilder result = new StringBuilder();
            foreach (string paragraph in text.Split('\n'))
            {
                int line = 0;
                foreach (string word in paragraph.Split(' '))
                {
                    if (line > 0 && line + 1 + word.Length > width)
                    {
                        result.Append('\n');
                        line = 0;
                    }
                    else if (line > 0)
                    {
                        result.Append(' ');
                        line++;
                    }

                    result.Append(word);
                    line += word.Length;
                }

                result.Append('\n');
            }

            return result.ToString().TrimEnd('\n');
        }

        public static string Join(List<string> lines)
        {
            return string.Join("\n", lines);
        }
    }
}
