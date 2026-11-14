using UnityEngine;

namespace SojaExiles
{
    // Cursor de la punta del dedo sobre un panel con botones: un circulo que
    // aparece sobre la tarjeta cuando el indice se acerca a un boton, se
    // achica al acercarse y se pinta celeste cuando ya esta dentro del boton.
    // Los paneles se dibujan encima de todo (OverlayPanel) y tapaban la mano:
    // sin esto no se sabia si el dedo estaba tocando un boton.
    // Lo agrega TouchButton a la raiz de su panel; no hace falta ponerlo a mano.
    public class PanelFingerCursor : MonoBehaviour
    {
        [Tooltip("Distancia delante del panel desde la que aparece el cursor (m).")]
        public float showDistance = 0.2f;
        [Tooltip("Cuanto puede pasar el dedo por detras del panel y seguir viendose (m).")]
        public float behindDistance = 0.04f;
        [Tooltip("Margen alrededor de los botones donde se muestra el cursor (m).")]
        public float buttonMargin = 0.08f;
        [Tooltip("Diametro del cursor con el dedo tocando el panel y lejos de el (m).")]
        public float nearDiameter = 0.012f;
        public float farDiameter = 0.036f;
        public Color idleColor = new Color(1f, 1f, 1f, 0.8f);
        public Color hoverColor = new Color(0.45f, 0.85f, 1f, 1f);

        private readonly RoundedPanel[] dots = new RoundedPanel[2];
        private TouchButton[] buttons = new TouchButton[0];
        private float nextScan;

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                buttons = GetComponentsInChildren<TouchButton>();
            }

            for (int h = 0; h < 2; h++)
            {
                UpdateDot(h);
            }
        }

        private void UpdateDot(int h)
        {
            bool show = false;
            bool hovered = false;
            Vector3 projected = Vector3.zero;
            float distance = 0f;

            if (buttons.Length > 0 && SimpleGrab.TryGetFingertip(h == 0, out Vector3 tip))
            {
                // El frente del panel es -Z local: positivo = delante.
                Vector3 back = transform.forward;
                distance = -Vector3.Dot(tip - transform.position, back);
                if (distance >= -behindDistance && distance <= showDistance)
                {
                    projected = tip + back * distance;
                    foreach (TouchButton button in buttons)
                    {
                        if (button == null || !button.isActiveAndEnabled)
                        {
                            continue;
                        }

                        if (NearButton(button, projected))
                        {
                            show = true;
                            hovered |= button.Hovered;
                        }
                    }
                }
            }

            RoundedPanel dot = dots[h];
            if (!show)
            {
                if (dot != null)
                {
                    dot.Renderer.enabled = false;
                }

                return;
            }

            if (dot == null)
            {
                dot = RoundedPanel.Create(transform, h == 0 ? "CursorDedo_Izq" : "CursorDedo_Der", Vector2.zero,
                    Vector2.one * 0.01f, -0.012f, 0.005f, 0f, 0.004f);
                dot.sheen = 0f;
                dots[h] = dot;
            }

            float scale = Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.x));
            float t = Mathf.Clamp01(distance / showDistance);
            float diameter = Mathf.Lerp(nearDiameter, farDiameter, t) / scale;
            Vector3 local = transform.InverseTransformPoint(projected);
            dot.radius = diameter * 0.5f;
            dot.SetSolid(hovered ? hoverColor : idleColor, hovered ? 0.8f : 0.3f);
            dot.SetFillAlpha(Mathf.Lerp(1f, 0.55f, t));
            dot.SetRect(new Vector2(local.x, local.y), new Vector2(diameter, diameter));
            dot.Renderer.enabled = true;
        }

        private bool NearButton(TouchButton button, Vector3 point)
        {
            BoxCollider box = button.GetComponent<BoxCollider>();
            if (box == null)
            {
                return false;
            }

            Vector3 local = button.transform.InverseTransformPoint(point) - box.center;
            Vector3 s = button.transform.lossyScale;
            return Mathf.Abs(local.x * s.x) <= Mathf.Abs(box.size.x * s.x) * 0.5f + buttonMargin
                && Mathf.Abs(local.y * s.y) <= Mathf.Abs(box.size.y * s.y) * 0.5f + buttonMargin;
        }

        private void OnDestroy()
        {
            foreach (RoundedPanel dot in dots)
            {
                if (dot != null)
                {
                    Destroy(dot.gameObject);
                }
            }
        }
    }
}
