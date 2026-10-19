using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Traduce la velocidad del PlayerController al parámetro "Speed" del Animator
    /// (blend tree Idle 0 / Walk 0.5 / Run 1). Si más adelante se cambia el modelo
    /// por un humanoide Mecanim, basta con un Animator Controller con el mismo parámetro.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerController controller;
        [Tooltip("Suavizado del parámetro Speed en segundos.")]
        [SerializeField] private float dampTime = 0.1f;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (animator == null || controller == null) return;
            animator.SetFloat(SpeedHash, controller.NormalizedAnimSpeed, dampTime, Time.deltaTime);
        }
    }
}
