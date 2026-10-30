using System;
using System.Collections.Generic;
using Ascua.AILevelGen;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.LevelGen;
using Ascua.Quests;
using Ascua.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ascua.Core
{
    /// <summary>
    /// Ritmo de la noche. Un solo selector ajusta duración, Esperanza de las zonas y Penumbra.
    /// Personalizado = se respetan los valores escritos a mano en cada componente.
    /// </summary>
    public enum NightPacing
    {
        Sutil,
        DemoRapida,
        Personalizado
    }

    /// <summary>Resultado de una noche, para la pantalla de resumen (y futuras mejoras entre noches).</summary>
    public struct NightResult
    {
        public int NightNumber;
        public int ZonesSaved;
        public int ZonesTotal;
        public bool EndedEarly;
        public int QuestsCompleted;
        public int QuestsTotal;
    }

    /// <summary>
    /// Ciclo de una noche con temporizador. Al acabar (o si todas las zonas mueren antes)
    /// pausa el juego, libera el cursor y emite NightEnded con el resultado.
    /// La UI de resumen escucha ese evento; este script no conoce la UI.
    /// </summary>
    public class NightManager : MonoBehaviour
    {
        public static NightManager Instance { get; private set; }

        [Header("Ritmo")]
        [Tooltip("Sutil = juego final (60 s). Demo rápida = para enseñarlo en clase (30 s). Personalizado = valores a mano.")]
        [SerializeField] private NightPacing pacing = NightPacing.Sutil;

        [Header("Set de misiones (= el nivel de esta noche)")]
        [Tooltip("Si hay un set asignado, SUS valores de dificultad mandan y el Ritmo se ignora. " +
                 "Diseñar una noche nueva = crear otro NightQuestSet y ponerlo aquí (o que lo genere la IA en la Fase 4).")]
        [SerializeField] private NightQuestSet questSet;
        [Tooltip("Sets de las noches siguientes (2, 3, 4...). Se elige el de la noche actual; más allá del último, " +
                 "se repite el último con la dificultad escalada (decayIncreasePerNight, una Polilla más por oleada).")]
        [SerializeField] private NightQuestSet[] laterNights;

        [Header("Noches generadas (IA de niveles)")]
        [Tooltip("Pasada la última noche diseñada a mano, cada noche nueva la genera AIDirector a partir del ADN de la Noche 1 " +
                 "(banco de IA validado o reglas locales, siempre sin internet). Desmarcar = se repite la última noche diseñada.")]
        [SerializeField] private bool generateNightsAfterAuthored = true;

        [Header("Noche")]
        [SerializeField] private int nightNumber = 1;
        [Tooltip("Duración de la noche en segundos (solo se usa tal cual en 'Personalizado').")]
        [SerializeField] private float nightDuration = 60f;
        [Tooltip("Termina la noche antes si todas las zonas mueren.")]
        [SerializeField] private bool endWhenAllZonesDead = true;
        [SerializeField] private string titleSceneName = "Title";

        [Header("Referencias (se buscan solas si están vacías)")]
        [SerializeField] private PlayerInputReader playerInput;
        [SerializeField] private GameSession gameSession;

        public event Action<NightManager> NightStarted;
        public event Action<NightResult> NightEnded;

        /// <summary>ADN del nivel que se está jugando (extraído del set o generado). Null si no hay set.</summary>
        public LevelDNA CurrentDna { get; private set; }
        /// <summary>Copia del ADN de la Noche 1 (BASE_DNA), para compararlo en el panel "Ver ADN".</summary>
        public LevelDNA BaseDna { get; private set; }
        /// <summary>El nivel generado de esta noche (null si la noche está diseñada a mano).</summary>
        public AIDirector.GeneratedLevel GeneratedLevel { get; private set; }
        public bool IsGeneratedNight => GeneratedLevel != null;

        public NightPacing Pacing => pacing;
        public NightQuestSet QuestSet => questSet;
        public int NightNumber => nightNumber;
        public float Duration => nightDuration;
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, nightDuration - Elapsed);
        public float Progress01 => Mathf.Clamp01(Elapsed / nightDuration);
        public bool IsRunning { get; private set; }

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputReader>();
            if (gameSession == null) gameSession = FindFirstObjectByType<GameSession>();

            // Las noches diseñadas a mano se recogen ANTES de que questSet se reasigne.
            List<NightQuestSet> authored = CollectAuthoredSets();
            questSet = SelectSetForNight(GameProgress.CurrentNight);
            ResolveLevelDna(authored);
            if (questSet != null) ApplyQuestSet(questSet);
            else
            {
                ApplyPacing();
                nightNumber = Mathf.Max(nightNumber, GameProgress.CurrentNight);
            }
        }

        private List<NightQuestSet> CollectAuthoredSets()
        {
            List<NightQuestSet> list = new List<NightQuestSet>();
            if (questSet != null) list.Add(questSet);
            if (laterNights != null)
            {
                foreach (NightQuestSet set in laterNights)
                {
                    if (set != null && !list.Contains(set)) list.Add(set);
                }
            }
            return list;
        }

        /// <summary>
        /// Fija el ADN de esta noche. Si ya pasó la última noche diseñada a mano, AIDirector genera el nivel
        /// (a partir de la Noche 1) y se juega ese set; si no, el ADN se extrae del set diseñado.
        /// </summary>
        private void ResolveLevelDna(List<NightQuestSet> authored)
        {
            if (authored.Count == 0) return;

            NightQuestSet first = authored[0];
            NightQuestSet last = authored[0];
            List<Quest> pool = new List<Quest>();
            foreach (NightQuestSet set in authored)
            {
                if (set.nightNumber < first.nightNumber) first = set;
                if (set.nightNumber > last.nightNumber) last = set;
                if (set.quests == null) continue;
                foreach (Quest quest in set.quests)
                {
                    if (quest != null && !pool.Contains(quest)) pool.Add(quest);
                }
            }
            BaseDna = AIDirector.GetBaseDna(first);

            int night = GameProgress.CurrentNight;
            if (generateNightsAfterAuthored && night > last.nightNumber)
            {
                AIDirector.GeneratedLevel level = AIDirector.GetOrGenerate(night, first, last, pool);
                if (level != null && level.Set != null)
                {
                    GeneratedLevel = level;
                    CurrentDna = level.Dna;
                    questSet = level.Set;
                    return;
                }
            }
            CurrentDna = LevelDnaConverter.FromSet(questSet, questSet == first ? LevelDnaOrigin.Base : LevelDnaOrigin.Manual);
        }

        /// <summary>
        /// El "nivel" de la noche: el set cuyo número coincide; si no hay, el de número más alto
        /// que no la supere (la dificultad extra la añade ApplyQuestSet). Datos, no código por noche.
        /// </summary>
        private NightQuestSet SelectSetForNight(int night)
        {
            NightQuestSet best = questSet;
            if (laterNights == null) return best;
            foreach (NightQuestSet set in laterNights)
            {
                if (set == null || set.nightNumber > night) continue;
                if (best == null || set.nightNumber > best.nightNumber) best = set;
            }
            return best;
        }

        /// <summary>
        /// Vuelca la dificultad del set de misiones en la noche, las zonas, la Penumbra,
        /// las luciérnagas y el Heraldo. Se ejecuta en Awake, antes de los Start de esos sistemas.
        /// </summary>
        private void ApplyQuestSet(NightQuestSet set)
        {
            // La noche real la lleva GameProgress (el set puede reutilizarse en noches siguientes).
            nightNumber = Mathf.Max(set.nightNumber, GameProgress.CurrentNight);
            nightDuration = Mathf.Max(10f, set.nightDuration);

            // Cada noche extra con el mismo set, la oscuridad aprieta un poco más.
            int extraNights = Mathf.Max(0, nightNumber - set.nightNumber);
            float decay = set.hopeDecayPerSecond * (1f + set.decayIncreasePerNight * extraNights);

            foreach (ZoneHope zone in FindObjectsByType<ZoneHope>(FindObjectsSortMode.None))
            {
                zone.ApplyPacing(set.startHope, decay, set.hopeRisePerSecond);
            }

            PenumbraSurge surge = FindFirstObjectByType<PenumbraSurge>();
            if (surge != null) surge.Configure(set.surgeEnabled, set.surgeAt, set.surgeSeconds, set.surgeMeters, set.surgeDecayMultiplier);

            Penumbra penumbra = FindFirstObjectByType<Penumbra>();
            if (penumbra != null) penumbra.ApplyCustom(set.penumbraStartRadius, set.penumbraEndRadius, set.fastPenumbraCurve);

            FireflyPool pool = FindFirstObjectByType<FireflyPool>();
            if (pool != null) pool.Configure(set.fireflySpawnInterval, set.fireflyMaxActive);

            Heraldo_AI heraldo = FindFirstObjectByType<Heraldo_AI>();
            if (heraldo != null) heraldo.Configure(set.heraldoEnabled, set.heraldoAppearAt, set.heraldoChaseSpeed);

            // Oleadas de sombras de esta noche (cada noche extra suma una Polilla por oleada).
            Ascua.Combat.CombatWaveDirector waves = FindFirstObjectByType<Ascua.Combat.CombatWaveDirector>();
            if (waves != null) waves.Configure(set.waves, extraNights);
        }

        /// <summary>
        /// Vuelca el ritmo elegido en la noche, las zonas y la Penumbra.
        /// Se ejecuta en Awake, antes de que zonas y Penumbra hagan su Start.
        /// </summary>
        private void ApplyPacing()
        {
            if (pacing == NightPacing.Personalizado) return;
            bool demo = pacing == NightPacing.DemoRapida;

            nightDuration = demo ? 30f : 60f;

            foreach (ZoneHope zone in FindObjectsByType<ZoneHope>(FindObjectsSortMode.None))
            {
                if (demo) zone.ApplyPacing(45f, 2.2f, 5f);
                else zone.ApplyPacing(60f, 1.5f, 4f);
            }

            Penumbra penumbra = FindFirstObjectByType<Penumbra>();
            if (penumbra != null) penumbra.ApplyPacing(pacing);
        }

        private void Start()
        {
            Elapsed = 0f;
            IsRunning = true;
            if (QuestManager.Instance != null) QuestManager.Instance.BeginNight(questSet);
            NightStarted?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Nunca dejar el juego en pausa al salir de la escena.
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (!IsRunning) return;

            Elapsed += Time.deltaTime;
            bool timeUp = Elapsed >= nightDuration;
            bool allDead = endWhenAllZonesDead && AllZonesDead();
            if (timeUp || allDead) EndNight(allDead && !timeUp);
        }

        /// <summary>Termina la noche ahora mismo.</summary>
        public void EndNight(bool endedEarly)
        {
            if (!IsRunning) return;
            IsRunning = false;

            NightResult result = new NightResult
            {
                NightNumber = nightNumber,
                ZonesTotal = ZoneHope.All.Count,
                EndedEarly = endedEarly
            };
            foreach (ZoneHope zone in ZoneHope.All)
            {
                if (!zone.IsDead) result.ZonesSaved++;
            }

            QuestManager quests = QuestManager.Instance;
            if (quests != null)
            {
                quests.EndNight();
                foreach (QuestRuntime q in quests.Quests)
                {
                    result.QuestsTotal++;
                    if (q.State == QuestState.Completada) result.QuestsCompleted++;
                }
            }

            LastSummary = NightSummary.Capture(nightNumber, endedEarly);
            // Solo números anónimos: de aquí sale la dificultad de las noches generadas.
            PlayerProfileTracker.Record(LastSummary);

            // Pausa: se congela todo lo que usa Time.deltaTime; la UI usa tiempo sin escalar.
            Time.timeScale = 0f;
            if (playerInput != null) playerInput.SetGameplayEnabled(false);
            if (gameSession != null) gameSession.enabled = false;
            GameSession.SetCursorLocked(false);

            NightEnded?.Invoke(result);
        }

        /// <summary>Vuelve a cargar la escena actual para repetir la noche.</summary>
        public void RestartNight()
        {
            Time.timeScale = 1f;
            // Lo ganado en un intento fallido no se conserva al repetir la noche.
            if (QuestManager.Instance != null) QuestManager.Instance.RevertNightProgress();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToTitle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
        }

        /// <summary>
        /// Pasa a la noche siguiente: avanza GameProgress y recarga la escena. Las mejoras elegidas
        /// (PlayerUpgrades), los Vínculos y las misiones ya cumplidas se conservan.
        /// </summary>
        public void StartNextNight()
        {
            Time.timeScale = 1f;
            GameProgress.AdvanceNight();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Resumen de la noche recién terminada (para la UI y, en la Fase 4, el AIDirector).</summary>
        public NightSummary LastSummary { get; private set; }

        private static bool AllZonesDead()
        {
            // Bucle con índice: foreach sobre IReadOnlyList asignaría memoria en cada frame.
            var zones = ZoneHope.All;
            if (zones.Count == 0) return false;
            for (int i = 0; i < zones.Count; i++)
            {
                if (!zones[i].IsDead) return false;
            }
            return true;
        }
    }
}
