using System.Collections.Generic;
using Ascua.Core;

namespace Ascua.AILevelGen
{
    /// <summary>
    /// Perfil anónimo del jugador: cómo le fue en cada noche (solo números, nada personal).
    /// De aquí sale la "habilidad" (0-1) que AIDirector usa para subir o bajar la dificultad.
    /// Vive en memoria y se reinicia con cada partida nueva.
    /// </summary>
    public static class PlayerProfileTracker
    {
        public struct NightProfile
        {
            public int Night;
            public float ZonesSavedRatio;
            public float QuestRatio;
            public bool EndedEarly;
            /// <summary>0-1: mezcla de zonas salvadas y misiones cumplidas (la mitad si Vesper se apagó antes del alba).</summary>
            public float Performance;
        }

        private static readonly List<NightProfile> history = new List<NightProfile>();

        public static IReadOnlyList<NightProfile> History => history;

        /// <summary>
        /// Habilidad actual del jugador (0 = le va mal, 1 = le va muy bien). Pesa más la última noche.
        /// Sin historial vale 0.5 (neutro).
        /// </summary>
        public static float Skill
        {
            get
            {
                if (history.Count == 0) return 0.5f;
                float last = history[history.Count - 1].Performance;
                if (history.Count == 1) return last;
                float before = history[history.Count - 2].Performance;
                return last * 0.6f + before * 0.4f;
            }
        }

        /// <summary>Apunta cómo terminó una noche. Si se repite la noche, la nueva medición sustituye a la anterior.</summary>
        public static void Record(NightSummary summary)
        {
            if (summary == null) return;

            int zones = summary.SavedZones.Count + summary.LostZones.Count;
            int quests = summary.CompletedQuests.Count + summary.MissedQuests.Count;

            NightProfile profile = new NightProfile();
            profile.Night = summary.NightNumber;
            profile.EndedEarly = summary.EndedEarly;
            profile.ZonesSavedRatio = zones > 0 ? (float)summary.SavedZones.Count / zones : 0f;
            profile.QuestRatio = quests > 0 ? (float)summary.CompletedQuests.Count / quests : 0.5f;
            float performance = profile.ZonesSavedRatio * 0.65f + profile.QuestRatio * 0.35f;
            if (summary.EndedEarly) performance *= 0.5f;
            profile.Performance = performance < 0f ? 0f : (performance > 1f ? 1f : performance);

            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Night == profile.Night) history.RemoveAt(i);
            }
            history.Add(profile);
        }

        public static void ResetAll()
        {
            history.Clear();
        }
    }
}
