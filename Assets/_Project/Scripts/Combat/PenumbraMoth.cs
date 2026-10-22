using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Polilla de Olvido: la sombra más común. Vuela (sin NavMesh), revolotea alrededor de Ilse y
    /// se lanza en picado. Muy frágil (3 de vida: 2-3 golpes), sin barra de vida: parpadea al recibir
    /// luz y se deshace en partículas, devolviendo 6 de energía.
    /// Ciclo: acercarse → rodear 1.2-2 s → picado (aviso: brillo y alas plegadas) → retirarse.
    /// </summary>
    public class PenumbraMoth : ShadowCreature
    {
        private enum Phase { Approach, Circle, WindUp, Dive, Retreat }

        [Header("Polilla")]
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;
        [SerializeField] private float flySpeed = 3.4f;
        [SerializeField] private float diveSpeed = 7.5f;
        [SerializeField] private float hoverHeight = 1.5f;
        [SerializeField] private float circleRadius = 2.6f;
        [SerializeField] private float windUpTime = 0.4f;
        [SerializeField] private float flapSpeed = 16f;
        [SerializeField] private LayerMask obstacles = 1;

        private Phase phase;
        private float phaseTimer;
        private float circleAngle;
        private float circleDirection = 1f;
        private Vector3 diveTarget;
        private float flapSeed;
        private float circleDuration = 1.5f;

        protected override void Awake()
        {
            base.Awake();
            typeId = "polilla";
            flapSeed = Random.value * 10f;
        }

        protected override void OnSpawned()
        {
            phase = Phase.Approach;
            phaseTimer = 0f;
            circleDirection = Random.value < 0.5f ? -1f : 1f;
        }

        protected override void Update()
        {
            base.Update();
            // Las alas aletean siempre (también aturdida, más despacio).
            float speed = phase == Phase.WindUp ? flapSpeed * 0.3f : IsStunned ? flapSpeed * 0.4f : flapSpeed;
            float angle = Mathf.Sin((Time.time + flapSeed) * speed) * (phase == Phase.Dive ? 15f : 55f);
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        protected override void Tick(float dt)
        {
            Vector3 toPlayer = FlatToPlayer();
            float distance = toPlayer.magnitude;
            phaseTimer += dt;
            Vector3 target = transform.position;
            float speed = flySpeed;

            switch (phase)
            {
                case Phase.Approach:
                    target = player.position - toPlayer.normalized * circleRadius + Vector3.up * hoverHeight;
                    if (distance < circleRadius + 0.6f) SetPhase(Phase.Circle);
                    break;

                case Phase.Circle:
                    circleAngle += circleDirection * dt * 1.4f;
                    Vector3 offset = new Vector3(Mathf.Cos(circleAngle), 0f, Mathf.Sin(circleAngle)) * circleRadius;
                    target = player.position + offset + Vector3.up * (hoverHeight + Mathf.Sin(Time.time * 3f) * 0.2f);
                    if (phaseTimer > circleDuration)
                    {
                        // Turnos: si ya hay 2 sombras atacando, da otra vuelta corta y lo vuelve a intentar.
                        if (RequestAttackTurn()) SetPhase(Phase.WindUp);
                        else circleDuration += 0.4f;
                    }
                    break;

                case Phase.WindUp:
                    // Aviso: se queda quieta un instante con las alas plegadas (se puede leer y esquivar).
                    speed = 0.5f;
                    target = transform.position;
                    ShowWarning(phaseTimer / windUpTime);
                    if (phaseTimer >= windUpTime)
                    {
                        diveTarget = player.position + Vector3.up * 0.9f;
                        SetPhase(Phase.Dive);
                    }
                    break;

                case Phase.Dive:
                    speed = diveSpeed;
                    target = diveTarget;
                    ShowWarning(0.6f);
                    TryContactDamage(true);
                    if ((transform.position - diveTarget).sqrMagnitude < 0.2f || phaseTimer > 1.2f) SetPhase(Phase.Retreat);
                    break;

                case Phase.Retreat:
                    target = player.position - toPlayer.normalized * (circleRadius + 1.5f) + Vector3.up * (hoverHeight + 0.6f);
                    if (phaseTimer > 0.9f) SetPhase(Phase.Circle);
                    break;
            }

            Vector3 move = Vector3.MoveTowards(transform.position, target, speed * dt) - transform.position;
            // Evitación mínima: si hay un muro delante, sube por encima en vez de atravesarlo.
            if (move.sqrMagnitude > 0.00001f && Physics.Raycast(transform.position, move.normalized, 0.8f, obstacles, QueryTriggerInteraction.Ignore))
            {
                move = Vector3.up * speed * dt;
            }
            transform.position += move;
            FacePlayer(dt, 10f);
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTimer = 0f;
            // El turno de ataque termina al retirarse (o si la aturden y vuelve a rodear).
            if (next == Phase.Retreat || next == Phase.Circle || next == Phase.Approach) EndAttackTurn();
            // Cada vuelta dura distinto para que varias polillas no ataquen a la vez.
            if (next == Phase.Circle) circleDuration = Random.Range(1.2f, 2.1f);
        }
    }
}
