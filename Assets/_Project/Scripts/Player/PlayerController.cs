using Ascua.Controls;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Movimiento en tercera persona de Ilse-recuerdo, relativo a la cámara.
    /// Usa CharacterController: sin físicas complejas, predecible y barato.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Si se deja vacío se usa Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Velocidades (m/s)")]
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 5f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float deceleration = 14f;

        [Header("Giro y gravedad")]
        [SerializeField] private float turnSmoothTime = 0.1f;
        [SerializeField] private float gravity = -20f;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.forward;
        private float turnVelocity;
        private float verticalVelocity;
        private bool movementEnabled = true;

        public float CurrentSpeed { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public bool IsMoving => CurrentSpeed > 0.05f;

        /// <summary>
        /// Velocidad normalizada para el Animator: 0 = quieto, 0.5 = caminar, 1 = correr.
        /// </summary>
        public float NormalizedAnimSpeed
        {
            get
            {
                if (CurrentSpeed <= walkSpeed) return 0.5f * (CurrentSpeed / walkSpeed);
                return 0.5f + 0.5f * Mathf.InverseLerp(walkSpeed, runSpeed, CurrentSpeed);
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            Vector2 moveInput = movementEnabled && input != null ? input.Move : Vector2.zero;

            // Dirección deseada relativa a la cámara, proyectada sobre el suelo.
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 desired = forward * moveInput.y + right * moveInput.x;
            float inputMagnitude = Mathf.Clamp01(desired.magnitude);

            float targetSpeed = 0f;
            if (inputMagnitude > 0.01f)
            {
                bool running = input != null && input.SprintHeld;
                targetSpeed = (running ? runSpeed : walkSpeed) * inputMagnitude;
                moveDirection = desired.normalized;

                // Giro suave hacia la dirección de movimiento.
                float targetYaw = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            float rate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);

            // Gravedad simple para mantener el personaje pegado al suelo.
            if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = moveDirection * CurrentSpeed + Vector3.up * verticalVelocity;
            characterController.Move(motion * Time.deltaTime);
        }

        /// <summary>Permite congelar el movimiento (cinemáticas, diálogos en fases futuras).</summary>
        public void SetMovementEnabled(bool value)
        {
            movementEnabled = value;
        }
    }
}
