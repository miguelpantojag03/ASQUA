using System;
using System.Collections.Generic;
using System.IO;
using Ascua.Player;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Guardado en disco (Application.persistentDataPath/ascua_save.json, con JsonUtility).
    /// - Finales vistos: solo viven aquí, así que GameProgress.ResetAll() ("Jugar") no los borra.
    /// - Progreso: se guarda solo al pasar de noche (NightManager.StartNextNight) y se restaura con "Continuar".
    /// Todo va con try/catch: si el archivo falta o está corrupto, se ignora y el juego sigue.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "ascua_save.json";

        // Copia en memoria del archivo (se lee una sola vez).
        private static SaveData cache;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Al entrar en Play se vuelve a leer el disco (por si se borró o editó el archivo).
            cache = null;
        }

        // ================================================================== Finales

        /// <summary>Ids de los finales vistos (EndingId como texto).</summary>
        public static IReadOnlyList<string> SeenEndings => Data.seenEndings;

        public static bool IsEndingSeen(string endingKey) => Data.seenEndings.Contains(endingKey);

        /// <summary>Registra un final como visto y lo guarda al momento.</summary>
        public static void MarkEndingSeen(string endingKey)
        {
            if (string.IsNullOrEmpty(endingKey) || Data.seenEndings.Contains(endingKey)) return;
            Data.seenEndings.Add(endingKey);
            Write();
        }

        // ================================================================== Progreso

        /// <summary>True si hay una partida guardada que se pueda continuar.</summary>
        public static bool HasProgress => Data.hasProgress && Data.progress != null;

        /// <summary>Guarda la partida en curso (noche, misiones, Diario, Vínculos, mejoras...).</summary>
        public static void SaveProgress()
        {
            try
            {
                Data.progress = new ProgressData
                {
                    game = GameProgress.Snapshot(),
                    bonds = QuestManager.GetBondsForSave(),
                    upgrades = PlayerUpgrades.Snapshot()
                };
                Data.hasProgress = true;
                Write();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] No se pudo preparar el guardado: " + e.Message);
            }
        }

        /// <summary>
        /// Restaura la partida guardada en memoria. Los Vínculos se aplican cuando exista QuestManager
        /// (ver QuestManager.LoadBonds). Devuelve false si no hay nada que continuar.
        /// </summary>
        public static bool RestoreProgress()
        {
            if (!HasProgress) return false;
            try
            {
                ProgressData progress = Data.progress;
                GameProgress.Restore(progress.game);
                PlayerUpgrades.Restore(progress.upgrades);
                QuestManager.LoadBonds(progress.bonds);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] No se pudo restaurar la partida: " + e.Message);
                return false;
            }
        }

        // ================================================================== Disco

        private static SaveData Data
        {
            get
            {
                if (cache == null) cache = Read();
                return cache;
            }
        }

        private static SaveData Read()
        {
            try
            {
                if (!File.Exists(FilePath)) return new SaveData();
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return Sanitize(data);
            }
            catch (Exception e)
            {
                // Archivo corrupto o ilegible: se ignora sin romper el juego (se sobrescribirá al guardar).
                Debug.LogWarning("[SaveSystem] Guardado ilegible, se ignora: " + e.Message);
                return new SaveData();
            }
        }

        /// <summary>Rellena lo que falte en un archivo antiguo o incompleto.</summary>
        private static SaveData Sanitize(SaveData data)
        {
            if (data == null) return new SaveData();
            if (data.seenEndings == null) data.seenEndings = new List<string>();
            if (data.progress == null)
            {
                data.progress = new ProgressData();
                data.hasProgress = false;
            }
            if (data.progress.game == null) data.hasProgress = false;
            return data;
        }

        private static void Write()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(cache, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] No se pudo escribir el guardado: " + e.Message);
            }
        }
    }
}
