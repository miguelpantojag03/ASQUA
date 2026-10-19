using Ascua.Lighting;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// HUD de la escena jugable: barra de energía del Farol, tamaño de radio actual,
    /// ayuda de controles que se desvanece, fundido de entrada y contador de FPS (F3).
    /// Solo escucha eventos de PlayerEnergy y LanternController; no contiene lógica de juego.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Sistemas")]
        [SerializeField] private PlayerEnergy energy;
        [SerializeField] private LanternController lantern;

        [Header("Barra de energía")]
        [Tooltip("Rect del relleno; su anchorMax.x representa la energía.")]
        [SerializeField] private RectTransform energyFill;
        [SerializeField] private Image energyFillImage;
        [SerializeField] private TMP_Text energyValueText;
        [SerializeField] private float barSmoothing = 10f;
        [SerializeField] private Color normalColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color lowColor = new Color(0.86f, 0.45f, 0.25f);
        [SerializeField] private Color exhaustedColor = new Color(0.45f, 0.47f, 0.6f);
        [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.25f;

        [Header("Ícono de llama")]
        [Tooltip("Llama exterior; toma el mismo color que la barra.")]
        [SerializeField] private Image flameIcon;
        [Tooltip("Núcleo claro de la llama (opcional).")]
        [SerializeField] private Image flameCore;
        [SerializeField] private float flameFlickerSpeed = 9f;

        [Header("Tamaño del Farol")]
        [SerializeField] private Image[] radiusPips;
        [SerializeField] private Color pipOnColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color pipOffColor = new Color(1f, 1f, 1f, 0.18f);

        [Header("Extras")]
        [SerializeField] private TMP_Text fpsText;
        [SerializeField] private bool showFps = false;
        [SerializeField] private CanvasGroup hintGroup;
        [SerializeField] private float hintVisibleSeconds = 12f;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private float fadeInDuration = 1.5f;

        private float displayedEnergy = 1f;
        private float targetEnergy = 1f;
        private int lastShownValue = -1;
        private bool exhausted;
        private float hintTimer;
        private float fpsTimer;
        private int fpsFrames;

        private void Awake()
        {
            if (energy == null) energy = FindFirstObjectByType<PlayerEnergy>();
            if (lantern == null) lantern = FindFirstObjectByType<LanternController>();
        }

        private void OnEnable()
        {
            if (energy != null)
            {
                energy.EnergyChanged += OnEnergyChanged;
                energy.ExhaustedChanged += OnExhaustedChanged;
            }
            if (lantern != null) lantern.RadiusChanged += OnRadiusChanged;
        }

        private void OnDisable()
        {
            if (energy != null)
            {
                energy.EnergyChanged -= OnEnergyChanged;
                energy.ExhaustedChanged -= OnExhaustedChanged;
            }
            if (lantern != null) lantern.RadiusChanged -= OnRadiusChanged;
        }

        private void Start()
        {
            if (energy != null)
            {
                targetEnergy = energy.Normalized;
                displayedEnergy = targetEnergy;
            }
            if (lantern != null) OnRadiusChanged(lantern.RadiusIndex);
            if (fpsText != null) fpsText.gameObject.SetActive(showFps);
            if (fadeGroup != null) fadeGroup.alpha = 1f;
        }

        private void Update()
        {
            UpdateEnergyBar();
            UpdateFlame();
            UpdateHint();
            UpdateFade();
            UpdateFps();
        }

        private void OnEnergyChanged(float current, float max)
        {
            targetEnergy = max > 0f ? current / max : 0f;
        }

        private void OnExhaustedChanged(bool value)
        {
            exhausted = value;
        }

        private void OnRadiusChanged(int index)
        {
            if (radiusPips == null) return;
            for (int i = 0; i < radiusPips.Length; i++)
            {
                if (radiusPips[i] == null) continue;
                radiusPips[i].color = i <= index ? pipOnColor : pipOffColor;
            }
        }

        private void UpdateEnergyBar()
        {
            displayedEnergy = Mathf.Lerp(displayedEnergy, targetEnergy, 1f - Mathf.Exp(-barSmoothing * Time.deltaTime));

            if (energyFill != null) energyFill.anchorMax = new Vector2(displayedEnergy, 1f);

            if (energyFillImage != null)
            {
                Color target;
                if (exhausted) target = Color.Lerp(exhaustedColor, lowColor, Mathf.PingPong(Time.time * 2f, 1f));
                else if (displayedEnergy < lowThreshold) target = lowColor;
                else target = normalColor;
                energyFillImage.color = Color.Lerp(energyFillImage.color, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }

            // Solo se reescribe el texto cuando cambia el número (evita basura por frame).
            if (energyValueText != null)
            {
                int max = energy != null ? Mathf.RoundToInt(energy.Max) : 100;
                int value = Mathf.CeilToInt(targetEnergy * max);
                if (value != lastShownValue)
                {
                    lastShownValue = value;
                    energyValueText.text = value + "/" + max;
                }
            }
        }

        private void UpdateFlame()
        {
            if (flameIcon == null) return;

            // La llama comparte color con la barra y "baila" más cuando el Farol está activo.
            if (energyFillImage != null) flameIcon.color = energyFillImage.color;

            float activity = lantern != null ? lantern.ActivationBlend : 0f;
            float amplitude = Mathf.Lerp(0.03f, 0.12f, activity);
            float noise = Mathf.PerlinNoise(Time.time * flameFlickerSpeed, 0.37f) * 2f - 1f;
            float sway = Mathf.PerlinNoise(0.81f, Time.time * flameFlickerSpeed * 0.5f) * 2f - 1f;

            RectTransform rt = flameIcon.rectTransform;
            rt.localScale = new Vector3(1f - noise * amplitude * 0.5f, 1f + noise * amplitude, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, sway * amplitude * 40f);

            if (flameCore != null)
            {
                Color core = flameCore.color;
                core.a = exhausted ? 0.2f : Mathf.Lerp(0.55f, 1f, activity);
                flameCore.color = core;
            }
        }

        private void UpdateHint()
        {
            if (hintGroup == null) return;
            hintTimer += Time.deltaTime;
            if (hintTimer > hintVisibleSeconds) hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, 0f, Time.deltaTime * 0.5f);
        }

        private void UpdateFade()
        {
            if (fadeGroup == null || fadeGroup.alpha <= 0f) return;
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, 0f, Time.deltaTime / fadeInDuration);
            fadeGroup.blocksRaycasts = fadeGroup.alpha > 0.01f;
        }

        private void UpdateFps()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame && fpsText != null)
            {
                showFps = !showFps;
                fpsText.gameObject.SetActive(showFps);
            }

            if (!showFps || fpsText == null) return;

            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fpsText.text = Mathf.RoundToInt(fpsFrames / fpsTimer) + " FPS";
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }
    }
}
