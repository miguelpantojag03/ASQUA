using System.Collections;
using Ascua.Quests;
using Ascua.World;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Noche 5 · "Los farolillos" (al empezar la noche). Cada personaje con Vínculo 3 o más enciende su
    /// farolillo propio junto a su NPC (BondLantern): una luz cálida de ~6 m donde la Esperanza de la zona
    /// más cercana sube mientras Ilse está dentro. Es la recompensa visible de los Vínculos antes del final;
    /// los de Vínculo bajo no lo encienden, y así se ve qué relaciones quedaron descuidadas.
    /// </summary>
    public partial class NightEventDirector
    {
        private const int LanternBond = 3;
        private const float LanternGap = 1.4f;
        private static readonly string[] LanternOrder = { EndingContext.MinaId, EndingContext.TeoId, EndingContext.PerpetuaId };

        partial void BeginFarolillos()
        {
            StartCoroutine(FarolillosRoutine());
        }

        private IEnumerator FarolillosRoutine()
        {
            QuestManager manager = QuestManager.Instance;
            int lit = 0;
            foreach (string id in LanternOrder)
            {
                NpcInteractable npc = NpcInteractable.Find(id);
                if (npc == null || manager == null || manager.GetBond(id) < LanternBond) continue;

                BondLantern.LightFor(npc);
                Toast(npc.DisplayName + " enciende su farolillo.", 2.4f);
                lit++;

                // Uno tras otro, para que se vea cada uno.
                float wait = LanternGap;
                while (wait > 0f)
                {
                    wait -= Time.deltaTime;
                    yield return null;
                }
            }

            if (lit == 0) Toast("Esta noche nadie enciende un farolillo por ti.", 3f);

            float hold = 1f;
            while (hold > 0f)
            {
                hold -= Time.deltaTime;
                yield return null;
            }
            FinishEvent();
        }
    }
}
