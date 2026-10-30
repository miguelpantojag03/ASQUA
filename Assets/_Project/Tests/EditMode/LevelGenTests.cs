using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>
    /// Pruebas del sistema de IA de niveles (lógica pura): el validador repara, el fallback es determinista,
    /// siempre produce un ADN válido sin red y BASE_DNA nunca cambia.
    /// Se ejecutan en Window > General > Test Runner > EditMode.
    /// </summary>
    public class LevelGenTests
    {
        /// <summary>ADN de la Noche 1 (los mismos valores que Data/Noches/Noche_01).</summary>
        private static LevelDNA BaseDna()
        {
            LevelDNA dna = new LevelDNA();
            dna.origin = LevelDnaOrigin.Base;
            dna.seed = 1;
            dna.nightDuration = 240f;
            dna.startHope = 70f;
            dna.hopeDecayPerSecond = 0.6f;
            dna.hopeRisePerSecond = 4f;
            dna.penumbraStartRadius = 60f;
            dna.penumbraEndRadius = 16f;
            dna.fireflySpawnInterval = 5f;
            dna.fireflyMaxActive = 5;
            dna.surgeAt = 0.55f;
            dna.surgeSeconds = 10f;
            dna.surgeMeters = 7f;
            dna.surgeDecayMultiplier = 1.7f;
            dna.questCount = 6;
            dna.questTypeWeights = new float[] { 0.3f, 0.2f, 0.1f, 0.1f, 0.1f, 0.1f, 0.1f };
            dna.waveCount = 2;
            dna.creaturesPerWave = 4;
            dna.hopeDrainPerCreature = 1.2f;
            dna.heraldoAppearAt = 0.25f;
            dna.heraldoChaseSpeed = 1.3f;
            return dna;
        }

        // ------------------------------------------------------------ validador

        [Test]
        public void ElAdnBaseYaEsValido()
        {
            Assert.AreEqual(0, LevelValidator.Validate(BaseDna()).Count);
        }

        [Test]
        public void UnAdnInvalidoSeRepara()
        {
            LevelDNA dna = BaseDna();
            dna.nightDuration = 99999f;
            dna.startHope = -5f;
            dna.hopeDecayPerSecond = float.NaN;
            dna.waveCount = 12;
            dna.tone = "furioso";

            List<string> log = LevelValidator.Repair(dna);

            Assert.IsTrue(log.Count >= 5, "Debe anotar cada corrección");
            Assert.AreEqual(LevelDnaLimits.DurationMax, dna.nightDuration);
            Assert.AreEqual(LevelDnaLimits.StartHopeMin, dna.startHope);
            Assert.IsFalse(float.IsNaN(dna.hopeDecayPerSecond));
            Assert.AreEqual(LevelDnaLimits.WaveCountMax, dna.waveCount);
            Assert.AreEqual("melancolico", dna.tone);
            Assert.AreEqual(0, LevelValidator.Validate(dna).Count, "Después de reparar ya es válido");
        }

        [Test]
        public void LosPesosSeCompletanYNormalizan()
        {
            LevelDNA dna = BaseDna();
            dna.questTypeWeights = new float[] { 0.4f, 0.3f, 0.2f, 0.1f };   // el ejemplo del handoff trae 4

            List<string> log = LevelValidator.Repair(dna);

            Assert.AreEqual(LevelDNA.QuestTypeCount, dna.questTypeWeights.Length);
            float sum = 0f;
            foreach (float w in dna.questTypeWeights) sum += w;
            Assert.AreEqual(1f, sum, 0.001f);
            Assert.IsTrue(log.Count > 0);
        }

        [Test]
        public void LosPesosNegativosOCerosSeArreglan()
        {
            LevelDNA dna = BaseDna();
            dna.questTypeWeights = new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            LevelValidator.Repair(dna);
            float sum = 0f;
            foreach (float w in dna.questTypeWeights) sum += w;
            Assert.AreEqual(1f, sum, 0.001f);
        }

        [Test]
        public void LaPenumbraSiempreSeCierra()
        {
            LevelDNA dna = BaseDna();
            dna.penumbraStartRadius = 45f;
            dna.penumbraEndRadius = 30f;
            LevelValidator.Repair(dna);
            Assert.IsTrue(dna.penumbraEndRadius <= dna.penumbraStartRadius - LevelDnaLimits.PenumbraMinClosing + 0.001f);
        }

        [Test]
        public void LaRafagaTerminaAntesDelAlba()
        {
            LevelDNA dna = BaseDna();
            dna.nightDuration = 60f;
            dna.surgeAt = 0.8f;
            dna.surgeSeconds = 15f;
            LevelValidator.Repair(dna);
            Assert.IsTrue(dna.surgeAt * dna.nightDuration + dna.surgeSeconds <= dna.nightDuration);
        }

        [Test]
        public void ElTextoSeSaneaAntesDeTextMeshPro()
        {
            Assert.AreEqual("hola", LevelValidator.SanitizeText("<color=red>hola</color>"));
            Assert.AreEqual("a b", LevelValidator.SanitizeText("a\n\n  b"));
            Assert.IsTrue(LevelValidator.SanitizeText(new string('x', 500)).Length <= LevelDnaLimits.MaxFragmentLength);
        }

        [Test]
        public void SoloSeConservanTresFragmentos()
        {
            LevelDNA dna = BaseDna();
            dna.narrativeFragments = new string[] { "uno", "dos", "tres", "cuatro", "<b></b>" };
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual(3, dna.narrativeFragments.Length);
            Assert.IsTrue(log.Count > 0, "Lo descartado queda anotado, no se pierde en silencio");
        }

        // ------------------------------------------------------------ generador local (fallback)

        [Test]
        public void ElFallbackEsDeterministaConLaMismaSemilla()
        {
            LevelDNA a = ProceduralFallback.Generate(BaseDna(), null, 0.7f, 5, 12345, null);
            LevelDNA b = ProceduralFallback.Generate(BaseDna(), null, 0.7f, 5, 12345, null);
            Assert.IsTrue(a.SameAs(b));
        }

        [Test]
        public void SemillasDistintasDanNivelesDistintos()
        {
            LevelDNA a = ProceduralFallback.Generate(BaseDna(), null, 0.7f, 5, 1, null);
            LevelDNA b = ProceduralFallback.Generate(BaseDna(), null, 0.7f, 5, 2, null);
            Assert.IsFalse(a.SameAs(b));
        }

        [Test]
        public void ElFallbackNuncaTocaLaBase()
        {
            LevelDNA baseDna = BaseDna();
            LevelDNA before = baseDna.Clone();
            LevelDNA previous = BaseDna();
            previous.hopeDecayPerSecond = 0.9f;
            LevelDNA previousBefore = previous.Clone();

            for (int night = 5; night < 15; night++)
            {
                ProceduralFallback.Generate(baseDna, previous, 0.8f, night, night * 31, null);
            }

            Assert.IsTrue(baseDna.SameAs(before), "BASE_DNA es inmutable");
            Assert.IsTrue(previous.SameAs(previousBefore), "El ADN anterior tampoco se modifica");
        }

        [Test]
        public void ElFallbackSiempreProduceUnAdnValidoSinRed()
        {
            float[] skills = { 0f, 0.2f, 0.5f, 0.8f, 1f };
            foreach (float skill in skills)
            {
                LevelDNA previous = null;
                for (int night = 5; night < 15; night++)
                {
                    LevelDNA dna = ProceduralFallback.Generate(BaseDna(), previous, skill, night, night * 101 + 7, null);
                    Assert.AreEqual(0, LevelValidator.Validate(dna).Count, "Noche " + night + " con habilidad " + skill);
                    Assert.AreEqual(LevelDnaOrigin.Local, dna.origin);
                    Assert.IsTrue(dna.narrativeFragments.Length > 0);
                    previous = dna;
                }
            }
        }

        [Test]
        public void LaDificultadSubeDeFormaCoherenteSiAlJugadorLeVaBien()
        {
            LevelDNA baseDna = BaseDna();
            LevelDNA previous = null;
            float firstDecay = 0f;
            for (int night = 5; night < 13; night++)
            {
                previous = ProceduralFallback.Generate(baseDna, previous, 0.9f, night, 99, null);
                if (night == 5) firstDecay = previous.hopeDecayPerSecond;
            }
            Assert.IsTrue(previous.hopeDecayPerSecond > firstDecay, "Ocho noches después la Esperanza cae más rápido");
            Assert.IsTrue(previous.hopeDecayPerSecond > baseDna.hopeDecayPerSecond);
        }

        [Test]
        public void SiAlJugadorLeVaMalElNivelSeSuaviza()
        {
            LevelDNA baseDna = BaseDna();
            LevelDNA hard = BaseDna();
            hard.hopeDecayPerSecond = 1.2f;
            LevelDNA easier = ProceduralFallback.Generate(baseDna, hard, 0f, 5, 5, null);
            Assert.IsTrue(easier.hopeDecayPerSecond < hard.hopeDecayPerSecond);
        }

        // ------------------------------------------------------------ copia y tabla

        [Test]
        public void ClonarNoComparteArreglos()
        {
            LevelDNA a = BaseDna();
            LevelDNA b = a.Clone();
            b.questTypeWeights[0] = 0.99f;
            Assert.AreNotEqual(0.99f, a.questTypeWeights[0]);
        }

        [Test]
        public void LaTablaDeAdnSeConstruyeYMarcaLosCambios()
        {
            LevelDNA baseDna = BaseDna();
            LevelDNA harder = BaseDna();
            harder.hopeDecayPerSecond = 1.2f;

            List<DnaRow> rows = LevelDnaRows.Compare(baseDna, harder);

            Assert.IsTrue(rows.Count > 10);
            DnaRow decay = rows.Find(r => r.Label.StartsWith("Caída"));
            Assert.AreEqual("+100%", decay.DeltaText);
            DnaRow duration = rows.Find(r => r.Label.StartsWith("Duración"));
            Assert.AreEqual("=", duration.DeltaText);
        }
    }
}
