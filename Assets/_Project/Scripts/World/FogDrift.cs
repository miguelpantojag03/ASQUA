using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Banco de niebla barato: un quad con textura suave que deriva lentamente,
    /// "respira" en opacidad y se reposiciona al salir de su área (con fundido en bordes).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class FogDrift : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Vector3 velocity = new Vector3(0.3f, 0f, 0.05f);
        [Tooltip("Centro del área (mundo) en la que deriva la niebla.")]
        [SerializeField] private Vector3 areaCenter = Vector3.zero;
        [Tooltip("Tamaño del área. Un eje con 0 no se envuelve.")]
        [SerializeField] private Vector3 areaSize = new Vector3(40f, 0f, 40f);
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.12f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 0.35f;
        [SerializeField] private float pulseSpeed = 0.15f;
        [SerializeField] private Color tint = new Color(0.55f, 0.62f, 0.85f, 1f);

        private Renderer fogRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float seed;

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(Vector3 newVelocity, Vector3 center, Vector3 size, float minAlpha, float maxAlpha, Color color)
        {
            velocity = newVelocity;
            areaCenter = center;
            areaSize = size;
            alphaMin = minAlpha;
            alphaMax = maxAlpha;
            tint = color;
        }

        private void Awake()
        {
            fogRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            Vector3 p = transform.position + velocity * Time.deltaTime;
            p.x = Wrap(p.x, areaCenter.x, areaSize.x);
            p.y = Wrap(p.y, areaCenter.y, areaSize.y);
            p.z = Wrap(p.z, areaCenter.z, areaSize.z);
            transform.position = p;

            float alpha = Mathf.Lerp(alphaMin, alphaMax, Mathf.PerlinNoise(Time.time * pulseSpeed, seed));
            alpha *= EdgeFade(p.x, areaCenter.x, areaSize.x) * EdgeFade(p.z, areaCenter.z, areaSize.z);

            Color c = tint;
            c.a = alpha;
            fogRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            fogRenderer.SetPropertyBlock(propertyBlock);
        }

        private static float Wrap(float value, float center, float size)
        {
            if (size <= 0f) return value;
            float half = size * 0.5f;
            if (value > center + half) return value - size;
            if (value < center - half) return value + size;
            return value;
        }

        // Atenúa la niebla en el 25% exterior del área para que el salto de posición no se note.
        private static float EdgeFade(float value, float center, float size)
        {
            if (size <= 0f) return 1f;
            float normalized = 1f - Mathf.Abs(value - center) / (size * 0.5f);
            return Mathf.Clamp01(normalized * 4f);
        }
    }
}
