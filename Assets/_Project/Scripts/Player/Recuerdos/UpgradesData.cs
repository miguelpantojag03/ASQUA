using System;
using System.Collections.Generic;
using Ascua.Player;

namespace Ascua.Core
{
    /// <summary>
    /// Copia de PlayerUpgrades para el guardado: Recuerdos elegidos, el Farol grande y si ya se usó
    /// la Última vela. Vive en el ensamblado puro Ascua.Recuerdos para poder probarla en EditMode.
    /// </summary>
    [Serializable]
    public class UpgradesData
    {
        public List<UpgradeType> chosen = new List<UpgradeType>();
        public bool largeLanternUnlocked;
        /// <summary>True si la Última vela ya salvó una zona en esta partida (los guardados viejos leen false).</summary>
        public bool ultimaVelaUsada;
    }
}
