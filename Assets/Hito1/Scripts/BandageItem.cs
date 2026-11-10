using UnityEngine;

namespace SojaExiles
{
    // Venda del botiquin: con la gasa ya puesta (CoverItem.GauzeOnHand), se
    // fija dando una vuelta alrededor de la mano de la tia con la venda en la
    // mano. La vuelta se mide alrededor del eje de su mano (muneca a
    // nudillo). Si se da muy pegada a la mano (radio medio menor que
    // tightRadius) queda apretada: es un error, porque al hincharse la mano
    // aprieta. Holgada, queda suelta y bien. Es opcional: sin venda la gasa
    // tambien vale. Queda una banda de venda sobre la gasa.
    public class BandageItem : MonoBehaviour
    {
        [Tooltip("Distancia maxima de la venda al eje de la mano para contar la vuelta (m).")]
        public float reach = 0.16f;
        [Tooltip("Radio medio de la vuelta por debajo del cual queda apretada (m).")]
        public float tightRadius = 0.055f;
        [Tooltip("Grados de vuelta que hay que dar.")]
        public float turnDegrees = 330f;
        [Tooltip("Medio ancho de la banda de venda sobre la mano (m).")]
        public float halfWidth = 0.025f;

        private Rigidbody body;
        private HoldHint hint;
        private PatientBody patient;
        private PatientBurnArm arm;
        private BurnSequenceManager burnSequence;
        private Transform knuckle;
        private bool tracking;
        private float lastAngle;
        private float turned;
        private float radiusSum;
        private int radiusCount;
        private float nextTick;
        private bool done;

        private void Start()
        {
            body = GetComponent<Rigidbody>();
            hint = new HoldHint("IndicacionVenda", 0.06f);
            patient = FindObjectOfType<PatientBody>();
            arm = patient != null ? patient.GetComponent<PatientBurnArm>() : null;
            burnSequence = FindObjectOfType<BurnSequenceManager>();
        }

        private void OnDestroy()
        {
            if (hint != null)
            {
                hint.Destroy();
            }
        }

        private void LateUpdate()
        {
            if (done || body == null || patient == null || !patient.IsReady)
            {
                return;
            }

            bool heldLeft = SimpleGrab.IsHeldBy(body, true);
            bool heldRight = SimpleGrab.IsHeldBy(body, false);
            if (!CoverItem.GauzeOnHand || (!heldLeft && !heldRight)
                || !SimpleGrab.TryGetHand(heldLeft, out Vector3 point, out bool _))
            {
                Reset();
                return;
            }

            Transform hand = patient.HandBone(true);
            Vector3 axis = Knuckle(hand) - hand.position;
            if (axis.sqrMagnitude < 1e-6f)
            {
                return;
            }

            axis.Normalize();
            Vector3 gauze = hand.TransformPoint(CoverItem.GauzeCenterLocal);
            Vector3 axisPoint = hand.position + axis * Vector3.Dot(gauze - hand.position, axis);
            Vector3 v = Vector3.ProjectOnPlane(point - axisPoint, axis);
            float radius = v.magnitude;
            float along = Vector3.Dot(point - axisPoint, axis);
            float near = Vector3.Distance(point, axisPoint);

            if (radius > reach || Mathf.Abs(along) > 0.09f)
            {
                tracking = false;
                turned = 0f;
                radiusSum = 0f;
                radiusCount = 0;
                if (near < 0.35f)
                {
                    hint.Show(axisPoint, "Da una vuelta\nalrededor de su mano", 0f);
                    if (arm != null && arm.Current != PatientBurnArm.Mode.InWater)
                    {
                        arm.ShowHand(1.5f);
                    }
                }
                else
                {
                    hint.Hide();
                }

                return;
            }

            if (arm != null && arm.Current != PatientBurnArm.Mode.InWater)
            {
                arm.ShowHand(1.5f);
            }

            // Angulo alrededor del eje de la mano.
            Vector3 e1 = Vector3.ProjectOnPlane(hand.forward, axis);
            if (e1.sqrMagnitude < 1e-6f)
            {
                e1 = Vector3.ProjectOnPlane(Vector3.up, axis);
            }

            e1.Normalize();
            Vector3 e2 = Vector3.Cross(axis, e1);
            float angle = Mathf.Atan2(Vector3.Dot(v, e2), Vector3.Dot(v, e1)) * Mathf.Rad2Deg;
            if (tracking)
            {
                turned += Mathf.DeltaAngle(lastAngle, angle);
                radiusSum += radius;
                radiusCount++;
            }

            tracking = true;
            lastAngle = angle;

            float progress = Mathf.Clamp01(Mathf.Abs(turned) / turnDegrees);
            hint.Show(axisPoint, "Da una vuelta\nalrededor, suelta", progress);
            if (Time.time >= nextTick && BurnHaptics.Current != null)
            {
                nextTick = Time.time + 0.12f;
                BurnHaptics.Current.Buzz(heldLeft ? 0 : 1, 0.15f + 0.3f * progress, 0.05f, 0f, 0.6f);
            }

            if (progress >= 1f && radiusCount > 0)
            {
                Finish(radiusSum / radiusCount < tightRadius);
            }
        }

        private void Reset()
        {
            tracking = false;
            turned = 0f;
            radiusSum = 0f;
            radiusCount = 0;
            if (hint != null)
            {
                hint.Hide();
            }
        }

        private void Finish(bool tight)
        {
            done = true;
            hint.Hide();
            SimpleGrab.ForceRelease(body);
            Transform hand = patient.HandBone(true);

            // Banda de venda alrededor de la mano, sobre la gasa (mas pegada
            // si quedo apretada).
            HandFrame frame = HandFrame.Build(patient, true, 0.45f, "Body");
            if (frame != null)
            {
                Vector3 c = frame.ToBind(hand.TransformPoint(CoverItem.GauzeCenterLocal));
                Vector3 longAxis = frame.Long;
                float half = halfWidth * frame.UnitsPerMeter;
                SkinnedMeshRenderer band = HandSkinMesh.Build(patient, true, 0.45f, tight ? 0.004f : 0.007f, "Body",
                    "VendaMano", "Gasa_Puesta", (center, normal) => Mathf.Abs(Vector3.Dot(center - c, longAxis)) < half);
                CoverItem.PlanarUV(band, c, frame.Long, frame.Wide, 0.08f * frame.UnitsPerMeter);
            }

            Hito1FX.PlayAt("tela_roce_2", hand.position, 0.7f);
            if (burnSequence != null)
            {
                burnSequence.Report(BurnSequenceManager.BurnEvent.BandageApplied, tight ? "Apretada" : "Suelta",
                    tight ? BurnSequenceManager.Severity.Error : BurnSequenceManager.Severity.None);
            }

            if (arm != null)
            {
                arm.EndShow();
            }

            Destroy(gameObject);
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
    }
}
