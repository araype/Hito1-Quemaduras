using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Objetos que se caen al piso vuelven solos a donde estaban. Antes la
    // taza, el pano, la chompa o la cuchara quedaban en el suelo sin forma
    // de recuperarlos (Grabbable solo los devolvia si caian 3 m, fuera del
    // escenario).
    //
    // Revisa cada medio segundo los objetos sueltos que se pueden agarrar
    // (SimpleGrab o el SDK de Meta). Anota su lugar la primera vez que los
    // ve; si uno queda quieto bastante mas abajo de ese lugar (en el piso) y
    // nadie lo sostiene, vuelve ahi. No toca la olla (despues del accidente
    // queda volcada a proposito), ni la paciente, ni el jugador, ni lo que
    // este fijo (kinematico: la chompa puesta, la gasa sobre la mano).
    // Va en el objeto de BurnSequenceManager (lo instala Hito1RuntimeBootstrap).
    public class FloorRecovery : MonoBehaviour
    {
        [Tooltip("Cuanto mas abajo de su lugar debe quedar para contar como caido (m).")]
        public float dropHeight = 0.35f;
        [Tooltip("Segundos quieto en el piso antes de volver.")]
        public float restSeconds = 1.5f;
        public float checkInterval = 0.5f;

        private class Tracked
        {
            public Vector3 home;
            public Quaternion homeRotation;
            public float onFloorSince = -1f;
        }

        private readonly Dictionary<Rigidbody, Tracked> tracked = new Dictionary<Rigidbody, Tracked>();
        private float nextCheck;
        private PatientBody patient;

        private void Update()
        {
            if (Time.time < nextCheck)
            {
                return;
            }

            nextCheck = Time.time + checkInterval;
            if (patient == null)
            {
                patient = FindObjectOfType<PatientBody>();
            }

            foreach (Rigidbody rb in FindObjectsOfType<Rigidbody>())
            {
                if (!tracked.TryGetValue(rb, out Tracked t))
                {
                    if (!Eligible(rb))
                    {
                        continue;
                    }

                    t = new Tracked { home = rb.position, homeRotation = rb.rotation };
                    tracked[rb] = t;
                }

                Check(rb, t);
            }
        }

        private bool Eligible(Rigidbody rb)
        {
            // Los que piden quedarse en el piso (Grabbable.returnFromFloor
            // apagado: la cuchara caida, el anillo que se saco) no vuelven.
            Grabbable own = rb.GetComponent<Grabbable>();
            if (own != null && !own.returnFromFloor)
            {
                return false;
            }

            bool grabbable = own != null || rb.GetComponent<Oculus.Interaction.Grabbable>() != null;
            return grabbable && !rb.isKinematic
                && rb.GetComponent<PotAccident>() == null
                && (patient == null || !rb.transform.IsChildOf(patient.transform))
                && rb.GetComponentInParent<VRLocomotion>() == null
                && rb.GetComponentInParent<OVRCameraRig>() == null;
        }

        private void Check(Rigidbody rb, Tracked t)
        {
            // Si el objeto tiene Grabbable, su lugar es el de Grabbable: cambia
            // con SetHome (p.ej. la taza que la tia deja en el meson). Antes se
            // usaba el primer lugar visto y la taza que se le caia a la tia
            // volvia a aparecer donde estaba al inicio, como si fuera otra.
            Grabbable g = rb.GetComponent<Grabbable>();
            if (g != null)
            {
                t.home = g.HomePosition;
                t.homeRotation = g.HomeRotation;
            }

            bool onFloor = !rb.isKinematic && rb.position.y < t.home.y - dropHeight
                && rb.velocity.sqrMagnitude < 0.01f && !Held(rb);
            if (!onFloor)
            {
                t.onFloorSince = -1f;
                return;
            }

            if (t.onFloorSince < 0f)
            {
                t.onFloorSince = Time.time;
                return;
            }

            if (Time.time - t.onFloorSince < restSeconds)
            {
                return;
            }

            t.onFloorSince = -1f;
            Vector3 home = t.home + Vector3.up * 0.02f;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = home;
            rb.rotation = t.homeRotation;
            rb.transform.SetPositionAndRotation(home, t.homeRotation);
            Physics.SyncTransforms();

            TowelCloth cloth = rb.GetComponent<TowelCloth>();
            if (cloth != null)
            {
                cloth.Snap();
            }

            Debug.Log("[FloorRecovery] " + rb.name + " estaba en el piso: vuelve a su lugar.");
        }

        private static bool Held(Rigidbody rb)
        {
            if (SimpleGrab.IsHeldObject(rb))
            {
                return true;
            }

            Oculus.Interaction.Grabbable sdk = rb.GetComponent<Oculus.Interaction.Grabbable>();
            return sdk != null && sdk.SelectingPointsCount > 0;
        }
    }
}
