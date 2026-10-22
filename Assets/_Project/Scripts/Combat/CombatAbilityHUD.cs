using Ascua.Core;
using TMPro;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Texto compacto en la fila del Farol con lo que Ilse sabe hacer en combate:
    /// "VELA · G" (o su recarga), "CARGA" si tiene la Lupa de Mina, "CONTRA" con el Reloj de Nadir
    /// y "VARA" con la Vara de luz. Oculto hasta que se desbloquea algo.
    /// </summary>
    public class CombatAbilityHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private BondCombatAbilities abilities;
        [SerializeField] private LanternStaff staff;
        [SerializeField] private float refreshInterval = 0.2f;

        private float timer;
        private readonly System.Text.StringBuilder builder = new System.Text.StringBuilder(64);

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval || label == null) return;
            timer = 0f;

            builder.Clear();
            if (abilities != null && abilities.CandleUnlocked)
            {
                float cd = abilities.CandleCooldownRemaining;
                builder.Append(cd > 0f ? "VELA " + Mathf.CeilToInt(cd) + " s" : "VELA · G");
            }
            if (GameProgress.HasRecuerdo(LightAttack.LupaMinaId)) Separator().Append("CARGA");
            if (GameProgress.HasRecuerdo(PlayerCombatState.RelojNadirId)) Separator().Append("CONTRA");
            if (staff != null && staff.IsUnlocked) Separator().Append("VARA");
            label.text = builder.ToString();
        }

        private System.Text.StringBuilder Separator()
        {
            if (builder.Length > 0) builder.Append("  ·  ");
            return builder;
        }
    }
}
