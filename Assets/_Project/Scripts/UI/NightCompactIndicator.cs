using Ascua.Combat;
using Ascua.Core;
using Ascua.Narrative;
using Ascua.Quests;
using Ascua.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.UI
{
    /// <summary>
    /// Indicador compacto "Noche N · objetivo actual", bajo el reloj de la noche. Creado por código (HUDController).
    ///  - No guarda datos propios: lee la noche de NightManager y el objetivo de QuestManager.TrackedQuest
    ///    (el mismo que muestra ObjectiveTrackerUI) o, durante una oleada, la zona que hay que defender.
    ///  - H (o la cruceta izquierda del mando) lo oculta y lo vuelve a mostrar; la elección se recuerda.
    ///  - Respeta HudFocus: es de la capa secundaria (baja en diálogos y en combate como el resto).
    ///  - Se oculta durante la Cacería del Heraldo y el combate con La Marea (usan ese mismo hueco).
    /// Colocación (referencia 1920×1080): x 711-1209 con su filo (entre el objetivo actual, hasta 652, y la
    /// Esperanza, desde 1418) e y -140…-172 (el reloj "Noche" acaba en -128 con su filo).
    /// </summary>
    public class NightCompactIndicator : MonoBehaviour
    {
        private const string PrefKey = "ascua_indicador_noche";
        private const float Width = 470f;
        private const float Top = -140f;

        private static NightCompactIndicator instance;

        private CanvasGroup group;
        private TMP_Text label;
        private HudFocus focus;
        private float refreshTimer;
        private string shownText;

        /// <summary>Preferencia del jugador (H): true = visible.</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Crea el indicador si aún no existe en la escena.</summary>
        public static NightCompactIndicator Create()
        {
            if (instance != null) return instance;
            return new GameObject("UI_IndicadorNoche").AddComponent<NightCompactIndicator>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            group = HudBuild.CreateCanvas(transform, "Lienzo", 10);
            RectTransform panel = HudBuild.Rect("Indicador_Noche", group.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, Top), new Vector2(Width, 32f));
            HudBuild.Style(panel, UIRole.Prompt);
            label = HudBuild.AddText(HudBuild.Fill("Texto", panel, 10f, 0f, 10f, 0f), "", 21f, new Color(0.85f, 0.88f, 0.96f), TextAlignmentOptions.Center);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Start()
        {
            focus = FindFirstObjectByType<HudFocus>();
        }

        private void Update()
        {
            if (group == null) return;
            float dt = Time.unscaledDeltaTime;

            if (TogglePressed() && !PauseMenu.IsPaused && !GameSession.IsModalOpen) Enabled = !Enabled;

            NightManager night = NightManager.Instance;
            bool visible = Enabled && night != null && night.IsRunning && !Hidden();
            float target = visible ? 1f : 0f;
            // Capa secundaria: misma opacidad que el objetivo, la Esperanza y el reloj (HudFocus).
            if (focus != null) target *= focus.SecondaryAlpha;
            group.alpha = Mathf.MoveTowards(group.alpha, target, dt * 5f);
            if (!visible) return;

            refreshTimer -= dt;
            if (refreshTimer > 0f && shownText != null) return;
            refreshTimer = 0.25f;
            string text = "Noche " + night.NightNumber + "  ·  " + CurrentObjective();
            if (text == shownText) return;
            shownText = text;
            label.text = text;
        }

        /// <summary>Diálogo, pausa, modo foto, Cacería o La Marea en pantalla.</summary>
        private static bool Hidden()
        {
            if (PauseMenu.IsPaused) return true;
            if (PhotoMode.Instance != null && PhotoMode.Instance.IsActive) return true;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return true;
            if (HeraldoHuntDirector.Instance != null && HeraldoHuntDirector.Instance.IsBusy) return true;
            FinalBossDirector boss = FinalBossDirector.Instance;
            return boss != null && boss.IsEncounterActive;
        }

        /// <summary>El objetivo que ya decide el juego (oleada > misión seguida por QuestManager).</summary>
        private static string CurrentObjective()
        {
            CombatWaveDirector waves = CombatWaveDirector.Instance;
            if (waves != null && waves.HasActiveWave)
            {
                ZoneHope zone = ZoneHope.Find(waves.ActiveZoneId);
                return "Defiende " + (zone != null ? zone.DisplayName : waves.ActiveZoneId);
            }
            QuestManager quests = QuestManager.Instance;
            QuestRuntime q = quests != null ? quests.TrackedQuest : null;
            if (q == null) return "Lleva la luz a Vesper";
            return q.State == QuestState.PorEntregar ? q.Data.title + " · entrégala" : q.Data.title;
        }

        private static bool TogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && keyboard.hKey.wasPressedThisFrame)
                   || (gamepad != null && gamepad.dpad.left.wasPressedThisFrame);
        }
    }
}
