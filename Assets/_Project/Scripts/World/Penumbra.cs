using Ascua.Core;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// La Penumbra: un anillo de niebla oscura que se cierra desde los bordes del mapa hacia
    /// su centro a lo largo de la noche. Su avance sigue una curva editable en el Inspector.
    /// Además espesa la niebla global y "apaga" (PenumbraFade) lo que va tocando.
    /// </summary>
    public class Penumbra : MonoBehaviour
    {
        public static Penumbra Instance { get; private set; }

        [Header("Avance")]
        [Tooltip("Radio inicial (m) medido desde este objeto: fuera del mapa.")]
        [SerializeField] private float startRadius = 60f;
        [Tooltip("Radio al terminar la noche.")]
        [SerializeField] private float endRadius = 14f;
        [Tooltip("X = progreso de la noche (0-1), Y = avance de la Penumbra (0-1). Curva con pendiente creciente = acelera.")]
        [SerializeField] private AnimationCurve advanceCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.5f, 0.25f, 1f, 1f),
            new Keyframe(1f, 1f, 2f, 0f));
        [Tooltip("Si no hay NightManager se usa esta duración (s).")]
        [SerializeField] private float fallbackDuration = 60f;
        [Tooltip("Distancia (m) en la que el borde pasa de no tocar a cubrir del todo (fundido de PenumbraFade).")]
        [SerializeField] private float edgeSoftness = 4f;

        [Header("Niebla global")]
        [SerializeField] private float fogDensityAtEnd = 0.055f;
        [SerializeField] private Color fogColorAtEnd = new Color(0.043f, 0.059f, 0.141f);

        [Header("Muro de niebla")]
        [Tooltip("Tarjetas de niebla que forman el borde visible (las crea el constructor de escenas).")]
        [SerializeField] private Transform[] wallCards;
        [SerializeField] private int rings = 2;
        [SerializeField] private float ringSpacing = 4f;
        [SerializeField] private float cardHeight = 10f;
        [SerializeField] private float cardOverlap = 1.6f;

        private float startFogDensity;
        private Color startFogColor;
        private float currentRadius;

        /// <summary>Radio actual de la zona segura (todo lo que está más lejos está cubierto).</summary>
        public float CurrentRadius => currentRadius;
        /// <summary>Avance de la Penumbra (0 = recién empieza, 1 = radio final).</summary>
        public float Advance { get; private set; }

        private void Awake()
        {
            Instance = this;
            currentRadius = startRadius;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            startFogDensity = RenderSettings.fogDensity;
            startFogColor = RenderSettings.fogColor;
            UpdateWall();
        }

        private void Update()
        {
            float progress = NightManager.Instance != null
                ? NightManager.Instance.Progress01
                : Mathf.Clamp01(Time.timeSinceLevelLoad / fallbackDuration);

            Advance = Mathf.Clamp01(advanceCurve.Evaluate(progress));
            currentRadius = Mathf.Lerp(startRadius, endRadius, Advance);

            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, fogDensityAtEnd, Advance);
            RenderSettings.fogColor = Color.Lerp(startFogColor, fogColorAtEnd, Advance);

            UpdateWall();
            UpdateFades();
        }

        /// <summary>True si el punto ya está dentro de la Penumbra.</summary>
        public bool IsCovered(Vector3 point)
        {
            return FlatDistance(point) > currentRadius;
        }

        /// <summary>Cobertura suave de un punto: 0 = fuera, 1 = dentro (con fundido de edgeSoftness).</summary>
        public float GetCoverage(Vector3 point)
        {
            return Mathf.Clamp01((FlatDistance(point) - currentRadius) / edgeSoftness + 0.5f);
        }

        /// <summary>Qué parte de un círculo (centro, radio) cubre la Penumbra: 0 = nada, 1 = todo.</summary>
        public float GetOverlap(Vector3 center, float areaRadius)
        {
            float distance = FlatDistance(center);
            return Mathf.Clamp01((distance + areaRadius - currentRadius) / (2f * areaRadius));
        }

        private float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void UpdateFades()
        {
            var fades = PenumbraFade.All;
            for (int i = 0; i < fades.Count; i++)
            {
                fades[i].SetTarget(GetCoverage(fades[i].transform.position));
            }
        }

        /// <summary>Coloca las tarjetas de niebla en anillos alrededor del centro, mirando hacia dentro.</summary>
        private void UpdateWall()
        {
            if (wallCards == null || wallCards.Length == 0) return;

            int perRing = Mathf.Max(1, wallCards.Length / Mathf.Max(1, rings));
            for (int i = 0; i < wallCards.Length; i++)
            {
                Transform card = wallCards[i];
                if (card == null) continue;

                int ring = i / perRing;
                int index = i % perRing;
                float ringRadius = currentRadius + ring * ringSpacing;
                float angle = (index + ring * 0.5f) / perRing * Mathf.PI * 2f;

                Vector3 position = transform.position + new Vector3(Mathf.Cos(angle) * ringRadius, cardHeight * 0.5f - 1f, Mathf.Sin(angle) * ringRadius);
                card.position = position;

                Vector3 toCenter = transform.position - position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.001f) card.rotation = Quaternion.LookRotation(-toCenter.normalized, Vector3.up);

                float width = 2f * Mathf.PI * ringRadius / perRing * cardOverlap;
                card.localScale = new Vector3(width, cardHeight, 1f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.35f, 0.7f, 0.9f);
            DrawCircle(Application.isPlaying ? currentRadius : startRadius);
            Gizmos.color = new Color(0.6f, 0.2f, 0.3f, 0.9f);
            DrawCircle(endRadius);
        }

        private void DrawCircle(float r)
        {
            const int segments = 64;
            Vector3 previous = transform.position + new Vector3(r, 0.2f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
