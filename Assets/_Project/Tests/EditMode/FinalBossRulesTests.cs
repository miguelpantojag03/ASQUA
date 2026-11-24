using NUnit.Framework;

namespace Ascua.LevelGen.Tests
{
    /// <summary>Pruebas del jefe final "La Marea" (FinalBossRules, lógica pura): fases, daño y cuándo aparece.</summary>
    public class FinalBossRulesTests
    {
        [Test]
        public void FasePorPorcentajeDeVida()
        {
            Assert.AreEqual(1, FinalBossRules.PhaseFor(1f));
            Assert.AreEqual(1, FinalBossRules.PhaseFor(0.8f));
            Assert.AreEqual(1, FinalBossRules.PhaseFor(0.67f));
            Assert.AreEqual(2, FinalBossRules.PhaseFor(0.66f));
            Assert.AreEqual(2, FinalBossRules.PhaseFor(0.5f));
            Assert.AreEqual(2, FinalBossRules.PhaseFor(0.34f));
            Assert.AreEqual(3, FinalBossRules.PhaseFor(0.33f));
            Assert.AreEqual(3, FinalBossRules.PhaseFor(0.1f));
            Assert.AreEqual(3, FinalBossRules.PhaseFor(0f));
        }

        [Test]
        public void LosLimitesExactosEmpiezanLaFaseSiguiente()
        {
            Assert.AreEqual(2, FinalBossRules.PhaseFor(FinalBossRules.Phase2Below), "2/3 exacto ya es fase 2");
            Assert.AreEqual(3, FinalBossRules.PhaseFor(FinalBossRules.Phase3Below), "1/3 exacto ya es fase 3");
        }

        [Test]
        public void FasePorVidaEntera()
        {
            int max = FinalBossRules.MaxHealth;
            Assert.AreEqual(1, FinalBossRules.PhaseFor(max, max));
            Assert.AreEqual(1, FinalBossRules.PhaseFor(max * 2 / 3 + 1, max));
            Assert.AreEqual(2, FinalBossRules.PhaseFor(max * 2 / 3, max));
            Assert.AreEqual(2, FinalBossRules.PhaseFor(max / 3 + 1, max));
            Assert.AreEqual(3, FinalBossRules.PhaseFor(max / 3, max));
            Assert.AreEqual(3, FinalBossRules.PhaseFor(0, max));
            Assert.AreEqual(3, FinalBossRules.PhaseFor(-5, max), "vida negativa tras el golpe final");
            Assert.AreEqual(3, FinalBossRules.PhaseFor(5, 0), "sin vida máxima no se divide entre cero");
        }

        [Test]
        public void LasFasesNuncaRetrocedenAlBajarLaVida()
        {
            int max = FinalBossRules.MaxHealth;
            int previous = 1;
            for (int health = max; health >= 0; health--)
            {
                int phase = FinalBossRules.PhaseFor(health, max);
                Assert.GreaterOrEqual(phase, previous, "vida " + health);
                previous = phase;
            }
            Assert.AreEqual(3, previous);
        }

        [Test]
        public void SoloLaLuzHaceDanoCompleto()
        {
            Assert.AreEqual(1 * FinalBossRules.LitMultiplier, FinalBossRules.DamageFor(1, true));
            Assert.AreEqual(3 * FinalBossRules.LitMultiplier, FinalBossRules.DamageFor(3, true));
            Assert.AreEqual(FinalBossRules.UnlitDamage, FinalBossRules.DamageFor(3, false));
            Assert.AreEqual(FinalBossRules.UnlitDamage, FinalBossRules.DamageFor(1, false));
            Assert.AreEqual(FinalBossRules.LitMultiplier, FinalBossRules.DamageFor(0, true), "un golpe de 0 cuenta como 1");
            Assert.Less(FinalBossRules.DamageFor(1, false), FinalBossRules.DamageFor(1, true));
        }

        [Test]
        public void LaVidaDaParaUnCombateDeUnMinutoLargo()
        {
            // Un combo iluminado (1 + 1 + 2) quita 16 puntos: hacen falta bastantes combos, no dos o tres.
            int combo = FinalBossRules.DamageFor(1, true) * 2 + FinalBossRules.DamageFor(2, true);
            int combos = FinalBossRules.MaxHealth / combo;
            Assert.GreaterOrEqual(combos, 20);
            Assert.LessOrEqual(combos, 40);
        }

        [Test]
        public void SoloSaleEnLaUltimaNocheYUnaVez()
        {
            Assert.IsTrue(FinalBossRules.AppearsTonight(5, 5, false));
            Assert.IsFalse(FinalBossRules.AppearsTonight(4, 5, false));
            Assert.IsFalse(FinalBossRules.AppearsTonight(1, 5, false));
            Assert.IsFalse(FinalBossRules.AppearsTonight(6, 5, false), "Noches sin fin");
            Assert.IsFalse(FinalBossRules.AppearsTonight(9, 5, false), "Noches sin fin");
            Assert.IsFalse(FinalBossRules.AppearsTonight(5, 5, true), "ya derrotada");
            Assert.IsFalse(FinalBossRules.AppearsTonight(0, 0, false), "sin noche final configurada");
        }

        [Test]
        public void EmpiezaAlTerminarLasMisiones()
        {
            Assert.IsTrue(FinalBossRules.QuestsFinished(0, 4));
            Assert.IsFalse(FinalBossRules.QuestsFinished(1, 4));
            Assert.IsFalse(FinalBossRules.QuestsFinished(0, 0), "sin misiones espera al final del tiempo");
        }

        [Test]
        public void SeRetiraTrasVariasCaidas()
        {
            Assert.IsFalse(FinalBossRules.ShouldRetreat(0));
            Assert.IsFalse(FinalBossRules.ShouldRetreat(FinalBossRules.DefeatsBeforeRetreat - 1));
            Assert.IsTrue(FinalBossRules.ShouldRetreat(FinalBossRules.DefeatsBeforeRetreat));
        }
    }
}
