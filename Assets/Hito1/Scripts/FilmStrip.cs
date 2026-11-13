using UnityEngine;

namespace SojaExiles
{
    // Una tira de film. La crea FilmRoll en el borde del rollo cuando una mano
    // lo sostiene; la otra mano la toma y tira: la tira se estira desde el
    // rollo hasta la mano y, al llegar a su largo, se corta y queda colgando
    // de la mano. Se apoya sobre la mano quemada, a lo largo (de la muneca a
    // los dedos), y al soltarla baja a la piel y se asienta (FilmCoverage la
    // pone sobre la piel): no se pega desde lejos, la tira tiene que estar
    // tocando la mano. Mientras se acerca con la tira, ella ofrece la mano.
    // Envolver la mano es un
    // error: soltarla atravesada o dar la vuelta a la mano con la tira la deja
    // envuelta (se ve unos segundos), ella se queja y el narrador corrige.
    [RequireComponent(typeof(Rigidbody))]
    public class FilmStrip : MonoBehaviour
    {
        public float width = 0.06f;
        [Tooltip("Si se suelta con la tira a menos de esta distancia del eje de la mano quemada (muneca a dedos), se pone (m).")]
        public float attachDistance = 0.055f;
        [Tooltip("A esta distancia de su mano, con una tira cortada, ella ofrece la mano (m). Antes 0,4: con la mano medio tapada por la manta no se encontraba.")]
        public float offerDistance = 1.2f;
        [Tooltip("Segundos que tarda la tira en bajar a la piel y asentarse.")]
        public float settleSeconds = 0.3f;
        [Tooltip("Segundos sin tocarla antes de que una tira suelta desaparezca.")]
        public float looseSeconds = 20f;

        public FilmRoll Roll { get; private set; }
        public bool Torn { get; private set; }
        public float Length { get; private set; }

        private Grabbable grabbable;
        private Rigidbody body;
        private Mesh mesh;
        private Vector3 localDir;
        private Vector3 localWide;
        private bool wasHeld;
        private float looseSince = -1f;
        private bool settling;
        private BurnSequenceManager burnSequence;
        private PatientBurnArm arm;

        // Gesto de envolver: con la tira en la mano, dar la vuelta alrededor
        // de la mano quemada. Se mide el giro de la mano del jugador alrededor
        // del eje largo de esa mano.
        [Tooltip("Grados de vuelta alrededor de la mano quemada para que el film se envuelva.")]
        public float wrapGestureDegrees = 270f;
        [Tooltip("Distancia maxima de la mano del jugador al eje de la mano quemada durante el gesto (m).")]
        public float wrapGestureRadius = 0.15f;
        private float wrapAngle;
        private Vector3 lastRadial;
        private bool trackingWrap;

        public bool IsHeld
        {
            get { return grabbable != null && grabbable.IsHeld; }
        }

        public static FilmStrip Create(FilmRoll roll, Vector3 position)
        {
            GameObject go = new GameObject("FilmTira");
            go.transform.position = position;
            go.AddComponent<NoAutoCollider>();
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.035f;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.drag = 4f;
            rb.angularDrag = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Grabbable g = go.AddComponent<Grabbable>();
            g.respawnIfLost = false;
            g.returnFromFloor = false;

            FilmStrip strip = go.AddComponent<FilmStrip>();
            strip.Roll = roll;
            strip.grabbable = g;
            strip.body = rb;
            strip.width = roll != null ? roll.stripWidth : strip.width;

            GameObject ribbon = new GameObject("Film_Tira");
            ribbon.AddComponent<NoAutoCollider>();
            ribbon.transform.SetParent(go.transform, false);
            strip.mesh = new Mesh { name = "FilmTira" };
            strip.mesh.MarkDynamic();
            ribbon.AddComponent<MeshFilter>().sharedMesh = strip.mesh;
            MeshRenderer renderer = ribbon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PropKit.Mat("Film_Tira", new Color(0.9f, 0.95f, 1f, 0.35f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return strip;
        }

        // Ya no esta unida al rollo: queda del largo que se tiro, colgando
        // hacia donde estaba el rollo.
        public void Tear(Vector3 rollEdge, Vector3 rollAxis)
        {
            // Se siente el corte en las dos manos (rollo y tira).
            if (BurnHaptics.Current != null)
            {
                BurnHaptics.Current.Buzz(-1, 0.6f, 0.12f, 0f, 0.9f);
            }

            Vector3 toRoll = rollEdge - transform.position;
            Length = toRoll.magnitude;
            localDir = transform.InverseTransformDirection(toRoll.normalized);
            Vector3 wide = Vector3.ProjectOnPlane(rollAxis, toRoll).normalized;
            if (wide.sqrMagnitude < 0.5f)
            {
                wide = Vector3.Cross(toRoll, Vector3.up).normalized;
            }

            localWide = transform.InverseTransformDirection(wide);
            Torn = true;
            Roll = null;
            Hito1FX.PlayAt("film_estirar", transform.position, 0.6f);
        }

        // Extremos de la tira en el mundo (el que esta en la mano y el otro).
        public Vector3 WorldDirection
        {
            get { return transform.TransformDirection(localDir); }
        }

        private void Update()
        {
            bool held = IsHeld;
            if (wasHeld && !held && Torn)
            {
                Released();
            }

            wasHeld = held;
            if (settling)
            {
                return;
            }

            if (Torn && held)
            {
                OfferHand();
            }

            if (Torn && held && TrackWrapGesture())
            {
                return;
            }

            if (Torn && !held)
            {
                if (looseSince < 0f)
                {
                    looseSince = Time.time;
                }
                else if (Time.time - looseSince > looseSeconds)
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                looseSince = -1f;
            }
        }

        private void LateUpdate()
        {
            if (Torn)
            {
                Ribbon(Vector3.zero, localDir * Length, localWide);
            }
            else if (Roll != null)
            {
                Vector3 edge = Roll.EdgePoint(transform.position);
                Ribbon(transform.InverseTransformPoint(edge), Vector3.zero,
                    transform.InverseTransformDirection(Roll.Axis));
            }
        }

        // Cinta plana de 'width' entre dos puntos (locales), con las dos caras.
        private void Ribbon(Vector3 from, Vector3 to, Vector3 wide)
        {
            const int Segments = 6;
            Vector3 half = wide.normalized * (width * 0.5f / Mathf.Max(1e-4f, transform.lossyScale.x));
            Vector3 along = to - from;
            Vector3 sagDir = transform.InverseTransformDirection(Vector3.down);
            float sag = Torn ? 0.012f : 0.004f;
            Vector3[] v = new Vector3[(Segments + 1) * 2];
            Vector2[] uv = new Vector2[v.Length];
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                // Un poco caida en el medio (al cortarse, mas hacia la punta).
                float s = Torn ? t * t : Mathf.Sin(t * Mathf.PI);
                Vector3 p = from + along * t + sagDir * sag * s;
                v[i * 2] = p - half;
                v[i * 2 + 1] = p + half;
                uv[i * 2] = new Vector2(0f, t);
                uv[i * 2 + 1] = new Vector2(1f, t);
            }

            int[] tris = new int[Segments * 12];
            int k = 0;
            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris[k++] = a; tris[k++] = c; tris[k++] = b;
                tris[k++] = b; tris[k++] = c; tris[k++] = d;
                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = b; tris[k++] = d; tris[k++] = c;
            }

            mesh.Clear();
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // Se corto despues de que la mano la soltara (FilmRoll): se pone si
        // esta sobre la mano quemada o cae.
        public void LetGo()
        {
            wasHeld = false;
            Released();
        }

        private void Released()
        {
            if (!TryApply())
            {
                // Cae sola, liviana.
                body.isKinematic = false;
                body.useGravity = true;
            }
        }

        // Intenta ponerla sobre la mano quemada; true si la tira se uso.
        public bool TryApply()
        {
            BurnSequenceManager burnSequence = FindObjectOfType<BurnSequenceManager>();
            FilmCoverage coverage = FilmCoverage.For(FindObjectOfType<PatientBody>());
            Transform hand = coverage != null ? coverage.Hand : null;
            if (hand == null || burnSequence == null)
            {
                return false;
            }

            Vector3 a = transform.position;
            Vector3 b = a + WorldDirection * Length;
            if (!coverage.WorldAxis(out Vector3 handCenter, out Vector3 handAxis, out float handHalf)
                || DistanceToHand(a, b, handCenter, handAxis, handHalf) > attachDistance)
            {
                return false;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            if (state == BurnSequenceManager.BurnState.Burned || state == BurnSequenceManager.BurnState.Cooling)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.CoveredTooEarly, "Film",
                    BurnSequenceManager.Severity.Error);
                return false;
            }

            if (state == BurnSequenceManager.BurnState.Idle)
            {
                return false;
            }

            if (!coverage.Apply((a + b) * 0.5f, WorldDirection, width, Length, out bool wrapped))
            {
                if (!wrapped)
                {
                    return false;
                }

                // Soltada atravesada: queda envuelta (y es el error).
                WrapAround(coverage, burnSequence, (a + b) * 0.5f);
                return true;
            }

            Hito1FX.PlayAt("film_estirar", hand.position, 0.5f);
            if (state == BurnSequenceManager.BurnState.Cooled && coverage.Coverage >= coverage.coverageNeeded)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.Covered, "Film");
                burnSequence.ChangeState(BurnSequenceManager.BurnState.Covered);
            }

            StartCoroutine(Settle(coverage.LastLayer, handCenter, handAxis));
            return true;
        }

        // La tira baja a la piel, se alinea con el largo de la mano y se
        // desvanece mientras aparece la capa de film sobre la piel (antes
        // la capa aparecia de golpe donde estaba la mano).
        private System.Collections.IEnumerator Settle(Renderer skinLayer, Vector3 handCenter, Vector3 handAxis)
        {
            settling = true;
            body.isKinematic = true;
            body.useGravity = false;
            foreach (Collider col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            Transform bone = FilmCoverage.For(FindObjectOfType<PatientBody>()).Hand;
            Vector3 mid = transform.position + WorldDirection * (Length * 0.5f);
            float along = Vector3.Dot(mid - handCenter, handAxis);
            Vector3 onAxis = handCenter + handAxis * along;
            Vector3 radial = mid - onAxis;
            Vector3 up = radial.sqrMagnitude > 1e-6f ? radial.normalized : Vector3.up;
            Vector3 dir = Vector3.Dot(WorldDirection, handAxis) >= 0f ? handAxis : -handAxis;
            // Encima de la piel (la mano tiene unos 2 cm de medio grosor).
            Vector3 targetMid = onAxis + up * 0.022f;
            Quaternion startRot = transform.rotation;
            Quaternion endRot = Quaternion.FromToRotation(WorldDirection, dir) * startRot;
            Vector3 startPos = transform.position;
            // Se sigue a la mano mientras se asienta, por si ella la mueve.
            Vector3 targetLocal = bone != null ? bone.InverseTransformPoint(targetMid - dir * (Length * 0.5f)) : Vector3.zero;

            Renderer ribbon = GetComponentInChildren<Renderer>();
            Material ribbonMat = ribbon != null ? ribbon.material : null;
            Color ribbonColor = ribbonMat != null && ribbonMat.HasProperty("_Color") ? ribbonMat.color : Color.white;
            Material layerMat = skinLayer != null ? skinLayer.material : null;
            Color layerColor = layerMat != null && layerMat.HasProperty("_Color") ? layerMat.color : Color.white;

            float t = 0f;
            float seconds = Mathf.Max(0.05f, settleSeconds);
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                Vector3 target = bone != null ? bone.TransformPoint(targetLocal) : targetMid - dir * (Length * 0.5f);
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, target, k),
                    Quaternion.Slerp(startRot, endRot, k));
                if (ribbonMat != null && ribbonMat.HasProperty("_Color"))
                {
                    Color c = ribbonColor;
                    c.a = ribbonColor.a * (1f - k);
                    ribbonMat.color = c;
                }

                if (layerMat != null && layerMat.HasProperty("_Color") && skinLayer != null)
                {
                    Color c = layerColor;
                    c.a = layerColor.a * k;
                    layerMat.color = c;
                }

                yield return null;
            }

            if (layerMat != null && layerMat.HasProperty("_Color") && skinLayer != null)
            {
                layerMat.color = layerColor;
            }

            if (ribbonMat != null)
            {
                Destroy(ribbonMat);
            }

            Destroy(gameObject);
        }

        // Con una tira cortada cerca de su mano (ya enfriada), ella la ofrece.
        private void OfferHand()
        {
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            if (burnSequence == null || burnSequence.CurrentState != BurnSequenceManager.BurnState.Cooled)
            {
                return;
            }

            if (arm == null)
            {
                arm = FindObjectOfType<PatientBurnArm>();
            }

            FilmCoverage coverage = FilmCoverage.For(FindObjectOfType<PatientBody>());
            Transform hand = coverage != null ? coverage.Hand : null;
            if (arm != null && hand != null && arm.Current != PatientBurnArm.Mode.InWater
                && Vector3.Distance(transform.position, hand.position) < offerDistance)
            {
                arm.ShowHand(1.5f);
            }
        }

        // Distancia minima entre la tira (a-b) y el eje de la mano quemada
        // (de la muneca a los dedos), medida en varios puntos de la tira.
        private static float DistanceToHand(Vector3 a, Vector3 b, Vector3 center, Vector3 axis, float halfLength)
        {
            Vector3 from = center - axis * halfLength;
            Vector3 to = center + axis * halfLength;
            float best = float.MaxValue;
            for (int i = 0; i <= 8; i++)
            {
                best = Mathf.Min(best, DistanceToSegment(Vector3.Lerp(a, b, i / 8f), from, to));
            }

            return best;
        }

        // True si el gesto de envolver termino (la tira ya se uso).
        private bool TrackWrapGesture()
        {
            BurnSequenceManager burnSequence = FindObjectOfType<BurnSequenceManager>();
            if (burnSequence == null || burnSequence.CurrentState == BurnSequenceManager.BurnState.Idle)
            {
                return false;
            }

            FilmCoverage coverage = FilmCoverage.For(FindObjectOfType<PatientBody>());
            if (coverage == null || !coverage.WorldAxis(out Vector3 center, out Vector3 axis, out float halfLength))
            {
                trackingWrap = false;
                return false;
            }

            Vector3 offset = transform.position - center;
            float along = Vector3.Dot(offset, axis);
            Vector3 radial = offset - axis * along;
            bool near = radial.magnitude < wrapGestureRadius && Mathf.Abs(along) < halfLength + 0.05f;
            if (!near || radial.sqrMagnitude < 1e-6f)
            {
                // Fuera de la mano: el gesto empieza de nuevo.
                trackingWrap = false;
                wrapAngle = 0f;
                return false;
            }

            if (trackingWrap)
            {
                wrapAngle += Vector3.SignedAngle(lastRadial, radial, axis);
            }

            trackingWrap = true;
            lastRadial = radial;
            if (Mathf.Abs(wrapAngle) < wrapGestureDegrees)
            {
                return false;
            }

            SimpleGrab.ForceRelease(body);
            WrapAround(coverage, burnSequence, center + axis * along);
            return true;
        }

        // El film queda envuelto alrededor de la mano: se ve unos segundos,
        // ella se queja ("me aprieta") y el narrador corrige. El error se
        // cuenta una vez; despues solo se queja y avisa.
        private void WrapAround(FilmCoverage coverage, BurnSequenceManager burnSequence, Vector3 point)
        {
            coverage.Wrap(point, width);
            burnSequence.Report(BurnSequenceManager.BurnEvent.FilmWrapped, null,
                coverage.WrapReported ? BurnSequenceManager.Severity.None : BurnSequenceManager.Severity.Error);
            coverage.WrapReported = true;
            Hito1FX.PlayAt("film_estirar", point, 0.6f);
            Hito1FX.PlayAt("tela_roce_1", point, 0.5f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }

            if (Roll != null)
            {
                Roll.StripGone(this);
            }
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
