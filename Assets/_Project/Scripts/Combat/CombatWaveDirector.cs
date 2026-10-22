using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Core;
using Ascua.Narrative;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.Combat
{
    /// <summary>
    /// Director de oleadas: 1-2 picos de intensidad por noche (no tensión constante).
    ///  - Cada oleada (CombatWave del NightQuestSet) aparece en su momento de la noche alrededor de una zona.
    ///  - Mientras queden sombras vivas, la Esperanza de esa zona cae (1.2/s por criatura).
    ///  - Al disolver la última: cámara lenta de 0.5 s, disolución grande y una valoración según la
    ///    energía perdida ("Zona defendida sin daño" / "Zona defendida" / "con dificultad").
    ///  - Defender una zona sube 1 el Vínculo con su vecino (Mina en La Plaza, Teo en El Mercado):
    ///    así el Vínculo 5 es alcanzable y sus habilidades de combate tienen sentido narrativo.
    ///  - Cada noche extra añade una Polilla (la dificultad crece con el ADN de la noche).
    /// Todas las criaturas salen de pools creados por el constructor de escenas.
    /// </summary>
    public class CombatWaveDirector : MonoBehaviour
    {
        public static CombatWaveDirector Instance { get; private set; }

        [Header("Pools de criaturas (desactivadas)")]
        [SerializeField] private PenumbraMoth[] moths;
        [SerializeField] private ShadowBrute[] brutes;
        [SerializeField] private ShadowBrute[] veiledBrutes;
        [SerializeField] private WhisperWraith[] whispers;
        [SerializeField] private EchoShade[] echoes;

        [Header("Aparición")]
        [SerializeField] private float spawnRadiusMin = 5f;
        [SerializeField] private float spawnRadiusMax = 8.5f;
        [SerializeField] private float minDistanceToPlayer = 4f;
        [SerializeField] private float finisherSlowScale = 0.3f;
        [SerializeField] private float finisherRealSeconds = 0.5f;

        [Header("Oleadas de la noche (las reemplaza el NightQuestSet)")]
        [SerializeField] private List<CombatWave> waves = new List<CombatWave>();

        private readonly List<ShadowCreature> alive = new List<ShadowCreature>();
        private int nextWave;
        private CombatWave active;
        private bool spawnedActive;
        private int extraNights;

        public bool HasActiveWave => active != null;
        public string ActiveZoneId => active != null ? active.zoneId : null;
        public int AliveCount => alive.Count;
        public int WavesThisNight => waves.Count;
        public int NextWaveIndex => nextWave;
        public IReadOnlyList<ShadowCreature> Alive => alive;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Oleadas de esta noche. extraNightCount suma una Polilla por noche extra.</summary>
        public void Configure(List<CombatWave> nightWaves, int extraNightCount)
        {
            waves = nightWaves != null ? new List<CombatWave>(nightWaves) : new List<CombatWave>();
            waves.Sort((a, b) => a.atProgress.CompareTo(b.atProgress));
            extraNights = Mathf.Max(0, extraNightCount);
            nextWave = 0;
        }

        private void Update()
        {
            NightManager night = NightManager.Instance;
            if (night == null || !night.IsRunning) return;

            if (active == null)
            {
                if (nextWave >= waves.Count) return;
                if (night.Progress01 < waves[nextWave].atProgress) return;
                if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return;
                StartWave(nextWave);
                return;
            }

            RefreshAlive();

            ZoneHope zone = ZoneHope.Find(active.zoneId);
            if (zone != null && zone.IsDead)
            {
                FailWave();
                return;
            }
            if (zone != null && alive.Count > 0) zone.AddHope(-active.hopeDrainPerCreature * alive.Count * Time.deltaTime);

            if (spawnedActive && alive.Count == 0) ClearWave();
        }

        /// <summary>Lanza ya una oleada (pruebas, depuración). Devuelve false si no existe.</summary>
        public bool StartWaveNow(int index)
        {
            if (active != null || index < 0 || index >= waves.Count) return false;
            StartWave(index);
            return true;
        }

        private void StartWave(int index)
        {
            active = waves[index];
            nextWave = index + 1;
            spawnedActive = false;

            ZoneHope zone = ZoneHope.Find(active.zoneId);
            Vector3 center = zone != null ? zone.Center : Vector3.zero;
            if (PlayerCombatState.Instance != null) PlayerCombatState.Instance.ResetDamageCounter();

            SpawnFrom(moths, active.moths + extraNights, center);
            SpawnFrom(brutes, active.brutes, center);
            SpawnFrom(veiledBrutes, active.veiledBrutes, center);
            SpawnFrom(whispers, active.whispers, center);
            SpawnFrom(echoes, active.echoes, center);
            spawnedActive = true;
            RefreshAlive();

            string zoneName = zone != null ? zone.DisplayName : active.zoneId;
            Toast("¡Sombras en " + zoneName + "! · Defiéndela");
            EventBus.RaiseCombatWave(active.zoneId, true);
        }

        private void SpawnFrom<T>(T[] pool, int count, Vector3 center) where T : ShadowCreature
        {
            if (pool == null) return;
            int spawned = 0;
            for (int i = 0; i < pool.Length && spawned < count; i++)
            {
                T creature = pool[i];
                if (creature == null || creature.gameObject.activeSelf) continue;
                creature.Spawn(FindSpawnPoint(center), active.zoneId);
                spawned++;
            }
        }

        private Vector3 FindSpawnPoint(Vector3 center)
        {
            Vector3 playerPosition = PlayerCombatState.Instance != null ? PlayerCombatState.Instance.transform.position : center;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);
                Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) continue;
                if ((hit.position - playerPosition).sqrMagnitude < minDistanceToPlayer * minDistanceToPlayer) continue;
                return hit.position;
            }
            return center + Vector3.forward * spawnRadiusMin;
        }

        private void RefreshAlive()
        {
            alive.Clear();
            if (active == null) return;
            var targets = LightTargetRegistry.All;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is ShadowCreature c && c.IsAlive && c.ZoneId == active.zoneId) alive.Add(c);
            }
        }

        /// <summary>True si 'creature' es la última sombra viva de la oleada (para el finisher).</summary>
        public bool IsLastAlive(ShadowCreature creature)
        {
            if (active == null) return false;
            RefreshAlive();
            // Se llama desde Die(): la criatura ya no cuenta como viva.
            return alive.Count == 0 && creature != null && creature.ZoneId == active.zoneId;
        }

        private void ClearWave()
        {
            string zoneId = active.zoneId;
            ZoneHope zone = ZoneHope.Find(zoneId);
            string zoneName = zone != null ? zone.DisplayName : zoneId;

            float damage = PlayerCombatState.Instance != null ? PlayerCombatState.Instance.DamageTakenCounter : 0f;
            string rating = damage <= 0.5f ? "sin daño" : damage < 25f ? "con firmeza" : "con dificultad";
            string message = zoneName + " defendida " + rating;

            if (CombatFx.Instance != null) CombatFx.Instance.SlowMotion(finisherSlowScale, finisherRealSeconds);

            GameProgress.AddWaveDefended();
            string npcId = NeighbourOf(zoneId);
            if (!string.IsNullOrEmpty(npcId) && QuestManager.Instance != null)
            {
                QuestManager.Instance.AddBond(npcId, 1);
                NpcInteractable npc = NpcInteractable.Find(npcId);
                if (npc != null) message += " · " + npc.DisplayName + " lo ha visto";
            }

            Toast(message);
            EventBus.RaiseWaveCleared(zoneId, rating);
            EventBus.RaiseCombatWave(zoneId, false);
            active = null;
            alive.Clear();
        }

        private void FailWave()
        {
            // La zona cayó: las sombras se retiran (ZoneHope ya anuncia la pérdida).
            foreach (ShadowCreature c in alive) c.Vanish();
            EventBus.RaiseCombatWave(active.zoneId, false);
            active = null;
            alive.Clear();
        }

        /// <summary>El vecino que "vive" en una zona (por su CharacterAI).</summary>
        private static string NeighbourOf(string zoneId)
        {
            foreach (CharacterAI ai in FindObjectsByType<CharacterAI>(FindObjectsSortMode.None))
            {
                if (ai.Zone == null || ai.Zone.ZoneId != zoneId) continue;
                NpcIdentity identity = ai.GetComponent<NpcIdentity>();
                if (identity != null) return identity.NpcId;
            }
            return null;
        }

        /// <summary>Punto al que debe guiar la flecha durante una oleada (la sombra más cercana).</summary>
        public bool TryGetObjective(Vector3 from, out Vector3 position)
        {
            position = Vector3.zero;
            if (active == null) return false;
            float best = float.MaxValue;
            foreach (ShadowCreature c in alive)
            {
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                position = c.transform.position;
            }
            if (best < float.MaxValue) return true;
            ZoneHope zone = ZoneHope.Find(active.zoneId);
            if (zone == null) return false;
            position = zone.Center;
            return true;
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.8f);
        }
    }
}
