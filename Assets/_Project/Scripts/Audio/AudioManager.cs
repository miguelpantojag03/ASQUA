using System;
using Ascua.Characters;
using Ascua.Core;
using Ascua.UI;
using Ascua.World;
using UnityEngine;
using UnityEngine.Audio;

namespace Ascua.Audio
{
    /// <summary>Los tres grupos de volumen del juego.</summary>
    public enum AudioChannel
    {
        Musica,
        Ambiente,
        Efectos
    }

    /// <summary>
    /// Sonido de Ascua en un solo sitio:
    ///  - Música adaptativa en 3 capas sincronizadas: base (siempre), esperanza (crece con la Esperanza
    ///    media de las zonas) y tensión (crece con el Heraldo cerca y con la ráfaga de la Penumbra).
    ///  - Ambiente: viento y agua lejana en bucle.
    ///  - Efectos: suenan solos al escuchar los eventos del juego (EventBus, ventanas, zonas...).
    /// Volumen por grupo guardado en PlayerPrefs. Si se asigna un AudioMixer con parámetros expuestos
    /// ("VolMusica", "VolAmbiente", "VolEfectos"), también los controla y enruta cada fuente a su grupo.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mezclador (opcional)")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup ambientGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private string musicParameter = "VolMusica";
        [SerializeField] private string ambientParameter = "VolAmbiente";
        [SerializeField] private string sfxParameter = "VolEfectos";

        [Header("Música (3 capas de la misma duración)")]
        [SerializeField] private AudioClip musicBase;
        [SerializeField] private AudioClip musicHope;
        [SerializeField] private AudioClip musicTension;
        [SerializeField] private float musicLevel = 0.6f;
        [SerializeField] private float layerSmoothing = 0.8f;

        [Header("Ambiente")]
        [SerializeField] private AudioClip windLoop;
        [SerializeField] private AudioClip waterLoop;
        [SerializeField] private float ambientLevel = 0.5f;

        [Header("Efectos")]
        [SerializeField] private AudioClip sfxPickup;
        [SerializeField] private AudioClip sfxQuestComplete;
        [SerializeField] private AudioClip sfxWindowOn;
        [SerializeField] private AudioClip sfxZoneLost;
        [SerializeField] private AudioClip sfxNpcTalk;
        [SerializeField] private AudioClip sfxPulse;
        [SerializeField] private AudioClip sfxHopeLevel;
        [SerializeField] private AudioClip sfxJump;
        [SerializeField] private AudioClip sfxLand;
        [SerializeField] private AudioClip sfxDash;
        [SerializeField] private AudioClip sfxLanternResize;
        [SerializeField] private AudioClip sfxSurge;
        [SerializeField] private AudioClip sfxBond;
        [SerializeField] private AudioClip sfxMoment;
        [SerializeField] private AudioClip sfxUiClick;
        [SerializeField] private int sfxVoices = 10;

        private const string MusicKey = "ascua_vol_musica";
        private const string AmbientKey = "ascua_vol_ambiente";
        private const string SfxKey = "ascua_vol_efectos";

        public event Action VolumesChanged;

        private AudioSource baseSource, hopeSource, tensionSource, windSource, waterSource;
        private AudioSource[] sfxSources;
        private int nextVoice;
        private float musicVolume = 0.8f, ambientVolume = 0.8f, sfxVolume = 0.9f;
        private float hopeLayer, tensionLayer, momentBoost;
        private bool surgeActive;
        private float lastWindowSound;

        public float MusicVolume => musicVolume;
        public float AmbientVolume => ambientVolume;
        public float SfxVolume => sfxVolume;

        private void Awake()
        {
            Instance = this;
            musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
            ambientVolume = PlayerPrefs.GetFloat(AmbientKey, 0.8f);
            sfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.9f);

            baseSource = CreateSource("Musica_Base", musicBase, true, musicGroup);
            hopeSource = CreateSource("Musica_Esperanza", musicHope, true, musicGroup);
            tensionSource = CreateSource("Musica_Tension", musicTension, true, musicGroup);
            windSource = CreateSource("Ambiente_Viento", windLoop, true, ambientGroup);
            waterSource = CreateSource("Ambiente_Agua", waterLoop, true, ambientGroup);

            sfxSources = new AudioSource[Mathf.Max(2, sfxVoices)];
            for (int i = 0; i < sfxSources.Length; i++) sfxSources[i] = CreateSource("Efecto_" + i, null, false, sfxGroup);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Las tres capas arrancan en el mismo instante de audio: quedan sincronizadas para siempre.
            double start = AudioSettings.dspTime + 0.1;
            foreach (AudioSource s in new[] { baseSource, hopeSource, tensionSource })
            {
                if (s.clip != null) s.PlayScheduled(start);
            }
            if (windSource.clip != null) windSource.Play();
            if (waterSource.clip != null) waterSource.PlayScheduled(start + 1.7);
            ApplyMixer();
            UpdateLayers(10f);
        }

        private void OnEnable()
        {
            EventBus.ItemPickedUp += OnItemPicked;
            EventBus.QuestStateChanged += OnQuestState;
            EventBus.NpcTalked += OnNpcTalked;
            EventBus.LightPulseFired += OnPulse;
            EventBus.HopeLevelReached += OnHopeLevel;
            EventBus.PlayerJumped += OnJump;
            EventBus.PlayerLanded += OnLand;
            EventBus.PlayerDashed += OnDash;
            EventBus.PlayerMantled += OnMantle;
            EventBus.LanternResized += OnLanternResized;
            EventBus.PenumbraSurge += OnSurge;
            EventBus.BondChanged += OnBond;
            EventBus.NarrativeMoment += OnMoment;
            EventBus.ShortcutDiscovered += OnShortcut;
            WindowLight.AnyWindowLit += OnWindowLit;
            ZoneHope.AnyZoneDied += OnZoneDied;
        }

        private void OnDisable()
        {
            EventBus.ItemPickedUp -= OnItemPicked;
            EventBus.QuestStateChanged -= OnQuestState;
            EventBus.NpcTalked -= OnNpcTalked;
            EventBus.LightPulseFired -= OnPulse;
            EventBus.HopeLevelReached -= OnHopeLevel;
            EventBus.PlayerJumped -= OnJump;
            EventBus.PlayerLanded -= OnLand;
            EventBus.PlayerDashed -= OnDash;
            EventBus.PlayerMantled -= OnMantle;
            EventBus.LanternResized -= OnLanternResized;
            EventBus.PenumbraSurge -= OnSurge;
            EventBus.BondChanged -= OnBond;
            EventBus.NarrativeMoment -= OnMoment;
            EventBus.ShortcutDiscovered -= OnShortcut;
            WindowLight.AnyWindowLit -= OnWindowLit;
            ZoneHope.AnyZoneDied -= OnZoneDied;
        }

        private void Update()
        {
            UpdateLayers(Time.unscaledDeltaTime);
        }

        // ================================================================== Música adaptativa

        private void UpdateLayers(float dt)
        {
            // Esperanza media de las zonas vivas (0-1).
            float hope = 0f;
            int alive = 0;
            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].IsDead) continue;
                hope += zones[i].Normalized;
                alive++;
            }
            hope = alive > 0 ? hope / alive : 0f;

            // Tensión: Heraldo cerca y ráfaga de la Penumbra.
            float heraldo = 0f;
            if (Heraldo_AI.Instance != null) heraldo = Mathf.Clamp01(1f - (Heraldo_AI.Instance.DistanceToPlayer - 4f) / 18f);
            float tension = Mathf.Max(heraldo, surgeActive ? 1f : 0f);

            float k = 1f - Mathf.Exp(-layerSmoothing * dt);
            hopeLayer = Mathf.Lerp(hopeLayer, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, hope)), k);
            tensionLayer = Mathf.Lerp(tensionLayer, tension, k * 1.5f);
            momentBoost = Mathf.MoveTowards(momentBoost, 0f, dt * 0.25f);

            // Con Audio Mixer el volumen del jugador lo aplica el mixer (dB); sin él, se aplica aquí.
            float music = musicLevel * (mixer != null ? 1f : musicVolume);
            baseSource.volume = music * Mathf.Lerp(0.8f, 0.55f, tensionLayer) * (1f + momentBoost * 0.3f);
            hopeSource.volume = music * Mathf.Max(hopeLayer * (1f - tensionLayer * 0.6f), momentBoost);
            tensionSource.volume = music * tensionLayer * 0.9f;

            float ambient = ambientLevel * (mixer != null ? 1f : ambientVolume);
            windSource.volume = ambient * Mathf.Lerp(0.7f, 1f, tensionLayer);
            waterSource.volume = ambient * 0.6f;
        }

        // ================================================================== Efectos

        /// <summary>Reproduce un efecto. 'subtitle' se muestra si el jugador activó los subtítulos de efectos.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f, string subtitle = null)
        {
            if (!string.IsNullOrEmpty(subtitle) && AccessibilitySettings.SfxSubtitles && SubtitleUI.Instance != null)
            {
                SubtitleUI.Instance.Show(subtitle);
            }
            if (clip == null || sfxSources == null) return;

            AudioSource source = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * (mixer != null ? 1f : sfxVolume));
        }

        public void PlayUiClick() => PlaySfx(sfxUiClick, 0.6f);

        private void OnItemPicked(string id, string name) => PlaySfx(sfxPickup, 0.8f, UnityEngine.Random.Range(0.97f, 1.05f));
        private void OnNpcTalked(string npcId) => PlaySfx(sfxNpcTalk, 0.55f);
        private void OnPulse(Vector3 origin, float radius) => PlaySfx(sfxPulse, 0.9f, 1f, "[Ráfaga de luz]");
        private void OnHopeLevel(string zoneId, int level, Vector3 position) => PlaySfx(sfxHopeLevel, 0.7f, 1f + 0.12f * (level - 1), "[Campanilla: el barrio recupera Esperanza]");
        private void OnJump(Vector3 position) => PlaySfx(sfxJump, 0.5f, UnityEngine.Random.Range(0.95f, 1.08f));
        private void OnLand(Vector3 position) => PlaySfx(sfxLand, 0.55f, UnityEngine.Random.Range(0.92f, 1.05f));
        private void OnDash(Vector3 position) => PlaySfx(sfxDash, 0.6f);
        private void OnMantle(Vector3 position) => PlaySfx(sfxJump, 0.4f, 0.85f);
        private void OnLanternResized(int index) => PlaySfx(sfxLanternResize, 0.45f, 0.85f + 0.1f * index);
        private void OnBond(string npcId, int level) => PlaySfx(sfxBond, 0.7f, 1f, "[Un acorde cálido: el Vínculo crece]");
        private void OnShortcut(string id, string name) => PlaySfx(sfxHopeLevel, 0.8f, 1.35f, "[Has encontrado un rincón escondido]");

        private void OnQuestState(string questId, string state)
        {
            if (state == "Completada") PlaySfx(sfxQuestComplete, 0.85f, 1f, "[Melodía: misión completada]");
        }

        private void OnWindowLit(WindowLight window)
        {
            // Muchas ventanas pueden encenderse a la vez: como mucho un "tintineo" cada 0,2 s.
            if (Time.unscaledTime - lastWindowSound < 0.2f) return;
            lastWindowSound = Time.unscaledTime;
            PlaySfx(sfxWindowOn, 0.3f, UnityEngine.Random.Range(0.9f, 1.15f));
        }

        private void OnZoneDied(ZoneHope zone) => PlaySfx(sfxZoneLost, 1f, 1f, "[Acorde grave: " + zone.DisplayName + " se ha apagado]");

        private void OnSurge(bool started)
        {
            surgeActive = started;
            if (started) PlaySfx(sfxSurge, 0.9f, 1f, "[Retumbo: la Penumbra se agita]");
        }

        private void OnMoment(string id, bool started)
        {
            if (!started) return;
            momentBoost = 1f;
            PlaySfx(sfxMoment, 0.9f);
        }

        // ================================================================== Volumen

        public float GetVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Musica: return musicVolume;
                case AudioChannel.Ambiente: return ambientVolume;
                default: return sfxVolume;
            }
        }

        public void SetVolume(AudioChannel channel, float value)
        {
            value = Mathf.Clamp01(value);
            switch (channel)
            {
                case AudioChannel.Musica:
                    musicVolume = value;
                    PlayerPrefs.SetFloat(MusicKey, value);
                    break;
                case AudioChannel.Ambiente:
                    ambientVolume = value;
                    PlayerPrefs.SetFloat(AmbientKey, value);
                    break;
                default:
                    sfxVolume = value;
                    PlayerPrefs.SetFloat(SfxKey, value);
                    break;
            }
            ApplyMixer();
            VolumesChanged?.Invoke();
        }

        /// <summary>Si hay mezclador, traduce el volumen lineal a decibelios en sus parámetros expuestos.</summary>
        private void ApplyMixer()
        {
            if (mixer == null) return;
            mixer.SetFloat(musicParameter, ToDecibels(musicVolume));
            mixer.SetFloat(ambientParameter, ToDecibels(ambientVolume));
            mixer.SetFloat(sfxParameter, ToDecibels(sfxVolume));
        }

        private static float ToDecibels(float linear) => linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;

        private AudioSource CreateSource(string sourceName, AudioClip clip, bool loop, AudioMixerGroup group)
        {
            GameObject go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = group;
            // La música y el ambiente siguen sonando con el juego en pausa.
            source.ignoreListenerPause = true;
            return source;
        }
    }
}
