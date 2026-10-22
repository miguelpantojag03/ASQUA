using System.Collections.Generic;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Elige el diálogo que toca para una misión (Parte D, contexto emocional):
    ///  - Oferta: el diálogo del asset + 1-2 líneas de "por qué me importa" (Quest.emotionalContext),
    ///    dichas por quien la pide justo antes de "Aceptar / Ahora no".
    ///  - En curso: normal, nerviosa o aliviada según el avance real (QuestManager.GetMood).
    /// Los assets nunca se modifican: la oferta ampliada es una copia en memoria, creada una vez por misión.
    /// </summary>
    public static class QuestDialogues
    {
        private static readonly Dictionary<Quest, DialogueSequence> offerCache = new Dictionary<Quest, DialogueSequence>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            offerCache.Clear();
        }

        /// <summary>Diálogo de oferta con el contexto emocional al final (antes de las elecciones).</summary>
        public static DialogueSequence Offer(QuestRuntime quest, string speaker)
        {
            if (quest == null) return null;
            Quest data = quest.Data;
            DialogueSequence original = data.offerDialogue;
            if (original == null || string.IsNullOrWhiteSpace(data.emotionalContext)) return original;

            if (offerCache.TryGetValue(data, out DialogueSequence cached) && cached != null) return cached;

            DialogueSequence extended = ScriptableObject.CreateInstance<DialogueSequence>();
            extended.hideFlags = HideFlags.DontSave;
            extended.name = original.name + "_Contexto";
            extended.dialogueId = original.dialogueId + "_contexto";
            extended.lines = new List<DialogueLine>(original.lines);
            foreach (string raw in data.emotionalContext.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0) extended.lines.Add(new DialogueLine(speaker, line));
            }
            extended.choices = original.choices;
            offerCache[data] = extended;
            return extended;
        }

        /// <summary>Diálogo de "misión en curso" según el ánimo (o el de ambiente si no hay ninguno).</summary>
        public static DialogueSequence Progress(QuestRuntime quest, DialogueSequence fallback)
        {
            if (quest == null) return fallback;
            DialogueSequence d = QuestManager.Instance != null ? QuestManager.Instance.GetProgressDialogue(quest) : quest.Data.inProgressDialogue;
            return d != null ? d : fallback;
        }
    }
}
