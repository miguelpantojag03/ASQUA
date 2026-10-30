using System;
using System.Collections.Generic;
using Ascua.LevelGen;
using UnityEngine;

namespace Ascua.AILevelGen
{
    /// <summary>Archivo JSON con varios ADN propuestos por una IA generativa (Resources/LevelDNA/banco_ia).</summary>
    [Serializable]
    public class LevelDnaBankFile
    {
        public LevelDNA[] entries = new LevelDNA[0];
    }

    /// <summary>
    /// Banco de ADN generados por una IA en tiempo de diseño: la IA propuso variaciones de la Noche 1,
    /// las dejamos como JSON y el juego las usa SIN internet. Antes de usarlas pasan por el LevelValidator,
    /// igual que pasaría con una respuesta en vivo. Cada entrada se usa una sola vez por partida.
    /// </summary>
    public static class LevelDnaBank
    {
        private const string ResourcePath = "LevelDNA/banco_ia";

        private static LevelDnaBankFile cache;
        private static readonly HashSet<int> used = new HashSet<int>();

        /// <summary>
        /// Elige la entrada sin usar cuyo nivel de habilidad objetivo (playerDifficulty) esté más cerca del jugador.
        /// Devuelve una copia validada, o null si no hay banco o ya se usaron todas (entonces manda el generador local).
        /// </summary>
        public static LevelDNA TryPick(float playerSkill, int seed, List<string> log)
        {
            LevelDnaBankFile bank = Load();
            if (bank == null || bank.entries == null || bank.entries.Length == 0) return null;

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < bank.entries.Length; i++)
            {
                if (bank.entries[i] == null || used.Contains(i)) continue;
                float distance = Mathf.Abs(bank.entries[i].playerDifficulty - playerSkill);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            if (best < 0) return null;

            used.Add(best);
            LevelDNA dna = bank.entries[best].Clone();
            // La IA propone, el código dispone: todo pasa por el validador y lo corregido queda anotado.
            List<string> repairs = LevelValidator.Repair(dna);
            for (int i = 0; i < repairs.Count; i++) log.Add("banco IA #" + best + ": " + repairs[i]);
            dna.origin = LevelDnaOrigin.AiBank;
            dna.seed = seed;
            return dna;
        }

        public static void ResetAll()
        {
            used.Clear();
        }

        private static LevelDnaBankFile Load()
        {
            if (cache != null) return cache;
            TextAsset text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null) return null;
            try
            {
                cache = JsonUtility.FromJson<LevelDnaBankFile>(text.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ascua][IA niveles] El banco de ADN no se pudo leer (se usan las reglas locales): " + e.Message);
                cache = null;
            }
            return cache;
        }
    }
}
