using System;
using System.Collections.Generic;

namespace Ascua.LevelGen
{
    /// <summary>Una luz candidata para el Apagavelas, como datos planos (sin Unity).</summary>
    public struct SnuffCandidate
    {
        /// <summary>Orden fijo en la lista de la noche (desempate).</summary>
        public int Id;
        public bool IsLit;
        /// <summary>Esperanza normalizada (0-1) de su zona.</summary>
        public float ZoneHope01;
        public float X;
        public float Z;

        public SnuffCandidate(int id, bool isLit, float zoneHope01, float x, float z)
        {
            Id = id;
            IsLit = isLit;
            ZoneHope01 = zoneHope01;
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Apagavelas (lógica pura, testeable): cuántos salen cada noche, cuándo y a qué luz van.
    /// </summary>
    public static class SnuffPlanner
    {
        /// <summary>snuffers = -1: lo decide la noche.</summary>
        public const int Auto = -1;
        public const int MaxSnuffers = 8;
        public const float Earliest = 0.15f;
        public const float Latest = 0.85f;

        /// <summary>Automático: noche 1 = 0, 2 = 1, 3 = 2, 4 = 3, 5 o más = 4. Si no, lo que diga el ADN (máx. 8).</summary>
        public static int CountForNight(int night, int dnaCount)
        {
            if (dnaCount >= 0) return Math.Min(dnaCount, MaxSnuffers);
            if (night <= 1) return 0;
            if (night >= 5) return 4;
            return night - 1;
        }

        /// <summary>
        /// Momentos de aparición (0-1, ordenados) entre Earliest y Latest. Reutiliza HuntSchedule.Compute
        /// (mismo reparto y separación, reescalado). Si hay más Apagavelas que huecos, salen varios a la vez.
        /// </summary>
        public static List<float> Schedule(int count, int seed)
        {
            List<float> moments = new List<float>();
            if (count <= 0) return moments;

            int slots = Math.Min(count, HuntSchedule.MaxHunts);
            List<float> baseMoments = HuntSchedule.Compute(slots, seed);
            float span = HuntSchedule.Latest - HuntSchedule.Earliest;
            for (int i = 0; i < count; i++)
            {
                float m = baseMoments[i % slots];
                float t = span > 0f ? (m - HuntSchedule.Earliest) / span : 0f;
                float moment = Earliest + t * (Latest - Earliest);
                moments.Add(Math.Max(Earliest, Math.Min(Latest, moment)));
            }
            moments.Sort();
            return moments;
        }

        /// <summary>
        /// Puntuación de una luz (más alta = mejor blanco): primero encendida, luego en la zona con menos
        /// Esperanza y después la más cercana.
        /// </summary>
        public static float ScoreTarget(SnuffCandidate candidate, float fromX, float fromZ)
        {
            float dx = candidate.X - fromX;
            float dz = candidate.Z - fromZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            float hope = Math.Max(0f, Math.Min(1f, candidate.ZoneHope01));
            return (candidate.IsLit ? 1000f : 0f) + (1f - hope) * 100f - Math.Min(distance, 190f) * 0.5f;
        }

        /// <summary>Índice del mejor blanco (-1 si la lista está vacía). Empate: el de menor Id.</summary>
        public static int PickBest(IList<SnuffCandidate> candidates, float fromX, float fromZ)
        {
            int best = -1;
            float bestScore = float.MinValue;
            if (candidates == null) return best;
            for (int i = 0; i < candidates.Count; i++)
            {
                float score = ScoreTarget(candidates[i], fromX, fromZ);
                bool better = best < 0 || score > bestScore || (score == bestScore && candidates[i].Id < candidates[best].Id);
                if (!better) continue;
                best = i;
                bestScore = score;
            }
            return best;
        }
    }
}
