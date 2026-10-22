using System.Collections;
using Ascua.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>Qué tipo de golpe conectó (define freeze-frame, sacudida y sonido).</summary>
    public enum HitWeight
    {
        /// <summary>Barrido normal (1º y 2º del combo, Brasa).</summary>
        Normal,
        /// <summary>Golpe final del combo, haz cargado, contraataque, Pulso, vela de Teo.</summary>
        Fuerte,
        /// <summary>El golpe que disuelve a la criatura.</summary>
        Disuelve
    }

    /// <summary>
    /// Impacto de cada golpe de luz que conecta:
    ///  - Freeze-frame: Time.timeScale = 0.05 durante 2-3 frames (0.035 / 0.06 / 0.085 s reales).
    ///  - Sacudida de cámara proporcional (0.12 / 0.25 / 0.38), solo si "Sacudidas de cámara" está activo.
    ///  - Destello de luz en el punto de impacto (escalado por "Reducir destellos").
    ///  - El sonido lo decide AudioManager según el evento (golpe / remate / disolución).
    /// El parpadeo de la criatura y las chispas los pone ShadowCreature + CombatFx.
    /// Nunca pisa una pausa ni una cámara lenta en curso (si timeScale &lt; 0.5 no congela).
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        public static HitFeedback Instance { get; private set; }

        [SerializeField] private CinemachineImpulseSource impulse;

        [Header("Freeze-frame (segundos reales)")]
        [SerializeField] private float freezeScale = 0.05f;
        [SerializeField] private float freezeNormal = 0.035f;
        [SerializeField] private float freezeStrong = 0.06f;
        [SerializeField] private float freezeDefeat = 0.085f;

        [Header("Sacudida")]
        [SerializeField] private float shakeNormal = 0.12f;
        [SerializeField] private float shakeStrong = 0.25f;
        [SerializeField] private float shakeDefeat = 0.38f;

        private Coroutine freeze;
        private float restoreScale = 1f;

        /// <summary>
        /// Velocidad "real" del juego ignorando un freeze-frame en curso. Quien guarde Time.timeScale
        /// para restaurarlo después (diálogo, pausa, Diario...) debe usar esto, o se quedaría en 0.05.
        /// </summary>
        public static float UnfrozenTimeScale => Instance != null && Instance.freeze != null ? Instance.restoreScale : Time.timeScale;

        /// <summary>Freeze-frames aplicados (pruebas).</summary>
        public int FreezeCount { get; private set; }
        public int ShakeCount { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (freeze != null && Time.timeScale > 0f && Mathf.Approximately(Time.timeScale, freezeScale)) Time.timeScale = restoreScale;
        }

        /// <summary>Un golpe de luz conectó en 'position'.</summary>
        public void Play(Vector3 position, HitWeight weight)
        {
            float seconds = weight == HitWeight.Disuelve ? freezeDefeat : weight == HitWeight.Fuerte ? freezeStrong : freezeNormal;
            float shake = weight == HitWeight.Disuelve ? shakeDefeat : weight == HitWeight.Fuerte ? shakeStrong : shakeNormal;

            Freeze(seconds);

            if (impulse != null && AccessibilitySettings.CameraShake)
            {
                impulse.GenerateImpulseWithForce(shake);
                ShakeCount++;
            }

            if (CombatFx.Instance != null && weight != HitWeight.Normal)
            {
                CombatFx.Instance.Flash(position, weight == HitWeight.Disuelve ? 2.5f : 1.6f, new Color(1f, 0.85f, 0.6f));
            }
        }

        private void Freeze(float realSeconds)
        {
            // Si ya hay pausa o cámara lenta (finisher, momento narrativo), no se congela encima.
            if (Time.timeScale < 0.5f && freeze == null) return;
            if (freeze != null) StopCoroutine(freeze);
            else restoreScale = Time.timeScale;
            freeze = StartCoroutine(FreezeRoutine(realSeconds));
        }

        private IEnumerator FreezeRoutine(float realSeconds)
        {
            FreezeCount++;
            Time.timeScale = freezeScale;
            yield return new WaitForSecondsRealtime(realSeconds);
            // Solo se restaura si nadie cambió el tiempo mientras tanto (pausa, cámara lenta...).
            if (Mathf.Approximately(Time.timeScale, freezeScale)) Time.timeScale = restoreScale;
            freeze = null;
        }
    }
}
