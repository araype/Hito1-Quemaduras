using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SojaExiles
{
    // Hace que un panel (tarjeta de la guia, menu, decision medica, resumen)
    // se dibuje siempre encima de la escena: no lo tapan paredes, gabinetes,
    // el lavadero ni la paciente, aunque quede "dentro" de ellos. Los fondos
    // pasan a un material sin luz (UI/Default) y todo se dibuja sin prueba de
    // profundidad, en orden del fondo hacia el jugador.
    // Va en la raiz del panel; revisa solo si aparecen piezas nuevas (por
    // ejemplo la barra de llenado de los botones).
    // Con una mano del jugador cerca del panel se vuelve a usar la prueba de
    // profundidad: si no, la tarjeta tapaba la mano y no se veia el dedo al
    // apretar un boton.
    public class OverlayPanel : MonoBehaviour
    {
        private const int BaseQueue = 4000;

        [Tooltip("Distancia de una mano al panel (m) a partir de la cual la mano se dibuja delante de la tarjeta.")]
        public float handNearDistance = 0.25f;

        private static Shader shader;

        private readonly HashSet<Renderer> converted = new HashSet<Renderer>();
        private readonly List<Material> materials = new List<Material>();
        private Renderer[] renderers = new Renderer[0];
        private int lastCount = -1;
        private bool handNear;

        private void Start()
        {
            Apply();
        }

        private void LateUpdate()
        {
            int count = GetComponentsInChildren<Renderer>(true).Length;
            if (count != lastCount)
            {
                Apply();
            }

            // Solo si la escena no la tapa: si la tarjeta quedo en parte dentro
            // del meson o de un gabinete, con la prueba de profundidad el
            // mueble la tapaba entera y "desaparecia" al acercarse a leerla.
            bool near = HandNear() && !BlockedFromEye();
            if (near != handNear)
            {
                handNear = near;
                SetZTest();
            }
        }

        // Alguna mano (la punta del indice o el punto de agarre) cerca de
        // la caja que ocupa el panel.
        private bool HandNear()
        {
            Bounds bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (any)
                {
                    bounds.Encapsulate(r.bounds);
                }
                else
                {
                    bounds = r.bounds;
                    any = true;
                }
            }

            if (!any)
            {
                return false;
            }

            bounds.Expand(handNearDistance * 2f);
            for (int h = 0; h < 2; h++)
            {
                if (SimpleGrab.TryGetFingertip(h == 0, out Vector3 tip) && bounds.Contains(tip))
                {
                    return true;
                }

                if (SimpleGrab.TryGetHand(h == 0, out Vector3 grab, out bool _) && bounds.Contains(grab))
                {
                    return true;
                }
            }

            return false;
        }

        // True si algo de la escena (no la propia tarjeta ni las manos) queda
        // entre los ojos y el centro o alguna esquina de la tarjeta.
        private bool BlockedFromEye()
        {
            Transform eye = VRPanelKit.Head();
            if (eye == null)
            {
                return false;
            }

            Bounds bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (any)
                {
                    bounds.Encapsulate(r.bounds);
                }
                else
                {
                    bounds = r.bounds;
                    any = true;
                }
            }

            if (!any)
            {
                return false;
            }

            Vector3 c = bounds.center;
            Vector3 right = transform.right * (bounds.extents.magnitude * 0.6f);
            Vector3 up = transform.up * (bounds.extents.magnitude * 0.45f);
            Vector3[] points = { c, c + right + up, c - right + up, c + right - up, c - right - up };
            foreach (Vector3 p in points)
            {
                foreach (RaycastHit hit in Physics.RaycastAll(eye.position, p - eye.position,
                    Vector3.Distance(eye.position, p), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(transform) && !SimpleGrab.IsHandCollider(hit.collider))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void SetZTest()
        {
            float mode = (float)(handNear ? CompareFunction.LessEqual : CompareFunction.Always);
            foreach (Material material in materials)
            {
                if (material != null)
                {
                    material.SetFloat("unity_GUIZTestMode", mode);
                    material.SetFloat("_ZTest", mode);
                }
            }
        }

        public void Apply()
        {
            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            lastCount = renderers.Length;
            materials.Clear();
            List<Entry> entries = new List<Entry>();

            foreach (Renderer r in renderers)
            {
                if (r == null || r.GetComponent<TextMesh>() != null)
                {
                    continue; // el TextMesh original ya no se dibuja (PanelText)
                }

                Material material;
                bool isText = false;
                TMP_Text text = r.GetComponent<TMP_Text>();
                if (text != null)
                {
                    material = text.fontMaterial; // instancia propia
                    isText = true;
                }
                else
                {
                    if (!converted.Contains(r) && shader != null)
                    {
                        Material src = r.sharedMaterial;
                        // Shader propio (panel redondeado): ya sabe dibujarse encima; solo
                        // hace falta una copia para ordenarlo. Lo demas pasa a UI/Default.
                        Material inst = src != null && src.shader != null && src.shader.name.StartsWith("Hito1/")
                            ? new Material(src) { name = src.name + "_Encima" }
                            : new Material(shader) { name = (src != null ? src.name : "Panel") + "_Encima" };
                        if (src != null && src.HasProperty("_Color"))
                        {
                            inst.color = src.color;
                        }

                        r.sharedMaterial = inst;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.receiveShadows = false;
                        converted.Add(r);
                    }

                    material = r.sharedMaterial;
                }

                if (material == null)
                {
                    continue;
                }

                materials.Add(material);
                // Frente del panel = -Z local: mas negativo, mas cerca del jugador.
                float depth = transform.InverseTransformPoint(r.transform.position).z;
                entries.Add(new Entry { material = material, depth = depth, isText = isText });
            }

            entries.Sort((a, b) =>
            {
                int c = b.depth.CompareTo(a.depth); // lo de atras primero
                return c != 0 ? c : a.isText.CompareTo(b.isText); // texto al final
            });

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].material.renderQueue = BaseQueue + i;
            }

            SetZTest();
        }

        private struct Entry
        {
            public Material material;
            public float depth;
            public bool isText;
        }
    }
}
