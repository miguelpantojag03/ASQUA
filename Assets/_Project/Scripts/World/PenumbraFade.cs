using System.Collections.Generic;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// "Apaga" visualmente un edificio o farola cuando la Penumbra lo alcanza:
    /// oscurece sus materiales (instancia propia por objeto, compatible con el SRP Batcher)
    /// y atenúa sus ventanas y farolas. Penumbra le indica cuánto está cubierto.
    /// </summary>
    public class PenumbraFade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<PenumbraFade> all = new List<PenumbraFade>();

        [Tooltip("Cuánto se oscurece el color base al quedar cubierto (0-1).")]
        [SerializeField, Range(0f, 1f)] private float darkenAmount = 0.65f;
        [Tooltip("Tono hacia el que se oscurece.")]
        [SerializeField] private Color shadowTint = new Color(0.05f, 0.06f, 0.13f);
        [Tooltip("Velocidad del fundido (1/s).")]
        [SerializeField] private float fadeSpeed = 0.6f;

        private readonly List<Material> instances = new List<Material>();
        private readonly List<Color> originalColors = new List<Color>();
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
            for (int i = 0; i < instances.Count; i++)
            {
                Color original = originalColors[i];
                Color dark = Color.Lerp(original, shadowTint, darkenAmount);
                dark.a = original.a;
                instances[i].SetColor(BaseColorId, Color.Lerp(original, dark, eased));
            }

            foreach (WindowLight window in windows) window.SetPenumbraDim(eased);
            foreach (StreetLampToggle lamp in lamps) lamp.SetPenumbraDim(eased);
        }
    }
}
