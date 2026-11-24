using Ascua.Combat;
using Ascua.LevelGen;
using Ascua.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Barra de vida de La Marea, arriba al centro, solo mientras dura el combate. Creada por código
    /// (FinalBossDirector la pide al crearse) con el estilo del UIStyleKit.
    ///  - Nombre "La Marea" y 3 rombos de fase (se encienden al avanzar de fase).
    ///  - Dos marcas sobre la barra en el 66 % y el 33 %: donde cambia de fase.
    ///  - El relleno brilla cuando está iluminada por el Farol (daño completo) y se apaga si no.
    /// Colocación (referencia 1920×1080): bajo el reloj de la noche ("Noche", hasta y = -128 con su filo) y entre
    /// el objetivo actual (izquierda, hasta x = 652) y la Esperanza (derecha, desde x = 1418): 470 px de ancho.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        private const float Width = 470f;
        private const float Height = 60f;
        private const float Top = -142f;

        private static readonly Color Amber = new Color(1f, 0.769f, 0.42f);
        private static readonly Color Cold = new Color(0.72f, 0.77f, 0.93f);
        private static readonly Color LitFill = new Color(0.45f, 0.85f, 0.95f);
        private static readonly Color DimFill = new Color(0.28f, 0.33f, 0.5f);
        private static readonly Color PipOff = new Color(1f, 1f, 1f, 0.18f);

        private static BossHealthBarUI instance;

        private CanvasGroup group;
        private RectTransform fill;
        private Image fillImage;
        private TMP_Text stateText;
        private readonly Image[] pips = new Image[3];
        private float shown = 1f;
        private int lastPhase = -1;
        private int lastLit = -1;

        /// <summary>Crea la barra si aún no existe (oculta hasta que empiece el combate).</summary>
        public static BossHealthBarUI Ensure()
        {
            if (instance != null) return instance;
            return new GameObject("UI_LaMarea_Vida").AddComponent<BossHealthBarUI>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Build()
        {
            group = HudBuild.CreateCanvas(transform, "Lienzo", 11);   // sobre el HUD (10), bajo los momentos y la pausa

            RectTransform panel = HudBuild.Rect("Vida_LaMarea", group.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, Top), new Vector2(Width, Height));
            HudBuild.Style(panel, UIRole.Hud);

            RectTransform name = HudBuild.Rect("Nombre", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -2f), new Vector2(215f, 30f));
            TMP_Text nameText = HudBuild.AddText(name, "LA MAREA", 24f, Amber, TextAlignmentOptions.TopLeft);
            nameText.fontStyle = FontStyles.Bold;
            nameText.characterSpacing = 8f;

            // 3 rombos de fase a la derecha (el mismo lenguaje que los puntos del tamaño del Farol).
            for (int i = 0; i < pips.Length; i++)
            {
                RectTransform pip = HudBuild.Rect("Fase_" + (i + 1), panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(Width - 48f + i * 18f, -15f), new Vector2(11f, 11f));
                pip.localRotation = Quaternion.Euler(0f, 0f, 45f);
                pips[i] = HudBuild.AddImage(pip, PipOff);
            }

            RectTransform state = HudBuild.Rect("Estado", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-66f, -5f), new Vector2(180f, 26f));
            stateText = HudBuild.AddText(state, "", 18f, Cold, TextAlignmentOptions.TopRight);

            // Barra: fondo, relleno (anchorMax.x = vida) y las dos marcas de cambio de fase.
            RectTransform back = HudBuild.Rect("BarraFondo", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(0f, 14f));
            HudBuild.AddImage(back, new Color(0.05f, 0.06f, 0.14f, 0.95f));
            fill = HudBuild.Rect("Relleno", back, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            fillImage = HudBuild.AddImage(fill, DimFill);
            AddMark(back, FinalBossRules.Phase2Below);
            AddMark(back, FinalBossRules.Phase3Below);

            group.alpha = 0f;
        }

        private static void AddMark(RectTransform bar, float at)
        {
            RectTransform mark = HudBuild.Rect("Marca_" + Mathf.RoundToInt(at * 100f), bar, new Vector2(at, 0f), new Vector2(at, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 8f));
            HudBuild.AddImage(mark, new Color(Amber.r, Amber.g, Amber.b, 0.9f));
        }

        private void Update()
        {
            if (group == null) return;
            float dt = Time.unscaledDeltaTime;

            FinalBossDirector director = FinalBossDirector.Instance;
            MareaBoss boss = director != null ? director.Boss : null;
            bool active = director != null && director.IsEncounterActive && boss != null;
            bool hidden = PauseMenu.IsPaused
                          || (PhotoMode.Instance != null && PhotoMode.Instance.IsActive)
                          || (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen);
            group.alpha = Mathf.MoveTowards(group.alpha, active && !hidden ? 1f : 0f, dt * 4f);
            if (boss == null || group.alpha <= 0f) return;

            float target = boss.IsAlive ? boss.Health01 : 0f;
            // Al reintentar, la barra vuelve a llenarse de golpe; al recibir daño baja suave.
            shown = target > shown + 0.05f ? target : Mathf.Lerp(shown, target, 1f - Mathf.Exp(-10f * dt));
            fill.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);

            fillImage.color = Color.Lerp(fillImage.color, boss.IsLit ? LitFill : DimFill, 1f - Mathf.Exp(-12f * dt));

            int phase = boss.Phase;
            int lit = boss.IsLit ? 1 : 0;
            if (phase == lastPhase && lit == lastLit) return;
            lastPhase = phase;
            lastLit = lit;
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < phase ? Amber : PipOff;
            stateText.text = lit == 1 ? "<color=#73D9F2>bajo la luz</color>" : "sin luz · ilumínala";
        }
    }
}
