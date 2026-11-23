using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>
    /// Pruebas de LevelDNA (lógica pura): valores por defecto, copia profunda y comparación campo a campo.
    /// SameAs es lo que garantiza que BASE_DNA no cambie, así que cada campo tiene que contar.
    /// </summary>
    public class LevelDnaTests
    {
        [Test]
        public void PorDefecto_SonLosValoresDeLaNocheUno()
        {
            LevelDNA dna = new LevelDNA();
            Assert.AreEqual(LevelDnaOrigin.Base, dna.origin);
            Assert.AreEqual("melancolico", dna.tone);
            Assert.AreEqual(240f, dna.nightDuration);
            Assert.AreEqual(70f, dna.startHope);
            Assert.AreEqual(6, dna.questCount);
            Assert.AreEqual(LevelDNA.QuestTypeCount, dna.questTypeWeights.Length);
            Assert.AreEqual(LevelDnaLimits.HuntCountAuto, dna.huntCount);
            Assert.AreEqual(LevelDnaLimits.HuntSpeedDefault, dna.huntSpeed);
            Assert.AreEqual(LevelDnaLimits.SnuffersAuto, dna.snuffers);
            Assert.IsNotNull(dna.narrativeFragments);
            Assert.AreEqual(0, dna.narrativeFragments.Length);
        }

        [Test]
        public void PorDefecto_YaEsValidoSalvoLosPesos()
        {
            // Los pesos nacen en cero (los rellena el set o el validador); lo demás ya cumple los rangos.
            LevelDNA dna = new LevelDNA();
            dna.questTypeWeights = LevelValidator.DefaultWeights();
            CollectionAssert.IsEmpty(LevelValidator.Validate(dna));
        }

        [Test]
        public void Clone_EsIgualAlOriginal()
        {
            LevelDNA dna = Sample();
            Assert.IsTrue(dna.Clone().SameAs(dna));
        }

        [Test]
        public void Clone_NoCompartePesosNiFragmentos()
        {
            LevelDNA dna = Sample();
            LevelDNA copy = dna.Clone();

            copy.questTypeWeights[0] = 99f;
            copy.narrativeFragments[0] = "otro";

            Assert.AreEqual(0.4f, dna.questTypeWeights[0]);
            Assert.AreEqual("Lo que aún respira", dna.narrativeFragments[0]);
        }

        [Test]
        public void Clone_ConArreglosNulos_NoFalla()
        {
            LevelDNA dna = new LevelDNA { questTypeWeights = null, narrativeFragments = null };
            LevelDNA copy = dna.Clone();
            Assert.IsNull(copy.questTypeWeights);
            Assert.IsNull(copy.narrativeFragments);
            Assert.IsTrue(copy.SameAs(dna));
        }

        [Test]
        public void SameAs_ConNulo_EsFalso()
        {
            Assert.IsFalse(new LevelDNA().SameAs(null));
        }

        [Test]
        public void SameAs_DetectaCadaCampo()
        {
            System.Action<LevelDNA>[] changes =
            {
                d => d.seed++,
                d => d.origin = LevelDnaOrigin.Local,
                d => d.tone = "tenso",
                d => d.playerDifficulty += 0.1f,
                d => d.nightDuration += 1f,
                d => d.startHope += 1f,
                d => d.hopeDecayPerSecond += 0.1f,
                d => d.hopeRisePerSecond += 0.1f,
                d => d.penumbraStartRadius += 1f,
                d => d.penumbraEndRadius += 1f,
                d => d.fastPenumbraCurve = !d.fastPenumbraCurve,
                d => d.fireflySpawnInterval += 1f,
                d => d.fireflyMaxActive++,
                d => d.surgeEnabled = !d.surgeEnabled,
                d => d.surgeAt += 0.01f,
                d => d.surgeSeconds += 1f,
                d => d.surgeMeters += 1f,
                d => d.surgeDecayMultiplier += 0.1f,
                d => d.questCount++,
                d => d.waveCount++,
                d => d.creaturesPerWave++,
                d => d.hopeDrainPerCreature += 0.1f,
                d => d.heraldoEnabled = !d.heraldoEnabled,
                d => d.heraldoAppearAt += 0.01f,
                d => d.heraldoChaseSpeed += 0.1f,
                d => d.huntCount++,
                d => d.huntSpeed += 0.1f,
                d => d.snuffers++,
                d => d.questTypeWeights[3] += 0.1f,
                d => d.questTypeWeights = new float[3],
                d => d.questTypeWeights = null,
                d => d.narrativeFragments[1] = "distinto",
                d => d.narrativeFragments = System.Array.Empty<string>(),
                d => d.narrativeFragments = null
            };

            LevelDNA original = Sample();
            for (int i = 0; i < changes.Length; i++)
            {
                LevelDNA changed = original.Clone();
                changes[i](changed);
                Assert.IsFalse(original.SameAs(changed), "El cambio #" + i + " no se detectó");
                Assert.IsFalse(changed.SameAs(original), "SameAs debe ser simétrico (cambio #" + i + ")");
            }
        }

        [Test]
        public void SameAs_CubreTodosLosCamposPublicos()
        {
            // Por reflexión: si se añade un campo al ADN y no se compara en SameAs, esta prueba falla.
            LevelDNA original = Sample();
            foreach (System.Reflection.FieldInfo field in typeof(LevelDNA).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                LevelDNA changed = original.Clone();
                field.SetValue(changed, Different(field.FieldType, field.GetValue(changed)));
                Assert.IsFalse(original.SameAs(changed), "SameAs no compara el campo " + field.Name);
            }
        }

        private static object Different(System.Type type, object value)
        {
            if (type == typeof(int)) return (int)value + 1;
            if (type == typeof(float)) return (float)value + 1f;
            if (type == typeof(bool)) return !(bool)value;
            if (type == typeof(string)) return value + "_otro";
            if (type == typeof(float[])) return new float[] { 123f };
            if (type == typeof(string[])) return new[] { "otro" };
            Assert.Fail("Tipo de campo sin cubrir en la prueba: " + type.Name);
            return null;
        }

        private static LevelDNA Sample()
        {
            return new LevelDNA
            {
                seed = 7,
                questTypeWeights = new float[] { 0.4f, 0.2f, 0.1f, 0.1f, 0f, 0.1f, 0.1f },
                narrativeFragments = new[] { "Lo que aún respira", "Una luz por cada nombre" }
            };
        }
    }
}
