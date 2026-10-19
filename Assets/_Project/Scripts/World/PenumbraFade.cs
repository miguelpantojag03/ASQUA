using System.Collections.Generic;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// "Apaga" visualmente un edificio o farola cuando la Penumbra lo alcanza: durante la
    /// transición lo recorre una escarcha fría (marca clara de "tocado") y después queda
    /// desaturado en un gris casi negro (instancia de material propia, compatible con el SRP Batcher).
    /// También atenúa sus ventanas y farolas. Penumbra le indica cuánto está cubierto.
    /// </summary>
    public class PenumbraFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<PenumbraFade> all = new List<PenumbraFade>();

        [Tooltip("Cuánto se oscurece el color base al quedar cubierto (0-1). 0.92 = casi negro.")]
        [SerializeField, Range(0f, 1f)] private float darkenAmount = 0.92f;
        [Tooltip("Gris oscuro hacia el que se apaga (sin el azul de la noche: se lee como 'muerto').")]
        [SerializeField] private Color shadowTint = new Color(0.055f, 0.055f, 0.065f);
        [Tooltip("Velocidad del fundido (1/s).")]
        [SerializeField] private float fadeSpeed = 0.8f;

        [Header("Escarcha del frente de avance")]
        [Tooltip("Brillo frío que recorre el edificio mientras la Penumbra lo devora (marca clara de 'tocado').")]
        [SerializeField, ColorUsage(false, true)] private Color frostColor = new Color(0.55f, 0.63f, 0.9f);
        [SerializeField] private float frostIntensity = 2.2f;

        /// <summary>Se dispara cuando la Penumbra "se traga" este objeto (cobertura &gt; 50%).</summary>
        public static event System.Action<PenumbraFade> AnyConsumed;
        private bool consumed;

        private readonly List<Material> instances = new List<Material>();
        private readonly List<Color> originalColors = new List<Color>();
        private readonly List<bool> frostable = new List<bool>();
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private WindowLight[] windows;
        private StreetLampToggle[] lamps;
        private float current;
        private float target;

        public static IReadOnlyList<PenumbraFade> All => all;
        public float Amount => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            AnyConsumed = null;
        }

        private void Awake()
        {
            windows = GetComponentsInChildren<WindowLight>(true);
            lamps = GetComponentsInChildren<StreetLampToggle>(true);

            // Una instancia por material compartido (no por renderer) para mantener pocos materiales.
            Dictionary<Material, Material> map = new Dictionary<Material, Material>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<WindowLight>() != null) continue;
                Material shared = r.sharedMaterial;
                if (shared == null || !shared.HasProperty(BaseColorId)) continue;

                if (!map.TryGetValue(shared, out Material instance))
                {
                    instance = new Material(shared) { name = shared.name + " (Penumbra)" };
                    map.Add(shared, instance);
                    instances.Add(instance);
                    originalColors.Add(shared.GetColor(BaseColorId));

                    // Solo los materiales sin emisión propia reciben la escarcha (no se pisa la de farolas).
                    bool canFrost = !shared.IsKeywordEnabled("_EMISSION") && shared.HasProperty(EmissionColorId);
                    frostable.Add(canFrost);
                    if (canFrost)
                    {
                        instance.EnableKeyword("_EMISSION");
                        instance.SetColor(EmissionColorId, Color.black);
                    }
                }
                r.sharedMaterial = instance;
            }
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        private void OnDestroy()
        {
            foreach (Material m in instances) Destroy(m);
        }

        /// <summary>Cobertura objetivo (0 = intacto, 1 = devorado por la Penumbra).</summary>
        public void SetTarget(float amount)
        {
            target = Mathf.Clamp01(amount);
        }

        private void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, fadeSpeed * Time.deltaTime);

            float eased = current * current * (3f - 2f * current);
            // La escarcha sube y baja durante la transición: máxima a mitad de camino, nula al final.
            Color frost = frostColor * (frostIntensity * Mathf.Sin(eased * Mathf.PI));

            for (int i = 0; i < instances.Count; i++)
            {
                if (frostable[i]) instances[i].SetColor(EmissionColorId, frost);

                // Primero se desatura (pierde el color) y luego se hunde hacia un gris casi negro.
                Color original = originalColors[i];
                float g = original.grayscale;
                Color gray = new Color(g, g, g, original.a);
                Color dead = Color.Lerp(gray, shadowTint, darkenAmount);
                dead.a = original.a;
                instances[i].SetColor(BaseColorId, Color.Lerp(original, dead, eased));
            }

            if (!consumed && current > 0.5f)
            {
                consumed = true;
                AnyConsumed?.Invoke(this);
            }

            foreach (WindowLight window in windows) window.SetPenumbraDim(eased);
            foreach (StreetLampToggle lamp in lamps) lamp.SetPenumbraDim(eased);
        }
    }
}
