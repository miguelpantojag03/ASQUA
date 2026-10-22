using Ascua.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.Combat
{
    /// <summary>
    /// Viñeta momentánea rojo-violeta al recibir daño (nunca roja "sangre": es la sombra que muerde
    /// la luz) más un destello de pantalla completa muy breve (0.15 s).
    /// Energía crítica (&lt; 20 %): el borde late con fuerza (0.25-0.5) hasta recuperar el 25 %.
    /// Todo se reduce al 30 % con Accesibilidad > Reducir destellos.
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
        [Header("Destello de daño")]
        [SerializeField] private CanvasGroup flashGroup;
        [SerializeField] private float flashPeak = 0.22f;
        [SerializeField] private float flashSeconds = 0.15f;

        [Header("Energía crítica")]
        [SerializeField] private float criticalMin = 0.25f;
        [SerializeField] private float criticalMax = 0.5f;
        [SerializeField] private float criticalBeatsPerSecond = 1.6f;

        private float alpha;
        private float flash;

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
            flash = 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, 0f, fadeSpeed * dt);
            flash = Mathf.MoveTowards(flash, 0f, dt / flashSeconds);

            // Energía crítica: latido fuerte y regular (como un corazón que se apaga).
            float floor = 0f;
            if (energy != null && energy.IsCritical)
            {
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * criticalBeatsPerSecond)), 3f);
                floor = Mathf.Lerp(criticalMin, criticalMax, beat);
            }
            float scale = AccessibilitySettings.FlashScale;
            if (group != null) group.alpha = Mathf.Max(alpha, floor) * scale;
            if (flashGroup != null) flashGroup.alpha = flash * flashPeak * scale;
        }
    }
}
