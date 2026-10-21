using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Remedio casero que NO se debe usar en una quemadura (distractor): hielo,
    // mantequilla, pasta dental, aceite o algodon. Si el jugador lo acerca a
    // la mano quemada, la paciente la aparta y protesta (error). Si lo pone
    // sobre la quemadura, queda una capa sobre la piel (grasa, pasta, frio o
    // pelusa) y la paciente grita (error critico; el algodon es error comun).
    // La grasa y la pasta se van de a poco con el agua del grifo.
    [RequireComponent(typeof(Rigidbody))]
    public class HomeRemedy : MonoBehaviour
    {
        public enum Remedy
        {
            Hielo,
            Mantequilla,
            PastaDental,
            Aceite,
            Algodon
        }

        public Remedy remedy;
        public BurnSequenceManager burnSequence;
        [Tooltip("A esta distancia de la mano quemada la paciente la aparta.")]
        public float nearDistance = 0.2f;
        [Tooltip("A esta distancia (o soltado aqui) cuenta como aplicado sobre la quemadura.")]
        public float applyDistance = 0.05f;

        public bool WasNear { get; private set; }
        public bool WasApplied { get; private set; }

        // El remedio que se acerco por ultima vez (la paciente aparta la mano
        // de el).
        public static HomeRemedy LastNear { get; private set; }

        private static readonly Dictionary<Remedy, Smear> Smears = new Dictionary<Remedy, Smear>();

        private Grabbable grabbable;
        private Collider[] colliders;
        private bool wasHeld;

        private void Start()
        {
            grabbable = GetComponent<Grabbable>();
            colliders = GetComponentsInChildren<Collider>();
            if (burnSequence == null)
            {
                burnSequence = FindObjectOfType<BurnSequenceManager>();
            }
        }

        private void OnDestroy()
        {
            if (LastNear == this)
            {
                LastNear = null;
            }
        }

        public bool IsCritical
        {
            get { return remedy != Remedy.Algodon; }
        }

        private void Update()
        {
            UpdateSmears();
            bool held = grabbable != null && grabbable.IsHeld;
            bool released = wasHeld && !held;
            wasHeld = held;

            if (burnSequence == null || (!held && !released))
            {
                return;
            }

            BurnSequenceManager.BurnState state = burnSequence.CurrentState;
            if (state == BurnSequenceManager.BurnState.Idle)
            {
                return;
            }

            PatientBody patient = FindObjectOfType<PatientBody>();
            Transform hand = patient != null && patient.IsReady ? patient.HandBone(true) : null;
            if (hand == null)
            {
                return;
            }

            float distance = Distance(hand.position);
            if (!WasNear && distance < nearDistance)
            {
                WasNear = true;
                LastNear = this;
                burnSequence.Report(BurnSequenceManager.BurnEvent.RemedyNear, remedy.ToString(),
                    IsCritical ? BurnSequenceManager.Severity.Error : BurnSequenceManager.Severity.None);
            }

            if (!WasApplied && distance < applyDistance)
            {
                WasApplied = true;
                LastNear = this;
                Apply(patient, hand);
            }
        }

        private void Apply(PatientBody patient, Transform hand)
        {
            Hito1FX.PlayAt(remedy == Remedy.Hielo ? "hielo_choque" : "tela_roce_3", hand.position, 0.7f);
            if (!Smears.TryGetValue(remedy, out Smear smear) || smear.renderer == null)
            {
                smear = new Smear(patient, remedy);
                Smears[remedy] = smear;
            }

            smear.amount = 1f;
            burnSequence.Report(BurnSequenceManager.BurnEvent.RemedyApplied, remedy.ToString(),
                IsCritical ? BurnSequenceManager.Severity.Critical : BurnSequenceManager.Severity.Error);
        }

        private float Distance(Vector3 point)
        {
            float best = Vector3.Distance(transform.position, point);
            foreach (Collider col in colliders)
            {
                if (col != null && col.enabled && !col.isTrigger)
                {
                    best = Mathf.Min(best, Vector3.Distance(col.ClosestPoint(point), point));
                }
            }

            return best;
        }

        // Capa del remedio sobre la piel de la mano: el hielo se derrite
        // solo; la grasa y la pasta se van con el agua (PatientWetHand dice si
        // la mano esta mojada); el algodon queda pegado.
        private class Smear
        {
            public readonly SkinnedMeshRenderer renderer;
            public float amount;
            private readonly Remedy remedy;
            private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            private readonly Color baseColor;

            public Smear(PatientBody patient, Remedy remedy)
            {
                this.remedy = remedy;
                renderer = HandSkinMesh.Build(patient, true, 0.55f, 0.0025f, "Body", "Remedio" + remedy,
                    "Remedio_" + remedy);
                baseColor = renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color")
                    ? renderer.sharedMaterial.color
                    : Color.white;
            }

            public void Update(float dt, PatientWetHand wet)
            {
                if (renderer == null)
                {
                    return;
                }

                if (remedy == Remedy.Hielo)
                {
                    amount = Mathf.MoveTowards(amount, 0f, dt / 25f);
                }
                else if (remedy != Remedy.Algodon && wet != null && wet.Wetness > 0.8f)
                {
                    amount = Mathf.MoveTowards(amount, 0f, dt / 8f);
                }

                renderer.enabled = amount > 0.01f;
                block.SetColor("_Color", new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * amount));
                renderer.SetPropertyBlock(block);
            }
        }

        private static int smearFrame = -1;
        private static PatientWetHand wetHand;

        // Una vez por frame, aunque haya varios remedios en la escena.
        private static void UpdateSmears()
        {
            if (smearFrame == Time.frameCount || Smears.Count == 0)
            {
                return;
            }

            smearFrame = Time.frameCount;
            if (wetHand == null)
            {
                wetHand = FindObjectOfType<PatientWetHand>();
            }

            foreach (Smear smear in Smears.Values)
            {
                smear.Update(Time.deltaTime, wetHand);
            }
        }
    }
}
