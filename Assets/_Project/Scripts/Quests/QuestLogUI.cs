using System.Text;
using Ascua.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.Quests
{
    /// <summary>
    /// Panel de misiones de la noche (Tab / botón Select).
    /// Diseño: marco con filo ámbar (mismo lenguaje que la barra del Farol), cabecera con el nombre
    /// de la noche, una fila por misión (QuestRowUI: icono por tipo, estado, barra de progreso) y
    /// los Vínculos al pie. Al abrirse entra con un fundido + escala breve.
    /// Cerrado, deja el recordatorio compacto "Tab · Misiones (n)" con la misión más urgente.
    /// </summary>
    public class QuestLogUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panelGroup;
        [Tooltip("Raíz que escala al abrir (el propio panel).")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private QuestRowUI[] rows;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private TMP_Text bondsText;
        [SerializeField] private TMP_Text compactText;
        [Tooltip("Icono por tipo de misión, en el orden de QuestType.")]
        [SerializeField] private Sprite[] typeIcons;
        [SerializeField] private bool startOpen = false;
        [SerializeField] private float refreshInterval = 0.25f;
        [Tooltip("Personajes cuyo Vínculo se muestra (ids de NpcIdentity).")]
        [SerializeField] private string[] bondNpcIds = { "mina", "teo" };

        private readonly StringBuilder builder = new StringBuilder(256);
        private bool open;
        private float timer;
        private float openAnim;

        public bool IsOpen => open;

        private void Awake()
        {
            SetOpen(startOpen);
            openAnim = startOpen ? 1f : 0f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            bool toggle = (keyboard != null && keyboard.tabKey.wasPressedThisFrame) || (gamepad != null && gamepad.selectButton.wasPressedThisFrame);
            if (toggle && (DialogueRunner.Instance == null || !DialogueRunner.Instance.IsOpen)) SetOpen(!open);

            // Entrada del panel: fundido + escala de 0.96 a 1 en ~0.2 s.
            float dt = Time.unscaledDeltaTime;
            openAnim = Mathf.MoveTowards(openAnim, open ? 1f : 0f, dt * 5f);
            float eased = 1f - (1f - openAnim) * (1f - openAnim);
            if (panelGroup != null) panelGroup.alpha = eased;
            if (panelRoot != null) panelRoot.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
            if (compactText != null) compactText.alpha = 1f - eased;

            timer += dt;
            if (timer < refreshInterval) return;
            timer = 0f;
            Refresh();
        }

        public void SetOpen(bool value)
        {
            open = value;
            Refresh();
        }

        private void Refresh()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null) return;

            if (compactText != null && !open) compactText.text = BuildCompact(manager);
            if (!open) return;

            if (headerText != null)
            {
                string set = manager.CurrentSet != null ? manager.CurrentSet.title : "";
                headerText.text = "<b>MISIONES DE LA NOCHE</b>\n<size=80%><color=#B8C4EC99>" + set + "</color></size>";
            }

            int row = 0;
            var quests = manager.Quests;
            for (int i = 0; i < quests.Count && rows != null && row < rows.Length; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State == QuestState.Bloqueada) continue;
                rows[row].Bind(q, IconFor(q.Data.type), GiverName(q.Data.giverNpcId), StateColor(q.State));
                row++;
            }
            if (rows != null) for (int i = row; i < rows.Length; i++) rows[i].Hide();
            if (emptyText != null) emptyText.gameObject.SetActive(row == 0);

            if (bondsText != null)
            {
                builder.Clear();
                builder.Append("<b>VÍNCULOS</b>   ");
                foreach (string npcId in bondNpcIds)
                {
                    int level = manager.GetBond(npcId);
                    builder.Append(GiverName(npcId)).Append(" <color=#FFC46B>");
                    for (int b = 0; b < QuestManager.MaxBond; b++) builder.Append(b < level ? "●" : "<color=#FFFFFF33>●</color>");
                    builder.Append("</color>     ");
                }
                builder.Append("\n<color=#B8C4EC66>Tab · cerrar</color>");
                bondsText.SetText(builder);
            }
        }

        private Sprite IconFor(QuestType type)
        {
            int index = (int)type;
            return typeIcons != null && index >= 0 && index < typeIcons.Length ? typeIcons[index] : null;
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

        /// <summary>Color de estado (el mismo en filas, iconos y objetivo actual).</summary>
        public static Color StateColor(QuestState state)
        {
            switch (state)
            {
                case QuestState.Disponible: return new Color(1f, 0.77f, 0.42f, 0.65f);
                case QuestState.Activa: return new Color(1f, 0.77f, 0.42f);
                case QuestState.PorEntregar: return new Color(1f, 0.95f, 0.79f);
                case QuestState.Completada: return new Color(0.62f, 0.85f, 0.63f);
                case QuestState.Fallida: return new Color(0.86f, 0.45f, 0.25f);
                default: return new Color(0.72f, 0.77f, 0.93f, 0.4f);
            }
        }

        private static string GiverName(string npcId)
        {
            NpcInteractable npc = NpcInteractable.Find(npcId);
            return npc != null ? npc.DisplayName : npcId;
        }
    }
}
