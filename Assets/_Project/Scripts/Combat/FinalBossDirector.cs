using System.Collections;
using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Ascua.Combat
{
    /// <summary>
    /// Director del jefe final "La Marea". Lo crea NightManager por código (sin tocar la escena) solo en la
    /// última noche de la campaña (Noche 5) y si aún no se derrotó en esta partida (FinalBossRules).
    /// Flujo:
    ///  1. Espera: cuando se terminan las misiones de la noche (o se acaba el tiempo) detiene el reloj de la noche
    ///     y espera a que no haya diálogo, oleada, Cacería, momento narrativo ni pausa.
    ///  2. Entrada: fundido a negro, Ilse aparece frente al centro de La Plaza y La Marea sale de la niebla.
    ///     Mientras dura el combate la Esperanza de las zonas no baja (el jefe no cambia qué final sale).
    ///  3. Si Ilse cae (energía a 0 por un golpe): fundido y reintento desde el inicio del combate,
    ///     sin perder nada de la noche. A la 3ª caída La Marea se retira y la noche sigue al final sin premio.
    ///  4. Si La Marea se deshace: pausa dramática de 2 s, subtítulo, +Esperanza a las zonas vivas, se guarda
    ///     que ya se venció y la noche termina con el flujo normal (resumen → final que decida EndingResolver).
    /// Depuración: F10 con F1 abierto lo lanza en cualquier noche (sin guardar ni terminar la noche).
    /// </summary>
    public class FinalBossDirector : MonoBehaviour
    {
        public static FinalBossDirector Instance { get; private set; }

        private enum State { Idle, Waiting, Intro, Fighting, Transition, Done }

        [SerializeField] private float fadeSeconds = 0.8f;
        [SerializeField] private float arenaDistance = 9f;
        [SerializeField] private float dramaticPauseSeconds = 2f;
        [SerializeField] private string arenaZoneId = "Plaza";

        private State state = State.Idle;
        private bool debugRun;
        private bool appearsTonight;
        private int defeats;
        private bool waveRaised;
        private MareaBoss boss;
        private Vector3 bossStart;
        private Vector3 playerStart;
        private CanvasGroup fade;
        private PlayerCombatState hookedPlayer;
        private readonly HashSet<string> activeMoments = new HashSet<string>();

        /// <summary>True mientras el jefe se prepara o se combate: el reloj de la noche se detiene.</summary>
        public static bool HoldsNight => Instance != null && Instance.state != State.Idle && Instance.state != State.Done;

        /// <summary>True durante el combate (no en la espera ni en los fundidos).</summary>
        public bool IsFighting => state == State.Fighting;
        /// <summary>True desde la entrada hasta el final del combate (para el HUD).</summary>
        public bool IsEncounterActive => state == State.Intro || state == State.Fighting || state == State.Transition;
        public MareaBoss Boss => boss;
        public int Defeats => defeats;
        public string StateName => state.ToString();

        /// <summary>
        /// Crea el director si La Marea debe salir esta noche (lo llama NightManager en Awake).
        /// Devuelve null en las demás noches, en "Noches sin fin" o si ya se derrotó.
        /// </summary>
        public static FinalBossDirector CreateFor(int night, int finalNight)
        {
            if (!FinalBossRules.AppearsTonight(night, finalNight, GameProgress.BossDefeated)) return null;
            FinalBossDirector director = Ensure();
            director.appearsTonight = true;
            return director;
        }

        /// <summary>Devuelve el director de la escena, creándolo si no existe (depuración).</summary>
        public static FinalBossDirector Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("FinalBossDirector").AddComponent<FinalBossDirector>();
        }

        /// <summary>
        /// NightManager lo llama cuando se acaba el tiempo: si La Marea aún tiene que salir, el final espera.
        /// Devuelve true si el combate empieza (o ya estaba en marcha).
        /// </summary>
        public static bool TryBeginBeforeEnding()
        {
            FinalBossDirector director = Instance;
            if (director == null) return false;
            if (HoldsNight) return true;
            if (!director.appearsTonight || director.state != State.Idle) return false;
            director.debugRun = false;
            director.state = State.Waiting;
            return true;
        }

        /// <summary>Depuración (F10): lanza ya el combate en cualquier noche. False si ahora no se puede.</summary>
        public bool StartNow()
        {
            NightManager night = NightManager.Instance;
            if (night == null || !night.IsRunning) return false;
            if (state != State.Idle && state != State.Done) return false;
            debugRun = !appearsTonight || state == State.Done;
            defeats = 0;
            state = State.Waiting;
            return true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildFade();
            // Barra de vida de La Marea (arriba al centro, solo durante el combate).
            BossHealthBarUI.Ensure();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnhookPlayer();
            if (state == State.Intro || state == State.Fighting || state == State.Transition) ZoneHope.GlobalDecayMultiplier = 1f;
            if (waveRaised) EventBus.RaiseCombatWave(MareaBoss.SpawnTag, false);
        }

        private void OnEnable()
        {
            EventBus.NarrativeMoment += OnNarrativeMoment;
        }

        private void OnDisable()
        {
            EventBus.NarrativeMoment -= OnNarrativeMoment;
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
            if (night == null) return;

            if (!night.IsRunning)
            {
                // La noche terminó por otra vía (todas las zonas apagadas, salto de depuración...).
                if (state == State.Waiting || state == State.Intro || state == State.Fighting || state == State.Transition) Abort();
                return;
            }
            if (Time.deltaTime <= 0f) return;   // pausa: nada avanza

            switch (state)
            {
                case State.Idle:
                    if (appearsTonight && QuestsFinished(night)) state = State.Waiting;
                    break;

                case State.Waiting:
                    if (!IsBusy()) StartCoroutine(Intro());
                    break;

                case State.Intro:
                case State.Fighting:
                case State.Transition:
                    // El combate no cambia qué final sale: las zonas no pierden Esperanza mientras dura.
                    ZoneHope.GlobalDecayMultiplier = 0f;
                    break;
            }
        }

        /// <summary>Misiones de la noche terminadas (sin contar Tratos del Heraldo que no se aceptaron).</summary>
        private static bool QuestsFinished(NightManager night)
        {
            // El momento memorable de la noche (los farolillos, al 5 %) va primero.
            NightEventDirector nightEvent = NightEventDirector.Instance;
            if (nightEvent != null && !nightEvent.HasPlayed && !NightEventRules.IsTooLate(night.Progress01)) return false;

            QuestManager quests = QuestManager.Instance;
            if (quests == null) return false;
            int total = 0, pending = 0;
            foreach (QuestRuntime q in quests.Quests)
            {
                total++;
                bool optionalDeal = q.Data.type == QuestType.TratoHeraldo && q.State == QuestState.Disponible;
                if (q.IsOpen && !optionalDeal) pending++;
            }
            return FinalBossRules.QuestsFinished(pending, total);
        }

        /// <summary>Diálogo, oleada, Cacería, momento narrativo o pausa en curso.</summary>
        private bool IsBusy()
        {
            if (PauseMenu.IsPaused) return true;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return true;
            CombatWaveDirector waves = CombatWaveDirector.Instance;
            if (waves != null && waves.HasActiveWave) return true;
            HeraldoHuntDirector hunts = HeraldoHuntDirector.Instance;
            if (hunts != null && hunts.IsBusy) return true;
            NightEventDirector nightEvent = NightEventDirector.Instance;
            if (nightEvent != null && nightEvent.IsPlaying) return true;
            return activeMoments.Count > 0;
        }

        // ================================================================== Entrada

        private IEnumerator Intro()
        {
            state = State.Intro;
            SetGameplay(false);
            yield return Fade(1f);

            Transform player = PlayerTransform();
            if (boss == null) boss = BuildBoss();
            if (boss == null || player == null)
            {
                Debug.LogWarning("[La Marea] No hay Sombra Mayor que clonar o no hay jugador: la noche sigue sin jefe.");
                yield return Fade(0f);
                SetGameplay(true);
                Finish(false);
                yield break;
            }

            // Fuera distracciones: los Apagavelas se van y el Heraldo se retira mientras dure el combate.
            if (CombatWaveDirector.Instance != null) CombatWaveDirector.Instance.VanishGroup(Apagavelas.SpawnTag);
            if (Heraldo_AI.Instance != null) Heraldo_AI.Instance.RetreatFor(600f);

            ChooseArena(player);
            ResetEncounter(player);
            HookPlayer();
            // Música y ambiente de combate (mismo evento que una oleada; su "zona" es el tag de La Marea).
            EventBus.RaiseCombatWave(MareaBoss.SpawnTag, true);
            waveRaised = true;
            state = State.Fighting;

            yield return Fade(0f);
            SetGameplay(true);
            Toast("La Marea · solo la luz del Farol la deshace", 3.2f);
        }

        /// <summary>Centro de La Plaza para La Marea; Ilse, a ~9 m, del lado por el que venía.</summary>
        private void ChooseArena(Transform player)
        {
            ZoneHope zone = ZoneHope.Find(arenaZoneId);
            if (zone == null && ZoneHope.All.Count > 0) zone = ZoneHope.All[0];
            Vector3 center = zone != null ? zone.Center : player.position + player.forward * arenaDistance;
            bossStart = Sample(center, 8f);

            Vector3 dir = player.position - bossStart;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            playerStart = Sample(bossStart + dir.normalized * arenaDistance, 4f);
        }

        /// <summary>Deja el combate como al principio: Ilse con el Farol lleno en su sitio y La Marea entera.</summary>
        private void ResetEncounter(Transform player)
        {
            boss.VanishSummons();
            boss.RestoreLamps();
            if (boss.gameObject.activeSelf) boss.gameObject.SetActive(false);

            Vector3 look = bossStart - playerStart;
            look.y = 0f;
            Teleport(player, playerStart, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look.normalized) : player.rotation);

            PlayerEnergy energy = player.GetComponent<PlayerEnergy>();
            if (energy != null) energy.Restore(energy.Max);

            boss.Spawn(bossStart, MareaBoss.SpawnTag);
            Vector3 face = playerStart - bossStart;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) boss.transform.rotation = Quaternion.LookRotation(face.normalized);
        }

        // ================================================================== Durante el combate

        private void HookPlayer()
        {
            PlayerCombatState current = PlayerCombatState.Instance;
            if (current == hookedPlayer) return;
            UnhookPlayer();
            hookedPlayer = current;
            if (hookedPlayer != null) hookedPlayer.Hurt += OnPlayerHurt;
        }

        private void UnhookPlayer()
        {
            if (hookedPlayer != null) hookedPlayer.Hurt -= OnPlayerHurt;
            hookedPlayer = null;
        }

        private void OnPlayerHurt(float amount)
        {
            if (state != State.Fighting || hookedPlayer == null) return;
            PlayerEnergy energy = hookedPlayer.GetComponent<PlayerEnergy>();
            // "Cae" si un golpe la deja sin luz (agotada por usar el Farol no cuenta hasta que la golpean).
            if (energy == null || energy.Current > 0.01f) return;
            defeats++;
            StartCoroutine(FinalBossRules.ShouldRetreat(defeats) && !debugRun ? Retreat() : Retry());
        }

        private void OnBossDefeated(ShadowCreature creature)
        {
            if (state != State.Fighting) return;
            StartCoroutine(Victory());
        }

        private IEnumerator Retry()
        {
            state = State.Transition;
            SetGameplay(false);
            Subtitle("Ilse cae… la luz vuelve a encenderse");
            yield return Fade(1f);
            Transform player = PlayerTransform();
            if (player != null) ResetEncounter(player);
            yield return Fade(0f);
            SetGameplay(true);
            state = State.Fighting;
            Toast("Otra vez · ilumínala y golpea", 2.4f);
        }

        private IEnumerator Retreat()
        {
            state = State.Transition;
            Subtitle("La Marea se retira a la niebla… sin vencerla");
            if (boss != null)
            {
                boss.VanishSummons();
                boss.Vanish();
            }
            yield return Wait(dramaticPauseSeconds);
            if (boss != null) boss.RestoreLamps();
            Finish(true);
        }

        private IEnumerator Victory()
        {
            state = State.Transition;
            // Pausa dramática: un instante a cámara lenta y 2 s de silencio antes de seguir.
            if (CombatFx.Instance != null) CombatFx.Instance.SlowMotion(0.35f, 0.6f);
            Subtitle("La Marea se deshace en luz. Vesper respira.");
            if (boss != null) boss.VanishSummons();
            yield return Wait(dramaticPauseSeconds);

            if (boss != null) boss.RestoreLamps();
            // Premio pequeño: un poco más de Esperanza en las zonas vivas (no revive zonas, no cambia el final).
            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && !zones[i].IsDead) zones[i].AddHope(FinalBossRules.VictoryHopeBonus);
            }
            if (!debugRun) SaveSystem.MarkBossDefeated();
            Finish(true);
        }

        /// <summary>Cierra el encuentro y, si era el de verdad, sigue el flujo normal hacia el final.</summary>
        private void Finish(bool endNight)
        {
            bool wasDebug = debugRun;
            UnhookPlayer();
            ZoneHope.GlobalDecayMultiplier = 1f;
            if (waveRaised) EventBus.RaiseCombatWave(MareaBoss.SpawnTag, false);
            waveRaised = false;
            state = State.Done;
            debugRun = false;
            if (fade != null) fade.alpha = 0f;

            NightManager night = NightManager.Instance;
            if (endNight && !wasDebug && night != null && night.IsRunning) night.EndNight(false);
        }

        /// <summary>La noche terminó a mitad del encuentro: se recoge todo sin premio.</summary>
        private void Abort()
        {
            StopAllCoroutines();
            if (boss != null)
            {
                boss.VanishSummons();
                boss.RestoreLamps();
                if (boss.IsAlive) boss.Vanish();
            }
            // Sin tocar el control: el resumen de la noche ya lo gestiona NightManager.
            Finish(false);
        }

        // ================================================================== La Marea (clon de una Sombra Mayor)

        /// <summary>
        /// Clona una Sombra Mayor de la escena (también de las inactivas del pool), le quita ShadowBrute
        /// (y el velo, si lo tiene) y le pone MareaBoss. El clon nace dentro de un padre inactivo: la Sombra
        /// Mayor clonada nunca llega a despertar ni a registrarse como objetivo.
        /// </summary>
        private MareaBoss BuildBoss()
        {
            ShadowBrute source = FindBruteToClone();
            if (source == null) return null;

            GameObject holder = new GameObject("Marea_Molde");
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            GameObject clone = Instantiate(source.gameObject, holder.transform);
            clone.name = "La_Marea";
            clone.transform.localScale = source.transform.lossyScale;

            NavMeshAgent cloneAgent = clone.GetComponent<NavMeshAgent>();
            if (cloneAgent != null) cloneAgent.enabled = false;
            // El velo depende de ShadowCreature (RequireComponent): se quita antes que la Sombra Mayor.
            LitVulnerable veil = clone.GetComponent<LitVulnerable>();
            if (veil != null) DestroyImmediate(veil);
            DestroyImmediate(clone.GetComponent<ShadowBrute>());

            MareaBoss marea = clone.AddComponent<MareaBoss>();
            marea.SetupFromClone();
            marea.Defeated += OnBossDefeated;

            clone.SetActive(false);
            clone.transform.SetParent(transform, true);
            Destroy(holder);
            // Un instante activa para que pase su Awake con los valores ya puestos; luego, a esperar.
            clone.SetActive(true);
            clone.SetActive(false);
            return marea;
        }

        private static ShadowBrute FindBruteToClone()
        {
            ShadowBrute best = null;
            int bestScore = -1;
            foreach (ShadowBrute brute in FindObjectsByType<ShadowBrute>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (brute == null) continue;
                // Mejor una del pool (quieta) y sin velo.
                int score = (brute.gameObject.activeSelf ? 0 : 2) + (brute.GetComponent<LitVulnerable>() == null ? 1 : 0);
                if (score <= bestScore) continue;
                bestScore = score;
                best = brute;
            }
            return best;
        }

        // ================================================================== utilidades

        private static Vector3 Sample(Vector3 point, float radius)
        {
            return NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas) ? hit.position : point;
        }

        private static Transform PlayerTransform()
        {
            if (PlayerCombatState.Instance != null) return PlayerCombatState.Instance.transform;
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            return p != null ? p.transform : null;
        }

        private static void Teleport(Transform player, Vector3 position, Quaternion rotation)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            player.SetPositionAndRotation(position + Vector3.up * 0.05f, rotation);
            if (controller != null) controller.enabled = wasEnabled;
        }

        private static void SetGameplay(bool enabled)
        {
            PlayerInputReader input = FindFirstObjectByType<PlayerInputReader>();
            if (input != null) input.SetGameplayEnabled(enabled);
        }

        /// <summary>Espera en tiempo de juego (la pausa la congela).</summary>
        private static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Fundido a negro (1) o de vuelta (0), con Time.deltaTime.</summary>
        private IEnumerator Fade(float target)
        {
            if (fade == null) yield break;
            float from = fade.alpha;
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                fade.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / fadeSeconds));
                yield return null;
            }
            fade.alpha = target;
        }

        /// <summary>Velo negro propio (Canvas creado por código, sobre el HUD y bajo los momentos y la pausa).</summary>
        private void BuildFade()
        {
            GameObject canvasObject = new GameObject("UI_LaMarea_Fundido", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 24;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject black = new GameObject("Negro", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = (RectTransform)black.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = black.GetComponent<Image>();
            image.color = new Color(0.01f, 0.01f, 0.03f, 1f);
            image.raycastTarget = false;

            fade = canvasObject.GetComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.interactable = false;
            fade.blocksRaycasts = false;
        }

        private static void Toast(string message, float hold)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, hold);
        }

        /// <summary>Subtítulo del cierre: se muestra siempre (es narrativo, no un efecto de sonido).</summary>
        private static void Subtitle(string message)
        {
            if (SubtitleUI.Instance != null) SubtitleUI.Instance.Show(message);
            else Toast(message, 2.6f);
        }
    }
}
