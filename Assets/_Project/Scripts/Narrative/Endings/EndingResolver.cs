using System;

namespace Ascua.Endings
{
    /// <summary>
    /// Decide el final de la partida. Usa proporciones (zonas salvadas / zonas totales) y no cantidades fijas,
    /// así sigue funcionando si se añaden zonas nuevas.
    /// Prioridad: Farol Apagado > El Trato > Constelación > Alba > Vigilia > Ceniza.
    /// </summary>
    public static class EndingResolver
    {
        /// <summary>Si es true, Constelación exige además que Ilse haya nombrado a Corvin (esa marca aún no existe).</summary>
        public const bool RequireCorvinName = false;

        /// <summary>Tratos aceptados para El Trato (hoy solo existe un Trato en el juego).</summary>
        public const int TratosForTratoEnding = 1;

        /// <summary>Páginas del Diario de Ilse en total.</summary>
        public const int TotalDiaryPages = 6;

        /// <summary>Vínculo máximo con un personaje.</summary>
        public const int MaxBond = 5;

        /// <summary>Por debajo de esta proporción de zonas salvadas: Ceniza.</summary>
        public const float VigiliaFrom = 0.2f;
        /// <summary>Desde esta proporción: Alba.</summary>
        public const float AlbaFrom = 0.55f;
        /// <summary>Desde esta proporción (con Vínculos altos): Constelación.</summary>
        public const float ConstelacionFrom = 0.85f;
        /// <summary>Vínculo promedio que pide Constelación.</summary>
        public const float ConstelacionBond = 3.5f;

        public static EndingId Resolve(EndingInput input)
        {
            if (IsFarolApagado(input)) return EndingId.FarolApagado;
            if (IsTrato(input)) return EndingId.ElTrato;
            if (IsConstelacion(input)) return EndingId.Constelacion;

            float ratio = SavedRatio(input);
            if (ratio >= AlbaFrom) return EndingId.Alba;
            if (ratio >= VigiliaFrom) return EndingId.Vigilia;
            return EndingId.Ceniza;
        }

        /// <summary>Zonas salvadas / zonas totales, entre 0 y 1 (0 si no hay zonas).</summary>
        public static float SavedRatio(EndingInput input)
        {
            if (input.TotalZones <= 0) return 0f;
            float ratio = (float)input.SavedZones / input.TotalZones;
            return Math.Max(0f, Math.Min(1f, ratio));
        }

        /// <summary>Promedio de los Vínculos de Mina, Teo y Perpetua.</summary>
        public static float AverageBond(EndingInput input)
        {
            return (input.BondMina + input.BondTeo + input.BondPerpetua) / 3f;
        }

        /// <summary>Secreto: los tres Vínculos al máximo, el Diario completo y ningún Trato.</summary>
        public static bool IsFarolApagado(EndingInput input)
        {
            bool allBondsMax = input.BondMina >= MaxBond && input.BondTeo >= MaxBond && input.BondPerpetua >= MaxBond;
            return allBondsMax && input.DiaryPages >= TotalDiaryPages && input.TratosAccepted == 0;
        }

        /// <summary>Aceptó un Trato y Vesper no se apagó del todo.</summary>
        public static bool IsTrato(EndingInput input)
        {
            return input.TratosAccepted >= TratosForTratoEnding && SavedRatio(input) >= VigiliaFrom;
        }

        /// <summary>Casi todo Vesper salvado y Vínculos altos (y Corvin nombrado, si se exige).</summary>
        public static bool IsConstelacion(EndingInput input)
        {
            bool corvinOk = !RequireCorvinName || input.NamedCorvin;
            return SavedRatio(input) >= ConstelacionFrom && AverageBond(input) >= ConstelacionBond && corvinOk;
        }
    }
}
