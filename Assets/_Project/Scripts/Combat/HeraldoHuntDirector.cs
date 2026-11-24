using System.Collections.Generic;
using Ascua.Audio;
using Ascua.Characters;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Narrative;
using Ascua.UI;
using Ascua.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ascua.Combat
{
    /// <summary>
    /// Cacería del Heraldo: en momentos fijos de la noche (HuntSchedule) el Heraldo corre rápido
    /// durante unos segundos e Ilse debe llegar a un refugio (SafeHaven). Nunca hay Game Over.
    ///  1. Cuántas: noches 1-2 = 0, noche 3 = 1, noche 4+ = 2, salvo que el nivel diga otra cosa (huntCount).
    ///  2. Solo empieza con el Heraldo presente y sin diálogo, oleada, momento narrativo ni pausa.
    ///     Si no se da, reintenta cada segundo hasta el 95 % de la noche.
    ///  3. Aviso de 4 s: texto, sonido de amenaza y farol violeta parpadeando (sin parpadeo con
    ///     "reducir destellos"). Los refugios ya se ven.
    ///  4. Cacería: texto en el HUD con flecha (8 sentidos) y distancia al refugio, música en tensión
    ///     máxima y subtítulo "[Pasos veloces]". Termina en el refugio, si te alcanza o si pierde interés.
    /// Todo usa Time.deltaTime: la pausa lo congela. Se crea por código (sin tocar escenas).
    /// </summary>
    public class HeraldoHuntDirector : MonoBehaviour
    {
        private enum Phase { Idle, Warning, Hunting }

        public static HeraldoHuntDirector Instance { get; private set; }

        [Tooltip("-1 = automático según la noche; 0-3 lo fija el nivel.")]
        [SerializeField] private int huntCount = -1;
        [SerializeField] private float huntSpeed = 4.8f;
        [SerializeField] private float warningSeconds = 4f;
        [Tooltip("A partir de este momento de la noche ya no se intenta la cacería pendiente.")]
        [SerializeField, Range(0f, 1f)] private float lastChance = 0.95f;
        [SerializeField] private float retrySeconds = 1f;
        [SerializeField] private float stepsSubtitleEvery = 3.5f;

        private readonly List<float> moments = new List<float>();
        private readonly HashSet<string> activeMoments = new HashSet<string>();
        private bool scheduleDirty = true;
        private int nextMoment;
        private Phase phase = Phase.Idle;
        private float timer;
        private float retryTimer;
        private float stepsTimer;
        private Heraldo_AI hookedHeraldo;
        private HeraldoOmen omen;
        private Transform player;

        // HUD creado por código.
        private CanvasGroup hudGroup;
        private TMP_Text hudText;
        private TMP_Text arrowText;

        public bool IsWarning => phase == Phase.Warning;
        public bool IsHunting => phase == Phase.Hunting;
        public bool IsBusy => phase != Phase.Idle;
        public int ScheduledCount => moments.Count;
        public int RemainingCount => Mathf.Max(0, moments.Count - nextMoment);
        public float HuntSpeed => huntSpeed;
        /// <summary>Momento (0-1) de la próxima cacería, o -1 si no queda ninguna.</summary>
        public float NextMoment => nextMoment < moments.Count ? moments[nextMoment] : -1f;

        // ================================================================== Creación por código

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene();
        }

        /// <summary>Solo en escenas con Heraldo (la del título no lo tiene).</summary>
        private static void EnsureForScene()
        {
            if (FindFirstObjectByType<Heraldo_AI>() != null) Ensure();
        }

        /// <summary>Devuelve el director de la escena, creándolo si aún no existe.</summary>
        public static HeraldoHuntDirector Ensure()
        {
            if (Instance != null) return Instance;
            HeraldoHuntDirector existing = FindFirstObjectByType<HeraldoHuntDirector>();
            if (existing != null) return existing;
            return new GameObject("HeraldoHuntDirector").AddComponent<HeraldoHuntDirector>();
        }

        /// <summary>Ajustes de la noche (los aplica NightManager desde el NightQuestSet).</summary>
        public void Configure(int count, float speed)
        {
            huntCount = count;
            huntSpeed = speed;
            scheduleDirty = true;
        }

        // ================================================================== Ciclo

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildHud();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unhook();
            if (AudioManager.Instance != null) AudioManager.Instance.SetHuntTension(false);
            SafeHaven.SetHuntVisible(false);
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
            SafeHaven.EnsureCreated();
            omen = FindFirstObjectByType<HeraldoOmen>();
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        private void OnNarrativeMoment(string id, bool started)
        {
            if (string.IsNullOrEmpty(id)) id = "?";
            if (started) activeMoments.Add(id);
            else activeMoments.Remove(id);
        }

        private void Update()
        {
            HookHeraldo();

            NightManager night = NightManager.Instance;
            if (night == null || !night.IsRunning)
            {
                // Fin de la noche: nada de cacerías colgando sobre el resumen.
                if (phase != Phase.Idle) Cancel();
                return;
            }

            UpdateHudVisibility();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // en pausa no avanza nada

            if (scheduleDirty) BuildSchedule(night);

            switch (phase)
            {
                case Phase.Idle:
                    UpdateIdle(night, dt);
                    break;

                case Phase.Warning:
                    timer -= dt;
                    if (hookedHeraldo == null || !hookedHeraldo.IsPresent)
                    {
                        Cancel();
                        break;
                    }
                    if (timer <= 0f) BeginHunt();
                    break;

                case Phase.Hunting:
                    UpdateHuntHud();
                    stepsTimer -= dt;
                    if (stepsTimer <= 0f)
                    {
                        stepsTimer = stepsSubtitleEvery;
                        StepsSubtitle();
                    }
                    // Por si el Heraldo desaparece sin avisar.
                    if (hookedHeraldo == null || !hookedHeraldo.IsHunting) FinishHunt();
                    break;
            }
        }

        private void BuildSchedule(NightManager night)
        {
            scheduleDirty = false;
            int count = HuntSchedule.CountForNight(night.NightNumber, huntCount);
            // Semilla fija por noche: al reintentar la noche, las cacerías caen en el mismo momento.
            int seed = night.NightNumber * 7919 + 101;
            moments.Clear();
            moments.AddRange(HuntSchedule.Compute(count, seed));
            nextMoment = 0;
        }

        private void UpdateIdle(NightManager night, float dt)
        {
            if (nextMoment >= moments.Count) return;
            float progress = night.Progress01;
            if (progress < moments[nextMoment]) return;
            if (progress > lastChance)
            {
                // Ya no da tiempo: esta cacería se pierde.
                nextMoment++;
                return;
            }

            retryTimer -= dt;
            if (retryTimer > 0f) return;
            retryTimer = retrySeconds;
            if (!CanStart()) return;

            nextMoment++;
            BeginWarning();
        }

        /// <summary>Heraldo presente y libre; sin diálogo, oleada, momento narrativo ni pausa.</summary>
        public bool CanStart()
        {
            Heraldo_AI heraldo = Heraldo_AI.Instance;
            if (heraldo == null || !heraldo.IsPresent || !heraldo.IsAvailableForHunt) return false;
            if (phase != Phase.Idle) return false;
            if (PauseMenu.IsPaused) return false;
            if (FinalBossDirector.HoldsNight) return false;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return false;
            CombatWaveDirector waves = CombatWaveDirector.Instance;
            if (waves != null && (waves.HasActiveWave || waves.AliveCount > 0)) return false;
            if (activeMoments.Count > 0) return false;
            if (omen != null && omen.IsPlaying) return false;
            return true;
        }

        /// <summary>Depuración (F6): lanza una cacería ya (con su aviso). Devuelve false si ahora no se puede.</summary>
        public bool StartNow()
        {
            Heraldo_AI heraldo = Heraldo_AI.Instance;
            if (heraldo != null && !heraldo.IsPresent) heraldo.ForceAppear();
            HookHeraldo();
            if (!CanStart()) return false;
            BeginWarning();
            return true;
        }

        // ================================================================== Fases

        private void BeginWarning()
        {
            phase = Phase.Warning;
            timer = warningSeconds;

            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show("Escucha… se acerca", warningSeconds);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayHuntWarning();
            if (hookedHeraldo != null) hookedHeraldo.SetWarningFlicker(true);
            // Los refugios se encienden ya, para que dé tiempo a buscarlos.
            SafeHaven.EnsureCreated();
            SafeHaven.SetHuntVisible(true);
        }

        private void BeginHunt()
        {
            Heraldo_AI heraldo = hookedHeraldo;
            if (heraldo == null || player == null)
            {
                Cancel();
                return;
            }
            heraldo.SetWarningFlicker(false);

            // Aparece del lado contrario al refugio más cercano (si no hay refugio, detrás de Ilse).
            SafeHaven haven = SafeHaven.NearestRefuge(player.position);
            Vector3 awayFrom = haven != null ? haven.Center : player.position + player.forward * 5f;

            phase = Phase.Hunting;   // antes de StartHunt: su evento ya nos encuentra cazando
            if (!heraldo.StartHunt(huntSpeed, awayFrom))
            {
                Cancel();
                return;
            }
            if (AudioManager.Instance != null) AudioManager.Instance.SetHuntTension(true);
            stepsTimer = stepsSubtitleEvery;
            StepsSubtitle();
            UpdateHuntHud();
        }

        private void OnHeraldoHuntEnded(Heraldo_AI.HuntOutcome outcome)
        {
            // Solo si la cacería terminó jugando (no al cancelarla al acabar la noche).
            if (phase == Phase.Hunting && outcome == Heraldo_AI.HuntOutcome.PerdioInteres && ZoneLostNotification.Instance != null)
            {
                ZoneLostNotification.Instance.Show("El Heraldo pierde tu rastro", 2.4f);
            }
            FinishHunt();
        }

        private void FinishHunt()
        {
            phase = Phase.Idle;
            retryTimer = retrySeconds;
            if (AudioManager.Instance != null) AudioManager.Instance.SetHuntTension(false);
            SafeHaven.SetHuntVisible(false);
            SetHudVisible(false);
        }

        /// <summary>Corta el aviso o la cacería sin resultado (fin de la noche, Heraldo ausente...).</summary>
        private void Cancel()
        {
            FinishHunt();
            if (hookedHeraldo != null)
            {
                hookedHeraldo.SetWarningFlicker(false);
                if (hookedHeraldo.IsHunting) hookedHeraldo.EndHunt(false);
            }
        }

        private static void StepsSubtitle()
        {
            // Sin sonido nuevo: solo el subtítulo (si el jugador los tiene activados).
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(null, 0f, 1f, "[Pasos veloces]");
        }

        private void HookHeraldo()
        {
            Heraldo_AI current = Heraldo_AI.Instance;
            if (current == hookedHeraldo) return;
            Unhook();
            hookedHeraldo = current;
            if (hookedHeraldo != null) hookedHeraldo.HuntEnded += OnHeraldoHuntEnded;
        }

        private void Unhook()
        {
            if (hookedHeraldo != null) hookedHeraldo.HuntEnded -= OnHeraldoHuntEnded;
            hookedHeraldo = null;
        }

        // ================================================================== HUD

        private void BuildHud()
        {
            GameObject canvasObject = new GameObject("UI_CaceriaHeraldo", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;   // sobre el HUD (10), bajo los momentos (25) y la pausa (30)
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hudGroup = canvasObject.GetComponent<CanvasGroup>();
            hudGroup.interactable = false;
            hudGroup.blocksRaycasts = false;

            // Franja oscura arriba al centro: se lee sobre cualquier fondo.
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -150f);
            panelRect.sizeDelta = new Vector2(760f, 78f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.07f, 0.78f);
            background.raycastTarget = false;

            arrowText = CreateText(panel.transform, "Flecha", new Vector2(-330f, 0f), new Vector2(70f, 70f), 46f);
            arrowText.text = ArrowGlyph();
            hudText = CreateText(panel.transform, "Texto", new Vector2(35f, 0f), new Vector2(640f, 70f), 30f);

            SetHudVisible(false);
        }

        private static TMP_Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TMP_Text text = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.769f, 0.42f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        /// <summary>Flecha "↑" (se gira en 8 sentidos); "^" si la fuente no la tiene.</summary>
        private static string ArrowGlyph()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font != null && !font.HasCharacter('↑', true, true)) return "^";
            return "↑";
        }

        private void SetHudVisible(bool visible)
        {
            if (hudGroup != null) hudGroup.alpha = visible ? 1f : 0f;
        }

        private void UpdateHudVisibility()
        {
            bool hidden = PauseMenu.IsPaused || (PhotoMode.Instance != null && PhotoMode.Instance.IsActive);
            SetHudVisible(phase == Phase.Hunting && !hidden);
        }

        private void UpdateHuntHud()
        {
            if (hudText == null || arrowText == null || player == null) return;

            SafeHaven haven = SafeHaven.NearestRefuge(player.position);
            if (haven == null)
            {
                arrowText.text = "";
                hudText.text = "¡Corre! Aléjate del Heraldo";
                return;
            }

            if (haven.Contains(player.position))
            {
                arrowText.text = "";
                hudText.text = "Quédate en la luz de " + haven.DisplayName + "…";
                return;
            }

            Vector3 toHaven = haven.Center - player.position;
            toHaven.y = 0f;
            int meters = Mathf.Max(0, Mathf.RoundToInt(toHaven.magnitude - haven.Radius));
            int sector = DirectionSector(toHaven);
            arrowText.text = ArrowGlyph();
            arrowText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f * sector);
            hudText.text = "Refugio: " + haven.DisplayName + " · " + meters + " m";
        }

        /// <summary>
        /// Dirección respecto a la cámara en 8 sentidos: 0 = delante (↑), 2 = derecha (→),
        /// 4 = detrás (↓), 6 = izquierda (←); los impares son las diagonales.
        /// </summary>
        private static int DirectionSector(Vector3 direction)
        {
            Transform view = Camera.main != null ? Camera.main.transform : null;
            Vector3 forward = view != null ? view.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            float angle = Mathf.Atan2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(angle / 45f);
            return ((sector % 8) + 8) % 8;
        }
    }
}
