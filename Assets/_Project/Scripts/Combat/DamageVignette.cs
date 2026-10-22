using Ascua.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.Combat
{
    /// <summary>
    /// Viñeta momentánea rojo-violeta al recibir daño (nunca roja "sangre": es la sombra que muerde
    /// la luz). Su intensidad se reduce al 30 % con Accesibilidad > Reducir destellos.
    /// Con energía muy baja deja un borde tenue permanente, como aviso.
    /// </summary>
    public class DamageVignette : MonoBehaviour
    {
        public static DamageVignette Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image image;
        [SerializeField] private Color color = new Color(0.55f, 0.1f, 0.38f, 1f);
        [SerializeField] private float peakAlpha = 0.6f;
        [SerializeField] private float fadeSpeed = 1.8f;
        [SerializeField] private Lighting.PlayerEnergy energy;
        [SerializeField, Range(0f, 1f)] private float lowEnergyThreshold = 0.15f;

        private float alpha;

        private void Awake()
        {
            Instance = this;
            if (image != null) image.color = color;
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Golpe recibido: 0-1 según su fuerza.</summary>
        public void Pulse(float strength)
        {
            alpha = Mathf.Max(alpha, Mathf.Lerp(0.35f, 1f, strength) * peakAlpha);
        }

        private void Update()
        {
            alpha = Mathf.MoveTowards(alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
            float floor = 0f;
            if (energy != null && energy.Normalized < lowEnergyThreshold) floor = 0.18f * (1f - energy.Normalized / lowEnergyThreshold);
            if (group != null) group.alpha = Mathf.Max(alpha, floor) * AccessibilitySettings.FlashScale;
        }
    }
}
