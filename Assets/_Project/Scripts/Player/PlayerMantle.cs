using Ascua.Core;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Trepar bordes bajos: al saltar contra un borde (o llegar a él en el aire), en vez de rebotar,
    /// Ilse sube con una animación corta. Detección con dos rayos: uno horizontal busca la pared
    /// y otro vertical busca la parte de arriba; luego se comprueba que cabe de pie.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerMantle : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private PlayerJump jumpFx;

        [Header("Detección")]
        [Tooltip("Altura mínima del borde (más bajo ya lo sube el CharacterController solo).")]
        [SerializeField] private float minLedgeHeight = 0.35f;
        [Tooltip("Altura máxima trepable desde el suelo (m).")]
        [SerializeField] private float maxLedgeHeight = 1.7f;
        [Tooltip("En el aire, altura máxima del borde respecto a los pies.")]
        [SerializeField] private float maxAirLedgeHeight = 1.1f;
        [SerializeField] private float reach = 0.75f;
        [SerializeField] private float characterRadius = 0.28f;
        [SerializeField] private float characterHeight = 1.7f;
        [SerializeField] private LayerMask geometry = 1; // Default

        [Header("Animación")]
        [SerializeField] private float baseDuration = 0.32f;
        [Tooltip("Segundos extra por metro de altura.")]
        [SerializeField] private float durationPerMeter = 0.12f;

        private float airCheckTimer;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (jumpFx == null) jumpFx = GetComponent<PlayerJump>();
        }

        private void Update()
        {
            // En el aire, empujando hacia delante contra un borde: trepar automáticamente.
            if (controller == null || controller.IsBusy || controller.IsGrounded) return;
            if (controller.DesiredDirection.sqrMagnitude < 0.5f) return;
            airCheckTimer -= Time.deltaTime;
            if (airCheckTimer > 0f) return;
            airCheckTimer = 0.05f;
            TryMantle(maxAirLedgeHeight);
        }

        /// <summary>Intenta trepar el borde que hay delante. Devuelve true si empieza a trepar.</summary>
        public bool TryMantle()
        {
            return TryMantle(maxLedgeHeight);
        }

        private bool TryMantle(float maxHeight)
        {
            if (controller == null || controller.IsBusy) return false;

            Vector3 feet = transform.position;
            Vector3 forward = controller.DesiredDirection.sqrMagnitude > 0.01f ? controller.DesiredDirection : transform.forward;
            forward.y = 0f;
            forward.Normalize();

            // 1) ¿Pared delante a la altura de la cintura?
            Vector3 chest = feet + Vector3.up * Mathf.Min(maxHeight - 0.1f, 0.6f);
            if (!Physics.Raycast(chest, forward, out RaycastHit wall, reach, geometry, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(wall.normal.y) > 0.3f) return false;

            // 2) ¿Dónde está la parte de arriba?
            Vector3 probe = wall.point - wall.normal * (characterRadius + 0.05f) + Vector3.up * (maxHeight + 0.3f);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxHeight + 0.3f, geometry, QueryTriggerInteraction.Ignore)) return false;
            if (top.normal.y < 0.7f) return false;

            float ledgeHeight = top.point.y - feet.y;
            if (ledgeHeight < minLedgeHeight || ledgeHeight > maxHeight) return false;

            // 3) ¿Cabe de pie arriba?
            Vector3 bottom = top.point + Vector3.up * (characterRadius + 0.05f);
            Vector3 head = top.point + Vector3.up * (characterHeight - characterRadius);
            if (Physics.CheckCapsule(bottom, head, characterRadius, geometry, QueryTriggerInteraction.Ignore)) return false;

            float duration = baseDuration + ledgeHeight * durationPerMeter;
            transform.rotation = Quaternion.LookRotation(-new Vector3(wall.normal.x, 0f, wall.normal.z).normalized, Vector3.up);
            controller.Mantle(top.point + Vector3.up * 0.02f, duration);

            if (jumpFx != null)
            {
                jumpFx.PlayLean(18f, duration);
                jumpFx.PlayDust(5);
            }
            EventBus.RaisePlayerMantled(transform.position);
            return true;
        }
    }
}
