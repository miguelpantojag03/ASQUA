using System;
using System.Collections.Generic;

namespace Ascua.Player
{
    /// <summary>
    /// Reglas puras de los Recuerdos (sin Unity): nombres y oferta de cada noche.
    /// </summary>
    public static class RecuerdoRules
    {
        /// <summary>Cuántos Recuerdos se ofrecen al terminar una noche.</summary>
        public const int OfferSize = 3;

        /// <summary>Esperanza en la que se queda la zona que salva la Última vela.</summary>
        public const float UltimaVelaHope = 10f;

        /// <summary>Los 7 Recuerdos, en el orden del enum.</summary>
        public static readonly UpgradeType[] All =
        {
            UpgradeType.RadioAmplio,
            UpgradeType.Eficiencia,
            UpgradeType.Regeneracion,
            UpgradeType.CarritoTeo,
            UpgradeType.RelojNadir,
            UpgradeType.LibroAmelia,
            UpgradeType.UltimaVela
        };

        /// <summary>
        /// Hasta 3 Recuerdos al azar que el jugador aún no eligió. La semilla es la noche: al reintentar
        /// la misma noche sale la misma oferta. Si quedan menos de 3 se ofrecen los que haya; si no queda
        /// ninguno, la lista viene vacía (y se salta la elección).
        /// </summary>
        public static List<UpgradeType> PickOffer(IReadOnlyList<UpgradeType> chosen, int night)
        {
            List<UpgradeType> remaining = new List<UpgradeType>();
            foreach (UpgradeType upgrade in All)
            {
                if (!Contains(chosen, upgrade)) remaining.Add(upgrade);
            }

            // Barajado de Fisher-Yates con System.Random sembrado por noche.
            Random random = new Random(unchecked(night * 7919 + 17));
            for (int i = remaining.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                UpgradeType swap = remaining[i];
                remaining[i] = remaining[j];
                remaining[j] = swap;
            }

            if (remaining.Count > OfferSize) remaining.RemoveRange(OfferSize, remaining.Count - OfferSize);
            return remaining;
        }

        /// <summary>Nombre del Recuerdo y qué hace (para la pantalla de elección).</summary>
        public static (string title, string description) Describe(UpgradeType upgrade)
        {
            switch (upgrade)
            {
                case UpgradeType.RadioAmplio: return ("Lupa de Mina", "+15% de alcance del Farol en todos sus tamaños.");
                case UpgradeType.Eficiencia: return ("Bata de Rueda", "El Farol gasta un 20% menos de energía.");
                case UpgradeType.Regeneracion: return ("Caja de música de Perpetua", "La energía se recupera un 35% más rápido.");
                case UpgradeType.CarritoTeo: return ("Carrito de Teo", "Las velas de Teo aparecen un 30% más seguido.");
                case UpgradeType.RelojNadir: return ("Reloj de Nadir", "La Penumbra avanza un 15% más lenta.");
                case UpgradeType.LibroAmelia: return ("Libro de Amelia", "Cada noche empieza con +5 de Esperanza en todas las zonas.");
                case UpgradeType.UltimaVela: return ("Última vela", "Una vez por partida, la primera zona que fuera a apagarse se queda en 10 de Esperanza.");
                default: return (upgrade.ToString(), "");
            }
        }

        private static bool Contains(IReadOnlyList<UpgradeType> list, UpgradeType upgrade)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == upgrade) return true;
            }
            return false;
        }
    }
}
