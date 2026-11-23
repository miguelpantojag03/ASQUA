using System.Collections.Generic;
using Ascua.Core;
using Ascua.Player;
using NUnit.Framework;

namespace Ascua.Recuerdos.Tests
{
    /// <summary>
    /// Pruebas de UpgradeLedger (lógica pura): elegir, contar, gastar la Última vela una sola vez
    /// y que Snapshot/Restore no compartan listas con el estado vivo.
    /// </summary>
    public class UpgradeLedgerTests
    {
        [Test]
        public void Nuevo_EstaVacio()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            Assert.AreEqual(0, ledger.Chosen.Count);
            Assert.IsFalse(ledger.LargeLanternUnlocked);
            Assert.IsFalse(ledger.UltimaVelaUsada);
            Assert.IsFalse(ledger.Has(UpgradeType.RadioAmplio));
        }

        [Test]
        public void Elegir_SeCuentaYSeConsulta()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.Eficiencia);
            ledger.Choose(UpgradeType.Eficiencia);
            ledger.Choose(UpgradeType.RelojNadir);

            Assert.AreEqual(2, ledger.Count(UpgradeType.Eficiencia));
            Assert.AreEqual(1, ledger.Count(UpgradeType.RelojNadir));
            Assert.AreEqual(0, ledger.Count(UpgradeType.LibroAmelia));
            Assert.IsTrue(ledger.Has(UpgradeType.RelojNadir));
            CollectionAssert.AreEqual(new[] { UpgradeType.Eficiencia, UpgradeType.Eficiencia, UpgradeType.RelojNadir }, ledger.Chosen);
        }

        [Test]
        public void UltimaVela_SeGastaUnaSolaVez()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.UltimaVela);

            Assert.IsTrue(ledger.TryUseUltimaVela(), "La primera zona se salva");
            Assert.IsTrue(ledger.UltimaVelaUsada);
            Assert.IsFalse(ledger.TryUseUltimaVela(), "La segunda ya no");
        }

        [Test]
        public void Reset_DejaLaPartidaLimpia()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.UltimaVela);
            ledger.LargeLanternUnlocked = true;
            ledger.TryUseUltimaVela();

            ledger.Reset();

            Assert.AreEqual(0, ledger.Chosen.Count);
            Assert.IsFalse(ledger.LargeLanternUnlocked);
            Assert.IsFalse(ledger.UltimaVelaUsada);
        }

        [Test]
        public void Snapshot_CopiaElEstado()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.UltimaVela);
            ledger.LargeLanternUnlocked = true;
            ledger.TryUseUltimaVela();

            UpgradesData data = ledger.Snapshot();

            CollectionAssert.AreEqual(new[] { UpgradeType.UltimaVela }, data.chosen);
            Assert.IsTrue(data.largeLanternUnlocked);
            Assert.IsTrue(data.ultimaVelaUsada);
        }

        [Test]
        public void Snapshot_NoCambiaSiLaPartidaSigue()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.RadioAmplio);
            UpgradesData data = ledger.Snapshot();

            ledger.Choose(UpgradeType.Regeneracion);

            Assert.AreEqual(1, data.chosen.Count, "El guardado es una copia, no la lista viva");
        }

        [Test]
        public void Restore_NoComparteLaListaDelGuardado()
        {
            UpgradesData data = new UpgradesData { chosen = new List<UpgradeType> { UpgradeType.CarritoTeo } };
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Restore(data);

            data.chosen.Add(UpgradeType.LibroAmelia);
            ledger.Choose(UpgradeType.RelojNadir);

            Assert.AreEqual(2, ledger.Chosen.Count);
            Assert.IsFalse(ledger.Has(UpgradeType.LibroAmelia));
            Assert.AreEqual(2, data.chosen.Count);
        }

        [Test]
        public void Restore_ReemplazaLoQueHabia()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.Eficiencia);
            ledger.Restore(new UpgradesData { chosen = new List<UpgradeType> { UpgradeType.RadioAmplio } });

            CollectionAssert.AreEqual(new[] { UpgradeType.RadioAmplio }, ledger.Chosen);
        }

        [Test]
        public void Restore_NuloOListaNula_EsPartidaVacia()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.Eficiencia);
            ledger.Restore(null);
            Assert.AreEqual(0, ledger.Chosen.Count);

            ledger.Restore(new UpgradesData { chosen = null, largeLanternUnlocked = true });
            Assert.AreEqual(0, ledger.Chosen.Count);
            Assert.IsTrue(ledger.LargeLanternUnlocked);
        }

        [Test]
        public void Restore_VelaYaGastadaNoSeVuelveAUsar()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Restore(new UpgradesData
            {
                chosen = new List<UpgradeType> { UpgradeType.UltimaVela },
                ultimaVelaUsada = true
            });
            Assert.IsFalse(ledger.TryUseUltimaVela());
        }
    }
}
