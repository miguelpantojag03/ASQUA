using System.Collections.Generic;
using Ascua.Core;
using Ascua.Endings;
using Ascua.Quests;

namespace Ascua.Narrative
{
    /// <summary>
    /// Puente entre el juego y EndingResolver (lógica pura): reúne los datos reales de la partida
    /// (zonas de la última noche, Vínculos, Trato, Diario) y decide el final.
    /// </summary>
    public static class EndingContext
    {
        /// <summary>Id de la misión del Trato del Heraldo (hoy el único Trato del juego).</summary>
        public const string TratoQuestId = "heraldo_trato_mercado";

        public const string MinaId = "mina";
        public const string TeoId = "teo";
        public const string PerpetuaId = "perpetua";

        /// <summary>Datos de la partida al terminar la última noche.</summary>
        /// <param name="summary">NightManager.LastSummary (puede ser null).</param>
        /// <param name="fallback">Resultado de la noche, por si no hay resumen.</param>
        public static EndingInput Collect(NightSummary summary, NightResult fallback)
        {
            EndingInput input = new EndingInput
            {
                SavedZones = summary != null ? summary.SavedZones.Count : fallback.ZonesSaved,
                TotalZones = summary != null ? summary.SavedZones.Count + summary.LostZones.Count : fallback.ZonesTotal,
                TratosAccepted = GameProgress.IsQuestCompleted(TratoQuestId) ? 1 : 0,
                DiaryPages = GameProgress.FoundPages.Count,
                NamedCorvin = false   // la marca aún no existe en el juego
            };

            QuestManager quests = QuestManager.Instance;
            if (quests != null)
            {
                input.BondMina = quests.GetBond(MinaId);
                input.BondTeo = quests.GetBond(TeoId);
                input.BondPerpetua = quests.GetBond(PerpetuaId);
            }
            return input;
        }

        /// <summary>Final que corresponde a esos datos.</summary>
        public static EndingInfo Resolve(EndingInput input)
        {
            return EndingCatalog.Get(EndingResolver.Resolve(input));
        }

        /// <summary>Líneas de los personajes con Vínculo 3 o más.</summary>
        public static List<string> CompanionLines(EndingInput input)
        {
            return EndingCatalog.CompanionLines(input.BondMina, input.BondTeo, input.BondPerpetua);
        }
    }
}
