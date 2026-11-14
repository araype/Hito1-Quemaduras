using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Bolsa plastica limpia para cubrir la mano quemada ya enfriada (Cruz Roja
    // Britanica: "loosely cover it with cling film or a clean plastic bag";
    // para una mano, la bolsa la cubre entera y holgada).
    // Se pone como en la vida real, no se pega sola al acercarla: con la bolsa
    // en la mano, se lleva su boca a la punta de los dedos de ella y se
    // desliza hacia la muneca. La bolsa va subiendo por la mano mientras se
    // avanza (se ve cubrir de los dedos a la muneca) y, si se retrocede,
    // baja. Al pasar la muneca queda puesta, suelta. Si se suelta antes o se
    // aparta la mano de su eje, la bolsa se sale de los dedos y cae. Mientras
    // se acerca, ella ofrece la mano. Si se intenta antes de terminar de
    // enfriar, ella no la saca del agua: es un error.
    // Gasa y Film quedan en el enum sin uso (las escenas guardan el numero):
    // la gasa se quito (2026-11-12, las guias piden film o bolsa) y el film va
    // en tiras (FilmRoll, FilmStrip).
    [RequireComponent(typeof(Rigidbody))]
    public class CoverItem : MonoBehaviour
    {
        public enum Kind
        {
            Film,
            Gasa,
            Bolsa
        }

        public Kind kind = Kind.Bolsa;
        public BurnSequenceManager burnSequence;
        [Tooltip("Distancia de la boca de la bolsa (la mano que la sostiene) a la punta de sus dedos para empezar a ponerla (m).")]
        public float mouthReach = 0.07f;
        [Tooltip("Si la mano que la desliza se aparta mas que esto del eje de su mano, la bolsa se sale (m).")]
        public float slipRadius = 0.12f;
        [Tooltip("Cuanto mas alla de la muneca hay que llevarla (m).")]
        public float wristMargin = 0.03f;
        [Tooltip("Fraccion del recorrido (dedos a muneca) con que queda puesta.")]
        [Range(0.5f, 1f)] public float completeAt = 0.92f;
        [Tooltip("A esta distancia de sus dedos, con la bolsa en la mano, ella ofrece la mano (m).")]
        public float offerDistance = 0.4f;
        [Tooltip("Con la bolsa en la mano a esta distancia de su mano (m), ella la saca y la ofrece.")]
        public float approachDistance = 1.2f;

        private Grabbable grabbable;
        private Rigidbody body;
        private Renderer[] renderers;
        private bool used;
        private bool sliding;
        private float progress;
        private float lastSound;
        private float nextEngage;
        private float nextEarlyReport;
        private PatientBody patient;
        private PatientBurnArm arm;
        private HoldHint hint;
        private Transform middle2, middle3;

        // Capa de la bolsa sobre la mano y sus triangulos ordenados de los
        // dedos a la muneca (se muestran los que ya cubrio la bolsa).
        private SkinnedMeshRenderer layer;
        private readonly List<int> sortedTriangles = new List<int>();
        private float[] triangleAt;
        private int shownTriangles = -1;

        private void Start()
        {
            grabbable = GetComponent<Grabbable>();
            body = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }

            hint = new HoldHint("IndicacionBolsa", 0.05f);
        }

        private void OnDestroy()
        {
            if (hint != null)
            {
                hint.Destroy();
            }

            if (!used && layer != null)
            {
                if (layer.sharedMesh != null)
                {
                    Destroy(layer.sharedMesh);
                }

                Destroy(layer.gameObject);
            }
        }

        private void Update()
        {
            if (used || kind != Kind.Bolsa || burnSequence == null || !Patient(out Transform hand))
            {
                return;
            }

            bool held = grabbable != null && grabbable.IsHeld;
            if (sliding)
            {
                UpdateSlide(held, hand);
            }
            else if (held)
            {
                UpdateApproach(hand);
            }
            else
            {
                hint.Hide();
            }
        }

        // Con la bolsa en la mano, cerca de sus dedos.
        private void UpdateApproach(Transform hand)
        {
            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            Vector3 mouth = Mouth();
            Vector3 tip = Fingertip(hand);
            float distance = Mathf.Min(Vector3.Distance(mouth, tip), Vector3.Distance(mouth, hand.position));

            if (state == BurnSequenceManager.BurnState.Burned || state == BurnSequenceManager.BurnState.Cooling)
            {
                // Todavia no: primero hay que terminar de enfriar.
                if (distance < 0.12f && Time.time >= nextEarlyReport)
                {
                    nextEarlyReport = Time.time + 8f;
                    burnSequence.Report(BurnSequenceManager.BurnEvent.CoveredTooEarly, kind.ToString(),
                        BurnSequenceManager.Severity.Error);
                }

                hint.Hide();
                return;
            }

            if (state != BurnSequenceManager.BurnState.Cooled || distance > offerDistance)
            {
                // Al acercarse con la bolsa ella ya saca la mano de debajo de
                // la manta: antes la ofrecia recien a 40 cm de la mano, y con
                // la mano medio tapada no se encontraba.
                if (state == BurnSequenceManager.BurnState.Cooled && arm != null
                    && arm.Current != PatientBurnArm.Mode.InWater
                    && Vector3.Distance(transform.position, hand.position) < approachDistance)
                {
                    arm.ShowHand(1.5f);
                }

                hint.Hide();
                return;
            }

            if (arm != null && arm.Current != PatientBurnArm.Mode.InWater)
            {
                arm.ShowHand(1.5f);
            }

            hint.Show(tip, "Pasa la bolsa\npor sus dedos", 0f);
            if (Time.time >= nextEngage && Vector3.Distance(mouth, tip) < mouthReach)
            {
                StartSlide(hand, tip);
            }
        }

        private void StartSlide(Transform hand, Vector3 tip)
        {
            if (layer == null && !BuildLayer(hand, tip))
            {
                return;
            }

            sliding = true;
            progress = 0f;
            lastSound = 0f;
            // La bolsa doblada deja de verse: ahora es la que va sobre la mano.
            SetPropVisible(false);
            Hito1FX.PlayAt("tela_roce_1", tip, 0.4f);
        }

        // La bolsa sigue a la mano que la desliza, medida sobre el eje de la
        // mano de ella (si ella mueve la mano, no cuenta como avance).
        private void UpdateSlide(bool held, Transform hand)
        {
            if (burnSequence.CurrentState != BurnSequenceManager.BurnState.Cooled)
            {
                SlipOff(hand);
                return;
            }

            Vector3 tip = Fingertip(hand);
            Vector3 wrist = hand.position;
            Vector3 axis = wrist - tip;
            float length = axis.magnitude + wristMargin;
            if (length < 0.05f)
            {
                return;
            }

            axis.Normalize();
            Vector3 offset = Mouth() - tip;
            float along = Vector3.Dot(offset, axis);
            float radial = (offset - axis * along).magnitude;
            if (!held || radial > slipRadius || along < -0.12f)
            {
                if (held && progress < completeAt)
                {
                    SimpleGrab.ForceRelease(body);
                }

                if (progress >= completeAt)
                {
                    Finish(hand);
                }
                else
                {
                    SlipOff(hand);
                }

                return;
            }

            float before = progress;
            progress = Mathf.Clamp01(along / length);
            ShowLayer(progress);
            if (arm != null && arm.Current != PatientBurnArm.Mode.InWater)
            {
                arm.ShowHand(1.5f);
            }

            // Roce del plastico al avanzar y una vibracion suave en la mano que
            // la desliza.
            if (Mathf.Abs(progress - lastSound) > 0.25f)
            {
                lastSound = progress;
                Hito1FX.PlayAt("tela_roce_" + Random.Range(1, 4), tip + axis * along, 0.3f);
            }

            float speed = Mathf.Abs(progress - before) / Mathf.Max(1e-4f, Time.deltaTime);
            if (BurnHaptics.Current != null && speed > 0.05f)
            {
                BurnHaptics.Current.Buzz(SimpleGrab.IsHeldBy(body, true) ? 0 : 1,
                    Mathf.Clamp(0.08f + 0.15f * speed, 0.08f, 0.3f), 0.05f, 0f, 0.4f);
            }

            hint.Show(wrist, "Deslízala hasta\nla muñeca", progress / completeAt);
            if (progress >= completeAt)
            {
                SimpleGrab.ForceRelease(body);
                Finish(hand);
            }
        }

        // Se salio de los dedos: la bolsa doblada vuelve a verse y cae.
        private void SlipOff(Transform hand)
        {
            sliding = false;
            progress = 0f;
            nextEngage = Time.time + 0.6f;
            ShowLayer(-1f);
            SetPropVisible(true);
            hint.Hide();
            if (!SimpleGrab.IsHeldObject(body))
            {
                transform.position = Fingertip(hand);
                body.isKinematic = false;
                body.useGravity = true;
            }

            Hito1FX.PlayAt("tela_roce_2", hand.position, 0.3f);
        }

        private void Finish(Transform hand)
        {
            used = true;
            sliding = false;
            hint.Hide();
            ShowLayer(1f);
            Hito1FX.PlayAt("tela_roce_3", hand.position, 0.6f);
            burnSequence.Report(BurnSequenceManager.BurnEvent.Covered, kind.ToString());
            burnSequence.ChangeState(BurnSequenceManager.BurnState.Covered);
            if (arm != null)
            {
                arm.EndShow();
            }

            Destroy(gameObject);
        }

        // Capa holgada sobre la piel de la mano hasta la muneca, con los
        // triangulos ordenados por lo lejos que estan de la punta de los dedos.
        private bool BuildLayer(Transform hand, Vector3 tip)
        {
            HandFrame frame = HandFrame.Build(patient, true, 0.45f, "Body");
            layer = HandSkinMesh.Build(patient, true, 0.25f, 0.01f, "Body", "BolsaMano", "Bolsa_Mano");
            if (frame == null || layer == null || layer.sharedMesh == null)
            {
                Debug.LogWarning("[CoverItem] No se pudo armar la bolsa sobre la mano (sin malla de piel).", this);
                return false;
            }

            Vector3 tipBind = frame.ToBind(tip);
            Vector3 axis = frame.ToBind(hand.position) - tipBind;
            float length = axis.magnitude + wristMargin * frame.UnitsPerMeter;
            axis.Normalize();

            Mesh mesh = layer.sharedMesh;
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            int count = t.Length / 3;
            float[] at = new float[count];
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 c = (v[t[i * 3]] + v[t[i * 3 + 1]] + v[t[i * 3 + 2]]) / 3f;
                at[i] = Vector3.Dot(c - tipBind, axis) / length;
                order[i] = i;
            }

            System.Array.Sort(order, (a, b) => at[a].CompareTo(at[b]));
            triangleAt = new float[count];
            sortedTriangles.Clear();
            for (int i = 0; i < count; i++)
            {
                int k = order[i];
                triangleAt[i] = at[k];
                sortedTriangles.Add(t[k * 3]);
                sortedTriangles.Add(t[k * 3 + 1]);
                sortedTriangles.Add(t[k * 3 + 2]);
            }

            ShowLayer(-1f);
            return true;
        }

        // Muestra la bolsa hasta 'upTo' (0 = dedos, 1 = muneca; negativo, nada).
        private void ShowLayer(float upTo)
        {
            if (layer == null || triangleAt == null)
            {
                return;
            }

            int count = 0;
            if (upTo >= completeAt)
            {
                count = triangleAt.Length;
            }
            else if (upTo >= 0f)
            {
                while (count < triangleAt.Length && triangleAt[count] <= upTo)
                {
                    count++;
                }
            }

            if (count == shownTriangles)
            {
                return;
            }

            shownTriangles = count;
            layer.enabled = count > 0;
            layer.sharedMesh.SetTriangles(sortedTriangles, 0, count * 3, 0, false);
        }

        private void SetPropVisible(bool visible)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }

        // La boca de la bolsa: el punto de agarre de la mano que la sostiene.
        private Vector3 Mouth()
        {
            for (int h = 0; h < 2; h++)
            {
                bool left = h == 0;
                if (SimpleGrab.IsHeldBy(body, left) && SimpleGrab.TryGetHand(left, out Vector3 p, out bool _))
                {
                    return p;
                }
            }

            return transform.position;
        }

        // Punta del dedo medio de la mano quemada (la tia de Avaturn no tiene
        // hueso de punta: se prolonga la ultima falange).
        private Vector3 Fingertip(Transform hand)
        {
            if (middle3 == null || !middle3.IsChildOf(hand))
            {
                middle2 = middle3 = null;
                foreach (Transform t in hand.GetComponentsInChildren<Transform>())
                {
                    if (t.name.EndsWith("Middle2"))
                    {
                        middle2 = t;
                    }
                    else if (t.name.EndsWith("Middle3"))
                    {
                        middle3 = t;
                    }
                }
            }

            if (middle3 != null && middle2 != null)
            {
                return middle3.position + (middle3.position - middle2.position) * 0.9f;
            }

            return hand.position + hand.up * 0.17f;
        }

        // La paciente y su mano quemada (false si aun no esta lista).
        private bool Patient(out Transform hand)
        {
            if (patient == null)
            {
                patient = FindObjectOfType<PatientBody>();
                arm = patient != null ? patient.GetComponent<PatientBurnArm>() : null;
            }

            hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            return hand != null;
        }
    }
}
