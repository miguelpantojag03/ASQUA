using NUnit.Framework;

namespace Ascua.Endings.Tests
{
    /// <summary>
    /// Pruebas del EndingResolver (lógica pura): un caso por final, la prioridad entre finales y los bordes
    /// de cada proporción. Se ejecutan en Window > General > Test Runner > EditMode.
    /// </summary>
    public class EndingResolverTests
    {
        /// <summary>
        /// Partida "neutra": zonas indicadas, Vínculos medios, sin Tratos y Diario a medias.
        /// Ilse SÍ nombró a Corvin (así los casos de Constelación solo dependen de zonas y Vínculos);
        /// los casos sin nombrarlo lo dicen explícitamente.
        /// </summary>
        private static EndingInput Input(int saved, int total)
        {
            return new EndingInput
            {
                SavedZones = saved,
                TotalZones = total,
                BondMina = 2,
                BondTeo = 2,
                BondPerpetua = 2,
                TratosAccepted = 0,
                DiaryPages = 3,
                NamedCorvin = true
            };
        }

        private static EndingInput WithBonds(EndingInput input, int mina, int teo, int perpetua)
        {
            input.BondMina = mina;
            input.BondTeo = teo;
            input.BondPerpetua = perpetua;
            return input;
        }

        // ------------------------------------------------------------ Un caso por final

        [Test]
        public void Ceniza_SinZonasSalvadas()
        {
            Assert.AreEqual(EndingId.Ceniza, EndingResolver.Resolve(Input(0, 2)));
        }

        [Test]
        public void Vigilia_MitadDeLasZonas()
        {
            Assert.AreEqual(EndingId.Vigilia, EndingResolver.Resolve(Input(1, 2)));
        }

        [Test]
        public void Alba_TodasLasZonasConVinculosBajos()
        {
            Assert.AreEqual(EndingId.Alba, EndingResolver.Resolve(Input(2, 2)));
        }

        [Test]
        public void Constelacion_TodasLasZonasYVinculosAltos()
        {
            EndingInput input = WithBonds(Input(2, 2), 4, 4, 3);
            Assert.AreEqual(EndingId.Constelacion, EndingResolver.Resolve(input));
        }

        [Test]
        public void ElTrato_UnTratoYAlgoDeLuz()
        {
            EndingInput input = Input(1, 2);
            input.TratosAccepted = 1;
            Assert.AreEqual(EndingId.ElTrato, EndingResolver.Resolve(input));
        }

        [Test]
        public void FarolApagado_VinculosAlMaximoDiarioCompletoYSinTratos()
        {
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages;
            Assert.AreEqual(EndingId.FarolApagado, EndingResolver.Resolve(input));
        }

        // ------------------------------------------------------------ Prioridad

        [Test]
        public void Prioridad_FarolApagadoGanaAConstelacion()
        {
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages;
            Assert.IsTrue(EndingResolver.IsConstelacion(input));
            Assert.AreEqual(EndingId.FarolApagado, EndingResolver.Resolve(input));
        }

        [Test]
        public void Prioridad_UnTratoImpideFarolApagado()
        {
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages;
            input.TratosAccepted = 1;
            Assert.AreEqual(EndingId.ElTrato, EndingResolver.Resolve(input));
        }

        [Test]
        public void Prioridad_ElTratoGanaAConstelacion()
        {
            EndingInput input = WithBonds(Input(2, 2), 4, 4, 4);
            input.TratosAccepted = 1;
            Assert.AreEqual(EndingId.ElTrato, EndingResolver.Resolve(input));
        }

        [Test]
        public void Prioridad_ElTratoConVesperApagadaEsCeniza()
        {
            EndingInput input = Input(0, 2);
            input.TratosAccepted = 1;
            Assert.AreEqual(EndingId.Ceniza, EndingResolver.Resolve(input));
        }

        [Test]
        public void Prioridad_FarolApagadoNoMiraLasZonas()
        {
            EndingInput input = WithBonds(Input(0, 2), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages;
            Assert.AreEqual(EndingId.FarolApagado, EndingResolver.Resolve(input));
        }

        // ------------------------------------------------------------ Bordes

        [Test]
        public void Borde_VigiliaEmpiezaEnVeintePorCiento()
        {
            Assert.AreEqual(EndingId.Ceniza, EndingResolver.Resolve(Input(19, 100)));
            Assert.AreEqual(EndingId.Vigilia, EndingResolver.Resolve(Input(20, 100)));
        }

        [Test]
        public void Borde_AlbaEmpiezaEnCincuentaYCincoPorCiento()
        {
            Assert.AreEqual(EndingId.Vigilia, EndingResolver.Resolve(Input(54, 100)));
            Assert.AreEqual(EndingId.Alba, EndingResolver.Resolve(Input(55, 100)));
        }

        [Test]
        public void Borde_ConstelacionEmpiezaEnOchentaYCincoPorCiento()
        {
            Assert.AreEqual(EndingId.Alba, EndingResolver.Resolve(WithBonds(Input(84, 100), 5, 5, 5)));
            Assert.AreEqual(EndingId.Constelacion, EndingResolver.Resolve(WithBonds(Input(85, 100), 4, 4, 4)));
        }

        [Test]
        public void Borde_VinculoPromedioJustoEnTresYMedio()
        {
            // Con tres Vínculos enteros 3.5 exacto no existe: 4+3+4 = 3.67 (sí llega), 4+3+3 = 3.33 (no llega).
            Assert.AreEqual(EndingId.Constelacion, EndingResolver.Resolve(WithBonds(Input(2, 2), 4, 3, 4)));
            Assert.AreEqual(EndingId.Alba, EndingResolver.Resolve(WithBonds(Input(2, 2), 4, 3, 3)));
        }

        [Test]
        public void Borde_TratoNecesitaVeintePorCientoDeZonas()
        {
            EndingInput low = Input(19, 100);
            low.TratosAccepted = 1;
            EndingInput ok = Input(20, 100);
            ok.TratosAccepted = 1;
            Assert.AreEqual(EndingId.Ceniza, EndingResolver.Resolve(low));
            Assert.AreEqual(EndingId.ElTrato, EndingResolver.Resolve(ok));
        }

        [Test]
        public void Borde_FarolApagadoNecesitaElDiarioCompleto()
        {
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages - 1;
            Assert.AreEqual(EndingId.Constelacion, EndingResolver.Resolve(input));
        }

        [Test]
        public void Borde_FarolApagadoNecesitaLosTresVinculosAlMaximo()
        {
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 4);
            input.DiaryPages = EndingResolver.TotalDiaryPages;
            Assert.AreEqual(EndingId.Constelacion, EndingResolver.Resolve(input));
        }

        [Test]
        public void Borde_SinZonasEsCeniza()
        {
            Assert.AreEqual(0f, EndingResolver.SavedRatio(Input(0, 0)));
            Assert.AreEqual(EndingId.Ceniza, EndingResolver.Resolve(Input(0, 0)));
        }

        [Test]
        public void Corvin_ConstelacionExigeNombrarlo()
        {
            Assert.IsTrue(EndingResolver.RequireCorvinName);
            EndingInput input = WithBonds(Input(2, 2), 4, 4, 4);
            input.NamedCorvin = false;
            Assert.IsFalse(EndingResolver.IsConstelacion(input));
            input.NamedCorvin = true;
            Assert.IsTrue(EndingResolver.IsConstelacion(input));
        }

        [Test]
        public void Corvin_TodoPerfectoSinNombrarloEsAlba()
        {
            // Todas las zonas, Vínculos al máximo, sin Tratos y Diario casi completo, pero sin decir su nombre.
            EndingInput input = WithBonds(Input(7, 7), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages - 1;
            input.NamedCorvin = false;
            Assert.AreEqual(EndingId.Alba, EndingResolver.Resolve(input));
        }

        [Test]
        public void Corvin_ElFinalSecretoNoDependeDeNombrarlo()
        {
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 5);
            input.DiaryPages = EndingResolver.TotalDiaryPages;
            input.NamedCorvin = false;
            Assert.AreEqual(EndingId.FarolApagado, EndingResolver.Resolve(input));
        }

        [Test]
        public void Diario_SieteSonElDiarioCompleto()
        {
            Assert.AreEqual(7, EndingResolver.TotalDiaryPages);
            EndingInput input = WithBonds(Input(2, 2), 5, 5, 5);
            input.DiaryPages = 6;
            Assert.AreEqual(EndingId.Constelacion, EndingResolver.Resolve(input), "Con 6 de 7 páginas aún no hay final secreto");
        }

        // ------------------------------------------------------------ Textos

        [Test]
        public void Catalogo_CadaFinalTieneNombreTituloYEpilogo()
        {
            Assert.AreEqual(6, EndingCatalog.All.Count);
            foreach (EndingInfo info in EndingCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(info.Name), info.Id + " sin nombre");
                Assert.IsFalse(string.IsNullOrEmpty(info.ShortTitle), info.Id + " sin título corto");
                Assert.That(info.Epilogue.Length, Is.InRange(2, 3), info.Id + " debe tener 2-3 líneas de epílogo");
            }
        }

        [Test]
        public void Catalogo_LineasDePersonajesSoloConVinculoTresOMas()
        {
            Assert.AreEqual(0, EndingCatalog.CompanionLines(2, 2, 2).Count);
            Assert.AreEqual(2, EndingCatalog.CompanionLines(3, 2, 5).Count);
            Assert.AreEqual(3, EndingCatalog.CompanionLines(3, 3, 3).Count);
        }
    }
}
