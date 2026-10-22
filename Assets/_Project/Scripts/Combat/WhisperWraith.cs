using Ascua.Core;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Susurro: rápido (5.5 m/s) y frágil (2 de vida: 1-2 golpes). No ataca: si toca a Ilse le "roba"
    /// la última página del Diario encontrada ESTA noche y huye con ella 10 s. Si se le disuelve antes,
    /// la página vuelve; si escapa, se pierde hasta la noche siguiente (volverá a su sitio).
    /// Si no hay nada que robar, se bebe 8 de energía y vuelve a acechar.
    /// </summary>
    public class WhisperWraith : ShadowCreature
    {
        private enum Phase { Hunt, Flee, Regroup }

        [Header("Susurro")]
        [SerializeField] private float huntSpeed = 5.5f;
        [SerializeField] private float fleeSpeed = 6.5f;
        [SerializeField] private float fleeSeconds = 10f;
        [SerializeField] private float energySteal = 8f;
        [Tooltip("Hoja que lleva consigo mientras huye con una página.")]
        [SerializeField] private GameObject carriedPage;
        [SerializeField] private Transform wisp;

        private Phase phase;
        private float phaseTimer;
        private string stolenPage;

        public bool IsCarryingPage => !string.IsNullOrEmpty(stolenPage);

        protected override void Awake()
        {
            base.Awake();
            typeId = "susurro";
        }

        protected override void OnSpawned()
        {
            phase = Phase.Hunt;
            phaseTimer = 0f;
            stolenPage = null;
            if (carriedPage != null) carriedPage.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();
            // Ondula como una voz en el aire.
            if (wisp != null) wisp.localPosition = new Vector3(Mathf.Sin(Time.time * 5f) * 0.08f, 0.9f + Mathf.Sin(Time.time * 3.3f) * 0.12f, 0f);
        }

        protected override void Tick(float dt)
        {
            phaseTimer += dt;
            switch (phase)
            {
                case Phase.Hunt:
                    ChasePlayer(huntSpeed, 0.2f);
                    if (FlatToPlayer().magnitude < contactRange + 0.2f) Touch();
                    break;

                case Phase.Flee:
                    FleeFromPlayer();
                    if (phaseTimer >= fleeSeconds) Escape();
                    break;

                case Phase.Regroup:
                    FleeFromPlayer();
                    if (phaseTimer >= 2f)
                    {
                        phase = Phase.Hunt;
                        phaseTimer = 0f;
                    }
                    break;
            }
        }

        private void Touch()
        {
            if (playerCombat == null) return;
            // El robo se esquiva o se contraataca como cualquier golpe (pero no resta energía si hay página).
            IncomingHitResult result = playerCombat.ReceiveHit(0.01f, transform.position, this);
            if (result != IncomingHitResult.Hit)
            {
                phase = Phase.Regroup;
                phaseTimer = 0f;
                return;
            }

            stolenPage = GameProgress.StealLatestPage();
            if (IsCarryingPage)
            {
                if (carriedPage != null) carriedPage.SetActive(true);
                Toast("Un Susurro te ha robado una página del Diario · ¡alcánzalo!");
                phase = Phase.Flee;
            }
            else
            {
                Lighting.PlayerEnergy energy = playerCombat.GetComponent<Lighting.PlayerEnergy>();
                if (energy != null) energy.ApplyDamage(energySteal);
                phase = Phase.Regroup;
            }
            phaseTimer = 0f;
        }

        private void FleeFromPlayer()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            Vector3 away = -FlatToPlayer().normalized;
            agent.speed = fleeSpeed;
            agent.stoppingDistance = 0f;
            if (!agent.pathPending && agent.remainingDistance < 1.5f)
            {
                agent.SetDestination(transform.position + away * 8f + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)));
            }
        }

        private void Escape()
        {
            if (IsCarryingPage) Toast("El Susurro se perdió en la niebla con tu página");
            stolenPage = null;
            Vanish();
        }

        protected override void OnDefeated(HitInfo hit)
        {
            if (!IsCarryingPage) return;
            GameProgress.RestorePage(stolenPage);
            stolenPage = null;
            if (carriedPage != null) carriedPage.SetActive(false);
            Toast("Recuperaste la página del Diario");
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.4f);
        }
    }
}
