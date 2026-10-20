using Ascua.Core;
using Ascua.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Momento de urgencia de la noche: una ráfaga de la Penumbra de 10 s, anunciada con antelación.
    /// Durante la ráfaga el muro avanza unos metros de golpe, la Esperanza cae más rápido y los bordes
    /// de la pantalla se oscurecen. Después la Penumbra retrocede a su ritmo normal.
    /// Configurable por noche desde NightQuestSet (momento, duración, fuerza).
    /// </summary>
    public class PenumbraSurge : MonoBehaviour
    {
        private enum Phase { Waiting, Warning, Surging, Receding, Done }

        [SerializeField] private bool surgeEnabled = true;
        [Tooltip("Momento de la noche (0-1) en que empieza el aviso.")]
        [SerializeField, Range(0f, 1f)] private float surgeAt = 0.55f;
        [SerializeField] private float warningSeconds = 3f;
        [SerializeField] private float surgeSeconds = 10f;
        [SerializeField] private float recedeSeconds = 4f;
        [Tooltip("Metros que la Penumbra avanza de más durante la ráfaga.")]
        [SerializeField] private float surgeMeters = 7f;
        [SerializeField] private float decayMultiplier = 1.7f;

        [Header("Efectos")]
        [SerializeField] private CanvasGroup vignette;
        [SerializeField] private CinemachineImpulseSource impulse;
        [SerializeField] private float impulseForce = 0.35f;

        private Phase phase = Phase.Waiting;
        private float timer;

        public bool IsSurging => phase == Phase.Surging;

        /// <summary>Ajustes de la noche (los aplica NightManager desde el NightQuestSet).</summary>
        public void Configure(bool enabledThisNight, float at, float seconds, float meters, float decay)
        {
            surgeEnabled = enabledThisNight;
            surgeAt = at;
            surgeSeconds = seconds;
            surgeMeters = meters;
            decayMultiplier = decay;
        }

        /// <summary>Lanza una ráfaga ahora mismo (empieza por el aviso). Para pruebas y depuración.</summary>
        public void TriggerNow()
        {
            if (phase == Phase.Warning || phase == Phase.Surging) return;
            phase = Phase.Waiting;
            surgeEnabled = true;
            surgeAt = 0f;
        }

        private void Start()
        {
            if (vignette != null) vignette.alpha = 0f;
        }

        private void OnDisable()
        {
            ZoneHope.GlobalDecayMultiplier = 1f;
            if (Penumbra.Instance != null) Penumbra.Instance.SetSurgeOffset(0f);
        }

        private void Update()
        {
            NightManager night = NightManager.Instance;
            if (!surgeEnabled || night == null || !night.IsRunning) return;
            float dt = Time.deltaTime;

            switch (phase)
            {
                case Phase.Waiting:
                    if (night.Progress01 >= surgeAt)
                    {
                        phase = Phase.Warning;
                        timer = 0f;
                        Toast("La Penumbra se agita… (" + Mathf.RoundToInt(surgeSeconds) + " s)");
                    }
                    break;

                case Phase.Warning:
                    timer += dt;
                    SetVignette(0.35f * Mathf.PingPong(timer * 2f, 1f));
                    if (timer >= warningSeconds)
                    {
                        phase = Phase.Surging;
                        timer = 0f;
                        ZoneHope.GlobalDecayMultiplier = decayMultiplier;
                        EventBus.RaisePenumbraSurge(true);
                        if (impulse != null && AccessibilitySettings.CameraShake) impulse.GenerateImpulseWithForce(impulseForce);
                    }
                    break;

                case Phase.Surging:
                    timer += dt;
                    float push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / 1.5f));
                    if (Penumbra.Instance != null) Penumbra.Instance.SetSurgeOffset(surgeMeters * push);
                    SetVignette(0.8f + 0.15f * Mathf.Sin(timer * 4f));
                    if (timer >= surgeSeconds)
                    {
                        phase = Phase.Receding;
                        timer = 0f;
                        ZoneHope.GlobalDecayMultiplier = 1f;
                        EventBus.RaisePenumbraSurge(false);
                        Toast("La Penumbra retrocede");
                    }
                    break;

                case Phase.Receding:
                    timer += dt;
                    float back = 1f - Mathf.Clamp01(timer / recedeSeconds);
                    if (Penumbra.Instance != null) Penumbra.Instance.SetSurgeOffset(surgeMeters * back);
                    SetVignette(0.8f * back);
                    if (timer >= recedeSeconds) phase = Phase.Done;
                    break;
            }
        }

        private void SetVignette(float alpha)
        {
            if (vignette != null) vignette.alpha = alpha;
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.6f);
        }
    }
}
