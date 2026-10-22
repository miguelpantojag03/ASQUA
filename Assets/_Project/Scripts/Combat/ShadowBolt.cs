using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Proyectil de oscuridad del Centinela. Viaja recto a 9 m/s durante 2.5 s.
    ///  - Si alcanza a Ilse resta 8 de energía (salvo esquiva perfecta o contraataque).
    ///  - Un barrido de luz a tiempo lo rompe (cuenta como golpe conectado del combo).
    ///  - Con el Reloj de Nadir, el contraataque lo devuelve contra el Centinela.
    ///  - Se deshace al chocar con un muro: las coberturas funcionan de verdad.
    /// </summary>
    public class ShadowBolt : MonoBehaviour, ILightTarget
    {
        [SerializeField] private float speed = 9f;
        [SerializeField] private float lifetime = 2.5f;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float hitRadius = 0.55f;
        [SerializeField] private LayerMask walls = 1;
        [SerializeField] private ParticleSystem trail;

        private Vector3 direction;
        private float age;
        private PenumbraSentinel owner;
        private bool reflected;
        private Transform player;
        private PlayerCombatState playerCombat;

        public Vector3 TargetPosition => transform.position;
        public bool CanBeHit => isActiveAndEnabled && !reflected;

        private void OnEnable()
        {
            LightTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            LightTargetRegistry.Unregister(this);
        }

        public void Launch(Vector3 origin, Vector3 dir, PenumbraSentinel from)
        {
            owner = from;
            reflected = false;
            direction = dir.normalized;
            age = 0f;
            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (playerCombat == null)
            {
                playerCombat = PlayerCombatState.Instance;
                if (playerCombat != null) player = playerCombat.transform;
            }
            gameObject.SetActive(true);
            if (trail != null) trail.Play();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            float step = speed * (reflected ? 1.4f : 1f) * dt;

            if (Physics.Raycast(transform.position, direction, step, walls, QueryTriggerInteraction.Ignore) || age > lifetime)
            {
                Dissipate();
                return;
            }
            transform.position += direction * step;

            if (reflected)
            {
                // Devuelto por el contraataque: busca al Centinela que lo disparó.
                if (owner != null && owner.IsAlive && (owner.TargetPosition - transform.position).sqrMagnitude < 0.8f)
                {
                    owner.ReceiveLight(new HitInfo(2, 0f, transform.position, HitKind.Contraataque, 1.5f));
                    Dissipate();
                }
                return;
            }

            if (player == null || playerCombat == null) return;
            Vector3 chest = player.position + Vector3.up * 1f;
            if ((chest - transform.position).sqrMagnitude > hitRadius * hitRadius) return;

            IncomingHitResult result = playerCombat.ReceiveHit(damage, transform.position);
            if (result == IncomingHitResult.Parried && owner != null && owner.IsAlive)
            {
                reflected = true;
                direction = (owner.TargetPosition - transform.position).normalized;
                return;
            }
            if (result != IncomingHitResult.Dodged) Dissipate();
        }

        public bool ReceiveLight(HitInfo hit)
        {
            if (!CanBeHit) return false;
            if (CombatFx.Instance != null) CombatFx.Instance.Impact(transform.position, false);
            Dissipate();
            return true;
        }

        private void Dissipate()
        {
            if (trail != null) trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
