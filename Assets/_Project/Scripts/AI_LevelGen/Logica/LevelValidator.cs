using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ascua.LevelGen
{
    /// <summary>
    /// Rangos permitidos del ADN. Son el "contrato" con la IA: lo que quede fuera se repara.
    /// Están calibrados con las noches diseñadas a mano (la Noche 1 y la 4 caen dentro).
    /// </summary>
    public static class LevelDnaLimits
    {
        public const float DurationMin = 60f, DurationMax = 360f;
        public const float StartHopeMin = 30f, StartHopeMax = 90f;
        public const float DecayMin = 0.2f, DecayMax = 2f;
        public const float RiseMin = 1f, RiseMax = 6f;
        public const float PenumbraStartMin = 40f, PenumbraStartMax = 80f;
        public const float PenumbraEndMin = 8f, PenumbraEndMax = 30f;
        /// <summary>La Penumbra tiene que cerrarse al menos esto (en metros) entre el inicio y el final.</summary>
        public const float PenumbraMinClosing = 10f;
        public const float FireflyIntervalMin = 2f, FireflyIntervalMax = 12f;
        public const int FireflyMaxMin = 2, FireflyMaxMax = 10;
        public const float SurgeAtMin = 0.3f, SurgeAtMax = 0.8f;
        public const float SurgeSecondsMin = 5f, SurgeSecondsMax = 15f;
        public const float SurgeMetersMin = 3f, SurgeMetersMax = 12f;
        public const float SurgeMultMin = 1.2f, SurgeMultMax = 2.5f;
        public const int QuestCountMin = 2, QuestCountMax = 6;
        public const int WaveCountMin = 0, WaveCountMax = 3;
        public const int CreaturesMin = 2, CreaturesMax = 6;
        public const float DrainMin = 0.6f, DrainMax = 2.2f;
        public const float HeraldoAtMin = 0.1f, HeraldoAtMax = 0.8f;
        public const float HeraldoSpeedMin = 0.8f, HeraldoSpeedMax = 2f;
        public const int MaxFragments = 3;
        public const int MaxFragmentLength = 120;
    }

    /// <summary>
    /// Validador del ADN (lógica pura, sin Unity, testeable). Es la defensa del juego: lo que diga la IA
    /// (o cualquier JSON) se repara aquí con Clamp antes de usarse. NUNCA descarta en silencio:
    /// cada corrección queda escrita en el registro que devuelve.
    /// </summary>
    public static class LevelValidator
    {
        public static readonly string[] Tones = { "esperanzado", "melancolico", "tenso", "climatico" };

        /// <summary>Pesos por defecto si el ADN trae pesos inservibles.</summary>
        public static float[] DefaultWeights()
        {
            return new float[] { 0.25f, 0.25f, 0.1f, 0.1f, 0.05f, 0.1f, 0.15f };
        }

        /// <summary>Lista de problemas del ADN sin modificarlo. Lista vacía = el ADN ya es válido.</summary>
        public static List<string> Validate(LevelDNA dna)
        {
            if (dna == null) return new List<string> { "El ADN es nulo." };
            return Repair(dna.Clone());
        }

        /// <summary>Repara el ADN en el sitio y devuelve el registro de lo que cambió (vacío = no hizo falta).</summary>
        public static List<string> Repair(LevelDNA dna)
        {
            List<string> log = new List<string>();
            if (dna == null)
            {
                log.Add("El ADN es nulo.");
                return log;
            }

            dna.nightDuration = Fix(dna.nightDuration, LevelDnaLimits.DurationMin, LevelDnaLimits.DurationMax, 240f, "nightDuration", log);
            dna.startHope = Fix(dna.startHope, LevelDnaLimits.StartHopeMin, LevelDnaLimits.StartHopeMax, 70f, "startHope", log);
            dna.hopeDecayPerSecond = Fix(dna.hopeDecayPerSecond, LevelDnaLimits.DecayMin, LevelDnaLimits.DecayMax, 0.6f, "hopeDecayPerSecond", log);
            dna.hopeRisePerSecond = Fix(dna.hopeRisePerSecond, LevelDnaLimits.RiseMin, LevelDnaLimits.RiseMax, 4f, "hopeRisePerSecond", log);

            dna.penumbraStartRadius = Fix(dna.penumbraStartRadius, LevelDnaLimits.PenumbraStartMin, LevelDnaLimits.PenumbraStartMax, 60f, "penumbraStartRadius", log);
            dna.penumbraEndRadius = Fix(dna.penumbraEndRadius, LevelDnaLimits.PenumbraEndMin, LevelDnaLimits.PenumbraEndMax, 16f, "penumbraEndRadius", log);
            // Coherencia: la Penumbra debe cerrarse de verdad (el radio final, bastante menor que el inicial).
            float maxEnd = dna.penumbraStartRadius - LevelDnaLimits.PenumbraMinClosing;
            if (dna.penumbraEndRadius > maxEnd)
            {
                float fixedEnd = Math.Max(LevelDnaLimits.PenumbraEndMin, maxEnd);
                log.Add("penumbraEndRadius " + Fmt(dna.penumbraEndRadius) + " -> " + Fmt(fixedEnd) + " (la Penumbra debe cerrarse)");
                dna.penumbraEndRadius = fixedEnd;
            }

            dna.fireflySpawnInterval = Fix(dna.fireflySpawnInterval, LevelDnaLimits.FireflyIntervalMin, LevelDnaLimits.FireflyIntervalMax, 5f, "fireflySpawnInterval", log);
            dna.fireflyMaxActive = FixInt(dna.fireflyMaxActive, LevelDnaLimits.FireflyMaxMin, LevelDnaLimits.FireflyMaxMax, "fireflyMaxActive", log);

            dna.surgeAt = Fix(dna.surgeAt, LevelDnaLimits.SurgeAtMin, LevelDnaLimits.SurgeAtMax, 0.55f, "surgeAt", log);
            dna.surgeSeconds = Fix(dna.surgeSeconds, LevelDnaLimits.SurgeSecondsMin, LevelDnaLimits.SurgeSecondsMax, 10f, "surgeSeconds", log);
            dna.surgeMeters = Fix(dna.surgeMeters, LevelDnaLimits.SurgeMetersMin, LevelDnaLimits.SurgeMetersMax, 7f, "surgeMeters", log);
            dna.surgeDecayMultiplier = Fix(dna.surgeDecayMultiplier, LevelDnaLimits.SurgeMultMin, LevelDnaLimits.SurgeMultMax, 1.7f, "surgeDecayMultiplier", log);
            // Coherencia: la ráfaga tiene que terminar antes del amanecer.
            float latestStart = (dna.nightDuration * 0.95f - dna.surgeSeconds) / dna.nightDuration;
            if (dna.surgeEnabled && dna.surgeAt > latestStart)
            {
                float fixedAt = Math.Max(0.05f, latestStart);
                log.Add("surgeAt " + Fmt(dna.surgeAt) + " -> " + Fmt(fixedAt) + " (la ráfaga debe acabar antes del alba)");
                dna.surgeAt = fixedAt;
            }

            dna.questCount = FixInt(dna.questCount, LevelDnaLimits.QuestCountMin, LevelDnaLimits.QuestCountMax, "questCount", log);
            dna.questTypeWeights = FixWeights(dna.questTypeWeights, log);

            dna.waveCount = FixInt(dna.waveCount, LevelDnaLimits.WaveCountMin, LevelDnaLimits.WaveCountMax, "waveCount", log);
            dna.creaturesPerWave = FixInt(dna.creaturesPerWave, LevelDnaLimits.CreaturesMin, LevelDnaLimits.CreaturesMax, "creaturesPerWave", log);
            dna.hopeDrainPerCreature = Fix(dna.hopeDrainPerCreature, LevelDnaLimits.DrainMin, LevelDnaLimits.DrainMax, 1.2f, "hopeDrainPerCreature", log);

            dna.heraldoAppearAt = Fix(dna.heraldoAppearAt, LevelDnaLimits.HeraldoAtMin, LevelDnaLimits.HeraldoAtMax, 0.25f, "heraldoAppearAt", log);
            dna.heraldoChaseSpeed = Fix(dna.heraldoChaseSpeed, LevelDnaLimits.HeraldoSpeedMin, LevelDnaLimits.HeraldoSpeedMax, 1.3f, "heraldoChaseSpeed", log);

            float difficulty = Fix(dna.playerDifficulty, 0f, 1f, 0.5f, "playerDifficulty", log);
            dna.playerDifficulty = difficulty;

            string tone = NormalizeTone(dna.tone);
            if (tone == null)
            {
                log.Add("tone '" + dna.tone + "' no es válido -> melancolico");
                tone = "melancolico";
            }
            dna.tone = tone;

            dna.narrativeFragments = FixFragments(dna.narrativeFragments, log);

            if (string.IsNullOrEmpty(dna.origin))
            {
                log.Add("origin vacío -> " + LevelDnaOrigin.Local);
                dna.origin = LevelDnaOrigin.Local;
            }
            return log;
        }

        // ------------------------------------------------------------------ utilidades

        private static float Fix(float value, float min, float max, float fallback, string name, List<string> log)
        {
            float result = value;
            if (float.IsNaN(value) || float.IsInfinity(value)) result = fallback;
            result = Math.Min(max, Math.Max(min, result));
            if (result != value) log.Add(name + " " + Fmt(value) + " -> " + Fmt(result) + " (rango " + Fmt(min) + "-" + Fmt(max) + ")");
            return result;
        }

        private static int FixInt(int value, int min, int max, string name, List<string> log)
        {
            int result = Math.Min(max, Math.Max(min, value));
            if (result != value) log.Add(name + " " + value + " -> " + result + " (rango " + min + "-" + max + ")");
            return result;
        }

        /// <summary>Siempre 7 pesos, sin negativos, sumando 1.</summary>
        private static float[] FixWeights(float[] weights, List<string> log)
        {
            if (weights == null || weights.Length == 0)
            {
                log.Add("questTypeWeights vacío -> pesos por defecto");
                return Normalize(DefaultWeights());
            }

            float[] result = new float[LevelDNA.QuestTypeCount];
            if (weights.Length != LevelDNA.QuestTypeCount)
            {
                log.Add("questTypeWeights tenía " + weights.Length + " valores -> " + LevelDNA.QuestTypeCount + " (se completa con ceros o se recorta)");
            }
            bool negatives = false;
            for (int i = 0; i < result.Length; i++)
            {
                float w = i < weights.Length ? weights[i] : 0f;
                if (float.IsNaN(w) || float.IsInfinity(w) || w < 0f)
                {
                    w = 0f;
                    negatives = true;
                }
                result[i] = w;
            }
            if (negatives) log.Add("questTypeWeights tenía valores negativos o inválidos -> 0");

            float sum = 0f;
            for (int i = 0; i < result.Length; i++) sum += result[i];
            if (sum <= 0f)
            {
                log.Add("questTypeWeights sumaba 0 -> pesos por defecto");
                return Normalize(DefaultWeights());
            }
            // Normalizar a suma 1 no es un error de la IA: solo se anota si la suma venía muy desviada.
            if (Math.Abs(sum - 1f) > 0.05f) log.Add("questTypeWeights sumaba " + Fmt(sum) + " -> normalizado a 1");
            return Normalize(result);
        }

        private static float[] Normalize(float[] weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++) sum += weights[i];
            float[] result = new float[weights.Length];
            for (int i = 0; i < weights.Length; i++) result[i] = sum > 0f ? weights[i] / sum : 0f;
            return result;
        }

        /// <summary>Devuelve el tono en su forma canónica (sin tildes, minúsculas) o null si no existe.</summary>
        public static string NormalizeTone(string tone)
        {
            if (string.IsNullOrEmpty(tone)) return null;
            string t = tone.Trim().ToLowerInvariant()
                .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u');
            for (int i = 0; i < Tones.Length; i++) if (Tones[i] == t) return t;
            return null;
        }

        private static string[] FixFragments(string[] fragments, List<string> log)
        {
            List<string> result = new List<string>();
            if (fragments != null)
            {
                for (int i = 0; i < fragments.Length; i++)
                {
                    string clean = SanitizeText(fragments[i]);
                    if (clean.Length == 0)
                    {
                        log.Add("fragmento narrativo " + (i + 1) + " vacío -> descartado");
                        continue;
                    }
                    if (result.Count >= LevelDnaLimits.MaxFragments)
                    {
                        log.Add("fragmento narrativo " + (i + 1) + " sobra (máx. " + LevelDnaLimits.MaxFragments + ") -> descartado");
                        continue;
                    }
                    if (clean != fragments[i]) log.Add("fragmento narrativo " + (i + 1) + " saneado");
                    result.Add(clean);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Limpia texto antes de mostrarlo en TextMeshPro: quita etiquetas (&lt;b&gt;, &lt;color&gt;...),
        /// caracteres de control y corta a un largo razonable.
        /// </summary>
        public static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string clean = Regex.Replace(text, "<[^>]*>", "");
            clean = clean.Replace("<", "").Replace(">", "");
            System.Text.StringBuilder sb = new System.Text.StringBuilder(clean.Length);
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                if (char.IsControl(c)) c = ' ';
                sb.Append(c);
            }
            clean = Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
            if (clean.Length > LevelDnaLimits.MaxFragmentLength) clean = clean.Substring(0, LevelDnaLimits.MaxFragmentLength).TrimEnd();
            return clean;
        }

        private static string Fmt(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
