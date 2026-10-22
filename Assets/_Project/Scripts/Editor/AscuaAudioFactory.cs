using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Ascua.EditorTools
{
    /// <summary>Clips de audio generados (rutas fijas para poder sustituirlos por música real con el mismo nombre).</summary>
    public class AudioAssets
    {
        public AudioClip MusicBase, MusicHope, MusicTension, Wind, Water;
        public AudioClip Pickup, QuestComplete, WindowOn, ZoneLost, NpcTalk, Pulse, HopeLevel;
        public AudioClip Jump, Land, Dash, LanternResize, Surge, Bond, Moment, UiClick;
        // Combate de luz.
        public AudioClip Swing, LightHit, Dissolve, Hurt, PerfectDodge, WaveStart, WaveCleared, BruteWarning, ShadowBolt;
        // Impacto y avisos (Partes A+B).
        public AudioClip ComboFinisher, PowerDown, CriticalEnergy, Threat;
        // Audio Mixer "AM_Ascua" con 3 grupos bajo Master y sus volúmenes expuestos.
        public AudioMixer Mixer;
        public AudioMixerGroup MusicGroup, AmbientGroup, SfxGroup;
    }

    /// <summary>
    /// Genera por código toda la música y los efectos como WAV (Assets/_Project/Audio/Generado).
    /// Son "temporales de calidad": melancólicos, en La menor, a 60 bpm. Para usar música real
    /// (incompetech, Pixabay, freesound...) basta con sustituir el WAV por otro con el MISMO nombre,
    /// o asignar otro clip en el AudioManager. Solo se generan si no existen (no pisa tus cambios).
    /// </summary>
    public static class AscuaAudioFactory
    {
        private const string Folder = AscuaAssetFactory.Root + "/Audio/Generado";
        private const int Rate = 22050;
        private const float LoopSeconds = 32f; // 8 compases de 4 tiempos a 60 bpm

        // Am – F – C – G, cada acorde 2 compases (8 s).
        private static readonly float[][] Chords =
        {
            new[] { 220.00f, 261.63f, 329.63f },
            new[] { 174.61f, 220.00f, 261.63f },
            new[] { 261.63f, 329.63f, 392.00f },
            new[] { 196.00f, 246.94f, 293.66f }
        };

        public static AudioAssets CreateAll()
        {
            AscuaAssetFactory.EnsureFolder(Folder);
            AudioAssets a = new AudioAssets
            {
                MusicBase = Clip("MUS_Base_Pad", () => Pad()),
                MusicHope = Clip("MUS_Esperanza_Piano", () => Piano()),
                MusicTension = Clip("MUS_Tension_Cuerdas", () => Tension()),
                Wind = Clip("AMB_Viento", () => Wind(20f)),
                Water = Clip("AMB_Agua", () => Water(24f)),
                Pickup = Clip("SFX_Recoger", () => Chime(new[] { 1318.5f, 1760f }, 0.09f, 0.7f, 0.5f)),
                QuestComplete = Clip("SFX_MisionCompletada", () => Chime(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.16f, 1.8f, 0.55f)),
                WindowOn = Clip("SFX_VentanaEnciende", () => Chime(new[] { 1567.98f }, 0f, 0.6f, 0.35f)),
                ZoneLost = Clip("SFX_ZonaApagada", () => Thump(1.8f)),
                NpcTalk = Clip("SFX_HablarNPC", () => Chime(new[] { 587.33f, 783.99f }, 0.08f, 0.5f, 0.35f)),
                Pulse = Clip("SFX_PulsoLuz", () => Sweep(1.1f, 300f, 1600f, 0.6f)),
                HopeLevel = Clip("SFX_NivelEsperanza", () => Chime(new[] { 880f, 1318.5f }, 0.07f, 1.1f, 0.45f)),
                Jump = Clip("SFX_Salto", () => Whoosh(0.25f, 0.35f)),
                Land = Clip("SFX_Aterrizaje", () => SoftHit(0.28f)),
                Dash = Clip("SFX_Esquiva", () => Whoosh(0.32f, 0.55f)),
                LanternResize = Clip("SFX_FarolTamano", () => Sweep(0.45f, 220f, 440f, 0.3f)),
                Surge = Clip("SFX_RafagaPenumbra", () => Rumble(2.6f)),
                Bond = Clip("SFX_Vinculo", () => Chime(new[] { 440f, 554.37f, 659.25f }, 0.12f, 1.6f, 0.45f)),
                Moment = Clip("SFX_Momento", () => Swell(4f)),
                UiClick = Clip("SFX_ClicUI", () => Chime(new[] { 2093f }, 0f, 0.08f, 0.25f))
            };
            // Combate: todo suena a luz y cristal (nada de golpes metálicos ni gritos).
            a.Swing = Clip("SFX_BarridoLuz", () => Mix(Whoosh(0.26f, 0.4f), Sweep(0.26f, 600f, 1900f, 0.22f)));
            a.LightHit = Clip("SFX_GolpeLuz", () => Chime(new[] { 1760f, 2637f }, 0.02f, 0.35f, 0.4f));
            a.Dissolve = Clip("SFX_SombraDisuelta", () => Mix(Sweep(0.9f, 1300f, 420f, 0.25f), Chime(new[] { 880f, 1318.5f, 1760f }, 0.06f, 1f, 0.35f)));
            a.Hurt = Clip("SFX_FarolHerido", () => Mix(Thump(0.5f), Sweep(0.4f, 320f, 120f, 0.35f)));
            a.PerfectDodge = Clip("SFX_EsquivaPerfecta", () => Chime(new[] { 2093f, 2637f, 3136f }, 0.03f, 0.8f, 0.45f));
            a.WaveStart = Clip("SFX_OleadaSombras", () => Mix(Rumble(1.6f), Thump(1.2f)));
            a.WaveCleared = Clip("SFX_OleadaSuperada", () => Chime(new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.1f, 2f, 0.5f));
            a.BruteWarning = Clip("SFX_AvisoEmbestida", () => Mix(Rumble(0.9f), Sweep(0.8f, 80f, 210f, 0.4f)));
            a.ShadowBolt = Clip("SFX_ProyectilSombra", () => Sweep(0.35f, 520f, 240f, 0.35f));
            // Remate del combo: cristal brillante + golpe sordo (pesa más que un barrido normal).
            a.ComboFinisher = Clip("SFX_GolpeRemate", () => Mix(Chime(new[] { 1318.5f, 1975.5f, 2637f }, 0.015f, 0.6f, 0.45f), SoftHit(0.3f)));
            // Daño recibido: "apagón" (la luz cae de golpe, como una bombilla que se funde).
            a.PowerDown = Clip("SFX_Apagon", () => Mix(Sweep(0.55f, 700f, 60f, 0.45f), Thump(0.45f)));
            // Energía crítica: latido doble y grave.
            a.CriticalEnergy = Clip("SFX_EnergiaCritica", () => Mix(Thump(0.35f), Thump(0.8f)));
            // Amenaza detectada: dos notas graves descendentes.
            a.Threat = Clip("SFX_Amenaza", () => Mix(Sweep(0.5f, 220f, 165f, 0.35f), Rumble(0.6f)));

            CreateMixer(a);
            return a;
        }

        // ================================================================== Audio Mixer

        public const string MixerPath = AscuaAssetFactory.Root + "/Audio/AM_Ascua.mixer";
        private static readonly string[] GroupNames = { "Musica", "Ambiente", "Efectos" };
        private static readonly string[] ParameterNames = { "VolMusica", "VolAmbiente", "VolEfectos" };

        /// <summary>
        /// Crea (solo si no existe) el Audio Mixer con los grupos Musica / Ambiente / Efectos y expone
        /// su volumen como VolMusica / VolAmbiente / VolEfectos. Unity no ofrece API pública para crear
        /// mixers, así que se usa la interna del editor; si fallara (otra versión de Unity), el juego
        /// funciona igual sin mixer (el AudioManager aplica los volúmenes en cada AudioSource).
        /// </summary>
        private static void CreateMixer(AudioAssets a)
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                try
                {
                    BuildMixerAsset();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ascua] No se pudo crear el Audio Mixer por código (" + e.GetBaseException().Message +
                                     "). El audio funciona sin él; puedes crearlo a mano (ver guía).\n" + e);
                    AssetDatabase.DeleteAsset(MixerPath);
                    return;
                }
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            if (mixer == null) return;

            a.Mixer = mixer;
            a.MusicGroup = FindGroup(mixer, GroupNames[0]);
            a.AmbientGroup = FindGroup(mixer, GroupNames[1]);
            a.SfxGroup = FindGroup(mixer, GroupNames[2]);
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            foreach (AudioMixerGroup g in mixer.FindMatchingGroups("Master"))
            {
                if (g.name == name) return g;
            }
            return null;
        }

        private static void BuildMixerAsset()
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Assembly editor = typeof(Editor).Assembly;
            Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController", true);
            Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);

            object controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
            object master = controllerType.GetProperty("masterGroup", all).GetValue(controller);
            MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", all);
            MethodInfo addChild = controllerType.GetMethod("AddChildToParent", all);
            MethodInfo addExposed = controllerType.GetMethod("AddExposedParameter", all);
            PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", all);

            for (int i = 0; i < GroupNames.Length; i++)
            {
                object group = createGroup.Invoke(controller, new object[] { GroupNames[i], false });
                addChild.Invoke(controller, new[] { group, master });

                // Exponer el volumen del grupo y renombrar el parámetro.
                GUID volumeGuid = (GUID)group.GetType().GetMethod("GetGUIDForVolume", all).Invoke(group, null);
                object path = Activator.CreateInstance(pathType, all, null, new[] { group, (object)volumeGuid }, null);
                addExposed.Invoke(controller, new[] { path });

                Array parameters = (Array)exposedProperty.GetValue(controller);
                for (int p = 0; p < parameters.Length; p++)
                {
                    object parameter = parameters.GetValue(p);
                    FieldInfo guidField = parameter.GetType().GetField("guid", all);
                    if ((GUID)guidField.GetValue(parameter) != volumeGuid) continue;
                    parameter.GetType().GetField("name", all).SetValue(parameter, ParameterNames[i]);
                    parameters.SetValue(parameter, p);
                }
                exposedProperty.SetValue(controller, parameters);
            }

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath);
        }

        /// <summary>Suma dos sonidos (el más largo marca la duración) y normaliza.</summary>
        private static float[] Mix(float[] a, float[] b)
        {
            float[] result = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) result[i] += a[i];
            for (int i = 0; i < b.Length; i++) result[i] += b[i];
            return Normalize(result, 0.75f);
        }

        // ================================================================== Música

        /// <summary>Capa base: colchón de cuerdas suaves (senos desafinados) que sigue los acordes.</summary>
        private static float[] Pad()
        {
            float[] data = new float[(int)(Rate * LoopSeconds)];
            for (int c = 0; c < Chords.Length; c++)
            {
                float start = c * 8f;
                foreach (float f in Chords[c])
                {
                    for (int detune = -1; detune <= 1; detune++)
                    {
                        float freq = f * 0.5f * (1f + detune * 0.003f);
                        AddTone(data, start - 0.8f, 9.6f, freq, 0.035f, 2.2f, 2.2f, 0f);
                    }
                }
                // Bajo: la fundamental una octava abajo.
                AddTone(data, start - 0.5f, 9f, Chords[c][0] * 0.25f, 0.05f, 1.5f, 2f, 0f);
            }
            return Normalize(data, 0.7f);
        }

        /// <summary>Capa de esperanza: arpegio de piano, notas sueltas que dejan aire.</summary>
        private static float[] Piano()
        {
            float[] data = new float[(int)(Rate * LoopSeconds)];
            System.Random rng = new System.Random(11);
            int[] pattern = { 0, 1, 2, 1, 0, 2, 1, 2 };
            for (int beat = 0; beat < 32; beat++)
            {
                float[] chord = Chords[beat / 8];
                if (beat % 4 == 3 && rng.NextDouble() < 0.5) continue; // respiraciones
                float note = chord[pattern[beat % pattern.Length]] * 2f;
                PianoNote(data, beat, note, 0.18f);
                // Contracanto ocasional, una octava arriba, a contratiempo.
                if (beat % 8 == 5) PianoNote(data, beat + 0.5f, chord[2] * 4f, 0.08f);
            }
            return Normalize(data, 0.7f);
        }

        /// <summary>Capa de tensión: nota pedal grave con trémolo lento y una segunda menor que roza.</summary>
        private static float[] Tension()
        {
            int n = (int)(Rate * LoopSeconds);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float tremolo = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t * 0.25f);
                float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 82.41f * t) * 0.3f;
                // La disonancia (Si bemol) aparece y se va cada 8 s.
                float rub = Mathf.Sin(2f * Mathf.PI * 116.54f * t) * 0.18f * Mathf.Pow(Mathf.Sin(Mathf.PI * (t % 8f) / 8f), 2f);
                data[i] = (drone + rub) * tremolo;
            }
            return Normalize(data, 0.6f);
        }

        // ================================================================== Ambiente

        private static float[] Wind(float seconds)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(3);
            float low = 0f, lower = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += (white - low) * 0.02f;
                lower += (low - lower) * 0.05f;
                // Ráfagas lentas, periódicas en la duración del bucle.
                float gust = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t * (3f / seconds)) * Mathf.Sin(2f * Mathf.PI * t * (1f / seconds));
                data[i] = lower * 6f * gust;
            }
            return Crossfade(Normalize(data, 0.5f), 0.5f);
        }

        private static float[] Water(float seconds)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(7);
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += (white - low) * 0.06f;
                // Olas pequeñas cada ~4 s que rompen y se retiran.
                float phase = (t % 4f) / 4f;
                float wave = Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 3f);
                data[i] = low * wave * 3f;
            }
            return Crossfade(Normalize(data, 0.45f), 0.5f);
        }

        // ================================================================== Efectos

        private static float[] Chime(float[] notes, float stagger, float seconds, float level)
        {
            float[] data = new float[(int)(Rate * (seconds + stagger * notes.Length + 0.2f))];
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * stagger;
                AddBell(data, start, notes[k], 1f / notes.Length + 0.3f, seconds);
            }
            return Normalize(data, level);
        }

        private static float[] Thump(float seconds)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float freq = Mathf.Lerp(90f, 38f, Mathf.Sqrt(t / seconds));
                phase += 2f * Mathf.PI * freq / Rate;
                // Acorde grave en La menor que se apaga.
                float chord = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 1.189f) + 0.4f * Mathf.Sin(phase * 1.498f);
                data[i] = chord * Mathf.Clamp01(t / 0.015f) * Mathf.Exp(-2.4f * t);
            }
            return Normalize(data, 0.9f);
        }

        private static float[] Sweep(float seconds, float from, float to, float level)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(5);
            float phase = 0f, band = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = t / seconds;
                float freq = Mathf.Lerp(from, to, k * k);
                phase += 2f * Mathf.PI * freq / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                band += (white - band) * Mathf.Lerp(0.05f, 0.35f, k);
                float env = Mathf.Sin(Mathf.PI * k);
                data[i] = (Mathf.Sin(phase) * 0.6f + band * 0.5f) * env;
            }
            return Normalize(data, level);
        }

        private static float[] Whoosh(float seconds, float level)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(9);
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += (white - low) * Mathf.Lerp(0.08f, 0.25f, Mathf.Sin(Mathf.PI * k));
                data[i] = low * Mathf.Sin(Mathf.PI * k);
            }
            return Normalize(data, level);
        }

        private static float[] SoftHit(float seconds)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(13);
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += (white - low) * 0.06f;
                data[i] = (low * 2f + Mathf.Sin(2f * Mathf.PI * 70f * t)) * Mathf.Exp(-18f * t);
            }
            return Normalize(data, 0.5f);
        }

        private static float[] Rumble(float seconds)
        {
            int n = (int)(Rate * seconds);
            float[] data = new float[n];
            System.Random rng = new System.Random(17);
            float low = 0f, lower = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = t / seconds;
                float white = (float)rng.NextDouble() * 2f - 1f;
                low += (white - low) * 0.01f;
                lower += (low - lower) * 0.03f;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.6f)) * Mathf.Clamp01((1f - k) / 0.3f);
                data[i] = (lower * 14f + Mathf.Sin(2f * Mathf.PI * 41.2f * t) * 0.5f) * env;
            }
            return Normalize(data, 0.9f);
        }

        /// <summary>Crecida para el momento memorable: acorde mayor que se abre (La mayor → Do mayor).</summary>
        private static float[] Swell(float seconds)
        {
            float[] data = new float[(int)(Rate * seconds)];
            float[] chord = { 261.63f, 329.63f, 392.00f, 523.25f };
            foreach (float f in chord)
            {
                AddTone(data, 0f, seconds, f, 0.06f, seconds * 0.45f, seconds * 0.45f, 0f);
                AddTone(data, 0f, seconds, f * 1.002f, 0.04f, seconds * 0.45f, seconds * 0.45f, 0f);
            }
            for (int k = 0; k < 4; k++) AddBell(data, 0.6f + k * 0.35f, chord[k] * 2f, 0.4f, 2.4f);
            return Normalize(data, 0.7f);
        }

        // ================================================================== Síntesis básica

        /// <summary>Tono senoidal con ataque y relajación lineales. Escribe "en bucle" (el final se suma al principio).</summary>
        private static void AddTone(float[] data, float start, float duration, float freq, float amp, float attack, float release, float vibrato)
        {
            int n = data.Length;
            int first = Mathf.FloorToInt(start * Rate);
            int count = Mathf.FloorToInt(duration * Rate);
            for (int j = 0; j < count; j++)
            {
                float t = (float)j / Rate;
                float env = Mathf.Min(1f, t / Mathf.Max(0.001f, attack)) * Mathf.Min(1f, (duration - t) / Mathf.Max(0.001f, release));
                float v = Mathf.Sin(2f * Mathf.PI * freq * t + vibrato * Mathf.Sin(2f * Mathf.PI * 5f * t)) * amp * env;
                int index = ((first + j) % n + n) % n;
                data[index] += v;
            }
        }

        /// <summary>Nota de "piano": fundamental + armónicos que decaen antes, ataque corto.</summary>
        private static void PianoNote(float[] data, float beat, float freq, float amp)
        {
            int n = data.Length;
            int first = Mathf.FloorToInt(beat * Rate);
            int count = Rate * 3;
            for (int j = 0; j < count; j++)
            {
                float t = (float)j / Rate;
                float attack = Mathf.Min(1f, t / 0.006f);
                float v = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-1.1f * t)
                          + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * Mathf.Exp(-2.5f * t)
                          + 0.15f * Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * Mathf.Exp(-4f * t);
                data[(first + j) % n] += v * amp * attack;
            }
        }

        /// <summary>Campana suave: parciales inarmónicos con decaimiento exponencial.</summary>
        private static void AddBell(float[] data, float start, float freq, float amp, float seconds)
        {
            int first = Mathf.FloorToInt(start * Rate);
            int count = Mathf.Min(data.Length - first, Mathf.FloorToInt(seconds * Rate));
            for (int j = 0; j < count; j++)
            {
                float t = (float)j / Rate;
                float attack = Mathf.Min(1f, t / 0.004f);
                float v = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-3f * t / seconds * 2f)
                          + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) * Mathf.Exp(-9f * t / seconds * 2f)
                          + 0.2f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t) * Mathf.Exp(-14f * t / seconds * 2f);
                data[first + j] += v * amp * attack;
            }
        }

        private static float[] Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
            return data;
        }

        /// <summary>Funde el final con el principio para que el bucle no tenga "clic".</summary>
        private static float[] Crossfade(float[] data, float seconds)
        {
            int fade = Mathf.Min(data.Length / 4, Mathf.FloorToInt(seconds * Rate));
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                int tail = data.Length - fade + i;
                data[i] = data[i] * k + data[tail] * (1f - k);
            }
            Array.Resize(ref data, data.Length - fade);
            return data;
        }

        // ================================================================== WAV

        private static AudioClip Clip(string name, Func<float[]> generate)
        {
            string path = $"{Folder}/{name}.wav";
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            if (!File.Exists(fullPath))
            {
                WriteWav(fullPath, generate());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>WAV PCM de 16 bits, mono.</summary>
        private static void WriteWav(string path, float[] samples)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);      // PCM
                writer.Write((short)1);      // mono
                writer.Write(Rate);
                writer.Write(Rate * 2);      // bytes por segundo
                writer.Write((short)2);      // alineación
                writer.Write((short)16);     // bits
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                foreach (float s in samples) writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
