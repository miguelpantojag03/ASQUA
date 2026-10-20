using Ascua.Audio;
using Ascua.Core;
using Ascua.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Pantalla al terminar la noche:
    ///  1) Resumen (NightSummary): zonas salvadas/perdidas, misiones y Vínculos.
    ///  2) Elegir 1 de 3 mejoras del Farol (PlayerUpgrades), que se conserva en las noches siguientes.
    ///  3) Siguiente noche · Modo foto · Reintentar · Título.
    /// Si Vesper se apagó entera no hay mejora: solo reintentar o volver.
    /// </summary>
    public class NightSummaryPanel : MonoBehaviour
    {
        [SerializeField] private NightManager night;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private float fadeDuration = 0.8f;

        [Header("Mejoras (1 de 3)")]
        [SerializeField] private CanvasGroup upgradeGroup;
        [SerializeField] private Button[] upgradeButtons;
        [SerializeField] private TMP_Text[] upgradeLabels;

        [Header("Después de elegir")]
        [SerializeField] private Button nextNightButton;
        [SerializeField] private Button photoButton;

        private static readonly UpgradeType[] Offer = { UpgradeType.RadioAmplio, UpgradeType.Eficiencia, UpgradeType.Regeneracion };

        private bool showing;
        private bool upgradeChosen;

        private void Awake()
        {
            if (night == null) night = FindFirstObjectByType<NightManager>();
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (titleButton != null) titleButton.onClick.AddListener(OnTitle);
            if (nextNightButton != null) nextNightButton.onClick.AddListener(OnNextNight);
            if (photoButton != null) photoButton.onClick.AddListener(OnPhoto);
            for (int i = 0; upgradeButtons != null && i < upgradeButtons.Length; i++)
            {
                int index = i;
                upgradeButtons[i].onClick.AddListener(() => ChooseUpgrade(index));
            }
            SetVisible(group, 0f);
            SetVisible(upgradeGroup, 0f);
        }

        private void OnEnable()
        {
            if (night != null) night.NightEnded += Show;
        }

        private void OnDisable()
        {
            if (night != null) night.NightEnded -= Show;
        }

        private void Update()
        {
            // El juego está en pausa (timeScale = 0): se usa tiempo sin escalar.
            if (!showing || group == null || group.alpha >= 1f) return;
            SetVisible(group, Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / fadeDuration));
        }

        private void Show(NightResult result)
        {
            showing = true;
            upgradeChosen = false;
            bool survived = result.ZonesSaved > 0;

            if (titleText != null) titleText.text = survived ? "Amanece en Vesper" : "Vesper se apagó";

            if (bodyText != null)
            {
                string reason = result.EndedEarly ? "La Penumbra se lo llevó todo antes del alba." : "Sobreviviste a la noche " + result.NightNumber + ".";
                NightSummary summary = night != null ? night.LastSummary : null;
                bodyText.text = reason + "\n\n" + (summary != null ? summary.ToDisplayText() : "Zonas salvadas: " + result.ZonesSaved + "/" + result.ZonesTotal);
            }

            // Mejoras solo si amaneció.
            for (int i = 0; upgradeLabels != null && i < upgradeLabels.Length && i < Offer.Length; i++)
            {
                (string title, string description) = PlayerUpgrades.Describe(Offer[i]);
                upgradeLabels[i].text = "<b>" + title + "</b>\n<size=75%>" + description + "</size>";
            }
            SetVisible(upgradeGroup, survived ? 1f : 0f);
            SetActive(nextNightButton, false);
            SetActive(photoButton, true);

            Select(survived && upgradeButtons != null && upgradeButtons.Length > 0 ? upgradeButtons[0] : retryButton);
        }

        private void ChooseUpgrade(int index)
        {
            if (upgradeChosen || index < 0 || index >= Offer.Length) return;
            upgradeChosen = true;
            PlayerUpgrades.Choose(Offer[index]);
            Click();

            (string title, _) = PlayerUpgrades.Describe(Offer[index]);
            if (bodyText != null) bodyText.text += "\n\n<color=#FFC46B>Mejora elegida: " + title + "</color>";
            SetVisible(upgradeGroup, 0f);
            SetActive(nextNightButton, true);
            Select(nextNightButton);
        }

        private void OnNextNight()
        {
            Click();
            if (night != null) night.StartNextNight();
        }

        private void OnPhoto()
        {
            Click();
            if (PhotoMode.Instance == null) return;
            CanvasGroup summary = group;
            SetVisible(summary, 0f);
            PhotoMode.Instance.Enter(() =>
            {
                SetVisible(summary, 1f);
                Select(upgradeChosen ? nextNightButton : retryButton);
            });
        }

        private void OnRetry()
        {
            Click();
            if (night != null) night.RestartNight();
        }

        private void OnTitle()
        {
            Click();
            if (night != null) night.ReturnToTitle();
        }

        private static void Click()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiClick();
        }

        private static void Select(Button button)
        {
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        private static void SetVisible(CanvasGroup g, float alpha)
        {
            if (g == null) return;
            g.alpha = alpha;
            g.interactable = alpha > 0.5f;
            g.blocksRaycasts = alpha > 0.01f;
        }
    }
}
