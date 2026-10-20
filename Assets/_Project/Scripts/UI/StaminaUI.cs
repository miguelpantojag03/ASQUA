using Ascua.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Barra de Stamina discreta: solo aparece mientras se gasta o se recupera, y desaparece
    /// un momento después de llenarse. Color frío para no confundirla con la energía (ámbar) del Farol.
    /// </summary>
    public class StaminaUI : MonoBehaviour
    {
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private Color normalColor = new Color(0.72f, 0.8f, 1f);
        [SerializeField] private Color exhaustedColor = new Color(0.86f, 0.45f, 0.25f);
        [SerializeField] private float hideAfter = 1.2f;

        private void Awake()
        {
            if (stamina == null) stamina = FindFirstObjectByType<PlayerStamina>();
            if (group != null) group.alpha = 0f;
        }

        private void Update()
        {
            if (stamina == null) return;

            bool visible = stamina.Normalized < 0.999f || stamina.TimeSinceUse < hideAfter;
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 3f);

            if (fill != null) fill.anchorMax = new Vector2(stamina.Normalized, 1f);
            if (fillImage != null)
            {
                fillImage.color = stamina.IsExhausted
                    ? Color.Lerp(exhaustedColor, normalColor, Mathf.PingPong(Time.unscaledTime * 3f, 1f))
                    : normalColor;
            }
        }
    }
}
