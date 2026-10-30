using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ascua.LevelGen
{
    /// <summary>Una fila legible de la tabla "Ver ADN de este nivel".</summary>
    public struct DnaRow
    {
        public string Label;
        public string BaseText;
        public string CurrentText;
        /// <summary>Cambio respecto a la base: "+12%", "-8%", "=" o "".</summary>
        public string DeltaText;
    }

    /// <summary>
    /// Convierte dos ADN (la base y el del nivel) en filas legibles. Es lo que ve el jugador en el panel
    /// de transparencia: números con nombre humano, nunca JSON crudo. Lógica pura, testeable.
    /// </summary>
    public static class LevelDnaRows
    {
        private static readonly string[] QuestTypeNames =
        {
            "Rescate de Esperanza", "Recolección", "Escolta", "Diálogo", "Trato del Heraldo", "Entrega", "Doble objetivo"
        };

        public static List<DnaRow> Compare(LevelDNA baseDna, LevelDNA current)
        {
            List<DnaRow> rows = new List<DnaRow>();
            if (current == null) return rows;
            LevelDNA b = baseDna ?? current;

            rows.Add(Num("Duración de la noche", b.nightDuration, current.nightDuration, " s", 0));
            rows.Add(Num("Esperanza inicial de las zonas", b.startHope, current.startHope, "", 0));
            rows.Add(Num("Caída de Esperanza a oscuras", b.hopeDecayPerSecond, current.hopeDecayPerSecond, " /s", 2));
            rows.Add(Num("Recuperación al iluminar", b.hopeRisePerSecond, current.hopeRisePerSecond, " /s", 1));
            rows.Add(Num("Penumbra: radio inicial", b.penumbraStartRadius, current.penumbraStartRadius, " m", 0));
            rows.Add(Num("Penumbra: radio final", b.penumbraEndRadius, current.penumbraEndRadius, " m", 0));
            rows.Add(Num("Luciérnagas: una cada", b.fireflySpawnInterval, current.fireflySpawnInterval, " s", 1));
            rows.Add(Num("Luciérnagas a la vez (máx.)", b.fireflyMaxActive, current.fireflyMaxActive, "", 0));
            rows.Add(Num("Ráfaga de urgencia: momento", b.surgeAt * 100f, current.surgeAt * 100f, " %", 0));
            rows.Add(Num("Ráfaga de urgencia: duración", b.surgeSeconds, current.surgeSeconds, " s", 0));
            rows.Add(Num("Misiones", b.questCount, current.questCount, "", 0));
            rows.Add(Num("Oleadas de sombras", b.waveCount, current.waveCount, "", 0));
            rows.Add(Num("Criaturas por oleada", b.creaturesPerWave, current.creaturesPerWave, "", 0));
            rows.Add(Num("Presión por criatura", b.hopeDrainPerCreature, current.hopeDrainPerCreature, " /s", 2));
            rows.Add(Num("El Heraldo aparece a", b.heraldoAppearAt * 100f, current.heraldoAppearAt * 100f, " %", 0));
            rows.Add(Num("Velocidad del Heraldo", b.heraldoChaseSpeed, current.heraldoChaseSpeed, "", 1));
            rows.Add(Text("Tono emocional", b.tone, current.tone));
            rows.Add(Text("Tipo de misión más frecuente", TopQuestType(b), TopQuestType(current)));
            return rows;
        }

        /// <summary>El tipo de misión con más peso, en lenguaje humano.</summary>
        public static string TopQuestType(LevelDNA dna)
        {
            if (dna == null || dna.questTypeWeights == null || dna.questTypeWeights.Length == 0) return "-";
            int best = 0;
            for (int i = 1; i < dna.questTypeWeights.Length && i < QuestTypeNames.Length; i++)
            {
                if (dna.questTypeWeights[i] > dna.questTypeWeights[best]) best = i;
            }
            return best < QuestTypeNames.Length ? QuestTypeNames[best] : "-";
        }

        private static DnaRow Num(string label, float baseValue, float current, string unit, int decimals)
        {
            string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);
            DnaRow row = new DnaRow();
            row.Label = label;
            row.BaseText = baseValue.ToString(format, CultureInfo.InvariantCulture) + unit;
            row.CurrentText = current.ToString(format, CultureInfo.InvariantCulture) + unit;
            row.DeltaText = Delta(baseValue, current);
            return row;
        }

        private static DnaRow Text(string label, string baseValue, string current)
        {
            DnaRow row = new DnaRow();
            row.Label = label;
            row.BaseText = string.IsNullOrEmpty(baseValue) ? "-" : baseValue;
            row.CurrentText = string.IsNullOrEmpty(current) ? "-" : current;
            row.DeltaText = row.BaseText == row.CurrentText ? "=" : "cambia";
            return row;
        }

        private static string Delta(float baseValue, float current)
        {
            if (Math.Abs(baseValue) < 0.0001f) return current == baseValue ? "=" : "";
            float pct = (current - baseValue) / Math.Abs(baseValue) * 100f;
            if (Math.Abs(pct) < 0.5f) return "=";
            return (pct > 0f ? "+" : "-") + Math.Abs(pct).ToString("0", CultureInfo.InvariantCulture) + "%";
        }
    }
}
