using UnityEngine;

namespace SojaExiles
{
    // Boton de un panel que se aprieta tocandolo con la mano (la esfera
    // fisica de SimpleGrab), igual que la llave del grifo. Se ilumina al
    // tocarlo, hace clic y vibra.
    [RequireComponent(typeof(BoxCollider))]
    public class TouchButton : MonoBehaviour
    {
        public System.Action OnPressed;
        public Renderer background;
        public Color idleColor = new Color(0.16f, 0.42f, 0.78f);
        public Color pressedColor = new Color(0.45f, 0.75f, 1f);
        public float cooldown = 0.8f;
        [Tooltip("Segundos desde que aparece hasta que se puede apretar (evita toques sin querer).")]
        public float armDelay = 0.8f;

        private float enabledTime;
        private float nextPress;
        private float flashUntil;

        private void OnEnable()
        {
            enabledTime = Time.time;
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Update()
        {
            IndicatorUtil.SetColor(background, Time.time < flashUntil ? pressedColor : idleColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!SimpleGrab.IsHandCollider(other) || Time.time < nextPress || Time.time - enabledTime < armDelay)
            {
                return;
            }

            nextPress = Time.time + cooldown;
            flashUntil = Time.time + 0.25f;
            Hito1FX.PlayAt("perilla_clic", transform.position, 0.7f, 0.02f);
            BurnHaptics haptics = FindObjectOfType<BurnHaptics>();
            if (haptics != null)
            {
                haptics.Buzz(-1, 0.4f, 0.05f);
            }

            OnPressed?.Invoke();
        }
    }
}
