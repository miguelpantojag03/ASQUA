using System;
using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ascua.Narrative
{
    /// <summary>
    /// Muestra conversaciones: caja inferior con nombre, texto tipo máquina de escribir y elecciones.
    /// Mientras está abierta la noche se detiene (Time.timeScale = 0) y el jugador no se mueve.
    /// Las elecciones se conectan con QuestManager (aceptar, rechazar o completar misiones).
    /// Avanzar: E, Espacio, Enter, click o botón A. Elegir: 1-3, click o mando.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        public static DialogueRunner Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text continueHint;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceLabels;

        [Header("Ritmo")]
        [SerializeField] private float charactersPerSecond = 42f;
        [SerializeField] private float fadeSpeed = 6f;

        [Header("Colores de quien habla")]
        [SerializeField] private Color playerColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color defaultColor = new Color(0.72f, 0.77f, 0.93f);
        [SerializeField] private string playerName = "Ilse";

        /// <summary>Se dispara al cerrar cualquier conversación.</summary>
        public event Action<NpcInteractable> DialogueClosed;

        private readonly List<DialogueLine> lines = new List<DialogueLine>();
        private DialogueSequence current;
        private NpcInteractable npc;
        private QuestRuntime quest;
        private bool turnInOnClose;
        private DialogueSequence followUp;
        private int lineIndex;
        private float visibleChars;
        private bool choosing;
        private bool open;
        private float previousTimeScale = 1f;
        private PlayerInputReader playerInput;
        private CharacterAI talkingCharacter;
        private float targetAlpha;
        private float baseBodySize = 30f;
        private float baseSpeakerSize = 30f;

        public bool IsOpen => open;

        private void Awake()
        {
            Instance = this;
            if (bodyText != null) baseBodySize = bodyText.fontSize;
            if (speakerText != null) baseSpeakerSize = speakerText.fontSize;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            for (int i = 0; choiceButtons != null && i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i].onClick.AddListener(() => Choose(index));
                choiceButtons[i].gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            playerInput = FindFirstObjectByType<PlayerInputReader>();
        }

        /// <summary>
        /// Abre una conversación. quest = misión asociada (para que las elecciones la acepten o completen).
        /// turnInOnClose = al terminar, entrega la misión (diálogo de agradecimiento).
        /// </summary>
        public void Play(DialogueSequence sequence, NpcInteractable speakerNpc, QuestRuntime relatedQuest, Transform listener, bool turnInOnClose = false)
        {
            if (sequence == null || sequence.lines == null || sequence.lines.Count == 0) return;
            if (NightManager.Instance != null && !NightManager.Instance.IsRunning) return;

            current = sequence;
            npc = speakerNpc;
            quest = relatedQuest;
            this.turnInOnClose = turnInOnClose;
            followUp = null;

            lines.Clear();
            lines.AddRange(sequence.lines);
            lineIndex = 0;

            if (!open)
            {
                open = true;
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                if (playerInput != null) playerInput.SetGameplayEnabled(false);
                GameSession.PushModal();

                talkingCharacter = speakerNpc != null ? speakerNpc.GetComponent<CharacterAI>() : null;
                if (talkingCharacter != null) talkingCharacter.BeginConversation(listener);
            }

            targetAlpha = 1f;
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }
            ShowLine();
        }

        private void Update()
        {
            if (group != null && !Mathf.Approximately(group.alpha, targetAlpha))
            {
                group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
            }
            if (!open) return;

            if (choosing)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.digit1Key.wasPressedThisFrame) Choose(0);
                    else if (keyboard.digit2Key.wasPressedThisFrame) Choose(1);
                    else if (keyboard.digit3Key.wasPressedThisFrame) Choose(2);
                }
                return;
            }

            // Máquina de escribir (tiempo real: la noche está en pausa).
            DialogueLine line = lines[lineIndex];
            int total = line.text != null ? line.text.Length : 0;
            if (visibleChars < total)
            {
                visibleChars = Mathf.Min(total, visibleChars + charactersPerSecond * Time.unscaledDeltaTime);
                bodyText.maxVisibleCharacters = Mathf.FloorToInt(visibleChars);
            }

            bool finished = visibleChars >= total;
            if (continueHint != null) continueHint.alpha = finished ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;

            if (AdvancePressed())
            {
                if (!finished)
                {
                    visibleChars = total;
                    bodyText.maxVisibleCharacters = total;
                }
                else
                {
                    NextLine();
                }
            }
        }

        private static bool AdvancePressed()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                   || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        }

        private void ShowLine()
        {
            DialogueLine line = lines[lineIndex];
            bool isPlayer = line.speaker == playerName;
            speakerText.text = line.speaker;
            speakerText.color = isPlayer ? playerColor : npc != null && line.speaker == npc.DisplayName ? npc.AccentColor : defaultColor;
            // Accesibilidad: tamaño de texto elegido en el menú de pausa.
            float scale = AccessibilitySettings.DialogueTextScale;
            bodyText.fontSize = baseBodySize * scale;
            speakerText.fontSize = baseSpeakerSize * scale;
            bodyText.text = line.text;
            bodyText.maxVisibleCharacters = 0;
            visibleChars = 0f;
            choosing = false;
            SetChoicesVisible(false);
        }

        private void NextLine()
        {
            lineIndex++;
            if (lineIndex < lines.Count)
            {
                ShowLine();
                return;
            }

            if (current != null && current.HasChoices) ShowChoices();
            else Close();
        }

        private void ShowChoices()
        {
            choosing = true;
            if (continueHint != null) continueHint.alpha = 0f;
            // La última línea queda entera a la vista mientras se elige.
            if (bodyText != null) bodyText.maxVisibleCharacters = 99999;

            List<DialogueChoice> choices = current.choices;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool used = i < choices.Count;
                choiceButtons[i].gameObject.SetActive(used);
                if (used) choiceLabels[i].text = (i + 1) + "   " + choices[i].label;
            }

            if (EventSystem.current != null && choiceButtons.Length > 0) EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);
        }

        private void Choose(int index)
        {
            if (!choosing || current == null || index < 0 || index >= current.choices.Count) return;
            DialogueChoice choice = current.choices[index];
            choosing = false;
            SetChoicesVisible(false);

            QuestManager manager = QuestManager.Instance;
            switch (choice.action)
            {
                case ChoiceAction.AceptarMision:
                    if (manager != null) manager.Accept(quest);
                    if (manager != null) manager.ApplyChoiceEffects(choice, npc != null ? npc.NpcId : null);
                    break;

                case ChoiceAction.RechazarMision:
                    if (manager != null) manager.Decline(quest);
                    if (manager != null) manager.ApplyChoiceEffects(choice, npc != null ? npc.NpcId : null);
                    break;

                case ChoiceAction.CompletarMision:
                    if (manager != null) manager.CompleteFromChoice(quest, choice);
                    // Tras completar por elección se muestra la despedida de la misión, si existe.
                    if (quest != null && quest.Data.completedDialogue != null) followUp = quest.Data.completedDialogue;
                    break;

                default:
                    if (manager != null) manager.ApplyChoiceEffects(choice, npc != null ? npc.NpcId : null);
                    break;
            }

            if (followUp != null)
            {
                DialogueSequence next = followUp;
                followUp = null;
                current = next;
                lines.Clear();
                lines.AddRange(next.lines);
                lineIndex = 0;
                ShowLine();
                return;
            }

            Close();
        }

        /// <summary>Salta a la última línea y la termina (muestra elecciones o cierra). Útil para pruebas y para la IA.</summary>
        public void SkipToEnd()
        {
            if (!open || choosing || lines.Count == 0) return;
            lineIndex = lines.Count - 1;
            ShowLine();
            NextLine();
        }

        /// <summary>Elige una opción por índice (equivale a pulsar 1-3).</summary>
        public void SelectChoice(int index)
        {
            Choose(index);
        }

        /// <summary>True si se están mostrando elecciones.</summary>
        public bool IsChoosing => choosing;

        private void SetChoicesVisible(bool visible)
        {
            if (choiceButtons == null) return;
            foreach (Button button in choiceButtons) button.gameObject.SetActive(visible && button.gameObject.activeSelf);
        }

        /// <summary>Cierra la conversación, reanuda la noche y, si toca, entrega la misión.</summary>
        public void Close()
        {
            if (!open) return;
            open = false;
            choosing = false;
            targetAlpha = 0f;
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            SetChoicesVisible(false);

            if (turnInOnClose && quest != null && QuestManager.Instance != null) QuestManager.Instance.TurnIn(quest);
            turnInOnClose = false;

            if (talkingCharacter != null) talkingCharacter.EndConversation();
            talkingCharacter = null;

            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            if (playerInput != null) playerInput.SetGameplayEnabled(true);
            GameSession.PopModal();

            NpcInteractable closedNpc = npc;
            npc = null;
            quest = null;
            current = null;
            DialogueClosed?.Invoke(closedNpc);
        }
    }
}
