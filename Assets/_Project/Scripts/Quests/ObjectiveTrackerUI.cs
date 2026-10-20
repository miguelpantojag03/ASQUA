using Ascua.Narrative;
using TMPro;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// "Objetivo actual" siempre visible en el HUD (arriba a la izquierda): título de la misión y
    /// UNA frase con la acción concreta. No hace falta abrir el panel (Tab) para saber qué hacer.
    /// </summary>
    public class ObjectiveTrackerUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text actionText;
        [SerializeField] private float refreshInterval = 0.2f;

        private float timer;
        private QuestRuntime shown;
        private string shownAction;

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;

            QuestManager manager = QuestManager.Instance;
            QuestRuntime q = manager != null ? manager.TrackedQuest : null;

            bool visible = q != null && (DialogueRunner.Instance == null || !DialogueRunner.Instance.IsOpen);
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, 0.5f);
            if (q == null) return;

            string action = BuildAction(q);
            if (q == shown && action == shownAction) return;
            shown = q;
            shownAction = action;
            if (titleText != null) titleText.text = q.Data.title;
            if (actionText != null) actionText.text = action;
        }

        /// <summary>Frase de acción: lenguaje simple, una acción por momento.</summary>
        private static string BuildAction(QuestRuntime q)
        {
            Quest data = q.Data;
            switch (q.State)
            {
                case QuestState.Disponible:
                    return q.ProgressText;
                case QuestState.PorEntregar:
                    return q.ProgressText;
                case QuestState.Activa:
                    if (string.IsNullOrEmpty(data.shortInstruction)) return q.ProgressText;
                    // Instrucción fija + progreso numérico entre paréntesis.
                    return data.shortInstruction + "\n<color=#FFC46B>" + q.ProgressText + "</color>";
                default:
                    return q.ProgressText;
            }
        }
    }
}
