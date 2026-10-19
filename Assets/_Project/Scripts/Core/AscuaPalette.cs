using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Paleta oficial de Ascua. Centralizar los colores aquí evita "colores mágicos"
    /// repartidos por el proyecto y mantiene la dirección de arte coherente.
    /// </summary>
    public static class AscuaPalette
    {
        // Fríos dominantes de Vesper.
        public static readonly Color Indigo = Hex("#141B3A");
        public static readonly Color Azul = Hex("#1E2A5A");
        public static readonly Color Niebla = Hex("#1A2350");

        // El único contraste cálido: el Farol.
        public static readonly Color Ambar = Hex("#FFC46B");

        // Rojo puro reservado EXCLUSIVAMENTE para Mina en todo el juego.
        public static readonly Color RojoMina = Hex("#E63946");

        // Tonos de apoyo para UI.
        public static readonly Color TextoFrio = Hex("#B8C4EC");
        public static readonly Color EnergiaBaja = Hex("#DB7340");

        /// <summary>Convierte un string hexadecimal (#RRGGBB o #RRGGBBAA) en Color.</summary>
        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
        }

        /// <summary>Devuelve un color HDR (para emisión) multiplicando por una intensidad.</summary>
        public static Color Hdr(Color baseColor, float intensity)
        {
            return new Color(baseColor.r * intensity, baseColor.g * intensity, baseColor.b * intensity, 1f);
        }
    }
}
