using System;
using Ascua.Controls;
using Ascua.Core;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Esquiva corta (Ctrl / botón B): un deslizamiento rápido en la dirección del movimiento,
    /// con cooldown y coste de Stamina. Pensada para zafarse del Heraldo: mientras dura,
    /// su abrazo no drena el Farol. Deja una estela de chispas, sin espectacularidad de acción.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerDash : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private PlayerJump jumpFx;
        [SerializeField] private ParticleSystem trail;

        [Header("Esquiva")]
        [SerializeField] private float distance = 3.2f;
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private float cooldown = 1.1f;
        [SerializeField] private float staminaCost = 22f;

        public event Action Dashed;

        private float cooldownTimer;
        private float trailTimer;

        public bool IsDashing => controller != null && controller.IsDashing;
        public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();
            if (jumpFx == null) jumpFx = GetComponent<PlayerJump>();
        }

        private void Update()
        {
            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

            // Estela mientras dura el impulso.
            if (trailTimer > 0f)
            {
                trailTimer -= Time.deltaTime;
                if (trail != null) trail.Emit(2);
            }

            if (input == null || !input.DashPressed) return;
            TryDash();
        }

        /// <summary>Lanza la esquiva si es posible.</summary>
        public bool TryDash()
        {
            if (cooldownTimer > 0f || controller == null || controller.IsBusy) return false;
            if (stamina != null && !stamina.TryConsume(staminaCost)) return false;

            Vector3 direction = controller.DesiredDirection.sqrMagnitude > 0.01f ? controller.DesiredDirection : transform.forward;
            controller.Dash(direction, distance, duration);
            cooldownTimer = cooldown;
            trailTimer = duration;
            if (jumpFx != null) jumpFx.PlayLean(14f, duration + 0.1f);

            EventBus.RaisePlayerDashed(transform.position);
            Dashed?.Invoke();
            return true;
        }
    }
}
