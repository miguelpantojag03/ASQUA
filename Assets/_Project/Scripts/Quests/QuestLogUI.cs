using System.Text;
using Ascua.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.Quests
{
    /// <summary>
    /// Panel de misiones de la noche (Tab / botón Select). Lista cada misión con su estado y progreso
    /// en tiempo real, y el Vínculo con cada personaje. Cerrado, deja un pequeño recordatorio
    /// "Tab · Misiones (n)" con la misión más urgente.
    /// </summary>
    public class QuestLogUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private TMP_Text listText;
        [SerializeField] private TMP_Text compactText;
        [SerializeField] private bool startOpen = false;
        [SerializeField] private float refreshInterval = 0.25f;
        [Tooltip("Personajes cuyo Vínculo se muestra (ids de NpcIdentity).")]
        [SerializeField] private string[] bondNpcIds = { "mina", "teo" };

        private readonly StringBuilder builder = new StringBuilder(1024);
        private bool open;
        private float timer;

        public bool IsOpen => open;

        private void Awake()
        {
            SetOpen(startOpen);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool toggle = (keyboard != null && keyboard.tabKey.wasPressedThisFrame) || (gamepad != null && gamepad.selectButton.wasPressedThisFrame);
            if (toggle && (DialogueRunner.Instance == null || !DialogueRunner.Instance.IsOpen)) SetOpen(!open);

            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval) return;
            timer = 0f;
            Refresh();
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (panelGroup != null) panelGroup.alpha = value ? 1f : 0f;
            if (compactText != null) compactText.alpha = value ? 0f : 1f;
            Refresh();
        }

        private void Refresh()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null) return;

            if (compactText != null && !open) compactText.text = BuildCompact(manager);
            if (!open || listText == null) return;

            builder.Clear();
            builder.Append("<size=120%><b>MISIONES DE LA NOCHE</b></size>\n");
            if (manager.CurrentSet != null) builder.Append("<color=#B8C4EC99>").Append(manager.CurrentSet.title).Append("</color>\n");
            builder.Append('\n');

            int shown = 0;
            var quests = manager.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State == QuestState.Bloqueada) continue;
                shown++;

                builder.Append(StateIcon(q.State)).Append("  <b>").Append(q.Data.title).Append("</b>");
                builder.Append("  <color=#B8C4EC88>· ").Append(GiverName(q.Data.giverNpcId)).Append("</color>\n");
                builder.Append("     <color=").Append(StateColor(q.State)).Append('>').Append(q.ProgressText).Append("</color>\n");
                if (q.State == QuestState.Activa && q.Progress01 > 0f) builder.Append("     ").Append(ProgressBar(q.Progress01)).Append('\n');
                builder.Append('\n');
            }
            if (shown == 0) builder.Append("<color=#B8C4EC99>Nadie te ha pedido nada todavía. Habla con la gente de Vesper.</color>\n\n");

            builder.Append("<b>VÍNCULOS</b>\n");
            foreach (string npcId in bondNpcIds)
            {
                int level = manager.GetBond(npcId);
                builder.Append(GiverName(npcId)).Append("  <color=#FFC46B>");
                for (int b = 0; b < QuestManager.MaxBond; b++) builder.Append(b < level ? "●" : "<color=#FFFFFF33>●</color>");
                builder.Append("</color>");
                if (level >= 3) builder.Append("  <color=#FFC46BAA>(habilidad activa)</color>");
                builder.Append('\n');
            }
            builder.Append("\n<color=#B8C4EC66>Tab · cerrar</color>");
            listText.SetText(builder);
        }

        private static string BuildCompact(QuestManager manager)
        {
            int openCount = manager.CountOpenQuests();
            QuestRuntime focus = null;
            var quests = manager.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State == QuestState.PorEntregar) { focus = q; break; }
                if (q.State == QuestState.Activa && focus == null) focus = q;
            }

            string line = "Tab · Misiones (" + openCount + ")";
            if (focus != null) line += "\n<color=#FFC46B>" + focus.Data.title + "</color> · " + focus.ProgressText;
            return line;
        }

        private static string StateIcon(QuestState state)
        {
            switch (state)
            {
                case QuestState.Disponible: return "<color=#FFC46B>○</color>";
                case QuestState.Activa: return "<color=#FFC46B>●</color>";
                case QuestState.PorEntregar: return "<color=#FFF1C9>!</color>";
                case QuestState.Completada: return "<color=#9FD8A0>●</color>";
                case QuestState.Fallida: return "<color=#DB7340>×</color>";
                default: return "<color=#B8C4EC66>·</color>";
            }
        }

        private static string StateColor(QuestState state)
        {
            switch (state)
            {
                case QuestState.Activa: return "#B8C4EC";
                case QuestState.PorEntregar: return "#FFF1C9";
                case QuestState.Completada: return "#9FD8A0";
                case QuestState.Fallida: return "#DB7340";
                default: return "#B8C4EC99";
            }
        }

        private static string ProgressBar(float progress)
        {
            const int segments = 16;
            int filled = Mathf.RoundToInt(progress * segments);
            return "<color=#FFC46B>" + new string('■', filled) + "</color><color=#FFFFFF22>" + new string('■', segments - filled) + "</color>";
        }

        private static string GiverName(string npcId)
        {
            NpcInteractable npc = NpcInteractable.Find(npcId);
            return npc != null ? npc.DisplayName : npcId;
        }
    }
}
