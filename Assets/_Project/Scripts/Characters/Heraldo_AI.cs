using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.Characters
{
    /// <summary>
    /// El Heraldo: antagonista lento con farol violeta. Aparece a mitad de la noche entre la niebla,
    /// merodea y persigue a Ilse si se acerca; a su lado el Farol se apaga (drena energía).
    /// El Pulso de luz lo repele. La primera vez que la alcanza le ofrece un Trato, que es una
    /// misión de tipo TratoHeraldo (la misma arquitectura de misiones, no un sistema aparte).
    /// En Persecución Activa se le puede "enfrentar" con luz (HeraldoConfrontation): nunca se le
    /// derrota, solo retrocede, se desvanece y reaparece, y al final se aleja revelando una pista.
    /// Cacería (HeraldoHuntDirector): en momentos fijos de la noche corre casi tan rápido como Ilse
    /// durante unos segundos. Si la alcanza hay una penalización breve; nunca hay Game Over.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Heraldo_AI : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private enum State { Dormido, Acechando, Persiguiendo, Repelido, Retirado, Cazando }

        /// <summary>Cómo terminó una cacería.</summary>
        public enum HuntOutcome { Refugio, Alcanzada, PerdioInteres }

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

        [Header("Cacería")]
        [Tooltip("Velocidad al cazar (Ilse corre a 5.2: solo escapa corriendo o con el dash).")]
        [SerializeField] private float huntSpeed = 4.8f;
        [SerializeField] private float huntAcceleration = 14f;
        [Tooltip("Segundos que el Pulso lo frena mientras caza (fuera de la cacería son repelDuration).")]
        [SerializeField] private float huntRepelSeconds = 1.5f;
        [SerializeField] private float huntCatchDistance = 1.6f;
        [Tooltip("Distancia a Ilse a la que aparece al empezar (del lado contrario al refugio).")]
        [SerializeField] private float huntSpawnDistance = 18f;
        [Tooltip("Si en este tiempo no la alcanza, pierde interés.")]
        [SerializeField] private float huntMaxSeconds = 15f;
        [SerializeField] private float caughtEnergyLoss = 25f;
        [SerializeField] private float caughtHopeLoss = 15f;
        [Tooltip("La cacería nunca deja una zona por debajo de esto.")]
        [SerializeField] private float caughtHopeFloor = 5f;
        [SerializeField] private float retreatAfterCatchSeconds = 20f;
        [SerializeField] private float retreatAfterHuntSeconds = 10f;

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
        private float huntElapsed;
        private float huntStunTimer;
        private float normalAcceleration = 8f;
        private bool warningFlicker;

        /// <summary>Se lanza al empezar una cacería.</summary>
        public event System.Action HuntStarted;
        /// <summary>Se lanza al terminar una cacería (refugio, alcanzada o sin interés).</summary>
        public event System.Action<HuntOutcome> HuntEnded;

        /// <summary>True si está en la escena (visible y activo).</summary>
        public bool IsPresent => state != State.Dormido;

        /// <summary>Persecución Activa: el único momento en que se le puede enfrentar.</summary>
        public bool IsChasing => state == State.Persiguiendo;

        /// <summary>Cacería en curso (corre rápido hacia Ilse).</summary>
        public bool IsHunting => state == State.Cazando;

        /// <summary>Puede empezar una cacería: presente y merodeando o persiguiendo (no repelido ni retirado).</summary>
        public bool IsAvailableForHunt => state == State.Acechando || state == State.Persiguiendo;

        /// <summary>Segundos que lleva la cacería actual.</summary>
        public float HuntElapsed => huntElapsed;
        public float HuntMaxSeconds => huntMaxSeconds;

        /// <summary>
        /// Se desvanece y reaparece a 'minRadius'-'maxRadius' m de 'center' (enfrentamiento).
        /// Devuelve false si no encontró sitio en el NavMesh.
        /// </summary>
        public bool TeleportAround(Vector3 center, float minRadius, float maxRadius)
        {
            if (!IsPresent || !agent.enabled || IsHunting) return false;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float radius = Random.Range(minRadius, maxRadius);
                Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                agent.Warp(hit.position);
                if (player != null)
                {
                    Vector3 look = player.position - hit.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                }
                // Tras reaparecer, observa un instante antes de seguir.
                state = State.Repelido;
                stateTimer = 1.2f;
                agent.ResetPath();
                return true;
            }
            return false;
        }

        /// <summary>Se aleja y no vuelve en 'seconds' (fin del enfrentamiento).</summary>
        public void RetreatFor(float seconds)
        {
            if (!IsPresent) return;
            if (IsHunting) StopHunt(HuntOutcome.PerdioInteres);
            state = State.Retirado;
            stateTimer = seconds;
            Transform far = FarthestLurkPoint();
            if (far != null && agent.enabled) agent.SetDestination(far.position);
        }

        private void Awake()
        {
            Instance = this;
            agent = GetComponent<NavMeshAgent>();
            interactable = GetComponent<NpcInteractable>();
            propertyBlock = new MaterialPropertyBlock();
            normalAcceleration = agent.acceleration;
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

                case State.Cazando:
                    UpdateHunt(distance, dt);
                    break;
            }

            // Junto al Heraldo, el Farol se vacía y los pasos pesan. Esquivar (dash) rompe su abrazo un momento.
            bool hunting = state == State.Persiguiendo || state == State.Acechando || state == State.Cazando;
            // Dentro de un refugio, durante la cacería, su abrazo no llega.
            bool released = Time.time - lastDashTime < dashReleaseSeconds
                            || (IsHunting && SafeHaven.RefugeContaining(player.position) != null)
                            || HuntFrozen;
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
                // Al cazar corre (hasta 1); el resto del tiempo camina (hasta 0.5).
                float topSpeed = IsHunting ? huntSpeed : chaseSpeed;
                float gait = IsHunting ? 1f : 0.5f;
                animator.SetFloat(SpeedHash, Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.1f, topSpeed)) * gait, 0.2f, dt);
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
            agent.speed = repelSpeed;
            MoveAwayFrom(from);

            // Cazando, el Pulso solo lo frena un momento: la cacería sigue.
            if (IsHunting)
            {
                huntStunTimer = huntRepelSeconds;
                return;
            }
            state = State.Repelido;
            stateTimer = repelDuration;
        }

        private void MoveAwayFrom(Vector3 from)
        {
            Vector3 away = transform.position - from;
            away.y = 0f;
            Vector3 target = transform.position + away.normalized * 10f;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 6f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }

        // ================================================================== Cacería

        /// <summary>
        /// Empieza una cacería: reaparece a ~huntSpawnDistance m de Ilse, del lado contrario a 'awayFrom'
        /// (el refugio más cercano) y corre hacia ella a 'speed'. Devuelve false si no está presente.
        /// </summary>
        public bool StartHunt(float speed, Vector3 awayFrom)
        {
            if (!IsPresent || !agent.enabled || player == null || IsHunting) return false;

            huntSpeed = Mathf.Max(0.5f, speed);
            WarpBehindPlayer(awayFrom);

            state = State.Cazando;
            huntElapsed = 0f;
            huntStunTimer = 0f;
            repathTimer = 0f;
            warningFlicker = false;
            agent.isStopped = false;
            agent.acceleration = huntAcceleration;
            agent.ResetPath();
            HuntStarted?.Invoke();
            return true;
        }

        /// <summary>
        /// Termina la cacería. success = Ilse llegó a un refugio; si no, pierde interés.
        /// En ambos casos se retira un rato. Las recompensas las da quien llama (SafeHaven).
        /// </summary>
        public void EndHunt(bool success)
        {
            if (!IsHunting) return;
            StopHunt(success ? HuntOutcome.Refugio : HuntOutcome.PerdioInteres);
            state = State.Retirado;
            stateTimer = retreatAfterHuntSeconds;
            Transform far = FarthestLurkPoint();
            if (far != null && agent.enabled) agent.SetDestination(far.position);
        }

        /// <summary>Aviso de cacería: el farol violeta parpadea (con "reducir destellos" solo brilla más).</summary>
        public void SetWarningFlicker(bool active)
        {
            warningFlicker = active;
        }

        /// <summary>Con un diálogo abierto Ilse no puede moverse: la cacería se congela (ni avanza ni alcanza).</summary>
        private bool HuntFrozen => IsHunting && DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen;

        private void UpdateHunt(float distance, float dt)
        {
            if (HuntFrozen)
            {
                if (!agent.isStopped) agent.isStopped = true;
                return;
            }
            if (agent.isStopped) agent.isStopped = false;

            huntElapsed += dt;

            if (huntStunTimer > 0f)
            {
                // Frenado por el Pulso: retrocede un instante.
                huntStunTimer -= dt;
                agent.speed = repelSpeed;
                if (huntStunTimer <= 0f) repathTimer = 0f;
            }
            else
            {
                agent.speed = huntSpeed;
                if (repathTimer <= 0f)
                {
                    repathTimer = 0.2f;
                    agent.SetDestination(HuntDestination());
                }
            }

            if (distance <= huntCatchDistance && CanCatchPlayer())
            {
                Caught();
                return;
            }
            if (huntElapsed >= huntMaxSeconds) EndHunt(false);
        }

        /// <summary>Hacia dónde corre al cazar: a Ilse, o al borde del refugio si ella está dentro (no puede entrar).</summary>
        private Vector3 HuntDestination()
        {
            SafeHaven haven = SafeHaven.RefugeContaining(player.position);
            if (haven == null) return player.position;
            Vector3 edge = haven.EdgePointToward(transform.position, 0.4f);
            return NavMesh.SamplePosition(edge, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : transform.position;
        }

        /// <summary>True si ahora mismo puede alcanzar a Ilse (no frenado y ella fuera de un refugio).</summary>
        private bool CanCatchPlayer()
        {
            return huntStunTimer <= 0f && SafeHaven.RefugeContaining(player.position) == null;
        }

        /// <summary>Te alcanzó: penalización breve (energía y algo de Esperanza), un aviso y se retira.</summary>
        private void Caught()
        {
            if (playerEnergy != null) playerEnergy.ApplyDamage(caughtEnergyLoss);

            // La zona más cercana pierde algo de Esperanza, pero la cacería nunca la apaga.
            ZoneHope zone = NearestLiveZone(player.position);
            if (zone != null)
            {
                float loss = Mathf.Min(caughtHopeLoss, zone.Hope - caughtHopeFloor);
                if (loss > 0f) zone.AddHope(-loss);
            }

            if (ZoneLostNotification.Instance != null)
            {
                ZoneLostNotification.Instance.Show("El Heraldo te alcanza… por un instante olvidas por qué brillabas", 2.8f);
            }

            StopHunt(HuntOutcome.Alcanzada);
            state = State.Retirado;
            stateTimer = retreatAfterCatchSeconds;
            Transform far = FarthestLurkPoint();
            if (far != null && agent.enabled) agent.SetDestination(far.position);
        }

        /// <summary>Sale del estado de cacería y avisa. Quien llama fija el estado siguiente.</summary>
        private void StopHunt(HuntOutcome outcome)
        {
            if (!IsHunting) return;
            state = State.Acechando;
            huntStunTimer = 0f;
            warningFlicker = false;
            agent.acceleration = normalAcceleration;
            if (agent.enabled) agent.isStopped = false;
            HuntEnded?.Invoke(outcome);
        }

        /// <summary>Reaparece a ~huntSpawnDistance m de Ilse, en dirección contraria a 'awayFrom'.</summary>
        private void WarpBehindPlayer(Vector3 awayFrom)
        {
            Vector3 back = player.position - awayFrom;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = -player.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = Vector3.back;
            back.Normalize();

            // Primero justo detrás; si no hay NavMesh allí, se abre el ángulo y se cambia la distancia.
            float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f };
            float[] distances = { huntSpawnDistance, huntSpawnDistance * 0.8f, huntSpawnDistance * 1.2f, huntSpawnDistance * 0.6f };
            foreach (float d in distances)
            {
                foreach (float a in angles)
                {
                    Vector3 p = player.position + Quaternion.Euler(0f, a, 0f) * back * d;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                    agent.Warp(hit.position);
                    Vector3 look = player.position - hit.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                    return;
                }
            }
            // Sin sitio en el NavMesh: caza desde donde está.
        }

        private static ZoneHope NearestLiveZone(Vector3 position)
        {
            ZoneHope best = null;
            float bestDistance = float.MaxValue;
            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null || zones[i].IsDead) continue;
                float d = (zones[i].Center - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = zones[i];
                }
            }
            return best;
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

            if (IsHunting) return;   // la cacería manda: el Trato no la interrumpe

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
            // El farol violeta late; repelido, casi se apaga. Antes de una cacería parpadea
            // (con "reducir destellos" solo brilla un poco más, sin parpadeo). Cazando, brilla fijo.
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.3f);
            float factor = state == State.Repelido ? 0.25f : pulse;
            if (warningFlicker)
            {
                factor = AccessibilitySettings.ReduceFlashes ? 1.15f : (Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? 1.5f : 0.2f);
            }
            else if (IsHunting)
            {
                factor = 1.2f;
            }
            if (violetLight != null) violetLight.intensity = 1.6f * factor;
            if (lanternRenderer != null)
            {
                lanternRenderer.GetPropertyBlock(propertyBlock ??= new MaterialPropertyBlock());
                propertyBlock.SetColor(EmissionColorId, lanternColor * (lanternEmission * factor));
                lanternRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
