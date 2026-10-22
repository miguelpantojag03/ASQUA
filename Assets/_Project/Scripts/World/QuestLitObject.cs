using Ascua.Core;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Un objeto del mundo que se enciende cuando se completa una misión y se queda encendido en las
    /// noches siguientes (p. ej. el farol de los Bravo tras "Todavía sirvo"). Así la ciudad recuerda lo que hiciste.
    /// Barato: una luz sin sombras y unos renderers que se activan; nada se actualiza cada frame.
    /// </summary>
    public class QuestLitObject : MonoBehaviour
    {
        [Tooltip("Id de la misión que lo enciende.")]
        [SerializeField] private string questId;
        [SerializeField] private Light lightSource;
        [SerializeField] private float lightIntensity = 3.5f;
        [Tooltip("Llama / cristal encendido (se activan al encenderse).")]
        [SerializeField] private Renderer[] litRenderers;
        [Tooltip("Segundos que tarda en prender (la mecha chisporrotea un poco).")]
        [SerializeField] private float igniteSeconds = 1.2f;

        private float igniteTimer = -1f;

        public bool IsLit { get; private set; }

        private void OnEnable()
        {
            EventBus.QuestStateChanged += OnQuestStateChanged;
        }

        private void OnDisable()
        {
            EventBus.QuestStateChanged -= OnQuestStateChanged;
        }

        private void Start()
        {
            SetLit(GameProgress.IsQuestCompleted(questId), 1f);
        }

        private void OnQuestStateChanged(string id, string state)
        {
            if (id != questId || IsLit) return;
            if (state == "Completada")
            {
                IsLit = true;
                igniteTimer = 0f;
                SetRenderers(true);
                if (lightSource != null) lightSource.enabled = true;
            }
        }

        private void Update()
        {
            if (igniteTimer < 0f) return;
            igniteTimer += Time.deltaTime;
            float k = Mathf.Clamp01(igniteTimer / igniteSeconds);
            // Prende con un par de chisporroteos y luego se asienta.
            float sputter = k < 1f ? 0.6f + 0.4f * Mathf.PerlinNoise(igniteTimer * 14f, 0.3f) : 1f;
            if (lightSource != null) lightSource.intensity = lightIntensity * k * sputter;
            if (k >= 1f) igniteTimer = -1f;
        }

        private void SetLit(bool lit, float amount)
        {
            IsLit = lit;
            SetRenderers(lit);
            if (lightSource != null)
            {
                // Apagada = desactivada (una luz a 0 seguiría costando en Forward+).
                lightSource.enabled = lit;
                lightSource.intensity = lit ? lightIntensity * amount : 0f;
            }
        }

        private void SetRenderers(bool on)
        {
            if (litRenderers == null) return;
            foreach (Renderer r in litRenderers)
            {
                if (r != null) r.enabled = on;
            }
        }
    }
}
