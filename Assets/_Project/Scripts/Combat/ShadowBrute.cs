using Ascua.Core;
using Ascua.World;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Sombra Mayor: grande y lenta (1.4 m/s), 7 de vida. Su ataque es una embestida muy telegrafiada:
    /// se agacha 0.8 s mientras una franja de luz violeta marca en el suelo su recorrido, y luego carga
    /// 7 m en línea recta. Si alcanza a Ilse resta 15 de energía. Tras la carga queda 1.3 s jadeando
    /// (ventana para el combo). La primera que cae suelta el Recuerdo "La lupa de Mina".
    /// Con el componente LitVulnerable es la "Sombra velada": invulnerable hasta ser iluminada 2 s.
    /// </summary>
    public class ShadowBrute : ShadowCreature
    {
        private enum Phase { Stalk, Telegraph, Charge, Recover }

        [Header("Sombra Mayor")]
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float chargeTriggerDistance = 6.5f;
        [SerializeField] private float telegraphTime = 0.8f;
        [SerializeField] private float chargeSpeed = 10f;
        [SerializeField] private float chargeDistance = 7f;
        [SerializeField] private float chargeDamage = 15f;
        [SerializeField] private float recoverTime = 1.3f;
        [SerializeField] private float chargeCooldown = 2.5f;

        [Header("Aviso visual")]
        [Tooltip("Franja en el suelo que marca la embestida (se estira durante el aviso).")]
        [SerializeField] private Transform telegraphStripe;
        [SerializeField] private Transform body;

        [Header("Botín")]
        [Tooltip("Recuerdo que suelta la primera Sombra Mayor derrotada (se recicla un PickupItem desactivado).")]
        [SerializeField] private PickupItem recuerdoDrop;
        [SerializeField] private string recuerdoId = LightAttack.LupaMinaId;

        private Phase phase;
        private float phaseTimer;
        private float cooldownTimer;
        private Vector3 chargeDirection;
        private float charged;
        private bool chargeHit;
        private Vector3 bodyBaseScale = Vector3.one;

        protected override void Awake()
        {
            base.Awake();
            if (body != null) bodyBaseScale = body.localScale;
            if (telegraphStripe != null) telegraphStripe.gameObject.SetActive(false);
        }

        protected override void OnSpawned()
        {
            phase = Phase.Stalk;
            phaseTimer = 0f;
            cooldownTimer = 1.5f;
            if (telegraphStripe != null) telegraphStripe.gameObject.SetActive(false);
            if (body != null) body.localScale = bodyBaseScale;
        }

        protected override void Tick(float dt)
        {
            phaseTimer += dt;
            if (cooldownTimer > 0f) cooldownTimer -= dt;
            float distance = FlatToPlayer().magnitude;

            switch (phase)
            {
                case Phase.Stalk:
                    ChasePlayer(walkSpeed, 1.2f);
                    TryContactDamage();
                    if (distance < chargeTriggerDistance && cooldownTimer <= 0f) StartTelegraph();
                    break;

                case Phase.Telegraph:
                    FacePlayer(dt, 6f);
                    // Se agacha y la franja crece: 0.8 s para leerlo y esquivar.
                    float t = Mathf.Clamp01(phaseTimer / telegraphTime);
                    if (body != null) body.localScale = new Vector3(bodyBaseScale.x * (1f + 0.12f * t), bodyBaseScale.y * (1f - 0.18f * t), bodyBaseScale.z * (1f + 0.12f * t));
                    if (telegraphStripe != null) telegraphStripe.localScale = new Vector3(1f, 1f, chargeDistance * t);
                    if (phaseTimer >= telegraphTime)
                    {
                        chargeDirection = transform.forward;
                        charged = 0f;
                        chargeHit = false;
                        phase = Phase.Charge;
                        phaseTimer = 0f;
                        if (body != null) body.localScale = bodyBaseScale;
                    }
                    break;

                case Phase.Charge:
                    float step = chargeSpeed * dt;
                    charged += step;
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(chargeDirection * step);
                    else transform.position += chargeDirection * step;

                    if (!chargeHit && player != null && playerCombat != null)
                    {
                        Vector3 delta = player.position - transform.position;
                        delta.y = 0f;
                        if (delta.magnitude < 1.3f)
                        {
                            chargeHit = true;
                            playerCombat.ReceiveHit(chargeDamage, transform.position - chargeDirection, this);
                        }
                    }
                    if (charged >= chargeDistance || phaseTimer > 1.2f) EndCharge();
                    break;

                case Phase.Recover:
                    if (phaseTimer >= recoverTime)
                    {
                        phase = Phase.Stalk;
                        phaseTimer = 0f;
                        if (agent != null && agent.enabled) agent.isStopped = false;
                    }
                    break;
            }
        }

        private void StartTelegraph()
        {
            phase = Phase.Telegraph;
            phaseTimer = 0f;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            if (telegraphStripe != null)
            {
                telegraphStripe.gameObject.SetActive(true);
                telegraphStripe.localScale = new Vector3(1f, 1f, 0.01f);
            }
            EventBus.RaiseAbilityUsed("embestida_aviso");
        }

        private void EndCharge()
        {
            phase = Phase.Recover;
            phaseTimer = 0f;
            cooldownTimer = chargeCooldown;
            if (telegraphStripe != null) telegraphStripe.gameObject.SetActive(false);
        }

        protected override void OnHit(HitInfo hit)
        {
            // El remate o el contraataque cortan el aviso: premia leer y golpear a tiempo.
            if (phase == Phase.Telegraph && hit.IsStrong)
            {
                EndCharge();
                stunTimer = Mathf.Max(stunTimer, 0.6f);
            }
        }

        protected override void OnDefeated(HitInfo hit)
        {
            if (telegraphStripe != null) telegraphStripe.gameObject.SetActive(false);
            if (recuerdoDrop != null && !string.IsNullOrEmpty(recuerdoId) && !GameProgress.HasRecuerdo(recuerdoId) && !recuerdoDrop.gameObject.activeSelf)
            {
                recuerdoDrop.ResetPickup(transform.position + Vector3.up * 0.8f);
            }
        }
    }
}
