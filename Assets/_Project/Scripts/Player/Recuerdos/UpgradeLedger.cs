using System.Collections.Generic;
using Ascua.Core;

namespace Ascua.Player
{
    /// <summary>
    /// Estado de los Recuerdos de una partida (lógica pura, sin Unity). PlayerUpgrades guarda una
    /// instancia estática y le añade los eventos y multiplicadores que usa el juego.
    /// </summary>
    public class UpgradeLedger
    {
        private readonly List<UpgradeType> chosen = new List<UpgradeType>();

        public IReadOnlyList<UpgradeType> Chosen => chosen;
        public bool LargeLanternUnlocked { get; set; }
        /// <summary>True si la Última vela ya salvó una zona en esta partida.</summary>
        public bool UltimaVelaUsada { get; private set; }

        public void Reset()
        {
            chosen.Clear();
            LargeLanternUnlocked = false;
            UltimaVelaUsada = false;
        }

        public void Choose(UpgradeType upgrade) => chosen.Add(upgrade);

        public bool Has(UpgradeType upgrade) => Count(upgrade) > 0;

        public int Count(UpgradeType upgrade)
        {
            int n = 0;
            for (int i = 0; i < chosen.Count; i++) if (chosen[i] == upgrade) n++;
            return n;
        }

        /// <summary>Gasta la Última vela si el jugador la tiene y aún no la usó. Devuelve true si se gastó ahora.</summary>
        public bool TryUseUltimaVela()
        {
            if (UltimaVelaUsada || !Has(UpgradeType.UltimaVela)) return false;
            UltimaVelaUsada = true;
            return true;
        }

        /// <summary>Copia para guardarla en disco.</summary>
        public UpgradesData Snapshot()
        {
            return new UpgradesData
            {
                chosen = new List<UpgradeType>(chosen),
                largeLanternUnlocked = LargeLanternUnlocked,
                ultimaVelaUsada = UltimaVelaUsada
            };
        }

        /// <summary>Vuelve al estado guardado (null = partida vacía).</summary>
        public void Restore(UpgradesData data)
        {
            Reset();
            if (data == null) return;
            if (data.chosen != null) chosen.AddRange(data.chosen);
            LargeLanternUnlocked = data.largeLanternUnlocked;
            UltimaVelaUsada = data.ultimaVelaUsada;
        }
    }
}
