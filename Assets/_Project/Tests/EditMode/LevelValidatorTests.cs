using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>
    /// Pruebas del LevelValidator (lógica pura) en los bordes que no cubre LevelGenTests:
    /// texto para TextMeshPro, tonos con tildes, NaN e infinitos, y que Validate no toque el ADN.
    /// </summary>
    public class LevelValidatorTests
    {
        // ------------------------------------------------------------ SanitizeText

        [Test]
        public void Texto_NuloOVacio_DevuelveCadenaVacia()
        {
            Assert.AreEqual("", LevelValidator.SanitizeText(null));
            Assert.AreEqual("", LevelValidator.SanitizeText(""));
            Assert.AreEqual("", LevelValidator.SanitizeText("   \t \n "));
        }

        [Test]
        public void Texto_QuitaEtiquetasYSignosSueltos()
        {
            Assert.AreEqual("Una luz", LevelValidator.SanitizeText("<color=red>Una</color> <b>luz</b>"));
            Assert.AreEqual("a b", LevelValidator.SanitizeText("a < b"));
            Assert.AreEqual("ab", LevelValidator.SanitizeText("a>b"));
        }

        [Test]
        public void Texto_ControlesYEspaciosSeReducenAUnEspacio()
        {
            Assert.AreEqual("La ciudad guarda silencio", LevelValidator.SanitizeText("  La\tciudad\r\n\u0007guarda   silencio "));
        }

        [Test]
        public void Texto_LargoSeRecortaAlMaximo()
        {
            string clean = LevelValidator.SanitizeText(new string('x', 500));
            Assert.AreEqual(LevelDnaLimits.MaxFragmentLength, clean.Length);
        }

        [Test]
        public void Texto_ElRecorteNoParteUnEmoji()
        {
            // El emoji (2 unidades UTF-16) empieza justo en la posición 120.
            string text = new string('a', LevelDnaLimits.MaxFragmentLength - 1) + "\U0001F56F" + " final";
            string clean = LevelValidator.SanitizeText(text);

            Assert.LessOrEqual(clean.Length, LevelDnaLimits.MaxFragmentLength);
            Assert.IsFalse(char.IsHighSurrogate(clean[clean.Length - 1]), "No puede quedar media pareja sustituta al final");
        }

        [Test]
        public void Texto_EmojiCompletoDentroDelLimiteSeConserva()
        {
            string text = "Una vela \U0001F56F";
            Assert.AreEqual(text, LevelValidator.SanitizeText(text));
        }

        // ------------------------------------------------------------ Tono

        [TestCase("melancolico", "melancolico")]
        [TestCase("Melancólico", "melancolico")]
        [TestCase("  TENSO ", "tenso")]
        [TestCase("Climático", "climatico")]
        [TestCase("esperanzado", "esperanzado")]
        public void Tono_SeNormaliza(string input, string expected)
        {
            Assert.AreEqual(expected, LevelValidator.NormalizeTone(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("alegre")]
        public void Tono_DesconocidoDevuelveNull(string input)
        {
            Assert.IsNull(LevelValidator.NormalizeTone(input));
        }

        [Test]
        public void Tono_InvalidoSeReparaYSeAnota()
        {
            LevelDNA dna = new LevelDNA { tone = "furioso" };
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual("melancolico", dna.tone);
            Assert.IsTrue(log.Exists(l => l.Contains("furioso")));
        }

        [Test]
        public void Tono_ConMayusculaSeGuardaCanonicoSinAnotar()
        {
            LevelDNA dna = new LevelDNA { tone = "Tenso" };
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual("tenso", dna.tone);
            Assert.IsFalse(log.Exists(l => l.StartsWith("tone", System.StringComparison.Ordinal)));
        }

        // ------------------------------------------------------------ Números raros

        [Test]
        public void NaN_EInfinitos_VuelvenAlValorPorDefecto()
        {
            LevelDNA dna = new LevelDNA
            {
                nightDuration = float.NaN,
                startHope = float.PositiveInfinity,
                hopeDecayPerSecond = float.NegativeInfinity,
                playerDifficulty = float.NaN,
                huntSpeed = float.NaN
            };
            List<string> log = LevelValidator.Repair(dna);

            Assert.AreEqual(240f, dna.nightDuration);
            Assert.AreEqual(70f, dna.startHope);
            Assert.AreEqual(0.6f, dna.hopeDecayPerSecond);
            Assert.AreEqual(0.5f, dna.playerDifficulty);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedDefault, dna.huntSpeed);
            Assert.GreaterOrEqual(log.Count, 5, "Cada valor reparado queda anotado");
        }

        [Test]
        public void HuntSpeedAusente_TomaElValorPorDefectoYSeAnota()
        {
            LevelDNA dna = new LevelDNA { huntSpeed = 0f };
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedDefault, dna.huntSpeed);
            Assert.IsTrue(log.Exists(l => l.Contains("huntSpeed ausente")));
        }

        [Test]
        public void OrigenVacio_PasaAReglasLocales()
        {
            LevelDNA dna = new LevelDNA { origin = "" };
            LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaOrigin.Local, dna.origin);
        }

        [Test]
        public void RafagaDesactivada_NoSeAdelanta()
        {
            LevelDNA dna = new LevelDNA { surgeEnabled = false, nightDuration = 60f, surgeSeconds = 15f, surgeAt = 0.8f };
            LevelValidator.Repair(dna);
            Assert.AreEqual(0.8f, dna.surgeAt, "Sin ráfaga no hace falta que acabe antes del alba");
        }

        // ------------------------------------------------------------ Validate y Repair

        [Test]
        public void Validate_NoModificaElAdn()
        {
            LevelDNA dna = new LevelDNA { nightDuration = 9999f, tone = "raro", questCount = 50 };
            LevelDNA before = dna.Clone();

            List<string> problems = LevelValidator.Validate(dna);

            Assert.Greater(problems.Count, 0);
            Assert.IsTrue(dna.SameAs(before), "Validate trabaja sobre una copia");
        }

        [Test]
        public void Nulo_SeInformaSinExcepcion()
        {
            Assert.AreEqual(1, LevelValidator.Validate(null).Count);
            Assert.AreEqual(1, LevelValidator.Repair(null).Count);
        }

        [Test]
        public void RepararDosVeces_LaSegundaNoCambiaNada()
        {
            LevelDNA dna = new LevelDNA
            {
                nightDuration = 5f,
                penumbraStartRadius = 41f,
                penumbraEndRadius = 40f,
                questTypeWeights = new float[] { 3f, -1f }
            };
            LevelValidator.Repair(dna);
            Assert.AreEqual(0, LevelValidator.Repair(dna).Count, "Un ADN reparado ya es válido");
        }

        [Test]
        public void Pesos_DeMasSeRecortanASiete()
        {
            LevelDNA dna = new LevelDNA { questTypeWeights = new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 5f, 5f } };
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDNA.QuestTypeCount, dna.questTypeWeights.Length);
            Assert.AreEqual(1f / 7f, dna.questTypeWeights[0], 0.0001f);
            Assert.IsTrue(log.Exists(l => l.Contains("tenía 9 valores")));
        }

        [Test]
        public void Pesos_PorDefectoSumanUno()
        {
            float[] weights = LevelValidator.DefaultWeights();
            float sum = 0f;
            foreach (float w in weights) sum += w;
            Assert.AreEqual(LevelDNA.QuestTypeCount, weights.Length);
            Assert.AreEqual(1f, sum, 0.0001f);
        }

        [Test]
        public void Fragmentos_VaciosSeDescartanSinOcuparHueco()
        {
            LevelDNA dna = new LevelDNA { narrativeFragments = new[] { "<b></b>", "Uno", "  ", "Dos", "Tres", "Cuatro" } };
            LevelValidator.Repair(dna);
            CollectionAssert.AreEqual(new[] { "Uno", "Dos", "Tres" }, dna.narrativeFragments);
        }

        [Test]
        public void Fragmentos_NulosQuedanComoListaVacia()
        {
            LevelDNA dna = new LevelDNA { narrativeFragments = null };
            LevelValidator.Repair(dna);
            Assert.IsNotNull(dna.narrativeFragments);
            Assert.AreEqual(0, dna.narrativeFragments.Length);
        }
    }
}
