using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Ambiente de la cocina:
    //   - sonido de fondo (zumbido del refrigerador y aire de la habitacion);
    //   - una sonda de reflejos en tiempo real que se renderiza una vez al
    //     empezar, para que el acero de la olla, el agua y el grifo reflejen la
    //     cocina en vez del cielo por defecto;
    //   - luz calida de cocina: una lampara sobre el meson (sin sombras, para
    //     cuidar el rendimiento en el Quest) y el sol algo mas tibio.
    // Se centra entre los objetos de la escena (olla, grifo, paciente) si no
    // se indica otro punto.
    public class KitchenAmbience : MonoBehaviour
    {
        public Transform center;
        public float ambienceVolume = 0.22f;

        [Header("Luz calida")]
        public bool warmLight = true;
        public Color lampColor = new Color(1f, 0.83f, 0.62f);
        public float lampIntensity = 0.9f;
        public float lampRange = 4.5f;
        [Tooltip("Altura de la lampara sobre el centro de la cocina (m).")]
        public float lampHeight = 1.3f;
        [Range(0f, 1f)] public float warmSunlight = 0.35f;

        [Header("Reflejos")]
        public bool reflectionProbe = true;
        public Vector3 probeSize = new Vector3(6f, 3.2f, 6f);
        public int probeResolution = 128;

        private ReflectionProbe probe;

        private IEnumerator Start()
        {
            Vector3 point = center != null ? center.position : GuessCenter();

            GameObject audio = new GameObject("AmbienteCocina");
            audio.transform.position = point;
            AudioSource source = Hito1FX.CreateSource(audio, "cocina_ambiente_loop", ambienceVolume, true, 2f, 20f);
            source.spatialBlend = 0.4f;
            source.priority = 200;
            if (source.clip != null)
            {
                source.Play();
            }

            if (warmLight)
            {
                AddWarmLight(point);
            }

            if (!reflectionProbe)
            {
                yield break;
            }

            GameObject go = new GameObject("ReflejosCocina");
            go.transform.position = point + Vector3.up * 0.4f;
            probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = probeSize;
            probe.resolution = probeResolution;
            probe.hdr = false;
            probe.boxProjection = true;
            probe.importance = 2;

            // Se espera a que la escena termine de armarse (olla, paciente,
            // colliders) antes de fotografiarla.
            yield return new WaitForSeconds(0.5f);
            probe.RenderProbe();
        }

        private void AddWarmLight(Vector3 point)
        {
            GameObject go = new GameObject("LamparaCocina");
            go.transform.position = point + Vector3.up * lampHeight;
            Light lamp = go.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = lampColor;
            lamp.intensity = lampIntensity;
            lamp.range = lampRange;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;

            // El sol (luz direccional) se entibia un poco.
            foreach (Light l in FindObjectsOfType<Light>())
            {
                if (l.type == LightType.Directional)
                {
                    l.color = Color.Lerp(l.color, new Color(1f, 0.86f, 0.7f), warmSunlight);
                }
            }
        }

        private Vector3 GuessCenter()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (PotFX pot in FindObjectsOfType<PotFX>())
            {
                sum += pot.transform.position;
                n++;
            }

            foreach (WaterTap tap in FindObjectsOfType<WaterTap>())
            {
                sum += tap.transform.position;
                n++;
            }

            foreach (PatientBody patient in FindObjectsOfType<PatientBody>())
            {
                sum += patient.transform.position + Vector3.up;
                n++;
            }

            return n > 0 ? sum / n : transform.position;
        }
    }
}
