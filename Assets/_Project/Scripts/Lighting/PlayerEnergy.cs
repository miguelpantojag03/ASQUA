using System;
using UnityEngine;

namespace Ascua.Lighting
{
    /// <summary>
    /// Energía del Farol (0-100). Separada del propio Farol para que en fases futuras
    /// otros sistemas (Pulso de luz, luciérnagas, Recuerdos) puedan consumirla o restaurarla.
    /// Si llega a 0 entra en "agotamiento" hasta recuperar un porcentaje mínimo.
    /// En combate no hay barra de vida: el daño de las sombras resta energía (ApplyDamage).
    /// Agotada, Ilse no muere: no puede atacar ni iluminar hasta recuperar el 25 %.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEnergy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxEnergy = 100f;
        [SerializeField] private float startEnergy = 100f;

        [Header("Regeneración pasiva")]
        [Tooltip("Energía recuperada por segundo cuando el Farol no se usa.")]
        [SerializeField] private float regenPerSecond = 9f;
        [Tooltip("Segundos sin consumir antes de empezar a regenerar.")]
        [SerializeField] private float regenDelay = 0.8f;

        [Header("Agotamiento")]
        [Tooltip("Fracción de energía necesaria para salir del agotamiento.")]
        [SerializeField, Range(0f, 1f)] private float exhaustedRecoverFraction = 0.25f;

        [Header("Energía crítica")]
        [Tooltip("Por debajo de esta fracción la energía es crítica (aviso fuerte).")]
        [SerializeField, Range(0f, 1f)] private float criticalFraction = 0.2f;
        [Tooltip("Hay que superar esta fracción para salir del estado crítico (evita parpadeos).")]
        [SerializeField, Range(0f, 1f)] private float criticalRecoverFraction = 0.25f;

        private float current;
        private float lastConsumeTime = -999f;
        private bool exhausted;
        private bool critical;

        /// <summary>(actual, máximo)</summary>
        public event Action<float, float> EnergyChanged;
        public event Action<bool> ExhaustedChanged;
        /// <summary>Daño recibido de una sombra (cantidad realmente perdida).</summary>
        public event Action<float> Damaged;

        public float Current => current;
        public float Max => maxEnergy;
        public float Normalized => current / maxEnergy;
        public bool IsExhausted => exhausted;
        /// <summary>Energía bajo el 20 %: borde de pantalla, Farol y sonido avisan con fuerza.</summary>
        public bool IsCritical => critical;
        public event Action<bool> CriticalChanged;
        public bool CanConsume => !exhausted && current > 0f;

        private void Awake()
        {
            current = Mathf.Clamp(startEnergy, 0f, maxEnergy);
        }

        private void Start()
        {
            EnergyChanged?.Invoke(current, maxEnergy);
        }

        private void Update()
        {
            if (Time.time - lastConsumeTime < regenDelay || current >= maxEnergy) return;
            // Las mejoras de "Brasa viva" aceleran la recuperación.
            SetEnergy(current + regenPerSecond * Ascua.Player.PlayerUpgrades.RegenMultiplier * Time.deltaTime);
        }

        /// <summary>Consume energía. Devuelve false si no se pudo consumir.</summary>
        public bool Consume(float amount)
        {
            if (amount <= 0f) return true;
            if (!CanConsume) return false;

            lastConsumeTime = Time.time;
            SetEnergy(current - amount);
            if (current <= 0f) SetExhausted(true);
            return true;
        }

        /// <summary>
        /// Daño de una criatura de sombra: resta energía aunque esté agotada y retrasa la regeneración.
        /// Devuelve la energía realmente perdida.
        /// </summary>
        public float ApplyDamage(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = current;
            lastConsumeTime = Time.time;
            SetEnergy(current - amount);
            if (current <= 0f) SetExhausted(true);
            float lost = before - current;
            if (lost > 0f) Damaged?.Invoke(lost);
            return lost;
        }

        /// <summary>Restaura energía (luciérnagas, Recuerdos... en fases futuras).</summary>
        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            SetEnergy(current + amount);
        }

        private void SetEnergy(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, maxEnergy);
            if (Mathf.Approximately(clamped, current)) return;

            current = clamped;
            if (exhausted && current >= maxEnergy * exhaustedRecoverFraction) SetExhausted(false);
            UpdateCritical();
            EnergyChanged?.Invoke(current, maxEnergy);
        }

        private void UpdateCritical()
        {
            bool next = critical ? current < maxEnergy * criticalRecoverFraction : current < maxEnergy * criticalFraction;
            if (next == critical) return;
            critical = next;
            CriticalChanged?.Invoke(critical);
            Ascua.Core.EventBus.RaiseEnergyCritical(critical);
        }

        private void SetExhausted(bool value)
        {
            if (exhausted == value) return;
            exhausted = value;
            ExhaustedChanged?.Invoke(value);
        }
    }
}
