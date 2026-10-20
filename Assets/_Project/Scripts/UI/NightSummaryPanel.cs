using Ascua.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Pantalla de resumen al terminar la noche: "Zonas salvadas: X/2" y botones
    /// para reintentar o volver al título. Escucha NightManager.NightEnded.
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

        private bool showing;

        private void Awake()
        {
            if (night == null) night = FindFirstObjectByType<NightManager>();
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (titleButton != null) titleButton.onClick.AddListener(OnTitle);
            SetVisible(0f);
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
            SetVisible(Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / fadeDuration));
        }

        private void Show(NightResult result)
        {
            showing = true;

            if (titleText != null)
            {
                titleText.text = result.ZonesSaved == 0 ? "Vesper se apagó" : "Amanece en Vesper";
            }

            if (bodyText != null)
            {
                string reason = result.EndedEarly ? "La Penumbra se lo llevó todo antes del alba." : "Sobreviviste a la noche " + result.NightNumber + ".";
                bodyText.text = reason + "\n\nZonas salvadas: " + result.ZonesSaved + "/" + result.ZonesTotal;
                if (result.QuestsTotal > 0) bodyText.text += "\nMisiones cumplidas: " + result.QuestsCompleted + "/" + result.QuestsTotal;
            }

            if (retryButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
            }
        }

        private void SetVisible(float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.interactable = alpha > 0.5f;
            group.blocksRaycasts = alpha > 0.01f;
        }

        private void OnRetry()
        {
            if (night != null) night.RestartNight();
        }

        private void OnTitle()
        {
            if (night != null) night.ReturnToTitle();
        }
    }
}
