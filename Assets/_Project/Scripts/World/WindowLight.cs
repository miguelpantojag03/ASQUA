using System;
using Ascua.Lighting;
using UnityEngine;
using UnityEngine.Events;

namespace Ascua.World
{
    /// <summary>
    /// Ventana que se enciende y se apaga con transición de emisión.
    /// Dos modos:
    ///  - Iluminación directa (Fase 1): se enciende si el Farol la ilumina unos segundos.
    ///  - Controlada por Esperanza (Fase 2): ZoneHope decide cuándo encenderla o apagarla.
    /// La Penumbra puede atenuarla (SetPenumbraDim) y una zona muerta la bloquea (SetLocked).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WindowLight : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Identidad")]
        [SerializeField] private string zoneId = "Plaza";

        [Header("Encendido por iluminación directa (si no la controla una zona)")]
        [Tooltip("Segundos de iluminación continua necesarios para encenderla.")]
        [SerializeField, Min(0.1f)] private float requiredLitTime = 2f;
        [Tooltip("Carga perdida por segundo cuando deja de iluminarse.")]
        [SerializeField] private float chargeDecayPerSecond = 0.6f;
        [SerializeField] private bool startLit = false;

        [Header("Transiciones")]
        [Tooltip("Duración del Lerp de emisión al encenderse.")]
        [SerializeField] private float igniteDuration = 1.4f;
        [Tooltip("Duración del Lerp de emisión al apagarse.")]
        [SerializeField] private float extinguishDuration = 0.9f;

        [Header("Aspecto")]
        [SerializeField] private Color litColor = new Color(1f, 0.66f, 0.32f);
        [SerializeField] private float litEmissionIntensity = 2.4f;
        [Tooltip("Emisión máxima mientras se va cargando (pista visual para el jugador).")]
        [SerializeField] private float chargeHintIntensity = 0.7f;
        [Tooltip("Distancia hacia fuera del muro desde donde se comprueba la luz.")]
        [SerializeField] private float sampleOffset = 0.35f;
        [SerializeField] private Light glowLight;
        [SerializeField] private float glowLightIntensity = 1.8f;

        [Header("Eventos")]
        [SerializeField] private UnityEvent onLit = new UnityEvent();
        [SerializeField] private UnityEvent onExtinguished = new UnityEvent();

        /// <summary>Se dispara cuando CUALQUIER ventana se enciende.</summary>
        public static event Action<WindowLight> AnyWindowLit;
        public static event Action<WindowLight> AnyWindowExtinguished;

        public event Action<WindowLight> Lit;
        public event Action<WindowLight> Extinguished;

        private Renderer windowRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float charge;
        private float level;          // 0 = apagada, 1 = encendida del todo
        private bool isLit;
        private bool hopeControlled;
        private bool locked;
        private float penumbraDim;
        private float noiseSeed;
        private float currentExtinguishDuration;
        private bool dramaticFade;

        public string ZoneId => zoneId;
        public bool IsLit => isLit;
        public bool IsLocked => locked;
        public bool IsHopeControlled => hopeControlled;
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
            currentExtinguishDuration = extinguishDuration;

            if (startLit)
            {
                isLit = true;
                level = 1f;
                charge = requiredLitTime;
            }
            ApplyVisuals();
        }

        private void Update()
        {
            // Transición de nivel hacia encendida/apagada.
            float target = isLit ? 1f : 0f;
            if (!Mathf.Approximately(level, target))
            {
                float duration = isLit ? igniteDuration : currentExtinguishDuration;
                level = Mathf.MoveTowards(level, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            }
            else if (dramaticFade && level <= 0f)
            {
                dramaticFade = false;
            }

            if (!hopeControlled && !isLit && !locked)
            {
                // Modo Fase 1: cargar con la luz del Farol.
                Vector3 samplePoint = transform.position + transform.forward * sampleOffset;
                bool illuminated = IlluminationRegistry.IsPointIlluminated(samplePoint, out _);
                charge = illuminated
                    ? charge + Time.deltaTime
                    : Mathf.Max(0f, charge - chargeDecayPerSecond * Time.deltaTime);

                if (charge >= requiredLitTime) Ignite();
            }

            ApplyVisuals();
        }

        /// <summary>Asigna la zona a la que pertenece (lo usa el constructor de escenas).</summary>
        public void ConfigureZone(string id)
        {
            zoneId = id;
        }

        /// <summary>Si es true, la ventana deja de cargarse con el Farol y obedece solo a ZoneHope.</summary>
        public void SetHopeControlled(bool value)
        {
            hopeControlled = value;
            if (value) charge = 0f;
        }

        /// <summary>Bloquea la ventana: no puede volver a encenderse (zona muerta esta noche).</summary>
        public void SetLocked(bool value)
        {
            locked = value;
        }

        /// <summary>Atenuación visual por la Penumbra (0 = sin efecto, 1 = completamente apagada).</summary>
        public void SetPenumbraDim(float amount)
        {
            penumbraDim = Mathf.Clamp01(amount);
        }

        /// <summary>Enciende la ventana (con transición). No hace nada si está bloqueada.</summary>
        public void Ignite()
        {
            if (isLit || locked) return;
            isLit = true;
            charge = requiredLitTime;

            Lit?.Invoke(this);
            AnyWindowLit?.Invoke(this);
            onLit?.Invoke();
        }

        /// <summary>
        /// Apaga la ventana. Con fadeDuration &gt; 0 el apagado es lento y "agónico"
        /// (parpadea mientras se apaga): se usa cuando muere una zona.
        /// </summary>
        public void Extinguish(float fadeDuration = -1f)
        {
            if (!isLit) return;
            isLit = false;
            charge = 0f;
            dramaticFade = fadeDuration > 0f;
            currentExtinguishDuration = dramaticFade ? fadeDuration : extinguishDuration;

            Extinguished?.Invoke(this);
            AnyWindowExtinguished?.Invoke(this);
            onExtinguished?.Invoke();
        }

        private void ApplyVisuals()
        {
            // Lerp suavizado entre la pista de carga y la emisión completa.
            float eased = level * level * (3f - 2f * level);
            float alive = 1f + (Mathf.PerlinNoise(Time.time * 1.3f, noiseSeed) - 0.5f) * 0.06f;

            float hint = 0f;
            if (!hopeControlled && !isLit)
            {
                float p = charge / requiredLitTime;
                float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, noiseSeed);
                hint = chargeHintIntensity * p * p * flicker;
            }

            // Apagado dramático: la luz tiembla y "tose" antes de morir.
            if (dramaticFade && !isLit && level > 0f)
            {
                float stutter = Mathf.PerlinNoise(Time.time * 14f, noiseSeed);
                alive *= stutter > 0.4f ? 1f : Mathf.Lerp(0.15f, 1f, 1f - level);
            }

            float visibility = 1f - penumbraDim;
            float emission = Mathf.Max(hint, litEmissionIntensity * eased * alive) * visibility;
            float glow = glowLightIntensity * eased * alive * visibility;

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
