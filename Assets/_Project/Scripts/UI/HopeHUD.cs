using Ascua.Core;
using Ascua.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// HUD de la Fase 2: temporizador de la noche (barra + mm:ss) y Esperanza de cada zona.
    /// Una fila por zona: nombre (ámbar si el Farol la está iluminando), barra y valor;
    /// la fila parpadea si la zona está en peligro y se apaga si muere.
    /// </summary>
    public class HopeHUD : MonoBehaviour
    {
        [Header("Noche")]
        [SerializeField] private NightManager night;
        [SerializeField] private RectTransform timerFill;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text nightLabel;
        [SerializeField] private float urgentSeconds = 10f;

        [Header("Zonas (mismo orden en todos los arrays)")]
        [SerializeField] private ZoneHope[] zones;
        [SerializeField] private TMP_Text[] zoneNames;
        [SerializeField] private RectTransform[] zoneFills;
        [SerializeField] private Image[] zoneFillImages;
        [SerializeField] private TMP_Text[] zoneValues;

        [Header("Colores")]
        [SerializeField] private Color hopeColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color dangerColor = new Color(0.86f, 0.45f, 0.25f);
        [SerializeField] private Color deadColor = new Color(0.3f, 0.32f, 0.42f);
        [SerializeField] private Color nameColor = new Color(0.72f, 0.77f, 0.93f);
        [SerializeField, Range(0f, 100f)] private float dangerThreshold = 25f;

        private int[] shownValues;
        private int shownSeconds = -1;

        private void Awake()
        {
            if (night == null) night = FindFirstObjectByType<NightManager>();
            shownValues = new int[zones != null ? zones.Length : 0];
            for (int i = 0; i < shownValues.Length; i++) shownValues[i] = -1;
        }

        private void Start()
        {
            if (nightLabel != null && night != null) nightLabel.text = "NOCHE " + night.NightNumber;
            for (int i = 0; zones != null && i < zones.Length; i++)
            {
                if (zones[i] != null && zoneNames[i] != null) zoneNames[i].text = zones[i].DisplayName;
            }
        }

        private void Update()
        {
            UpdateTimer();
            UpdateZones();
        }

        private void UpdateTimer()
        {
            if (night == null) return;

            float remaining01 = 1f - night.Progress01;
            if (timerFill != null) timerFill.anchorMax = new Vector2(remaining01, 1f);

            bool urgent = night.Remaining <= urgentSeconds && night.IsRunning;
            if (timerFillImage != null)
            {
                timerFillImage.color = urgent
                    ? Color.Lerp(dangerColor, hopeColor, Mathf.PingPong(Time.unscaledTime * 3f, 1f))
                    : nameColor;
            }

            int seconds = Mathf.CeilToInt(night.Remaining);
            if (timerText != null && seconds != shownSeconds)
            {
                shownSeconds = seconds;
                timerText.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            }
        }

        private void UpdateZones()
        {
            if (zones == null) return;

            for (int i = 0; i < zones.Length; i++)
            {
                ZoneHope zone = zones[i];
                if (zone == null) continue;

                float normalized = zone.Normalized;
                if (zoneFills[i] != null)
                {
                    Vector2 max = zoneFills[i].anchorMax;
                    max.x = Mathf.Lerp(max.x, normalized, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                    zoneFills[i].anchorMax = max;
                }

                Color barColor;
                if (zone.IsDead) barColor = deadColor;
                else if (zone.Hope < dangerThreshold) barColor = Color.Lerp(dangerColor, hopeColor, Mathf.PingPong(Time.unscaledTime * 2f, 1f));
                else barColor = hopeColor;
                if (zoneFillImages[i] != null) zoneFillImages[i].color = barColor;

                if (zoneNames[i] != null)
                {
                    zoneNames[i].color = zone.IsDead ? deadColor : zone.IsIlluminated ? hopeColor : nameColor;
                }

                if (zoneValues[i] != null)
                {
                    int value = zone.IsDead ? -2 : Mathf.CeilToInt(zone.Hope);
                    if (value != shownValues[i])
                    {
                        shownValues[i] = value;
                        zoneValues[i].text = zone.IsDead ? "apagada" : value.ToString();
                        zoneValues[i].color = zone.IsDead ? deadColor : hopeColor;
                    }
                }
            }
        }
    }
}
