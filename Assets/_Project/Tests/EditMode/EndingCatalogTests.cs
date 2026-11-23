using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.Endings.Tests
{
    /// <summary>
    /// Pruebas del catálogo de finales (EndingCatalog y EndingInfo, lógica pura): que la galería y el
    /// guardado encuentren cada final, y que las líneas de los personajes salgan en orden y con su umbral.
    /// </summary>
    public class EndingCatalogTests
    {
        [Test]
        public void HayUnFinalPorCadaValorDelEnum()
        {
            EndingId[] ids = (EndingId[])Enum.GetValues(typeof(EndingId));
            Assert.AreEqual(ids.Length, EndingCatalog.All.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(ids[i], EndingCatalog.All[i].Id, "La galería sigue el orden del enum");
            }
        }

        [Test]
        public void Get_DevuelveElFinalPedido()
        {
            foreach (EndingId id in Enum.GetValues(typeof(EndingId)))
            {
                Assert.AreEqual(id, EndingCatalog.Get(id).Id);
            }
        }

        [Test]
        public void Get_IdDesconocido_DevuelveCenizaSinFallar()
        {
            EndingInfo info = EndingCatalog.Get((EndingId)99);
            Assert.AreEqual(EndingId.Ceniza, info.Id);
        }

        [Test]
        public void ClaveDeGuardado_EsElNombreDelEnumYEsUnica()
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (EndingInfo info in EndingCatalog.All)
            {
                Assert.AreEqual(info.Id.ToString(), info.SaveKey);
                Assert.IsTrue(keys.Add(info.SaveKey));
                Assert.IsTrue(Enum.TryParse(info.SaveKey, out EndingId parsed) && parsed == info.Id, "La clave vuelve a leerse como el mismo final");
            }
        }

        [Test]
        public void NombresYTitulosNoSeRepiten()
        {
            HashSet<string> names = new HashSet<string>();
            HashSet<string> titles = new HashSet<string>();
            foreach (EndingInfo info in EndingCatalog.All)
            {
                Assert.IsTrue(names.Add(info.Name), "Nombre repetido: " + info.Name);
                Assert.IsTrue(titles.Add(info.ShortTitle), "Título repetido: " + info.ShortTitle);
            }
        }

        [Test]
        public void EpilogosDeTresLineasSinEtiquetas()
        {
            foreach (EndingInfo info in EndingCatalog.All)
            {
                Assert.AreEqual(3, info.Epilogue.Length, info.Name);
                foreach (string line in info.Epilogue)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), info.Name);
                    Assert.IsFalse(line.Contains('<'), "El epílogo no lleva etiquetas de TextMeshPro: " + info.Name);
                }
            }
        }

        [Test]
        public void LineasDePersonajes_EnOrdenMinaTeoPerpetua()
        {
            List<string> lines = EndingCatalog.CompanionLines(5, 5, 5);
            Assert.AreEqual(3, lines.Count);
            StringAssert.StartsWith("Mina", lines[0]);
            StringAssert.StartsWith("Teo", lines[1]);
            StringAssert.StartsWith("Perpetua", lines[2]);
        }

        [Test]
        public void LineasDePersonajes_SoloLosQueLleganAlUmbral()
        {
            CollectionAssert.IsEmpty(EndingCatalog.CompanionLines(2, 2, 2));
            List<string> lines = EndingCatalog.CompanionLines(2, 3, 2);
            Assert.AreEqual(1, lines.Count);
            StringAssert.StartsWith("Teo", lines[0]);
        }

        [Test]
        public void LineasDePersonajes_UmbralPersonalizado()
        {
            Assert.AreEqual(3, EndingCatalog.CompanionLines(1, 1, 1, minBond: 1).Count);
            Assert.AreEqual(1, EndingCatalog.CompanionLines(5, 4, 4, minBond: 5).Count);
            CollectionAssert.IsEmpty(EndingCatalog.CompanionLines(5, 5, 5, minBond: 6));
        }
    }
}
