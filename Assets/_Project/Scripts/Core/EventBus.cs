using System;
using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Bus de eventos global y sencillo: los sistemas publican "algo pasó" sin conocer a quién le importa.
    /// Lo usan misiones, diálogos, Vínculos, audio, gestos del personaje y la interfaz. En la Fase 4 el
    /// AIDirector podrá escuchar estos mismos eventos para adaptar la noche.
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

        /// <summary>Ilse recogió un objeto. Args: id del objeto, nombre visible.</summary>
        public static event Action<string, string> ItemPickedUp;

        /// <summary>Movimiento del jugador (para sonido y efectos). Arg: posición.</summary>
        public static event Action<Vector3> PlayerJumped;
        public static event Action<Vector3> PlayerLanded;
        public static event Action<Vector3> PlayerDashed;
        public static event Action<Vector3> PlayerMantled;

        /// <summary>El Farol cambió de tamaño. Arg: índice nuevo.</summary>
        public static event Action<int> LanternResized;

        /// <summary>Una zona cruzó un nivel de Esperanza (25/50/75/100). Args: id de zona, nivel (1-4), posición.</summary>
        public static event Action<string, int, Vector3> HopeLevelReached;

        /// <summary>Empieza o termina una ráfaga de urgencia de la Penumbra. Arg: true al empezar.</summary>
        public static event Action<bool> PenumbraSurge;

        /// <summary>Se descubrió un atajo de exploración. Args: id, nombre visible.</summary>
        public static event Action<string, string> ShortcutDiscovered;

        /// <summary>Un momento narrativo (NightEvent) empieza o termina. Args: id, true al empezar.</summary>
        public static event Action<string, bool> NarrativeMoment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CelebrationRequested = null;
            NpcTalked = null;
            BondChanged = null;
            QuestStateChanged = null;
            LightPulseFired = null;
            ItemPickedUp = null;
            PlayerJumped = null;
            PlayerLanded = null;
            PlayerDashed = null;
            PlayerMantled = null;
            LanternResized = null;
            HopeLevelReached = null;
            PenumbraSurge = null;
            ShortcutDiscovered = null;
            NarrativeMoment = null;
        }

        public static void RaiseCelebration(string reason) => CelebrationRequested?.Invoke(reason);
        public static void RaiseNpcTalked(string npcId) => NpcTalked?.Invoke(npcId);
        public static void RaiseBondChanged(string npcId, int level) => BondChanged?.Invoke(npcId, level);
        public static void RaiseQuestStateChanged(string questId, string state) => QuestStateChanged?.Invoke(questId, state);
        public static void RaiseLightPulse(Vector3 origin, float radius) => LightPulseFired?.Invoke(origin, radius);
        public static void RaiseItemPickedUp(string itemId, string displayName) => ItemPickedUp?.Invoke(itemId, displayName);
        public static void RaisePlayerJumped(Vector3 position) => PlayerJumped?.Invoke(position);
        public static void RaisePlayerLanded(Vector3 position) => PlayerLanded?.Invoke(position);
        public static void RaisePlayerDashed(Vector3 position) => PlayerDashed?.Invoke(position);
        public static void RaisePlayerMantled(Vector3 position) => PlayerMantled?.Invoke(position);
        public static void RaiseLanternResized(int index) => LanternResized?.Invoke(index);
        public static void RaiseHopeLevelReached(string zoneId, int level, Vector3 position) => HopeLevelReached?.Invoke(zoneId, level, position);
        public static void RaisePenumbraSurge(bool started) => PenumbraSurge?.Invoke(started);
        public static void RaiseShortcutDiscovered(string id, string displayName) => ShortcutDiscovered?.Invoke(id, displayName);
        public static void RaiseNarrativeMoment(string id, bool started) => NarrativeMoment?.Invoke(id, started);
    }
}
