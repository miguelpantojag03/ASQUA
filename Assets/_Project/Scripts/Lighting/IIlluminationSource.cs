using UnityEngine;

namespace Ascua.Lighting
{
    /// <summary>
    /// Cualquier cosa capaz de "iluminar" el mundo de forma jugable (no solo visual).
    /// Hoy lo implementa el Farol; en fases futuras también el Pulso de luz,
    /// las luciérnagas o las farolas reencendidas, sin tocar WindowLight ni ZoneHope.
    /// </summary>
    public interface IIlluminationSource
    {
        /// <summary>Origen de la luz en coordenadas de mundo.</summary>
        Vector3 LightPosition { get; }

        /// <summary>Radio jugable actual en metros.</summary>
        float IlluminationRadius { get; }

        /// <summary>True si la fuente está iluminando de forma "activa" (gastando energía).</summary>
        bool IsIlluminating { get; }
    }
}
