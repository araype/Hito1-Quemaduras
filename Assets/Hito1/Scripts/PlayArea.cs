using UnityEngine;

namespace SojaExiles
{
    // Zona de la cocina donde puede caminar el jugador. Antes se podia salir
    // al comedor (y la paciente, llevada del brazo, quedaba en el marco de la
    // puerta). Se arma al dar Play con lo que importa de la escena (cocina,
    // lavadero, paciente y jugador) mas un margen; VRLocomotion no deja que la
    // capsula del jugador camine fuera de ella.
    public static class PlayArea
    {
        public static bool Active { get; private set; }
        public static Vector2 Min { get; private set; }
        public static Vector2 Max { get; private set; }

        public static float margin = 0.9f;

        public static void FromKitchen()
        {
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;

            void Add(Vector3 p)
            {
                Vector2 q = new Vector2(p.x, p.z);
                if (!any)
                {
                    min = max = q;
                    any = true;
                }
                else
                {
                    min = Vector2.Min(min, q);
                    max = Vector2.Max(max, q);
                }
            }

            PotFX pot = Object.FindObjectOfType<PotFX>();
            if (pot != null)
            {
                Add(pot.transform.position);
            }

            foreach (WaterTap tap in Object.FindObjectsOfType<WaterTap>())
            {
                Add(tap.transform.position);
            }

            foreach (PatientBody patient in Object.FindObjectsOfType<PatientBody>())
            {
                Add(patient.transform.position);
            }

            OVRCameraRig rig = Object.FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                Add(rig.transform.position);
            }

            if (!any)
            {
                Active = false;
                return;
            }

            Min = min - Vector2.one * margin;
            Max = max + Vector2.one * margin;
            Active = true;
            Debug.Log("[PlayArea] Zona de juego x " + Min.x.ToString("F2") + ".." + Max.x.ToString("F2")
                + ", z " + Min.y.ToString("F2") + ".." + Max.y.ToString("F2"));
        }

        // Quita del paso la parte que sacaria al punto de la zona.
        public static Vector3 Clip(Vector3 position, Vector3 step)
        {
            if (!Active)
            {
                return step;
            }

            Vector3 next = position + step;
            if ((next.x < Min.x && step.x < 0f) || (next.x > Max.x && step.x > 0f))
            {
                step.x = 0f;
            }

            if ((next.z < Min.y && step.z < 0f) || (next.z > Max.y && step.z > 0f))
            {
                step.z = 0f;
            }

            return step;
        }
    }
}
