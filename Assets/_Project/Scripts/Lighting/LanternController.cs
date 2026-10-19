using System;
using Ascua.Controls;
using Ascua.Player;
using UnityEngine;

namespace Ascua.Lighting
{
    /// <summary>
    /// El Farol de Ilse. En reposo es una brasa tenue y gratuita; al mantener el botón
    /// se expande al radio elegido (3 tamaños con Q/E) y consume energía de PlayerEnergy.
    /// Se registra como IIlluminationSource para que el mundo reaccione a su luz.
    /// </summary>
    [DisallowMultipleComponent]
    public class LanternController : MonoBehaviour, IIlluminationSource
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerEnergy energy;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Light lanternLight;
        [Tooltip("Malla emisiva del interior del Farol (la brasa).")]
        [SerializeField] private Renderer coreRenderer;
        [Tooltip("Pivote visual que se balancea al caminar.")]
        [SerializeField] private Transform swayPivot;
        [Tooltip("Luz de relleno sobre el personaje (sin sombras) para que no quede a contraluz.")]
        [SerializeField] private Light fillLight;

        [Header("Radios del Farol (m) y consumo (energía/s)")]
        [Tooltip("Radio JUGABLE: hasta dónde reacciona el mundo (ventanas, etc.).")]
        [SerializeField] private float[] radii = { 4.5f, 7f, 10.5f };
        [SerializeField] private float[] drainPerSecond = { 6f, 10f, 16f };
        [SerializeField] private int startRadiusIndex = 1;

        [Header("Luz principal")]
        [SerializeField] private Color lightColor = new Color(1f, 0.769f, 0.42f);
        [Tooltip("Radio jugable en reposo (no cuenta para iluminar objetos).")]
        [SerializeField] private float idleRange = 2.8f;
        [Tooltip("El halo visible se extiende más allá del radio jugable: range de la Light = radio x este factor.")]
        [SerializeField] private float lightRangeMultiplier = 1.5f;
        [SerializeField] private float idleIntensity = 0.8f;
        [Tooltip("Intensidad activa por tamaño (pequeño / medio / grande).")]
        [SerializeField] private float[] activeIntensities = { 2.0f, 2.4f, 2.8f };
        [Tooltip("Velocidad de transición reposo <-> activo (1/s).")]
        [SerializeField] private float rampSpeed = 5f;
        [SerializeField] private float rangeSmoothing = 8f;

        [Header("Luz de relleno")]
        [SerializeField] private Color fillColor = new Color(1f, 0.85f, 0.66f);
        [SerializeField] private float fillRange = 3f;
        [SerializeField] private float fillIntensityIdle = 0.35f;
        [SerializeField] private float fillIntensityActive = 0.6f;

        [Header("Brasa")]
        [SerializeField] private float coreEmissionIdle = 1.6f;
        [SerializeField] private float coreEmissionActive = 6f;

        [Header("Parpadeo")]
        [SerializeField] private float flickerAmount = 0.05f;
        [SerializeField] private float flickerSpeed = 3.5f;
        [Tooltip("Por debajo de esta energía normalizada el Farol parpadea más.")]
        [SerializeField] private float lowEnergyThreshold = 0.2f;
        [SerializeField] private float lowEnergyFlicker = 0.25f;

        [Header("Balanceo")]
        [SerializeField] private float swayAngle = 7f;
        [SerializeField] private float idleSwayAngle = 1.5f;
        [SerializeField] private float swaySpeed = 7.5f;

        [Header("Iluminación jugable")]
        [Tooltip("Fracción de la transición a partir de la cual el Farol cuenta como 'iluminando'.")]
        [SerializeField, Range(0.3f, 1f)] private float illuminationThreshold = 0.7f;

        private MaterialPropertyBlock propertyBlock;
        private float blend;
        private float currentRange;
        private float noiseSeed;

        public event Action<int> RadiusChanged;
        public event Action<bool> ActiveChanged;

        public int RadiusIndex { get; private set; }
        public int RadiusCount => radii.Length;
        public float CurrentRadiusSetting => radii[RadiusIndex];
        public bool IsActive { get; private set; }
        public float ActivationBlend => blend;

        // IIlluminationSource
        public Vector3 LightPosition => lanternLight != null ? lanternLight.transform.position : transform.position;
        public float IlluminationRadius => currentRange;
        public bool IsIlluminating => IsActive && blend >= illuminationThreshold;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (energy == null) energy = GetComponent<PlayerEnergy>();
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (lanternLight == null) lanternLight = GetComponentInChildren<Light>();

            propertyBlock = new MaterialPropertyBlock();
            noiseSeed = UnityEngine.Random.value * 100f;
            RadiusIndex = Mathf.Clamp(startRadiusIndex, 0, radii.Length - 1);
            currentRange = idleRange;
        }

        private void OnEnable()
        {
            IlluminationRegistry.Register(this);
        }

        private void OnDisable()
        {
            IlluminationRegistry.Unregister(this);
            SetActive(false);
        }

        private void Start()
        {
            RadiusChanged?.Invoke(RadiusIndex);
        }

        private void Update()
        {
            if (input != null)
            {
                if (input.RadiusUpPressed) SetRadiusIndex(RadiusIndex + 1);
                if (input.RadiusDownPressed) SetRadiusIndex(RadiusIndex - 1);
            }

            // Activo solo si se mantiene el botón y hay energía disponible.
            bool active = input != null && input.LanternHeld && energy != null && energy.CanConsume;
            if (active)
            {
                energy.Consume(GetDrain(RadiusIndex) * Time.deltaTime);
                active = energy.CanConsume;
            }
            SetActive(active);

            blend = Mathf.MoveTowards(blend, active ? 1f : 0f, rampSpeed * Time.deltaTime);
            float eased = blend * blend * (3f - 2f * blend);

            float targetRange = Mathf.Lerp(idleRange, radii[RadiusIndex], eased);
            currentRange = Mathf.Lerp(currentRange, targetRange, 1f - Mathf.Exp(-rangeSmoothing * Time.deltaTime));

            float flicker = ComputeFlicker();

            if (lanternLight != null)
            {
                lanternLight.color = lightColor;
                lanternLight.range = currentRange * lightRangeMultiplier;
                lanternLight.intensity = Mathf.Lerp(idleIntensity, GetActiveIntensity(RadiusIndex), eased) * flicker;
            }

            if (fillLight != null)
            {
                fillLight.color = fillColor;
                fillLight.range = fillRange;
                fillLight.intensity = Mathf.Lerp(fillIntensityIdle, fillIntensityActive, eased) * flicker;
            }

            if (coreRenderer != null)
            {
                coreRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, lightColor * (Mathf.Lerp(coreEmissionIdle, coreEmissionActive, eased) * flicker));
                coreRenderer.SetPropertyBlock(propertyBlock);
            }

            UpdateSway();
        }

        /// <summary>Cambia el tamaño del Farol (0 = pequeño, 2 = grande).</summary>
        public void SetRadiusIndex(int index)
        {
            int clamped = Mathf.Clamp(index, 0, radii.Length - 1);
            if (clamped == RadiusIndex) return;
            RadiusIndex = clamped;
            RadiusChanged?.Invoke(clamped);
        }

        private float GetDrain(int index)
        {
            if (drainPerSecond == null || drainPerSecond.Length == 0) return 0f;
            return drainPerSecond[Mathf.Min(index, drainPerSecond.Length - 1)];
        }

        private float GetActiveIntensity(int index)
        {
            if (activeIntensities == null || activeIntensities.Length == 0) return idleIntensity;
            return activeIntensities[Mathf.Min(index, activeIntensities.Length - 1)];
        }

        private void SetActive(bool value)
        {
            if (IsActive == value) return;
            IsActive = value;
            ActiveChanged?.Invoke(value);
        }

        private float ComputeFlicker()
        {
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseSeed) * 2f - 1f;
            float amount = flickerAmount;
            if (energy != null && IsActive && energy.Normalized < lowEnergyThreshold)
            {
                amount += lowEnergyFlicker * (1f - energy.Normalized / lowEnergyThreshold);
            }
            return 1f + noise * amount;
        }

        private void UpdateSway()
        {
            if (swayPivot == null) return;

            float speed01 = playerController != null ? Mathf.Clamp01(playerController.CurrentSpeed / playerController.RunSpeed) : 0f;
            float t = Time.time * swaySpeed;
            float pitch = Mathf.Sin(t) * Mathf.Lerp(idleSwayAngle, swayAngle, speed01);
            float roll = Mathf.Sin(t * 0.5f + 1.3f) * Mathf.Lerp(idleSwayAngle, swayAngle * 0.5f, speed01);
            swayPivot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
