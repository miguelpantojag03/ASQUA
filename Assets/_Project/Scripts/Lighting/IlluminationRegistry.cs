using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Lighting
{
    /// <summary>
    /// Registro global de fuentes de iluminación jugable. Permite que objetos del mundo
    /// (ventanas, y más adelante zonas, Penumbra, NPCs) pregunten si un punto está iluminado
    /// sin conocer qué tipo de fuente lo ilumina.
    /// </summary>
    public static class IlluminationRegistry
    {
        private static readonly List<IIlluminationSource> sources = new List<IIlluminationSource>();

        public static IReadOnlyList<IIlluminationSource> Sources => sources;

        // Limpia el registro al entrar en Play aunque el "Domain Reload" esté desactivado.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            sources.Clear();
        }

        public static void Register(IIlluminationSource source)
        {
            if (source != null && !sources.Contains(source)) sources.Add(source);
        }

        public static void Unregister(IIlluminationSource source)
        {
            sources.Remove(source);
        }

        /// <summary>
        /// Devuelve true si alguna fuente activa alcanza el punto.
        /// strength va de 0 (borde del radio) a 1 (junto a la fuente).
        /// </summary>
        public static bool IsPointIlluminated(Vector3 point, out float strength, bool requireLineOfSight = true, int obstacleMask = Physics.DefaultRaycastLayers)
        {
            strength = 0f;
            bool any = false;

            for (int i = 0; i < sources.Count; i++)
            {
                IIlluminationSource source = sources[i];
                if (source == null || !source.IsIlluminating) continue;

                float radius = source.IlluminationRadius;
                if (radius <= 0f) continue;

                Vector3 from = source.LightPosition;
                float distance = Vector3.Distance(from, point);
                if (distance > radius) continue;

                if (requireLineOfSight && Physics.Linecast(from, point, obstacleMask, QueryTriggerInteraction.Ignore)) continue;

                float s = 1f - distance / radius;
                if (s > strength) strength = s;
                any = true;
            }

            return any;
        }
    }
}
