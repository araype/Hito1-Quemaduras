using UnityEngine;

namespace SojaExiles
{
    // Seguridad propia (E.8): quien ayuda no debe quemarse tambien.
    //   - Tocar la olla despues de que se volco: esta caliente. Vibra, suena
    //     el siseo, la mano la suelta sola y cuenta como error.
    //   - Pisar el agua caliente derramada en el suelo mientras echa vapor:
    //     cuenta como error (quema y resbala).
    // Cada error se cuenta una sola vez; las veces siguientes solo vibra.
    // Avisa por BurnSequenceManager.Report (HotPotTouched, HotSpillStepped).
    // Va en el objeto de BurnSequenceManager.
    public class PlayerSafety : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;
        [Tooltip("Segundos que la olla sigue caliente despues de volcarse (el metal tarda en enfriarse).")]
        public float potHotSeconds = 600f;
        [Tooltip("Distancia de la mano a la olla que cuenta como tocarla.")]
        public float touchDistance = 0.02f;
        [Tooltip("Fraccion del radio del charco que cuenta como pisarlo (la cabeza puede inclinarse sobre el borde).")]
        public float stepRadiusFraction = 0.7f;
        [Tooltip("Segundos parado sobre el charco hasta que cuenta.")]
        public float stepSeconds = 0.5f;

        public bool TouchedPot { get; private set; }
        public bool SteppedOnSpill { get; private set; }

        private PotAccident accident;
        private Collider[] potColliders;
        private BurnHaptics haptics;
        private Transform head;
        private float onSpillTime;
        private readonly float[] nextTouchFeedback = new float[2];
        private float nextStepFeedback;

        private void Start()
        {
            if (burnSequence == null)
            {
                burnSequence = GetComponent<BurnSequenceManager>();
            }

            accident = FindObjectOfType<PotAccident>();
            if (burnSequence == null || accident == null)
            {
                enabled = false;
                return;
            }

            potColliders = accident.GetComponentsInChildren<Collider>();
            haptics = FindObjectOfType<BurnHaptics>();
        }

        private void Update()
        {
            if (!accident.Spilled)
            {
                return;
            }

            if (Time.time - accident.SpillTime < potHotSeconds)
            {
                CheckHand(0);
                CheckHand(1);
            }

            CheckFeet();
        }

        private void CheckHand(int index)
        {
            if (!SimpleGrab.TryGetHand(index == 0, out Vector3 hand, out bool _))
            {
                return;
            }

            if (PotDistance(hand) > touchDistance)
            {
                return;
            }

            // Se aparta la mano: si la tenia agarrada, la suelta.
            Rigidbody rb = accident.GetComponent<Rigidbody>();
            if (rb != null && SimpleGrab.IsHeldBy(rb, index == 0))
            {
                SimpleGrab.ForceRelease(rb);
            }

            if (Time.time >= nextTouchFeedback[index])
            {
                nextTouchFeedback[index] = Time.time + 1.5f;
                if (haptics != null)
                {
                    haptics.Buzz(index, 0.9f, 0.35f);
                }

                Hito1FX.PlayAt("quemadura_siseo", hand, 0.35f);
            }

            if (!TouchedPot)
            {
                TouchedPot = true;
                burnSequence.Report(BurnSequenceManager.BurnEvent.HotPotTouched, null,
                    BurnSequenceManager.Severity.Error);
            }
        }

        private float PotDistance(Vector3 point)
        {
            float best = float.PositiveInfinity;
            foreach (Collider col in potColliders)
            {
                if (col != null && col.enabled && !col.isTrigger)
                {
                    best = Mathf.Min(best, Vector3.Distance(col.ClosestPoint(point), point));
                }
            }

            return best;
        }

        // Los pies estan debajo de la cabeza.
        private void CheckFeet()
        {
            if (!accident.TryGetHotFloorPuddle(out Vector3 center, out float radius))
            {
                onSpillTime = 0f;
                return;
            }

            if (head == null)
            {
                head = VRPanelKit.Head();
                if (head == null)
                {
                    return;
                }
            }

            Vector3 d = head.position - center;
            d.y = 0f;
            if (d.magnitude > radius * stepRadiusFraction)
            {
                onSpillTime = 0f;
                return;
            }

            onSpillTime += Time.deltaTime;
            if (onSpillTime < stepSeconds)
            {
                return;
            }

            if (Time.time >= nextStepFeedback)
            {
                nextStepFeedback = Time.time + 2f;
                if (haptics != null)
                {
                    haptics.Buzz(-1, 0.5f, 0.25f);
                }

                Hito1FX.PlayAt("gota_2", new Vector3(head.position.x, center.y, head.position.z), 0.8f);
            }

            if (!SteppedOnSpill)
            {
                SteppedOnSpill = true;
                burnSequence.Report(BurnSequenceManager.BurnEvent.HotSpillStepped, null,
                    BurnSequenceManager.Severity.Error);
            }
        }
    }
}
