namespace Ascua.Player
{
    /// <summary>
    /// Los 7 Recuerdos (mejoras que se eligen al final de cada noche, 1 de 3).
    /// El guardado los escribe como número: los valores nuevos van SIEMPRE al final
    /// para que las partidas guardadas sigan leyéndose igual.
    /// </summary>
    public enum UpgradeType
    {
        /// <summary>Lupa de Mina: +15% de radio del Farol en todos los tamaños.</summary>
        RadioAmplio,
        /// <summary>Bata de Rueda: −20% de gasto de energía del Farol.</summary>
        Eficiencia,
        /// <summary>Caja de música de Perpetua: +35% de regeneración de energía.</summary>
        Regeneracion,
        /// <summary>Carrito de Teo: las velas aparecen un 30% más seguido.</summary>
        CarritoTeo,
        /// <summary>Reloj de Nadir: la Penumbra avanza un 15% más lenta.</summary>
        RelojNadir,
        /// <summary>Libro de Amelia: +5 de Esperanza a todas las zonas al empezar cada noche.</summary>
        LibroAmelia,
        /// <summary>Última vela: una vez por partida, la primera zona que fuera a apagarse se queda en 10.</summary>
        UltimaVela
    }
}
