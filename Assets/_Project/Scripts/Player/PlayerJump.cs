using Ascua.Controls;
using Ascua.Core;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Salto simple para subir bordes bajos, bancas o cajas (no plataformeo).
    /// Incluye margen de "coyote" y búfer de pulsación para que responda bien, y una curva de
    /// anticipación / estiramiento / aterrizaje sobre el transform "Visual" (no lo toca el Animator).
    /// Al saltar contra un borde trepable, cede el turno a PlayerMantle.
    /// También ofrece pequeños gestos de cuerpo (inclinación, polvo) a dash y mantle.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerJump : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerMantle mantle;
        [Tooltip("Transform 'Visual' del personaje (para estirar/aplastar).")]
        [SerializeField] private Transform visual;
        [SerializeField] private ParticleSystem landingDust;

        [Header("Salto")]
        [Tooltip("Altura máxima del salto (m).")]
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = 20f;
        [Tooltip("Margen tras salir de un borde en el que aún se puede saltar.")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("Si se pulsa saltar justo antes de tocar suelo, se salta al aterrizar.")]
        [SerializeField] private float jumpBuffer = 0.15f;
        [Tooltip("Anticipación: pequeño agachado antes del impulso (s).")]
        [SerializeField] private float anticipation = 0.07f;

        [Header("Cuerpo (squash & stretch)")]
        [SerializeField] private float squashAmount = 0.16f;
        [SerializeField] private float stretchAmount = 0.1f;
        [SerializeField] private float recoverSpeed = 9f;

        private Vector3 visualBaseScale = Vector3.one;
        private Quaternion visualBaseRotation = Quaternion.identity;
        private float bufferTimer;
        private float anticipationTimer = -1f;
        private float squash;            // >0 aplastado, <0 estirado
        private float leanAngle;
        private float leanTimer;
        private float leanDuration;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (mantle == null) mantle = GetComponent<PlayerMantle>();
            if (visual != null)
            {
                visualBaseScale = visual.localScale;
                visualBaseRotation = visual.localRotation;
            }
        }

        private void OnEnable()
        {
            if (controller != null) controller.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (controller != null) controller.Landed -= OnLanded;
        }

        private void Update()
        {
            if (input == null || controller == null) return;
            float dt = Time.deltaTime;

            if (input.JumpPressed) bufferTimer = jumpBuffer;
            else bufferTimer -= dt;

            // Anticipación en curso: al terminar, impulso.
            if (anticipationTimer >= 0f)
            {
                anticipationTimer -= dt;
                if (anticipationTimer < 0f) Launch();
                return;
            }

            if (bufferTimer <= 0f || controller.IsBusy) return;

            // ¿Hay un borde trepable delante? Trepar se siente mejor que rebotar contra la pared.
            if (mantle != null && mantle.TryMantle())
            {
                bufferTimer = 0f;
                return;
            }

            bool canJump = controller.IsGrounded || controller.TimeSinceGrounded <= coyoteTime;
            if (!canJump) return;

            bufferTimer = 0f;
            anticipationTimer = anticipation;
            squash = squashAmount;
        }

        private void Launch()
        {
            float velocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            controller.Jump(velocity);
            squash = -stretchAmount;
            EventBus.RaisePlayerJumped(transform.position);
        }

        private void OnLanded(float airTime)
        {
            // Cuanto más larga la caída, más se aplasta y más polvo levanta.
            squash = Mathf.Lerp(squashAmount * 0.5f, squashAmount * 1.3f, Mathf.Clamp01(airTime / 0.9f));
            PlayDust(Mathf.RoundToInt(Mathf.Lerp(6f, 16f, Mathf.Clamp01(airTime / 0.9f))));
            EventBus.RaisePlayerLanded(transform.position);
        }

        /// <summary>Nube de polvo/chispas a los pies.</summary>
        public void PlayDust(int count)
        {
            if (landingDust != null) landingDust.Emit(count);
        }

        /// <summary>Inclina el cuerpo unos grados durante un instante (dash, trepar).</summary>
        public void PlayLean(float degrees, float duration)
        {
            leanAngle = degrees;
            leanDuration = Mathf.Max(0.05f, duration);
            leanTimer = leanDuration;
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            float dt = Time.deltaTime;

            // Estirado mientras sube rápido, vuelta a la normalidad suave.
            if (!controller.IsGrounded && controller.VerticalVelocity > 2f && anticipationTimer < 0f) squash = Mathf.Min(squash, -stretchAmount * 0.6f);
            squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-recoverSpeed * dt));

            float y = 1f - squash;
            float xz = 1f + squash * 0.5f;
            visual.localScale = new Vector3(visualBaseScale.x * xz, visualBaseScale.y * y, visualBaseScale.z * xz);

            float lean = 0f;
            if (leanTimer > 0f)
            {
                leanTimer -= dt;
                lean = leanAngle * Mathf.Sin(Mathf.Clamp01(1f - leanTimer / leanDuration) * Mathf.PI);
            }
            visual.localRotation = visualBaseRotation * Quaternion.Euler(lean, 0f, 0f);
        }
    }
}
