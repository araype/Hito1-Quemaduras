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

        // Vapor que sube desde un disco de radio dado (olla, charco caliente).
        // El objeto se gira para que el eje Z del cono apunte hacia arriba.
        public static ParticleSystem CreateSteam(string name, Transform parent, Vector3 worldPosition, float radius,
            float rate, int maxParticles = 60)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<NoAutoCollider>();
            go.transform.SetParent(parent, true);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem steam = go.AddComponent<ParticleSystem>();
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = steam.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.006f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = steam.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = Mathf.Max(0.005f, radius);

            ParticleSystem.SizeOverLifetimeModule size = steam.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.6f));

            ParticleSystem.RotationOverLifetimeModule rotation = steam.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystem.ColorOverLifetimeModule color = steam.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            ParticleSystem.NoiseModule noise = steam.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 10f;
            Material material = Mat("Vapor");
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            steam.Play();
            return steam;
        }

        // Disco de borde irregular de radio 1 (charco de agua); se escala.
        public static Mesh PuddleMesh(int seed = 0)
        {
            const int segments = 28;
            Vector3[] v = new Vector3[segments + 2];
            Vector3[] n = new Vector3[segments + 2];
            int[] tris = new int[segments * 3];
            n[0] = Vector3.up;
            float phase = seed * 1.7f;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float r = 1f + 0.08f * Mathf.Sin(a * 3f + phase) + 0.05f * Mathf.Sin(a * 7f + 1f + phase * 2f);
                v[s + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                n[s + 1] = Vector3.up;
                if (s < segments)
                {
                    tris[s * 3] = 0;
                    tris[s * 3 + 1] = s + 2;
                    tris[s * 3 + 2] = s + 1;
                }
            }

            Mesh mesh = new Mesh { name = "Charco" };
            mesh.vertices = v;
            mesh.normals = n;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
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
