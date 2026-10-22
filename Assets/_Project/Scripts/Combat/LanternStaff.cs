using System.Collections;
using Ascua.Core;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Vara de luz: a media partida (al defender 3 oleadas) el Farol "evoluciona". Una vara de luz
    /// crece desde la mano bajo el Farol con un destello y una espiral de chispas: señal visible de
    /// progreso. Mismo barrido y misma hitbox, pero con un 45 % más de alcance.
    /// El estado se deduce de GameProgress.WavesDefended, así que se conserva entre noches.
    /// </summary>
    public class LanternStaff : MonoBehaviour
    {
        [SerializeField] private int wavesToUnlock = 3;
        [SerializeField] private float rangeMultiplier = 1.45f;

        [Header("Visual")]
        [Tooltip("Vara (raíz con escala 1 en Y cuando está completa).")]
        [SerializeField] private Transform staffVisual;
        [SerializeField] private ParticleSystem evolveParticles;
        [SerializeField] private Light evolveLight;
        [SerializeField] private float evolveSeconds = 1.2f;

        private bool unlocked;
        private Vector3 fullScale = Vector3.one;

        public bool IsUnlocked => unlocked;
        public float RangeMultiplier => unlocked ? rangeMultiplier : 1f;

        private void Awake()
        {
            if (staffVisual != null)
            {
                fullScale = staffVisual.localScale;
                staffVisual.gameObject.SetActive(false);
            }
            if (evolveLight != null) evolveLight.enabled = false;
        }

        private void Start()
        {
            // Ya desbloqueada en una noche anterior: aparece directamente, sin ceremonia.
            if (GameProgress.WavesDefended >= wavesToUnlock) SetUnlocked(false);
        }

        private void OnEnable()
        {
            EventBus.WaveCleared += OnWaveCleared;
        }

        private void OnDisable()
        {
            EventBus.WaveCleared -= OnWaveCleared;
        }

        private void OnWaveCleared(string zoneId, string rating)
        {
            if (!unlocked && GameProgress.WavesDefended >= wavesToUnlock) SetUnlocked(true);
        }

        /// <summary>Desbloquea ya la Vara (pruebas, depuración).</summary>
        public void UnlockNow()
        {
            if (!unlocked) SetUnlocked(true);
        }

        private void SetUnlocked(bool withCeremony)
        {
            unlocked = true;
            if (staffVisual != null) staffVisual.gameObject.SetActive(true);
            if (withCeremony) StartCoroutine(Evolve());
            else if (staffVisual != null) staffVisual.localScale = fullScale;
        }

        private IEnumerator Evolve()
        {
            if (evolveParticles != null) evolveParticles.Play();
            if (evolveLight != null) evolveLight.enabled = true;
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show("El Farol se ha hecho Vara de luz · su barrido llega más lejos", 3f);
            EventBus.RaiseAbilityUsed("vara_luz");
            EventBus.RaiseCelebration("vara");

            float t = 0f;
            while (t < evolveSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / evolveSeconds);
                float grow = 1f - Mathf.Pow(1f - k, 3f);
                if (staffVisual != null) staffVisual.localScale = new Vector3(fullScale.x, fullScale.y * grow, fullScale.z);
                if (evolveLight != null) evolveLight.intensity = Mathf.Sin(k * Mathf.PI) * 5f * AccessibilitySettings.FlashScale;
                yield return null;
            }

            if (staffVisual != null) staffVisual.localScale = fullScale;
            if (evolveLight != null) evolveLight.enabled = false;
            if (evolveParticles != null) evolveParticles.Stop();
        }
    }
}
