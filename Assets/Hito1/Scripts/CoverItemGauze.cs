using System.Collections;
using UnityEngine;

namespace SojaExiles
{
    // Parte de CoverItem solo para la gasa: donde esta respecto a la
    // quemadura, como se asienta sobre la mano y cuanto la cubre.
    public partial class CoverItem
    {
        private const float HandHalfThickness = 0.018f;
        private Transform knuckle;

        // Medio segundo: la gasa baja, se pone plana sobre el dorso de la mano
        // (siguiendola si se mueve) y se aplana como tela. Despues queda una
        // capa de gasa en ese lugar y se mide cuanto de la quemadura cubre.
        private IEnumerator SettleGauze(Transform hand)
        {
            body.isKinematic = true;
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    col.enabled = false;
                }
            }

            Placement(hand, out float offset, out float _, out float _);
            Vector3 dorsum = Dorsum(hand);
            Vector3 skin = SkinPoint(hand, dorsum);
            Vector3 lateral = Vector3.ProjectOnPlane(GauzeCenter - skin, dorsum);
            Vector3 lateralLocal = Quaternion.Inverse(hand.rotation) * lateral;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;
            const float seconds = 0.45f;
            float t = 0f;
            Vector3 target = skin + lateral;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                dorsum = Dorsum(hand);
                target = SkinPoint(hand, dorsum) + hand.rotation * lateralLocal + dorsum * 0.003f;
                Vector3 along = Vector3.ProjectOnPlane(Knuckle(hand) - hand.position, dorsum);
                Quaternion flatRot = Quaternion.LookRotation(along.sqrMagnitude > 1e-6f ? along.normalized : hand.forward,
                    dorsum);
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, target, k),
                    Quaternion.Slerp(startRot, flatRot, k));
                transform.localScale = Vector3.Scale(startScale, new Vector3(1f, Mathf.Lerp(1f, 0.35f, k), 1f));
                yield return null;
            }

            // Capa de gasa donde quedo apoyada (no con la forma de la
            // quemadura): si quedo corrida, se ve la quemadura asomar.
            SkinnedMeshRenderer layer = GauzeLayer(target, Dorsum(hand));
            if (layer == null)
            {
                SkinnedMeshRenderer source = burnOverlay != null ? burnOverlay.Overlay : null;
                layer = HandSkinMesh.CopyWhere(source, c => c.a > 0.3f || c.r > 0.1f, 0.002f, "GasaMano", "Gasa_Puesta");
            }

            if (layer != null)
            {
                patient.StartCoroutine(FadeIn(layer, 0.3f));
            }

            GauzeOnHand = true;
            GauzeCenterLocal = hand.InverseTransformPoint(target);

            float burnRadius = burnOverlay != null ? burnOverlay.burnRadius : 0.04f;
            float coverage = Coverage(burnRadius, gauzeHalfSize * 1.128f, offset);
            LastCoverage = coverage;
            if (coverage < coverageNeeded)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.GauzeOffCenter,
                    Mathf.RoundToInt(coverage * 100f).ToString(), BurnSequenceManager.Severity.Error);
            }

            FinishCover(hand);
        }

        // Cuanto cubrio la ultima gasa puesta (0 a 1).
        public static float LastCoverage { get; private set; }

        private SkinnedMeshRenderer GauzeLayer(Vector3 centerWorld, Vector3 dorsum)
        {
            HandFrame frame = HandFrame.Build(patient, true, 0.45f, "Body");
            if (frame == null)
            {
                return null;
            }

            Vector3 c = frame.ToBind(centerWorld);
            Vector3 n = frame.DirToBind(dorsum);
            float radius = gauzeHalfSize * 1.128f * frame.UnitsPerMeter;
            SkinnedMeshRenderer layer = HandSkinMesh.Build(patient, true, 0.45f, 0.003f, "Body", "GasaMano",
                "Gasa_Puesta", (center, normal) => Vector3.Dot(normal, n) > 0.15f
                    && Vector3.ProjectOnPlane(center - c, n).magnitude < radius);
            PlanarUV(layer, c, frame.Long, frame.Wide, 0.1f * frame.UnitsPerMeter);
            return layer;
        }

        // Coordenadas de textura planas sobre la mano (en la pose de
        // enlace): una vez la textura cada 'size' unidades, para que la trama
        // del tejido tenga su tamano real (las de la piel la estiraban).
        public static void PlanarUV(SkinnedMeshRenderer layer, Vector3 origin, Vector3 u, Vector3 v, float size)
        {
            if (layer == null || layer.sharedMesh == null || size <= 0f)
            {
                return;
            }

            Mesh mesh = layer.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 d = vertices[i] - origin;
                uv[i] = new Vector2(Vector3.Dot(d, u) / size, Vector3.Dot(d, v) / size);
            }

            mesh.uv = uv;
        }

        private static IEnumerator FadeIn(Renderer layer, float seconds)
        {
            Material m = layer.material;
            if (!m.HasProperty("_Color"))
            {
                yield break;
            }

            Color full = m.color;
            float t = 0f;
            while (t < seconds && layer != null)
            {
                t += Time.deltaTime;
                Color c = full;
                c.a = full.a * Mathf.Clamp01(t / seconds);
                m.color = c;
                yield return null;
            }

            if (layer != null)
            {
                m.color = full;
            }
        }

        // Fraccion de un circulo (la quemadura, radio r) que tapa otro (la
        // gasa, radio big) con los centros a 'd'.
        private static float Coverage(float r, float big, float d)
        {
            if (d >= r + big)
            {
                return 0f;
            }

            if (d <= Mathf.Abs(big - r))
            {
                return big >= r ? 1f : big * big / (r * r);
            }

            float a = r * r * Mathf.Acos(Mathf.Clamp((d * d + r * r - big * big) / (2f * d * r), -1f, 1f));
            float b = big * big * Mathf.Acos(Mathf.Clamp((d * d + big * big - r * r) / (2f * d * big), -1f, 1f));
            float c = 0.5f * Mathf.Sqrt(Mathf.Max(0f, (-d + r + big) * (d + r - big) * (d - r + big) * (d + r + big)));
            return Mathf.Clamp01((a + b - c) / (Mathf.PI * r * r));
        }

        // Como esta la gasa respecto a la quemadura: corrimiento sobre la piel
        // (m), inclinacion respecto al dorso (grados) y cuanto se hundio la
        // mano del jugador bajo la piel (m, negativo = adentro).
        private void Placement(Transform hand, out float offset, out float tilt, out float depth)
        {
            Vector3 dorsum = Dorsum(hand);
            Vector3 skin = SkinPoint(hand, dorsum);
            offset = Vector3.ProjectOnPlane(GauzeCenter - skin, dorsum).magnitude;
            float a = Vector3.Angle(transform.up, dorsum);
            tilt = Mathf.Min(a, 180f - a);
            depth = Vector3.Dot(GauzeCenter - skin, dorsum);
            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                if (SimpleGrab.IsHeldBy(body, left) && SimpleGrab.TryGetHand(left, out Vector3 p, out bool _))
                {
                    depth = Mathf.Min(depth, Vector3.Dot(p - skin, dorsum));
                }
            }
        }

        // Centro de la gasa (la raiz esta en su cara de abajo). Desde la
        // transformacion: el centro de masa de un cuerpo cinematico no se
        // actualiza hasta el siguiente paso de fisica.
        private Vector3 GauzeCenter
        {
            get { return transform.TransformPoint(new Vector3(0f, 0.004f, 0f)); }
        }

        // Hacia donde mira el dorso de la mano (donde esta la quemadura).
        private Vector3 Dorsum(Transform hand)
        {
            Vector3 local = burnOverlay != null ? burnOverlay.dorsumDirection : Vector3.back;
            return (hand.rotation * local).normalized;
        }

        // Punto de la piel sobre la quemadura: el centro de la quemadura
        // llevado al eje de la mano (muneca a nudillo) y subido medio grosor
        // de la mano hacia el dorso.
        private Vector3 SkinPoint(Transform hand, Vector3 dorsum)
        {
            Vector3 burn = BurnCenter(hand);
            Vector3 a = hand.position;
            Vector3 b = Knuckle(hand);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(burn - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t + dorsum * HandHalfThickness;
        }

        private Vector3 Knuckle(Transform hand)
        {
            if (knuckle == null || !knuckle.IsChildOf(hand))
            {
                foreach (Transform t in hand.GetComponentsInChildren<Transform>())
                {
                    if (t.name.EndsWith("Middle1"))
                    {
                        knuckle = t;
                        break;
                    }
                }
            }

            return knuckle != null ? knuckle.position : hand.position + hand.up * 0.08f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetOnSceneLoad()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => GauzeOnHand = false;
        }
    }
}
