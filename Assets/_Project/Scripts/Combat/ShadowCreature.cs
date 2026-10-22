using System;
using System.Collections;
using Ascua.Core;
using Ascua.Lighting;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.Combat
{
    /// <summary>
    /// Base de todas las criaturas de sombra (Polillas, Sombra Mayor, Susurros, Ecos, Centinelas).
    /// Reglas comunes:
    ///  - Sin barra de vida: cada golpe hace parpadear su silueta (shader Ascua/SombraCriatura, _FlashAmount).
    ///  - Empuje y aturdimiento según el golpe; el Pulso de luz las aturde 2.5 s.
    ///  - Al llegar a 0 se disuelven en luz (_Dissolve + partículas), nunca de forma "realista",
    ///    y devuelven energía al Farol de Ilse.
    ///  - Se reciclan (pool): Spawn() las reactiva, nada se instancia durante la partida.
    /// </summary>
    public abstract class ShadowCreature : MonoBehaviour, ILightTarget
    {
        private static readonly int FlashId = Shader.PropertyToID("_FlashAmount");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");

        [Header("Identidad")]
        [SerializeField] protected string typeId = "sombra";
        [SerializeField] protected Color accentColor = new Color(0.55f, 0.4f, 1f);

        [Header("Vida y botín")]
        [SerializeField] protected int maxHealth = 3;
        [Tooltip("Energía que devuelve al Farol al disolverse.")]
        [SerializeField] protected float energyDrop = 6f;

        [Header("Contacto")]
        [SerializeField] protected float contactDamage = 6f;
        [SerializeField] protected float contactRange = 0.9f;
        [SerializeField] protected float contactCooldown = 1.2f;

        [Header("Cuerpo")]
        [Tooltip("Altura del centro de la hitbox sobre el suelo.")]
        [SerializeField] protected float centerHeight = 0.9f;
        [SerializeField] protected Renderer[] bodyRenderers;
        [SerializeField] protected float knockbackResistance = 0f;

        public event Action<ShadowCreature> Defeated;

        protected int health;
        protected NavMeshAgent agent;
        protected Transform player;
        protected PlayerCombatState playerCombat;
        protected float stunTimer;
        protected bool dying;
        private float flash;
        private float contactTimer;
        private Vector3 knockVelocity;
        private float revealValue = 1f;
        private float dissolveValue;
        private float repathTimer;
        private LitVulnerable veil;
        private float baseEnergyDrop;
        private MaterialPropertyBlock propertyBlock;

        public string TypeId => typeId;
        public string ZoneId { get; private set; }
        public bool IsAlive => !dying && isActiveAndEnabled;
        public bool IsStunned => stunTimer > 0f;
        public int Health => health;

        // ILightTarget
        public Vector3 TargetPosition => transform.position + Vector3.up * centerHeight * transform.localScale.y;
        public virtual bool CanBeHit => IsAlive;

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            veil = GetComponent<LitVulnerable>();
            propertyBlock = new MaterialPropertyBlock();
            baseEnergyDrop = energyDrop;
        }

        protected virtual void OnEnable()
        {
            LightTargetRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            LightTargetRegistry.Unregister(this);
        }

        /// <summary>Activa la criatura en una posición (del pool). zoneId = zona que defiende la oleada.</summary>
        public virtual void Spawn(Vector3 position, string zoneId)
        {
            ZoneId = zoneId;
            health = maxHealth;
            dying = false;
            stunTimer = 0f;
            flash = 0f;
            knockVelocity = Vector3.zero;
            contactTimer = contactCooldown * 0.5f;
            dissolveValue = 1f;
            energyDrop = baseEnergyDrop;
            revealValue = veil != null ? 0.15f : 1f;
            gameObject.SetActive(true);

            if (agent != null)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
                else transform.position = position;
                agent.isStopped = false;
            }
            else
            {
                transform.position = position;
            }

            CachePlayer();
            StartCoroutine(Materialize());
            OnSpawned();
        }

        protected virtual void OnSpawned() { }

        private IEnumerator Materialize()
        {
            // Aparece desde la niebla: el umbral de disolución baja de 1 a 0.
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                dissolveValue = 1f - Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            dissolveValue = 0f;
        }

        protected void CachePlayer()
        {
            if (player != null) return;
            playerCombat = PlayerCombatState.Instance;
            if (playerCombat != null) player = playerCombat.transform;
            else
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    player = p.transform;
                    playerCombat = p.GetComponent<PlayerCombatState>();
                }
            }
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!dying)
            {
                if (stunTimer > 0f) stunTimer -= dt;
                if (contactTimer > 0f) contactTimer -= dt;
                if (repathTimer > 0f) repathTimer -= dt;

                // Empuje: se aplica unos frames y se frena.
                if (knockVelocity.sqrMagnitude > 0.0001f)
                {
                    Vector3 step = knockVelocity * dt;
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
                    else transform.position += step;
                    knockVelocity = Vector3.MoveTowards(knockVelocity, Vector3.zero, 22f * dt);
                }

                if (player != null && !IsStunned) Tick(dt);
            }

            flash = Mathf.MoveTowards(flash, 0f, dt * 6f);
            ApplyMaterial();
        }

        /// <summary>Comportamiento propio de cada criatura (no se llama aturdida ni muriendo).</summary>
        protected abstract void Tick(float dt);

        /// <summary>Daño de contacto con cooldown (cuerpo a cuerpo de las criaturas).</summary>
        protected bool TryContactDamage()
        {
            if (contactTimer > 0f || player == null || playerCombat == null) return false;
            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude > contactRange + 0.3f) return false;
            contactTimer = contactCooldown;
            IncomingHitResult result = playerCombat.ReceiveHit(contactDamage, transform.position, this);
            OnContactResult(result);
            return result == IncomingHitResult.Hit;
        }

        protected virtual void OnContactResult(IncomingHitResult result) { }

        // ------------------------------------------------------------------ Golpes recibidos

        public bool ReceiveLight(HitInfo hit)
        {
            if (!IsAlive) return false;

            if (!AcceptsHit(hit))
            {
                // Golpe "rebotado": la sombra ni se inmuta (LitVulnerable sin revelar).
                if (CombatFx.Instance != null) CombatFx.Instance.Impact(TargetPosition, false);
                OnDeflected(hit);
                return false;
            }

            flash = 1f;
            health -= Mathf.Max(1, hit.Damage);
            stunTimer = Mathf.Max(stunTimer, hit.StunSeconds);

            Vector3 away = transform.position - hit.SourcePosition;
            away.y = 0f;
            float push = hit.Knockback * (1f - knockbackResistance);
            if (away.sqrMagnitude > 0.0001f && push > 0f) knockVelocity = away.normalized * push * 4f;

            if (CombatFx.Instance != null) CombatFx.Instance.Impact(TargetPosition, hit.IsStrong);
            EventBus.RaiseShadowHit(TargetPosition, hit.IsStrong);

            OnHit(hit);
            if (health <= 0 && !dying) Die(hit);
            return true;
        }

        /// <summary>False = el golpe no afecta (p. ej. invulnerable hasta ser iluminada).</summary>
        /// <remarks>Por defecto: siempre, salvo que tenga LitVulnerable y no esté revelada.</remarks>
        protected virtual bool AcceptsHit(HitInfo hit) => veil == null || veil.IsRevealed;

        protected virtual void OnDeflected(HitInfo hit)
        {
            if (veil != null) veil.NotifyDeflected();
        }
        protected virtual void OnHit(HitInfo hit) { }

        /// <summary>Se deshace en luz. scale &gt; 1 = finisher (último enemigo de una oleada).</summary>
        protected virtual void Die(HitInfo hit)
        {
            dying = true;
            LightTargetRegistry.Unregister(this);
            if (agent != null && agent.enabled) agent.enabled = false;

            CachePlayer();
            if (playerCombat != null && energyDrop > 0f)
            {
                PlayerEnergy energy = playerCombat.GetComponent<PlayerEnergy>();
                if (energy != null) energy.Restore(energyDrop);
            }

            OnDefeated(hit);
            EventBus.RaiseShadowDefeated(typeId, TargetPosition);
            float scale = CombatWaveDirector.Instance != null && CombatWaveDirector.Instance.IsLastAlive(this) ? 2.2f : 1f;
            if (CombatFx.Instance != null) CombatFx.Instance.Dissolve(TargetPosition, scale, accentColor);
            Defeated?.Invoke(this);
            StartCoroutine(DissolveAndHide());
        }

        protected virtual void OnDefeated(HitInfo hit) { }

        /// <summary>Retirada sin derrota (fin de oleada fallida, Penumbra...): se desvanece sin botín.</summary>
        public void Vanish()
        {
            if (dying || !gameObject.activeSelf) return;
            dying = true;
            LightTargetRegistry.Unregister(this);
            if (agent != null && agent.enabled) agent.enabled = false;
            StartCoroutine(DissolveAndHide());
        }

        private IEnumerator DissolveAndHide()
        {
            float t = 0f;
            while (t < 0.55f)
            {
                t += Time.deltaTime;
                dissolveValue = Mathf.Clamp01(t / 0.55f);
                ApplyMaterial();
                yield return null;
            }
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Material

        /// <summary>1 = sombra visible y vulnerable; 0 = velada (lo usa LitVulnerable).</summary>
        public void SetRevealValue(float value)
        {
            revealValue = Mathf.Clamp01(value);
        }

        private void ApplyMaterial()
        {
            if (bodyRenderers == null) return;
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Renderer r = bodyRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
                propertyBlock.SetFloat(FlashId, flash * Mathf.Lerp(0.5f, 1f, AccessibilitySettings.FlashScale));
                propertyBlock.SetFloat(DissolveId, dissolveValue);
                propertyBlock.SetFloat(RevealId, revealValue);
                r.SetPropertyBlock(propertyBlock);
            }
        }

        // ------------------------------------------------------------------ Utilidades de movimiento

        protected Vector3 FlatToPlayer()
        {
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            return d;
        }

        protected void FacePlayer(float dt, float speed = 8f)
        {
            Vector3 d = FlatToPlayer();
            if (d.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), 1f - Mathf.Exp(-speed * dt));
        }

        protected void ChasePlayer(float speed, float stopDistance)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.speed = speed;
            agent.stoppingDistance = stopDistance;
            // Recalcular la ruta 4 veces por segundo basta y ahorra CPU con varias criaturas.
            if (repathTimer > 0f) return;
            repathTimer = 0.25f;
            agent.SetDestination(player.position);
        }
    }
}
