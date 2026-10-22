using Ascua.Combat;
using Ascua.Core;
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
    /// Parte C:
    ///  - Progreso doble: en misiones de doble objetivo, barra A (Esperanza de la zona que hay que
    ///    sostener, en rojo si está por debajo) y barra B (sombras restantes + tiempo).
    ///    En misiones con presupuesto, la barra B es la energía gastada.
    ///  - Al fallar una misión, durante 5 s se ve "Misión fallida" con el PORQUÉ, nunca solo el estado.
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

        [Header("Progreso doble y fallos (Parte C)")]
        [SerializeField] private GameObject secondaryRoot;
        [SerializeField] private Image secondaryFill;
        [SerializeField] private Color dangerColor = new Color(0.86f, 0.45f, 0.25f);
        [SerializeField] private Color secondaryColor = new Color(0.72f, 0.8f, 1f);
        [SerializeField] private float failureSeconds = 5f;

        private float timer;
        private string shownKey;
        private float pop;
        private float swapFade = 1f;
        private QuestRuntime failed;
        private float failureTimer;
        private QuestManager subscribed;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            AnimatePop(dt);

            SubscribeIfNeeded();
            if (failureTimer > 0f) failureTimer -= dt;

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

            if (failureTimer > 0f && failed != null)
            {
                ShowFailure(dialogue);
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
            ShowProgressBars(q);
        }

        /// <summary>Una barra (progreso) o dos (A y B) según el tipo de misión.</summary>
        private void ShowProgressBars(QuestRuntime q)
        {
            bool active = q.State == QuestState.Activa;
            Quest d = q.Data;
            bool dual = active && (d.type == QuestType.DobleObjetivo || q.EffEnergyBudget > 0f);

            if (active && d.type == QuestType.DobleObjetivo)
            {
                // A: Esperanza de la zona a sostener (roja si está por debajo del umbral).
                ZoneHope zone = ZoneHope.Find(d.targetZoneId);
                float hope = zone != null ? zone.Hope : 0f;
                SetBar(progressRoot, progressFill, hope / 100f, hope < q.EffThreshold ? dangerColor : AscuaPalette.Ambar);
                // B: sombras disueltas.
                SetBar(secondaryRoot, secondaryFill, q.Progress01, secondaryColor);
                return;
            }

            bool numeric = active && q.Progress01 > 0f;
            SetBar(progressRoot, progressFill, Mathf.Clamp01(q.Progress01), AscuaPalette.Ambar, numeric || dual);
            if (dual)
            {
                // Presupuesto: energía gastada (se pone roja al acercarse al límite).
                float used = q.EffEnergyBudget > 0f ? q.EnergySpent / q.EffEnergyBudget : 0f;
                SetBar(secondaryRoot, secondaryFill, used, used > 0.75f ? dangerColor : secondaryColor);
            }
            else if (secondaryRoot != null) secondaryRoot.SetActive(false);
        }

        private static void SetBar(GameObject root, Image fill, float value, Color color, bool visible = true)
        {
            if (root != null) root.SetActive(visible);
            if (fill == null) return;
            fill.fillAmount = Mathf.Clamp01(value);
            fill.color = color;
        }

        /// <summary>Tarjeta de fallo: título de la misión + el motivo concreto.</summary>
        private void ShowFailure(bool dialogue)
        {
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, dialogue ? 0f : 1f, 0.5f);
            string key = "fallo|" + failed.Data.questId;
            if (key != shownKey) StartPop(key);
            if (titleText != null) titleText.text = "Misión fallida · " + failed.Data.title;
            if (actionText != null) actionText.text = "<color=#DB7340>" + failed.FailReason + "</color>";
            if (icon != null) icon.color = dangerColor;
            if (progressRoot != null) progressRoot.SetActive(false);
            if (secondaryRoot != null) secondaryRoot.SetActive(false);
        }

        private void SubscribeIfNeeded()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == subscribed) return;
            if (subscribed != null) subscribed.QuestStateChanged -= OnQuestStateChanged;
            subscribed = manager;
            if (subscribed != null) subscribed.QuestStateChanged += OnQuestStateChanged;
        }

        private void OnDestroy()
        {
            if (subscribed != null) subscribed.QuestStateChanged -= OnQuestStateChanged;
        }

        private void OnQuestStateChanged(QuestRuntime q)
        {
            if (q.State != QuestState.Fallida) return;
            failed = q;
            failureTimer = failureSeconds;
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
            if (secondaryRoot != null) secondaryRoot.SetActive(false);
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

        /// <summary>Doble objetivo: una línea por objetivo, con su estado al momento.</summary>
        private static string DoubleAction(QuestRuntime q)
        {
            Quest d = q.Data;
            ZoneHope zone = ZoneHope.Find(d.targetZoneId);
            string zoneName = zone != null ? zone.DisplayName : d.targetZoneId;
            float hope = zone != null ? zone.Hope : 0f;
            string hopeColor = hope < q.EffThreshold ? "#DB7340" : "#FFC46B";
            int remaining = Mathf.Max(0, q.DefeatTarget - q.Defeated);
            int timeLeft = Mathf.Max(0, Mathf.CeilToInt(q.EffTimeLimit - q.Elapsed));
            ZoneHope defeatZone = ZoneHope.Find(d.defeatZoneId);
            string defeatName = defeatZone != null ? defeatZone.DisplayName : d.defeatZoneId;
            return "A · " + zoneName + " ≥ " + q.EffThreshold.ToString("0") + ": <color=" + hopeColor + ">" + hope.ToString("0") + "</color>\n"
                   + "B · sombras en " + defeatName + ": <color=#B8CCFF>" + remaining + "</color> · " + timeLeft + " s";
        }

        /// <summary>Frase de acción: lenguaje simple, una acción por momento.</summary>
        private static string BuildAction(QuestRuntime q)
        {
            Quest data = q.Data;
            switch (q.State)
            {
                case QuestState.Activa:
                    if (data.type == QuestType.DobleObjetivo) return DoubleAction(q);
                    if (string.IsNullOrEmpty(data.shortInstruction)) return q.ProgressText;
                    return data.shortInstruction + "\n<color=#FFC46B>" + q.ProgressText + "</color>";
                default:
                    return q.ProgressText;
            }
        }
    }
}
