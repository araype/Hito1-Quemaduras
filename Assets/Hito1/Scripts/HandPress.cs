using UnityEngine;

namespace SojaExiles
{
    // Apretar un control de la cocina (perilla, llave del grifo) como en la
    // vida real: acercar la mano y apretar (boton lateral o gatillo del
    // control, o la pinza con la mano). Antes bastaba con tocarlos y se
    // accionaban sin querer al pasar la mano.
    // Cuenta el momento de apretar: si se llega con la mano ya apretada, hay
    // que soltar y volver a apretar. Con esa mano sosteniendo algo no cuenta.
    // No es un componente: lo usan StoveKnob y WaterTap.
    public class HandPress
    {
        public float reach;

        private readonly bool[] wasPressed = new bool[2];
        private int lastNear = -1;

        // Mano a su alcance este cuadro (0 izquierda, 1 derecha, -1 ninguna).
        public int Near { get; private set; } = -1;

        public HandPress(float reach)
        {
            this.reach = reach;
        }

        // Devuelve la mano que apreto este cuadro junto a 'center' (o -1).
        // 'otherCloser' descarta el punto si otro control esta mas cerca (las
        // dos llaves del grifo estan juntas).
        public int Update(Vector3 center, System.Func<Vector3, bool> otherCloser = null)
        {
            int result = -1;
            Near = -1;
            for (int h = 0; h < 2; h++)
            {
                if (!SimpleGrab.TryGetHandInput(h == 0, out Vector3 point, out bool pressed, out bool holding))
                {
                    wasPressed[h] = false;
                    continue;
                }

                // La mano que se ve puede quedar frenada en la paciente (su mano
                // bajo el chorro esta junto a las llaves): vale el punto de la
                // mano real si esta mas cerca.
                if (SimpleGrab.TryGetRawHandPoint(h == 0, out Vector3 raw)
                    && Vector3.Distance(raw, center) < Vector3.Distance(point, center))
                {
                    point = raw;
                }

                bool inReach = !holding && Vector3.Distance(point, center) < reach
                    && (otherCloser == null || !otherCloser(point));
                if (inReach)
                {
                    Near = h;
                    if (pressed && !wasPressed[h])
                    {
                        result = h;
                    }
                }
                else if (pressed && !wasPressed[h] && Vector3.Distance(point, center) < reach * 2.5f)
                {
                    // Para revisar en el simulador por que no respondio.
                    Debug.Log($"[HandPress] Apreto cerca pero no cuenta: a {Vector3.Distance(point, center) * 100f:F1} cm "
                        + $"(alcance {reach * 100f:F0} cm), sosteniendo algo: {holding}, otro control mas cerca: "
                        + (otherCloser != null && otherCloser(point)));
                }

                wasPressed[h] = pressed;
            }

            // Al llegar al control, un toque leve en el control (o la mano).
            if (Near >= 0 && Near != lastNear)
            {
                Buzz(Near, 0.15f, 0.02f);
            }

            lastNear = Near;
            if (result >= 0)
            {
                Buzz(result, 0.5f, 0.06f);
            }

            return result;
        }

        public bool Contains(Vector3 point, Vector3 center)
        {
            return Vector3.Distance(point, center) < reach;
        }

        private static void Buzz(int hand, float amplitude, float seconds)
        {
            if (BurnHaptics.Current != null)
            {
                BurnHaptics.Current.Buzz(hand, amplitude, seconds);
            }
        }
    }
}
