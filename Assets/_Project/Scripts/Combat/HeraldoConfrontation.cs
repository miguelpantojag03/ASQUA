using Ascua.Characters;
using Ascua.Core;
using Ascua.Narrative;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Enfrentamiento con el Heraldo (opcional, solo en Persecución Activa). No es un jefe:
    ///  1. La luz no le hace daño: cada golpe lo hace retroceder, se deshace en bruma violeta y
    ///     reaparece a 5-7 m (nunca hay cuerpo a cuerpo directo; él tampoco ataca, solo apaga el Farol).
    ///  2. Tras reaparecer es intocable 1 s: hay que volver a acercarse leyendo dónde está.
    ///  3. Con 4 golpes en menos de 30 s se aleja 60 s y deja caer una línea de diálogo:
    ///     una pista sobre Ilse y el olvido (rota entre varias secuencias, una por enfrentamiento).
    ///  4. Si pasan 30 s sin golpearlo, la cuenta vuelve a 0.
    /// </summary>
    [RequireComponent(typeof(Heraldo_AI))]
    public class HeraldoConfrontation : MonoBehaviour, ILightTarget
    {
        [SerializeField] private int hitsToRepel = 4;
        [SerializeField] private float window = 30f;
        [SerializeField] private float untouchableAfterTeleport = 1f;
        [SerializeField] private float retreatSeconds = 60f;
        [SerializeField] private float teleportMin = 5f;
        [SerializeField] private float teleportMax = 7f;
        [Tooltip("Líneas que revela al alejarse (una por enfrentamiento, en orden).")]
        [SerializeField] private DialogueSequence[] revelations;
        [SerializeField] private Color mistColor = new Color(0.55f, 0.4f, 1f);

        private Heraldo_AI heraldo;
        private NpcInteractable interactable;
        private int hits;
        private float lastHitTime = -100f;
        private float untouchableUntil;
        private int completedCount;
        private static bool hintShown;

        public int Hits => hits;
        public int CompletedCount => completedCount;

        // ILightTarget
        public Vector3 TargetPosition => transform.position + Vector3.up * 1.1f;
        public bool CanBeHit => heraldo != null && heraldo.IsChasing && Time.time >= untouchableUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            hintShown = false;
        }

        private void Awake()
        {
            heraldo = GetComponent<Heraldo_AI>();
            interactable = GetComponent<NpcInteractable>();
        }

        private void OnEnable()
        {
            LightTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            LightTargetRegistry.Unregister(this);
        }

        private void Update()
        {
            if (hits > 0 && Time.time - lastHitTime > window) hits = 0;

            // La primera persecución explica que se le puede plantar cara (una sola vez).
            if (!hintShown && heraldo != null && heraldo.IsChasing && heraldo.DistanceToPlayer < 9f)
            {
                hintShown = true;
                Toast("El Heraldo te persigue · tu luz puede hacerle retroceder");
            }
        }

        public bool ReceiveLight(HitInfo hit)
        {
            if (!CanBeHit) return false;

            hits++;
            lastHitTime = Time.time;
            Vector3 from = TargetPosition;
            if (CombatFx.Instance != null) CombatFx.Instance.Dissolve(from, 1.2f, mistColor);
            EventBus.RaiseHeraldoConfronted(hits, hits >= hitsToRepel);

            if (hits >= hitsToRepel)
            {
                Complete();
                return true;
            }

            Vector3 center = PlayerCombatState.Instance != null ? PlayerCombatState.Instance.transform.position : transform.position;
            heraldo.TeleportAround(center, teleportMin, teleportMax);
            untouchableUntil = Time.time + untouchableAfterTeleport;
            if (hits == 1) Toast("El Heraldo retrocede ante tu luz…");
            return true;
        }

        private void Complete()
        {
            hits = 0;
            heraldo.RetreatFor(retreatSeconds);
            untouchableUntil = Time.time + retreatSeconds;

            DialogueSequence line = null;
            if (revelations != null && revelations.Length > 0) line = revelations[completedCount % revelations.Length];
            completedCount++;

            DialogueRunner runner = DialogueRunner.Instance;
            if (line != null && runner != null && !runner.IsOpen && PlayerCombatState.Instance != null)
            {
                runner.Play(line, interactable, null, PlayerCombatState.Instance.transform);
            }
            else
            {
                Toast("El Heraldo se aleja entre la niebla");
            }
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.6f);
        }
    }
}
