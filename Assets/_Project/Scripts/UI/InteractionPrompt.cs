using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>Aviso de interacción del HUD ("Mantén E · Hablar con Mina") con barra de mantener.</summary>
    public class InteractionPrompt : MonoBehaviour
    {
        public static InteractionPrompt Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image holdFill;
        [SerializeField] private float fadeSpeed = 8f;

        private float targetAlpha;

        private void Awake()
        {
            Instance = this;
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        }

        public void Show(string text)
        {
            if (label != null) label.text = text;
            targetAlpha = 1f;
        }

        public void Hide()
        {
            targetAlpha = 0f;
            SetFill(0f);
        }

        public void SetFill(float amount)
        {
            if (holdFill != null) holdFill.fillAmount = Mathf.Clamp01(amount);
        }
    }
}
