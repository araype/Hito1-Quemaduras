using UnityEngine;

namespace SojaExiles
{
    // Crea y colorea indicadores visuales simples (esferas translucidas):
    // el punto dorado del grifo, la zona del abdomen en la maniobra de
    // Heimlich, etc. No llevan collider (el jugador los atraviesa) y estan
    // marcados con NoAutoCollider para que AutoColliders no les agregue uno.
    public static class IndicatorUtil
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static Renderer CreateSphere(string name, Transform parent, Vector3 worldPosition, float radius,
            Material material, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            SetWorldDiameter(go.transform, radius * 2f);

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            SetColor(renderer, color);
            return renderer;
        }

        // Escala local tal que el tamano en el mundo sea el pedido aunque el
        // padre tenga escala.
        public static void SetWorldDiameter(Transform t, float diameter)
        {
            Vector3 parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(
                diameter / Mathf.Max(1e-5f, Mathf.Abs(parentScale.x)),
                diameter / Mathf.Max(1e-5f, Mathf.Abs(parentScale.y)),
                diameter / Mathf.Max(1e-5f, Mathf.Abs(parentScale.z)));
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetColor(EmissionId, new Color(color.r, color.g, color.b) * 0.6f);
            renderer.SetPropertyBlock(block);
        }
    }
}
