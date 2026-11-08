using System;
using System.Collections.Generic;

namespace Ascua.Core
{
    /// <summary>
    /// Todo lo que se guarda en ascua_save.json (con JsonUtility: solo campos públicos y clases [Serializable]).
    /// Los finales vistos viven aparte del progreso: empezar una partida nueva no los borra.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        /// <summary>Finales ya vistos (EndingId como texto), para la galería.</summary>
        public List<string> seenEndings = new List<string>();
        /// <summary>False hasta el primer guardado automático: decide si aparece "Continuar".</summary>
        public bool hasProgress;
        public ProgressData progress = new ProgressData();
    }

    /// <summary>La partida en curso: GameProgress + Vínculos + mejoras del Farol.</summary>
    [Serializable]
    public class ProgressData
    {
        public GameProgressData game = new GameProgressData();
        public List<BondEntry> bonds = new List<BondEntry>();
        public UpgradesData upgrades = new UpgradesData();
    }

    /// <summary>Copia de GameProgress (noche, misiones, Diario, atajos, momentos, Recuerdos, marcas y oleadas).</summary>
    [Serializable]
    public class GameProgressData
    {
        public int currentNight = 1;
        public bool tutorialPending = true;
        public List<string> completedQuests = new List<string>();
        public List<string> foundPages = new List<string>();
        public List<string> shortcuts = new List<string>();
        public List<string> seenMoments = new List<string>();
        public List<string> recuerdos = new List<string>();
        public List<string> flags = new List<string>();
        public int wavesDefended;
    }

    /// <summary>Vínculo con un personaje (los diccionarios no se pueden guardar con JsonUtility).</summary>
    [Serializable]
    public class BondEntry
    {
        public string npcId;
        public int value;

        /// <summary>Constructor vacío: lo necesita JsonUtility al leer el archivo.</summary>
        public BondEntry() { }

        public BondEntry(string npcId, int value)
        {
            this.npcId = npcId;
            this.value = value;
        }
    }

    // UpgradesData (copia de PlayerUpgrades) vive en Player/Recuerdos/UpgradesData.cs (ensamblado Ascua.Recuerdos).
}
