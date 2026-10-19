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
                int value = Mathf.CeilToInt(targetEnergy * 100f);
                if (value != lastShownValue)
                {
                    lastShownValue = value;
                    energyValueText.text = value.ToString();
                }
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
