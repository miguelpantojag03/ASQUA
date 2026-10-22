using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.Quests
{
    /// <summary>
    /// Una fila del panel de misiones: icono por tipo (estilo de línea de luz), título, quién la pidió,
    /// estado/progreso en texto y una barra fina para los objetivos numéricos.
    /// Al cambiar de estado (aceptar, cumplir, completar) hace un breve fundido + escala.
    /// </summary>
    public class QuestRowUI : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private Image progressFill;
        [SerializeField] private Image stateMark;

        private QuestRuntime bound;
        private QuestState lastState;
        private float pop;

        public void Bind(QuestRuntime quest, Sprite typeIcon, string giverName, Color stateColor)
        {
            gameObject.SetActive(true);
            if (quest != bound || quest.State != lastState)
            {
                // Animación solo cuando cambia algo de verdad (no en cada refresco).
                if (bound == quest) pop = 1f;
                bound = quest;
                lastState = quest.State;
            }

            if (icon != null)
            {
                icon.sprite = typeIcon;
                icon.color = quest.State == QuestState.Completada ? new Color(0.62f, 0.85f, 0.63f) : stateColor;
            }
            if (title != null) title.text = quest.Data.title;
            if (detail != null) detail.text = "<color=#B8C4EC99>" + giverName + "</color>  ·  " + quest.ProgressText;

            bool numeric = quest.State == QuestState.Activa && quest.Progress01 > 0f;
            if (progressRoot != null) progressRoot.SetActive(numeric);
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(quest.Progress01);
            if (stateMark != null) stateMark.color = stateColor;
        }

        public void Hide()
        {
            bound = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (pop <= 0f) return;
            pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 2.5f);
            float eased = pop * pop;
            if (root != null) root.localScale = Vector3.one * (1f + 0.06f * eased);
            if (group != null) group.alpha = 1f - 0.5f * Mathf.Sin(eased * Mathf.PI);
        }
    }
}
