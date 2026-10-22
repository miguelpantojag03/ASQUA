using Ascua.Lighting;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Criatura "velada": la luz del barrido le resbala hasta que Ilse la mantiene bajo el Farol
    /// encendido 2 segundos seguidos. Entonces se revela (su silueta se solidifica y aparecen grietas
    /// ámbar) y durante 6 s recibe golpes normalmente; después vuelve a velarse.
    /// Une la mecánica central (iluminar) con el combate en vez de ser sistemas separados.
    /// Se añade a cualquier ShadowCreature (en el juego: la "Sombra velada", variante de Sombra Mayor).
    /// </summary>
    [RequireComponent(typeof(ShadowCreature))]
    public class LitVulnerable : MonoBehaviour
    {
        [SerializeField] private float revealSeconds = 2f;
        [SerializeField] private float revealedDuration = 6f;
        [Tooltip("Velocidad a la que pierde la revelación parcial si deja de estar iluminada.")]
        [SerializeField] private float decayPerSecond = 0.6f;

        private static bool hintShown;
        private ShadowCreature creature;
        private float charge;
        private float revealedTimer;

        public bool IsRevealed => revealedTimer > 0f;
        /// <summary>0-1: progreso de la iluminación (o 1 si ya está revelada).</summary>
        public float Reveal01 => IsRevealed ? 1f : charge / revealSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hintShown = false;
        }

        private void Awake()
        {
            creature = GetComponent<ShadowCreature>();
        }

        private void OnEnable()
        {
            charge = 0f;
            revealedTimer = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || creature == null || !creature.IsAlive) return;

            if (IsRevealed)
            {
                revealedTimer -= dt;
                if (revealedTimer <= 0f) charge = 0f;
            }
            else
            {
                // Solo cuenta la luz del Farol/Pulso encendidos (IlluminationRegistry).
                bool lit = IlluminationRegistry.IsPointIlluminated(creature.TargetPosition, out _);
                charge = lit ? charge + dt : Mathf.Max(0f, charge - decayPerSecond * dt);
                if (charge >= revealSeconds) revealedTimer = revealedDuration;
            }

            // Velada = casi transparente y sin grietas; revelada = sólida con grietas ámbar.
            creature.SetRevealValue(Mathf.Lerp(0.15f, 1f, Reveal01));
        }

        /// <summary>Un golpe le resbaló: la primera vez se explica cómo vencerla.</summary>
        public void NotifyDeflected()
        {
            if (hintShown) return;
            hintShown = true;
            if (ZoneLostNotification.Instance != null)
            {
                ZoneLostNotification.Instance.Show("Sombra velada · ilumínala con el Farol 2 s para poder herirla", 3f);
            }
        }
    }
}
