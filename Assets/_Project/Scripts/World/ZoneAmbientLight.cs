using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Luz de relleno fría y suave sobre una zona, proporcional a su Esperanza: una zona cuidada se
    /// ve mejor (se leen fachadas y suelo), una zona apagada o devorada por la Penumbra sigue a oscuras.
    /// Así se gana legibilidad sin perder el contraste frío/cálido.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class ZoneAmbientLight : MonoBehaviour
    {
        [SerializeField] private ZoneHope zone;
        [SerializeField] private Color color = new Color(0.62f, 0.72f, 1f);
        [Tooltip("Intensidad con la zona al 100%.")]
        [SerializeField] private float maxIntensity = 1.1f;
        [Tooltip("Por debajo de esta Esperanza (0-1) no hay relleno.")]
        [SerializeField, Range(0f, 1f)] private float startAt = 0.4f;
        [SerializeField] private float smoothing = 1.5f;

        private Light fill;
        private float current;

        private void Awake()
        {
            fill = GetComponent<Light>();
            fill.color = color;
            fill.shadows = LightShadows.None;
            if (zone == null) zone = GetComponentInParent<ZoneHope>();
        }

        private void Update()
        {
            float target = 0f;
            if (zone != null && !zone.IsDead)
            {
                target = Mathf.InverseLerp(startAt, 1f, zone.Normalized) * maxIntensity * (1f - zone.PenumbraOverlap);
            }
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            fill.intensity = current;
            fill.enabled = current > 0.01f;
        }
    }
}
