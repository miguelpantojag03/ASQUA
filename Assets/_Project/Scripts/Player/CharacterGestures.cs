using Ascua.Core;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Gestos de personalidad de Ilse, sin modelado nuevo:
    ///  - Respiración en reposo (el pecho y la capa se inflan levemente).
    ///  - Anticipación al empezar a caminar: estado "Arranque" del Animator + latigazo de capa.
    ///  - Celebración corta (EventBus): estado "Celebracion" del Animator + levantar el Farol.
    ///  - Estela de chispas del Farol al correr.
    /// </summary>
    public class CharacterGestures : MonoBehaviour
    {
        private static readonly int StartTrigger = Animator.StringToHash("Arrancar");
        private static readonly int CelebrateTrigger = Animator.StringToHash("Celebrar");
        private static readonly int FlickId = Shader.PropertyToID("_Flick");

        [Header("Referencias")]
        [SerializeField] private PlayerController controller;
        [SerializeField] private Animator animator;
        [Tooltip("Malla del torso (respira).")]
        [SerializeField] private Transform torso;
        [Tooltip("Capa (respira y da el latigazo).")]
        [SerializeField] private Transform cape;
        [SerializeField] private Renderer capeRenderer;
        [Tooltip("Pivote del Farol (se levanta al celebrar).")]
        [SerializeField] private Transform lanternPivot;
        [SerializeField] private ParticleSystem runTrail;

        [Header("Respiración")]
        [SerializeField] private float breathAmount = 0.03f;
        [SerializeField] private float breathSpeed = 1.6f;

        [Header("Anticipación")]
        [Tooltip("Segundos quieto antes de que el siguiente arranque haga el gesto.")]
        [SerializeField] private float idleBeforeAnticipation = 0.35f;
        [SerializeField] private float flickDecay = 2.2f;

        [Header("Celebración")]
        [SerializeField] private float lanternRaiseHeight = 0.38f;
        [SerializeField] private float celebrationDuration = 0.9f;

        [Header("Estela al correr")]
        [SerializeField] private float trailRate = 9f;

        private Vector3 torsoBaseScale = Vector3.one;
        private Vector3 capeBaseScale = Vector3.one;
        private Vector3 lanternBasePosition;
        private MaterialPropertyBlock propertyBlock;
        private float idleTime;
        private bool wasMoving;
        private float flick;
        private float celebrationTimer;
        private float breathPhase;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (torso != null) torsoBaseScale = torso.localScale;
            if (cape != null) capeBaseScale = cape.localScale;
            if (lanternPivot != null) lanternBasePosition = lanternPivot.localPosition;
        }

        private void OnEnable()
        {
            EventBus.CelebrationRequested += OnCelebration;
        }

        private void OnDisable()
        {
            EventBus.CelebrationRequested -= OnCelebration;
        }

        private void OnCelebration(string reason)
        {
            celebrationTimer = celebrationDuration;
            if (animator != null) animator.SetTrigger(CelebrateTrigger);
        }

        private void Update()
        {
            if (controller == null) return;
            float dt = Time.deltaTime;

            // --- Anticipación al arrancar
            bool moving = controller.CurrentSpeed > 0.15f;
            if (!moving) idleTime += dt;
            if (moving && !wasMoving && idleTime >= idleBeforeAnticipation)
            {
                if (animator != null) animator.SetTrigger(StartTrigger);
                flick = 1f;
            }
            if (moving) idleTime = 0f;
            wasMoving = moving;

            // --- Estela de chispas al correr
            if (runTrail != null)
            {
                bool running = controller.CurrentSpeed > controller.WalkSpeed + 0.6f;
                ParticleSystem.EmissionModule emission = runTrail.emission;
                emission.rateOverTime = running ? trailRate : 0f;
            }
        }

        private void LateUpdate()
        {
            // Tiempo real: Ilse respira también durante los diálogos (noche en pausa).
            float dt = Time.unscaledDeltaTime;

            // --- Respiración (más marcada cuanto más quieta está)
            float stillness = controller != null ? 1f - Mathf.Clamp01(controller.CurrentSpeed / Mathf.Max(0.1f, controller.WalkSpeed)) : 1f;
            breathPhase += dt * breathSpeed * Mathf.PI * 2f * 0.5f;
            float breath = Mathf.Sin(breathPhase) * breathAmount * Mathf.Lerp(0.35f, 1f, stillness);
            if (torso != null) torso.localScale = new Vector3(torsoBaseScale.x * (1f + breath), torsoBaseScale.y * (1f + breath * 0.5f), torsoBaseScale.z * (1f + breath));
            if (cape != null) cape.localScale = new Vector3(capeBaseScale.x * (1f + breath * 0.8f), capeBaseScale.y, capeBaseScale.z * (1f + breath * 0.8f));

            // --- Latigazo de capa (decae solo)
            if (flick > 0f) flick = Mathf.Max(0f, flick - flickDecay * dt);
            if (capeRenderer != null)
            {
                capeRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
                propertyBlock.SetFloat(FlickId, flick);
                capeRenderer.SetPropertyBlock(propertyBlock);
            }

            // --- Levantar el Farol al celebrar
            if (lanternPivot != null)
            {
                float lift = 0f;
                if (celebrationTimer > 0f)
                {
                    celebrationTimer -= dt;
                    float t = 1f - Mathf.Clamp01(celebrationTimer / celebrationDuration);
                    lift = Mathf.Sin(t * Mathf.PI) * lanternRaiseHeight;
                }
                lanternPivot.localPosition = lanternBasePosition + Vector3.up * lift;
            }
        }
    }
}
