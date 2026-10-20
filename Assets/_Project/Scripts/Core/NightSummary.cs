using System.Collections.Generic;
using System.Text;
using Ascua.Quests;
using Ascua.World;

namespace Ascua.Core
{
    /// <summary>
    /// Datos del resumen de una noche (lo que se muestra en NightSummaryPanel y lo que, en la Fase 4,
    /// leerá el AIDirector para decidir la siguiente noche): zonas salvadas y perdidas, misiones y Vínculos.
    /// </summary>
    public class NightSummary
    {
        public int NightNumber;
        public bool EndedEarly;
        public readonly List<string> SavedZones = new List<string>();
        public readonly List<string> LostZones = new List<string>();
        public readonly List<string> CompletedQuests = new List<string>();
        public readonly List<string> MissedQuests = new List<string>();
        public readonly List<string> Bonds = new List<string>();

        /// <summary>Recoge el estado actual del mundo al terminar la noche.</summary>
        public static NightSummary Capture(int nightNumber, bool endedEarly)
        {
            NightSummary summary = new NightSummary { NightNumber = nightNumber, EndedEarly = endedEarly };

            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].IsDead) summary.LostZones.Add(zones[i].DisplayName);
                else summary.SavedZones.Add(zones[i].DisplayName);
            }

            QuestManager manager = QuestManager.Instance;
            if (manager != null)
            {
                foreach (QuestRuntime q in manager.Quests)
                {
                    if (q.State == QuestState.Completada) summary.CompletedQuests.Add(q.Data.title);
                    else if (q.State != QuestState.Bloqueada) summary.MissedQuests.Add(q.Data.title);
                }
                summary.Bonds.Add("Mina " + manager.GetBond("mina") + "/" + QuestManager.MaxBond);
                summary.Bonds.Add("Teo " + manager.GetBond("teo") + "/" + QuestManager.MaxBond);
            }
            return summary;
        }

        /// <summary>Texto listo para el panel de resumen.</summary>
        public string ToDisplayText()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Zonas salvadas: ").Append(SavedZones.Count).Append('/').Append(SavedZones.Count + LostZones.Count);
            if (LostZones.Count > 0) sb.Append("   <color=#DB7340>(se apagó: ").Append(string.Join(", ", LostZones)).Append(")</color>");
            sb.Append('\n');
            int totalQuests = CompletedQuests.Count + MissedQuests.Count;
            if (totalQuests > 0) sb.Append("Misiones cumplidas: ").Append(CompletedQuests.Count).Append('/').Append(totalQuests).Append('\n');
            if (Bonds.Count > 0) sb.Append("Vínculos: ").Append(string.Join("  ·  ", Bonds));
            return sb.ToString();
        }
    }
}
