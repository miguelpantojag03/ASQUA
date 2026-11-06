using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Progreso de la partida que sobrevive entre noches (y a recargar la escena):
    /// número de noche, misiones ya completadas, páginas del Diario, atajos descubiertos,
    /// Recuerdos con movimiento de combate y oleadas defendidas (que desbloquean la Vara de luz).
    /// Las mejoras del Farol viven en PlayerUpgrades. SaveSystem lo guarda en disco con Snapshot() / Restore().
    /// </summary>
    public static class GameProgress
    {
        private static readonly HashSet<string> completedQuests = new HashSet<string>();
        private static readonly HashSet<string> foundPages = new HashSet<string>();
        private static readonly HashSet<string> discoveredShortcuts = new HashSet<string>();
        private static readonly HashSet<string> seenMoments = new HashSet<string>();
        private static readonly HashSet<string> recuerdos = new HashSet<string>();
        // Páginas encontradas en ESTA noche, en orden: los Susurros roban la última.
        private static readonly List<string> pagesThisNight = new List<string>();
        // Decisiones con consecuencia tardía (p. ej. a quién dio Ilse las velas de reserva en la Noche 2).
        private static readonly HashSet<string> flags = new HashSet<string>();
        private static readonly List<string> flagsThisNight = new List<string>();

        /// <summary>Noche actual (1, 2, 3...).</summary>
        public static int CurrentNight { get; private set; } = 1;

        /// <summary>True mientras no se haya completado el tutorial de la Noche 1.</summary>
        public static bool TutorialPending { get; set; } = true;

        public static IReadOnlyCollection<string> FoundPages => foundPages;

        /// <summary>Oleadas de sombras superadas en toda la partida (la Vara de luz se desbloquea con 3).</summary>
        public static int WavesDefended { get; private set; }

        /// <summary>Ids de los Recuerdos que ya enseñan un movimiento.</summary>
        public static IReadOnlyCollection<string> Recuerdos => recuerdos;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Al entrar en Play desde el editor se empieza una partida nueva.
            ResetAll();
        }

        public static void ResetAll()
        {
            CurrentNight = 1;
            TutorialPending = true;
            completedQuests.Clear();
            foundPages.Clear();
            discoveredShortcuts.Clear();
            seenMoments.Clear();
            recuerdos.Clear();
            pagesThisNight.Clear();
            flags.Clear();
            flagsThisNight.Clear();
            WavesDefended = 0;
            // Partida nueva: se olvidan los niveles generados y el perfil del jugador (IA de niveles).
            Ascua.AILevelGen.AIDirector.ResetAll();
        }

        public static void AdvanceNight()
        {
            CurrentNight++;
            pagesThisNight.Clear();
            flagsThisNight.Clear();
        }

        public static void MarkQuestCompleted(string questId) => completedQuests.Add(questId);
        public static bool IsQuestCompleted(string questId) => completedQuests.Contains(questId);
        public static void UnmarkQuest(string questId) => completedQuests.Remove(questId);

        /// <summary>Registra una página del Diario. Devuelve true si es nueva.</summary>
        public static bool AddPage(string pageId)
        {
            if (!foundPages.Add(pageId)) return false;
            pagesThisNight.Add(pageId);
            return true;
        }

        /// <summary>
        /// Un Susurro roba la última página encontrada esta noche. Devuelve su id (o null si no hay).
        /// La página deja de contar como encontrada hasta que se recupere.
        /// </summary>
        public static string StealLatestPage()
        {
            if (pagesThisNight.Count == 0) return null;
            string id = pagesThisNight[pagesThisNight.Count - 1];
            pagesThisNight.RemoveAt(pagesThisNight.Count - 1);
            foundPages.Remove(id);
            return id;
        }

        /// <summary>Devuelve una página robada (al disolver al Susurro que la llevaba).</summary>
        public static void RestorePage(string pageId)
        {
            if (string.IsNullOrEmpty(pageId)) return;
            if (foundPages.Add(pageId)) pagesThisNight.Add(pageId);
        }

        /// <summary>Desbloquea un Recuerdo. Devuelve true si es nuevo.</summary>
        public static bool UnlockRecuerdo(string id) => recuerdos.Add(id);
        public static bool HasRecuerdo(string id) => recuerdos.Contains(id);

        public static void AddWaveDefended() => WavesDefended++;

        /// <summary>Guarda una decisión que tendrá consecuencias en noches futuras.</summary>
        public static void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
            flagsThisNight.Add(flag);
        }

        public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        /// <summary>Al reintentar una noche se deshacen las decisiones tomadas en ella.</summary>
        public static void RevertNightFlags()
        {
            foreach (string flag in flagsThisNight) flags.Remove(flag);
            flagsThisNight.Clear();
        }

        /// <summary>Solo para pruebas: salta directamente a una noche concreta.</summary>
        public static void SetNightForTesting(int night) => CurrentNight = Mathf.Max(1, night);
        public static bool HasPage(string pageId) => foundPages.Contains(pageId);

        /// <summary>Registra un atajo descubierto. Devuelve true si es nuevo.</summary>
        public static bool DiscoverShortcut(string id) => discoveredShortcuts.Add(id);
        public static bool IsShortcutDiscovered(string id) => discoveredShortcuts.Contains(id);

        /// <summary>Los momentos memorables solo se ven una vez por partida.</summary>
        public static bool MarkMomentSeen(string id) => seenMoments.Add(id);

        /// <summary>True si el momento ya se vio (sin marcarlo).</summary>
        public static bool HasSeenMoment(string id) => seenMoments.Contains(id);

        // ================================================================== Guardado (SaveSystem)

        /// <summary>Copia del progreso para guardarla en disco.</summary>
        public static GameProgressData Snapshot()
        {
            return new GameProgressData
            {
                currentNight = CurrentNight,
                tutorialPending = TutorialPending,
                completedQuests = new List<string>(completedQuests),
                foundPages = new List<string>(foundPages),
                shortcuts = new List<string>(discoveredShortcuts),
                seenMoments = new List<string>(seenMoments),
                recuerdos = new List<string>(recuerdos),
                flags = new List<string>(flags),
                wavesDefended = WavesDefended
            };
        }

        /// <summary>Vuelve al progreso guardado (empieza desde una partida limpia y copia los datos).</summary>
        public static void Restore(GameProgressData data)
        {
            ResetAll();
            if (data == null) return;
            CurrentNight = Mathf.Max(1, data.currentNight);
            TutorialPending = data.tutorialPending;
            WavesDefended = Mathf.Max(0, data.wavesDefended);
            CopyInto(data.completedQuests, completedQuests);
            CopyInto(data.foundPages, foundPages);
            CopyInto(data.shortcuts, discoveredShortcuts);
            CopyInto(data.seenMoments, seenMoments);
            CopyInto(data.recuerdos, recuerdos);
            CopyInto(data.flags, flags);
        }

        private static void CopyInto(List<string> source, HashSet<string> target)
        {
            if (source == null) return;
            foreach (string id in source)
            {
                if (!string.IsNullOrEmpty(id)) target.Add(id);
            }
        }
    }
}
