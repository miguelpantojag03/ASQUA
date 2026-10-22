using Ascua.Core;
using Ascua.World;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Centinela de Penumbra: obelisco estático. Si ve a Ilse (línea de visión libre, hasta 16 m)
    /// carga 0.6 s (su ojo crece y brilla) y dispara una ráfaga de 3 proyectiles de oscuridad.
    /// Se esquivan con el dash, se rompen con un barrido a tiempo o se evitan tras una cobertura
    /// (el quiosco, puestos, cajas): obliga a moverse entre coberturas o cerrar distancia rápido.
    /// 5 de vida. El primero que cae suelta el Recuerdo "El reloj de Nadir".
    /// </summary>
    public class PenumbraSentinel : ShadowCreature
    {
        private enum Phase { Watch, Charge, Burst, Cooldown }

        [Header("Centinela")]
        [SerializeField] private Transform eye;
        [SerializeField] private ShadowBolt[] bolts;
        [SerializeField] private float range = 16f;
        [SerializeField] private float chargeTime = 0.6f;
        [SerializeField] private int boltsPerBurst = 3;
        [SerializeField] private float boltInterval = 0.25f;
        [SerializeField] private float cooldown = 2.4f;
        [SerializeField] private LayerMask sightBlockers = 1;
        [Tooltip("Si es true se activa sola al empezar la escena (centinela fijo del mapa).")]
        [SerializeField] private bool placedInWorld = true;

        [Header("Botín")]
        [SerializeField] private PickupItem recuerdoDrop;
        [SerializeField] private string recuerdoId = PlayerCombatState.RelojNadirId;

        private Phase phase;
        private float phaseTimer;
        private int fired;
        private Vector3 eyeBaseScale = Vector3.one;

        protected override void Awake()
        {
            base.Awake();
            typeId = "centinela";
            if (eye != null) eyeBaseScale = eye.localScale;
        }

        private void Start()
        {
            // El centinela del mapa existe desde el principio de la noche.
            if (placedInWorld) Spawn(transform.position, null);
        }

        protected override void OnSpawned()
        {
            phase = Phase.Watch;
            phaseTimer = 0f;
            if (eye != null) eye.localScale = eyeBaseScale;
        }

        protected override void Update()
        {
            base.Update();
            if (eye != null) eye.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
        }

        protected override void Tick(float dt)
        {
            NightManager night = NightManager.Instance;
            if (night != null && !night.IsRunning) return;
            phaseTimer += dt;

            switch (phase)
            {
                case Phase.Watch:
                    if (CanSeePlayer()) SetPhase(Phase.Charge);
                    break;

                case Phase.Charge:
                    float t = Mathf.Clamp01(phaseTimer / chargeTime);
                    if (eye != null) eye.localScale = eyeBaseScale * (1f + 0.6f * t);
                    if (!CanSeePlayer())
                    {
                        // Ilse se puso a cubierto: aborta.
                        if (eye != null) eye.localScale = eyeBaseScale;
                        SetPhase(Phase.Watch);
                    }
                    else if (phaseTimer >= chargeTime)
                    {
                        fired = 0;
                        SetPhase(Phase.Burst);
                        FireBolt();
                    }
                    break;

                case Phase.Burst:
                    if (phaseTimer >= boltInterval)
                    {
                        phaseTimer = 0f;
                        if (fired < boltsPerBurst) FireBolt();
                        else
                        {
                            if (eye != null) eye.localScale = eyeBaseScale;
                            SetPhase(Phase.Cooldown);
                        }
                    }
                    break;

                case Phase.Cooldown:
                    if (phaseTimer >= cooldown) SetPhase(Phase.Watch);
                    break;
            }
        }

        private bool CanSeePlayer()
        {
            if (player == null || eye == null) return false;
            Vector3 target = player.position + Vector3.up * 1f;
            Vector3 delta = target - eye.position;
            float distance = delta.magnitude;
            if (distance > range) return false;
            return !Physics.Raycast(eye.position, delta / distance, distance - 0.3f, sightBlockers, QueryTriggerInteraction.Ignore);
        }

        private void FireBolt()
        {
            fired++;
            if (bolts == null || player == null) return;
            for (int i = 0; i < bolts.Length; i++)
            {
                ShadowBolt bolt = bolts[i];
                if (bolt == null || bolt.gameObject.activeSelf) continue;
                Vector3 target = player.position + Vector3.up * 1f;
                bolt.Launch(eye.position, (target - eye.position).normalized, this);
                EventBus.RaiseAbilityUsed("proyectil_sombra");
                return;
            }
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTimer = 0f;
        }

        protected override void OnDefeated(HitInfo hit)
        {
            if (recuerdoDrop != null && !string.IsNullOrEmpty(recuerdoId) && !GameProgress.HasRecuerdo(recuerdoId) && !recuerdoDrop.gameObject.activeSelf)
            {
                recuerdoDrop.ResetPickup(transform.position + transform.forward * 1.2f + Vector3.up * 0.8f);
            }
        }
    }
}
