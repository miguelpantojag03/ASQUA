using System;
using System.Collections.Generic;
using System.Text;
using Ascua.Characters;
using Ascua.Combat;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.UI;
using Ascua.World;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Gestor de misiones (único y persistente entre escenas).
    /// - Al empezar una noche, NightManager le pasa un NightQuestSet: esas son las misiones de la noche.
    /// - Sigue el progreso solo, escuchando el juego (Esperanza, luciérnagas, zonas perdidas, escoltas).
    /// - Lleva el Vínculo (0-5) con cada personaje, que persiste entre noches.
    /// - Avisa por eventos (y por EventBus) de cada cambio, para la UI, los gestos de Ilse y la Fase 4.
    /// Parte C (misiones más difíciles pero claras):
    /// - Doble objetivo simultáneo (A: mantener una zona MIENTRAS B: disolver sombras, con tiempo).
    /// - Límite de recurso (energía máxima gastada durante la misión).
    /// - Consecuencias tardías: las decisiones de noches anteriores cambian umbrales y tiempos.
    /// - Resumen multilínea al ofrecerla y fallos que SIEMPRE explican el porqué.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class QuestManager : MonoBehaviour
    {
        public const int MaxBond = 5;

        public static QuestManager Instance { get; private set; }

        [Tooltip("Cada cuánto se reescriben los textos de progreso (evita generar basura cada frame).")]
        [SerializeField] private float progressTextInterval = 0.25f;

        private readonly List<QuestRuntime> quests = new List<QuestRuntime>();
        private readonly Dictionary<string, int> bonds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> bondsAtNightStart = new Dictionary<string, int>();

        private readonly List<string> completedThisNight = new List<string>();
        private QuestRuntime trackedQuest;
        private NightQuestSet currentSet;
        private PlayerEnergy playerEnergy;
        private Transform player;
        private FireflyPool fireflyPool;
        private float textTimer;
        private float lastEnergy = -1f;
        private bool pageRewardPending;
        private readonly List<KeyValuePair<string, float>> pendingHopeBonus = new List<KeyValuePair<string, float>>();

        public event Action<QuestRuntime> QuestStateChanged;
        public event Action<QuestRuntime> QuestProgressChanged;
        public event Action<string, int> BondChanged;

        public IReadOnlyList<QuestRuntime> Quests => quests;
        public NightQuestSet CurrentSet => currentSet;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Ya existe uno de una noche anterior: ese conserva los Vínculos.
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            Firefly.AnyCollected += OnFireflyCollected;
            ZoneHope.AnyZoneDied += OnZoneDied;
        }

        private void OnDisable()
        {
            Firefly.AnyCollected -= OnFireflyCollected;
            ZoneHope.AnyZoneDied -= OnZoneDied;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================== Noche

        /// <summary>Activa las misiones del set para la noche que empieza.</summary>
        public void BeginNight(NightQuestSet set)
        {
            currentSet = set;
            quests.Clear();

            bondsAtNightStart.Clear();
            foreach (KeyValuePair<string, int> pair in bonds) bondsAtNightStart[pair.Key] = pair.Value;

            if (playerEnergy != null) playerEnergy.EnergyChanged -= OnEnergyChanged;
            playerEnergy = FindFirstObjectByType<PlayerEnergy>();
            if (playerEnergy != null)
            {
                playerEnergy.EnergyChanged += OnEnergyChanged;
                lastEnergy = playerEnergy.Current;
            }
            fireflyPool = FindFirstObjectByType<FireflyPool>();
            pendingHopeBonus.Clear();
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;

            completedThisNight.Clear();
            trackedQuest = null;

            if (set != null)
            {
                foreach (Quest quest in set.quests)
                {
                    // Lo ya cumplido en noches anteriores no se repite.
                    if (quest == null || GameProgress.IsQuestCompleted(quest.questId)) continue;
                    QuestRuntime runtime = new QuestRuntime(quest, QuestState.Bloqueada);
                    ApplyConditions(runtime);
                    quests.Add(runtime);
                }
            }

            RefreshLocks();
            foreach (QuestRuntime q in quests) UpdateProgressText(q);
        }

        /// <summary>Al terminar la noche: lo pendiente caduca; lo cumplido y sin entregar se entrega solo.</summary>
        public void EndNight()
        {
            foreach (QuestRuntime q in quests)
            {
                if (q.State == QuestState.PorEntregar) TurnIn(q);
                else if (q.State == QuestState.Activa || q.State == QuestState.Disponible) SetState(q, QuestState.Expirada);
            }
        }

        /// <summary>Partida nueva (desde el título): sin Vínculos ni misiones.</summary>
        public void ResetAll()
        {
            bonds.Clear();
            bondsAtNightStart.Clear();
            quests.Clear();
            completedThisNight.Clear();
            trackedQuest = null;
        }

        /// <summary>Al reintentar una noche, los Vínculos vuelven a como estaban al empezarla.</summary>
        public void RevertNightProgress()
        {
            bonds.Clear();
            foreach (KeyValuePair<string, int> pair in bondsAtNightStart) bonds[pair.Key] = pair.Value;
            foreach (string questId in completedThisNight) GameProgress.UnmarkQuest(questId);
            completedThisNight.Clear();
            GameProgress.RevertNightFlags();
        }

        /// <summary>
        /// Valores efectivos = los del asset + las consecuencias de decisiones anteriores (marcas de GameProgress).
        /// La Esperanza extra se aplica en el primer frame (las zonas aún no han arrancado en BeginNight).
        /// </summary>
        private void ApplyConditions(QuestRuntime q)
        {
            Quest d = q.Data;
            q.EffThreshold = d.hopeThreshold;
            q.EffHoldSeconds = d.holdSeconds;
            q.EffTimeLimit = d.timeLimit;
            q.EffEnergyBudget = d.energyBudget;
            q.ConditionNote = "";
            if (d.conditions == null) return;

            StringBuilder notes = new StringBuilder();
            foreach (QuestCondition c in d.conditions)
            {
                if (c == null || !GameProgress.HasFlag(c.flag)) continue;
                q.EffThreshold = Mathf.Clamp(q.EffThreshold + c.thresholdDelta, 0f, 100f);
                q.EffHoldSeconds = Mathf.Max(1f, q.EffHoldSeconds + c.holdSecondsDelta);
                if (q.EffTimeLimit > 0f) q.EffTimeLimit = Mathf.Max(5f, q.EffTimeLimit + c.timeLimitDelta);
                if (q.EffEnergyBudget > 0f) q.EffEnergyBudget = Mathf.Max(5f, q.EffEnergyBudget + c.energyBudgetDelta);
                if (c.startHopeBonus != 0f) pendingHopeBonus.Add(new KeyValuePair<string, float>(d.targetZoneId, c.startHopeBonus));
                if (!string.IsNullOrEmpty(c.note))
                {
                    if (notes.Length > 0) notes.Append('\n');
                    notes.Append(c.note);
                }
            }
            q.ConditionNote = notes.ToString();
        }

        // ================================================================== Objetivo actual (HUD y flecha)

        /// <summary>
        /// La misión que el HUD muestra como "objetivo actual": primero una por entregar, luego la última
        /// aceptada que siga activa, luego cualquier activa y, si no hay, la primera disponible (sin Tratos).
        /// </summary>
        public QuestRuntime TrackedQuest
        {
            get
            {
                QuestRuntime firstActive = null;
                QuestRuntime firstAvailable = null;
                for (int i = 0; i < quests.Count; i++)
                {
                    QuestRuntime q = quests[i];
                    if (q.State == QuestState.PorEntregar) return q;
                    if (q.State == QuestState.Activa && firstActive == null) firstActive = q;
                    if (q.State == QuestState.Disponible && firstAvailable == null && q.Data.type != QuestType.TratoHeraldo) firstAvailable = q;
                }
                if (trackedQuest != null && trackedQuest.State == QuestState.Activa) return trackedQuest;
                return firstActive ?? firstAvailable;
            }
        }

        /// <summary>Dónde está el objetivo de una misión ahora mismo (para la flecha del HUD).</summary>
        public bool TryGetObjectivePosition(QuestRuntime q, out Vector3 position)
        {
            position = Vector3.zero;
            if (q == null) return false;
            Quest data = q.Data;

            if (q.State == QuestState.Disponible) return TryGetNpcPosition(data.giverNpcId, out position);
            if (q.State == QuestState.PorEntregar) return TryGetNpcPosition(data.TurnInNpcId, out position);
            if (q.State != QuestState.Activa) return false;

            switch (data.type)
            {
                case QuestType.RescateEsperanza:
                    ZoneHope zone = ZoneHope.Find(data.targetZoneId);
                    if (zone == null) return false;
                    position = zone.Center;
                    return true;

                case QuestType.Recoleccion:
                    ZoneHope collectZone = ZoneHope.Find(data.targetZoneId);
                    if (fireflyPool != null && fireflyPool.TryGetNearestActive(player != null ? player.position : Vector3.zero,
                            collectZone != null ? collectZone.Center : (Vector3?)null,
                            collectZone != null ? collectZone.Radius * data.collectRadiusMultiplier : 0f, out position)) return true;
                    if (collectZone == null) return false;
                    position = collectZone.Center;
                    return true;

                case QuestType.Escolta:
                    QuestPoint destination = QuestPoint.Find(data.escortDestinationId);
                    if (destination == null) return false;
                    position = destination.transform.position;
                    return true;

                case QuestType.DobleObjetivo:
                    // Si la zona A está en peligro, la flecha manda allí; si no, a la sombra más cercana.
                    ZoneHope holdZone = ZoneHope.Find(data.targetZoneId);
                    if (holdZone != null && holdZone.Hope < q.EffThreshold + 6f)
                    {
                        position = holdZone.Center;
                        return true;
                    }
                    if (TryNearestTagged(q.CreatureTag, out position)) return true;
                    ZoneHope defeatZone = ZoneHope.Find(data.defeatZoneId);
                    if (defeatZone == null) return false;
                    position = defeatZone.Center;
                    return true;

                case QuestType.Entrega:
                    for (int i = 0; i < PickupItem.All.Count; i++)
                    {
                        PickupItem item = PickupItem.All[i];
                        if (item.ItemId == data.deliverItemId && item.IsAvailable)
                        {
                            position = item.transform.position;
                            return true;
                        }
                    }
                    return false;

                default:
                    return TryGetNpcPosition(data.giverNpcId, out position);
            }
        }

        private static bool TryGetNpcPosition(string npcId, out Vector3 position)
        {
            NpcInteractable npc = NpcInteractable.Find(npcId);
            position = npc != null ? npc.transform.position : Vector3.zero;
            return npc != null && npc.CanInteract;
        }

        // ================================================================== Consultas

        public QuestRuntime Find(Quest quest)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].Data == quest) return quests[i];
            }
            return null;
        }

        public QuestRuntime FindById(string questId)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].Data.questId == questId) return quests[i];
            }
            return null;
        }

        /// <summary>La misión más relevante de un NPC: primero la que hay que entregar, luego la activa, luego la disponible.</summary>
        public QuestRuntime GetQuestForNpc(string npcId)
        {
            QuestRuntime best = null;
            int bestPriority = 0;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                // Las entregas se resuelven hablando con el destinatario, no con quien la dio.
                bool isTurnIn = q.State == QuestState.PorEntregar && q.Data.TurnInNpcId == npcId;
                bool isGiver = q.State != QuestState.PorEntregar && q.Data.giverNpcId == npcId;
                if (!isTurnIn && !isGiver) continue;
                int priority = q.State == QuestState.PorEntregar ? 3 : q.State == QuestState.Activa ? 2 : q.State == QuestState.Disponible ? 1 : 0;
                if (priority > bestPriority)
                {
                    best = q;
                    bestPriority = priority;
                }
            }
            return best;
        }

        public int GetBond(string npcId)
        {
            return bonds.TryGetValue(npcId, out int value) ? value : 0;
        }

        public int CountOpenQuests()
        {
            int count = 0;
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].IsOpen) count++;
            }
            return count;
        }

        // ================================================================== Acciones (las llama el diálogo)

        public void Accept(QuestRuntime q)
        {
            if (q == null || q.State != QuestState.Disponible) return;

            q.Timer = 0f;
            q.Count = 0;
            trackedQuest = q;
            SetState(q, QuestState.Activa);

            switch (q.Data.type)
            {
                case QuestType.Recoleccion:
                    // Mientras dure, las luciérnagas prefieren aparecer en la zona de la misión.
                    ZoneHope zone = ZoneHope.Find(q.Data.targetZoneId);
                    if (zone != null && fireflyPool != null) fireflyPool.SetHotspot(q.Data.questId, zone.Center, zone.Radius);
                    break;

                case QuestType.Escolta:
                    CharacterAI npc = CharacterAI.Find(q.Data.escortNpcId);
                    QuestPoint destination = QuestPoint.Find(q.Data.escortDestinationId);
                    if (npc == null || destination == null)
                    {
                        Fail(q, "Falta el personaje o el destino en la escena.");
                        return;
                    }
                    npc.StartEscort(destination.transform.position);
                    break;

                case QuestType.DobleObjetivo:
                    q.CreatureTag = "mision:" + q.Data.questId;
                    q.Elapsed = 0f;
                    q.BelowTimer = 0f;
                    q.Defeated = 0;
                    CombatWaveDirector director = CombatWaveDirector.Instance;
                    List<ShadowCreature> group = director != null
                        ? director.SpawnGroup(q.Data.defeatZoneId, q.Data.defeatMoths, q.Data.defeatEchoes, q.CreatureTag)
                        : new List<ShadowCreature>();
                    q.DefeatTarget = group.Count;
                    break;
            }

            // Presupuesto de energía: se cuenta desde este momento.
            q.EnergySpent = 0f;
            if (playerEnergy != null) lastEnergy = playerEnergy.Current;

            UpdateProgressText(q);
            Toast("Nueva misión: " + q.Data.title);
        }

        /// <summary>"Ahora no": la misión sigue disponible para más tarde.</summary>
        public void Decline(QuestRuntime q)
        {
            if (q == null) return;
            EventBus.RaiseQuestStateChanged(q.Data.questId, "Rechazada");
        }

        /// <summary>Completa una misión de Diálogo o un Trato en el momento de elegir.</summary>
        public void CompleteFromChoice(QuestRuntime q, DialogueChoice choice)
        {
            if (q == null || q.State != QuestState.Disponible && q.State != QuestState.Activa) return;

            ApplyChoiceEffects(choice, q.Data.giverNpcId);
            GrantRewards(q);
            SetState(q, QuestState.Completada);

            // Un Trato del Heraldo no se celebra: es un alivio con precio.
            if (q.Data.type != QuestType.TratoHeraldo)
            {
                EventBus.RaiseCelebration("mision");
                Toast("Misión completada: " + q.Data.title + PageToastSuffix());
            }
            RefreshLocks();
        }

        /// <summary>Entrega una misión con el objetivo cumplido y da la recompensa.</summary>
        public void TurnIn(QuestRuntime q)
        {
            if (q == null || q.State != QuestState.PorEntregar) return;

            // Entregar: el objeto sale del inventario.
            if (q.Data.type == QuestType.Entrega && Ascua.Player.PickupSystem.Instance != null)
            {
                Ascua.Player.PickupSystem.Instance.Remove(q.Data.deliverItemId);
            }

            GrantRewards(q);
            SetState(q, QuestState.Completada);
            EventBus.RaiseCelebration("mision");
            Toast("Misión completada: " + q.Data.title + PageToastSuffix());
            RefreshLocks();
        }

        /// <summary>Aplica los efectos propios de una elección de diálogo.</summary>
        public void ApplyChoiceEffects(DialogueChoice choice, string npcId)
        {
            if (choice == null) return;

            if (Mathf.Abs(choice.hopeDelta) > 0.01f)
            {
                ZoneHope zone = ZoneHope.Find(choice.hopeZoneId);
                if (zone != null) zone.AddHope(choice.hopeDelta);
            }
            if (choice.energyDelta > 0f && playerEnergy != null) playerEnergy.Restore(choice.energyDelta);
            if (choice.bondDelta != 0 && !string.IsNullOrEmpty(npcId)) AddBond(npcId, choice.bondDelta);
            if (!string.IsNullOrEmpty(choice.setFlag)) GameProgress.SetFlag(choice.setFlag);
            if (!string.IsNullOrEmpty(choice.toast)) Toast(choice.toast);
        }

        public void AddBond(string npcId, int delta)
        {
            int previous = GetBond(npcId);
            int value = Mathf.Clamp(previous + delta, 0, MaxBond);
            if (value == previous) return;

            bonds[npcId] = value;
            BondChanged?.Invoke(npcId, value);
            EventBus.RaiseBondChanged(npcId, value);
            if (value > previous) EventBus.RaiseCelebration("vinculo");
        }

        // ================================================================== Progreso

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || quests.Count == 0) return;

            if (pendingHopeBonus.Count > 0)
            {
                foreach (KeyValuePair<string, float> bonus in pendingHopeBonus)
                {
                    ZoneHope zone = ZoneHope.Find(bonus.Key);
                    if (zone != null) zone.AddHope(bonus.Value);
                }
                pendingHopeBonus.Clear();
            }

            textTimer += dt;
            bool refreshTexts = textTimer >= progressTextInterval;
            if (refreshTexts) textTimer = 0f;

            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State != QuestState.Activa) continue;

                switch (q.Data.type)
                {
                    case QuestType.RescateEsperanza: UpdateRescue(q, dt); break;
                    case QuestType.Escolta: UpdateEscort(q, dt); break;
                    case QuestType.DobleObjetivo: UpdateDouble(q, dt); break;
                    case QuestType.Entrega:
                        if (Ascua.Player.PickupSystem.Instance != null && Ascua.Player.PickupSystem.Instance.Has(q.Data.deliverItemId)) ObjectiveMet(q);
                        break;
                }

                if (refreshTexts && q.State == QuestState.Activa) UpdateProgressText(q);
            }
        }

        private void UpdateRescue(QuestRuntime q, float dt)
        {
            ZoneHope zone = ZoneHope.Find(q.Data.targetZoneId);
            if (zone == null || zone.IsDead) return;

            // Hay que sostenerlo: si la Esperanza cae por debajo, el contador retrocede.
            q.Timer = zone.Hope >= q.EffThreshold ? q.Timer + dt : Mathf.Max(0f, q.Timer - dt);
            q.Progress01 = Mathf.Clamp01(q.Timer / Mathf.Max(0.01f, q.EffHoldSeconds));

            if (q.Timer >= q.EffHoldSeconds) ObjectiveMet(q);
        }

        /// <summary>Doble objetivo: A (zona ≥ umbral) se vigila todo el tiempo; B (sombras) tiene reloj.</summary>
        private void UpdateDouble(QuestRuntime q, float dt)
        {
            Quest d = q.Data;
            q.Elapsed += dt;

            ZoneHope holdZone = ZoneHope.Find(d.targetZoneId);
            if (holdZone != null)
            {
                q.BelowTimer = holdZone.Hope < q.EffThreshold ? q.BelowTimer + dt : 0f;
                if (q.BelowTimer > d.holdGraceSeconds || holdZone.IsDead)
                {
                    Fail(q, holdZone.DisplayName + " bajó de " + q.EffThreshold.ToString("0") + " de Esperanza mientras combatías en " + GetZoneName(d.defeatZoneId) + ".");
                    return;
                }
            }

            int remaining = CountTagged(q.CreatureTag);
            q.Defeated = Mathf.Max(0, q.DefeatTarget - remaining);
            q.Progress01 = q.DefeatTarget > 0 ? Mathf.Clamp01(1f - (float)remaining / q.DefeatTarget) : 1f;
            if (remaining == 0)
            {
                ObjectiveMet(q);
                return;
            }

            if (q.EffTimeLimit > 0f && q.Elapsed >= q.EffTimeLimit)
            {
                Fail(q, "Se acabó el tiempo: " + (remaining == 1 ? "quedaba 1 sombra" : "quedaban " + remaining + " sombras") + " en " + GetZoneName(d.defeatZoneId) + ".");
            }
        }

        /// <summary>Energía gastada (Farol, ataques, daño) cuenta para los presupuestos de las misiones activas.</summary>
        private void OnEnergyChanged(float current, float max)
        {
            float spent = lastEnergy >= 0f ? lastEnergy - current : 0f;
            lastEnergy = current;
            if (spent <= 0f) return;
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State != QuestState.Activa || q.EffEnergyBudget <= 0f) continue;
                q.EnergySpent += spent;
                if (q.EnergySpent > q.EffEnergyBudget)
                {
                    Fail(q, "Gastaste " + Mathf.CeilToInt(q.EnergySpent) + " de " + q.EffEnergyBudget.ToString("0") + " de Energía: no quedó luz para el camino.");
                }
            }
        }

        private static int CountTagged(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return 0;
            int count = 0;
            var targets = LightTargetRegistry.All;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ShadowCreature c && c.IsAlive && c.ZoneId == tag) count++;
            }
            return count;
        }

        private bool TryNearestTagged(string tag, out Vector3 position)
        {
            position = Vector3.zero;
            float best = float.MaxValue;
            Vector3 from = player != null ? player.position : Vector3.zero;
            var targets = LightTargetRegistry.All;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!(targets[i] is ShadowCreature c) || !c.IsAlive || c.ZoneId != tag) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                position = c.transform.position;
            }
            return best < float.MaxValue;
        }

        // ================================================================== Claridad: resumen al ofrecer

        /// <summary>
        /// Resumen multilínea que se muestra junto a "Aceptar / Ahora no": condición principal,
        /// secundaria, límite de tiempo o de recurso y, si las hay, las consecuencias de decisiones pasadas.
        /// </summary>
        public string BuildOfferSummary(QuestRuntime q)
        {
            if (q == null) return "";
            Quest d = q.Data;
            StringBuilder sb = new StringBuilder();
            switch (d.type)
            {
                case QuestType.RescateEsperanza:
                    sb.Append("<b>Objetivo:</b> mantén ").Append(GetZoneName(d.targetZoneId)).Append(" con Esperanza ≥ ").Append(q.EffThreshold.ToString("0"))
                      .Append(" durante ").Append(q.EffHoldSeconds.ToString("0")).Append(" s.\n<b>Ojo:</b> si baja, el contador retrocede.");
                    break;
                case QuestType.Recoleccion:
                    sb.Append("<b>Objetivo:</b> recoge ").Append(d.requiredCount).Append(" luciérnagas");
                    if (d.collectRadiusMultiplier > 0f) sb.Append(" en ").Append(GetZoneName(d.targetZoneId));
                    sb.Append('.');
                    break;
                case QuestType.Escolta:
                    sb.Append("<b>Objetivo:</b> acompaña a ").Append(GetNpcName(d.escortNpcId));
                    if (!string.IsNullOrEmpty(d.escortDestinationName)) sb.Append(" hasta ").Append(d.escortDestinationName);
                    sb.Append(".\n<b>Además:</b> no te alejes más de ").Append(d.escortMaxDistance.ToString("0")).Append(" m ni dejes que la Penumbra la alcance.");
                    break;
                case QuestType.Entrega:
                    sb.Append("<b>Objetivo:</b> recoge ").Append(d.deliverItemName).Append(" y llévaselo a ").Append(GetNpcName(d.TurnInNpcId)).Append('.');
                    break;
                case QuestType.DobleObjetivo:
                    ZoneHope holdZone = ZoneHope.Find(d.targetZoneId);
                    sb.Append("<b>A, todo el rato:</b> ").Append(GetZoneName(d.targetZoneId)).Append(" con Esperanza ≥ ").Append(q.EffThreshold.ToString("0"));
                    if (holdZone != null) sb.Append(" (ahora ").Append(holdZone.Hope.ToString("0")).Append(')');
                    sb.Append(". Si baja más de ").Append(d.holdGraceSeconds.ToString("0")).Append(" s, fallas.");
                    sb.Append("\n<b>B, a la vez:</b> disuelve las ").Append(d.defeatMoths + d.defeatEchoes).Append(" sombras que aparecerán en ").Append(GetZoneName(d.defeatZoneId)).Append('.');
                    break;
                default:
                    sb.Append("<b>Objetivo:</b> ").Append(string.IsNullOrEmpty(d.shortInstruction) ? d.title : d.shortInstruction);
                    break;
            }

            if (q.EffTimeLimit > 0f && d.type == QuestType.DobleObjetivo) sb.Append("\n<b>Límite:</b> ").Append(q.EffTimeLimit.ToString("0")).Append(" s para B.");
            if (q.EffEnergyBudget > 0f) sb.Append("\n<b>Límite:</b> no gastes más de ").Append(q.EffEnergyBudget.ToString("0")).Append(" de Energía (el Farol, los barridos y los golpes que recibas cuentan).");
            if (!string.IsNullOrEmpty(q.ConditionNote)) sb.Append("\n<color=#FFE3A0>").Append(q.ConditionNote).Append("</color>");
            return sb.ToString();
        }

        private void UpdateEscort(QuestRuntime q, float dt)
        {
            CharacterAI npc = CharacterAI.Find(q.Data.escortNpcId);
            if (npc == null) return;

            if (Penumbra.Instance != null && Penumbra.Instance.IsCovered(npc.transform.position))
            {
                Fail(q, "La Penumbra alcanzó a " + GetNpcName(q.Data.escortNpcId) + ".");
                return;
            }
            if (npc.Mood == CharacterMood.Collapsed)
            {
                Fail(q, GetNpcName(q.Data.escortNpcId) + " ya no puede seguir.");
                return;
            }

            if (player != null)
            {
                float distance = Vector3.Distance(player.position, npc.transform.position);
                q.Timer = distance > q.Data.escortMaxDistance ? q.Timer + dt : 0f;
                if (q.Timer > q.Data.escortGraceSeconds)
                {
                    Fail(q, "Te alejaste demasiado de " + GetNpcName(q.Data.escortNpcId) + ".");
                    return;
                }
            }

            q.Progress01 = npc.EscortProgress01;
            if (npc.EscortArrived) ObjectiveMet(q);
        }

        private void OnFireflyCollected(Firefly firefly)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State != QuestState.Activa || q.Data.type != QuestType.Recoleccion) continue;

                if (q.Data.collectRadiusMultiplier > 0f)
                {
                    ZoneHope zone = ZoneHope.Find(q.Data.targetZoneId);
                    if (zone != null)
                    {
                        Vector3 offset = firefly.transform.position - zone.Center;
                        offset.y = 0f;
                        if (offset.magnitude > zone.Radius * q.Data.collectRadiusMultiplier) continue;
                    }
                }

                q.Count++;
                q.Progress01 = Mathf.Clamp01((float)q.Count / Mathf.Max(1, q.Data.requiredCount));
                UpdateProgressText(q);
                if (q.Count >= q.Data.requiredCount) ObjectiveMet(q);
            }
        }

        private void OnZoneDied(ZoneHope zone)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State != QuestState.Activa || !q.Data.failIfTargetZoneDies) continue;
                bool usesZone = q.Data.type == QuestType.RescateEsperanza || q.Data.type == QuestType.Recoleccion;
                if (usesZone && q.Data.targetZoneId == zone.ZoneId) Fail(q, zone.DisplayName + " se apagó antes de que pudieras sostenerla.");
            }
        }

        private void ObjectiveMet(QuestRuntime q)
        {
            q.Progress01 = 1f;
            SetState(q, QuestState.PorEntregar);
            if (q.Data.type == QuestType.Escolta)
            {
                CharacterAI npc = CharacterAI.Find(q.Data.escortNpcId);
                if (npc != null) npc.HoldPosition();
            }
            UpdateProgressText(q);
            string who = GetNpcName(q.Data.TurnInNpcId);
            Toast(q.Data.type == QuestType.Entrega ? "Llévaselo a " + who : "Objetivo cumplido · vuelve con " + who);
        }

        private void Fail(QuestRuntime q, string reason)
        {
            q.FailReason = reason;
            SetState(q, QuestState.Fallida);
            UpdateProgressText(q);
            Toast("Misión fallida: " + reason);
        }

        private void GrantRewards(QuestRuntime q)
        {
            Quest data = q.Data;
            if (data.rewardEnergy > 0f && playerEnergy != null) playerEnergy.Restore(data.rewardEnergy);

            if (data.rewardHope > 0f)
            {
                ZoneHope zone = ZoneHope.Find(data.rewardHopeZoneId);
                if (zone != null) zone.AddHope(data.rewardHope);
            }
            if (data.penaltyHope > 0f)
            {
                ZoneHope zone = ZoneHope.Find(data.penaltyZoneId);
                if (zone != null) zone.AddHope(-data.penaltyHope);
            }
            if (data.rewardBond != 0) AddBond(data.giverNpcId, data.rewardBond);

            // Recompensa narrativa: una página nueva del Diario de Ilse.
            if (data.rewardDiaryPage != null && GameProgress.AddPage(data.rewardDiaryPage.pageId))
            {
                pageRewardPending = true;
            }
        }

        // ================================================================== Ánimo de quien la pidió (Parte D)

        /// <summary>
        /// Ánimo del personaje según el avance REAL de su misión activa:
        ///  - Nerviosa: queda menos del 35 % del límite de tiempo, la zona está bajo el umbral (o a 5 puntos),
        ///    Ilse se ha alejado en una escolta, se ha gastado más del 70 % del presupuesto de energía,
        ///    o a la noche le queda menos del 20 % (mín. 30 s). Si ya está casi hecha (≥ 85 %), no se pone nerviosa.
        ///  - Aliviada: más de la mitad hecha (≥ 50 %) y sin apuros.
        ///  - Normal: el resto.
        /// </summary>
        public QuestMood GetMood(QuestRuntime q)
        {
            if (q == null || q.State != QuestState.Activa) return QuestMood.Normal;
            Quest d = q.Data;
            bool urgent = false;

            switch (d.type)
            {
                case QuestType.RescateEsperanza:
                    ZoneHope zone = ZoneHope.Find(d.targetZoneId);
                    if (zone != null && zone.Hope < q.EffThreshold) urgent = true;
                    break;
                case QuestType.DobleObjetivo:
                    ZoneHope holdZone = ZoneHope.Find(d.targetZoneId);
                    if (holdZone != null && holdZone.Hope < q.EffThreshold + 5f) urgent = true;
                    if (q.EffTimeLimit > 0f && q.EffTimeLimit - q.Elapsed < q.EffTimeLimit * 0.35f) urgent = true;
                    break;
                case QuestType.Escolta:
                    if (q.Timer > 0f) urgent = true; // Ilse está demasiado lejos ahora mismo
                    break;
            }
            if (q.EffEnergyBudget > 0f && q.EnergySpent > q.EffEnergyBudget * 0.7f) urgent = true;

            NightManager night = NightManager.Instance;
            if (night != null && night.IsRunning && night.Remaining < Mathf.Max(30f, night.Duration * 0.2f)) urgent = true;

            if (urgent && q.Progress01 < 0.85f) return QuestMood.Nerviosa;
            if (q.Progress01 >= 0.5f) return QuestMood.Aliviada;
            return QuestMood.Normal;
        }

        /// <summary>El diálogo de "en curso" que toca según el ánimo (con el normal como respaldo).</summary>
        public DialogueSequence GetProgressDialogue(QuestRuntime q)
        {
            if (q == null) return null;
            switch (GetMood(q))
            {
                case QuestMood.Nerviosa when q.Data.progressNervousDialogue != null: return q.Data.progressNervousDialogue;
                case QuestMood.Aliviada when q.Data.progressReliefDialogue != null: return q.Data.progressReliefDialogue;
                default: return q.Data.inProgressDialogue;
            }
        }

        private void SetState(QuestRuntime q, QuestState state)
        {
            if (q.State == state) return;
            q.State = state;

            // Limpiezas al salir del estado Activa.
            if (state != QuestState.Activa && fireflyPool != null) fireflyPool.ClearHotspot(q.Data.questId);
            if (state != QuestState.Activa && !string.IsNullOrEmpty(q.CreatureTag) && CombatWaveDirector.Instance != null)
            {
                CombatWaveDirector.Instance.VanishGroup(q.CreatureTag);
            }
            if (q.Data.type == QuestType.Escolta && (state == QuestState.Completada || state == QuestState.Fallida || state == QuestState.Expirada))
            {
                CharacterAI npc = CharacterAI.Find(q.Data.escortNpcId);
                if (npc != null) npc.StopEscort();
            }

            if (state == QuestState.Completada)
            {
                // Persistente: no se repetirá en noches siguientes (salvo que se reintente esta noche).
                GameProgress.MarkQuestCompleted(q.Data.questId);
                completedThisNight.Add(q.Data.questId);
            }

            QuestStateChanged?.Invoke(q);
            EventBus.RaiseQuestStateChanged(q.Data.questId, state.ToString());
            if (state == QuestState.Completada) RefreshLocks();
        }

        /// <summary>Desbloquea las misiones cuyo requisito ya está completado.</summary>
        private void RefreshLocks()
        {
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntime q = quests[i];
                if (q.State != QuestState.Bloqueada) continue;

                Quest requirement = q.Data.prerequisite;
                QuestRuntime required = requirement != null ? Find(requirement) : null;
                bool doneBefore = requirement != null && required == null && GameProgress.IsQuestCompleted(requirement.questId);
                if (requirement == null || doneBefore || (required != null && required.State == QuestState.Completada))
                {
                    SetState(q, QuestState.Disponible);
                    UpdateProgressText(q);
                }
            }
        }

        private void UpdateProgressText(QuestRuntime q)
        {
            Quest data = q.Data;
            switch (q.State)
            {
                case QuestState.Disponible:
                    q.ProgressText = "Habla con " + GetNpcName(data.giverNpcId);
                    break;
                case QuestState.PorEntregar:
                    q.ProgressText = data.type == QuestType.Entrega
                        ? "Lleva " + data.deliverItemName + " a " + GetNpcName(data.TurnInNpcId)
                        : "Vuelve con " + GetNpcName(data.giverNpcId);
                    break;
                case QuestState.Completada:
                    q.ProgressText = "Completada";
                    break;
                case QuestState.Fallida:
                    q.ProgressText = "Fallida · " + q.FailReason;
                    break;
                case QuestState.Expirada:
                    q.ProgressText = "Expiró con la noche";
                    break;
                case QuestState.Bloqueada:
                    q.ProgressText = "";
                    break;
                case QuestState.Activa:
                    q.ProgressText = ActiveProgressText(q);
                    break;
            }
            QuestProgressChanged?.Invoke(q);
        }

        private string ActiveProgressText(QuestRuntime q)
        {
            Quest data = q.Data;
            switch (data.type)
            {
                case QuestType.RescateEsperanza:
                    return "Esperanza de " + GetZoneName(data.targetZoneId) + " ≥ " + q.EffThreshold.ToString("0") + " · "
                           + Mathf.FloorToInt(q.Timer) + "/" + q.EffHoldSeconds.ToString("0") + " s";

                case QuestType.DobleObjetivo:
                    ZoneHope holdZone = ZoneHope.Find(data.targetZoneId);
                    int remaining = CountTagged(q.CreatureTag);
                    int timeLeft = Mathf.Max(0, Mathf.CeilToInt(q.EffTimeLimit - q.Elapsed));
                    return "A · " + GetZoneName(data.targetZoneId) + " ≥ " + q.EffThreshold.ToString("0") + ": " + (holdZone != null ? holdZone.Hope.ToString("0") : "?")
                           + "   B · sombras: " + remaining + " · " + timeLeft + " s";

                case QuestType.Recoleccion:
                    string where = data.collectRadiusMultiplier > 0f ? " en " + GetZoneName(data.targetZoneId) : "";
                    return "Luciérnagas" + where + ": " + q.Count + "/" + data.requiredCount;

                case QuestType.Escolta:
                    CharacterAI npc = CharacterAI.Find(data.escortNpcId);
                    if (npc == null || player == null) return "Acompaña a " + GetNpcName(data.escortNpcId);
                    float distance = Vector3.Distance(player.position, npc.transform.position);
                    string warning = distance > data.escortMaxDistance ? "  ¡acércate!" : "";
                    string budget = q.EffEnergyBudget > 0f ? " · Energía " + Mathf.CeilToInt(q.EnergySpent) + "/" + q.EffEnergyBudget.ToString("0") : "";
                    return "Acompaña a " + GetNpcName(data.escortNpcId) + " · " + Mathf.RoundToInt(npc.EscortProgress01 * 100f) + "% del camino" + budget + warning;

                case QuestType.Entrega:
                    return "Recoge " + data.deliverItemName;

                default:
                    return "Habla con " + GetNpcName(data.giverNpcId);
            }
        }

        private static string GetNpcName(string npcId)
        {
            NpcInteractable npc = NpcInteractable.Find(npcId);
            return npc != null ? npc.DisplayName : npcId;
        }

        private static string GetZoneName(string zoneId)
        {
            ZoneHope zone = ZoneHope.Find(zoneId);
            return zone != null ? zone.DisplayName : zoneId;
        }

        /// <summary>Si la última recompensa incluía una página del Diario, lo añade al aviso (un solo aviso, no dos).</summary>
        private string PageToastSuffix()
        {
            if (!pageRewardPending) return "";
            pageRewardPending = false;
            return " · Nueva página del Diario (J)";
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.2f);
        }
    }
}
