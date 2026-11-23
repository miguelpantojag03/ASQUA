using System;
using System.Collections.Generic;

namespace Ascua.LevelGen
{
    /// <summary>
    /// Cuándo caza el Heraldo (lógica pura, sin Unity, testeable).
    /// Los momentos son fracciones de la noche (0-1) entre Earliest y Latest, separados al menos MinGap.
    /// Misma semilla = mismos momentos (al reintentar una noche, las cacerías caen igual).
    /// </summary>
    public static class HuntSchedule
    {
        public const float Earliest = 0.30f;
        public const float Latest = 0.90f;
        public const float MinGap = 0.15f;
        /// <summary>Lo máximo que cabe en [Earliest, Latest] con MinGap entre cada una.</summary>
        public const int MaxHunts = 5;

        /// <summary>
        /// Cacerías de una noche: huntCount &gt;= 0 lo fija el nivel; -1 = automático
        /// (noches 1-2: 0, noche 3: 1, noche 4 o más: 2).
        /// </summary>
        public static int CountForNight(int nightNumber, int huntCount)
        {
            if (huntCount >= 0) return Math.Min(huntCount, MaxHunts);
            if (nightNumber <= 2) return 0;
            if (nightNumber == 3) return 1;
            return 2;
        }

        /// <summary>
        /// Momentos de la noche (0-1, ordenados) en que empieza cada cacería.
        /// huntCount &lt;= 0 devuelve una lista vacía.
        /// </summary>
        public static List<float> Compute(int huntCount, int seed)
        {
            List<float> moments = new List<float>();
            int count = Math.Min(huntCount, MaxHunts);
            if (count <= 0) return moments;

            // Se reserva el hueco mínimo entre cacerías y el sobrante se reparte al azar:
            // así la separación y el rango se cumplen siempre, sin reintentos.
            // Math.Max: con 5 cacerías el sobrante es 0 y el redondeo de float lo dejaba en -0.00000005.
            float slack = Math.Max(0f, (Latest - Earliest) - MinGap * (count - 1));
            Random rng = new Random(seed);
            List<float> offsets = new List<float>(count);
            for (int i = 0; i < count; i++) offsets.Add((float)rng.NextDouble() * slack);
            offsets.Sort();

            for (int i = 0; i < count; i++)
            {
                float moment = Earliest + offsets[i] + MinGap * i;
                if (moment > Latest) moment = Latest;
                moments.Add(moment);
            }
            return moments;
        }
    }
}
