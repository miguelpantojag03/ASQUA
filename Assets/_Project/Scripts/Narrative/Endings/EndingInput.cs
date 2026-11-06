namespace Ascua.Endings
{
    /// <summary>
    /// Datos de la partida que deciden el final. Lógica pura: no sabe nada de Unity,
    /// así se puede probar en el Test Runner sin escenas.
    /// </summary>
    public struct EndingInput
    {
        /// <summary>Zonas que siguen encendidas al terminar la última noche.</summary>
        public int SavedZones;
        /// <summary>Zonas que había en total (hoy: Plaza y Mercado).</summary>
        public int TotalZones;

        /// <summary>Vínculos (0-5) con cada personaje.</summary>
        public int BondMina;
        public int BondTeo;
        public int BondPerpetua;

        /// <summary>Tratos del Heraldo aceptados en toda la partida.</summary>
        public int TratosAccepted;

        /// <summary>Páginas del Diario de Ilse encontradas (de 6).</summary>
        public int DiaryPages;

        /// <summary>True si Ilse pronunció el nombre de Corvin (marca que aún no existe en el juego).</summary>
        public bool NamedCorvin;
    }
}
