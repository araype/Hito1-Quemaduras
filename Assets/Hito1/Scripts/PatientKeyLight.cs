using UnityEngine;

namespace SojaExiles
{
    // Luz principal de la paciente. La cocina tiene la luz horneada y la
    // paciente solo recibe la de las sondas (pareja, sin volumen: se veia
    // plana). Esta luz en tiempo real, sin sombras, ilumina solo la capa
    // "Paciente": le da un lado iluminado y otro en sombra sin sumar luz a la
    // cocina horneada. Pasa a esa capa los renderers de la paciente, tambien
    // los que se le agregan durante la practica (quemadura, chompa, film).
    // No toca objetos con collider (la fisica usa sus propias capas).
    [RequireComponent(typeof(Light))]
    public class PatientKeyLight : MonoBehaviour
    {
        public string layerName = "Paciente";
        public float refreshSeconds = 0.5f;

        private int layer = -1;
        private Transform patient;
        private float next;

        private void Start()
        {
            layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                enabled = false;
                return;
            }

            Light light = GetComponent<Light>();
            light.cullingMask = 1 << layer;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private void LateUpdate()
        {
            if (layer < 0 || Time.time < next)
            {
                return;
            }

            next = Time.time + refreshSeconds;
            if (patient == null)
            {
                PatientBody body = FindObjectOfType<PatientBody>();
                if (body == null)
                {
                    return;
                }

                patient = body.transform;
            }

            foreach (Renderer r in patient.GetComponentsInChildren<Renderer>(true))
            {
                GameObject go = r.gameObject;
                if (go.layer != layer && go.layer == 0 && go.GetComponent<Collider>() == null
                    && !(r is ParticleSystemRenderer))
                {
                    go.layer = layer;
                }
            }
        }
    }
}
