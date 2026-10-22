using System;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Una oleada corta de sombras (se define en el NightQuestSet de cada noche, junto a sus misiones).
    /// 3-4 criaturas juntas atacan una zona: mientras sigan vivas, la Esperanza de esa zona cae.
    /// Es parte del "ADN" de la noche: momento, zona y mezcla de enemigos.
    /// </summary>
    [Serializable]
    public class CombatWave
    {
        [Tooltip("Nombre para depuración (F1).")]
        public string label = "Oleada";
        [Tooltip("Momento de la noche (0-1) en que empieza.")]
        [Range(0f, 1f)] public float atProgress = 0.3f;
        [Tooltip("Zona atacada (ZoneHope.ZoneId).")]
        public string zoneId = "Plaza";

        [Header("Criaturas")]
        [Min(0)] public int moths = 2;
        [Min(0)] public int brutes;
        [Tooltip("Sombras veladas (Sombra Mayor con LitVulnerable).")]
        [Min(0)] public int veiledBrutes;
        [Min(0)] public int whispers;
        [Min(0)] public int echoes = 1;

        [Header("Presión")]
        [Tooltip("Esperanza que pierde la zona por segundo y por criatura viva.")]
        public float hopeDrainPerCreature = 1.2f;

        public int Total => moths + brutes + veiledBrutes + whispers + echoes;
    }
}
