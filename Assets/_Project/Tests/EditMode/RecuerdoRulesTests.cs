using System;
using System.Collections.Generic;
using Ascua.Player;
using NUnit.Framework;

namespace Ascua.Recuerdos.Tests
{
    /// <summary>
    /// Pruebas de RecuerdoRules (lógica pura) que RecuerdoTests no cubre: la lista All frente al enum,
    /// los textos de la pantalla de elección y la oferta con entradas límite.
    /// </summary>
    public class RecuerdoRulesTests
    {
        [Test]
        public void All_TieneCadaValorDelEnumUnaVezYEnOrden()
        {
            UpgradeType[] values = (UpgradeType[])Enum.GetValues(typeof(UpgradeType));
            CollectionAssert.AreEqual(values, RecuerdoRules.All);
        }

        [Test]
        public void Describe_CadaRecuerdoTieneTituloYDescripcionPropios()
        {
            HashSet<string> titles = new HashSet<string>();
            foreach (UpgradeType upgrade in RecuerdoRules.All)
            {
                (string title, string description) = RecuerdoRules.Describe(upgrade);
                Assert.IsFalse(string.IsNullOrWhiteSpace(title), upgrade.ToString());
                Assert.IsFalse(string.IsNullOrWhiteSpace(description), upgrade.ToString());
                Assert.AreNotEqual(upgrade.ToString(), title, "Debe tener nombre humano: " + upgrade);
                Assert.IsTrue(titles.Add(title), "Título repetido: " + title);
            }
        }

        [Test]
        public void Describe_UltimaVelaUsaLaMismaEsperanzaQueLaRegla()
        {
            string description = RecuerdoRules.Describe(UpgradeType.UltimaVela).description;
            StringAssert.Contains(RecuerdoRules.UltimaVelaHope.ToString("0", System.Globalization.CultureInfo.InvariantCulture), description);
        }

        [Test]
        public void Describe_ValorDesconocidoNoFalla()
        {
            (string title, string description) = RecuerdoRules.Describe((UpgradeType)42);
            Assert.AreEqual("42", title);
            Assert.AreEqual("", description);
        }

        [Test]
        public void Oferta_SinListaDeElegidos_OfreceTres()
        {
            List<UpgradeType> offer = RecuerdoRules.PickOffer(null, 2);
            Assert.AreEqual(RecuerdoRules.OfferSize, offer.Count);
            CollectionAssert.AllItemsAreUnique(offer);
        }

        [TestCase(0)]
        [TestCase(-5)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void Oferta_NochesExtremas_NoFallan(int night)
        {
            List<UpgradeType> offer = RecuerdoRules.PickOffer(new List<UpgradeType>(), night);
            Assert.AreEqual(RecuerdoRules.OfferSize, offer.Count);
            CollectionAssert.AllItemsAreUnique(offer);
        }

        [Test]
        public void Oferta_ElegidosRepetidosNoRestanDeMas()
        {
            List<UpgradeType> chosen = new List<UpgradeType>
            {
                UpgradeType.RadioAmplio, UpgradeType.RadioAmplio, UpgradeType.RadioAmplio,
                UpgradeType.Eficiencia, UpgradeType.Eficiencia
            };
            // Quedan 5 sin elegir: la oferta sigue siendo de 3.
            List<UpgradeType> offer = RecuerdoRules.PickOffer(chosen, 4);
            Assert.AreEqual(3, offer.Count);
            CollectionAssert.DoesNotContain(offer, UpgradeType.RadioAmplio);
            CollectionAssert.DoesNotContain(offer, UpgradeType.Eficiencia);
        }

        [Test]
        public void Oferta_DistintasNochesNoSiempreIguales()
        {
            HashSet<string> offers = new HashSet<string>();
            for (int night = 1; night <= 10; night++)
            {
                offers.Add(string.Join(",", RecuerdoRules.PickOffer(null, night)));
            }
            Assert.Greater(offers.Count, 1, "La semilla depende de la noche");
        }
    }
}
