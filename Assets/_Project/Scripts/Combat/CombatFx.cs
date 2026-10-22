using System.Collections;
using Ascua.Core;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Efectos compartidos del combate (un solo objeto en la escena, sin instanciar nada en juego):
    ///  - Disolución en luz de las criaturas (partículas ámbar que suben, nunca nada "realista").
    ///  - Chispas de impacto, destello de esquiva perfecta y de contraataque.
    ///  - Cámara lenta breve (finisher, esquiva perfecta), con prioridad sobre pausas y diálogos.
    /// Todos los destellos se escalan con Accesibilidad > Reducir destellos.
    /// </summary>
    public class CombatFx : MonoBehaviour
    {
        public static CombatFx Instance { get; private set; }

        [Header("Partículas (Emit manual, sin bucle)")]
        [SerializeField] private ParticleSystem dissolveBurst;
        [SerializeField] private ParticleSystem impactSparks;
        [SerializeField] private ParticleSystem dodgeRing;

        [Header("Destello")]
        [SerializeField] private Light flashLight;
        [SerializeField] private float flashDecay = 6f;

        [Header("Colores")]
        [SerializeField] private Color dissolveColor = new Color(1f, 0.82f, 0.5f);
        [SerializeField] private Color impactColor = new Color(1f, 0.9f, 0.7f);
        [SerializeField] private Color dodgeColor = new Color(0.75f, 0.85f, 1f);
        [SerializeField] private Color parryColor = new Color(1f, 0.95f, 0.75f);

        private float flashIntensity;
        private Coroutine slowMotion;

        private void Awake()
        {
            Instance = this;
            if (flashLight != null)
            {
                flashLight.intensity = 0f;
                flashLight.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Nunca dejar el juego a cámara lenta si se descarga la escena a mitad.
            if (slowMotion != null && Time.timeScale > 0f) Time.timeScale = 1f;
        }

        private void Update()
        {
            if (flashLight == null || flashIntensity <= 0f) return;
            flashIntensity = Mathf.MoveTowards(flashIntensity, 0f, flashDecay * flashIntensity * Time.unscaledDeltaTime + 0.01f);
            flashLight.intensity = flashIntensity;
            flashLight.enabled = flashIntensity > 0.01f;
        }

        /// <summary>La criatura se deshace en luz. 'scale' &gt; 1 para el último enemigo de una oleada.</summary>
        public void Dissolve(Vector3 position, float scale, Color tint)
        {
            Emit(dissolveBurst, position, Mathf.RoundToInt(26 * scale), Color.Lerp(dissolveColor, tint, 0.25f), scale);
            Flash(position, 2.2f * scale, dissolveColor);
        }

        public void Impact(Vector3 position, bool strong)
        {
            Emit(impactSparks, position, strong ? 16 : 8, impactColor, strong ? 1.3f : 1f);
            if (strong) Flash(position, 1.4f, impactColor);
        }

        public void PerfectDodge(Vector3 position)
        {
            Emit(dodgeRing, position + Vector3.up * 0.9f, 30, dodgeColor, 1f);
            Flash(position + Vector3.up, 3f, dodgeColor);
            SlowMotion(0.35f, 0.22f);
        }

        public void Parry(Vector3 position)
        {
            Emit(dodgeRing, position + Vector3.up * 0.9f, 36, parryColor, 1.2f);
            Flash(position + Vector3.up, 4f, parryColor);
            SlowMotion(0.3f, 0.25f);
        }

        /// <summary>Destello puntual de luz cálida en un punto.</summary>
        public void Flash(Vector3 position, float intensity, Color color)
        {
            if (flashLight == null) return;
            flashLight.transform.position = position + Vector3.up * 0.6f;
            flashLight.color = color;
            flashIntensity = Mathf.Max(flashIntensity, intensity * AccessibilitySettings.FlashScale);
            flashLight.intensity = flashIntensity;
            flashLight.enabled = true;
        }

        /// <summary>Cámara lenta breve (segundos REALES). No pisa una pausa (timeScale 0).</summary>
        public void SlowMotion(float scale, float realSeconds)
        {
            if (Time.timeScale <= 0f) return;
            if (slowMotion != null) StopCoroutine(slowMotion);
            slowMotion = StartCoroutine(SlowMotionRoutine(scale, realSeconds));
        }

        private IEnumerator SlowMotionRoutine(float scale, float realSeconds)
        {
            float previous = Time.timeScale;
            // Si ya había otra cámara lenta, se vuelve a la velocidad "normal" guardada en ella.
            if (previous < 1f && previous > 0f) previous = 1f;
            Time.timeScale = previous * scale;
            yield return new WaitForSecondsRealtime(realSeconds);
            // Si alguien pausó entretanto (menú, diálogo), no se toca su timeScale.
            if (Time.timeScale > 0f) Time.timeScale = previous;
            slowMotion = null;
        }

        private static void Emit(ParticleSystem system, Vector3 position, int count, Color color, float sizeScale)
        {
            if (system == null || count <= 0) return;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true,
                startColor = color,
                startSize = system.main.startSize.constantMax * sizeScale
            };
            system.Emit(emit, count);
        }
    }
}
