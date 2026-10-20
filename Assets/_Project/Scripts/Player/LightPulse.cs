using System;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Pulso de luz: ráfaga de emergencia (R / botón Y). Cuesta 20 de energía, se recarga en 25 s,
    /// ilumina de golpe un radio grande (cuenta como fuente de luz para zonas y ventanas) y
    /// repele al Heraldo. Visual: una esfera ámbar que se expande y una luz que destella.
    /// </summary>
    public class LightPulse : MonoBehaviour, IIlluminationSource
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerEnergy energy;
        [Tooltip("Esfera que se expande (material transparente).")]
        [SerializeField] private Transform ringVisual;
        [SerializeField] private Light flashLight;

        [Header("Ajustes")]
        [SerializeField] private float energyCost = 20f;
        [SerializeField] private float cooldown = 25f;
        [SerializeField] private float radius = 12f;
        [SerializeField] private float duration = 0.7f;
        [SerializeField] private float flashIntensity = 6f;
        [SerializeField] private Color pulseColor = new Color(2.4f, 1.8f, 0.95f, 0.32f);

        public event Action Pulsed;
        public event Action Failed;

        private float cooldownRemaining;
        private float activeTimer;
        private Renderer ringRenderer;
        private MaterialPropertyBlock propertyBlock;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;
        public float EnergyCost => energyCost;
        public bool IsReady => cooldownRemaining <= 0f;

        // IIlluminationSource: durante el pulso, ilumina un radio grande.
        public Vector3 LightPosition => transform.position + Vector3.up;
        public float IlluminationRadius => radius * Mathf.Clamp01(1f - activeTimer / duration + 0.2f);
        public bool IsIlluminating => activeTimer > 0f;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (energy == null) energy = GetComponent<PlayerEnergy>();
            propertyBlock = new MaterialPropertyBlock();
            if (ringVisual != null)
            {
                ringRenderer = ringVisual.GetComponent<Renderer>();
                ringVisual.gameObject.SetActive(false);
            }
            if (flashLight != null) flashLight.enabled = false;
        }

        private void OnEnable()
        {
            IlluminationRegistry.Register(this);
        }

        private void OnDisable()
        {
            IlluminationRegistry.Unregister(this);
        }

        private void Update()
        {
            if (cooldownRemaining > 0f) cooldownRemaining -= Time.deltaTime;

            if (input != null && input.PulsePressed) TryFire();

            if (activeTimer > 0f)
            {
                activeTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(activeTimer / duration);
                UpdateVisual(t);
                if (activeTimer <= 0f) EndVisual();
            }
        }

        /// <summary>Lanza el pulso si está listo y hay energía.</summary>
        public bool TryFire()
        {
            if (!IsReady || energy == null || energy.Current < energyCost || !energy.Consume(energyCost))
            {
                Failed?.Invoke();
                return false;
            }

            cooldownRemaining = cooldown;
            activeTimer = duration;

            // Repele al Heraldo si está dentro del radio.
            Heraldo_AI heraldo = Heraldo_AI.Instance;
            if (heraldo != null && heraldo.IsPresent && Vector3.Distance(heraldo.transform.position, transform.position) <= radius)
            {
                heraldo.Repel(transform.position);
            }

            if (ringVisual != null) ringVisual.gameObject.SetActive(true);
            if (flashLight != null) flashLight.enabled = true;
            UpdateVisual(0f);

            EventBus.RaiseLightPulse(transform.position, radius);
            Pulsed?.Invoke();
            return true;
        }

        private void UpdateVisual(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            if (ringVisual != null)
            {
                ringVisual.localScale = Vector3.one * Mathf.Lerp(0.5f, radius * 2f, eased);
                if (ringRenderer != null)
                {
                    Color c = pulseColor;
                    c.a *= 1f - t;
                    ringRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(BaseColorId, c);
                    ringRenderer.SetPropertyBlock(propertyBlock);
                }
            }
            if (flashLight != null)
            {
                flashLight.range = Mathf.Lerp(4f, radius * 1.4f, eased);
                flashLight.intensity = flashIntensity * (1f - t);
            }
        }

        private void EndVisual()
        {
            activeTimer = 0f;
            if (ringVisual != null) ringVisual.gameObject.SetActive(false);
            if (flashLight != null) flashLight.enabled = false;
        }
    }
}
