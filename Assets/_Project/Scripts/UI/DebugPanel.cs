using System.Text;
using Ascua.Characters;
using Ascua.Core;
using Ascua.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.UI
{
    /// <summary>
    /// Panel de depuración que se muestra/oculta con F1. Oculto por defecto.
    /// En builds de lanzamiento (no "Development Build") F1 no hace nada salvo que se marque
    /// allowInReleaseBuild. Muestra FPS, noche, Penumbra, zonas, luciérnagas, NPCs y controles.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField] private bool startVisible = false;
        [Tooltip("Permite abrir el panel también en builds finales (no Development Build).")]
        [SerializeField] private bool allowInReleaseBuild = false;
        [Tooltip("Segundos entre actualizaciones del texto (evita generar basura cada frame).")]
        [SerializeField] private float refreshInterval = 0.25f;

        private readonly StringBuilder builder = new StringBuilder(512);
        private bool visible;
        private float refreshTimer;
        private float fpsTimer;
        private int fpsFrames;
        private float fps;
        private FireflyPool fireflyPool;
        private CharacterAI[] characters;

        /// <summary>True si F1 está permitido en esta compilación.</summary>
        public bool IsAllowed => Application.isEditor || Debug.isDebugBuild || allowInReleaseBuild;

        private void Awake()
        {
            SetVisible(startVisible && IsAllowed);
        }

        private void Start()
        {
            fireflyPool = FindFirstObjectByType<FireflyPool>();
            characters = FindObjectsByType<CharacterAI>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (IsAllowed && keyboard != null && keyboard.f1Key.wasPressedThisFrame) SetVisible(!visible);
            // F5 (solo con el panel abierto): salta a la noche siguiente. Sirve para probar las noches generadas.
            if (IsAllowed && visible && keyboard != null && keyboard.f5Key.wasPressedThisFrame && NightManager.Instance != null)
            {
                NightManager.Instance.StartNextNight();
                return;
            }

            // Los FPS se miden siempre (es barato) para que el dato sea fiable al abrir el panel.
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fps = fpsFrames / fpsTimer;
                fpsFrames = 0;
                fpsTimer = 0f;
            }

            if (!visible) return;
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < refreshInterval) return;
            refreshTimer = 0f;
            Refresh();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (group != null)
            {
                group.alpha = value ? 1f : 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (value) Refresh();
        }

        private void Refresh()
        {
            if (text == null) return;
            builder.Clear();

            builder.Append("<b>DEBUG</b>  (F1 oculta · F5 salta de noche)\n");
            builder.Append("FPS: ").Append(Mathf.RoundToInt(fps)).Append("  (").Append((1000f / Mathf.Max(1f, fps)).ToString("0.0")).Append(" ms)\n");

            NightManager night = NightManager.Instance;
            if (night != null)
            {
                int seconds = Mathf.CeilToInt(night.Remaining);
                builder.Append("Noche ").Append(night.NightNumber).Append(" · ").Append(seconds / 60).Append(':').Append((seconds % 60).ToString("00"))
                       .Append(" · ritmo ").Append(night.Pacing).Append('\n');
            }

            Penumbra penumbra = Penumbra.Instance;
            if (penumbra != null)
            {
                builder.Append("Penumbra: radio ").Append(penumbra.CurrentRadius.ToString("0.0")).Append(" m · avance ")
                       .Append(Mathf.RoundToInt(penumbra.Advance * 100f)).Append("%\n");
            }

            foreach (ZoneHope zone in ZoneHope.All)
            {
                builder.Append("Zona ").Append(zone.DisplayName).Append(": ");
                if (zone.IsDead) builder.Append("apagada");
                else builder.Append(zone.Hope.ToString("0.0")).Append(zone.IsIlluminated ? " (iluminada)" : "")
                            .Append(" · Penumbra ").Append(Mathf.RoundToInt(zone.PenumbraOverlap * 100f)).Append('%');
                builder.Append('\n');
            }

            if (fireflyPool != null) builder.Append("Luciérnagas activas: ").Append(fireflyPool.ActiveCount).Append('\n');

            if (characters != null)
            {
                foreach (CharacterAI character in characters)
                {
                    if (character != null) builder.Append(character.name).Append(": ").Append(character.Mood).Append('\n');
                }
            }

            builder.Append("\n<b>Controles</b>\n");
            builder.Append("WASD / stick: moverse · Shift: correr\n");
            builder.Append("Click izq. / F / RT: avivar el Farol\n");
            builder.Append("Q · E / LB · RB: tamaño de la luz\n");
            builder.Append("Rueda: cámara · Esc: soltar ratón · F3: FPS");

            text.SetText(builder);
        }
    }
}
