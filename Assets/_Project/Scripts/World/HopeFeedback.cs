using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Micro-recompensa visible cuando una zona sube un nivel de Esperanza (25/50/75/100%):
    /// una lluvia de chispas cálidas que sube desde el centro del barrio. El sonido lo pone AudioManager.
    /// </summary>
    public class HopeFeedback : MonoBehaviour
    {
        [SerializeField] private ZoneHope zone;
        [SerializeField] private ParticleSystem sparks;
        [Tooltip("Chispas por nivel (el nivel 4 suelta el doble).")]
        [SerializeField] private int sparksPerLevel = 18;

        private void Awake()
        {
            if (zone == null) zone = GetComponentInParent<ZoneHope>();
        }

        private void OnEnable()
        {
            if (zone != null) zone.HopeLevelReached += OnLevel;
        }

        private void OnDisable()
        {
            if (zone != null) zone.HopeLevelReached -= OnLevel;
        }

        private void OnLevel(ZoneHope source, int level)
        {
            if (sparks == null) return;
            sparks.Emit(sparksPerLevel * level);
        }
    }
}
