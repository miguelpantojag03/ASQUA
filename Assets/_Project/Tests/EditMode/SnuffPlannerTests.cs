using System.Collections.Generic;
using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>Pruebas del Apagavelas (SnuffPlanner, lógica pura): cantidad, momentos y elección de blanco.</summary>
    public class SnuffPlannerTests
    {
        [Test]
        public void CantidadAutomaticaPorNoche()
        {
            Assert.AreEqual(0, SnuffPlanner.CountForNight(1, SnuffPlanner.Auto));
            Assert.AreEqual(1, SnuffPlanner.CountForNight(2, SnuffPlanner.Auto));
            Assert.AreEqual(2, SnuffPlanner.CountForNight(3, SnuffPlanner.Auto));
            Assert.AreEqual(3, SnuffPlanner.CountForNight(4, SnuffPlanner.Auto));
            Assert.AreEqual(4, SnuffPlanner.CountForNight(5, SnuffPlanner.Auto));
            Assert.AreEqual(4, SnuffPlanner.CountForNight(9, SnuffPlanner.Auto));
        }

        [Test]
        public void ElAdnFijaLaCantidad()
        {
            Assert.AreEqual(0, SnuffPlanner.CountForNight(5, 0));
            Assert.AreEqual(6, SnuffPlanner.CountForNight(1, 6));
            Assert.AreEqual(SnuffPlanner.MaxSnuffers, SnuffPlanner.CountForNight(3, 20));
        }

        [Test]
        public void ElHorarioTieneLaCantidadYElRango()
        {
            for (int count = 0; count <= SnuffPlanner.MaxSnuffers; count++)
            {
                List<float> moments = SnuffPlanner.Schedule(count, 77);
                Assert.AreEqual(count, moments.Count, "count " + count);
                for (int i = 0; i < moments.Count; i++)
                {
                    Assert.GreaterOrEqual(moments[i], SnuffPlanner.Earliest - 0.0001f);
                    Assert.LessOrEqual(moments[i], SnuffPlanner.Latest + 0.0001f);
                    if (i > 0) Assert.GreaterOrEqual(moments[i], moments[i - 1], "ordenados");
                }
            }
        }

        [Test]
        public void ElHorarioEsDeterminista()
        {
            CollectionAssert.AreEqual(SnuffPlanner.Schedule(4, 123), SnuffPlanner.Schedule(4, 123));
            CollectionAssert.AreNotEqual(SnuffPlanner.Schedule(3, 1), SnuffPlanner.Schedule(3, 2));
        }

        [Test]
        public void PrefiereLucesEncendidas()
        {
            SnuffCandidate on = new SnuffCandidate(0, true, 0.9f, 50f, 0f);
            SnuffCandidate off = new SnuffCandidate(1, false, 0.1f, 1f, 0f);
            Assert.Greater(SnuffPlanner.ScoreTarget(on, 0f, 0f), SnuffPlanner.ScoreTarget(off, 0f, 0f));
        }

        [Test]
        public void PrefiereLaZonaConMenosEsperanza()
        {
            SnuffCandidate rich = new SnuffCandidate(0, true, 0.9f, 5f, 0f);
            SnuffCandidate poor = new SnuffCandidate(1, true, 0.3f, 10f, 0f);
            var list = new List<SnuffCandidate> { rich, poor };
            Assert.AreEqual(1, SnuffPlanner.PickBest(list, 0f, 0f));
        }

        [Test]
        public void ConIgualEsperanzaPrefiereLaMasCercana()
        {
            var list = new List<SnuffCandidate>
            {
                new SnuffCandidate(0, true, 0.5f, 20f, 0f),
                new SnuffCandidate(1, true, 0.5f, 4f, 3f)
            };
            Assert.AreEqual(1, SnuffPlanner.PickBest(list, 0f, 0f));
        }

        [Test]
        public void ElEmpateSeResuelveConElMenorId()
        {
            // Mismos datos, distinto orden en la lista: gana siempre el Id más bajo.
            var list = new List<SnuffCandidate>
            {
                new SnuffCandidate(7, true, 0.5f, 3f, 4f),
                new SnuffCandidate(2, true, 0.5f, 4f, 3f)
            };
            Assert.AreEqual(1, SnuffPlanner.PickBest(list, 0f, 0f));
            list.Reverse();
            Assert.AreEqual(0, SnuffPlanner.PickBest(list, 0f, 0f));
        }

        [Test]
        public void ListaVaciaDevuelveMenosUno()
        {
            Assert.AreEqual(-1, SnuffPlanner.PickBest(new List<SnuffCandidate>(), 0f, 0f));
        }

        [Test]
        public void ListaNulaDevuelveMenosUno()
        {
            Assert.AreEqual(-1, SnuffPlanner.PickBest(null, 0f, 0f));
        }

        [Test]
        public void MasApagavelasQueHuecos_SalenVariosALaVez()
        {
            // HuntSchedule da como mucho 5 momentos: con 8 Apagavelas se repiten, sin salir del rango.
            List<float> moments = SnuffPlanner.Schedule(SnuffPlanner.MaxSnuffers, 5);
            HashSet<float> distinct = new HashSet<float>(moments);
            Assert.AreEqual(SnuffPlanner.MaxSnuffers, moments.Count);
            Assert.AreEqual(HuntSchedule.MaxHunts, distinct.Count);
            foreach (float m in moments)
            {
                Assert.GreaterOrEqual(m, SnuffPlanner.Earliest);
                Assert.LessOrEqual(m, SnuffPlanner.Latest);
            }
        }

        [Test]
        public void CantidadNegativaNoProgramaNada()
        {
            CollectionAssert.IsEmpty(SnuffPlanner.Schedule(-3, 1));
        }

        [Test]
        public void EsperanzaFueraDeRangoSeAcota()
        {
            SnuffCandidate overHope = new SnuffCandidate(0, true, 5f, 0f, 0f);
            SnuffCandidate fullHope = new SnuffCandidate(1, true, 1f, 0f, 0f);
            SnuffCandidate underHope = new SnuffCandidate(2, true, -3f, 0f, 0f);
            SnuffCandidate noHope = new SnuffCandidate(3, true, 0f, 0f, 0f);

            Assert.AreEqual(SnuffPlanner.ScoreTarget(fullHope, 0f, 0f), SnuffPlanner.ScoreTarget(overHope, 0f, 0f));
            Assert.AreEqual(SnuffPlanner.ScoreTarget(noHope, 0f, 0f), SnuffPlanner.ScoreTarget(underHope, 0f, 0f));
        }

        [Test]
        public void LaDistanciaPesaHastaUnTope()
        {
            // Más allá de 190 m la distancia ya no resta: dos luces lejanas empatan y decide el Id.
            SnuffCandidate far = new SnuffCandidate(0, true, 0.5f, 200f, 0f);
            SnuffCandidate farther = new SnuffCandidate(1, true, 0.5f, 900f, 0f);
            Assert.AreEqual(SnuffPlanner.ScoreTarget(far, 0f, 0f), SnuffPlanner.ScoreTarget(farther, 0f, 0f));
        }

        [Test]
        public void UnaLuzApagadaNuncaGanaAUnaEncendida()
        {
            // Lo peor para la encendida (zona llena, lejísimos) contra lo mejor para la apagada.
            SnuffCandidate lit = new SnuffCandidate(5, true, 1f, 1000f, 0f);
            SnuffCandidate dark = new SnuffCandidate(0, false, 0f, 0f, 0f);
            var list = new List<SnuffCandidate> { dark, lit };
            Assert.AreEqual(1, SnuffPlanner.PickBest(list, 0f, 0f));
        }
    }
}
