using Ascua.Combat;
using Ascua.LevelGen;
using Ascua.Quests;

namespace Ascua.AILevelGen
{
    /// <summary>Extrae el ADN de un NightQuestSet. Solo lee: nunca modifica el asset.</summary>
    public static class LevelDnaConverter
    {
        /// <summary>
        /// ADN real de una noche diseñada a mano. Con la Noche 1 sale BASE_DNA, el punto de partida
        /// del que derivan todos los niveles generados.
        /// </summary>
        public static LevelDNA FromSet(NightQuestSet set, string origin)
        {
            if (set == null) return null;

            LevelDNA dna = new LevelDNA();
            dna.origin = origin;
            dna.seed = set.nightNumber;
            dna.nightDuration = set.nightDuration;
            dna.startHope = set.startHope;
            dna.hopeDecayPerSecond = set.hopeDecayPerSecond;
            dna.hopeRisePerSecond = set.hopeRisePerSecond;
            dna.penumbraStartRadius = set.penumbraStartRadius;
            dna.penumbraEndRadius = set.penumbraEndRadius;
            dna.fastPenumbraCurve = set.fastPenumbraCurve;
            dna.fireflySpawnInterval = set.fireflySpawnInterval;
            dna.fireflyMaxActive = set.fireflyMaxActive;
            dna.surgeEnabled = set.surgeEnabled;
            dna.surgeAt = set.surgeAt;
            dna.surgeSeconds = set.surgeSeconds;
            dna.surgeMeters = set.surgeMeters;
            dna.surgeDecayMultiplier = set.surgeDecayMultiplier;
            dna.heraldoEnabled = set.heraldoEnabled;
            dna.heraldoAppearAt = set.heraldoAppearAt;
            dna.heraldoChaseSpeed = set.heraldoChaseSpeed;

            // Misiones: cuántas y con qué mezcla de tipos.
            float[] weights = new float[LevelDNA.QuestTypeCount];
            int count = 0;
            for (int i = 0; set.quests != null && i < set.quests.Count; i++)
            {
                Quest quest = set.quests[i];
                if (quest == null) continue;
                int index = (int)quest.type;
                if (index >= 0 && index < weights.Length) weights[index] += 1f;
                count++;
            }
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += weights[i];
            if (sum > 0f)
            {
                for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
                dna.questTypeWeights = weights;
            }
            else
            {
                dna.questTypeWeights = LevelValidator.DefaultWeights();
            }
            dna.questCount = count;

            // Oleadas: cuántas, tamaño medio y presión media.
            int waves = 0;
            float creatures = 0f;
            float drain = 0f;
            for (int i = 0; set.waves != null && i < set.waves.Count; i++)
            {
                CombatWave wave = set.waves[i];
                if (wave == null) continue;
                waves++;
                creatures += wave.Total;
                drain += wave.hopeDrainPerCreature;
            }
            dna.waveCount = waves;
            if (waves > 0)
            {
                dna.creaturesPerWave = (int)System.Math.Round(creatures / waves);
                dna.hopeDrainPerCreature = drain / waves;
            }

            dna.tone = ToneForNight(set.nightNumber);
            dna.narrativeFragments = new string[0];
            return dna;
        }

        /// <summary>Tono de las noches diseñadas: empiezan con esperanza y se vuelven tensas.</summary>
        private static string ToneForNight(int night)
        {
            if (night <= 1) return "esperanzado";
            if (night <= 3) return "melancolico";
            if (night == 4) return "tenso";
            return "climatico";
        }
    }
}
