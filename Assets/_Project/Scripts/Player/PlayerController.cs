using System;
using Ascua.Controls;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Motor de movimiento en tercera persona de Ilse-recuerdo, relativo a la cámara (CharacterController).
    /// Estados: Normal (caminar/correr/aire), Dash (impulso corto) y Mantle (trepar un borde).
    /// Los componentes PlayerStamina, PlayerJump, PlayerDash y PlayerMantle deciden CUÁNDO;
    /// este script sabe CÓMO moverse. Ágil pero sin brusquedad: es un recuerdo, no un héroe de acción.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        private enum MotionState { Normal, Dash, Mantle }

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Si se deja vacío se usa Camera.main.")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private PlayerStamina stamina;

        [Header("Velocidades (m/s)")]
        [SerializeField] private float walkSpeed = 2.4f;
        [Tooltip("Velocidad corriendo (gasta Stamina).")]
        [SerializeField] private float runSpeed = 5.2f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float deceleration = 14f;
        [Tooltip("Control en el aire (0 = ninguno, 1 = igual que en el suelo).")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.45f;

        [Header("Giro y gravedad")]
        [SerializeField] private float turnSmoothTime = 0.1f;
        [SerializeField] private float gravity = -20f;
        [Tooltip("Caídas más largas que esto cuentan como aterrizaje (polvo, sonido).")]
        [SerializeField] private float landingMinAirTime = 0.18f;

        public event Action<float> Landed;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.forward;
        private float turnVelocity;
        private float verticalVelocity;
        private bool movementEnabled = true;
        private MotionState state = MotionState.Normal;
        private float slowFactor = 1f;
        private float airTime;
        private bool wasGrounded = true;

        // Dash
        private Vector3 dashVelocity;
        private float dashTimer;

        // Mantle
        private Vector3 mantleStart;
        private Vector3 mantleTarget;
        private float mantleDuration;
        private float mantleTimer;

        public float CurrentSpeed { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public bool IsMoving => CurrentSpeed > 0.05f;
        public bool IsRunning { get; private set; }
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsDashing => state == MotionState.Dash;
        public bool IsMantling => state == MotionState.Mantle;
        public bool IsBusy => state != MotionState.Normal;
        /// <summary>Segundos desde que dejó de tocar el suelo (para el "coyote time" del salto).</summary>
        public float TimeSinceGrounded => airTime;
        /// <summary>Dirección de movimiento deseada este frame (relativa a cámara, plana). Cero si no hay input.</summary>
        public Vector3 DesiredDirection { get; private set; }
        public float VerticalVelocity => verticalVelocity;

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
            if (stamina == null) stamina = GetComponent<PlayerStamina>();
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case MotionState.Dash: UpdateDash(dt); break;
                case MotionState.Mantle: UpdateMantle(dt); break;
                default: UpdateNormal(dt); break;
            }
            // El efecto de ralentización (Heraldo) dura un solo frame: quien lo aplica lo renueva.
            slowFactor = 1f;
        }

        private void UpdateNormal(float dt)
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
            DesiredDirection = inputMagnitude > 0.01f ? desired.normalized : Vector3.zero;

            bool grounded = characterController.isGrounded;
            float targetSpeed = 0f;
            IsRunning = false;
            if (inputMagnitude > 0.01f)
            {
                // Correr: Shift/L3 y Stamina disponible.
                bool wantsRun = input != null && input.SprintHeld;
                bool canRun = stamina == null || stamina.CanSprint;
                IsRunning = wantsRun && canRun && inputMagnitude > 0.5f;
                if (IsRunning && stamina != null) stamina.DrainForSprint(dt);

                targetSpeed = (IsRunning ? runSpeed : walkSpeed) * inputMagnitude * slowFactor;

                // En el aire se gira y se cambia de dirección con menos control.
                float control = grounded ? 1f : airControl;
                moveDirection = Vector3.Slerp(moveDirection, desired.normalized, Mathf.Clamp01(control * 12f * dt) + (grounded ? 1f : 0f)).normalized;

                float targetYaw = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVelocity, turnSmoothTime / Mathf.Max(0.2f, control));
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            float rate = (targetSpeed > CurrentSpeed ? acceleration : deceleration) * (grounded ? 1f : airControl);
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * dt);

            // Gravedad simple para mantener el personaje pegado al suelo.
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            // Integración con la velocidad media del frame: la altura del salto es la misma
            // a 30, 60 o 144 FPS (con la suma simple, a pocos FPS el salto se queda corto).
            float previousVertical = verticalVelocity;
            verticalVelocity += gravity * dt;
            float averageVertical = 0.5f * (previousVertical + verticalVelocity);

            Vector3 motion = moveDirection * CurrentSpeed + Vector3.up * averageVertical;
            characterController.Move(motion * dt);
            TrackGrounding(dt);
        }

        private void TrackGrounding(float dt)
        {
            bool grounded = characterController.isGrounded;
            if (grounded)
            {
                if (!wasGrounded && airTime >= landingMinAirTime) Landed?.Invoke(airTime);
                airTime = 0f;
            }
            else
            {
                airTime += dt;
            }
            wasGrounded = grounded;
        }

        // ------------------------------------------------------------------ API para salto, dash y mantle

        /// <summary>Impulso vertical (salto). Lo usa PlayerJump.</summary>
        public void Jump(float upwardVelocity)
        {
            if (state != MotionState.Normal) return;
            verticalVelocity = upwardVelocity;
            airTime = Mathf.Max(airTime, 0.01f);
            wasGrounded = false;
        }

        /// <summary>Impulso horizontal corto. Lo usa PlayerDash.</summary>
        public void Dash(Vector3 direction, float distance, float duration)
        {
            if (state != MotionState.Normal) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            direction.Normalize();

            state = MotionState.Dash;
            dashTimer = Mathf.Max(0.05f, duration);
            dashVelocity = direction * (distance / dashTimer);
            moveDirection = direction;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            verticalVelocity = Mathf.Max(verticalVelocity, 0f);
        }

        private void UpdateDash(float dt)
        {
            // Nunca se avanza más que el tiempo que le queda a la esquiva: un frame largo
            // (tirón de carga) no puede convertir 3 m en 15 m.
            float step = Mathf.Min(dt, dashTimer);
            dashTimer -= dt;
            characterController.Move((dashVelocity + Vector3.up * -1f) * step);
            if (dashTimer <= 0f)
            {
                state = MotionState.Normal;
                CurrentSpeed = Mathf.Max(CurrentSpeed, walkSpeed);
                verticalVelocity = 0f;
            }
        }

        /// <summary>Sube a un borde: primero en vertical, luego hacia delante. Lo usa PlayerMantle.</summary>
        public void Mantle(Vector3 targetFeetPosition, float duration)
        {
            if (state == MotionState.Mantle) return;
            state = MotionState.Mantle;
            mantleStart = transform.position;
            mantleTarget = targetFeetPosition;
            mantleDuration = Mathf.Max(0.1f, duration);
            mantleTimer = 0f;
            verticalVelocity = 0f;
            CurrentSpeed = 0f;
        }

        private void UpdateMantle(float dt)
        {
            mantleTimer += dt;
            float t = Mathf.Clamp01(mantleTimer / mantleDuration);

            // Curva en "L": sube en el primer 60% y avanza en el último 40% (con suavizado).
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f));
            float forward = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));
            Vector3 position = new Vector3(
                Mathf.Lerp(mantleStart.x, mantleTarget.x, forward),
                Mathf.Lerp(mantleStart.y, mantleTarget.y, up),
                Mathf.Lerp(mantleStart.z, mantleTarget.z, forward));

            // Se mueve sin colisión durante la animación (el destino ya se comprobó libre).
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;

            if (t >= 1f)
            {
                state = MotionState.Normal;
                verticalVelocity = -2f;
                airTime = 0f;
                wasGrounded = true;
            }
        }

        /// <summary>Ralentiza el movimiento este frame (p. ej. el Heraldo cerca). 1 = normal.</summary>
        public void ApplySlow(float factor)
        {
            slowFactor = Mathf.Min(slowFactor, Mathf.Clamp01(factor));
        }

        /// <summary>Permite congelar el movimiento (cinemáticas, diálogos).</summary>
        public void SetMovementEnabled(bool value)
        {
            movementEnabled = value;
        }
    }
}
