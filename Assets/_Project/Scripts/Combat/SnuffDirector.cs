using System.Collections.Generic;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Narrative;
using Ascua.UI;
using Ascua.World;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Programa los Apagavelas de la noche. Lo crea NightManager por código (sin tocar la escena).
    ///  - Al empezar: reúne las luces (SnuffTarget.Collect) y prepara un pool de 6 Apagavelas inactivos,
    ///    clonados de una Polilla de la escena (también de las inactivas).
    ///  - Cuántos y cuándo: SnuffPlanner (automático por noche o lo que diga el ADN), entre el 15 % y el 85 %.
    ///  - Cada uno aparece a ~16 m de Ilse, del lado de la zona con más luces encendidas.
    ///  - Nunca durante una Cacería, un momento narrativo, un diálogo o la pausa: reintenta cada segundo
    ///    hasta el 90 %. Usan su propio tag ("apagavelas"): no cuentan como oleada de CombatWaveDirector.
    /// </summary>
    public class SnuffDirector : MonoBehaviour
    {
        public static SnuffDirector Instance { get; private set; }

        [Tooltip("-1 = automático según la noche; 0-8 lo fija el nivel.")]
        [SerializeField] private int snufferCount = SnuffPlanner.Auto;
        [SerializeField] private int poolSize = 6;
        [SerializeField] private float spawnDistance = 16f;
        [SerializeField] private float spawnHeight = 1.8f;
        [SerializeField, Range(0f, 1f)] private float lastChance = 0.9f;
        [SerializeField] private float retrySeconds = 1f;

        private readonly List<Apagavelas> pool = new List<Apagavelas>();
        private readonly List<float> moments = new List<float>();
        private readonly HashSet<string> activeMoments = new HashSet<string>();
        private readonly Dictionary<string, int> litPerZone = new Dictionary<string, int>();
        private bool scheduleDirty = true;
        private int nextMoment;
        private float retryTimer;
        private Transform player;

        public int ScheduledCount => moments.Count;
        public int RemainingCount => Mathf.Max(0, moments.Count - nextMoment);
        public float NextMoment => nextMoment < moments.Count ? moments[nextMoment] : -1f;
        public int PoolCount => pool.Count;

        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < pool.Count; i++) if (pool[i] != null && pool[i].IsAlive) n++;
                return n;
            }
        }

        /// <summary>Devuelve el director de la escena, creándolo si no existe.</summary>
        public static SnuffDirector Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("SnuffDirector").AddComponent<SnuffDirector>();
        }

        /// <summary>Apagavelas de esta noche (-1 = automático). Lo aplica NightManager desde el NightQuestSet.</summary>
        public void Configure(int count)
        {
            snufferCount = count;
            scheduleDirty = true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.NarrativeMoment += OnNarrativeMoment;
        }

        private void OnDisable()
        {
            EventBus.NarrativeMoment -= OnNarrativeMoment;
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
            SnuffTarget.Collect();
            BuildPool();
        }

        private void OnNarrativeMoment(string id, bool started)
        {
            if (string.IsNullOrEmpty(id)) id = "?";
            if (started) activeMoments.Add(id);
            else activeMoments.Remove(id);
        }

        private void Update()
        {
            NightManager night = NightManager.Instance;
            if (night == null || !night.IsRunning) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // pausa: no se programa nada

            if (scheduleDirty) BuildSchedule(night);
            if (nextMoment >= moments.Count) return;

            float progress = night.Progress01;
            if (progress < moments[nextMoment]) return;
            if (progress > lastChance)
            {
                nextMoment++;   // ya no da tiempo
                return;
            }

            retryTimer -= dt;
            if (retryTimer > 0f) return;
            retryTimer = retrySeconds;
            if (IsBusy()) return;
            if (SpawnOne()) nextMoment++;
        }

        private void BuildSchedule(NightManager night)
        {
            scheduleDirty = false;
            int count = SnuffPlanner.CountForNight(night.NightNumber, snufferCount);
            // Semilla fija por noche (distinta de la de la Cacería): al reintentar, salen en el mismo momento.
            moments.Clear();
            moments.AddRange(SnuffPlanner.Schedule(count, night.NightNumber * 4099 + 31));
            nextMoment = 0;
        }

        /// <summary>Cacería, momento narrativo, diálogo o pausa.</summary>
        public bool IsBusy()
        {
            if (PauseMenu.IsPaused) return true;
            if (FinalBossDirector.HoldsNight) return true;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return true;
            HeraldoHuntDirector hunts = HeraldoHuntDirector.Instance;
            if (hunts != null && hunts.IsBusy) return true;
            return activeMoments.Count > 0;
        }

        /// <summary>Saca un Apagavelas del pool (depuración: F9). False si no hay ninguno libre.</summary>
        public bool SpawnOne()
        {
            if (player == null) return false;
            for (int i = 0; i < pool.Count; i++)
            {
                Apagavelas snuffer = pool[i];
                if (snuffer == null || snuffer.gameObject.activeSelf) continue;
                snuffer.Spawn(SpawnPoint(), Apagavelas.SpawnTag);
                return true;
            }
            return false;
        }

        /// <summary>A ~16 m de Ilse, hacia la zona con más luces encendidas (empate: la de nombre menor).</summary>
        private Vector3 SpawnPoint()
        {
            litPerZone.Clear();
            var targets = SnuffTarget.All;
            for (int i = 0; i < targets.Count; i++)
            {
                SnuffTarget t = targets[i];
                if (!t.IsLamp || !t.Exists || !t.IsLit) continue;
                string id = t.Lamp.ZoneId ?? "";
                litPerZone.TryGetValue(id, out int n);
                litPerZone[id] = n + 1;
            }

            ZoneHope best = null;
            int bestCount = -1;
            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                ZoneHope zone = zones[i];
                if (zone == null || zone.IsDead) continue;
                litPerZone.TryGetValue(zone.ZoneId ?? "", out int lit);
                bool better = lit > bestCount || (lit == bestCount && best != null && string.CompareOrdinal(zone.ZoneId, best.ZoneId) < 0);
                if (!better) continue;
                best = zone;
                bestCount = lit;
            }

            Vector3 dir = best != null ? best.Center - player.position : player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            return player.position + dir.normalized * spawnDistance + Vector3.up * spawnHeight;
        }

        /// <summary>Pool de Apagavelas: clones de una Polilla sin su PenumbraMoth, con Apagavelas en su lugar.</summary>
        private void BuildPool()
        {
            pool.Clear();
            PenumbraMoth source = FindMothToClone();
            if (source == null)
            {
                Debug.LogWarning("[Apagavelas] No hay ninguna Polilla en la escena para clonar: esta noche no habrá Apagavelas.");
                return;
            }

            GameObject template = source.gameObject;
            bool wasActive = template.activeSelf;
            // Se clona desde inactiva: así la Polilla clonada nunca llega a despertar.
            if (wasActive) template.SetActive(false);
            for (int i = 0; i < poolSize; i++)
            {
                GameObject clone = Instantiate(template, transform);
                clone.name = "Apagavelas_" + (i + 1);
                DestroyImmediate(clone.GetComponent<PenumbraMoth>());
                Apagavelas snuffer = clone.AddComponent<Apagavelas>();
                snuffer.SetupFromClone();
                // Un instante activo para que pase su Awake con los valores ya puestos; luego, al pool.
                clone.SetActive(true);
                clone.SetActive(false);
                pool.Add(snuffer);
            }
            if (wasActive) template.SetActive(true);
        }

        private static PenumbraMoth FindMothToClone()
        {
            PenumbraMoth[] moths = FindObjectsByType<PenumbraMoth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            PenumbraMoth fallback = null;
            foreach (PenumbraMoth moth in moths)
            {
                if (moth == null) continue;
                if (!moth.gameObject.activeSelf) return moth;   // mejor una del pool, quieta
                if (fallback == null) fallback = moth;
            }
            return fallback;
        }
    }
}
