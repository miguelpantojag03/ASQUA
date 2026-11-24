using System;
using System.Collections.Generic;
using Ascua.Combat;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.World;
using TMPro;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Tutorial diegético: pensamientos de Ilse en pantalla, uno cada vez, que avanzan cuando el jugador
    /// hace lo que piden (TutorialSequence, lógica pura). Capítulos (TutorialRules):
    ///  - Noche 1 (solo la primera vez, GameProgress.TutorialPending): recoger, el Farol, hablar, luciérnagas, Esperanza.
    ///  - Noche 2 (solo la primera vez en la partida): esquivar, la luz contra las sombras, ataque cargado
    ///    (si ya tiene la Lupa de Mina) y contraataque (si ya tiene el Reloj de Nadir).
    /// Además, dos pensamientos "de una vez" en cualquier noche: la primera Cacería del Heraldo y el primer Apagavelas.
    /// "Saltar tutorial" (pantalla Cómo jugar, SaveSystem) lo apaga todo, también a mitad de un capítulo.
    /// </summary>
    public class TutorialHints : MonoBehaviour
    {
        /// <summary>Momentos (GameProgress) que marcan lo ya visto en la partida.</summary>
        public const string Night2ChapterId = "tutorial_noche2";
        public const string HuntHintId = "tutorial_caceria";
        public const string SnuffHintId = "tutorial_apagavelas";

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField] private float fadeSpeed = 3f;
        [Tooltip("Segundos que se ve un pensamiento de una vez (Cacería, Apagavelas).")]
        [SerializeField] private float oneShotSeconds = 5f;

        private class Step
        {
            public string Line;
            public Func<bool> Done;
            public float MinSeconds;
            /// <summary>Null = siempre disponible.</summary>
            public Func<bool> Available;
        }

        private readonly List<Step> steps = new List<Step>();
        private TutorialSequence sequence;
        private int chapter;
        private string shownLine;

        // Noche 1
        private bool fireflyCollected;
        private bool itemPicked;
        private float lanternTime;
        private LanternController lantern;

        // Noche 2: cuentan desde que se muestra el paso (se avanza al hacerlo, no antes).
        private bool dashed;
        private bool shadowHit;
        private bool chargedAttack;
        private bool parried;

        // Pensamientos de una vez
        private string oneShotLine;
        private float oneShotTimer;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        private void OnEnable()
        {
            Firefly.AnyCollected += OnFirefly;
            EventBus.ItemPickedUp += OnItem;
            EventBus.PlayerDashed += OnDashed;
            EventBus.ShadowHit += OnShadowHit;
            EventBus.ShadowDefeated += OnShadowDefeated;
            EventBus.PlayerAttacked += OnAttacked;
            EventBus.Parried += OnParried;
        }

        private void OnDisable()
        {
            Firefly.AnyCollected -= OnFirefly;
            EventBus.ItemPickedUp -= OnItem;
            EventBus.PlayerDashed -= OnDashed;
            EventBus.ShadowHit -= OnShadowHit;
            EventBus.ShadowDefeated -= OnShadowDefeated;
            EventBus.PlayerAttacked -= OnAttacked;
            EventBus.Parried -= OnParried;
        }

        private void OnFirefly(Firefly firefly) => fireflyCollected = true;
        private void OnItem(string id, string name) => itemPicked = true;
        private void OnDashed(Vector3 position) => dashed = true;
        private void OnShadowHit(Vector3 position, bool strong) => shadowHit = true;
        private void OnShadowDefeated(string type, Vector3 position) => shadowHit = true;
        private void OnParried(Vector3 position) => parried = true;

        private void OnAttacked(int step)
        {
            // 4 = ataque cargado (Lupa de Mina).
            if (step == 4) chargedAttack = true;
        }

        private void Start()
        {
            lantern = FindFirstObjectByType<LanternController>();
            chapter = TutorialRules.ChapterFor(GameProgress.CurrentNight, SaveSystem.SkipTutorial,
                GameProgress.TutorialPending, GameProgress.HasSeenMoment(Night2ChapterId));

            if (chapter == TutorialRules.Night1Chapter) BuildNight1();
            else if (chapter == TutorialRules.Night2Chapter) BuildNight2();
            if (steps.Count == 0) return;

            float[] minSeconds = new float[steps.Count];
            for (int i = 0; i < steps.Count; i++) minSeconds[i] = steps[i].MinSeconds;
            sequence = new TutorialSequence(minSeconds);
            sequence.Begin(IsAvailable);
            if (sequence.Finished) FinishChapter();
            else ShowStep(sequence.Index);
        }

        /// <summary>Noche 1: una frase, una acción.</summary>
        private void BuildNight1()
        {
            steps.Add(new Step { Line = "Algo brilla en el suelo, justo delante. Me acerco y pulso E.", Done = () => itemPicked, MinSeconds = 2f });
            steps.Add(new Step { Line = "Si mantengo el clic derecho (o F), el Farol se aviva.", Done = () => lanternTime > 1f, MinSeconds = 2f });
            steps.Add(new Step { Line = "Mina me espera junto a la fuente. Sigo la flecha y mantengo E para hablar.", Done = () => GetQuestState("mina_brasa") != QuestState.Disponible, MinSeconds = 2f });
            steps.Add(new Step { Line = "Las luciérnagas se esconden donde no hay luz. Si las toco, recargan el Farol.", Done = () => fireflyCollected, MinSeconds = 3f });
            steps.Add(new Step { Line = "Cada barrio necesita luz. Si su Esperanza llega a cero, se apaga.", Done = () => StepTime > 6f, MinSeconds = 6f });
            steps.Add(new Step { Line = "Clic izquierdo: barrido de luz contra las sombras · Ctrl para esquivar · Tab: misiones.", Done = () => StepTime > 7f, MinSeconds = 7f });
        }

        /// <summary>Noche 2: combate. Cada paso espera a que el jugador lo haga de verdad.</summary>
        private void BuildNight2()
        {
            steps.Add(new Step { Line = "Si algo se me echa encima, Ctrl (o B) me aparta. Justo a tiempo, ni me roza.", Done = () => dashed, MinSeconds = 2f });
            steps.Add(new Step { Line = "Mi luz deshace las sombras: clic izquierdo para barrer. A las veladas, antes las ilumino con el Farol.", Done = () => shadowHit, MinSeconds = 2f });
            steps.Add(new Step
            {
                Line = "Con la lupa de Mina: mantengo el clic izquierdo y lo suelto para un golpe cargado.",
                Done = () => chargedAttack, MinSeconds = 2f, Available = () => GameProgress.HasRecuerdo(LightAttack.LupaMinaId)
            });
            steps.Add(new Step
            {
                Line = "Con el reloj de Nadir: si ataco justo antes de que me alcancen, les devuelvo el golpe.",
                Done = () => parried, MinSeconds = 2f, Available = () => GameProgress.HasRecuerdo(PlayerCombatState.RelojNadirId)
            });
        }

        private bool IsAvailable(int index)
        {
            if (index < 0 || index >= steps.Count) return false;
            Func<bool> available = steps[index].Available;
            return available == null || available();
        }

        private float StepTime => sequence != null ? sequence.StepTime : 0f;

        private static QuestState GetQuestState(string questId)
        {
            QuestRuntime q = QuestManager.Instance != null ? QuestManager.Instance.FindById(questId) : null;
            return q != null ? q.State : QuestState.Bloqueada;
        }

        private void Update()
        {
            bool skip = SaveSystem.SkipTutorial;
            bool hidden = (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) || GameSession.IsModalOpen
                          || (NightManager.Instance != null && !NightManager.Instance.IsRunning);

            // "Saltar tutorial" a mitad de capítulo: se termina ya (y el de la Noche 1 cuenta como hecho).
            if (skip && sequence != null && !sequence.Finished)
            {
                sequence.Skip();
                FinishChapter();
            }

            if (!hidden && !skip && oneShotTimer <= 0f) CheckOneShots();

            bool showingOneShot = oneShotTimer > 0f;
            bool showingStep = sequence != null && !sequence.Finished;
            bool visible = !hidden && !skip && (showingOneShot || showingStep);
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (hidden || skip) return;

            float dt = Time.deltaTime;
            if (showingOneShot)
            {
                // El capítulo espera mientras se ve el pensamiento de una vez.
                oneShotTimer -= dt;
                if (oneShotTimer <= 0f && showingStep) SetLine(steps[sequence.Index].Line);
                return;
            }
            if (!showingStep) return;

            if (chapter == TutorialRules.Night1Chapter && lantern != null && lantern.IsActive) lanternTime += dt;

            if (!sequence.Tick(dt, steps[sequence.Index].Done, IsAvailable)) return;
            if (sequence.Finished) FinishChapter();
            else ShowStep(sequence.Index);
        }

        /// <summary>Primera Cacería del Heraldo y primer Apagavelas de la partida.</summary>
        private void CheckOneShots()
        {
            HeraldoHuntDirector hunts = HeraldoHuntDirector.Instance;
            bool hunting = hunts != null && hunts.IsBusy;
            if (TutorialRules.ShowOneShot(false, GameProgress.HasSeenMoment(HuntHintId), hunting))
            {
                GameProgress.MarkMomentSeen(HuntHintId);
                ShowOneShot("Me busca. Corro a un refugio de luz.");
                return;
            }

            SnuffDirector snuff = SnuffDirector.Instance;
            bool snuffing = snuff != null && snuff.ActiveCount > 0;
            if (TutorialRules.ShowOneShot(false, GameProgress.HasSeenMoment(SnuffHintId), snuffing))
            {
                GameProgress.MarkMomentSeen(SnuffHintId);
                ShowOneShot("Algo apaga las luces. Con el Farol las vuelvo a encender.");
            }
        }

        private void ShowOneShot(string line)
        {
            oneShotLine = line;
            oneShotTimer = oneShotSeconds;
            SetLine(oneShotLine);
        }

        private void ShowStep(int index)
        {
            // Las acciones del capítulo 2 cuentan desde que aparece su paso.
            dashed = false;
            shadowHit = false;
            chargedAttack = false;
            parried = false;
            if (oneShotTimer <= 0f) SetLine(steps[index].Line);
        }

        private void FinishChapter()
        {
            if (chapter == TutorialRules.Night1Chapter) GameProgress.TutorialPending = false;
            else if (chapter == TutorialRules.Night2Chapter) GameProgress.MarkMomentSeen(Night2ChapterId);
        }

        private void SetLine(string line)
        {
            if (line == shownLine || text == null) return;
            shownLine = line;
            text.text = "<i>«" + line + "»</i>";
        }
    }
}
