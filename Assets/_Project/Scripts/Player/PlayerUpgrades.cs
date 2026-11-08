using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Recuerdos elegidos (mejoras permanentes: sobreviven entre noches y al recargar la escena).
    /// La lógica pura vive en UpgradeLedger y RecuerdoRules (ensamblado Ascua.Recuerdos, con pruebas);
    /// aquí se añaden el evento Changed y los multiplicadores que leen los sistemas del juego:
    /// LanternController, PlayerEnergy, CandleSpawner, Penumbra y ZoneHope.
    /// También guarda el desbloqueo del "Farol grande" (regalo de Teo).
    /// </summary>
    public static class PlayerUpgrades
    {
        private static readonly UpgradeLedger ledger = new UpgradeLedger();

        /// <summary>Se dispara al elegir un Recuerdo, desbloquear el Farol grande o gastar la Última vela.</summary>
        public static event Action Changed;

        public static bool LargeLanternUnlocked => ledger.LargeLanternUnlocked;
        public static IReadOnlyList<UpgradeType> Chosen => ledger.Chosen;
        public static bool UltimaVelaUsada => ledger.UltimaVelaUsada;

        /// <summary>Lupa de Mina.</summary>
        public static float RadiusMultiplier => 1f + 0.15f * Count(UpgradeType.RadioAmplio);
        /// <summary>Bata de Rueda.</summary>
        public static float DrainMultiplier => Mathf.Pow(0.8f, Count(UpgradeType.Eficiencia));
        /// <summary>Caja de música de Perpetua.</summary>
        public static float RegenMultiplier => 1f + 0.35f * Count(UpgradeType.Regeneracion);
        /// <summary>Carrito de Teo: las velas aparecen un 30% más seguido (el intervalo se divide entre 1.3).</summary>
        public static float CandleIntervalMultiplier => Has(UpgradeType.CarritoTeo) ? 1f / 1.3f : 1f;
        /// <summary>Reloj de Nadir: la Penumbra avanza un 15% más lenta.</summary>
        public static float PenumbraSpeedMultiplier => Has(UpgradeType.RelojNadir) ? 0.85f : 1f;
        /// <summary>Libro de Amelia: Esperanza extra de cada zona al empezar la noche.</summary>
        public static float StartHopeBonus => Has(UpgradeType.LibroAmelia) ? 5f : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ledger.Reset();
            Changed = null;
        }

        /// <summary>Partida nueva (desde el título).</summary>
        public static void ResetAll()
        {
            ledger.Reset();
            Changed?.Invoke();
        }

        public static void Choose(UpgradeType upgrade)
        {
            ledger.Choose(upgrade);
            Changed?.Invoke();
        }

        public static void UnlockLargeLantern()
        {
            if (ledger.LargeLanternUnlocked) return;
            ledger.LargeLanternUnlocked = true;
            Changed?.Invoke();
        }

        /// <summary>Última vela: true (y se gasta) si el jugador la tiene y aún no la usó en esta partida.</summary>
        public static bool TryUseUltimaVela()
        {
            if (!ledger.TryUseUltimaVela()) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hasta 3 Recuerdos aún no elegidos, al azar con la noche como semilla.</summary>
        public static List<UpgradeType> OfferForNight(int night) => RecuerdoRules.PickOffer(ledger.Chosen, night);

        /// <summary>Copia de los Recuerdos para guardarla en disco.</summary>
        public static Ascua.Core.UpgradesData Snapshot() => ledger.Snapshot();

        /// <summary>Vuelve a los Recuerdos guardados.</summary>
        public static void Restore(Ascua.Core.UpgradesData data)
        {
            ledger.Restore(data);
            Changed?.Invoke();
        }

        public static bool Has(UpgradeType upgrade) => ledger.Has(upgrade);

        public static int Count(UpgradeType upgrade) => ledger.Count(upgrade);

        /// <summary>Nombre del Recuerdo y descripción para la pantalla de elección.</summary>
        public static (string title, string description) Describe(UpgradeType upgrade) => RecuerdoRules.Describe(upgrade);
    }
}
