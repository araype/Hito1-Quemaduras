using UnityEngine;

namespace SojaExiles
{
    // Marca un objeto como material de primeros auxilios, para que los
    // detectores de la herida sepan que es lo que la toca sin depender de
    // tags. El chorro de agua del grifo tambien lleva este componente
    // (tipo Agua) sobre su collider trigger. Los valores nuevos se agregan al
    // final: las escenas guardan el numero, no el nombre.
    public class FirstAidItem : MonoBehaviour
    {
        public enum ItemType
        {
            Gasa,
            Agua,
            Venda,
            Guantes,
            Antiseptico,
            Panuelo
        }

        public ItemType type;
    }
}
