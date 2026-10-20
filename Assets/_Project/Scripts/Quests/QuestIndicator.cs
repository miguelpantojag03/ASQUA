using Ascua.Narrative;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Pequeño rombo flotante sobre un NPC:
    ///  - Ámbar, girando despacio: tiene una misión para ofrecer.
    ///  - Blanco cálido, latiendo: hay una misión cumplida para entregarle.
    ///  - Oculto: nada pendiente (o la misión está en curso).
    /// </summary>
    public class QuestIndicator : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private NpcInteractable npc;
        [SerializeField] private Renderer diamond;
        [SerializeField] private Color availableColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color turnInColor = new Color(1f, 0.95f, 0.82f);
        [SerializeField] private float intensity = 3f;
        [SerializeField] private float checkInterval = 0.25f;

        private MaterialPropertyBlock propertyBlock;
        private Vector3 basePosition;
        private float timer;
        private QuestState shownState = QuestState.Bloqueada;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (npc == null) npc = GetComponentInParent<NpcInteractable>();
            if (diamond != null)
            {
                basePosition = diamond.transform.localPosition;
                diamond.enabled = false;
            }
        }

        private void Update()
        {
            if (diamond == null || npc == null) return;

            timer += Time.unscaledDeltaTime;
            if (timer >= checkInterval)
            {
                timer = 0f;
                QuestRuntime q = QuestManager.Instance != null ? QuestManager.Instance.GetQuestForNpc(npc.NpcId) : null;
                shownState = q != null && npc.CanInteract ? q.State : QuestState.Bloqueada;
                diamond.enabled = shownState == QuestState.Disponible || shownState == QuestState.PorEntregar;
            }
            if (!diamond.enabled) return;

            // Flota y gira; si hay que entregar, late más fuerte.
            float t = Time.unscaledTime;
            Transform tr = diamond.transform;
            tr.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * 2f) * 0.06f);
            tr.localRotation = Quaternion.Euler(45f, t * 60f, 45f);

            bool turnIn = shownState == QuestState.PorEntregar;
            float pulse = turnIn ? 0.6f + 0.4f * Mathf.Sin(t * 6f) : 1f;
            diamond.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, (turnIn ? turnInColor : availableColor) * (intensity * pulse));
            diamond.SetPropertyBlock(propertyBlock);
        }
    }
}
