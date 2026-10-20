using Ascua.Core;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// La Penumbra: un anillo de niebla que se cierra desde los bordes del mapa hacia su centro
    /// a lo largo de la noche, según una curva editable. El anillo exterior es una bruma fría y
    /// pálida (para que el borde se lea contra la noche) y el interior, casi negro.
    /// También espesa la niebla global y "apaga" (PenumbraFade) los edificios que alcanza.
    /// </summary>
    public class Penumbra : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static Penumbra Instance { get; private set; }

        [Header("Avance (lo sobrescribe el ritmo de NightManager salvo en 'Personalizado')")]
        [Tooltip("Radio inicial (m) medido desde este objeto: fuera del mapa.")]
        [SerializeField] private float startRadius = 60f;
        [Tooltip("Radio al terminar la noche.")]
        [SerializeField] private float endRadius = 16f;
        [Tooltip("X = progreso de la noche (0-1), Y = avance de la Penumbra (0-1). Pendiente creciente = acelera.")]
        [SerializeField] private AnimationCurve advanceCurve = SubtleCurve();
        [Tooltip("Si no hay NightManager se usa esta duración (s).")]
        [SerializeField] private float fallbackDuration = 60f;
        [Tooltip("Distancia (m) en la que el borde pasa de no tocar a cubrir del todo (fundido de PenumbraFade).")]
        [SerializeField] private float edgeSoftness = 4f;

        [Header("Niebla global")]
        [SerializeField] private float fogDensityAtEnd = 0.05f;
        [SerializeField] private Color fogColorAtEnd = new Color(0.043f, 0.059f, 0.141f);

        [Header("Muro de niebla")]
        [Tooltip("Tarjetas de niebla del borde visible (las crea el constructor de escenas). Primera mitad = anillo exterior.")]
        [SerializeField] private Transform[] wallCards;
        [SerializeField] private int rings = 2;
        [SerializeField] private float ringSpacing = 5f;
        [SerializeField] private float cardHeight = 12f;
        [SerializeField] private float cardOverlap = 1.6f;
        [Tooltip("Las tarjetas se desvanecen al acercarse la cámara (evita sobre-dibujado a pantalla completa).")]
        [SerializeField] private float cameraFadeDistance = 10f;

        private float startFogDensity;
        private Color startFogColor;
        private float currentRadius;
        private Renderer[] cardRenderers;
        private Color[] cardBaseColors;
        private MaterialPropertyBlock propertyBlock;
        private Transform cameraTransform;

        /// <summary>Radio actual de la zona segura (todo lo que está más lejos está cubierto).</summary>
        public float CurrentRadius => currentRadius;
        /// <summary>Avance de la Penumbra (0 = recién empieza, 1 = radio final).</summary>
        public float Advance { get; private set; }

        private void Awake()
        {
            Instance = this;
            currentRadius = startRadius;
            propertyBlock = new MaterialPropertyBlock();

            int count = wallCards != null ? wallCards.Length : 0;
            cardRenderers = new Renderer[count];
            cardBaseColors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                if (wallCards[i] == null) continue;
                cardRenderers[i] = wallCards[i].GetComponent<Renderer>();
                if (cardRenderers[i] != null) cardBaseColors[i] = cardRenderers[i].sharedMaterial.GetColor(BaseColorId);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            startFogDensity = RenderSettings.fogDensity;
            startFogColor = RenderSettings.fogColor;
            if (Camera.main != null) cameraTransform = Camera.main.transform;
            UpdateWall();
        }

        private void Update()
        {
            float progress = NightManager.Instance != null
                ? NightManager.Instance.Progress01
                : Mathf.Clamp01(Time.timeSinceLevelLoad / fallbackDuration);

            Advance = Mathf.Clamp01(advanceCurve.Evaluate(progress));
            // La ráfaga (PenumbraSurge) empuja el muro unos metros más hacia dentro durante unos segundos.
            currentRadius = Mathf.Max(endRadius * 0.6f, Mathf.Lerp(startRadius, endRadius, Advance) - surgeOffset);

            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, fogDensityAtEnd, Advance);
            RenderSettings.fogColor = Color.Lerp(startFogColor, fogColorAtEnd, Advance);

            UpdateWall();
            UpdateFades();
        }

        /// <summary>
        /// Aplica los valores de un ritmo predefinido. Lo llama NightManager en Awake.
        /// 'Personalizado' respeta lo que haya en el Inspector.
        /// </summary>
        public void ApplyPacing(NightPacing pacing)
        {
            switch (pacing)
            {
                case NightPacing.Sutil:
                    startRadius = 60f;
                    endRadius = 16f;
                    advanceCurve = SubtleCurve();
                    fogDensityAtEnd = 0.05f;
                    edgeSoftness = 4f;
                    break;
                case NightPacing.DemoRapida:
                    startRadius = 50f;
                    endRadius = 10f;
                    advanceCurve = DemoCurve();
                    fogDensityAtEnd = 0.07f;
                    edgeSoftness = 3f;
                    break;
            }
            currentRadius = startRadius;
        }

        private float surgeOffset;

        /// <summary>Metros extra de avance (ráfaga de urgencia). 0 = ritmo normal.</summary>
        public void SetSurgeOffset(float meters)
        {
            surgeOffset = Mathf.Max(0f, meters);
        }

        /// <summary>Valores a medida (los usa el NightQuestSet de cada noche).</summary>
        public void ApplyCustom(float start, float end, bool fastCurve)
        {
            startRadius = start;
            endRadius = end;
            advanceCurve = fastCurve ? DemoCurve() : SubtleCurve();
            currentRadius = startRadius;
        }

        /// <summary>Lenta al principio; se nota en el último tercio de la noche.</summary>
        public static AnimationCurve SubtleCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0.3f),
                new Keyframe(0.67f, 0.45f, 1.3f, 1.3f),
                new Keyframe(1f, 1f, 2.2f, 0f));
        }

        /// <summary>Casi lineal: entra en el mapa en pocos segundos (para enseñarla en clase).</summary>
        public static AnimationCurve DemoCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 1.6f, 1.6f),
                new Keyframe(1f, 1f, 0.7f, 0f));
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

        /// <summary>Coloca las tarjetas en anillos alrededor del centro, mirando hacia dentro, con un leve oleaje.</summary>
        private void UpdateWall()
        {
            if (wallCards == null || wallCards.Length == 0) return;

            int perRing = Mathf.Max(1, wallCards.Length / Mathf.Max(1, rings));
            float time = Time.time;

            for (int i = 0; i < wallCards.Length; i++)
            {
                Transform card = wallCards[i];
                if (card == null) continue;

                int ring = i / perRing;
                int index = i % perRing;
                float ringRadius = currentRadius + ring * ringSpacing;
                float angle = (index + ring * 0.5f) / perRing * Mathf.PI * 2f;
                float swell = Mathf.Sin(time * 0.4f + i * 1.7f);

                Vector3 position = transform.position + new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    cardHeight * 0.5f - 1.5f + swell * 0.6f,
                    Mathf.Sin(angle) * ringRadius);
                card.position = position;

                Vector3 toCenter = transform.position - position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.001f) card.rotation = Quaternion.LookRotation(-toCenter.normalized, Vector3.up);

                float width = 2f * Mathf.PI * ringRadius / perRing * cardOverlap;
                card.localScale = new Vector3(width, cardHeight * (1f + swell * 0.08f), 1f);

                ApplyCameraFade(i, position);
            }
        }

        private void ApplyCameraFade(int index, Vector3 position)
        {
            Renderer r = cardRenderers != null && index < cardRenderers.Length ? cardRenderers[index] : null;
            if (r == null || cameraTransform == null) return;

            float distance = Vector3.Distance(cameraTransform.position, position);
            float fade = Mathf.Clamp01((distance - 2f) / Mathf.Max(0.1f, cameraFadeDistance - 2f));
            Color c = cardBaseColors[index];
            c.a *= fade;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            r.SetPropertyBlock(propertyBlock);
            r.enabled = fade > 0.01f;
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
