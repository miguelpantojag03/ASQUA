using Ascua.Combat;
using Ascua.Core;
using Ascua.Narrative;
using UnityEngine;

namespace Ascua.UI
{
    /// <summary>
    /// Jerarquía visual cuando coinciden varias cosas a la vez. El HUD está repartido en dos capas
    /// (contenedores con CanvasGroup, sin tocar la opacidad que gestiona cada panel por su cuenta):
    ///  - Capa secundaria: objetivo, Esperanza, noche, misiones, inventario, flecha, tutorial.
    ///  - Capa de combate: energía del Farol y combo.
    /// Prioridades:
    ///  1. Diálogo abierto: lo secundario baja a 0.25 y el combate a 0.5 (se lee la conversación).
    ///  2. Combate (oleada activa, golpe dado o recibido hace &lt; 3 s): lo secundario baja a 0.55;
    ///     energía y combo al 100 % y el bloque de energía "late" un poco más grande.
    ///  3. Exploración: todo al 100 %.
    /// </summary>
    public class HudFocus : MonoBehaviour
    {
        [SerializeField] private CanvasGroup secondaryLayer;
        [SerializeField] private CanvasGroup combatLayer;
        [Tooltip("Bloque de la energía (se resalta en combate).")]
        [SerializeField] private RectTransform energyBlock;
        [SerializeField] private float fadeSpeed = 4f;
        [SerializeField] private float combatMemory = 3f;

        [Header("Opacidades")]
        [SerializeField, Range(0f, 1f)] private float secondaryInDialogue = 0.25f;
        [SerializeField, Range(0f, 1f)] private float combatInDialogue = 0.5f;
        [SerializeField, Range(0f, 1f)] private float secondaryInCombat = 0.55f;
        [SerializeField] private float energyCombatScale = 1.06f;

        private float lastCombatTime = -100f;

        /// <summary>Estado actual (para pruebas y depuración).</summary>
        public string Mode { get; private set; } = "Exploracion";
        public float SecondaryAlpha => secondaryLayer != null ? secondaryLayer.alpha : 1f;

        private void OnEnable()
        {
            EventBus.PlayerAttacked += OnCombat;
            EventBus.PlayerHurt += OnHurt;
            EventBus.ShadowHit += OnShadowHit;
        }

        private void OnDisable()
        {
            EventBus.PlayerAttacked -= OnCombat;
            EventBus.PlayerHurt -= OnHurt;
            EventBus.ShadowHit -= OnShadowHit;
        }

        private void OnCombat(int step) => lastCombatTime = Time.unscaledTime;
        private void OnHurt(float amount) => lastCombatTime = Time.unscaledTime;
        private void OnShadowHit(Vector3 position, bool strong) => lastCombatTime = Time.unscaledTime;

        private void Update()
        {
            bool dialogue = DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen;
            bool wave = CombatWaveDirector.Instance != null && CombatWaveDirector.Instance.HasActiveWave;
            bool combat = wave || Time.unscaledTime - lastCombatTime < combatMemory;

            float secondary = 1f, combatAlpha = 1f, energyScale = 1f;
            if (dialogue)
            {
                Mode = "Dialogo";
                secondary = secondaryInDialogue;
                combatAlpha = combatInDialogue;
            }
            else if (combat)
            {
                Mode = "Combate";
                secondary = secondaryInCombat;
                energyScale = energyCombatScale;
            }
            else
            {
                Mode = "Exploracion";
            }

            float k = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            if (secondaryLayer != null) secondaryLayer.alpha = Mathf.Lerp(secondaryLayer.alpha, secondary, k);
            if (combatLayer != null) combatLayer.alpha = Mathf.Lerp(combatLayer.alpha, combatAlpha, k);
            if (energyBlock != null) energyBlock.localScale = Vector3.Lerp(energyBlock.localScale, Vector3.one * energyScale, k);
        }
    }
}
