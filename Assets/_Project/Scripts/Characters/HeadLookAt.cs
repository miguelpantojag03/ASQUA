using UnityEngine;

namespace Ascua.Characters
{
    /// <summary>
    /// Giro de cabeza suave hacia el jugador cuando está cerca. No es IA de movimiento:
    /// el NPC sigue quieto, solo "nota" la presencia de Ilse. Se aplica después del Animator.
    /// </summary>
    public class HeadLookAt : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [Tooltip("Si se deja vacío se busca el objeto con tag Player.")]
        [SerializeField] private Transform target;
        [SerializeField] private float targetHeight = 1.3f;
        [SerializeField] private float range = 6f;
        [SerializeField] private float maxYaw = 70f;
        [SerializeField] private float maxPitch = 20f;
        [SerializeField] private float blendSpeed = 2f;
        [SerializeField] private float turnSmoothing = 6f;

        private float weight;
        private float smoothYaw;
        private float smoothPitch;

        private void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (head == null || target == null) return;

            Vector3 toTarget = target.position + Vector3.up * targetHeight - head.position;
            Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            Vector3 bodyForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            float yaw = Vector3.SignedAngle(bodyForward, flat, Vector3.up);
            float pitch = -Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg;

            bool wantsToLook = toTarget.magnitude < range && Mathf.Abs(yaw) < maxYaw + 40f;
            weight = Mathf.MoveTowards(weight, wantsToLook ? 1f : 0f, blendSpeed * Time.deltaTime);

            float k = 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime);
            smoothYaw = Mathf.Lerp(smoothYaw, Mathf.Clamp(yaw, -maxYaw, maxYaw), k);
            smoothPitch = Mathf.Lerp(smoothPitch, Mathf.Clamp(pitch, -maxPitch, maxPitch), k);

            if (weight <= 0f) return;
            Quaternion look = transform.rotation * Quaternion.Euler(smoothPitch, smoothYaw, 0f);
            head.rotation = Quaternion.Slerp(head.rotation, look, weight);
        }
    }
}
