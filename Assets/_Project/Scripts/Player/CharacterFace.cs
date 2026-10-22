using Ascua.Core;
using Ascua.Lighting;
using Ascua.World;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>Estados del rostro de Ilse.</summary>
    public enum FaceMood
    {
        Normal,
        Alegre,
        Cansado
    }

    /// <summary>
    /// Rostro simbólico de Ilse: dos ojos de luz sin boca. Toda la expresión sale de su forma y brillo:
    ///  - Normal: brillo medio, ojos ovalados.
    ///  - Alegre (misión completada, luciérnaga, Vínculo que sube): más abiertos, brillantes y con un leve arco.
    ///  - Cansado (energía bajo 25%): entrecerrados, caídos hacia fuera y tenues.
    /// Parpadea al azar para que nunca se vea estática.
    /// </summary>
    public class CharacterFace : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Referencias")]
        [SerializeField] private Transform leftEye;
        [SerializeField] private Transform rightEye;
        [SerializeField] private PlayerEnergy energy;

        [Header("Color")]
        [SerializeField] private Color eyeColor = new Color(1f, 0.95f, 0.82f);

        [Header("Normal")]
        [SerializeField] private float normalIntensity = 1.8f;
        [SerializeField] private float normalOpen = 1f;

        [Header("Alegre")]
        [SerializeField] private float joyIntensity = 6f;
        [SerializeField] private float joyOpen = 1.35f;
        [Tooltip("Inclinación de los ojos (grados): arco alegre hacia arriba.")]
        [SerializeField] private float joyTilt = 16f;
        [SerializeField] private float joyDuration = 2.2f;

        [Header("Cansado")]
        [SerializeField] private float tiredIntensity = 0.9f;
        [SerializeField] private float tiredOpen = 0.42f;
        [SerializeField] private float tiredTilt = -10f;
        [SerializeField, Range(0f, 1f)] private float tiredEnergyThreshold = 0.25f;

        [Header("Parpadeo y transición")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 6f);
        [SerializeField] private float blinkDuration = 0.13f;
        [SerializeField] private float transitionSpeed = 7f;

        private Renderer leftRenderer;
        private Renderer rightRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 leftBaseScale;
        private Vector3 rightBaseScale;
        private Quaternion leftBaseRotation;
        private Quaternion rightBaseRotation;
        private float open = 1f;
        private float intensity = 1.8f;
        private float tilt;
        private float joyTimer;
        private float blinkTimer;
        private float blinkPhase = -1f;

        public FaceMood Mood { get; private set; } = FaceMood.Normal;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (leftEye != null)
            {
                leftRenderer = leftEye.GetComponent<Renderer>();
                leftBaseScale = leftEye.localScale;
                leftBaseRotation = leftEye.localRotation;
            }
            if (rightEye != null)
            {
                rightRenderer = rightEye.GetComponent<Renderer>();
                rightBaseScale = rightEye.localScale;
                rightBaseRotation = rightEye.localRotation;
            }
            if (energy == null) energy = GetComponentInParent<PlayerEnergy>();
            blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
            intensity = normalIntensity;
        }

        private void OnEnable()
        {
            EventBus.CelebrationRequested += OnCelebration;
            Firefly.AnyCollected += OnFirefly;
        }

        private void OnDisable()
        {
            EventBus.CelebrationRequested -= OnCelebration;
            Firefly.AnyCollected -= OnFirefly;
        }

        private void OnCelebration(string reason)
        {
            TriggerJoy(joyDuration);
        }

        private void OnFirefly(Firefly firefly)
        {
            TriggerJoy(1.2f);
        }

        /// <summary>Pone los ojos en estado Alegre durante unos segundos.</summary>
        public void TriggerJoy(float seconds)
        {
            joyTimer = Mathf.Max(joyTimer, seconds);
        }

        private void LateUpdate()
        {
            // Tiempo real: los ojos siguen vivos aunque la noche esté en pausa (diálogos).
            float dt = Time.unscaledDeltaTime;
            if (joyTimer > 0f) joyTimer -= dt;

            bool tired = energy != null && energy.Normalized < tiredEnergyThreshold;
            Mood = joyTimer > 0f ? FaceMood.Alegre : tired ? FaceMood.Cansado : FaceMood.Normal;

            float targetOpen = Mood == FaceMood.Alegre ? joyOpen : Mood == FaceMood.Cansado ? tiredOpen : normalOpen;
            float targetIntensity = Mood == FaceMood.Alegre ? joyIntensity : Mood == FaceMood.Cansado ? tiredIntensity : normalIntensity;
            float targetTilt = Mood == FaceMood.Alegre ? joyTilt : Mood == FaceMood.Cansado ? tiredTilt : 0f;

            float k = 1f - Mathf.Exp(-transitionSpeed * dt);
            open = Mathf.Lerp(open, targetOpen, k);
            intensity = Mathf.Lerp(intensity, targetIntensity, k);
            tilt = Mathf.Lerp(tilt, targetTilt, k);

            float blink = UpdateBlink(dt);
            ApplyEye(leftEye, leftRenderer, leftBaseScale, leftBaseRotation, open * blink, -tilt);
            ApplyEye(rightEye, rightRenderer, rightBaseScale, rightBaseRotation, open * blink, tilt);
        }

        /// <summary>Devuelve un factor 0-1 de apertura por parpadeo.</summary>
        private float UpdateBlink(float dt)
        {
            if (blinkPhase < 0f)
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f) blinkPhase = 0f;
                return 1f;
            }

            blinkPhase += dt / blinkDuration;
            if (blinkPhase >= 1f)
            {
                blinkPhase = -1f;
                // A veces, doble parpadeo: da sensación de pensamiento.
                blinkTimer = Random.value < 0.2f ? 0.18f : Random.Range(blinkInterval.x, blinkInterval.y);
                return 1f;
            }
            return Mathf.Lerp(1f, 0.08f, Mathf.Sin(blinkPhase * Mathf.PI));
        }

        private void ApplyEye(Transform eye, Renderer eyeRenderer, Vector3 baseScale, Quaternion baseRotation, float openness, float tiltDegrees)
        {
            if (eye == null) return;
            eye.localScale = new Vector3(baseScale.x * Mathf.Lerp(1.05f, 1f, openness), baseScale.y * openness, baseScale.z);
            eye.localRotation = baseRotation * Quaternion.Euler(0f, 0f, tiltDegrees);

            if (eyeRenderer == null) return;
            eyeRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
            propertyBlock.SetColor(EmissionColorId, eyeColor * intensity);
            eyeRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
