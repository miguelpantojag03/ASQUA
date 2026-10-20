using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Brasa: la chispa de Mina. Tras su regreso (Noche 1) revolotea alrededor de la cabeza de Mina
    /// el resto de la partida, con un leve parpadeo de ascua viva.
    /// </summary>
    public class BrasaSpark : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.35f, 0f);
        [SerializeField] private float orbitRadius = 0.45f;
        [SerializeField] private float orbitSpeed = 1.6f;
        [SerializeField] private Renderer glow;
        [SerializeField] private Color color = new Color(1f, 0.48f, 0.24f);
        [SerializeField] private float intensity = 4f;

        private MaterialPropertyBlock propertyBlock;
        private float angle;

        public void SetTarget(Transform newTarget, Vector3 newOffset)
        {
            target = newTarget;
            offset = newOffset;
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            angle += orbitSpeed * Time.deltaTime;
            Vector3 orbit = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 2.3f) * 0.25f, Mathf.Sin(angle)) * orbitRadius;
            transform.position = Vector3.Lerp(transform.position, target.position + offset + orbit, 1f - Mathf.Exp(-6f * Time.deltaTime));

            if (glow == null) return;
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 3.1f);
            glow.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, color * (intensity * flicker));
            glow.SetPropertyBlock(propertyBlock);
        }
    }
}
