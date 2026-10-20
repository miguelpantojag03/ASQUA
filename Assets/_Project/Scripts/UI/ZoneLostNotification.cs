using System.Collections.Generic;
using Ascua.World;
using TMPro;
using UnityEngine;

namespace Ascua.UI
{
    /// <summary>
    /// Sistema simple de avisos en pantalla ("toast"), reutilizable: cualquier sistema puede llamar
    /// ZoneLostNotification.Instance.Show("texto"). Los avisos se encolan y se muestran de uno en uno
    /// con fundido. Por defecto escucha la muerte de zonas ("El Mercado se apagó"); en la Fase 3
    /// servirá para eventos narrativos.
    /// </summary>
    public class ZoneLostNotification : MonoBehaviour
    {
        private enum Phase { Idle, FadeIn, Hold, FadeOut }

        public static ZoneLostNotification Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private RectTransform panel;

        [Header("Tiempos (s, sin escalar)")]
        [SerializeField] private float fadeInDuration = 0.35f;
        [SerializeField] private float defaultHoldDuration = 2.6f;
        [SerializeField] private float fadeOutDuration = 0.9f;
        [Tooltip("Duración de un aviso cuando hay otros esperando detrás.")]
        [SerializeField] private float busyHoldDuration = 1.1f;
        [SerializeField] private int maxQueued = 3;

        [Header("Zonas")]
        [SerializeField] private bool announceZoneLoss = true;
        [Tooltip("{0} = nombre de la zona.")]
        [SerializeField] private string zoneLostFormat = "{0} se apagó";

        private readonly Queue<(string message, float hold)> queue = new Queue<(string, float)>();
        private Phase phase = Phase.Idle;
        private float timer;
        private float currentHold;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            Instance = this;
            if (panel != null) baseScale = panel.localScale;
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            ZoneHope.AnyZoneDied += OnZoneDied;
        }

        private void OnDisable()
        {
            ZoneHope.AnyZoneDied -= OnZoneDied;
        }

        /// <summary>Encola un aviso. holdSeconds &lt;= 0 usa la duración por defecto.</summary>
        public void Show(string message, float holdSeconds = -1f)
        {
            if (string.IsNullOrEmpty(message)) return;
            // Nunca más de 3 en espera: si se acumulan, se descartan los más viejos (ya no son noticia).
            while (queue.Count >= maxQueued) queue.Dequeue();
            queue.Enqueue((message, holdSeconds > 0f ? holdSeconds : defaultHoldDuration));
        }

        private void OnZoneDied(ZoneHope zone)
        {
            if (announceZoneLoss) Show(string.Format(zoneLostFormat, zone.DisplayName));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            switch (phase)
            {
                case Phase.Idle:
                    if (queue.Count == 0) return;
                    (string message, float hold) next = queue.Dequeue();
                    if (messageText != null) messageText.text = next.message;
                    currentHold = next.hold;
                    timer = 0f;
                    phase = Phase.FadeIn;
                    break;

                case Phase.FadeIn:
                    timer += dt;
                    float tIn = Mathf.Clamp01(timer / fadeInDuration);
                    SetAlpha(tIn);
                    // Pequeño "golpe" de escala al aparecer.
                    if (panel != null) panel.localScale = baseScale * Mathf.Lerp(1.08f, 1f, tIn * tIn);
                    if (tIn >= 1f) { phase = Phase.Hold; timer = 0f; }
                    break;

                case Phase.Hold:
                    timer += dt;
                    // Si hay más avisos esperando, este se queda menos tiempo.
                    float hold = queue.Count > 0 ? Mathf.Min(currentHold, busyHoldDuration) : currentHold;
                    if (timer >= hold) { phase = Phase.FadeOut; timer = 0f; }
                    break;

                case Phase.FadeOut:
                    timer += dt;
                    float tOut = Mathf.Clamp01(timer / fadeOutDuration);
                    SetAlpha(1f - tOut);
                    if (tOut >= 1f) phase = Phase.Idle;
                    break;
            }
        }

        private void SetAlpha(float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }
}
