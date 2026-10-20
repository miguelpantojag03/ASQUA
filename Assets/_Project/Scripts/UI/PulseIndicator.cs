using Ascua.Player;
using TMPro;
using UnityEngine;

namespace Ascua.UI
{
    /// <summary>Indicador del Pulso de luz junto a la barra de energía: "PULSO · R" listo, o la cuenta atrás.</summary>
    public class PulseIndicator : MonoBehaviour
    {
        [SerializeField] private LightPulse pulse;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color readyColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color waitingColor = new Color(0.72f, 0.77f, 0.93f, 0.55f);
        [SerializeField] private Color failColor = new Color(0.86f, 0.45f, 0.25f);

        private int shownSeconds = -2;
        private float failFlash;

        private void Awake()
        {
            if (pulse == null) pulse = FindFirstObjectByType<LightPulse>();
        }

        private void OnEnable()
        {
            if (pulse != null) pulse.Failed += OnFailed;
        }

        private void OnDisable()
        {
            if (pulse != null) pulse.Failed -= OnFailed;
        }

        private void OnFailed()
        {
            failFlash = 0.6f;
        }

        private void Update()
        {
            if (pulse == null || label == null) return;

            int seconds = pulse.IsReady ? -1 : Mathf.CeilToInt(pulse.CooldownRemaining);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                label.text = seconds < 0 ? "PULSO · R" : "PULSO  " + seconds + " s";
            }

            if (failFlash > 0f) failFlash -= Time.unscaledDeltaTime;
            label.color = failFlash > 0f ? failColor : pulse.IsReady ? readyColor : waitingColor;
        }
    }
}
