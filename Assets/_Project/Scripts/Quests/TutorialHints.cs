using System;
using System.Collections.Generic;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.World;
using TMPro;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Tutorial diegético mínimo de la Noche 1: pensamientos de Ilse en pantalla, uno cada vez,
    /// que avanzan cuando el jugador hace lo que piden. Solo se ve la primera vez (GameProgress).
    /// </summary>
    public class TutorialHints : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField] private float fadeSpeed = 3f;

        private class Step
        {
            public string Line;
            public Func<bool> Done;
            public float MinSeconds;
        }

        private readonly List<Step> steps = new List<Step>();
        private int index;
        private float stepTime;
        private bool fireflyCollected;
        private bool itemPicked;
        private float lanternTime;
        private LanternController lantern;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        private void OnEnable()
        {
            Firefly.AnyCollected += OnFirefly;
            EventBus.ItemPickedUp += OnItem;
        }

        private void OnDisable()
        {
            Firefly.AnyCollected -= OnFirefly;
            EventBus.ItemPickedUp -= OnItem;
        }

        private void OnFirefly(Firefly firefly) => fireflyCollected = true;
        private void OnItem(string id, string name) => itemPicked = true;

        private void Start()
        {
            lantern = FindFirstObjectByType<LanternController>();
            // El tutorial es de la Noche 1: en noches siguientes no vuelve aunque quedara a medias.
            if (!GameProgress.TutorialPending || GameProgress.CurrentNight > 1)
            {
                enabled = false;
                return;
            }

            // Una frase, una acción. Se avanza haciendo, no leyendo.
            steps.Add(new Step { Line = "Algo brilla en el suelo, justo delante. Me acerco y pulso E.", Done = () => itemPicked, MinSeconds = 2f });
            steps.Add(new Step { Line = "Si mantengo el clic derecho (o F), el Farol se aviva.", Done = () => lanternTime > 1f, MinSeconds = 2f });
            steps.Add(new Step { Line = "Mina me espera junto a la fuente. Sigo la flecha y mantengo E para hablar.", Done = () => GetQuestState("mina_brasa") != QuestState.Disponible, MinSeconds = 2f });
            steps.Add(new Step { Line = "Las luciérnagas se esconden donde no hay luz. Si las toco, recargan el Farol.", Done = () => fireflyCollected, MinSeconds = 3f });
            steps.Add(new Step { Line = "Cada barrio necesita luz. Si su Esperanza llega a cero, se apaga.", Done = () => stepTime > 6f, MinSeconds = 6f });
            steps.Add(new Step { Line = "Clic izquierdo: barrido de luz contra las sombras · Ctrl para esquivar · Tab: misiones.", Done = () => stepTime > 7f, MinSeconds = 7f });
            Show(0);
        }

        private static QuestState GetQuestState(string questId)
        {
            QuestRuntime q = QuestManager.Instance != null ? QuestManager.Instance.FindById(questId) : null;
            return q != null ? q.State : QuestState.Bloqueada;
        }

        private void Update()
        {
            if (index >= steps.Count)
            {
                if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
                return;
            }

            bool hidden = (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) || GameSession.IsModalOpen
                          || (NightManager.Instance != null && !NightManager.Instance.IsRunning);
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 1f, fadeSpeed * Time.unscaledDeltaTime);
            if (hidden) return;

            stepTime += Time.deltaTime;
            if (lantern != null && lantern.IsActive) lanternTime += Time.deltaTime;

            Step step = steps[index];
            if (stepTime >= step.MinSeconds && step.Done())
            {
                index++;
                if (index < steps.Count) Show(index);
                else GameProgress.TutorialPending = false;
            }
        }

        private void Show(int i)
        {
            stepTime = 0f;
            if (text != null) text.text = "<i>«" + steps[i].Line + "»</i>";
        }
    }
}
