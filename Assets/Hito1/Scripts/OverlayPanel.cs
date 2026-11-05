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
    public class OverlayPanel : MonoBehaviour
    {
        private const int BaseQueue = 4000;

        private static Shader shader;

        private readonly HashSet<Renderer> converted = new HashSet<Renderer>();
        private int lastCount = -1;

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
        }

        public void Apply()
        {
            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            lastCount = renderers.Length;
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

                material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
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
        }

        private struct Entry
        {
            public Material material;
            public float depth;
            public bool isText;
        }
    }
}
