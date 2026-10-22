using Ascua.Core;
using Ascua.Lighting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ascua.World
{
    /// <summary>
    /// Post-procesado dinámico sobre el Volume global (que queda limpio y "cinematográfico"):
    /// la aberración cromática ya NO está siempre puesta; solo aparece en momentos de urgencia:
    ///  - Energía del Farol bajo el 20 % (hasta 0.15).
    ///  - Ráfaga de la Penumbra (0.18) y oleada de sombras activa (0.08).
    ///  - Golpe recibido: un pico de 0.28 que se desvanece en ~0.6 s.
    /// Todo se multiplica por Accesibilidad > Reducir destellos (0.3).
    /// Usa un Volume propio (prioridad 5) con un perfil creado en memoria: no toca ningún asset.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class PostFxDirector : MonoBehaviour
    {
        [SerializeField] private PlayerEnergy energy;
        [SerializeField, Range(0f, 1f)] private float lowEnergyThreshold = 0.2f;
        [SerializeField] private float lowEnergyAberration = 0.15f;
        [SerializeField] private float surgeAberration = 0.18f;
        [SerializeField] private float waveAberration = 0.08f;
        [SerializeField] private float hurtAberration = 0.28f;
        [SerializeField] private float smoothing = 6f;

        private Volume volume;
        private ChromaticAberration aberration;
        private float hurtPulse;
        private bool surgeActive;
        private bool waveActive;
        private float current;

        private void Awake()
        {
            volume = GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            // .profile crea una copia en memoria (no modifica el asset del proyecto).
            VolumeProfile profile = volume.profile;
            if (!profile.TryGet(out aberration)) aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.overrideState = true;
            aberration.intensity.value = 0f;
            if (energy == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) energy = player.GetComponent<PlayerEnergy>();
            }
        }

        private void OnEnable()
        {
            EventBus.PlayerHurt += OnHurt;
            EventBus.PenumbraSurge += OnSurge;
            EventBus.CombatWave += OnWave;
        }

        private void OnDisable()
        {
            EventBus.PlayerHurt -= OnHurt;
            EventBus.PenumbraSurge -= OnSurge;
            EventBus.CombatWave -= OnWave;
        }

        private void OnHurt(float amount) => hurtPulse = 1f;
        private void OnSurge(bool started) => surgeActive = started;
        private void OnWave(string zoneId, bool started) => waveActive = started;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hurtPulse = Mathf.MoveTowards(hurtPulse, 0f, dt / 0.6f);

            float target = 0f;
            if (energy != null && energy.Normalized < lowEnergyThreshold)
            {
                target = Mathf.Max(target, lowEnergyAberration * (1f - energy.Normalized / lowEnergyThreshold));
            }
            if (surgeActive) target = Mathf.Max(target, surgeAberration);
            if (waveActive) target = Mathf.Max(target, waveAberration);
            target = Mathf.Max(target, hurtAberration * hurtPulse);
            target *= AccessibilitySettings.FlashScale;

            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * dt));
            if (aberration != null) aberration.intensity.value = current;
        }
    }
}
