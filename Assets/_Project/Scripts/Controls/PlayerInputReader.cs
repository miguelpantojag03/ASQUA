using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.Controls
{
    /// <summary>
    /// Lector central de entrada del jugador (teclado + ratón y mando).
    /// Las acciones se definen por código para no depender de un asset .inputactions
    /// y se exponen ya procesadas para el resto de sistemas (movimiento, cámara, Farol).
    /// En fases futuras (diálogos, menús) basta con llamar a SetGameplayEnabled(false).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Cámara")]
        [Tooltip("Grados de giro por píxel de movimiento del ratón.")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [Tooltip("Grados por segundo con el stick derecho al máximo.")]
        [SerializeField] private float gamepadLookSpeed = 150f;
        [SerializeField] private bool invertY = false;

        [Header("Zoom")]
        [Tooltip("Pasos de zoom por segundo al mantener la cruceta del mando.")]
        [SerializeField] private float gamepadZoomSpeed = 4f;

        private InputActionMap gameplayMap;
        private InputAction moveAction;
        private InputAction lookMouseAction;
        private InputAction lookStickAction;
        private InputAction sprintAction;
        private InputAction lanternAction;
        private InputAction radiusUpAction;
        private InputAction radiusDownAction;
        private InputAction zoomScrollAction;
        private InputAction zoomPadAction;
        private InputAction interactAction;
        private InputAction pulseAction;

        /// <summary>Movimiento normalizado (x = lateral, y = adelante).</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Giro de cámara en grados para este frame (x = yaw, y = pitch).</summary>
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        /// <summary>True mientras se mantiene el botón del Farol.</summary>
        public bool LanternHeld { get; private set; }
        public bool RadiusUpPressed { get; private set; }
        public bool RadiusDownPressed { get; private set; }
        /// <summary>Cambio de distancia de cámara en "pasos" (negativo = acercar).</summary>
        public float ZoomDelta { get; private set; }
        public bool GameplayEnabled => gameplayMap != null && gameplayMap.enabled;
        /// <summary>True mientras se mantiene Interactuar (E / botón A).</summary>
        public bool InteractHeld { get; private set; }
        /// <summary>Pulso de luz pulsado este frame (R / botón Y).</summary>
        public bool PulsePressed { get; private set; }
        /// <summary>
        /// Cerca de un NPC la tecla E sirve para hablar: mientras sea true, E no cambia el tamaño del Farol
        /// (Q y los bumpers siguen funcionando).
        /// </summary>
        public bool BlockRadiusUp { get; set; }

        private void Awake()
        {
            CreateActions();
        }

        private void OnEnable()
        {
            gameplayMap?.Enable();
        }

        private void OnDisable()
        {
            gameplayMap?.Disable();
            ClearValues();
        }

        private void OnDestroy()
        {
            gameplayMap?.Dispose();
        }

        private void CreateActions()
        {
            gameplayMap = new InputActionMap("Gameplay");

            // Movimiento: WASD, flechas y stick izquierdo.
            moveAction = gameplayMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            // Cámara: ratón y stick derecho por separado (unidades distintas).
            lookMouseAction = gameplayMap.AddAction("LookMouse", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            lookStickAction = gameplayMap.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            // Correr: Shift, L3 o B/Círculo.
            sprintAction = gameplayMap.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");
            sprintAction.AddBinding("<Gamepad>/buttonEast");

            // Farol activo: click izquierdo, F o gatillo derecho.
            lanternAction = gameplayMap.AddAction("Lantern", InputActionType.Button, "<Mouse>/leftButton");
            lanternAction.AddBinding("<Keyboard>/f");
            lanternAction.AddBinding("<Gamepad>/rightTrigger");

            // Tamaño del Farol: Q/E o bumpers.
            radiusUpAction = gameplayMap.AddAction("RadiusUp", InputActionType.Button, "<Keyboard>/e");
            radiusUpAction.AddBinding("<Gamepad>/rightShoulder");
            radiusDownAction = gameplayMap.AddAction("RadiusDown", InputActionType.Button, "<Keyboard>/q");
            radiusDownAction.AddBinding("<Gamepad>/leftShoulder");

            // Distancia de cámara: rueda del ratón o cruceta arriba/abajo.
            zoomScrollAction = gameplayMap.AddAction("ZoomScroll", InputActionType.Value, "<Mouse>/scroll/y", expectedControlLayout: "Axis");
            zoomPadAction = gameplayMap.AddAction("ZoomPad", InputActionType.Value, expectedControlLayout: "Axis");
            zoomPadAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Gamepad>/dpad/up")
                .With("Positive", "<Gamepad>/dpad/down");

            // Interactuar (mantener): E o botón A / Cruz.
            interactAction = gameplayMap.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonSouth");

            // Pulso de luz: R o botón Y / Triángulo.
            pulseAction = gameplayMap.AddAction("Pulse", InputActionType.Button, "<Keyboard>/r");
            pulseAction.AddBinding("<Gamepad>/buttonNorth");
        }

        private void Update()
        {
            if (!GameplayEnabled)
            {
                ClearValues();
                return;
            }

            Move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

            // El stick se escala por tiempo; el ratón ya es un delta por frame.
            Vector2 look = lookStickAction.ReadValue<Vector2>() * (gamepadLookSpeed * Time.deltaTime);
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                look += lookMouseAction.ReadValue<Vector2>() * mouseSensitivity;
            }
            if (invertY) look.y = -look.y;
            LookDelta = look;

            SprintHeld = sprintAction.IsPressed();
            LanternHeld = lanternAction.IsPressed();
            RadiusUpPressed = radiusUpAction.WasPressedThisFrame() && !(BlockRadiusUp && Keyboard.current != null && Keyboard.current.eKey.isPressed);
            InteractHeld = interactAction.IsPressed();
            PulsePressed = pulseAction.WasPressedThisFrame();
            RadiusDownPressed = radiusDownAction.WasPressedThisFrame();

            // La rueda se trata como pasos discretos (los valores crudos varían según plataforma).
            float scroll = zoomScrollAction.ReadValue<float>();
            float zoom = Mathf.Abs(scroll) > 0.01f ? -Mathf.Sign(scroll) : 0f;
            zoom += zoomPadAction.ReadValue<float>() * gamepadZoomSpeed * Time.deltaTime;
            ZoomDelta = zoom;
        }

        /// <summary>Activa o desactiva todo el control de juego (útil para diálogos y menús).</summary>
        public void SetGameplayEnabled(bool enabledState)
        {
            if (gameplayMap == null) return;
            if (enabledState) gameplayMap.Enable();
            else
            {
                gameplayMap.Disable();
                ClearValues();
            }
        }

        private void ClearValues()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            SprintHeld = false;
            LanternHeld = false;
            RadiusUpPressed = false;
            RadiusDownPressed = false;
            ZoomDelta = 0f;
            InteractHeld = false;
            PulsePressed = false;
        }
    }
}
