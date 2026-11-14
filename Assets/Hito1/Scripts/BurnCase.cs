using UnityEngine;

namespace SojaExiles
{
    // Caso de la partida (RF-11): grado, extension y zona de la quemadura,
    // siempre en el dorso de la mano derecha (la mano, el accidente y las
    // poses no cambian). Asi no se puede memorizar una sola quemadura:
    //   - el grado decide la atencion medica (ampollas en la mano: centro de
    //     salud; solo roja: en casa, guia LSEBN);
    //   - la extension cambia cuanto film hace falta (FilmCoverage mide sobre
    //     la mancha), cuantas ampollas salen y que tan rapido se hincha;
    //   - en los nudillos la hinchazon llega antes al anillo: sacarlo es mas
    //     urgente.
    // Se elige al cargar la escena (BurnInjury.Awake): 'Next' si se pidio
    // uno (repetir el caso, otro caso), si no al azar. 'Current' es el de la
    // partida en curso.
    [System.Serializable]
    public class BurnCase
    {
        public enum Size
        {
            Pequena,
            Mediana,
            Grande
        }

        public enum Zone
        {
            Dorso,
            Nudillos
        }

        public BurnInjury.BurnDegree degree = BurnInjury.BurnDegree.SegundoSuperficial;
        public Size size = Size.Mediana;
        public Zone zone = Zone.Dorso;
        // Forma de la mancha (BurnSkinPattern) y lugar de las ampollas: el
        // mismo caso repetido se ve igual.
        public int seed;

        // Caso pedido para la proxima carga de la escena (sobrevive a la
        // recarga y se usa una vez).
        public static BurnCase Next;

        // Caso de la partida en curso (null antes de cargar la paciente).
        public static BurnCase Current { get; private set; }

        public static BurnCase Random()
        {
            return new BurnCase
            {
                degree = UnityEngine.Random.value < 0.5f ? BurnInjury.BurnDegree.Primero : BurnInjury.BurnDegree.SegundoSuperficial,
                size = (Size)UnityEngine.Random.Range(0, 3),
                zone = UnityEngine.Random.value < 0.5f ? Zone.Dorso : Zone.Nudillos,
                seed = UnityEngine.Random.Range(1, 100000)
            };
        }

        // Otro caso al azar que no sea igual al actual (al menos cambia el
        // grado, la extension o la zona).
        public static BurnCase RandomOtherThan(BurnCase current)
        {
            for (int i = 0; i < 20; i++)
            {
                BurnCase c = Random();
                if (current == null || c.degree != current.degree || c.size != current.size || c.zone != current.zone)
                {
                    return c;
                }
            }

            return Random();
        }

        public BurnCase Copy()
        {
            return (BurnCase)MemberwiseClone();
        }

        // Lo llama BurnInjury.Awake: usa el pedido o uno al azar.
        public static BurnCase Choose(bool random, BurnInjury.BurnDegree fallbackDegree)
        {
            BurnCase c;
            if (Next != null)
            {
                c = Next;
                Next = null;
            }
            else if (random)
            {
                c = Random();
            }
            else
            {
                c = new BurnCase { degree = fallbackDegree, seed = UnityEngine.Random.Range(1, 100000) };
            }

            Current = c;
            return c;
        }

        // Radio de la mancha respecto del de BurnSkinOverlay (la mediana).
        // Pequena: una moneda grande; grande: casi todo el dorso.
        public float RadiusFactor
        {
            get { return size == Size.Pequena ? 0.62f : (size == Size.Grande ? 1.45f : 1f); }
        }

        // Cuanto se corre el centro hacia los dedos, en el espacio del hueso
        // de la mano (m).
        public float TowardFingers
        {
            get { return zone == Zone.Nudillos ? 0.03f : 0f; }
        }

        // Ampollas en segundo grado, segun el area.
        public int BlisterCount
        {
            get { return size == Size.Pequena ? 3 : (size == Size.Grande ? 10 : 6); }
        }

        // Multiplica los segundos hasta hincharse del todo (menos = mas
        // rapido). Con margen: en el peor caso (grande en los nudillos) el
        // anillo se traba tras unos 100 s sin agua, no a los 42 s de antes
        // (ver BurnInjury.swellSecondsUntreated).
        public float SwellTimeFactor
        {
            get { return (size == Size.Grande ? 0.9f : (size == Size.Pequena ? 1.1f : 1f)) * (zone == Zone.Nudillos ? 0.85f : 1f); }
        }

        public string DegreeName
        {
            get { return degree == BurnInjury.BurnDegree.Primero ? "primer grado" : "segundo grado superficial"; }
        }

        public string SizeName
        {
            get { return size == Size.Pequena ? "pequeña" : (size == Size.Grande ? "grande" : "mediana"); }
        }

        public string ZoneName
        {
            get { return zone == Zone.Nudillos ? "en los nudillos" : "en el dorso de la mano"; }
        }

        // "segundo grado superficial, grande, en los nudillos"
        public string Describe()
        {
            return DegreeName + ", " + SizeName + ", " + ZoneName;
        }

        public override string ToString()
        {
            return degree + "/" + size + "/" + zone + "/" + seed;
        }
    }
}
