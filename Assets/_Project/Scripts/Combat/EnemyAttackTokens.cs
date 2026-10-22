using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Turnos de ataque: como mucho N sombras pueden estar atacando a Ilse a la vez (por defecto 2).
    /// Las demás siguen rodeando y esperan su turno. Así una oleada grande se siente amenazante
    /// pero se puede leer y esquivar: nunca llegan 5 picados en el mismo segundo.
    ///  - Una criatura pide turno (TryAcquire) justo antes de su aviso de ataque.
    ///  - Lo devuelve (Release) al terminar el ataque, al morir o al desaparecer.
    ///  - Seguridad: un turno caduca solo a los 'TokenLifetime' segundos (por si una criatura se queda colgada).
    /// </summary>
    public static class EnemyAttackTokens
    {
        /// <summary>Segundos máximos que una criatura puede retener un turno.</summary>
        public const float TokenLifetime = 2.5f;

        private static readonly Dictionary<ShadowCreature, float> holders = new Dictionary<ShadowCreature, float>();
        private static readonly List<ShadowCreature> expired = new List<ShadowCreature>();
        private static int maxAttackers = 2;

        /// <summary>Máximo de atacantes simultáneos (lo fija CombatWaveDirector).</summary>
        public static int MaxAttackers
        {
            get => maxAttackers;
            set => maxAttackers = Mathf.Max(1, value);
        }

        /// <summary>Atacantes ahora mismo (para la depuración y la prueba automática).</summary>
        public static int ActiveCount
        {
            get
            {
                Purge();
                return holders.Count;
            }
        }

        /// <summary>Mayor número de atacantes simultáneos visto desde el último ResetPeak (prueba).</summary>
        public static int PeakCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            holders.Clear();
            maxAttackers = 2;
            PeakCount = 0;
        }

        public static void ResetPeak()
        {
            PeakCount = ActiveCount;
        }

        /// <summary>Pide turno. True = puede atacar ya (o ya lo tenía).</summary>
        public static bool TryAcquire(ShadowCreature creature)
        {
            if (creature == null) return false;
            Purge();
            if (holders.ContainsKey(creature))
            {
                holders[creature] = Time.time + TokenLifetime;
                return true;
            }
            if (holders.Count >= maxAttackers) return false;
            holders.Add(creature, Time.time + TokenLifetime);
            if (holders.Count > PeakCount) PeakCount = holders.Count;
            return true;
        }

        public static bool Holds(ShadowCreature creature) => creature != null && holders.ContainsKey(creature);

        /// <summary>Devuelve el turno (no pasa nada si no lo tenía).</summary>
        public static void Release(ShadowCreature creature)
        {
            if (creature != null) holders.Remove(creature);
        }

        /// <summary>Quita turnos caducados o de criaturas que ya no están activas.</summary>
        private static void Purge()
        {
            if (holders.Count == 0) return;
            expired.Clear();
            float now = Time.time;
            foreach (KeyValuePair<ShadowCreature, float> pair in holders)
            {
                if (pair.Key == null || !pair.Key.IsAlive || now > pair.Value) expired.Add(pair.Key);
            }
            for (int i = 0; i < expired.Count; i++) holders.Remove(expired[i]);
        }
    }
}
