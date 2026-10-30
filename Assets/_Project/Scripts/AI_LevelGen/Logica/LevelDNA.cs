using System;

namespace Ascua.LevelGen
{
    /// <summary>De dónde salió un ADN de nivel.</summary>
    public static class LevelDnaOrigin
    {
        /// <summary>ADN extraído de la Noche 1 (el nivel de referencia, curado a mano).</summary>
        public const string Base = "base";
        /// <summary>ADN extraído de una noche diseñada a mano (2, 3, 4...).</summary>
        public const string Manual = "manual";
        /// <summary>ADN propuesto por una IA generativa (banco revisado y validado por código).</summary>
        public const string AiBank = "ia";
        /// <summary>ADN calculado por las reglas locales (ProceduralFallback), 100 % sin internet.</summary>
        public const string Local = "reglas_locales";
    }

    /// <summary>
    /// El "ADN" de un nivel: todos los parámetros que definen cómo se juega una noche, como datos planos.
    /// La IA (o el generador local) SOLO produce esto. Nunca geometría, prefabs ni código.
    /// Los nombres coinciden con los de NightQuestSet para que la conversión sea 1 a 1.
    /// Es una clase simple y serializable: se lee y escribe con JsonUtility.
    /// Esta carpeta es un assembly aparte (Ascua.LevelGen) sin dependencias de Unity, para poder probarla sola.
    /// </summary>
    [Serializable]
    public class LevelDNA
    {
        /// <summary>Cantidad de tipos de misión (QuestType): Rescate, Recolección, Escolta, Diálogo, Trato, Entrega, Doble.</summary>
        public const int QuestTypeCount = 7;

        public int seed;
        public string origin = LevelDnaOrigin.Base;
        /// <summary>esperanzado | melancolico | tenso | climatico</summary>
        public string tone = "melancolico";
        /// <summary>0-1: qué tan bien le fue al jugador (0 = mal, 1 = muy bien). Viene de PlayerProfileTracker.</summary>
        public float playerDifficulty = 0.5f;

        // ---- Noche y Esperanza
        public float nightDuration = 240f;
        public float startHope = 70f;
        public float hopeDecayPerSecond = 0.6f;
        public float hopeRisePerSecond = 4f;

        // ---- Penumbra
        public float penumbraStartRadius = 60f;
        public float penumbraEndRadius = 16f;
        public bool fastPenumbraCurve;

        // ---- Luciérnagas
        public float fireflySpawnInterval = 5f;
        public int fireflyMaxActive = 5;

        // ---- Ráfaga de urgencia
        public bool surgeEnabled = true;
        public float surgeAt = 0.55f;
        public float surgeSeconds = 10f;
        public float surgeMeters = 7f;
        public float surgeDecayMultiplier = 1.7f;

        // ---- Misiones (solo tipos que ya existen)
        public int questCount = 6;
        /// <summary>Peso de cada tipo de misión, en el orden de QuestType. Siempre 7 valores.</summary>
        public float[] questTypeWeights = new float[QuestTypeCount];

        // ---- Oleadas de sombras
        public int waveCount = 2;
        public int creaturesPerWave = 4;
        public float hopeDrainPerCreature = 1.2f;

        // ---- Heraldo
        public bool heraldoEnabled = true;
        public float heraldoAppearAt = 0.25f;
        public float heraldoChaseSpeed = 1.3f;

        /// <summary>Textos cortos (máx. 3). El primero se usa como subtítulo de la noche.</summary>
        public string[] narrativeFragments = new string[0];

        /// <summary>Copia profunda: modificar la copia nunca toca al original (así BASE_DNA es inmutable).</summary>
        public LevelDNA Clone()
        {
            LevelDNA copy = (LevelDNA)MemberwiseClone();
            copy.questTypeWeights = questTypeWeights != null ? (float[])questTypeWeights.Clone() : null;
            copy.narrativeFragments = narrativeFragments != null ? (string[])narrativeFragments.Clone() : null;
            return copy;
        }

        /// <summary>True si todos los campos son idénticos (para pruebas y para comprobar que la base no cambió).</summary>
        public bool SameAs(LevelDNA other)
        {
            if (other == null) return false;
            return seed == other.seed && origin == other.origin && tone == other.tone
                && playerDifficulty == other.playerDifficulty
                && nightDuration == other.nightDuration && startHope == other.startHope
                && hopeDecayPerSecond == other.hopeDecayPerSecond && hopeRisePerSecond == other.hopeRisePerSecond
                && penumbraStartRadius == other.penumbraStartRadius && penumbraEndRadius == other.penumbraEndRadius
                && fastPenumbraCurve == other.fastPenumbraCurve
                && fireflySpawnInterval == other.fireflySpawnInterval && fireflyMaxActive == other.fireflyMaxActive
                && surgeEnabled == other.surgeEnabled && surgeAt == other.surgeAt && surgeSeconds == other.surgeSeconds
                && surgeMeters == other.surgeMeters && surgeDecayMultiplier == other.surgeDecayMultiplier
                && questCount == other.questCount
                && waveCount == other.waveCount && creaturesPerWave == other.creaturesPerWave
                && hopeDrainPerCreature == other.hopeDrainPerCreature
                && heraldoEnabled == other.heraldoEnabled && heraldoAppearAt == other.heraldoAppearAt
                && heraldoChaseSpeed == other.heraldoChaseSpeed
                && SameArray(questTypeWeights, other.questTypeWeights)
                && SameArray(narrativeFragments, other.narrativeFragments);
        }

        private static bool SameArray(float[] a, float[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool SameArray(string[] a, string[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
