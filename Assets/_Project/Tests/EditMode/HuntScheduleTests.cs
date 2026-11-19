using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>Pruebas de los momentos de la Cacería del Heraldo (HuntSchedule, lógica pura).</summary>
    public class HuntScheduleTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void DevuelveLaCantidadPedida()
        {
            for (int count = 1; count <= 3; count++)
            {
                Assert.AreEqual(count, HuntSchedule.Compute(count, 42).Count, "huntCount " + count);
            }
        }

        [Test]
        public void SinCaceriasDevuelveVacio()
        {
            Assert.AreEqual(0, HuntSchedule.Compute(0, 42).Count);
            Assert.AreEqual(0, HuntSchedule.Compute(-1, 42).Count);
        }

        [Test]
        public void LosMomentosQuedanEnRangoYSeparados()
        {
            for (int count = 1; count <= HuntSchedule.MaxHunts; count++)
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    List<float> moments = HuntSchedule.Compute(count, seed);
                    for (int i = 0; i < moments.Count; i++)
                    {
                        Assert.GreaterOrEqual(moments[i], HuntSchedule.Earliest - Tolerance);
                        Assert.LessOrEqual(moments[i], HuntSchedule.Latest + Tolerance);
                        if (i > 0) Assert.GreaterOrEqual(moments[i] - moments[i - 1], HuntSchedule.MinGap - Tolerance, "semilla " + seed);
                    }
                }
            }
        }

        [Test]
        public void EsDeterministaConLaMismaSemilla()
        {
            List<float> a = HuntSchedule.Compute(3, 777);
            List<float> b = HuntSchedule.Compute(3, 777);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void SemillasDistintasCambianLosMomentos()
        {
            CollectionAssert.AreNotEqual(HuntSchedule.Compute(2, 1), HuntSchedule.Compute(2, 2));
        }

        [Test]
        public void CantidadAutomaticaPorNoche()
        {
            Assert.AreEqual(0, HuntSchedule.CountForNight(1, -1));
            Assert.AreEqual(0, HuntSchedule.CountForNight(2, -1));
            Assert.AreEqual(1, HuntSchedule.CountForNight(3, -1));
            Assert.AreEqual(2, HuntSchedule.CountForNight(4, -1));
            Assert.AreEqual(2, HuntSchedule.CountForNight(9, -1));
        }

        [Test]
        public void ElNivelPuedeFijarLaCantidad()
        {
            Assert.AreEqual(3, HuntSchedule.CountForNight(1, 3));
            Assert.AreEqual(0, HuntSchedule.CountForNight(6, 0));
        }
    }
}
