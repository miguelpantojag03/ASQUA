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

        // ------------------------------------------------------------------ Combate de luz

        /// <summary>Ilse lanza un barrido de luz. Arg: paso del combo (1-3; 4 = ataque cargado).</summary>
        public static event Action<int> PlayerAttacked;

        /// <summary>Un golpe de luz alcanza a una criatura SIN disolverla (si la disuelve: ShadowDefeated). Args: posición, golpe fuerte.</summary>
        public static event Action<Vector3, bool> ShadowHit;

        /// <summary>Una criatura de sombra se disuelve en luz. Args: tipo, posición.</summary>
        public static event Action<string, Vector3> ShadowDefeated;

        /// <summary>Ilse recibe daño (se traduce en energía perdida). Arg: cantidad.</summary>
        public static event Action<float> PlayerHurt;

        /// <summary>Esquiva perfecta: el golpe llegó durante los i-frames. Arg: posición.</summary>
        public static event Action<Vector3> PerfectDodge;

        /// <summary>Contraataque (Reloj de Nadir). Arg: posición.</summary>
        public static event Action<Vector3> Parried;

        /// <summary>Empieza (true) o termina (false) una oleada. Args: zona, empieza.</summary>
        public static event Action<string, bool> CombatWave;

        /// <summary>Oleada superada. Args: zona, valoración ("sin daño", "con dificultad"...).</summary>
        public static event Action<string, string> WaveCleared;

        /// <summary>Golpe al Heraldo en su enfrentamiento. Args: golpes dados, enfrentamiento completado.</summary>
        public static event Action<int, bool> HeraldoConfronted;

        /// <summary>Un Recuerdo desbloquea un movimiento. Arg: id del Recuerdo.</summary>
        public static event Action<string> RecuerdoUnlocked;

        /// <summary>Habilidad especial usada (vela de Teo, Brasa guardiana, Vara de luz...). Arg: id.</summary>
        public static event Action<string> AbilityUsed;

        /// <summary>Una sombra entra por primera vez en el radio de detección. Arg: posición.</summary>
        public static event Action<Vector3> ThreatDetected;

        /// <summary>La energía del Farol entra (true) o sale (false) de la zona crítica (&lt; 20 %).</summary>
        public static event Action<bool> EnergyCritical;

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
            PlayerAttacked = null;
            ShadowHit = null;
            ShadowDefeated = null;
            PlayerHurt = null;
            PerfectDodge = null;
            Parried = null;
            CombatWave = null;
            WaveCleared = null;
            HeraldoConfronted = null;
            RecuerdoUnlocked = null;
            AbilityUsed = null;
            ThreatDetected = null;
            EnergyCritical = null;
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
        public static void RaisePlayerAttacked(int step) => PlayerAttacked?.Invoke(step);
        public static void RaiseShadowHit(Vector3 position, bool strong) => ShadowHit?.Invoke(position, strong);
        public static void RaiseShadowDefeated(string type, Vector3 position) => ShadowDefeated?.Invoke(type, position);
        public static void RaisePlayerHurt(float amount) => PlayerHurt?.Invoke(amount);
        public static void RaisePerfectDodge(Vector3 position) => PerfectDodge?.Invoke(position);
        public static void RaiseParried(Vector3 position) => Parried?.Invoke(position);
        public static void RaiseCombatWave(string zoneId, bool started) => CombatWave?.Invoke(zoneId, started);
        public static void RaiseWaveCleared(string zoneId, string rating) => WaveCleared?.Invoke(zoneId, rating);
        public static void RaiseHeraldoConfronted(int hits, bool completed) => HeraldoConfronted?.Invoke(hits, completed);
        public static void RaiseRecuerdoUnlocked(string id) => RecuerdoUnlocked?.Invoke(id);
        public static void RaiseAbilityUsed(string id) => AbilityUsed?.Invoke(id);
        public static void RaiseThreatDetected(Vector3 position) => ThreatDetected?.Invoke(position);
        public static void RaiseEnergyCritical(bool critical) => EnergyCritical?.Invoke(critical);
    }
}
