using System.Collections.Generic;
using Ascua.Combat;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Momento memorable de las noches 3, 4 y 5 (handoff §8.6), creado por código: NightManager lo crea
    /// en Awake según la noche (no hay que cablear nada en la escena).
    ///  - Noche 3 · "El tren fantasma" (55 %)   → NightEventDirector.Tren.cs
    ///  - Noche 4 · "El nombre" (70 %)          → NightEventDirector.Nombre.cs
    ///  - Noche 5 · "Los farolillos" (5 %)      → NightEventDirector.Farolillos.cs
    /// Cada evento ocurre una vez por partida, solo en su noche, y espera a que no haya diálogo, oleada,
    /// Cacería, pausa ni otro momento narrativo. Si al 90 % no se dio, no ocurre (NightEventRules).
    /// Al empezar y terminar publica EventBus.RaiseNarrativeMoment para que la música y la Cacería lo respeten.
    /// Todo avanza con Time.deltaTime: la pausa lo congela.
    /// </summary>
    public partial class NightEventDirector : MonoBehaviour
    {
        public const string TrenId = "noche3_tren";
        public const string NombreId = "noche4_nombre";
        public const string FarolillosId = "noche5_farolillos";

        private struct NightEventDef
        {
            public int Night;
            public string Id;
            public float At;

            public NightEventDef(int night, string id, float at)
            {
                Night = night;
                Id = id;
                At = at;
            }
        }

        private static readonly NightEventDef[] Events =
        {
            new NightEventDef(3, TrenId, 0.55f),
            new NightEventDef(4, NombreId, 0.70f),
            new NightEventDef(5, FarolillosId, 0.05f)
        };

        public static NightEventDirector Instance { get; private set; }

        private NightEventDef def;
        private bool playing;
        private readonly HashSet<string> otherMoments = new HashSet<string>();
        private Transform player;

        public string EventId => def.Id;
        public int EventNight => def.Night;
        public bool IsPlaying => playing;
        public bool HasPlayed => GameProgress.HasSeenMoment(def.Id);

        /// <summary>Crea el director si la noche tiene evento propio (3, 4 o 5). Devuelve null si no.</summary>
        public static NightEventDirector CreateFor(int night)
        {
            for (int i = 0; i < Events.Length; i++)
            {
                if (Events[i].Night != night) continue;
                NightEventDirector director = new GameObject("NightEventDirector").AddComponent<NightEventDirector>();
                director.def = Events[i];
                return director;
            }
            return null;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Si la escena se descarga a mitad del evento, la música no se queda en "momento".
            if (playing) EventBus.RaiseNarrativeMoment(def.Id, false);
        }

        private void OnEnable()
        {
            EventBus.NarrativeMoment += OnNarrativeMoment;
        }

        private void OnDisable()
        {
            EventBus.NarrativeMoment -= OnNarrativeMoment;
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        private void OnNarrativeMoment(string id, bool started)
        {
            if (id == def.Id) return;   // el nuestro no cuenta como "otro momento"
            if (string.IsNullOrEmpty(id)) id = "?";
            if (started) otherMoments.Add(id);
            else otherMoments.Remove(id);
        }

        private void Update()
        {
            if (playing || string.IsNullOrEmpty(def.Id)) return;
            NightManager night = NightManager.Instance;
            if (night == null || !night.IsRunning) return;
            if (Time.deltaTime <= 0f) return;   // en pausa no se programa nada

            bool seen = GameProgress.HasSeenMoment(def.Id);
            if (NightEventRules.ShouldTrigger(GameProgress.CurrentNight, def.Night, night.Progress01, def.At, seen, IsBusy()))
            {
                Begin();
            }
        }

        /// <summary>Diálogo, oleada, Cacería, pausa u otro momento narrativo en curso.</summary>
        public bool IsBusy()
        {
            if (PauseMenu.IsPaused) return true;
            if (FinalBossDirector.HoldsNight) return true;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return true;
            CombatWaveDirector waves = CombatWaveDirector.Instance;
            if (waves != null && (waves.HasActiveWave || waves.AliveCount > 0)) return true;
            HeraldoHuntDirector hunts = HeraldoHuntDirector.Instance;
            if (hunts != null && hunts.IsBusy) return true;
            return otherMoments.Count > 0;
        }

        /// <summary>Depuración (F7): lanza ya el evento de esta noche, aunque ya se haya visto. False si ahora no se puede.</summary>
        public bool TriggerNow()
        {
            if (playing || IsBusy()) return false;
            NightManager night = NightManager.Instance;
            if (night == null || !night.IsRunning) return false;
            Begin();
            return true;
        }

        private void Begin()
        {
            playing = true;
            // Al reintentar la noche, el evento vuelve a ocurrir (como sus recompensas y decisiones).
            GameProgress.MarkMomentSeenThisNight(def.Id);
            EventBus.RaiseNarrativeMoment(def.Id, true);

            switch (def.Id)
            {
                case TrenId: BeginTren(); break;
                case NombreId: BeginNombre(); break;
                case FarolillosId: BeginFarolillos(); break;
            }
        }

        /// <summary>Lo llama cada evento al terminar su parte "de escena".</summary>
        private void FinishEvent()
        {
            if (!playing) return;
            playing = false;
            EventBus.RaiseNarrativeMoment(def.Id, false);
        }

        // Cada evento vive en su propio archivo (partial). Sin implementación, la llamada desaparece.
        partial void BeginTren();
        partial void BeginNombre();
        partial void BeginFarolillos();

        // ------------------------------------------------------------------ utilidades comunes

        private static void Toast(string message, float hold = 2.8f)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, hold);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
