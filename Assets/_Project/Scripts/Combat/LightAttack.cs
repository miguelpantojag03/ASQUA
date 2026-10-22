using System;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Player;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Barrido de luz (clic izquierdo / gatillo derecho). El Farol no es un arma física: cada golpe
    /// es un arco de luz frente a Ilse (shader Ascua/BarridoLuz) con una hitbox esférica.
    ///  - Combo de 3: 1 + 1 + 2 de daño; el 3º (Remate) empuja más y divide a los Ecos.
    ///    Ventana para encadenar: 0.45 s tras cada barrido. Entrada con búfer de 0.25 s.
    ///  - Cuesta 4 de energía por barrido: el combate gasta el mismo recurso que iluminar.
    ///  - Cancelable con la esquiva en cualquier momento.
    ///  - Apuntado suave: gira hacia la sombra más cercana dentro de un cono de 75°.
    ///  - Recuerdo "Lupa de Mina": mantener 0.55 s y soltar = haz cargado de 5 m (3 de daño).
    ///  - Vara de luz (LanternStaff): mismo golpe con un 45 % más de alcance.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightAttack : MonoBehaviour
    {
        public const string LupaMinaId = "lupa_mina";

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int[] AttackTriggers =
        {
            Animator.StringToHash("Ataque1"), Animator.StringToHash("Ataque2"), Animator.StringToHash("Ataque3")
        };

        [Header("Referencias")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerEnergy energy;
        [SerializeField] private PlayerCombatState combatState;
        [SerializeField] private LanternStaff staff;
        [SerializeField] private Animator animator;
        [Tooltip("Pivote del arco de luz (a la altura del pecho).")]
        [SerializeField] private Transform sweepPivot;
        [SerializeField] private Renderer sweepRenderer;
        [Tooltip("Haz recto del ataque cargado.")]
        [SerializeField] private Renderer beamRenderer;
        [SerializeField] private Light flashLight;
        [SerializeField] private ParticleSystem chargeParticles;

        [Header("Combo")]
        [SerializeField] private float energyCost = 4f;
        [SerializeField] private float swingDuration = 0.34f;
        [Tooltip("Momento del barrido (s) en que la luz golpea.")]
        [SerializeField] private float hitTime = 0.09f;
        [SerializeField] private float comboWindow = 0.45f;
        [SerializeField] private float inputBuffer = 0.25f;
        [SerializeField] private int[] damage = { 1, 1, 2 };
        [SerializeField] private float[] knockback = { 1.2f, 1.2f, 3.5f };
        [SerializeField] private float hitReach = 1.05f;
        [SerializeField] private float hitRadius = 1.15f;
        [SerializeField] private float lungeDistance = 0.35f;

        [Header("Apuntado suave")]
        [SerializeField] private float aimRange = 3.6f;
        [SerializeField] private float aimHalfAngle = 75f;

        [Header("Ataque cargado (Lupa de Mina)")]
        [SerializeField] private float chargeTime = 0.55f;
        [SerializeField] private float chargedCost = 10f;
        [SerializeField] private int chargedDamage = 3;
        [SerializeField] private float chargedLength = 5f;
        [SerializeField] private float chargedWidth = 0.9f;

        [Header("Visual")]
        [SerializeField] private float sweepVisualTime = 0.16f;
        [SerializeField] private float fadeTime = 0.14f;
        [SerializeField] private float flashIntensity = 3f;

        /// <summary>(paso del combo 1-3, golpes conectados en este combo)</summary>
        public event Action<int, int> ComboChanged;
        public event Action Failed;

        private MaterialPropertyBlock propertyBlock;
        private bool attacking;
        private float swingTimer;
        private bool hitDone;
        private int comboStep;
        private int connectedHits;
        private float comboTimer;
        private float bufferTimer;
        private float heldTime;
        private bool charging;
        private float visualTimer = -1f;
        private float beamTimer = -1f;

        public bool IsAttacking => attacking;
        public int ComboStep => comboStep;
        public int ConnectedHits => connectedHits;
        public bool HasChargedAttack => GameProgress.HasRecuerdo(LupaMinaId);
        private float RangeMultiplier => staff != null ? staff.RangeMultiplier : 1f;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (energy == null) energy = GetComponent<PlayerEnergy>();
            if (combatState == null) combatState = GetComponent<PlayerCombatState>();
            if (staff == null) staff = GetComponent<LanternStaff>();
            propertyBlock = new MaterialPropertyBlock();
            if (sweepRenderer != null) sweepRenderer.enabled = false;
            if (beamRenderer != null) beamRenderer.enabled = false;
            if (flashLight != null) flashLight.enabled = false;
        }

        private void OnEnable()
        {
            EventBus.PlayerDashed += OnDashed;
        }

        private void OnDisable()
        {
            EventBus.PlayerDashed -= OnDashed;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ReadInput(dt);

            if (attacking)
            {
                swingTimer += dt;
                if (!hitDone && swingTimer >= hitTime)
                {
                    hitDone = true;
                    DoSweepHit();
                }
                if (swingTimer >= swingDuration)
                {
                    attacking = false;
                    comboTimer = comboWindow;
                }
            }
            else if (comboTimer > 0f)
            {
                comboTimer -= dt;
                if (comboTimer <= 0f) ResetCombo();
            }

            if (bufferTimer > 0f)
            {
                bufferTimer -= dt;
                if (!attacking && TryStartSwing()) bufferTimer = 0f;
            }

            UpdateVisuals(Time.unscaledDeltaTime);
        }

        private void ReadInput(float dt)
        {
            if (input == null) return;

            if (input.AttackPressed)
            {
                if (combatState != null) combatState.NotifyAttackPressed();
                bufferTimer = inputBuffer;
            }

            // Carga (solo con la Lupa de Mina): se acumula mientras se mantiene el botón.
            if (HasChargedAttack && input.AttackHeld)
            {
                heldTime += dt;
                bool nowCharging = heldTime >= chargeTime;
                if (nowCharging && !charging && chargeParticles != null) chargeParticles.Play();
                charging = nowCharging;
            }
            if (input.AttackReleased || (!input.AttackHeld && heldTime > 0f))
            {
                if (charging) TryChargedAttack();
                heldTime = 0f;
                charging = false;
                if (chargeParticles != null) chargeParticles.Stop();
            }
        }

        /// <summary>Lanza el siguiente barrido del combo si es posible (lo usan la entrada y las pruebas).</summary>
        public bool TryStartSwing()
        {
            if (attacking || controller == null || controller.IsMantling) return false;
            if (combatState != null && !combatState.CanFight) return Fail();
            if (energy == null || !energy.Consume(energyCost)) return Fail();

            comboStep = comboTimer > 0f && comboStep < 3 ? comboStep + 1 : 1;
            if (comboStep == 1) connectedHits = 0;
            comboTimer = 0f;
            attacking = true;
            swingTimer = 0f;
            hitDone = false;

            AimAssist();
            if (!controller.IsBusy) controller.Dash(transform.forward, lungeDistance * (comboStep == 3 ? 1.6f : 1f), 0.1f);
            if (animator != null) animator.SetTrigger(AttackTriggers[comboStep - 1]);

            StartSweepVisual(comboStep);
            EventBus.RaisePlayerAttacked(comboStep);
            ComboChanged?.Invoke(comboStep, connectedHits);
            return true;
        }

        /// <summary>Haz cargado recto (Recuerdo: Lupa de Mina).</summary>
        public bool TryChargedAttack()
        {
            if (!HasChargedAttack || controller == null || controller.IsMantling) return false;
            if (combatState != null && !combatState.CanFight) return Fail();
            if (energy == null || !energy.Consume(chargedCost)) return Fail();

            AimAssist(chargedLength * RangeMultiplier, 40f);
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            Vector3 forward = transform.forward;
            float length = chargedLength * RangeMultiplier;
            bool connected = false;

            // Cápsula a lo largo del haz: se comprueba la distancia de cada objetivo al segmento.
            var targets = LightTargetRegistry.InSphere(origin + forward * (length * 0.5f), length * 0.5f + chargedWidth);
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                ILightTarget t = targets[i];
                Vector3 to = t.TargetPosition - origin;
                float along = Vector3.Dot(to, forward);
                if (along < 0f || along > length) continue;
                Vector3 closest = origin + forward * along;
                Vector3 flatDelta = t.TargetPosition - closest;
                flatDelta.y *= 0.5f;
                if (flatDelta.magnitude > chargedWidth + 0.4f) continue;
                if (t.ReceiveLight(new HitInfo(chargedDamage, 4f, transform.position, HitKind.Cargado, 0.6f))) connected = true;
            }

            beamTimer = 0f;
            if (beamRenderer != null)
            {
                beamRenderer.transform.localScale = new Vector3(chargedWidth * 1.4f, 1f, length);
                beamRenderer.enabled = true;
            }
            Flash(1.6f);
            if (animator != null) animator.SetTrigger(AttackTriggers[2]);
            if (connected && CombatFx.Instance != null) CombatFx.Instance.SlowMotion(0.5f, 0.08f);
            EventBus.RaisePlayerAttacked(4);
            return true;
        }

        private bool Fail()
        {
            Failed?.Invoke();
            return false;
        }

        private void OnDashed(Vector3 position)
        {
            // La esquiva cancela el barrido en curso (la luz se deshace al instante).
            if (!attacking) return;
            attacking = false;
            comboTimer = comboWindow;
            visualTimer = Mathf.Max(visualTimer, sweepVisualTime);
        }

        private void ResetCombo()
        {
            comboStep = 0;
            connectedHits = 0;
            ComboChanged?.Invoke(0, 0);
        }

        private void AimAssist(float range = -1f, float halfAngle = -1f)
        {
            float r = range > 0f ? range : aimRange * RangeMultiplier;
            float a = halfAngle > 0f ? halfAngle : aimHalfAngle;
            Vector3 forward = controller != null && controller.DesiredDirection.sqrMagnitude > 0.01f ? controller.DesiredDirection : transform.forward;
            // Con un objetivo fijado (TargetLock), se apunta a él aunque esté fuera del cono normal
            // (hasta 2.2 veces el alcance de apuntado: no se teletransporta el golpe, solo se gira).
            ILightTarget target = null;
            TargetLock targetLock = TargetLock.Instance;
            if (targetLock != null && targetLock.HasTarget)
            {
                Vector3 toLocked = targetLock.Current.TargetPosition - transform.position;
                toLocked.y = 0f;
                if (toLocked.magnitude <= r * 2.2f) target = targetLock.Current;
            }
            if (target == null) target = LightTargetRegistry.NearestInCone(transform.position, forward, r, a);
            Vector3 look = target != null ? target.TargetPosition - transform.position : forward;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private void DoSweepHit()
        {
            int index = Mathf.Clamp(comboStep - 1, 0, 2);
            float mul = RangeMultiplier;
            Vector3 center = transform.position + transform.forward * (hitReach * mul) + Vector3.up * 0.9f;
            HitKind kind = comboStep == 3 ? HitKind.Remate : HitKind.Barrido;
            HitInfo hit = new HitInfo(damage[index], knockback[index], transform.position, kind, comboStep == 3 ? 0.35f : 0.12f);

            bool connected = false;
            var targets = LightTargetRegistry.InSphere(center, hitRadius * mul);
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i].ReceiveLight(hit)) connected = true;
            }

            if (connected)
            {
                connectedHits = Mathf.Min(3, connectedHits + 1);
                Flash(comboStep == 3 ? 1.3f : 0.8f);
                ComboChanged?.Invoke(comboStep, connectedHits);
            }
        }

        // ------------------------------------------------------------------ Visual

        private void StartSweepVisual(int step)
        {
            if (sweepPivot == null || sweepRenderer == null) return;
            float mul = RangeMultiplier;
            // Cada golpe del combo dibuja el arco con otra inclinación y sentido (izq→der, der→izq, amplio).
            float roll = step == 1 ? -14f : step == 2 ? 16f : 0f;
            float scale = step == 3 ? 1.3f : 1f;
            // Inclinado 25° hacia la cámara (en 3ª persona, un arco horizontal se vería de canto).
            sweepPivot.localRotation = Quaternion.Euler(-25f, 0f, roll);
            sweepPivot.localScale = Vector3.one * (scale * mul);
            sweepRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
            propertyBlock.SetFloat(DirectionId, step == 2 ? -1f : 1f);
            propertyBlock.SetFloat(ProgressId, 0f);
            propertyBlock.SetFloat(FadeId, 1f);
            sweepRenderer.SetPropertyBlock(propertyBlock);
            sweepRenderer.enabled = true;
            visualTimer = 0f;
            Flash(step == 3 ? 1f : 0.6f);
        }

        private void UpdateVisuals(float dt)
        {
            if (visualTimer >= 0f && sweepRenderer != null)
            {
                visualTimer += dt;
                float progress = Mathf.Clamp01(visualTimer / sweepVisualTime);
                float fade = 1f - Mathf.Clamp01((visualTimer - sweepVisualTime) / fadeTime);
                sweepRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
                propertyBlock.SetFloat(ProgressId, 1f - (1f - progress) * (1f - progress));
                propertyBlock.SetFloat(FadeId, fade);
                sweepRenderer.SetPropertyBlock(propertyBlock);
                if (fade <= 0f)
                {
                    sweepRenderer.enabled = false;
                    visualTimer = -1f;
                }
            }

            if (beamTimer >= 0f && beamRenderer != null)
            {
                beamTimer += dt;
                float fade = 1f - Mathf.Clamp01(beamTimer / 0.3f);
                beamRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
                propertyBlock.SetFloat(ProgressId, 1f);
                propertyBlock.SetFloat(FadeId, fade);
                beamRenderer.SetPropertyBlock(propertyBlock);
                if (fade <= 0f)
                {
                    beamRenderer.enabled = false;
                    beamTimer = -1f;
                }
            }

            if (flashLight != null && flashLight.enabled)
            {
                flashLight.intensity = Mathf.MoveTowards(flashLight.intensity, 0f, flashIntensity * 6f * dt);
                if (flashLight.intensity <= 0.01f) flashLight.enabled = false;
            }
        }

        private void Flash(float amount)
        {
            if (flashLight == null) return;
            flashLight.intensity = Mathf.Max(flashLight.intensity, flashIntensity * amount * AccessibilitySettings.FlashScale);
            flashLight.enabled = true;
        }
    }
}
