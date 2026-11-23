using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>
    /// Pruebas de la tabla "Ver ADN del nivel" (LevelDnaRows, lógica pura): lo que lee el jugador,
    /// así que importan el formato, los porcentajes y que nunca salga un valor vacío o JSON crudo.
    /// </summary>
    public class LevelDnaRowsTests
    {
        private static DnaRow Row(List<DnaRow> rows, string label)
        {
            DnaRow row = rows.Find(r => r.Label == label);
            Assert.IsNotNull(row.Label, "Falta la fila " + label);
            return row;
        }

        [Test]
        public void SinNivel_NoHayFilas()
        {
            CollectionAssert.IsEmpty(LevelDnaRows.Compare(new LevelDNA(), null));
        }

        [Test]
        public void SinBase_SeComparaConsigoMismo()
        {
            List<DnaRow> rows = LevelDnaRows.Compare(null, new LevelDNA { questTypeWeights = LevelValidator.DefaultWeights() });
            Assert.Greater(rows.Count, 0);
            foreach (DnaRow row in rows) Assert.AreEqual("=", row.DeltaText, row.Label);
        }

        [Test]
        public void CadaFilaTieneEtiquetaYValores()
        {
            List<DnaRow> rows = LevelDnaRows.Compare(new LevelDNA(), new LevelDNA());
            Assert.AreEqual(21, rows.Count);
            HashSet<string> labels = new HashSet<string>();
            foreach (DnaRow row in rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.Label));
                Assert.IsFalse(string.IsNullOrEmpty(row.BaseText), row.Label);
                Assert.IsFalse(string.IsNullOrEmpty(row.CurrentText), row.Label);
                Assert.IsTrue(labels.Add(row.Label), "Etiqueta repetida: " + row.Label);
            }
        }

        [Test]
        public void Numeros_ConUnidadYDecimales()
        {
            LevelDNA b = new LevelDNA();
            LevelDNA c = new LevelDNA { nightDuration = 300f, hopeDecayPerSecond = 0.8f, surgeAt = 0.4f };
            List<DnaRow> rows = LevelDnaRows.Compare(b, c);

            Assert.AreEqual("240 s", Row(rows, "Duración de la noche").BaseText);
            Assert.AreEqual("300 s", Row(rows, "Duración de la noche").CurrentText);
            Assert.AreEqual("0.80 /s", Row(rows, "Caída de Esperanza a oscuras").CurrentText, "Dos decimales y punto, sin depender del idioma");
            Assert.AreEqual("40 %", Row(rows, "Ráfaga de urgencia: momento").CurrentText, "Las fracciones se muestran en %");
        }

        [Test]
        public void Delta_PorcentajeConSigno()
        {
            LevelDNA b = new LevelDNA { nightDuration = 200f, startHope = 80f };
            LevelDNA c = new LevelDNA { nightDuration = 250f, startHope = 40f };
            List<DnaRow> rows = LevelDnaRows.Compare(b, c);

            Assert.AreEqual("+25%", Row(rows, "Duración de la noche").DeltaText);
            Assert.AreEqual("-50%", Row(rows, "Esperanza inicial de las zonas").DeltaText);
        }

        [Test]
        public void Delta_CambiosMinimosCuentanComoIguales()
        {
            LevelDNA b = new LevelDNA { nightDuration = 240f };
            LevelDNA c = new LevelDNA { nightDuration = 240.5f };
            Assert.AreEqual("=", Row(LevelDnaRows.Compare(b, c), "Duración de la noche").DeltaText);
        }

        [Test]
        public void Delta_DesdeCeroNoInventaPorcentaje()
        {
            LevelDNA b = new LevelDNA { waveCount = 0 };
            LevelDNA same = new LevelDNA { waveCount = 0 };
            LevelDNA more = new LevelDNA { waveCount = 2 };

            Assert.AreEqual("=", Row(LevelDnaRows.Compare(b, same), "Oleadas de sombras").DeltaText);
            Assert.AreEqual("", Row(LevelDnaRows.Compare(b, more), "Oleadas de sombras").DeltaText, "De 0 a 2 no es un porcentaje");
        }

        [Test]
        public void Texto_IgualOCambia()
        {
            LevelDNA b = new LevelDNA { tone = "esperanzado" };
            LevelDNA c = new LevelDNA { tone = "tenso" };
            DnaRow tone = Row(LevelDnaRows.Compare(b, c), "Tono emocional");
            Assert.AreEqual("esperanzado", tone.BaseText);
            Assert.AreEqual("tenso", tone.CurrentText);
            Assert.AreEqual("cambia", tone.DeltaText);
        }

        [Test]
        public void Texto_VacioSeMuestraComoGuion()
        {
            LevelDNA b = new LevelDNA { tone = "" };
            DnaRow tone = Row(LevelDnaRows.Compare(b, b), "Tono emocional");
            Assert.AreEqual("-", tone.BaseText);
            Assert.AreEqual("=", tone.DeltaText);
        }

        [Test]
        public void Automatico_SeMuestraComoAuto()
        {
            Assert.AreEqual("auto", LevelDnaRows.AutoCountText(-1));
            Assert.AreEqual("auto", LevelDnaRows.AutoCountText(-7));
            Assert.AreEqual("0", LevelDnaRows.AutoCountText(0));
            Assert.AreEqual("3", LevelDnaRows.HuntCountText(3));
        }

        [Test]
        public void TipoDeMision_ElDeMasPeso()
        {
            LevelDNA dna = new LevelDNA { questTypeWeights = new float[] { 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, 0.2f, 0.3f } };
            Assert.AreEqual("Doble objetivo", LevelDnaRows.TopQuestType(dna));
        }

        [Test]
        public void TipoDeMision_EmpateGanaElPrimero()
        {
            LevelDNA dna = new LevelDNA { questTypeWeights = new float[] { 0.2f, 0.4f, 0.4f, 0f, 0f, 0f, 0f } };
            Assert.AreEqual("Recolección", LevelDnaRows.TopQuestType(dna));
        }

        [Test]
        public void TipoDeMision_SinPesosEsGuion()
        {
            Assert.AreEqual("-", LevelDnaRows.TopQuestType(null));
            Assert.AreEqual("-", LevelDnaRows.TopQuestType(new LevelDNA { questTypeWeights = null }));
            Assert.AreEqual("-", LevelDnaRows.TopQuestType(new LevelDNA { questTypeWeights = System.Array.Empty<float>() }));
        }

        [Test]
        public void TipoDeMision_PesosDeMasNoSeSalenDeLaLista()
        {
            LevelDNA dna = new LevelDNA { questTypeWeights = new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 9f } };
            Assert.AreEqual("Rescate de Esperanza", LevelDnaRows.TopQuestType(dna));
        }
    }
}
