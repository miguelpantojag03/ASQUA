using System;
using Ascua.World;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.Characters
{
    /// <summary>Estado anímico visible de un NPC según la Esperanza de su zona.</summary>
    public enum CharacterMood
    {
        Calm,
        Worried,
        Collapsed
    }

    /// <summary>
    /// NPC con NavMesh: patrulla waypoints de su zona y reacciona a la Esperanza.
    ///  - Calm: camina a ritmo normal.
    ///  - Worried (Esperanza baja): camina más lento y se encoge.
    ///  - Collapsed (zona muerta): se detiene y se queda sentado, con su acento casi apagado.
    /// Sin diálogos todavía: en la Fase 3 el sistema de diálogos/Vínculos usará Mood y NpcIdentity.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CharacterAI : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Referencias")]
        [SerializeField] private ZoneHope zone;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private Animator animator;
        [Tooltip("Transform 'Visual' del chibi (no lo anima el Animator).")]
        [SerializeField] private Transform visual;
        [Tooltip("Renderers con el color de acento (bufanda, delantal...).")]
        [SerializeField] private Renderer[] accentRenderers;

        [Header("Patrulla")]
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float worriedSpeed = 0.65f;
        [SerializeField] private Vector2 waitRange = new Vector2(1.5f, 4f);
        [SerializeField] private bool randomOrder = false;

        [Header("Reacción a la Esperanza")]
        [SerializeField, Range(0f, 100f)] private float worriedThreshold = 35f;
        [SerializeField] private Color accentColor = new Color(0.902f, 0.224f, 0.275f);
        [SerializeField] private float accentEmission = 0.9f;
        [Tooltip("Velocidad de cambio de postura (1/s).")]
        [SerializeField] private float postureSpeed = 1.5f;

        public event Action<CharacterAI, CharacterMood> MoodChanged;

        private NavMeshAgent agent;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 visualBasePosition;
        private Vector3 visualBaseScale;
        private float posture;          // 0 = erguido, 0.5 = encogido, 1 = sentado
        private int waypointIndex;
        private float waitTimer;
        private bool waiting;

        public CharacterMood Mood { get; private set; } = CharacterMood.Calm;
        public ZoneHope Zone => zone;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            propertyBlock = new MaterialPropertyBlock();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (visual == null && animator != null) visual = animator.transform;

            if (visual != null)
            {
                visualBasePosition = visual.localPosition;
                visualBaseScale = visual.localScale;
            }
        }

        private void Start()
        {
            agent.speed = walkSpeed;
            if (waypoints != null && waypoints.Length > 0)
            {
                waypointIndex = UnityEngine.Random.Range(0, waypoints.Length);
                GoToCurrentWaypoint();
            }
            ApplyAccent(1f);
        }

        private void Update()
        {
            UpdateMood();

            if (Mood == CharacterMood.Collapsed)
            {
                if (agent.isOnNavMesh && !agent.isStopped) agent.isStopped = true;
            }
            else
            {
                agent.speed = Mood == CharacterMood.Worried ? worriedSpeed : walkSpeed;
                Patrol();
            }

            if (animator != null)
            {
                float speed01 = agent.velocity.magnitude / Mathf.Max(0.01f, walkSpeed);
                animator.SetFloat(SpeedHash, Mathf.Clamp01(speed01) * 0.5f, 0.15f, Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            // La postura se aplica sobre 'Visual', que el Animator no toca.
            float target = Mood == CharacterMood.Collapsed ? 1f : Mood == CharacterMood.Worried ? 0.5f : 0f;
            posture = Mathf.MoveTowards(posture, target, postureSpeed * Time.deltaTime);
            if (visual == null) return;

            float eased = posture * posture * (3f - 2f * posture);
            visual.localScale = new Vector3(visualBaseScale.x, visualBaseScale.y * Mathf.Lerp(1f, 0.62f, eased), visualBaseScale.z);
            visual.localPosition = visualBasePosition;
            visual.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 16f, eased), 0f, 0f);

            ApplyAccent(Mathf.Lerp(1f, 0.25f, eased));
        }

        private void UpdateMood()
        {
            CharacterMood mood = CharacterMood.Calm;
            if (zone != null)
            {
                if (zone.IsDead) mood = CharacterMood.Collapsed;
                else if (zone.Hope < worriedThreshold) mood = CharacterMood.Worried;
            }

            if (mood == Mood) return;
            Mood = mood;
            MoodChanged?.Invoke(this, mood);
        }

        private void Patrol()
        {
            if (waypoints == null || waypoints.Length == 0 || !agent.isOnNavMesh) return;

            if (waiting)
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer > 0f) return;

                waiting = false;
                waypointIndex = randomOrder
                    ? UnityEngine.Random.Range(0, waypoints.Length)
                    : (waypointIndex + 1) % waypoints.Length;
                GoToCurrentWaypoint();
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                waiting = true;
                waitTimer = UnityEngine.Random.Range(waitRange.x, waitRange.y);
            }
        }

        private void GoToCurrentWaypoint()
        {
            Transform target = waypoints[waypointIndex];
            if (target == null || !agent.isOnNavMesh) return;
            agent.isStopped = false;
            agent.SetDestination(target.position);
        }

        private void ApplyAccent(float intensityFactor)
        {
            if (accentRenderers == null) return;
            Color emission = accentColor * (accentEmission * intensityFactor);
            foreach (Renderer r in accentRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(ZoneHope ownZone, Transform[] patrolPoints, Renderer[] accents, Color accent)
        {
            zone = ownZone;
            waypoints = patrolPoints;
            accentRenderers = accents;
            accentColor = accent;
        }
    }
}
