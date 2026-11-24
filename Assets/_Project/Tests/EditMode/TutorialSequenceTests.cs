using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>Pruebas del avance del tutorial (TutorialSequence y TutorialRules, lógica pura).</summary>
    public class TutorialSequenceTests
    {
        private static bool Yes()
        {
            return true;
        }

        private static bool No()
        {
            return false;
        }

        [Test]
        public void EmpiezaEnElPrimerPaso()
        {
            TutorialSequence s = new TutorialSequence(new[] { 2f, 2f, 3f });
            s.Begin(null);
            Assert.AreEqual(0, s.Index);
            Assert.AreEqual(3, s.Count);
            Assert.IsFalse(s.Finished);
        }

        [Test]
        public void NoAvanzaSoloPorLeer()
        {
            TutorialSequence s = new TutorialSequence(new[] { 2f, 2f });
            s.Begin(null);
            for (int i = 0; i < 600; i++) Assert.IsFalse(s.Tick(0.1f, No, null));
            Assert.AreEqual(0, s.Index, "sin hacer la acción, el paso no cambia aunque pase el tiempo");
        }

        [Test]
        public void NoAvanzaAntesDelTiempoMinimo()
        {
            TutorialSequence s = new TutorialSequence(new[] { 2f, 2f });
            s.Begin(null);
            Assert.IsFalse(s.Tick(1f, Yes, null), "1 s de 2");
            Assert.IsFalse(s.Tick(0.5f, Yes, null), "1.5 s de 2");
            Assert.IsTrue(s.Tick(0.5f, Yes, null), "2 s: ya puede avanzar");
            Assert.AreEqual(1, s.Index);
            Assert.AreEqual(0f, s.StepTime, "el tiempo del paso vuelve a empezar");
        }

        [Test]
        public void LaAccionSoloSeConsultaPasadoElMinimo()
        {
            int calls = 0;
            TutorialSequence s = new TutorialSequence(new[] { 2f });
            s.Begin(null);
            s.Tick(1f, () => { calls++; return true; }, null);
            Assert.AreEqual(0, calls);
            s.Tick(1f, () => { calls++; return true; }, null);
            Assert.AreEqual(1, calls);
            Assert.IsTrue(s.Finished);
        }

        [Test]
        public void AvanzaPasoAPasoHastaTerminar()
        {
            TutorialSequence s = new TutorialSequence(new[] { 1f, 1f, 1f });
            s.Begin(null);
            Assert.IsTrue(s.Tick(1f, Yes, null));
            Assert.IsTrue(s.Tick(1f, Yes, null));
            Assert.IsTrue(s.Tick(1f, Yes, null));
            Assert.IsTrue(s.Finished);
            Assert.IsFalse(s.Tick(1f, Yes, null), "terminado no se mueve más");
            Assert.AreEqual(3, s.Index);
        }

        [Test]
        public void SaltaLosPasosNoDisponibles()
        {
            // Pasos 1 y 2 piden Recuerdos que aún no se tienen (ataque cargado, contraataque).
            TutorialSequence s = new TutorialSequence(new[] { 1f, 1f, 1f, 1f });
            s.Begin(i => i != 1 && i != 2);
            Assert.AreEqual(0, s.Index);
            Assert.IsTrue(s.Tick(1f, Yes, i => i != 1 && i != 2));
            Assert.AreEqual(3, s.Index, "del paso 0 al 3");
        }

        [Test]
        public void SiNoHayNadaDisponibleTerminaAlEmpezar()
        {
            TutorialSequence s = new TutorialSequence(new[] { 1f, 1f });
            s.Begin(i => false);
            Assert.IsTrue(s.Finished);
        }

        [Test]
        public void SaltarTutorialLoTerminaYa()
        {
            TutorialSequence s = new TutorialSequence(new[] { 1f, 1f });
            s.Begin(null);
            s.Skip();
            Assert.IsTrue(s.Finished);
            Assert.IsFalse(s.Tick(5f, Yes, null));
        }

        [Test]
        public void LaPausaNoCuentaTiempo()
        {
            TutorialSequence s = new TutorialSequence(new[] { 2f });
            s.Begin(null);
            for (int i = 0; i < 100; i++) s.Tick(0f, Yes, null);
            Assert.AreEqual(0f, s.StepTime);
            Assert.IsFalse(s.Finished);
        }

        [Test]
        public void CapituloSegunLaNoche()
        {
            Assert.AreEqual(TutorialRules.Night1Chapter, TutorialRules.ChapterFor(1, false, true, false));
            Assert.AreEqual(TutorialRules.NoChapter, TutorialRules.ChapterFor(1, false, false, false), "Noche 1 ya hecha");
            Assert.AreEqual(TutorialRules.Night2Chapter, TutorialRules.ChapterFor(2, false, false, false));
            Assert.AreEqual(TutorialRules.Night2Chapter, TutorialRules.ChapterFor(2, false, true, false), "aunque la Noche 1 quedara a medias");
            Assert.AreEqual(TutorialRules.NoChapter, TutorialRules.ChapterFor(2, false, false, true), "solo la primera vez");
            Assert.AreEqual(TutorialRules.NoChapter, TutorialRules.ChapterFor(3, false, true, false));
            Assert.AreEqual(TutorialRules.NoChapter, TutorialRules.ChapterFor(6, false, false, false));
        }

        [Test]
        public void SaltarTutorialQuitaLosCapitulos()
        {
            Assert.AreEqual(TutorialRules.NoChapter, TutorialRules.ChapterFor(1, true, true, false));
            Assert.AreEqual(TutorialRules.NoChapter, TutorialRules.ChapterFor(2, true, false, false));
        }

        [Test]
        public void AvisosDeUnaVez()
        {
            Assert.IsTrue(TutorialRules.ShowOneShot(false, false, true));
            Assert.IsFalse(TutorialRules.ShowOneShot(false, true, true), "ya visto");
            Assert.IsFalse(TutorialRules.ShowOneShot(false, false, false), "aún no pasa");
            Assert.IsFalse(TutorialRules.ShowOneShot(true, false, true), "tutorial saltado");
        }
    }
}
