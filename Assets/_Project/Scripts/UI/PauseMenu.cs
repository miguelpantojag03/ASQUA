using Ascua.Audio;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Menú de pausa (Esc / Start): pausa real del juego, Reanudar / Reintentar noche / Diario /
    /// Accesibilidad / Salir al título, y volúmenes de Música, Ambiente y Efectos conectados al
    /// AudioManager. El panel de Accesibilidad ajusta tamaño de texto, sacudidas y subtítulos.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        [Header("Paneles")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private CanvasGroup accessibilityPanel;

        [Header("Botones")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button diaryButton;
        [SerializeField] private Button accessibilityButton;
        [SerializeField] private Button titleButton;

        [Header("Volumen")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider ambientSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Accesibilidad")]
        [SerializeField] private Slider textScaleSlider;
        [SerializeField] private TMP_Text textScaleLabel;
        [SerializeField] private Toggle shakeToggle;
        [SerializeField] private Toggle subtitlesToggle;

        private bool paused;
        private float previousTimeScale = 1f;
        private PlayerInputReader playerInput;
        private bool syncing;

        public static bool IsPaused => Instance != null && Instance.paused;

        private void Awake()
        {
            Instance = this;
            GameSession.EscapeHandledExternally = true;
            SetGroup(group, false);
            SetGroup(accessibilityPanel, false);

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (diaryButton != null) diaryButton.onClick.AddListener(OpenDiary);
            if (accessibilityButton != null) accessibilityButton.onClick.AddListener(ToggleAccessibility);
            if (titleButton != null) titleButton.onClick.AddListener(ToTitle);

            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => SetVolume(AudioChannel.Musica, v));
            if (ambientSlider != null) ambientSlider.onValueChanged.AddListener(v => SetVolume(AudioChannel.Ambiente, v));
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => SetVolume(AudioChannel.Efectos, v));

            if (textScaleSlider != null) textScaleSlider.onValueChanged.AddListener(OnTextScale);
            if (shakeToggle != null) shakeToggle.onValueChanged.AddListener(v => { if (!syncing) AccessibilitySettings.SetCameraShake(v); });
            if (subtitlesToggle != null) subtitlesToggle.onValueChanged.AddListener(v => { if (!syncing) AccessibilitySettings.SetSfxSubtitles(v); });
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameSession.EscapeHandledExternally = false;
        }

        private void Start()
        {
            playerInput = FindFirstObjectByType<PlayerInputReader>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool pressed = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
            if (!pressed) return;

            // Esc cierra primero lo que esté encima.
            if (CodexUI.Instance != null && CodexUI.Instance.IsOpen)
            {
                CodexUI.Instance.Close();
                return;
            }
            if (paused && accessibilityPanel != null && accessibilityPanel.alpha > 0.5f)
            {
                SetGroup(accessibilityPanel, false);
                return;
            }
            if (paused)
            {
                Resume();
                return;
            }

            bool blocked = (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen)
                           || (PhotoMode.Instance != null && PhotoMode.Instance.IsActive)
                           || (NightManager.Instance != null && !NightManager.Instance.IsRunning)
                           || GameSession.IsModalOpen;
            if (!blocked) Pause();
        }

        public void Pause()
        {
            if (paused) return;
            paused = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (playerInput != null) playerInput.SetGameplayEnabled(false);
            GameSession.PushModal();
            SyncControls();
            SetGroup(group, true);
            if (resumeButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        public void Resume()
        {
            if (!paused) return;
            paused = false;
            SetGroup(group, false);
            SetGroup(accessibilityPanel, false);
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            if (playerInput != null) playerInput.SetGameplayEnabled(true);
            GameSession.PopModal();
            Click();
        }

        private void Retry()
        {
            Click();
            paused = false;
            GameSession.PopModal();
            if (NightManager.Instance != null) NightManager.Instance.RestartNight();
        }

        private void ToTitle()
        {
            Click();
            paused = false;
            GameSession.PopModal();
            if (NightManager.Instance != null) NightManager.Instance.ReturnToTitle();
        }

        private void OpenDiary()
        {
            Click();
            if (CodexUI.Instance != null) CodexUI.Instance.Open(true);
        }

        private void ToggleAccessibility()
        {
            Click();
            if (accessibilityPanel == null) return;
            SetGroup(accessibilityPanel, accessibilityPanel.alpha < 0.5f);
        }

        private void SetVolume(AudioChannel channel, float value)
        {
            if (syncing || AudioManager.Instance == null) return;
            AudioManager.Instance.SetVolume(channel, value);
        }

        private void OnTextScale(float value)
        {
            if (textScaleLabel != null) textScaleLabel.text = Mathf.RoundToInt(value * 100f) + " %";
            if (!syncing) AccessibilitySettings.SetDialogueTextScale(value);
        }

        /// <summary>Pone los controles con los valores actuales sin disparar cambios.</summary>
        private void SyncControls()
        {
            syncing = true;
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                if (musicSlider != null) musicSlider.value = audio.MusicVolume;
                if (ambientSlider != null) ambientSlider.value = audio.AmbientVolume;
                if (sfxSlider != null) sfxSlider.value = audio.SfxVolume;
            }
            if (textScaleSlider != null)
            {
                textScaleSlider.minValue = AccessibilitySettings.MinTextScale;
                textScaleSlider.maxValue = AccessibilitySettings.MaxTextScale;
                textScaleSlider.value = AccessibilitySettings.DialogueTextScale;
                OnTextScale(textScaleSlider.value);
            }
            if (shakeToggle != null) shakeToggle.isOn = AccessibilitySettings.CameraShake;
            if (subtitlesToggle != null) subtitlesToggle.isOn = AccessibilitySettings.SfxSubtitles;
            syncing = false;
        }

        private static void Click()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiClick();
        }

        private static void SetGroup(CanvasGroup g, bool visible)
        {
            if (g == null) return;
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
        }
    }
}
