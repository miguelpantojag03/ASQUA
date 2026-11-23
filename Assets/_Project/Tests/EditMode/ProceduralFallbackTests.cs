using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>
    /// Pruebas del generador local (ProceduralFallback, lógica pura): las fórmulas públicas de dificultad,
    /// el tono y entradas raras (NaN, sin pesos, base sin caída) que LevelGenTests no recorre.
    /// </summary>
    public class ProceduralFallbackTests
    {
        private static LevelDNA BaseDna()
        {
            LevelDNA dna = new LevelDNA { seed = 1, questTypeWeights = LevelValidator.DefaultWeights() };
            LevelValidator.Repair(dna);
            return dna;
        }

        // ------------------------------------------------------------ Hardness y SignedHardness

        [Test]
        public void Dureza_IgualALaBaseEsCero()
        {
            LevelDNA b = BaseDna();
            Assert.AreEqual(0f, ProceduralFallback.Hardness(b, b));
            Assert.AreEqual(0f, ProceduralFallback.SignedHardness(b, b));
        }

        [Test]
        public void Dureza_SeAcotaEntreSusLimites()
        {
            LevelDNA b = BaseDna();
            LevelDNA hard = b.Clone();
            hard.hopeDecayPerSecond = b.hopeDecayPerSecond * 10f;
            LevelDNA easy = b.Clone();
            easy.hopeDecayPerSecond = 0f;

            Assert.AreEqual(1f, ProceduralFallback.Hardness(hard, b));
            Assert.AreEqual(1f, ProceduralFallback.SignedHardness(hard, b));
            Assert.AreEqual(0f, ProceduralFallback.Hardness(easy, b), "Más fácil que la base cuenta como 0");
            Assert.AreEqual(-0.5f, ProceduralFallback.SignedHardness(easy, b), "Con signo no baja de -0.5");
        }

        [Test]
        public void Dureza_ConBaseSinCaidaEsCero()
        {
            LevelDNA b = BaseDna();
            b.hopeDecayPerSecond = 0f;
            Assert.AreEqual(0f, ProceduralFallback.Hardness(BaseDna(), b));
            Assert.AreEqual(0f, ProceduralFallback.SignedHardness(BaseDna(), b));
        }

        // ------------------------------------------------------------ Fórmulas

        [Test]
        public void VelocidadDeCaceria_VaDeFacilADificil()
        {
            Assert.AreEqual(ProceduralFallback.HuntSpeedEasy, ProceduralFallback.HuntSpeedFor(-0.5f), 0.0001f);
            Assert.AreEqual(ProceduralFallback.HuntSpeedHard, ProceduralFallback.HuntSpeedFor(1f), 0.0001f);
            Assert.AreEqual(4.6f, ProceduralFallback.HuntSpeedFor(0f), 0.0001f);
            Assert.AreEqual(ProceduralFallback.HuntSpeedEasy, ProceduralFallback.HuntSpeedFor(-3f), 0.0001f, "Se acota por abajo");
            Assert.AreEqual(ProceduralFallback.HuntSpeedHard, ProceduralFallback.HuntSpeedFor(9f), 0.0001f, "Se acota por arriba");
        }

        [Test]
        public void VelocidadDeCaceria_SiempreDentroDelRangoValido()
        {
            for (float s = -0.5f; s <= 1f; s += 0.05f)
            {
                float speed = ProceduralFallback.HuntSpeedFor(s);
                Assert.GreaterOrEqual(speed, LevelDnaLimits.HuntSpeedMin);
                Assert.LessOrEqual(speed, LevelDnaLimits.HuntSpeedMax);
            }
        }

        [Test]
        public void Tono_TablaCompleta()
        {
            Assert.AreEqual("esperanzado", ProceduralFallback.ToneFor(0.1f, 0.4f), "Noche suave y al jugador le va mal");
            Assert.AreEqual("melancolico", ProceduralFallback.ToneFor(0.1f, 0.6f), "Noche suave y le va bien");
            Assert.AreEqual("melancolico", ProceduralFallback.ToneFor(0.34f, 0.2f));
            Assert.AreEqual("tenso", ProceduralFallback.ToneFor(0.35f, 0.9f));
            Assert.AreEqual("tenso", ProceduralFallback.ToneFor(0.69f, 0.1f));
            Assert.AreEqual("climatico", ProceduralFallback.ToneFor(0.7f, 0.5f));
        }

        [Test]
        public void Tono_SiempreEsUnoDeLosValidos()
        {
            for (float h = 0f; h <= 1f; h += 0.1f)
            {
                for (float s = 0f; s <= 1f; s += 0.1f)
                {
                    Assert.IsNotNull(LevelValidator.NormalizeTone(ProceduralFallback.ToneFor(h, s)));
                }
            }
        }

        [Test]
        public void Apagavelas_FormulaEnSusBordes()
        {
            Assert.AreEqual(0, ProceduralFallback.SnuffersFor(-0.21f, 0f), "Noche fácil: ninguno");
            Assert.AreEqual(2, ProceduralFallback.SnuffersFor(-0.2f, 0f), "Justo en el borde ya salen");
            Assert.AreEqual(6, ProceduralFallback.SnuffersFor(1f, 1f));
            Assert.AreEqual(6, ProceduralFallback.SnuffersFor(1f, 5f), "La dureza se acota a 1");
            Assert.AreEqual(2, ProceduralFallback.SnuffersFor(0f, -1f), "Y por abajo a 0");
        }

        // ------------------------------------------------------------ Generate con entradas raras

        [Test]
        public void Generate_SinBase_LanzaExcepcion()
        {
            Assert.Throws<ArgumentNullException>(() => ProceduralFallback.Generate(null, null, 0.5f, 5, 1, null));
        }

        [Test]
        public void Generate_SinRegistro_NoFalla()
        {
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), null, 0.5f, 5, 1, null);
            CollectionAssert.IsEmpty(LevelValidator.Validate(dna));
        }

        [Test]
        public void Generate_HabilidadNaN_SeTrataComoNeutra()
        {
            LevelDNA a = ProceduralFallback.Generate(BaseDna(), null, float.NaN, 5, 3, new List<string>());
            LevelDNA b = ProceduralFallback.Generate(BaseDna(), null, 0.5f, 5, 3, new List<string>());
            Assert.AreEqual(0.5f, a.playerDifficulty);
            Assert.IsTrue(a.SameAs(b));
        }

        [Test]
        public void Generate_HabilidadFueraDeRango_SeAcota()
        {
            Assert.AreEqual(1f, ProceduralFallback.Generate(BaseDna(), null, 7f, 5, 3, null).playerDifficulty);
            Assert.AreEqual(0f, ProceduralFallback.Generate(BaseDna(), null, -2f, 5, 3, null).playerDifficulty);
        }

        [Test]
        public void Generate_BaseSinPesos_UsaLosPesosPorDefecto()
        {
            LevelDNA b = BaseDna();
            b.questTypeWeights = null;
            LevelDNA dna = ProceduralFallback.Generate(b, null, 0.5f, 5, 1, null);
            Assert.AreEqual(LevelDNA.QuestTypeCount, dna.questTypeWeights.Length);
            Assert.IsNull(b.questTypeWeights, "La base sigue sin tocar");
        }

        [Test]
        public void Generate_NoTocaLaNocheAnterior()
        {
            LevelDNA previous = ProceduralFallback.Generate(BaseDna(), null, 0.9f, 5, 11, null);
            LevelDNA snapshot = previous.Clone();
            ProceduralFallback.Generate(BaseDna(), previous, 0.9f, 6, 12, null);
            Assert.IsTrue(previous.SameAs(snapshot));
        }

        [Test]
        public void Generate_MarcaOrigenSemillaYTresFragmentos()
        {
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), null, 0.5f, 5, 4242, null);
            Assert.AreEqual(LevelDnaOrigin.Local, dna.origin);
            Assert.AreEqual(4242, dna.seed);
            Assert.AreEqual(3, dna.narrativeFragments.Length);
            CollectionAssert.AllItemsAreUnique(dna.narrativeFragments);
        }

        [Test]
        public void Generate_MisionesEntreTresYSeis()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                foreach (float skill in new[] { 0f, 0.5f, 1f })
                {
                    int quests = ProceduralFallback.Generate(BaseDna(), null, skill, 5, seed, null).questCount;
                    Assert.That(quests, Is.InRange(3, LevelDnaLimits.QuestCountMax), "semilla " + seed);
                }
            }
        }

        [Test]
        public void Generate_EncadenarNochesDificilesNoSaleDeRango()
        {
            LevelDNA previous = null;
            for (int night = 5; night < 30; night++)
            {
                List<string> log = new List<string>();
                LevelDNA dna = ProceduralFallback.Generate(BaseDna(), previous, 1f, night, night, log);
                CollectionAssert.IsEmpty(LevelValidator.Validate(dna), "noche " + night);
                previous = dna;
            }
        }
    }
}
