using System.Text;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// El Diario de Ilse (Codex): 12 huecos; las páginas encontradas se leen, el resto aparece como "???".
    /// Se abre con J o desde el menú de pausa. ←/→ (o A/D, o los botones) para pasar página.
    /// </summary>
    public class CodexUI : MonoBehaviour
    {
        public static CodexUI Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text indexText;
        [SerializeField] private TMP_Text pageTitle;
        [SerializeField] private TMP_Text pageBody;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private DiaryPage[] pages;
        [SerializeField] private int totalPages = 12;

        private readonly StringBuilder builder = new StringBuilder(512);
        private bool open;
        private bool pausedByMe;
        private float previousTimeScale = 1f;
        private int selected = 1;

        public bool IsOpen => open;

        private void Awake()
        {
            Instance = this;
            SetVisible(false);
            if (previousButton != null) previousButton.onClick.AddListener(() => Move(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Move(1));
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.jKey.wasPressedThisFrame)
            {
                if (open) Close();
                else if (CanOpenAlone()) Open(false);
                return;
            }
            if (!open) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Move(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Move(1);
        }

        private static bool CanOpenAlone()
        {
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return false;
            if (PhotoMode.Instance != null && PhotoMode.Instance.IsActive) return false;
            return !GameSession.IsModalOpen || PauseMenu.IsPaused;
        }

        /// <summary>Abre el Diario. fromPause = el menú de pausa ya detuvo el juego.</summary>
        public void Open(bool fromPause)
        {
            if (open) return;
            open = true;
            pausedByMe = !fromPause && !PauseMenu.IsPaused;
            if (pausedByMe)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                PlayerInputReader input = FindFirstObjectByType<PlayerInputReader>();
                if (input != null) input.SetGameplayEnabled(false);
                GameSession.PushModal();
            }

            // Abre en la primera página encontrada.
            selected = 1;
            for (int n = 1; n <= totalPages; n++)
            {
                if (FindFound(n) != null) { selected = n; break; }
            }
            Refresh();
            SetVisible(true);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            SetVisible(false);
            if (pausedByMe)
            {
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
                PlayerInputReader input = FindFirstObjectByType<PlayerInputReader>();
                if (input != null) input.SetGameplayEnabled(true);
                GameSession.PopModal();
            }
            pausedByMe = false;
        }

        private void Move(int delta)
        {
            selected = Mathf.Clamp(selected + delta, 1, totalPages);
            Refresh();
        }

        private DiaryPage FindFound(int number)
        {
            if (pages == null) return null;
            foreach (DiaryPage page in pages)
            {
                if (page != null && page.number == number && GameProgress.HasPage(page.pageId)) return page;
            }
            return null;
        }

        private void Refresh()
        {
            int found = 0;
            builder.Clear();
            builder.Append("<b>DIARIO DE ILSE</b>\n\n");
            for (int n = 1; n <= totalPages; n++)
            {
                DiaryPage page = FindFound(n);
                if (page != null) found++;
                string line = page != null ? page.title : "???";
                bool current = n == selected;
                if (current) builder.Append("<color=#FFC46B>» ");
                else builder.Append(page != null ? "   " : "<color=#B8C4EC55>   ");
                builder.Append(n.ToString("00")).Append("  ").Append(line);
                if (current || page == null) builder.Append("</color>");
                builder.Append('\n');
            }
            builder.Append("\n<color=#B8C4EC88>").Append(found).Append("/").Append(totalPages).Append(" páginas · ←/→ pasar · J o Esc cerrar</color>");
            if (indexText != null) indexText.SetText(builder);

            DiaryPage selectedPage = FindFound(selected);
            if (pageTitle != null) pageTitle.text = selectedPage != null ? selectedPage.title : "???";
            if (pageBody != null)
            {
                pageBody.text = selectedPage != null
                    ? selectedPage.text
                    : "<i><color=#B8C4EC88>Esta página todavía no ha vuelto. Quizá esté en algún rincón de Vesper que nadie mira.</color></i>";
            }
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
