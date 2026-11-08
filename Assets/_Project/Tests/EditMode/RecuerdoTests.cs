using System.Collections.Generic;
using Ascua.Core;
using Ascua.Player;
using NUnit.Framework;
using UnityEngine;

namespace Ascua.Recuerdos.Tests
{
    /// <summary>
    /// Pruebas de los Recuerdos (RecuerdoRules y UpgradeLedger, lógica pura): la oferta de cada noche
    /// y que el guardado (JsonUtility, como SaveSystem) restaure los 7.
    /// Se ejecutan en Window > General > Test Runner > EditMode.
    /// </summary>
    public class RecuerdoTests
    {
        // ------------------------------------------------------------ Oferta

        [Test]
        public void Oferta_TresSinRepetidos_EnMuchasNoches()
        {
            for (int night = 1; night <= 50; night++)
            {
                List<UpgradeType> offer = RecuerdoRules.PickOffer(new List<UpgradeType>(), night);
                Assert.AreEqual(3, offer.Count, "Noche " + night);
                Assert.AreEqual(offer.Count, new HashSet<UpgradeType>(offer).Count, "Repetido en la noche " + night);
            }
        }

        [Test]
        public void Oferta_NoIncluyeLosYaElegidos()
        {
            List<UpgradeType> chosen = new List<UpgradeType> { UpgradeType.RadioAmplio, UpgradeType.UltimaVela, UpgradeType.CarritoTeo };
            for (int night = 1; night <= 50; night++)
            {
                List<UpgradeType> offer = RecuerdoRules.PickOffer(chosen, night);
                Assert.AreEqual(3, offer.Count);
                foreach (UpgradeType upgrade in offer) CollectionAssert.DoesNotContain(chosen, upgrade, "Noche " + night);
                Assert.AreEqual(offer.Count, new HashSet<UpgradeType>(offer).Count);
            }
        }

        [Test]
        public void Oferta_MismaNoche_MismaOferta()
        {
            List<UpgradeType> chosen = new List<UpgradeType> { UpgradeType.Eficiencia };
            CollectionAssert.AreEqual(RecuerdoRules.PickOffer(chosen, 3), RecuerdoRules.PickOffer(chosen, 3));
        }

        [Test]
        public void Oferta_QuedanMenosDeTres_OfreceLosQueHaya()
        {
            List<UpgradeType> chosen = new List<UpgradeType>(RecuerdoRules.All);
            chosen.Remove(UpgradeType.LibroAmelia);
            chosen.Remove(UpgradeType.RelojNadir);

            List<UpgradeType> offer = RecuerdoRules.PickOffer(chosen, 4);
            Assert.AreEqual(2, offer.Count);
            CollectionAssert.AreEquivalent(new[] { UpgradeType.LibroAmelia, UpgradeType.RelojNadir }, offer);
        }

        [Test]
        public void Oferta_SinRestantes_VieneVacia()
        {
            Assert.AreEqual(0, RecuerdoRules.PickOffer(new List<UpgradeType>(RecuerdoRules.All), 5).Count);
        }

        [Test]
        public void Oferta_ElegirCadaNoche_LlegaALosSieteSinRepetir()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            for (int night = 1; night <= 7; night++)
            {
                List<UpgradeType> offer = RecuerdoRules.PickOffer(ledger.Chosen, night);
                Assert.IsNotEmpty(offer, "Noche " + night);
                ledger.Choose(offer[0]);
            }
            CollectionAssert.AreEquivalent(RecuerdoRules.All, ledger.Chosen);
            Assert.IsEmpty(RecuerdoRules.PickOffer(ledger.Chosen, 8));
        }

        // ------------------------------------------------------------ Guardado

        [Test]
        public void Enum_LosValoresViejosNoCambian()
        {
            // Los guardados escriben el enum como número: los 3 primeros no pueden moverse.
            Assert.AreEqual(0, (int)UpgradeType.RadioAmplio);
            Assert.AreEqual(1, (int)UpgradeType.Eficiencia);
            Assert.AreEqual(2, (int)UpgradeType.Regeneracion);
            Assert.AreEqual(7, RecuerdoRules.All.Length);
        }

        [Test]
        public void Guardado_RestauraLosSiete()
        {
            UpgradeLedger original = new UpgradeLedger();
            foreach (UpgradeType upgrade in RecuerdoRules.All) original.Choose(upgrade);
            original.LargeLanternUnlocked = true;
            Assert.IsTrue(original.TryUseUltimaVela());

            // Mismo camino que SaveSystem: JsonUtility ida y vuelta.
            string json = JsonUtility.ToJson(original.Snapshot());
            UpgradeLedger restored = new UpgradeLedger();
            restored.Restore(JsonUtility.FromJson<UpgradesData>(json));

            foreach (UpgradeType upgrade in RecuerdoRules.All) Assert.AreEqual(1, restored.Count(upgrade), upgrade.ToString());
            Assert.AreEqual(7, restored.Chosen.Count);
            Assert.IsTrue(restored.LargeLanternUnlocked);
            Assert.IsTrue(restored.UltimaVelaUsada);
            Assert.IsFalse(restored.TryUseUltimaVela(), "La Última vela ya gastada no se puede volver a usar tras cargar");
        }

        [Test]
        public void Guardado_Viejo_SinMarcaDeVela_LaDejaDisponible()
        {
            // Guardado de antes de los 7 Recuerdos: sin el campo ultimaVelaUsada.
            UpgradesData data = JsonUtility.FromJson<UpgradesData>("{\"chosen\":[0,2,6],\"largeLanternUnlocked\":false}");
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Restore(data);

            Assert.AreEqual(3, ledger.Chosen.Count);
            Assert.IsTrue(ledger.Has(UpgradeType.UltimaVela));
            Assert.IsFalse(ledger.UltimaVelaUsada);
            Assert.IsTrue(ledger.TryUseUltimaVela());
        }

        [Test]
        public void UltimaVela_SinElegirla_NoSeUsa()
        {
            UpgradeLedger ledger = new UpgradeLedger();
            ledger.Choose(UpgradeType.LibroAmelia);
            Assert.IsFalse(ledger.TryUseUltimaVela());
            Assert.IsFalse(ledger.UltimaVelaUsada);
        }
    }
}
