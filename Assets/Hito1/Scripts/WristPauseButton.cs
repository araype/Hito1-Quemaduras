using UnityEngine;

namespace SojaExiles
{
    // Pausa sin controles: al mirar la palma izquierda aparece un boton
    // "Pausa" sobre la muneca; se toca con la otra mano y se hace la pinza,
    // como los demas botones (TouchButton). Con controles no aparece: ahi
    // esta el boton de menu del control izquierdo (o mantener B/Y).
    // El gesto de menu del sistema con la mano izquierda (OVRInput.Button.Start
    // en PauseMenu) no se confirmo en el visor; este boton no depende de el.
    // Para que no se active sin querer: solo con la mano izquierda vacia, la
    // palma hacia la cara un momento y la mano cerca; y hay que tocarlo con
    // la otra mano y apretar. No aparece en el panel de inicio, en el resumen
    // ni con la pausa ya abierta (PauseMenu.CanPause).
    // Va en el objeto de BurnSequenceManager (Hito1RuntimeBootstrap).
    public class WristPauseButton : MonoBehaviour
    {
        [Tooltip("Coseno minimo entre la palma y la direccion a los ojos para que aparezca (0,6 = unos 53 grados).")]
        [Range(0f, 1f)] public float showFacing = 0.6f;
        [Tooltip("Por debajo de este coseno se esconde (un poco menos que showFacing para que no parpadee).")]
        [Range(0f, 1f)] public float hideFacing = 0.4f;
        [Tooltip("Distancia maxima de la muneca a los ojos (m).")]
        public float maxDistance = 0.7f;
        [Tooltip("Segundos con la palma hacia la cara antes de aparecer.")]
        public float showDelay = 0.35f;
        [Tooltip("Segundos sin la palma hacia la cara antes de esconderse.")]
        public float hideDelay = 0.3f;
        [Tooltip("Separacion del boton sobre la palma, hacia la cara (m).")]
        public float aboveWrist = 0.035f;
        [Tooltip("Corrimiento del boton desde la muneca hacia el antebrazo (m): lejos de los dedos de esa mano.")]
        public float towardElbow = 0.02f;

        public static WristPauseButton Instance { get; private set; }

        // El boton se ve ahora (lo usa la guia para resaltarlo).
        public bool Visible
        {
            get { return root != null && root.gameObject.activeSelf; }
        }

        public Transform ButtonTransform
        {
            get { return root; }
        }

        private Transform root;
        private Transform head;
        private float facingTime;
        private float awayTime;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        private void Build()
        {
            GameObject go = new GameObject("PausaMuneca");
            go.AddComponent<NoAutoCollider>();
            root = go.transform;
            Vector2 size = new Vector2(0.08f, 0.036f);
            TouchButton button = VRPanelKit.RoundedButton(root, "Pausa", Vector2.zero, size,
                new Color(0.45f, 0.3f, 0.62f), OpenPause);
            button.armDelay = 0.3f;
            go.SetActive(false);
        }

        private void OpenPause()
        {
            PauseMenu menu = FindObjectOfType<PauseMenu>();
            if (menu != null)
            {
                menu.Pause();
            }

            Hide();
        }

        private void Update()
        {
            if (root == null)
            {
                Build();
            }

            if (head == null)
            {
                head = VRPanelKit.Head();
            }

            bool can = head != null && PauseMenu.CanPause && !PauseMenu.Paused;
            bool facing = false;
            Vector3 wrist = Vector3.zero, palm = Vector3.forward, fingers = Vector3.up;
            if (can && SimpleGrab.TryGetPalm(true, out wrist, out palm, out fingers))
            {
                bool holding = SimpleGrab.TryGetHand(true, out Vector3 _, out bool held) && held;
                Vector3 toEyes = head.position - wrist;
                float distance = toEyes.magnitude;
                float dot = distance > 1e-4f ? Vector3.Dot(palm, toEyes / distance) : 0f;
                facing = !holding && distance < maxDistance && dot >= (Visible ? hideFacing : showFacing);
            }

            float dt = Time.unscaledDeltaTime;
            facingTime = facing ? facingTime + dt : 0f;
            awayTime = facing ? 0f : awayTime + dt;

            if (!Visible && facing && facingTime >= showDelay)
            {
                root.gameObject.SetActive(true);
            }
            else if (Visible && (!can || awayTime >= hideDelay))
            {
                Hide();
            }

            if (Visible && facing)
            {
                Vector3 p = wrist + palm * aboveWrist - fingers * towardElbow;
                Vector3 forward = p - head.position;
                Vector3 up = Vector3.ProjectOnPlane(fingers, forward);
                if (up.sqrMagnitude < 1e-6f)
                {
                    up = Vector3.up;
                }

                root.SetPositionAndRotation(p, Quaternion.LookRotation(forward, up));
            }
        }

        private void Hide()
        {
            if (root != null && root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(false);
            }

            facingTime = 0f;
        }
    }
}
