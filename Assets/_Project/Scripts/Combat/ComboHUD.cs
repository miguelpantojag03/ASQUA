using UnityEngine;
using UnityEngine.UI;

namespace Ascua.Combat
{
    /// <summary>
    /// Indicador sutil de combo: 3 puntos de luz bajo el centro de la pantalla que se encienden con
    /// cada golpe que conecta. Aparece solo en combate y se desvanece 1.2 s después del último golpe.
    /// </summary>
    public class ComboHUD : MonoBehaviour
    {
        [SerializeField] private LightAttack attack;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image[] dots;
        [SerializeField] private Color litColor = new Color(1f, 0.82f, 0.5f, 1f);
        [SerializeField] private Color unlitColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private float hideAfter = 1.2f;

        private float visibleTimer;
        private float[] pop;

        private void Awake()
        {
            pop = new float[dots != null ? dots.Length : 0];
            if (group != null) group.alpha = 0f;
            SetLit(0);
        }

        private void OnEnable()
        {
            if (attack != null) attack.ComboChanged += OnComboChanged;
        }

        private void OnDisable()
        {
            if (attack != null) attack.ComboChanged -= OnComboChanged;
        }

        private void OnComboChanged(int step, int connected)
        {
            if (step > 0) visibleTimer = hideAfter;
            SetLit(connected);
            if (connected > 0 && connected <= pop.Length) pop[connected - 1] = 1f;
        }

        private void SetLit(int count)
        {
            if (dots == null) return;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] != null) dots[i].color = i < count ? litColor : unlitColor;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (visibleTimer > 0f) visibleTimer -= dt;
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, visibleTimer > 0f ? 1f : 0f, dt * 5f);

            // Cada punto "late" al encenderse.
            for (int i = 0; i < pop.Length; i++)
            {
                if (dots[i] == null) continue;
                pop[i] = Mathf.MoveTowards(pop[i], 0f, dt * 5f);
                dots[i].rectTransform.localScale = Vector3.one * (1f + 0.5f * pop[i]);
            }
        }
    }
}
