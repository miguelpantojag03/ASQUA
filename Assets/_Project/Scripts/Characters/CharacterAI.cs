using System;
using System.Collections.Generic;
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

        // Escolta y conversación (Fase 3).
        private enum Mode { Patrol, Escort, Hold, Talking }
        private static readonly List<CharacterAI> all = new List<CharacterAI>();
        private Mode mode = Mode.Patrol;
        private Mode modeBeforeTalk = Mode.Patrol;
        private Vector3 escortDestination;
        private float escortStartDistance = 1f;
        private NpcIdentity identity;

        public CharacterMood Mood { get; private set; } = CharacterMood.Calm;
        public ZoneHope Zone => zone;
        public string NpcId => identity != null ? identity.NpcId : name;
        /// <summary>True cuando el NPC escoltado llegó a su destino.</summary>
        public bool EscortArrived { get; private set; }
        /// <summary>0-1: cuánto camino de la escolta lleva recorrido.</summary>
        public float EscortProgress01 { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        /// <summary>Busca un NPC por su id de NpcIdentity (mina, teo...).</summary>
        public static CharacterAI Find(string npcId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].NpcId == npcId) return all[i];
            }
            return null;
        }

        private static readonly int CelebrateHash = Animator.StringToHash("Celebrar");
        private float shiverSeed;

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            Ascua.Core.EventBus.HopeLevelReached += OnHopeLevel;
            Ascua.Core.EventBus.PenumbraSurge += OnSurge;
        }

        private void OnDisable()
        {
            all.Remove(this);
            Ascua.Core.EventBus.HopeLevelReached -= OnHopeLevel;
            Ascua.Core.EventBus.PenumbraSurge -= OnSurge;
        }

        /// <summary>Reacción en tiempo real: su barrio gana Esperanza → un saltito de alegría.</summary>
        private void OnHopeLevel(string zoneId, int level, Vector3 position)
        {
            if (zone == null || zone.ZoneId != zoneId || Mood == CharacterMood.Collapsed || mode == Mode.Talking) return;
            if (animator != null) animator.SetTrigger(CelebrateHash);
        }

        /// <summary>La ráfaga de la Penumbra los asusta: se encogen un momento.</summary>
        private void OnSurge(bool started)
        {
            surgeFear = started ? 1f : 0f;
        }

        private float surgeFear;

        // ------------------------------------------------------------------ Escolta / conversación

        /// <summary>Deja la patrulla y camina hacia un destino (misiones de escolta).</summary>
        public void StartEscort(Vector3 destination)
        {
            mode = Mode.Escort;
            escortDestination = destination;
            EscortArrived = false;
            EscortProgress01 = 0f;
            escortStartDistance = Mathf.Max(1f, Vector3.Distance(transform.position, destination));
            waiting = false;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
        }

        /// <summary>Se queda quieto donde está (p. ej. tras llegar, esperando la entrega de la misión).</summary>
        public void HoldPosition()
        {
            mode = Mode.Hold;
            if (agent.isOnNavMesh) agent.isStopped = true;
        }

        /// <summary>Termina la escolta y vuelve a su patrulla habitual.</summary>
        public void StopEscort()
        {
            if (mode == Mode.Talking) modeBeforeTalk = Mode.Patrol;
            else mode = Mode.Patrol;
            EscortArrived = false;
            if (agent.isOnNavMesh) agent.isStopped = false;
            if (mode == Mode.Patrol && waypoints != null && waypoints.Length > 0) GoToCurrentWaypoint();
        }

        /// <summary>Se detiene y mira a quien le habla.</summary>
        public void BeginConversation(Transform listener)
        {
            if (mode != Mode.Talking) modeBeforeTalk = mode;
            mode = Mode.Talking;
            if (agent.isOnNavMesh) agent.isStopped = true;

            if (listener != null)
            {
                Vector3 toListener = listener.position - transform.position;
                toListener.y = 0f;
                if (toListener.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toListener.normalized, Vector3.up);
            }
        }

        public void EndConversation()
        {
            if (mode != Mode.Talking) return;
            mode = modeBeforeTalk;
            if (agent.isOnNavMesh && mode != Mode.Hold) agent.isStopped = false;
            if (mode == Mode.Escort && agent.isOnNavMesh) agent.SetDestination(escortDestination);
        }

        private void UpdateEscort()
        {
            if (!agent.isOnNavMesh) return;
            float remaining = Vector3.Distance(transform.position, escortDestination);
            EscortProgress01 = Mathf.Clamp01(1f - remaining / escortStartDistance);
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.25f)
            {
                EscortArrived = true;
                EscortProgress01 = 1f;
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            identity = GetComponent<NpcIdentity>();
            shiverSeed = UnityEngine.Random.value * 10f;
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
                switch (mode)
                {
                    case Mode.Patrol: Patrol(); break;
                    case Mode.Escort: UpdateEscort(); break;
                }
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
            target = Mathf.Max(target, surgeFear * 0.45f);
            posture = Mathf.MoveTowards(posture, target, postureSpeed * Time.deltaTime);
            if (visual == null) return;

            float eased = posture * posture * (3f - 2f * posture);
            visual.localScale = new Vector3(visualBaseScale.x, visualBaseScale.y * Mathf.Lerp(1f, 0.62f, eased), visualBaseScale.z);
            visual.localPosition = visualBasePosition;
            // Preocupados o asustados, tiemblan un poco (se nota sin hablarles).
            float shiver = Mood == CharacterMood.Worried || surgeFear > 0f ? Mathf.Sin(Time.time * 23f + shiverSeed) * 1.8f : 0f;
            visual.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 16f, eased), 0f, shiver);

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
                r.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
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
