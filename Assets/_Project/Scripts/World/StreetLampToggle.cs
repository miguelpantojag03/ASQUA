using System;
using UnityEngine;
using UnityEngine.Events;

namespace Ascua.World
{
    /// <summary>
    /// Encendido/apagado visual de una farola con Lerp de emisión y de su luz puntual.
    /// No decide CUÁNDO encenderse: en la Fase 2 el sistema de Esperanza (ZoneHope)
    /// solo tendrá que llamar a TurnOn() / TurnOff() / SetOn().
    /// </summary>
    [DisallowMultipleComponent]
    public class StreetLampToggle : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Identidad")]
        [SerializeField] private string zoneId = "Plaza";

        [Header("Referencias")]
        [Tooltip("Renderer del cristal de la farola (su material debe tener emisión activada).")]
        [SerializeField] private Renderer glassRenderer;
        [Tooltip("Luz puntual opcional que ilumina la calle al encenderse.")]
        [SerializeField] private Light lampLight;

        [Header("Aspecto encendida")]
        [SerializeField] private Color litColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private float emissionIntensity = 4f;
        [SerializeField] private float lightIntensity = 2.2f;

        [Header("Transición")]
        [SerializeField] private float turnOnDuration = 1.6f;
        [SerializeField] private float turnOffDuration = 0.8f;
        [Tooltip("Parpadeo al arrancar, como una lámpara vieja que cuesta encender.")]
        [SerializeField] private bool flickerOnIgnite = true;
        [SerializeField] private bool startOn = false;

        [Header("Eventos")]
        [SerializeField] private UnityEvent onTurnedOn = new UnityEvent();
        [SerializeField] private UnityEvent onTurnedOff = new UnityEvent();

        public event Action<StreetLampToggle> TurnedOn;
        public event Action<StreetLampToggle> TurnedOff;

        private MaterialPropertyBlock propertyBlock;
        private float level;
        private float targetLevel;
        private float noiseSeed;

        public string ZoneId => zoneId;
        /// <summary>True si el objetivo actual es estar encendida.</summary>
        public bool IsOn => targetLevel > 0.5f;
        /// <summary>Nivel actual de encendido (0 = apagada, 1 = encendida).</summary>
        public float Level => level;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            noiseSeed = UnityEngine.Random.value * 100f;
            level = targetLevel = startOn ? 1f : 0f;
            ApplyVisuals();
        }

        private void Update()
        {
            if (Mathf.Approximately(level, targetLevel)) return;

            float duration = targetLevel > level ? turnOnDuration : turnOffDuration;
            level = Mathf.MoveTowards(level, targetLevel, Time.deltaTime / Mathf.Max(0.01f, duration));
            ApplyVisuals();
        }

        /// <summary>Enciende la farola con transición.</summary>
        [ContextMenu("Encender")]
        public void TurnOn()
        {
            SetOn(true, false);
        }

        /// <summary>Apaga la farola con transición.</summary>
        [ContextMenu("Apagar")]
        public void TurnOff()
        {
            SetOn(false, false);
        }

        [ContextMenu("Alternar")]
        public void Toggle()
        {
            SetOn(!IsOn, false);
        }

        /// <summary>Cambia el estado; instant = true salta la transición (útil al cargar partida).</summary>
        public void SetOn(bool on, bool instant)
        {
            bool wasOn = IsOn;
            targetLevel = on ? 1f : 0f;
            if (instant)
            {
                level = targetLevel;
                ApplyVisuals();
            }

            if (on && !wasOn)
            {
                TurnedOn?.Invoke(this);
                onTurnedOn?.Invoke();
            }
            else if (!on && wasOn)
            {
                TurnedOff?.Invoke(this);
                onTurnedOff?.Invoke();
            }
        }

        private void ApplyVisuals()
        {
            // Lerp suavizado del nivel de encendido.
            float eased = level * level * (3f - 2f * level);

            // Mientras arranca, la lámpara "tose" con un parpadeo que desaparece al llegar a 1.
            if (flickerOnIgnite && targetLevel > level && level > 0f)
            {
                float noise = Mathf.PerlinNoise(Time.time * 18f, noiseSeed);
                eased *= Mathf.Lerp(noise > 0.45f ? 1f : 0.15f, 1f, level);
            }

            if (glassRenderer != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                glassRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, litColor * (emissionIntensity * eased));
                glassRenderer.SetPropertyBlock(propertyBlock);
            }

            if (lampLight != null)
            {
                lampLight.color = litColor;
                lampLight.intensity = lightIntensity * eased;
                lampLight.enabled = eased > 0.001f;
            }
        }
    }
}
