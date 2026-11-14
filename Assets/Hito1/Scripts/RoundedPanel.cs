using UnityEngine;

namespace SojaExiles
{
    // Fondo redondeado de las tarjetas (shader Hito1/PanelRedondeado): un
    // quad que cubre el panel mas el margen del brillo. El tamano, los
    // colores y la transparencia se pasan por MaterialPropertyBlock, asi que
    // cada panel o puntito puede tener los suyos sin crear materiales.
    // Las medidas van en unidades locales del padre (la tarjeta).
    [RequireComponent(typeof(MeshRenderer))]
    public class RoundedPanel : MonoBehaviour
    {
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int Color2Id = Shader.PropertyToID("_Color2");
        private static readonly int BorderAId = Shader.PropertyToID("_BorderA");
        private static readonly int BorderBId = Shader.PropertyToID("_BorderB");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int BorderId = Shader.PropertyToID("_Border");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int SheenId = Shader.PropertyToID("_Sheen");

        public float radius = 0.022f;
        public float border = 0.0018f;
        public float glow = 0.012f;
        public float glowStrength = 0.45f;
        public float sheen = 0.07f;
        public Color top = new Color(0.1f, 0.14f, 0.24f, 0.9f);
        public Color bottom = new Color(0.05f, 0.07f, 0.14f, 0.9f);
        public Color borderLeft = new Color(0.4f, 0.78f, 1f, 0.95f);
        public Color borderRight = new Color(0.68f, 0.5f, 1f, 0.95f);

        public Vector2 Size { get; private set; }

        private Renderer rend;
        private MaterialPropertyBlock block;
        private float fillAlpha = 1f;

        public static RoundedPanel Create(Transform parent, string name, Vector2 center, Vector2 size, float depth,
            float radius, float border, float glow)
        {
            Renderer quad = PropKit.Part(parent, PrimitiveType.Quad, new Vector3(center.x, center.y, depth),
                Vector3.one, "Panel_Redondeado", new Color(0.07f, 0.1f, 0.16f, 0.9f));
            quad.name = name;
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quad.receiveShadows = false;
            RoundedPanel panel = quad.gameObject.AddComponent<RoundedPanel>();
            panel.radius = radius;
            panel.border = border;
            panel.glow = glow;
            panel.SetRect(center, size);
            return panel;
        }

        public Renderer Renderer
        {
            get
            {
                if (rend == null)
                {
                    rend = GetComponent<Renderer>();
                }

                return rend;
            }
        }

        // Centro y tamano del panel visible (sin contar el brillo).
        public void SetRect(Vector2 center, Vector2 size)
        {
            Size = size;
            Transform t = transform;
            t.localPosition = new Vector3(center.x, center.y, t.localPosition.z);
            t.localScale = new Vector3(size.x + glow * 2f, size.y + glow * 2f, 1f);
            Push();
        }

        // Transparencia del relleno (el borde y el brillo se atenuan con el).
        public void SetFillAlpha(float alpha)
        {
            if (Mathf.Approximately(alpha, fillAlpha))
            {
                return;
            }

            fillAlpha = alpha;
            Push();
        }

        // Un solo color plano (puntitos de progreso).
        public void SetSolid(Color color, float glowAmount)
        {
            top = color;
            bottom = color;
            borderLeft = color;
            borderRight = color;
            glowStrength = glowAmount;
            Push();
        }

        public void Push()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }

            Renderer.GetPropertyBlock(block);
            Vector3 s = transform.localScale;
            block.SetVector(SizeId, new Vector4(s.x, s.y, 0f, 0f));
            block.SetColor(ColorId, Fade(top, fillAlpha));
            block.SetColor(Color2Id, Fade(bottom, fillAlpha));
            float edgeFade = Mathf.Lerp(0.55f, 1f, fillAlpha);
            block.SetColor(BorderAId, Fade(borderLeft, edgeFade));
            block.SetColor(BorderBId, Fade(borderRight, edgeFade));
            block.SetFloat(RadiusId, radius);
            block.SetFloat(BorderId, border);
            block.SetFloat(GlowId, glow);
            block.SetFloat(GlowStrengthId, glowStrength * edgeFade);
            block.SetFloat(SheenId, sheen);
            Renderer.SetPropertyBlock(block);
        }

        private static Color Fade(Color c, float alpha)
        {
            c.a *= alpha;
            return c;
        }
    }
}
