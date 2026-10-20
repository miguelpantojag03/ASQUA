using System;
using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Bus de eventos global y sencillo: los sistemas publican "algo pasó" sin conocer a quién le importa.
    /// Lo usan misiones, diálogos, Vínculos y los gestos del personaje. En la Fase 4 el AIDirector
    /// podrá escuchar estos mismos eventos para adaptar la noche.
    /// </summary>
    public static class EventBus
    {
        /// <summary>Algo merece una celebración corta de Ilse (misión completada, Vínculo que sube...). Arg: motivo.</summary>
        public static event Action<string> CelebrationRequested;

        /// <summary>El jugador habló con un NPC. Arg: id del NPC.</summary>
        public static event Action<string> NpcTalked;

        /// <summary>El Vínculo con un personaje cambió. Args: id del NPC, nivel nuevo (0-5).</summary>
        public static event Action<string, int> BondChanged;

        /// <summary>Una misión cambió de estado. Args: id de la misión, estado nuevo (texto).</summary>
        public static event Action<string, string> QuestStateChanged;

        /// <summary>Se lanzó un Pulso de luz. Args: origen, radio.</summary>
        public static event Action<Vector3, float> LightPulseFired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CelebrationRequested = null;
            NpcTalked = null;
            BondChanged = null;
            QuestStateChanged = null;
            LightPulseFired = null;
        }

        public static void RaiseCelebration(string reason) => CelebrationRequested?.Invoke(reason);
        public static void RaiseNpcTalked(string npcId) => NpcTalked?.Invoke(npcId);
        public static void RaiseBondChanged(string npcId, int level) => BondChanged?.Invoke(npcId, level);
        public static void RaiseQuestStateChanged(string questId, string state) => QuestStateChanged?.Invoke(questId, state);
        public static void RaiseLightPulse(Vector3 origin, float radius) => LightPulseFired?.Invoke(origin, radius);
    }
}
