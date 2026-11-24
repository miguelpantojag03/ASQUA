using System;
using System.Collections.Generic;
using Ascua.Audio;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Lighting;
using Ascua.UI;
using Ascua.World;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Jefe final "La Marea": una Sombra Mayor enorme (×2.5) y casi negra que sale en el centro de La Plaza
    /// al final de la Noche 5. No tiene modelo propio: FinalBossDirector clona una Sombra Mayor de la escena,
    /// le quita ShadowBrute y le pone esto (SetupFromClone).
    /// Reglas (los números viven en FinalBossRules, lógica pura testeable):
    ///  - Solo recibe daño completo si está iluminada por el Farol (o el Pulso); si no, daño mínimo.
    ///  - Fase 1 (100-66 %): golpe en área lento, con un aro en el suelo que se llena antes del impacto.
    ///  - Fase 2 (66-33 %): además invoca 2 Polillas cada 12 s (de las inactivas de la escena).
    ///  - Fase 3 (33-0 %): pulso que apaga las farolas cercanas (se reencienden con el Farol) y se acelera.
    /// Todo con Time.deltaTime (la pausa la congela) y sin destellos si "reducir destellos" está activo.
    /// </summary>
    public class MareaBoss : ShadowCreature
    {
        public const string TypeIdValue = "la_marea";
        /// <summary>"Zona" de La Marea y sus Polillas: nunca cuentan como oleada ni drenan Esperanza.</summary>
        public const string SpawnTag = "marea";
        public const string DisplayName = "La Marea";
        public const float BossScale = 2.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
        private static readonly Color DarkBody = new Color(0.03f, 0.04f, 0.08f, 1f);
        private static readonly Color DeepAccent = new Color(0.32f, 0.78f, 0.9f, 1f);
        private static readonly Color WarnColor = new Color(1f, 0.45f, 0.35f, 1f);
        private static readonly Color PulseColor = new Color(0.35f, 0.3f, 0.8f, 1f);
        private static Material warnMaterial;
        private static Material pulseMaterial;
        private static bool unlitHintShown;

        private enum Move { Stalk, SlamWindup, SlamRecover, PulseWindup, Roar }

        [Header("La Marea · movimiento")]
        [SerializeField] private float walkSpeed = 1.3f;
        [SerializeField] private float fastWalkSpeed = 2.2f;

        [Header("Golpe en área")]
        [SerializeField] private float slamTriggerDistance = 4.5f;
        [SerializeField] private float slamForward = 2.2f;
        [SerializeField] private float slamRadius = 3.6f;
        [SerializeField] private float slamDamage = 20f;
        [SerializeField] private float slamWindup = 1.5f;
        [SerializeField] private float slamWindupFast = 1.05f;
        [SerializeField] private float slamRecover = 1.3f;
        [SerializeField] private float slamCooldown = 3.5f;
        [SerializeField] private float slamCooldownFast = 2f;

        [Header("Fase 2 · Polillas")]
        [SerializeField] private float summonInterval = 12f;
        [SerializeField] private int summonCount = 2;
        [SerializeField] private int maxMoths = 4;

        [Header("Fase 3 · pulso")]
        [SerializeField] private float pulseInterval = 14f;
        [SerializeField] private float pulseWindup = 1.2f;
        [SerializeField] private float pulseRadius = 16f;

        [Header("Cambio de fase")]
        [SerializeField] private float roarSeconds = 1.2f;

        public event Action<int> PhaseChanged;

        private Move move;
        private float moveTimer;
        private float slamTimer;
        private float summonTimer;
        private float pulseTimer;
        private Vector3 slamCenter;
        private MaterialPropertyBlock tintBlock;
        private Transform slamOuter, slamInner, pulseRing;
        private PenumbraMoth[] mothPool;
        private readonly List<StreetLampToggle> snuffedLamps = new List<StreetLampToggle>();

        /// <summary>Fase actual (1-3).</summary>
        public int Phase { get; private set; } = 1;
        /// <summary>Vida restante (0-1), para la barra del HUD.</summary>
        public float Health01 => maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 0f;
        public int MaxHealth => maxHealth;
        /// <summary>True si el Farol (o el Pulso) la ilumina ahora: los golpes hacen daño completo.</summary>
        public bool IsLit { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            unlitHintShown = false;
        }

        /// <summary>
        /// Ajustes del clon de Sombra Mayor (lo llama FinalBossDirector tras AddComponent, con el objeto inactivo).
        /// Los valores van aquí y no en el inspector porque no hay prefab propio.
        /// </summary>
        public void SetupFromClone()
        {
            typeId = TypeIdValue;
            accentColor = DeepAccent;
            maxHealth = FinalBossRules.MaxHealth;
            energyDrop = 30f;
            contactDamage = 10f;
            contactRange = 2.2f;
            contactCooldown = 1.6f;
            contactWindup = 0.5f;
            hitSquash = 0.08f;
            knockbackResistance = 1f;
            centerHeight = 1.1f;

            // Cuerpo: los renderers con el shader de las sombras (como hace el constructor de escenas).
            List<Renderer> body = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Material shared = r.sharedMaterial;
                if (shared != null && shared.shader != null && shared.shader.name == "Ascua/SombraCriatura") body.Add(r);
            }
            bodyRenderers = body.ToArray();

            // La franja de la embestida de la Sombra Mayor no se usa.
            Transform stripe = transform.Find("Aviso_Embestida");
            if (stripe != null) stripe.gameObject.SetActive(false);

            transform.localScale *= BossScale;
        }

        protected override void Awake()
        {
            base.Awake();
            typeId = TypeIdValue;
            slamOuter = CreateRing("Marea_Aviso_Golpe", WarnMaterial());
            slamInner = CreateRing("Marea_Aviso_Relleno", WarnMaterial());
            pulseRing = CreateRing("Marea_Pulso", PulseMaterial());
            ApplyTint();
        }

        protected override void OnSpawned()
        {
            Phase = 1;
            SetMove(Move.Stalk);
            slamTimer = 2f;
            summonTimer = 0f;
            pulseTimer = 0f;
            IsLit = false;
            HideVisuals();
            ApplyTint();
            if (agent != null && agent.enabled)
            {
                agent.speed = walkSpeed;
                agent.isStopped = false;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            HideVisuals();
        }

        private void OnDestroy()
        {
            if (slamOuter != null) Destroy(slamOuter.gameObject);
            if (slamInner != null) Destroy(slamInner.gameObject);
            if (pulseRing != null) Destroy(pulseRing.gameObject);
        }

        protected override void Update()
        {
            base.Update();
            if (Time.deltaTime <= 0f || !IsAlive) return;
            IsLit = ComputeLit();
            UpdateVisuals();
        }

        // ------------------------------------------------------------------ comportamiento

        protected override void Tick(float dt)
        {
            moveTimer += dt;
            if (slamTimer > 0f) slamTimer -= dt;
            if (summonTimer > 0f) summonTimer -= dt;
            if (pulseTimer > 0f) pulseTimer -= dt;

            int phaseNow = FinalBossRules.PhaseFor(health, maxHealth);
            if (phaseNow > Phase && move != Move.SlamWindup && move != Move.PulseWindup)
            {
                EnterPhase(phaseNow);
                return;
            }

            float distance = FlatToPlayer().magnitude;
            switch (move)
            {
                case Move.Stalk:
                    ChasePlayer(Phase >= 3 ? fastWalkSpeed : walkSpeed, 1.8f);
                    TryContactDamage();
                    if (Phase >= 2 && summonTimer <= 0f)
                    {
                        Summon();
                        summonTimer = summonInterval;
                    }
                    if (Phase >= 3 && pulseTimer <= 0f)
                    {
                        StartPulse();
                        break;
                    }
                    if (slamTimer <= 0f && distance < slamTriggerDistance) StartSlam();
                    break;

                case Move.SlamWindup:
                    float windup = Phase >= 3 ? slamWindupFast : slamWindup;
                    float t = Mathf.Clamp01(moveTimer / windup);
                    // Gira hacia Ilse solo al principio: luego el golpe ya está decidido (se puede esquivar).
                    if (t < 0.35f)
                    {
                        FacePlayer(dt, 3f);
                        slamCenter = SlamCenter();
                    }
                    ShowWarning(t);
                    if (moveTimer >= windup) DoSlam();
                    break;

                case Move.SlamRecover:
                    // Ventana para castigar: queda quieta (y con la guardia baja) un momento.
                    if (moveTimer >= slamRecover) Resume();
                    break;

                case Move.PulseWindup:
                    ShowWarning(Mathf.Clamp01(moveTimer / pulseWindup));
                    if (moveTimer >= pulseWindup) DoPulse();
                    break;

                case Move.Roar:
                    if (moveTimer >= roarSeconds) Resume();
                    break;
            }
        }

        private void EnterPhase(int next)
        {
            Phase = next;
            Stop();
            SetMove(Move.Roar);
            if (next == 2)
            {
                summonTimer = 0.4f;
                Subtitle("[La Marea ruge: de su cuerpo salen polillas]");
            }
            else if (next >= 3)
            {
                pulseTimer = 0.3f;
                Subtitle("[La Marea se encrespa: la oscuridad se acelera]");
            }
            EventBus.RaiseAbilityUsed("marea_fase_" + next);
            PhaseChanged?.Invoke(next);
        }

        private void StartSlam()
        {
            Stop();
            slamCenter = SlamCenter();
            SetMove(Move.SlamWindup);
            EventBus.RaiseAbilityUsed("marea_golpe_aviso");
        }

        private Vector3 SlamCenter()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return transform.position + forward.normalized * slamForward;
        }

        private void DoSlam()
        {
            slamTimer = Phase >= 3 ? slamCooldownFast : slamCooldown;
            SetMove(Move.SlamRecover);
            if (CombatFx.Instance != null)
            {
                CombatFx.Instance.Impact(slamCenter + Vector3.up * 0.3f, true);
                CombatFx.Instance.Flash(slamCenter, 1.5f, WarnColor);
            }
            EventBus.RaiseAbilityUsed("marea_golpe");

            if (player == null || playerCombat == null) return;
            Vector3 toPlayer = player.position - slamCenter;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= slamRadius) playerCombat.ReceiveHit(slamDamage, slamCenter, this);
        }

        private void StartPulse()
        {
            Stop();
            SetMove(Move.PulseWindup);
            Subtitle("[La Marea toma aire…]");
        }

        /// <summary>Apaga y bloquea las farolas cercanas encendidas: el Farol de Ilse las vuelve a encender.</summary>
        private void DoPulse()
        {
            pulseTimer = pulseInterval;
            int count = 0;
            float r2 = pulseRadius * pulseRadius;
            foreach (StreetLampToggle lamp in FindObjectsByType<StreetLampToggle>(FindObjectsSortMode.None))
            {
                if (lamp == null || !lamp.IsOn || lamp.IsLocked) continue;
                Vector3 d = lamp.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                lamp.TurnOff();
                lamp.SetLocked(true);
                // Igual que con el Apagavelas: se reenciende iluminándola con el Farol.
                RelightOnLantern.Attach(lamp);
                if (!snuffedLamps.Contains(lamp)) snuffedLamps.Add(lamp);
                count++;
            }
            Subtitle(count > 0 ? "[Pulso de la Marea: se apagan las farolas]" : "[Pulso de la Marea]");
            if (count > 0 && ZoneLostNotification.Instance != null)
            {
                ZoneLostNotification.Instance.Show("La Marea apaga las farolas · vuelve a encenderlas con el Farol", 2.6f);
            }
            EventBus.RaiseAbilityUsed("marea_pulso");
            Resume();
        }

        /// <summary>Invoca Polillas de las inactivas de la escena (con su propio tag: no son oleada).</summary>
        private void Summon()
        {
            if (mothPool == null) mothPool = FindObjectsByType<PenumbraMoth>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            int alive = 0;
            for (int i = 0; i < mothPool.Length; i++)
            {
                PenumbraMoth m = mothPool[i];
                if (m != null && m.IsAlive && m.ZoneId == SpawnTag) alive++;
            }

            int spawned = 0;
            for (int i = 0; i < mothPool.Length && spawned < summonCount && alive < maxMoths; i++)
            {
                PenumbraMoth moth = mothPool[i];
                if (moth == null || moth.gameObject.activeSelf) continue;
                float angle = (spawned * 2f - 1f) * 60f + transform.eulerAngles.y;
                Vector3 side = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                moth.Spawn(transform.position + side * 2.5f + Vector3.up * 1.6f, SpawnTag);
                spawned++;
                alive++;
            }
            if (spawned > 0) Subtitle("[Aleteo: la Marea suelta polillas]");
        }

        /// <summary>Retira (sin botín) las Polillas que invocó.</summary>
        public void VanishSummons()
        {
            if (mothPool == null) return;
            for (int i = 0; i < mothPool.Length; i++)
            {
                PenumbraMoth m = mothPool[i];
                if (m != null && m.IsAlive && m.ZoneId == SpawnTag) m.Vanish();
            }
        }

        /// <summary>
        /// Deshace los apagones del pulso: desbloquea y enciende las farolas que apagó (salvo en zonas muertas).
        /// Se llama al reintentar el combate y al terminarlo.
        /// </summary>
        public void RestoreLamps()
        {
            foreach (StreetLampToggle lamp in snuffedLamps)
            {
                if (lamp == null) continue;
                RelightOnLantern relight = lamp.GetComponent<RelightOnLantern>();
                if (relight != null)
                {
                    relight.enabled = false;
                    Destroy(relight);
                }
                ZoneHope zone = ZoneHope.Find(lamp.ZoneId);
                if (zone != null && zone.IsDead) continue;
                lamp.SetLocked(false);
                lamp.TurnOn();
            }
            snuffedLamps.Clear();
        }

        private void Stop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void Resume()
        {
            SetMove(Move.Stalk);
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }

        private void SetMove(Move next)
        {
            move = next;
            moveTimer = 0f;
        }

        // ------------------------------------------------------------------ golpes recibidos

        /// <summary>La Marea siempre "acepta" el golpe: la luz decide cuánto daño hace (AdjustDamage).</summary>
        protected override bool AcceptsHit(HitInfo hit) => true;

        protected override int AdjustDamage(HitInfo hit)
        {
            // El Pulso es luz por sí mismo: cuenta como iluminada.
            bool lit = IsLit || hit.Kind == HitKind.Pulso;
            if (!lit && !unlitHintShown)
            {
                unlitHintShown = true;
                if (ZoneLostNotification.Instance != null)
                {
                    ZoneLostNotification.Instance.Show("La Marea solo se deshace bajo la luz · ilumínala con el Farol", 3f);
                }
            }
            return FinalBossRules.DamageFor(hit.Damage, lit);
        }

        protected override void OnHit(HitInfo hit)
        {
            // Es un jefe: los golpes apenas la aturden (el contraataque, un poco más).
            stunTimer = Mathf.Min(stunTimer, hit.Kind == HitKind.Contraataque ? 0.8f : 0.25f);
        }

        protected override void OnDefeated(HitInfo hit)
        {
            HideVisuals();
            VanishSummons();
        }

        private bool ComputeLit()
        {
            // Sin línea de visión: la plaza es abierta y el cuerpo es enorme (el centro queda alto).
            return IlluminationRegistry.IsPointIlluminated(TargetPosition, out _, false)
                || IlluminationRegistry.IsPointIlluminated(transform.position + Vector3.up * 1f, out _, false);
        }

        // ------------------------------------------------------------------ aspecto

        private void ApplyTint()
        {
            if (bodyRenderers == null) return;
            tintBlock ??= new MaterialPropertyBlock();
            foreach (Renderer r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(tintBlock);
                tintBlock.SetColor(BaseColorId, DarkBody);
                tintBlock.SetColor(AccentColorId, DeepAccent);
                r.SetPropertyBlock(tintBlock);
            }
        }

        private void UpdateVisuals()
        {
            bool slam = move == Move.SlamWindup;
            SetVisible(slamOuter, slam);
            SetVisible(slamInner, slam);
            if (slam)
            {
                float windup = Phase >= 3 ? slamWindupFast : slamWindup;
                float t = Mathf.Clamp01(moveTimer / windup);
                Vector3 ground = slamCenter + Vector3.up * 0.06f;
                // Con "reducir destellos" el aro no late: solo se llena.
                float beat = AccessibilitySettings.ReduceFlashes ? 1f : 1f + 0.04f * Mathf.Sin(Time.time * 18f);
                slamOuter.position = ground;
                slamOuter.localScale = Vector3.one * slamRadius * beat;
                slamInner.position = ground + Vector3.up * 0.01f;
                slamInner.localScale = Vector3.one * Mathf.Max(0.05f, slamRadius * t);
            }

            bool pulse = move == Move.PulseWindup;
            SetVisible(pulseRing, pulse);
            if (pulse)
            {
                float t = Mathf.Clamp01(moveTimer / pulseWindup);
                pulseRing.position = transform.position + Vector3.up * 0.08f;
                pulseRing.localScale = Vector3.one * Mathf.Lerp(1f, pulseRadius, t * t);
            }
        }

        private void HideVisuals()
        {
            SetVisible(slamOuter, false);
            SetVisible(slamInner, false);
            SetVisible(pulseRing, false);
        }

        private static void SetVisible(Transform t, bool visible)
        {
            if (t != null && t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
        }

        private static Transform CreateRing(string name, Material material)
        {
            GameObject ring = new GameObject(name);
            ring.AddComponent<MeshFilter>().sharedMesh = SafeHaven.SharedRingMesh();
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ring.SetActive(false);
            return ring.transform;
        }

        private static Material WarnMaterial()
        {
            if (warnMaterial == null) warnMaterial = UnlitMaterial("Marea_Aviso (runtime)", WarnColor * 1.8f);
            return warnMaterial;
        }

        private static Material PulseMaterial()
        {
            if (pulseMaterial == null) pulseMaterial = UnlitMaterial("Marea_Pulso (runtime)", PulseColor * 1.4f);
            return pulseMaterial;
        }

        private static Material UnlitMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader) { name = name };
            color.a = 1f;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            return material;
        }

        private static void Subtitle(string text)
        {
            // Mismo canal que el resto de sonidos narrativos: respeta la opción de subtítulos.
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(null, 0f, 1f, text);
        }
    }
}
