using System;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Player;
using Ascua.UI;
using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>Qué pasó con un golpe dirigido a Ilse.</summary>
    public enum IncomingHitResult
    {
        /// <summary>Recibido: resta energía.</summary>
        Hit,
        /// <summary>Esquivado durante los i-frames del dash (esquiva perfecta).</summary>
        Dodged,
        /// <summary>Contraatacado a tiempo (Recuerdo: Reloj de Nadir).</summary>
        Parried,
        /// <summary>Ignorado (invulnerabilidad breve tras otro golpe, pausa...).</summary>
        Ignored
    }

    /// <summary>
    /// Punto único por el que entra todo el daño a Ilse. No hay barra de vida: el daño se traduce
    /// en energía del Farol (PlayerEnergy.ApplyDamage). Gestiona:
    ///  - I-frames: los concede PlayerDash al empezar la esquiva (0.25 s).
    ///  - Esquiva perfecta: si un golpe llega durante los i-frames, destello + cámara lenta breve.
    ///  - Contraataque: con el Reloj de Nadir, pulsar ataque hasta 0.2 s antes del golpe lo anula
    ///    y aturde al atacante.
    ///  - Feedback: parpadeo del Farol, viñeta de daño (DamageVignette), sacudida si está permitida
    ///    y un pequeño empujón.
    ///  - Agotada (energía 0): no puede atacar ni iluminar hasta recuperar el 25 %.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCombatState : MonoBehaviour
    {
        public const string RelojNadirId = "reloj_nadir";

        public static PlayerCombatState Instance { get; private set; }

        [SerializeField] private PlayerEnergy energy;
        [SerializeField] private PlayerController controller;
        [SerializeField] private LanternController lantern;
        [SerializeField] private CinemachineImpulseSource impulse;

        [Header("Reglas")]
        [Tooltip("Tras recibir un golpe, segundos sin poder recibir otro.")]
        [SerializeField] private float hurtCooldown = 0.45f;
        [Tooltip("Ventana del contraataque (segundos ANTES del golpe en que vale haber pulsado atacar).")]
        [SerializeField] private float parryWindow = 0.2f;
        [SerializeField] private float knockbackDistance = 0.7f;
        [SerializeField] private float impulseForce = 0.25f;

        public event Action<float> Hurt;
        public event Action<Vector3> PerfectDodged;
        public event Action<Vector3> Parried;

        private float invulnerableUntil = -1f;
        private float hurtUntil = -1f;
        private float lastAttackPressTime = -10f;
        private bool exhaustedWarned;

        public bool IsInvulnerable => Time.time < invulnerableUntil;
        /// <summary>True si Ilse puede usar la luz como arma (no agotada).</summary>
        public bool CanFight => energy == null || !energy.IsExhausted;
        public bool HasParry => GameProgress.HasRecuerdo(RelojNadirId);
        /// <summary>Energía perdida por daño desde que se llamó a ResetDamageCounter (valoración de oleadas).</summary>
        public float DamageTakenCounter { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (energy == null) energy = GetComponent<PlayerEnergy>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (lantern == null) lantern = GetComponent<LanternController>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (energy != null) energy.ExhaustedChanged += OnExhaustedChanged;
        }

        private void OnDisable()
        {
            if (energy != null) energy.ExhaustedChanged -= OnExhaustedChanged;
        }

        /// <summary>Invulnerabilidad breve (la usa PlayerDash al inicio de la esquiva).</summary>
        public void GrantInvulnerability(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        }

        /// <summary>LightAttack avisa de cada pulsación de ataque (para la ventana de contraataque).</summary>
        public void NotifyAttackPressed()
        {
            lastAttackPressTime = Time.time;
        }

        public void ResetDamageCounter()
        {
            DamageTakenCounter = 0f;
        }

        /// <summary>
        /// Un ataque de sombra alcanza a Ilse. 'attacker' (opcional) recibe el aturdimiento del
        /// contraataque. Devuelve qué ocurrió para que el atacante reaccione (p. ej. el proyectil se rompe).
        /// </summary>
        public IncomingHitResult ReceiveHit(float energyDamage, Vector3 from, ShadowCreature attacker = null)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f) return IncomingHitResult.Ignored;

            Vector3 position = transform.position;

            if (IsInvulnerable)
            {
                if (CombatFx.Instance != null) CombatFx.Instance.PerfectDodge(position);
                EventBus.RaisePerfectDodge(position);
                PerfectDodged?.Invoke(position);
                return IncomingHitResult.Dodged;
            }

            if (HasParry && Time.time - lastAttackPressTime <= parryWindow)
            {
                lastAttackPressTime = -10f;
                if (attacker != null)
                {
                    attacker.ReceiveLight(new HitInfo(2, 3.5f, position, HitKind.Contraataque, 1.6f));
                }
                if (CombatFx.Instance != null) CombatFx.Instance.Parry(position);
                EventBus.RaiseParried(position);
                Parried?.Invoke(position);
                return IncomingHitResult.Parried;
            }

            if (Time.time < hurtUntil) return IncomingHitResult.Ignored;
            hurtUntil = Time.time + hurtCooldown;

            float lost = energy != null ? energy.ApplyDamage(energyDamage) : energyDamage;
            DamageTakenCounter += lost;

            // Feedback: Farol que tiembla, viñeta, sacudida (si se permite) y empujón hacia atrás.
            if (lantern != null) lantern.PlayHurtFlicker();
            if (DamageVignette.Instance != null) DamageVignette.Instance.Pulse(Mathf.Clamp01(energyDamage / 15f));
            if (impulse != null && AccessibilitySettings.CameraShake) impulse.GenerateImpulseWithForce(impulseForce);
            if (controller != null && !controller.IsBusy)
            {
                Vector3 away = position - from;
                away.y = 0f;
                if (away.sqrMagnitude > 0.001f) controller.Dash(away.normalized, knockbackDistance, 0.14f);
            }

            EventBus.RaisePlayerHurt(lost);
            Hurt?.Invoke(lost);
            return IncomingHitResult.Hit;
        }

        private void OnExhaustedChanged(bool exhausted)
        {
            if (!exhausted)
            {
                exhaustedWarned = false;
                return;
            }
            // Solo se avisa si el agotamiento llega por combate (en la exploración ya se entiende solo).
            if (exhaustedWarned || Time.time > hurtUntil + 1f) return;
            exhaustedWarned = true;
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show("El Farol se apaga · huye y deja que recupere su luz", 2.6f);
        }
    }
}
