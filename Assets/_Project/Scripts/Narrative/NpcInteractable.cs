using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Core;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Hace que un personaje se pueda hablar (acercarse + mantener E).
    /// Decide qué diálogo toca según su misión: ofrecerla, recordarla o agradecerla.
    /// Sin misiones pendientes, usa su diálogo de ambiente.
    /// </summary>
    [RequireComponent(typeof(NpcIdentity))]
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        private static readonly List<NpcInteractable> all = new List<NpcInteractable>();

        [Tooltip("Lo que dice cuando no tiene ninguna misión que ofrecer o entregar.")]
        [SerializeField] private DialogueSequence idleDialogue;
        [Tooltip("Altura del punto al que mira la cámara / del aviso de interacción.")]
        [SerializeField] private float talkHeight = 1.3f;
        [SerializeField] private bool interactable = true;

        private NpcIdentity identity;

        public static IReadOnlyList<NpcInteractable> All => all;
        public string NpcId => identity != null ? identity.NpcId : name;
        public string DisplayName => identity != null ? identity.DisplayName : name;
        public Color AccentColor => identity != null ? identity.AccentColor : Color.white;
        public Vector3 TalkPoint => transform.position + Vector3.up * talkHeight;
        public bool CanInteract => interactable && isActiveAndEnabled;

        // IInteractable: hablar se hace manteniendo E un instante (evita abrir diálogos sin querer).
        public Vector3 InteractionPoint => transform.position;
        public string InteractionVerb => "Hablar con";
        public string InteractionName => DisplayName;
        public float InteractionHoldTime => 0.35f;
        public float InteractionRadius => 2.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        public static NpcInteractable Find(string npcId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].NpcId == npcId) return all[i];
            }
            return null;
        }

        private void Awake()
        {
            identity = GetComponent<NpcIdentity>();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            InteractableRegistry.Register(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            InteractableRegistry.Unregister(this);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
        }

        public void SetIdleDialogue(DialogueSequence dialogue)
        {
            idleDialogue = dialogue;
        }

        /// <summary>Abre la conversación adecuada al estado de su misión.</summary>
        public void Interact(Transform speaker)
        {
            DialogueRunner runner = DialogueRunner.Instance;
            if (runner == null || runner.IsOpen) return;

            EventBus.RaiseNpcTalked(NpcId);

            QuestRuntime quest = QuestManager.Instance != null ? QuestManager.Instance.GetQuestForNpc(NpcId) : null;
            if (quest == null)
            {
                if (idleDialogue != null) runner.Play(idleDialogue, this, null, speaker);
                return;
            }

            switch (quest.State)
            {
                case QuestState.Disponible:
                    // Oferta + 1-2 frases de por qué le importa (contexto emocional).
                    runner.Play(QuestDialogues.Offer(quest, DisplayName), this, quest, speaker);
                    break;
                case QuestState.Activa:
                    // Nerviosa, aliviada o normal según cómo vaya de verdad la misión.
                    runner.Play(QuestDialogues.Progress(quest, idleDialogue), this, quest, speaker);
                    break;
                case QuestState.PorEntregar:
                    runner.Play(quest.Data.completedDialogue, this, quest, speaker, turnInOnClose: true);
                    break;
            }
        }
    }
}
