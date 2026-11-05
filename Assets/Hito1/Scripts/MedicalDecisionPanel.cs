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
            // Hay que mirar la mano para decidir: el panel no la tapa.
            PanelFollow.Add(root, 0.55f, 0.2f, Size);
            MedicalDecisionPanel panel = root.gameObject.AddComponent<MedicalDecisionPanel>();
            panel.protocol = protocol;
            current = panel;
            panel.Build();
            Hito1FX.PlayAt("logro_paso", root.position, 0.4f, 0f);
            return panel;
        }

        private static readonly Vector2 Size = new Vector2(0.56f, 0.36f);

        private void Build()
        {
            Transform t = transform;
            RoundedPanel bg = RoundedPanel.Create(t, "FondoDecision", Vector2.zero, Size, 0.002f, 0.035f, 0.0025f, 0.02f);
            bg.top = new Color(0.1f, 0.14f, 0.24f, 0.96f);
            bg.bottom = new Color(0.05f, 0.07f, 0.14f, 0.96f);
            bg.Push();
            VRPanelKit.Text(t, "¿Necesita atención médica?", new Vector2(0f, 0.15f), 0.034f, Color.white,
                TextAnchor.UpperCenter, TextAlignment.Center);
            VRPanelKit.Text(t, VRPanelKit.Wrap("Mira la quemadura antes de decidir: su color, si tiene ampollas y dónde está.", 44),
                new Vector2(0f, 0.095f), 0.02f, new Color(0.8f, 0.85f, 0.92f), TextAnchor.UpperCenter,
                TextAlignment.Center);

            VRPanelKit.RoundedButton(t, "Ir al centro de salud", new Vector2(-0.135f, -0.08f), new Vector2(0.25f, 0.11f),
                new Color(0.16f, 0.42f, 0.78f), () => Choose(true));
            VRPanelKit.RoundedButton(t, "Cuidarla en casa", new Vector2(0.135f, -0.08f), new Vector2(0.25f, 0.11f),
                new Color(0.2f, 0.55f, 0.38f), () => Choose(false));
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
