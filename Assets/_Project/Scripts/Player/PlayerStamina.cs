using System;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Stamina (0-100): cuánto puede correr y esquivar Ilse. Separada de la Energía del Farol
    /// (cuánto puede iluminar) para no mezclar mecánicas. Se recupera rápido al caminar o parar.
    /// Si se vacía, hay que recuperar un mínimo antes de volver a correr.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] private float maxStamina = 100f;
        [Tooltip("Gasto por segundo corriendo.")]
        [SerializeField] private float sprintDrainPerSecond = 20f;
        [Tooltip("Recuperación por segundo al caminar o estar quieta.")]
        [SerializeField] private float regenPerSecond = 35f;
        [Tooltip("Segundos sin gastar antes de empezar a recuperar.")]
        [SerializeField] private float regenDelay = 0.35f;
        [Tooltip("Tras vaciarse, hay que llegar a esta cantidad para volver a correr.")]
        [SerializeField] private float recoverThreshold = 25f;

        private float current;
        private float lastUseTime = -10f;
        private bool exhausted;

        /// <summary>(actual, máximo)</summary>
        public event Action<float, float> Changed;

        public float Current => current;
        public float Max => maxStamina;
        public float Normalized => current / maxStamina;
        public bool IsExhausted => exhausted;
        public bool CanSprint => !exhausted && current > 0f;
        /// <summary>Segundos desde el último gasto (la UI se oculta cuando lleva un rato llena).</summary>
        public float TimeSinceUse => Time.time - lastUseTime;

        private void Awake()
        {
            current = maxStamina;
        }

        private void Update()
        {
            if (Time.time - lastUseTime < regenDelay || current >= maxStamina) return;
            Set(current + regenPerSecond * Time.deltaTime);
        }

        public void DrainForSprint(float deltaTime)
        {
            Use(sprintDrainPerSecond * deltaTime);
        }

        /// <summary>Gasta una cantidad fija (dash). Devuelve false si no hay suficiente.</summary>
        public bool TryConsume(float amount)
        {
            if (exhausted || current < amount) return false;
            Use(amount);
            return true;
        }

        private void Use(float amount)
        {
            lastUseTime = Time.time;
            Set(current - amount);
            if (current <= 0f) exhausted = true;
        }

        private void Set(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, maxStamina);
            if (Mathf.Approximately(clamped, current)) return;
            current = clamped;
            if (exhausted && current >= recoverThreshold) exhausted = false;
            Changed?.Invoke(current, maxStamina);
        }
    }
}
