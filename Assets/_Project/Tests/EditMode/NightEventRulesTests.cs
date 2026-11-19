using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>Pruebas de cuándo empieza el evento memorable de cada noche (NightEventRules).</summary>
    public class NightEventRulesTests
    {
        [Test]
        public void SeDisparaEnSuNocheTrasElUmbral()
        {
            Assert.IsTrue(NightEventRules.ShouldTrigger(3, 3, 0.56f, 0.55f, false, false));
        }

        [Test]
        public void NoSeDisparaEnOtraNoche()
        {
            Assert.IsFalse(NightEventRules.ShouldTrigger(2, 3, 0.6f, 0.55f, false, false));
            Assert.IsFalse(NightEventRules.ShouldTrigger(6, 5, 0.6f, 0.05f, false, false), "Las noches generadas posteriores no lo repiten");
        }

        [Test]
        public void NoSeRepiteSiYaSeVio()
        {
            Assert.IsFalse(NightEventRules.ShouldTrigger(4, 4, 0.75f, 0.7f, true, false));
        }

        [Test]
        public void EsperaSiHayAlgoEnCurso()
        {
            Assert.IsFalse(NightEventRules.ShouldTrigger(4, 4, 0.75f, 0.7f, false, true));
        }

        [Test]
        public void NoSeDisparaAntesDelUmbral()
        {
            Assert.IsFalse(NightEventRules.ShouldTrigger(3, 3, 0.54f, 0.55f, false, false));
            Assert.IsTrue(NightEventRules.ShouldTrigger(3, 3, 0.55f, 0.55f, false, false), "Justo en el umbral, sí");
        }

        [Test]
        public void DespuesDelNoventaPorCientoYaNoOcurre()
        {
            Assert.IsTrue(NightEventRules.ShouldTrigger(3, 3, 0.89f, 0.55f, false, false));
            Assert.IsFalse(NightEventRules.ShouldTrigger(3, 3, 0.90f, 0.55f, false, false));
            Assert.IsFalse(NightEventRules.ShouldTrigger(3, 3, 1f, 0.55f, false, false));
            Assert.IsTrue(NightEventRules.IsTooLate(0.9f));
            Assert.IsFalse(NightEventRules.IsTooLate(0.89f));
        }
    }
}
