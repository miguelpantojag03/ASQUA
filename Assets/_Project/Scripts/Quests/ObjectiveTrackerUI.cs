using Ascua.Combat;
using Ascua.Narrative;
using Ascua.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.Quests
{
    /// <summary>
    /// "Objetivo actual" siempre visible en el HUD (arriba a la izquierda): icono del tipo de misión,
    /// título, UNA frase con la acción concreta y una barra fina si el objetivo es numérico.
    /// Al aceptar o completar una misión (o empezar una oleada) el panel hace un fundido + escala
    /// breve en vez de cambiar el texto de golpe.
    /// Durante una oleada de sombras muestra "Defiende La Plaza · N sombras" (tiene prioridad).
    /// </summary>
    public class ObjectiveTrackerUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private float refreshInterval = 0.2f;

        [Header("Presentación")]
        [Tooltip("Raíz que escala en la animación de cambio.")]
        [SerializeField] private RectTransform popRoot;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private Image progressFill;
        [Tooltip("Icono por tipo de misión, en el orden de QuestType.")]
        [SerializeField] private Sprite[] typeIcons;
        [SerializeField] private Sprite combatIcon;

        private float timer;
        private string shownKey;
        private float pop;
        private float swapFade = 1f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            AnimatePop(dt);

            timer += dt;
            if (timer < refreshInterval) return;
            timer = 0f;

            bool dialogue = DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen;
            CombatWaveDirector waves = CombatWaveDirector.Instance;

            if (waves != null && waves.HasActiveWave)
            {
                ShowWave(waves, dialogue);
                return;
            }

            QuestManager manager = QuestManager.Instance;
            QuestRuntime q = manager != null ? manager.TrackedQuest : null;
            bool visible = q != null && !dialogue;
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visible ? swapFade : 0f, 0.5f);
            if (q == null) return;

            string action = BuildAction(q);
            string key = q.Data.questId + "|" + q.State;
            if (key != shownKey) StartPop(key);

            if (titleText != null) titleText.text = q.Data.title;
            if (actionText != null) actionText.text = action;
            if (icon != null)
            {
                int index = (int)q.Data.type;
                icon.sprite = typeIcons != null && index < typeIcons.Length ? typeIcons[index] : null;
                icon.color = QuestLogUI.StateColor(q.State);
            }
            bool numeric = q.State == QuestState.Activa && q.Progress01 > 0f;
            if (progressRoot != null) progressRoot.SetActive(numeric);
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(q.Progress01);
        }

        private void ShowWave(CombatWaveDirector waves, bool dialogue)
        {
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, dialogue ? 0f : swapFade, 0.5f);
            string key = "oleada|" + waves.ActiveZoneId;
            if (key != shownKey) StartPop(key);

            ZoneHope zone = ZoneHope.Find(waves.ActiveZoneId);
            string zoneName = zone != null ? zone.DisplayName : waves.ActiveZoneId;
            int count = waves.AliveCount;
            if (titleText != null) titleText.text = "Defiende " + zoneName;
            if (actionText != null) actionText.text = "Disuelve las sombras con tu luz\n<color=#FFC46B>" + count + (count == 1 ? " sombra" : " sombras") + "</color>";
            if (icon != null)
            {
                icon.sprite = combatIcon;
                icon.color = new Color(1f, 0.77f, 0.42f);
            }
            if (progressRoot != null) progressRoot.SetActive(zone != null);
            if (progressFill != null && zone != null) progressFill.fillAmount = zone.Hope / 100f;
        }

        private void StartPop(string key)
        {
            bool first = shownKey == null;
            shownKey = key;
            if (first) return;
            pop = 1f;
            swapFade = 0.2f;
        }

        /// <summary>Fundido + escala: entra un poco más grande y se asienta en ~0.35 s.</summary>
        private void AnimatePop(float dt)
        {
            if (pop <= 0f && swapFade >= 1f) return;
            pop = Mathf.MoveTowards(pop, 0f, dt * 3f);
            swapFade = Mathf.MoveTowards(swapFade, 1f, dt * 3.5f);
            if (popRoot != null) popRoot.localScale = Vector3.one * (1f + 0.07f * pop * pop);
            if (group != null && group.alpha > 0.01f) group.alpha = Mathf.Min(group.alpha + dt * 3.5f, swapFade);
        }

        /// <summary>Frase de acción: lenguaje simple, una acción por momento.</summary>
        private static string BuildAction(QuestRuntime q)
        {
            Quest data = q.Data;
            switch (q.State)
            {
                case QuestState.Activa:
                    if (string.IsNullOrEmpty(data.shortInstruction)) return q.ProgressText;
                    return data.shortInstruction + "\n<color=#FFC46B>" + q.ProgressText + "</color>";
                default:
                    return q.ProgressText;
            }
        }
    }
}
