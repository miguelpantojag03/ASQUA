using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Quests;
using Ascua.UI;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.Characters
{
    /// <summary>
    /// El Heraldo: antagonista lento con farol violeta. Aparece a mitad de la noche entre la niebla,
    /// merodea y persigue a Ilse si se acerca; a su lado el Farol se apaga (drena energía).
    /// El Pulso de luz lo repele. La primera vez que la alcanza le ofrece un Trato, que es una
    /// misión de tipo TratoHeraldo (la misma arquitectura de misiones, no un sistema aparte).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Heraldo_AI : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private enum State { Dormido, Acechando, Persiguiendo, Repelido, Retirado }

        public static Heraldo_AI Instance { get; private set; }

        [Header("Aparición")]
        [SerializeField] private bool heraldoEnabled = true;
        [SerializeField, Range(0f, 1f)] private float appearAtProgress = 0.25f;
        [SerializeField] private Transform[] lurkPoints;

        [Header("Movimiento")]
        [SerializeField] private float wanderSpeed = 0.8f;
        [SerializeField] private float chaseSpeed = 1.3f;
        [SerializeField] private float detectionRadius = 15f;
        [SerializeField] private float loseRadius = 24f;

        [Header("Efecto sobre Ilse")]
        [SerializeField] private float drainRadius = 2.4f;
        [SerializeField] private float energyDrainPerSecond = 8f;
        [Tooltip("Velocidad de Ilse cerca del Heraldo (0-1).")]
        [SerializeField, Range(0.2f, 1f)] private float slowFactor = 0.6f;
        [Tooltip("Segundos que el dash libera del abrazo del Heraldo.")]
        [SerializeField] private float dashReleaseSeconds = 1.5f;

        private Ascua.Player.PlayerController playerController;
        private float lastDashTime = -10f;

        [Header("Repulsión y Trato")]
        [SerializeField] private float repelDuration = 5f;
        [SerializeField] private float repelSpeed = 3.5f;
        [SerializeField] private float retreatAfterDealSeconds = 45f;
        [Tooltip("Distancia a la que ofrece su Trato la primera vez.")]
        [SerializeField] private float offerDistance = 3f;

        [Header("Visual")]
        [SerializeField] private Animator animator;
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Renderer lanternRenderer;
        [SerializeField] private Light violetLight;
        [SerializeField] private Color lanternColor = new Color(0.231f, 0.165f, 0.42f);
        [SerializeField] private float lanternEmission = 4f;

        private NavMeshAgent agent;
        private NpcInteractable interactable;
        private Transform player;
        private PlayerEnergy playerEnergy;
        private State state = State.Dormido;
        private float stateTimer;
        private float repathTimer;
        private bool offered;
        private MaterialPropertyBlock propertyBlock;

        /// <summary>True si está en la escena (visible y activo).</summary>
        public bool IsPresent => state != State.Dormido;

        private void Awake()
        {
            Instance = this;
            agent = GetComponent<NavMeshAgent>();
            interactable = GetComponent<NpcInteractable>();
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.QuestStateChanged += OnQuestStateChanged;
            EventBus.PlayerDashed += OnPlayerDashed;
        }

        private void OnDisable()
        {
            EventBus.QuestStateChanged -= OnQuestStateChanged;
            EventBus.PlayerDashed -= OnPlayerDashed;
        }

        private void OnPlayerDashed(Vector3 position)
        {
            lastDashTime = Time.time;
        }

        /// <summary>Distancia actual a Ilse (o infinito si no está presente). La usa la música de tensión.</summary>
        public float DistanceToPlayer => IsPresent && player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

        /// <summary>Ajustes de la noche (los aplica NightManager desde el NightQuestSet).</summary>
        public void Configure(bool enabledThisNight, float appearAt, float speed)
        {
            heraldoEnabled = enabledThisNight;
            appearAtProgress = appearAt;
            chaseSpeed = speed;
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerEnergy = playerObject.GetComponent<PlayerEnergy>();
                playerController = playerObject.GetComponent<Ascua.Player.PlayerController>();
            }
            SetVisible(false);
            agent.enabled = false;
            if (interactable != null) interactable.SetInteractable(false);
        }

        private void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (state == State.Dormido)
            {
                if (heraldoEnabled && NightManager.Instance != null && NightManager.Instance.IsRunning && NightManager.Instance.Progress01 >= appearAtProgress) Appear();
                return;
            }

            float distance = Vector3.Distance(transform.position, player.position);
            stateTimer -= dt;
            repathTimer -= dt;

            switch (state)
            {
                case State.Acechando:
                    agent.speed = wanderSpeed;
                    if (!agent.pathPending && agent.remainingDistance < 0.5f) GoToRandomLurkPoint();
                    if (distance < detectionRadius) state = State.Persiguiendo;
                    break;

                case State.Persiguiendo:
                    agent.speed = chaseSpeed;
                    if (repathTimer <= 0f)
                    {
                        repathTimer = 0.3f;
                        agent.SetDestination(player.position);
                    }
                    if (distance > loseRadius) EnterLurk();
                    TryOfferDeal(distance);
                    break;

                case State.Repelido:
                case State.Retirado:
                    if (stateTimer <= 0f) EnterLurk();
                    break;
            }

            // Junto al Heraldo, el Farol se vacía y los pasos pesan. Esquivar (dash) rompe su abrazo un momento.
            bool hunting = state == State.Persiguiendo || state == State.Acechando;
            bool released = Time.time - lastDashTime < dashReleaseSeconds;
            if (hunting && !released && distance <= drainRadius * 1.5f && playerController != null)
            {
                playerController.ApplySlow(slowFactor);
            }
            if (hunting && !released && distance <= drainRadius && playerEnergy != null && (playerController == null || !playerController.IsDashing))
            {
                playerEnergy.Consume(energyDrainPerSecond * dt);
            }

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.1f, chaseSpeed)) * 0.5f, 0.2f, dt);
            }
            UpdateLantern();
        }

        /// <summary>Hace aparecer al Heraldo ya (eventos narrativos, pruebas, IA).</summary>
        public void ForceAppear()
        {
            if (state == State.Dormido) Appear();
        }

        private void Appear()
        {
            Transform spawn = FarthestLurkPoint();
            Vector3 position = spawn != null ? spawn.position : transform.position;
            agent.enabled = true;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 4f, NavMesh.AllAreas)) agent.Warp(hit.position);
            SetVisible(true);
            if (interactable != null) interactable.SetInteractable(true);
            EnterLurk();

            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show("Algo camina entre la niebla…", 2.6f);
        }

        private void EnterLurk()
        {
            state = State.Acechando;
            GoToRandomLurkPoint();
        }

        /// <summary>El Pulso de luz lo hace retroceder unos segundos.</summary>
        public void Repel(Vector3 from)
        {
            if (!IsPresent || !agent.enabled) return;
            state = State.Repelido;
            stateTimer = repelDuration;
            agent.speed = repelSpeed;

            Vector3 away = transform.position - from;
            away.y = 0f;
            Vector3 target = transform.position + away.normalized * 10f;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 6f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        private void TryOfferDeal(float distance)
        {
            if (offered || distance > offerDistance) return;
            QuestManager manager = QuestManager.Instance;
            DialogueRunner runner = DialogueRunner.Instance;
            if (manager == null || runner == null || runner.IsOpen || interactable == null) return;

            QuestRuntime deal = manager.GetQuestForNpc(interactable.NpcId);
            if (deal == null || deal.State != QuestState.Disponible) return;

            offered = true;
            agent.isStopped = true;
            interactable.Interact(player);
            agent.isStopped = false;
        }

        private void OnQuestStateChanged(string questId, string state)
        {
            if (interactable == null || QuestManager.Instance == null) return;
            QuestRuntime q = QuestManager.Instance.FindById(questId);
            if (q == null || q.Data.giverNpcId != interactable.NpcId) return;

            if (q.State == QuestState.Completada)
            {
                // Trato aceptado: se retira, satisfecho, durante un buen rato.
                this.state = State.Retirado;
                stateTimer = retreatAfterDealSeconds;
                Transform far = FarthestLurkPoint();
                if (far != null && agent.enabled) agent.SetDestination(far.position);
            }
            else if (state == "Rechazada")
            {
                // Rechazado: se aparta un momento y vuelve más rápido.
                chaseSpeed *= 1.15f;
                Repel(player.position);
            }
        }

        private void GoToRandomLurkPoint()
        {
            if (lurkPoints == null || lurkPoints.Length == 0 || !agent.enabled) return;
            Transform point = lurkPoints[Random.Range(0, lurkPoints.Length)];
            if (point != null) agent.SetDestination(point.position);
        }

        private Transform FarthestLurkPoint()
        {
            Transform best = null;
            float bestDistance = -1f;
            if (lurkPoints == null || player == null) return null;
            foreach (Transform point in lurkPoints)
            {
                if (point == null) continue;
                float d = (point.position - player.position).sqrMagnitude;
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = point;
                }
            }
            return best;
        }

        private void SetVisible(bool visible)
        {
            if (bodyRenderers != null) foreach (Renderer r in bodyRenderers) if (r != null) r.enabled = visible;
            if (lanternRenderer != null) lanternRenderer.enabled = visible;
            if (violetLight != null) violetLight.enabled = visible;
        }

        private void UpdateLantern()
        {
            // El farol violeta late; repelido, casi se apaga.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.3f);
            float factor = state == State.Repelido ? 0.25f : pulse;
            if (violetLight != null) violetLight.intensity = 1.6f * factor;
            if (lanternRenderer != null)
            {
                lanternRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, lanternColor * (lanternEmission * factor));
                lanternRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
