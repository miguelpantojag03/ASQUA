using System.Collections;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.Core;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Momento memorable de una noche, diseñado a mano (no es una misión más).
    /// Noche 1: "Brasa vuelve". Al entregar la misión de Brasa, la cámara corta a Mina, aparecen
    /// bandas de cine, la música crece y Brasa (una ascua viva) sale de entre las luciérnagas y se
    /// queda revoloteando junto a Mina para el resto de la partida. Dura ~4 s y solo se ve una vez.
    /// Durante el momento el HUD se oculta y el tiempo va al 35 % (la noche no castiga por mirar).
    /// </summary>
    public class NightEvent : MonoBehaviour
    {
        [Header("Disparador")]
        [SerializeField] private string eventId = "noche1_brasa";
        [Tooltip("Misión cuya entrega dispara el momento.")]
        [SerializeField] private string triggerQuestId = "mina_brasa";
        [SerializeField] private string npcId = "mina";

        [Header("Puesta en escena")]
        [SerializeField] private CinemachineCamera momentCamera;
        [SerializeField] private BrasaSpark brasa;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;
        [SerializeField] private CanvasGroup captionGroup;
        [SerializeField] private TMP_Text caption;
        [SerializeField, TextArea] private string captionText = "Brasa ha vuelto a casa.";
        [SerializeField] private float duration = 4f;
        [SerializeField] private float barHeight = 110f;
        [Tooltip("CanvasGroup del HUD de juego: se oculta durante el momento.")]
        [SerializeField] private CanvasGroup hudGroup;
        [Tooltip("Velocidad del juego mientras dura el momento.")]
        [SerializeField, Range(0.05f, 1f)] private float slowMotion = 0.35f;
        [SerializeField] private float cameraDistance = 2.5f;
        [SerializeField] private float cameraAngle = 60f;

        private bool pending;
        private bool playing;

        public bool IsPlaying => playing;

        private void Awake()
        {
            if (momentCamera != null) momentCamera.gameObject.SetActive(false);
            if (brasa != null && !GameProgress.HasSeenMoment(eventId)) brasa.gameObject.SetActive(false);
            SetBars(0f);
            if (captionGroup != null) captionGroup.alpha = 0f;
        }

        private void Start()
        {
            // Si el momento ya ocurrió en una noche anterior, Brasa sigue junto a Mina.
            if (brasa != null && GameProgress.HasSeenMoment(eventId)) AttachBrasa();
        }

        private void OnEnable()
        {
            EventBus.QuestStateChanged += OnQuestStateChanged;
        }

        private void OnDisable()
        {
            EventBus.QuestStateChanged -= OnQuestStateChanged;
        }

        private void OnQuestStateChanged(string questId, string state)
        {
            if (questId == triggerQuestId && state == "Completada" && !GameProgress.HasSeenMoment(eventId)) pending = true;
        }

        private void Update()
        {
            if (!pending || playing) return;
            // Espera a que termine la conversación de entrega.
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return;
            if (NightManager.Instance != null && !NightManager.Instance.IsRunning) return;
            pending = false;
            GameProgress.MarkMomentSeen(eventId);
            StartCoroutine(Play());
        }

        /// <summary>Lanza el momento ya (pruebas, depuración).</summary>
        public void PlayNow()
        {
            if (playing) return;
            pending = false;
            GameProgress.MarkMomentSeen(eventId);
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            playing = true;
            EventBus.RaiseNarrativeMoment(eventId, true);

            PlayerInputReader input = FindFirstObjectByType<PlayerInputReader>();
            if (input != null) input.SetGameplayEnabled(false);

            CharacterAI npc = CharacterAI.Find(npcId);
            Transform focus = npc != null ? npc.transform : null;

            float previousTimeScale = Ascua.Combat.HitFeedback.UnfrozenTimeScale;
            Time.timeScale = previousTimeScale * slowMotion;

            // Corte seco al plano (y vuelta suave al final).
            CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            CinemachineBlendDefinition previousBlend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            // Plano: Mina de tres cuartos, a la altura de los ojos, desde el lado contrario a Ilse
            // (así el cuerpo de Ilse no tapa el encuadre).
            if (momentCamera != null && focus != null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                // Mina mira a Ilse al terminar de hablar: el plano parte de esa dirección (se le ve la cara)
                // y se abre 60° para que Ilse quede fuera de cuadro.
                Vector3 toPlayer = player != null ? player.transform.position - focus.position : focus.forward;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude < 0.01f) toPlayer = focus.forward;
                Vector3 side = Quaternion.Euler(0f, cameraAngle, 0f) * toPlayer.normalized;
                momentCamera.transform.position = focus.position + side * cameraDistance + Vector3.up * 1.1f;
                momentCamera.transform.LookAt(focus.position + Vector3.up * 0.85f);
                momentCamera.gameObject.SetActive(true);
            }

            // Brasa aparece desde lo alto y baja hasta Mina.
            if (brasa != null && focus != null)
            {
                brasa.transform.position = focus.position + Vector3.up * 4f + focus.forward * 1.5f;
                brasa.gameObject.SetActive(true);
                AttachBrasa();
            }

            if (caption != null) caption.text = captionText;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float inOut = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((duration - t) / 0.6f);
                SetBars(inOut);
                if (hudGroup != null) hudGroup.alpha = 1f - inOut;
                if (captionGroup != null) captionGroup.alpha = Mathf.Clamp01((t - 1f) / 0.6f) * Mathf.Clamp01((duration - 0.4f - t) / 0.6f);
                yield return null;
            }

            SetBars(0f);
            if (captionGroup != null) captionGroup.alpha = 0f;
            if (hudGroup != null) hudGroup.alpha = 1f;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);
            if (momentCamera != null) momentCamera.gameObject.SetActive(false);
            if (input != null) input.SetGameplayEnabled(true);
            // Si alguien pausó durante el momento, no se le pisa la pausa.
            if (Time.timeScale > 0f) Time.timeScale = previousTimeScale;

            playing = false;
            EventBus.RaiseNarrativeMoment(eventId, false);

            // Tras la vuelta suave se restaura la mezcla habitual de la cámara.
            yield return new WaitForSecondsRealtime(1f);
            if (brain != null) brain.DefaultBlend = previousBlend;
        }

        private void AttachBrasa()
        {
            NpcInteractable npc = NpcInteractable.Find(npcId);
            if (npc != null) brasa.SetTarget(npc.transform, new Vector3(0f, 1.25f, 0f));
            brasa.gameObject.SetActive(true);
        }

        private void SetBars(float amount)
        {
            if (topBar != null) topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, barHeight * amount);
            if (bottomBar != null) bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, barHeight * amount);
        }
    }
}
