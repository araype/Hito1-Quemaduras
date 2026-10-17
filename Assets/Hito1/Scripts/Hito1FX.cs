using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Acceso a los sonidos y materiales de efectos que estan en
    // Assets/Hito1/Resources (Hito1Audio y Hito1FX). Se cargan por nombre para
    // que los componentes que se instalan en runtime no necesiten referencias
    // en la escena. Para cambiar un sonido basta con reemplazar el .wav con
    // el mismo nombre.
    public static class Hito1FX
    {
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (!Clips.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Hito1Audio/" + name);
                if (clip == null)
                {
                    Debug.LogWarning("[Hito1FX] No se encontro el sonido Resources/Hito1Audio/" + name);
                }

                Clips[name] = clip;
            }

            return clip;
        }

        public static Material Mat(string name)
        {
            if (!Materials.TryGetValue(name, out Material material))
            {
                material = Resources.Load<Material>("Hito1FX/" + name);
                if (material == null)
                {
                    Debug.LogWarning("[Hito1FX] No se encontro el material Resources/Hito1FX/" + name);
                }

                Materials[name] = material;
            }

            return material;
        }

        // Fuente de audio 3D (con atenuacion realista para una habitacion).
        public static AudioSource CreateSource(GameObject host, string clipName, float volume, bool loop,
            float minDistance = 0.5f, float maxDistance = 8f)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.clip = Clip(clipName);
            source.volume = volume;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.spread = 30f;
            return source;
        }

        // Sonido corto en un punto del mundo. A diferencia de
        // AudioSource.PlayClipAtPoint, permite variar el tono y no se corta.
        public static void PlayAt(string clipName, Vector3 position, float volume = 1f, float pitchJitter = 0.05f,
            float minDistance = 0.4f)
        {
            AudioClip clip = Clip(clipName);
            if (clip == null)
            {
                return;
            }

            GameObject go = new GameObject("Sonido_" + clipName);
            go.transform.position = position;
            AudioSource source = CreateSource(go, clipName, volume, false, minDistance, 10f);
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.5f, source.pitch) + 0.2f);
        }

        // Fundido de volumen de un loop: lo arranca al subir y lo pausa en
        // silencio para no gastar mezcla.
        public static void FadeLoop(AudioSource source, float target, float speed)
        {
            if (source == null || source.clip == null)
            {
                return;
            }

            source.volume = Mathf.MoveTowards(source.volume, target, speed * Time.deltaTime);
            if (source.volume > 0.001f)
            {
                if (!source.isPlaying)
                {
                    source.time = Random.Range(0f, source.clip.length * 0.9f);
                    source.Play();
                }
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
