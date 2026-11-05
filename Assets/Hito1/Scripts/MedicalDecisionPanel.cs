using UnityEngine;

namespace SojaExiles
{
    // Ultimo paso del protocolo: despues de cubrir la quemadura, decidir si
    // la persona necesita atencion medica. Aparece frente al jugador un panel
    // con la pregunta y dos botones que se tocan con la mano. Para decidir
    // hay que mirar la quemadura: con ampollas en la mano (segundo grado) va
    // al centro de salud; una de primer grado pequena se cuida en casa.
    public class MedicalDecisionPanel : MonoBehaviour
    {
        private BurnProtocol protocol;
        private static MedicalDecisionPanel current;

        // True mientras el panel esta abierto (la guia oculta su tarjeta,
        // que quedaba encima).
        public static bool IsOpen
        {
            get { return current != null; }
        }

        public static MedicalDecisionPanel Show(BurnProtocol protocol)
        {
            Transform root = VRPanelKit.CreateRoot("PanelDecisionMedica", 0.55f, 0.25f);
            MedicalDecisionPanel panel = root.gameObject.AddComponent<MedicalDecisionPanel>();
            panel.protocol = protocol;
            current = panel;
            panel.Build();
            Hito1FX.PlayAt("logro_paso", root.position, 0.4f, 0f);
            return panel;
        }

        private void Build()
        {
            Transform t = transform;
            // Mismo estilo que la tarjeta del narrador (VRPanelKit.CardPanel).
            const float left = -0.255f;
            VRPanelKit.CardPanel(t, Vector2.zero, new Vector2(0.56f, 0.32f));
            VRPanelKit.Text(t, "Paso 8 de 8  ·  Último paso", new Vector2(left, 0.138f), 0.016f,
                VRPanelKit.CardLabelColor);
            VRPanelKit.BoldText(t, "¿Necesita atención médica?", new Vector2(left, 0.113f), 0.034f, Color.white);
            VRPanelKit.Text(t, VRPanelKit.Wrap("Mira la quemadura antes de decidir: su color, si tiene ampollas y dónde está.", 54),
                new Vector2(left, 0.058f), 0.019f, VRPanelKit.CardHintColor);

            VRPanelKit.RoundedButton(t, "Ir al centro de salud", new Vector2(-0.128f, -0.07f), new Vector2(0.24f, 0.085f),
                new Color(0.22f, 0.48f, 0.95f), () => Choose(true));
            VRPanelKit.RoundedButton(t, "Cuidarla en casa", new Vector2(0.128f, -0.07f), new Vector2(0.24f, 0.085f),
                new Color(0.2f, 0.62f, 0.42f), () => Choose(false));
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }
        }

        private void Choose(bool goToHealthCenter)
        {
            if (protocol != null)
            {
                protocol.Decide(goToHealthCenter);
            }

            Destroy(gameObject);
        }
    }
}
