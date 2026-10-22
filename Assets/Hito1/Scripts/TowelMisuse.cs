using UnityEngine;

namespace SojaExiles
{
    // El pano de cocina no sirve para cubrir una quemadura: suelta pelusa que
    // se pega a la piel. Si se deja sobre la mano quemada se avisa como error
    // (una vez cada vez que se apoya). Va en el pano (con TowelCloth).
    [RequireComponent(typeof(TowelCloth))]
    public class TowelMisuse : MonoBehaviour
    {
        public BurnSequenceManager burnSequence;

        private TowelCloth cloth;
        private bool wasOnHand;

        private void Start()
        {
            cloth = GetComponent<TowelCloth>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }
        }

        private void Update()
        {
            bool onHand = cloth.IsOnPatientHand;
            if (onHand && !wasOnHand && burnSequence != null)
            {
                BurnSequenceManager.BurnState s = burnSequence.CurrentState;
                if (s == BurnSequenceManager.BurnState.Burned || s == BurnSequenceManager.BurnState.Cooling
                    || s == BurnSequenceManager.BurnState.Cooled)
                {
                    burnSequence.Report(BurnSequenceManager.BurnEvent.ClothOnBurn, null,
                        BurnSequenceManager.Severity.Error);
                }
            }

            wasOnHand = onHand;
        }
    }
}
