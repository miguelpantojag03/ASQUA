using System.Collections.Generic;
using Ascua.LevelGen;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.AILevelGen
{
    /// <summary>
    /// El director de niveles. Pasada la última noche diseñada a mano, genera cada noche nueva así:
    ///   1) toma BASE_DNA (el ADN de la Noche 1) y el perfil del jugador (PlayerProfileTracker);
    ///   2) pide un ADN al banco de la IA (LevelDnaBank) o, si no hay, al generador local (ProceduralFallback);
    ///   3) el LevelValidator lo repara y anota cada corrección;
    ///   4) QuestSetBuilder lo convierte en un NightQuestSet con las misiones que ya existen.
    /// La IA NUNCA genera geometría, prefabs ni código: solo parámetros. Funciona 100 % sin internet.
    /// Cada noche se genera una sola vez y se guarda: al reintentar la noche se juega el mismo nivel.
    /// </summary>
    public static class AIDirector
    {
        /// <summary>Una noche generada: su ADN, su set jugable y lo que el validador tuvo que corregir.</summary>
        public sealed class GeneratedLevel
        {
            public int Night;
            public LevelDNA Dna;
            public NightQuestSet Set;
            public List<string> RepairLog = new List<string>();
        }

        private static readonly Dictionary<int, GeneratedLevel> cache = new Dictionary<int, GeneratedLevel>();
        private static LevelDNA baseDna;
        private static NightQuestSet baseDnaSource;
        private static int sessionSeed = NewSessionSeed();

        /// <summary>Semilla de esta partida (se muestra en el panel "Ver ADN" para poder repetir un nivel).</summary>
        public static int SessionSeed => sessionSeed;

        /// <summary>
        /// BASE_DNA: el ADN de la Noche 1. Siempre devuelve una COPIA: el original no se puede modificar
        /// desde fuera, así que la base es inmutable mientras se juega.
        /// </summary>
        public static LevelDNA GetBaseDna(NightQuestSet baseSet)
        {
            if (baseSet == null) return null;
            if (baseDna == null || baseDnaSource != baseSet)
            {
                baseDna = LevelDnaConverter.FromSet(baseSet, LevelDnaOrigin.Base);
                baseDnaSource = baseSet;
            }
            return baseDna.Clone();
        }

        /// <summary>El nivel ya generado para esa noche (o null si todavía no se generó).</summary>
        public static GeneratedLevel Peek(int night)
        {
            GeneratedLevel level;
            return cache.TryGetValue(night, out level) && level.Set != null ? level : null;
        }

        /// <summary>
        /// Devuelve el nivel de la noche; lo genera si no existe. baseSet = Noche 1; lastAuthored = la última
        /// noche diseñada a mano (de ella arranca la primera generada); pool = misiones disponibles.
        /// </summary>
        public static GeneratedLevel GetOrGenerate(int night, NightQuestSet baseSet, NightQuestSet lastAuthored, IList<Quest> pool)
        {
            GeneratedLevel existing = Peek(night);
            if (existing != null) return existing;
            if (baseSet == null) return null;

            LevelDNA baseCopy = GetBaseDna(baseSet);
            GeneratedLevel previousLevel = Peek(night - 1);
            LevelDNA previous = previousLevel != null
                ? previousLevel.Dna
                : LevelDnaConverter.FromSet(lastAuthored, LevelDnaOrigin.Manual);

            float skill = PlayerProfileTracker.Skill;
            int seed = sessionSeed + night * 7919;

            GeneratedLevel level = new GeneratedLevel();
            level.Night = night;

            // 1) Banco de la IA (propuestas hechas en diseño, validadas por código). 2) Si no hay, reglas locales.
            LevelDNA dna = LevelDnaBank.TryPick(skill, seed, level.RepairLog);
            if (dna == null) dna = ProceduralFallback.Generate(baseCopy, previous, skill, night, seed, level.RepairLog);

            level.Dna = dna;
            level.Set = QuestSetBuilder.Build(dna, night, pool, baseSet);
            cache[night] = level;

            // Nunca se descarta nada en silencio: lo que el validador corrigió queda en la consola.
            Debug.Log("[Ascua][IA niveles] Noche " + night + " generada · origen: " + dna.origin + " · tono: " + dna.tone
                      + " · habilidad del jugador: " + skill.ToString("0.00") + " · semilla: " + dna.seed
                      + " · misiones: " + level.Set.quests.Count + " · correcciones del validador: " + level.RepairLog.Count);
            for (int i = 0; i < level.RepairLog.Count; i++) Debug.Log("[Ascua][IA niveles]   - " + level.RepairLog[i]);
            return level;
        }

        /// <summary>Partida nueva: se olvidan los niveles generados, el perfil del jugador y el banco usado.</summary>
        public static void ResetAll()
        {
            cache.Clear();
            baseDna = null;
            baseDnaSource = null;
            sessionSeed = NewSessionSeed();
            PlayerProfileTracker.ResetAll();
            LevelDnaBank.ResetAll();
        }

        private static int NewSessionSeed()
        {
            return System.Environment.TickCount & 0xFFFFF;
        }
    }
}
