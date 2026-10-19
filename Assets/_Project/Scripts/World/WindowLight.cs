using System;
using Ascua.Lighting;
using UnityEngine;
using UnityEngine.Events;

namespace Ascua.World
{
    /// <summary>
    /// Ventana que se "reenciende" si el Farol activo la ilumina durante unos segundos.
    /// Pensada para que ZoneHope (Fase 2) escuche AnyWindowLit / Lit y sume Esperanza
    /// por zona (ZoneId). Extinguish() queda disponible para la Penumbra.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WindowLight : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Identidad")]
        [SerializeField] private string zoneId = "Plaza";

        [Header("Encendido")]
        [Tooltip("Segundos de iluminación continua necesarios para encenderla.")]
        [SerializeField, Min(0.1f)] private float requiredLitTime = 2f;
        [Tooltip("Carga perdida por segundo cuando deja de iluminarse.")]
        [SerializeField] private float chargeDecayPerSecond = 0.6f;
        [Tooltip("Duración del Lerp de emisión al encenderse.")]
        [SerializeField] private float igniteDuration = 1.4f;
        [SerializeField] private bool startLit = false;

        [Header("Aspecto")]
        [SerializeField] private Color litColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private float litEmissionIntensity = 3.2f;
        [Tooltip("Emisión máxima mientras se va cargando (pista visual para el jugador).")]
        [SerializeField] private float chargeHintIntensity = 0.7f;
        [Tooltip("Distancia hacia fuera del muro desde donde se comprueba la luz.")]
        [SerializeField] private float sampleOffset = 0.35f;
        [SerializeField] private Light glowLight;
        [SerializeField] private float glowLightIntensity = 1.8f;

        [Header("Eventos")]
        [SerializeField] private UnityEvent onLit = new UnityEvent();
        [SerializeField] private UnityEvent onExtinguished = new UnityEvent();

        /// <summary>Se dispara cuando CUALQUIER ventana se enciende (útil para ZoneHope).</summary>
        public static event Action<WindowLight> AnyWindowLit;
        public static event Action<WindowLight> AnyWindowExtinguished;

        public event Action<WindowLight> Lit;
        public event Action<WindowLight> Extinguished;

        private Renderer windowRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float charge;
        private float igniteProgress;
        private bool isLit;
        private float noiseSeed;

        public string ZoneId => zoneId;
        public bool IsLit => isLit;
        public float ChargeProgress => isLit ? 1f : charge / requiredLitTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyWindowLit = null;
            AnyWindowExtinguished = null;
        }

        private void Awake()
        {
            windowRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            noiseSeed = UnityEngine.Random.value * 100f;

            if (startLit)
            {
                isLit = true;
                igniteProgress = 1f;
                charge = requiredLitTime;
            }
            ApplyVisuals();
        }

        private void Update()
        {
            if (isLit)
            {
                if (igniteProgress < 1f) igniteProgress = Mathf.MoveTowards(igniteProgress, 1f, Time.deltaTime / igniteDuration);
                ApplyVisuals();
                return;
            }

            Vector3 samplePoint = transform.position + transform.forward * sampleOffset;
            bool illuminated = IlluminationRegistry.IsPointIlluminated(samplePoint, out _);

            charge = illuminated
                ? charge + Time.deltaTime
                : Mathf.Max(0f, charge - chargeDecayPerSecond * Time.deltaTime);

            if (charge >= requiredLitTime) Ignite();
            else ApplyVisuals();
        }

        /// <summary>Enciende la ventana inmediatamente (con transición).</summary>
        public void Ignite()
        {
            if (isLit) return;
            isLit = true;
            charge = requiredLitTime;
            igniteProgress = 0f;

            Lit?.Invoke(this);
            AnyWindowLit?.Invoke(this);
            onLit?.Invoke();
        }

        /// <summary>Apaga la ventana (lo usará la Penumbra en fases futuras).</summary>
        public void Extinguish()
        {
            if (!isLit) return;
            isLit = false;
            charge = 0f;
            igniteProgress = 0f;
            ApplyVisuals();

            Extinguished?.Invoke(this);
            AnyWindowExtinguished?.Invoke(this);
            onExtinguished?.Invoke();
        }

        private void ApplyVisuals()
        {
            float emission;
            float glow;

            if (isLit)
            {
                // Lerp desde la pista de carga hasta la emisión completa.
                float eased = igniteProgress * igniteProgress * (3f - 2f * igniteProgress);
                float alive = 1f + (Mathf.PerlinNoise(Time.time * 1.3f, noiseSeed) - 0.5f) * 0.06f;
                emission = Mathf.Lerp(chargeHintIntensity, litEmissionIntensity, eased) * alive;
                glow = glowLightIntensity * eased * alive;
            }
            else
            {
                // Mientras carga: brillo tenue y tembloroso, como una mecha que prende.
                float p = charge / requiredLitTime;
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, noiseSeed);
                emission = chargeHintIntensity * p * p * flicker;
                glow = glowLightIntensity * 0.15f * p * p;
            }

            windowRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, litColor * emission);
            windowRenderer.SetPropertyBlock(propertyBlock);

            if (glowLight != null)
            {
                glowLight.intensity = glow;
                glowLight.enabled = glow > 0.001f;
            }
        }
    }
}
