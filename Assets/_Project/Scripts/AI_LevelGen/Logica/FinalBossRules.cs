namespace Ascua.LevelGen
{
    /// <summary>
    /// Reglas del jefe final "La Marea" (lógica pura, sin Unity, testeable).
    ///  - Aparece solo en la última noche de la campaña (Noche 5), nunca en noches posteriores
    ///    ("Noches sin fin") ni si ya se derrotó en esta partida.
    ///  - Empieza al terminar las misiones de la noche o, si no se terminan, al acabarse el tiempo.
    ///  - 3 fases por vida restante: 100-66 % golpe en área · 66-33 % invoca Polillas · 33-0 % apaga farolas y acelera.
    ///  - Daño completo solo si está iluminado por el Farol; si no, daño mínimo.
    /// </summary>
    public static class FinalBossRules
    {
        /// <summary>Por debajo de esta fracción de vida empieza la fase 2.</summary>
        public const float Phase2Below = 2f / 3f;
        /// <summary>Por debajo de esta fracción de vida empieza la fase 3.</summary>
        public const float Phase3Below = 1f / 3f;

        /// <summary>
        /// Vida en "puntos". Un combo iluminado (1+1+2 de daño ×4) quita 16: con ~50 % del tiempo atacando
        /// bajo el Farol, el combate dura entre 60 y 90 s.
        /// </summary>
        public const int MaxHealth = 420;
        /// <summary>Multiplicador del daño de un golpe si La Marea está iluminada.</summary>
        public const int LitMultiplier = 4;
        /// <summary>Daño de cualquier golpe si NO está iluminada.</summary>
        public const int UnlitDamage = 1;

        /// <summary>Esperanza extra para cada zona viva si se vence a La Marea (no cambia el final).</summary>
        public const float VictoryHopeBonus = 10f;

        /// <summary>Derrotas de Ilse tras las que La Marea se retira (la noche sigue al final sin bonificación).</summary>
        public const int DefeatsBeforeRetreat = 3;

        /// <summary>Fase (1, 2 o 3) según la fracción de vida restante (0-1).</summary>
        public static int PhaseFor(float health01)
        {
            if (health01 > Phase2Below) return 1;
            if (health01 > Phase3Below) return 2;
            return 3;
        }

        /// <summary>Fase (1, 2 o 3) según la vida actual y la máxima.</summary>
        public static int PhaseFor(int health, int maxHealth)
        {
            if (maxHealth <= 0) return 3;
            return PhaseFor((float)health / maxHealth);
        }

        /// <summary>Daño real de un golpe: completo (×LitMultiplier) bajo el Farol, mínimo si no.</summary>
        public static int DamageFor(int hitDamage, bool lit)
        {
            if (!lit) return UnlitDamage;
            int baseDamage = hitDamage < 1 ? 1 : hitDamage;
            return baseDamage * LitMultiplier;
        }

        /// <summary>True si La Marea debe aparecer esta noche.</summary>
        /// <param name="night">Noche que se está jugando.</param>
        /// <param name="finalNight">Última noche de la campaña (NightManager.FinalNight).</param>
        /// <param name="alreadyDefeated">True si ya se derrotó en esta partida.</param>
        public static bool AppearsTonight(int night, int finalNight, bool alreadyDefeated)
        {
            if (alreadyDefeated || finalNight <= 0) return false;
            // Solo la última noche de la campaña: las posteriores son "Noches sin fin".
            return night == finalNight;
        }

        /// <summary>True si las misiones de la noche ya terminaron (hay misiones y ninguna pendiente).</summary>
        /// <param name="pending">Misiones aún por hacer (disponibles, activas o por entregar; sin contar Tratos opcionales).</param>
        /// <param name="total">Misiones de la noche.</param>
        public static bool QuestsFinished(int pending, int total)
        {
            return total > 0 && pending <= 0;
        }

        /// <summary>True si, tras esta cantidad de derrotas, La Marea se retira en vez de reintentar.</summary>
        public static bool ShouldRetreat(int defeats)
        {
            return defeats >= DefeatsBeforeRetreat;
        }
    }
}
