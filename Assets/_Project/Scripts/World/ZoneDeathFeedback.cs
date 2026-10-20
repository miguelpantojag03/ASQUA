using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// "Momento de impacto" al morir una zona: una sacudida suave de cámara (Cinemachine Impulse)
    /// y un sonido grave generado por código (no requiere archivos de audio).
    /// Escucha ZoneHope.AnyZoneDied; no conoce la UI (el aviso en pantalla es ZoneLostNotification).
    /// </summary>
    public class ZoneDeathFeedback : MonoBehaviour
    {
        [Header("Cámara")]
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [Tooltip("Fuerza del impulso. 0.4-0.8 = sutil; más de 1.5 ya marea.")]
        [SerializeField] private float impulseForce = 0.6f;

        [Header("Sonido")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
        [Tooltip("Duración del sonido generado (s).")]
        [SerializeField] private float soundDuration = 1.6f;

        private AudioClip deathClip;

        private void Awake()
        {
            if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            // Se genera una sola vez al cargar la escena: nada de memoria nueva durante el juego.
            deathClip = CreateDeathClip(soundDuration);
        }

        private void OnEnable()
        {
            ZoneHope.AnyZoneDied += OnZoneDied;
        }

        private void OnDisable()
        {
            ZoneHope.AnyZoneDied -= OnZoneDied;
        }

        private void OnDestroy()
        {
            if (deathClip != null) Destroy(deathClip);
        }

        private void OnZoneDied(ZoneHope zone)
        {
            // Accesibilidad: la sacudida se puede desactivar en el menú de pausa.
            if (impulseSource != null && Ascua.Core.AccessibilitySettings.CameraShake) impulseSource.GenerateImpulseWithForce(impulseForce);
            // Si hay AudioManager, el acorde grave lo pone él (en el grupo de Efectos y con subtítulo).
            if (Ascua.Audio.AudioManager.Instance == null && audioSource != null && deathClip != null) audioSource.PlayOneShot(deathClip, volume);
        }

        /// <summary>
        /// Golpe sordo que cae de tono (90 Hz a 38 Hz) más un soplo de ruido filtrado:
        /// suena a "algo grande que se apaga".
        /// </summary>
        private static AudioClip CreateDeathClip(float duration)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            System.Random rng = new System.Random(5);
            float phase = 0f;
            float lowpass = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float n = t / duration;

                float frequency = Mathf.Lerp(90f, 38f, Mathf.Sqrt(n));
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float attack = Mathf.Clamp01(t / 0.015f);
                float thump = Mathf.Sin(phase) * attack * Mathf.Exp(-3.2f * t);

                float white = (float)rng.NextDouble() * 2f - 1f;
                lowpass += (white - lowpass) * 0.035f;
                float breath = lowpass * 2.2f * Mathf.Sin(Mathf.PI * n) * (1f - n);

                data[i] = Mathf.Clamp((thump * 0.85f + breath * 0.5f), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ZonaApagada", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
