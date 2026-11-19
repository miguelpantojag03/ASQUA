namespace Ascua.LevelGen
{
    /// <summary>
    /// ¿Debe empezar ya el evento memorable de la noche? (lógica pura, sin Unity, testeable).
    /// Cada evento pertenece a una sola noche, ocurre una vez y espera a que no haya nada en curso.
    /// Si al llegar a Cutoff (90 % de la noche) no se dio, ya no ocurre: la noche sigue normal.
    /// </summary>
    public static class NightEventRules
    {
        /// <summary>A partir de este momento de la noche (0-1) el evento ya no empieza.</summary>
        public const float Cutoff = 0.90f;

        /// <param name="currentNight">Noche que se está jugando.</param>
        /// <param name="eventNight">Noche a la que pertenece el evento.</param>
        /// <param name="progress">Progreso de la noche (0-1).</param>
        /// <param name="threshold">Momento (0-1) a partir del cual puede empezar.</param>
        /// <param name="alreadySeen">True si ya se vio en esta partida.</param>
        /// <param name="busy">True si hay diálogo, oleada, cacería, pausa u otro momento en curso.</param>
        public static bool ShouldTrigger(int currentNight, int eventNight, float progress, float threshold, bool alreadySeen, bool busy)
        {
            if (currentNight != eventNight || alreadySeen || busy) return false;
            return progress >= threshold && progress < Cutoff;
        }

        /// <summary>True si ya pasó el momento límite y el evento no ocurrirá esta noche.</summary>
        public static bool IsTooLate(float progress)
        {
            return progress >= Cutoff;
        }
    }
}
