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
            DnaRow decay = rows.Find(r => r.Label.StartsWith("Caída", System.StringComparison.Ordinal));
            Assert.AreEqual("+100%", decay.DeltaText);
            DnaRow duration = rows.Find(r => r.Label.StartsWith("Duración", System.StringComparison.Ordinal));
            Assert.AreEqual("=", duration.DeltaText);
        }

        // ------------------------------------------------------------ Cacería del Heraldo

        [Test]
        public void LaCaceriaTieneValoresPorDefecto()
        {
            LevelDNA dna = new LevelDNA();
            Assert.AreEqual(LevelDnaLimits.HuntCountAuto, dna.huntCount);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedDefault, dna.huntSpeed);

            // Un JSON sin los campos nuevos (p. ej. el banco antiguo) se queda con esos valores.
            LevelDNA fromJson = UnityEngine.JsonUtility.FromJson<LevelDNA>("{\"seed\":3,\"tone\":\"tenso\"}");
            Assert.AreEqual(LevelDnaLimits.HuntCountAuto, fromJson.huntCount);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedDefault, fromJson.huntSpeed);
        }

        [Test]
        public void ElValidadorAcotaLaCaceria()
        {
            LevelDNA dna = BaseDna();
            dna.huntCount = 7;
            dna.huntSpeed = 9f;
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaLimits.HuntCountMax, dna.huntCount);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedMax, dna.huntSpeed);
            Assert.IsTrue(log.Exists(l => l.StartsWith("huntCount", System.StringComparison.Ordinal)), "Anota la corrección de huntCount");
            Assert.IsTrue(log.Exists(l => l.StartsWith("huntSpeed", System.StringComparison.Ordinal)), "Anota la corrección de huntSpeed");

            dna = BaseDna();
            dna.huntCount = -4;
            dna.huntSpeed = 1f;
            LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaLimits.HuntCountMin, dna.huntCount);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedMin, dna.huntSpeed);

            dna = BaseDna();
            dna.huntSpeed = float.NaN;
            LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedDefault, dna.huntSpeed);
        }

        [Test]
        public void ElModoAutomaticoDeCaceriaEsValido()
        {
            LevelDNA dna = BaseDna();
            dna.huntCount = LevelDnaLimits.HuntCountAuto;
            Assert.AreEqual(0, LevelValidator.Validate(dna).Count);
            dna.huntCount = 2;
            Assert.AreEqual(0, LevelValidator.Validate(dna).Count);
        }

        [Test]
        public void ElFallbackSubeLasCaceriasEnNochesDuras()
        {
            LevelDNA hard = BaseDna();
            hard.hopeDecayPerSecond = 1.2f;
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), hard, 1f, 6, 11, null);
            Assert.Greater(ProceduralFallback.SignedHardness(dna, BaseDna()), 0.6f);
            Assert.AreEqual(3, dna.huntCount);
            Assert.Greater(dna.huntSpeed, 5f);
            Assert.LessOrEqual(dna.huntSpeed, ProceduralFallback.HuntSpeedHard + 0.0001f);
        }

        [Test]
        public void ElFallbackQuitaLasCaceriasEnNochesFaciles()
        {
            LevelDNA easy = BaseDna();
            easy.hopeDecayPerSecond = 0.3f;
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), easy, 0f, 6, 11, null);
            Assert.Less(ProceduralFallback.SignedHardness(dna, BaseDna()), -0.2f);
            Assert.AreEqual(0, dna.huntCount);
            Assert.AreEqual(ProceduralFallback.HuntSpeedEasy, dna.huntSpeed, 0.01f);
        }

        [Test]
        public void ElFallbackMantieneLaCaceriaDeLaBaseEnNochesIntermedias()
        {
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), null, 0.5f, 5, 3, null);
            float signed = ProceduralFallback.SignedHardness(dna, BaseDna());
            Assert.IsTrue(signed >= -0.2f && signed <= 0.6f, "Dificultad intermedia: " + signed);
            Assert.AreEqual(LevelDnaLimits.HuntCountAuto, dna.huntCount);
            Assert.IsTrue(dna.huntSpeed > ProceduralFallback.HuntSpeedEasy && dna.huntSpeed < ProceduralFallback.HuntSpeedHard);
        }

        [Test]
        public void LaVelocidadDeCaceriaCreceConLaDificultad()
        {
            Assert.AreEqual(ProceduralFallback.HuntSpeedEasy, ProceduralFallback.HuntSpeedFor(-0.5f), 0.0001f);
            Assert.AreEqual(ProceduralFallback.HuntSpeedHard, ProceduralFallback.HuntSpeedFor(1f), 0.0001f);
            Assert.Less(ProceduralFallback.HuntSpeedFor(0f), ProceduralFallback.HuntSpeedFor(0.5f));
        }

        [Test]
        public void LaTablaDeAdnMuestraLaCaceria()
        {
            LevelDNA baseDna = BaseDna();
            LevelDNA current = BaseDna();
            current.huntCount = 3;
            current.huntSpeed = 5.4f;

            List<DnaRow> rows = LevelDnaRows.Compare(baseDna, current);

            DnaRow count = rows.Find(r => r.Label == "Cacerías del Heraldo");
            Assert.AreEqual("auto", count.BaseText);
            Assert.AreEqual("3", count.CurrentText);
            Assert.AreEqual("cambia", count.DeltaText);
            DnaRow speed = rows.Find(r => r.Label == "Velocidad de la cacería");
            Assert.AreEqual("4.8 m/s", speed.BaseText);
            Assert.AreEqual("5.4 m/s", speed.CurrentText);
        }

        // ------------------------------------------------------------ Apagavelas

        [Test]
        public void LosApagavelasSonAutomaticosPorDefecto()
        {
            Assert.AreEqual(LevelDnaLimits.SnuffersAuto, new LevelDNA().snuffers);
            // Un JSON sin el campo (p. ej. el banco de IA actual) se queda en automático.
            LevelDNA fromJson = UnityEngine.JsonUtility.FromJson<LevelDNA>("{\"seed\":4}");
            Assert.AreEqual(LevelDnaLimits.SnuffersAuto, fromJson.snuffers);
        }

        [Test]
        public void ElValidadorAcotaLosApagavelas()
        {
            LevelDNA dna = BaseDna();
            dna.snuffers = 12;
            List<string> log = LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaLimits.SnuffersMax, dna.snuffers);
            Assert.IsTrue(log.Exists(l => l.StartsWith("snuffers", System.StringComparison.Ordinal)), "Anota la corrección");

            dna = BaseDna();
            dna.snuffers = -5;
            LevelValidator.Repair(dna);
            Assert.AreEqual(LevelDnaLimits.SnuffersMin, dna.snuffers);

            dna = BaseDna();
            dna.snuffers = 3;
            Assert.AreEqual(0, LevelValidator.Validate(dna).Count);
            dna.snuffers = LevelDnaLimits.SnuffersAuto;
            Assert.AreEqual(0, LevelValidator.Validate(dna).Count, "-1 (automático) es válido");
        }

        [Test]
        public void ElFallbackSubeLosApagavelasConLaDificultad()
        {
            LevelDNA hard = BaseDna();
            hard.hopeDecayPerSecond = 1.2f;
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), hard, 1f, 6, 11, null);
            Assert.GreaterOrEqual(dna.snuffers, 5, "Noche dura: 2 + hardness*4 con hardness cerca de 1");

            LevelDNA middle = ProceduralFallback.Generate(BaseDna(), null, 0.5f, 5, 3, null);
            Assert.That(middle.snuffers, Is.InRange(2, 3), "Noche intermedia");
        }

        [Test]
        public void ElFallbackQuitaLosApagavelasEnNochesFaciles()
        {
            LevelDNA easy = BaseDna();
            easy.hopeDecayPerSecond = 0.3f;
            LevelDNA dna = ProceduralFallback.Generate(BaseDna(), easy, 0f, 6, 11, null);
            Assert.AreEqual(0, dna.snuffers);
        }

        [Test]
        public void LaFormulaDeApagavelas()
        {
            Assert.AreEqual(0, ProceduralFallback.SnuffersFor(-0.3f, 0f));
            Assert.AreEqual(2, ProceduralFallback.SnuffersFor(0f, 0f));
            Assert.AreEqual(6, ProceduralFallback.SnuffersFor(1f, 1f));
            Assert.AreEqual(4, ProceduralFallback.SnuffersFor(0.5f, 0.5f));
        }

        [Test]
        public void LaTablaDeAdnMuestraLosApagavelas()
        {
            LevelDNA current = BaseDna();
            current.snuffers = 5;
            DnaRow row = LevelDnaRows.Compare(BaseDna(), current).Find(r => r.Label == "Apagavelas");
            Assert.AreEqual("auto", row.BaseText);
            Assert.AreEqual("5", row.CurrentText);
        }
    }
}
