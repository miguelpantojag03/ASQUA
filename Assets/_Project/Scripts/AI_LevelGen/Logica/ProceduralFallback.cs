using System;
using System.Collections.Generic;

namespace Ascua.LevelGen
{
    /// <summary>
    /// Generador de ADN 100 % offline (reglas, sin IA ni red). Parte del ADN anterior (o de BASE_DNA si no hay),
    /// y lo empuja hacia "más difícil" si al jugador le va bien o hacia "más fácil" si le va mal,
    /// con una pequeña variación sembrada. Misma semilla + mismas entradas = mismo nivel.
    /// NUNCA modifica los ADN que recibe: trabaja siempre sobre copias.
    /// Se usa cuando no hay banco de IA disponible, falla el JSON o no hay red.
    /// </summary>
    public static class ProceduralFallback
    {
        /// <summary>
        /// Genera el ADN de una noche.
        /// baseDna: el ADN de la Noche 1 (referencia, no se toca). previous: ADN de la noche anterior (puede ser null).
        /// playerSkill: 0 = le va mal, 1 = le va muy bien.
        /// </summary>
        public static LevelDNA Generate(LevelDNA baseDna, LevelDNA previous, float playerSkill, int nightNumber, int seed, List<string> log)
        {
            if (baseDna == null) throw new ArgumentNullException("baseDna");
            if (log == null) log = new List<string>();

            LevelDNA b = baseDna.Clone();
            LevelDNA start = (previous != null ? previous : baseDna).Clone();
            Random rng = new Random(seed);

            float skill = Clamp01(playerSkill);
            LevelDNA toward;
            float step;
            if (skill >= 0.5f)
            {
                step = 0.12f + 0.18f * ((skill - 0.5f) * 2f);   // 0.12 - 0.30 hacia lo difícil
                toward = Extreme(b, 1f);
            }
            else
            {
                step = 0.20f * ((0.5f - skill) * 2f);           // 0 - 0.20 hacia lo fácil
                toward = Extreme(b, -1f);
            }

            LevelDNA r = Blend(start, toward, step);

            // Variación sembrada (±6 %): que dos partidas no sean idénticas, pero sin saltos bruscos.
            r.nightDuration = Jitter(r.nightDuration, rng, 0.06f);
            r.startHope = Jitter(r.startHope, rng, 0.06f);
            r.hopeDecayPerSecond = Jitter(r.hopeDecayPerSecond, rng, 0.06f);
            r.fireflySpawnInterval = Jitter(r.fireflySpawnInterval, rng, 0.06f);
            r.surgeSeconds = Jitter(r.surgeSeconds, rng, 0.06f);
            r.hopeDrainPerCreature = Jitter(r.hopeDrainPerCreature, rng, 0.06f);

            // Valores enteros (oleadas, criaturas, luciérnagas, misiones): se derivan de la dificultad y no
            // se acumulan noche a noche, porque redondear en cadena los dejaría atascados.
            float signed = SignedHardness(r, b);
            float hardness = Hardness(r, b);
            r.waveCount = (int)Math.Round(b.waveCount + signed * 0.8f);
            r.creaturesPerWave = (int)Math.Round(b.creaturesPerWave + signed * 1.2f);
            r.fireflyMaxActive = (int)Math.Round(b.fireflyMaxActive * (1f - 0.3f * signed));
            r.questCount = 3 + (int)Math.Round(hardness * 2f) + rng.Next(0, 2);
            r.seed = seed;
            r.origin = LevelDnaOrigin.Local;
            r.playerDifficulty = skill;
            r.tone = ToneFor(hardness, skill);
            r.narrativeFragments = PickFragments(r.tone, rng);

            // La red de seguridad: pase lo que pase, el resultado sale dentro de los rangos.
            log.AddRange(LevelValidator.Repair(r));
            return r;
        }

        /// <summary>Como Hardness pero con signo: de -0.5 (más fácil que la base) a 1 (el extremo difícil).</summary>
        public static float SignedHardness(LevelDNA r, LevelDNA b)
        {
            if (b.hopeDecayPerSecond <= 0f) return 0f;
            float v = (r.hopeDecayPerSecond - b.hopeDecayPerSecond) / (b.hopeDecayPerSecond * 0.9f);
            return v < -0.5f ? -0.5f : (v > 1f ? 1f : v);
        }

        /// <summary>Qué tan difícil es r respecto a la base, de 0 (como la base o más fácil) a 1 (el extremo difícil).</summary>
        public static float Hardness(LevelDNA r, LevelDNA b)
        {
            if (b.hopeDecayPerSecond <= 0f) return 0f;
            return Clamp01((r.hopeDecayPerSecond - b.hopeDecayPerSecond) / (b.hopeDecayPerSecond * 0.9f));
        }

        /// <summary>Tono emocional según lo difícil que es la noche y cómo le va al jugador.</summary>
        public static string ToneFor(float hardness, float skill)
        {
            if (hardness < 0.2f && skill < 0.5f) return "esperanzado";
            if (hardness < 0.35f) return "melancolico";
            if (hardness < 0.7f) return "tenso";
            return "climatico";
        }

        // ------------------------------------------------------------------ extremos y mezcla

        /// <summary>Factor para empujar un parámetro: lo difícil (s > 0) empuja el doble que lo fácil.</summary>
        private static float F(float s, float k)
        {
            return s >= 0f ? 1f + s * k : 1f + s * k * 0.5f;
        }

        /// <summary>
        /// El ADN "más difícil" (s = 1) o "más fácil" (s = -1) que se puede llegar a pedir a partir de la base.
        /// El validador recorta cualquier exceso, así que estos números solo marcan la dirección.
        /// </summary>
        private static LevelDNA Extreme(LevelDNA b, float s)
        {
            LevelDNA e = b.Clone();
            e.nightDuration = b.nightDuration * F(s, 0.25f);
            e.startHope = b.startHope * F(s, -0.30f);
            e.hopeDecayPerSecond = b.hopeDecayPerSecond * F(s, 0.9f);
            e.hopeRisePerSecond = b.hopeRisePerSecond * F(s, -0.2f);
            e.penumbraEndRadius = b.penumbraEndRadius * F(s, -0.4f);
            e.fireflySpawnInterval = b.fireflySpawnInterval * F(s, 0.5f);
            e.fireflyMaxActive = (int)Math.Round(b.fireflyMaxActive * F(s, -0.4f));
            e.surgeAt = b.surgeAt * F(s, -0.2f);
            e.surgeSeconds = b.surgeSeconds * F(s, 0.5f);
            e.surgeMeters = b.surgeMeters * F(s, 0.3f);
            e.surgeDecayMultiplier = b.surgeDecayMultiplier * F(s, 0.3f);
            e.waveCount = (int)Math.Round(b.waveCount + s);
            e.creaturesPerWave = (int)Math.Round(b.creaturesPerWave + s * 2f);
            e.hopeDrainPerCreature = b.hopeDrainPerCreature * F(s, 0.6f);
            e.heraldoAppearAt = b.heraldoAppearAt * F(s, -0.4f);
            e.heraldoChaseSpeed = b.heraldoChaseSpeed * F(s, 0.4f);

            float[] w = SafeWeights(b.questTypeWeights);
            if (s > 0f)
            {
                w[6] += 0.3f;   // más Doble objetivo
                w[5] += 0.2f;   // más Entrega
            }
            else
            {
                w[0] += 0.2f;   // más Rescate de Esperanza
                w[1] += 0.2f;   // más Recolección
            }
            e.questTypeWeights = w;
            return e;
        }

        private static LevelDNA Blend(LevelDNA a, LevelDNA b, float t)
        {
            LevelDNA r = a.Clone();
            r.nightDuration = Lerp(a.nightDuration, b.nightDuration, t);
            r.startHope = Lerp(a.startHope, b.startHope, t);
            r.hopeDecayPerSecond = Lerp(a.hopeDecayPerSecond, b.hopeDecayPerSecond, t);
            r.hopeRisePerSecond = Lerp(a.hopeRisePerSecond, b.hopeRisePerSecond, t);
            r.penumbraStartRadius = Lerp(a.penumbraStartRadius, b.penumbraStartRadius, t);
            r.penumbraEndRadius = Lerp(a.penumbraEndRadius, b.penumbraEndRadius, t);
            r.fireflySpawnInterval = Lerp(a.fireflySpawnInterval, b.fireflySpawnInterval, t);
            r.surgeAt = Lerp(a.surgeAt, b.surgeAt, t);
            r.surgeSeconds = Lerp(a.surgeSeconds, b.surgeSeconds, t);
            r.surgeMeters = Lerp(a.surgeMeters, b.surgeMeters, t);
            r.surgeDecayMultiplier = Lerp(a.surgeDecayMultiplier, b.surgeDecayMultiplier, t);
            r.hopeDrainPerCreature = Lerp(a.hopeDrainPerCreature, b.hopeDrainPerCreature, t);
            r.heraldoAppearAt = Lerp(a.heraldoAppearAt, b.heraldoAppearAt, t);
            r.heraldoChaseSpeed = Lerp(a.heraldoChaseSpeed, b.heraldoChaseSpeed, t);

            float[] wa = SafeWeights(a.questTypeWeights);
            float[] wb = SafeWeights(b.questTypeWeights);
            float[] w = new float[LevelDNA.QuestTypeCount];
            for (int i = 0; i < w.Length; i++) w[i] = Lerp(wa[i], wb[i], t);
            r.questTypeWeights = w;
            return r;
        }

        /// <summary>Copia de 7 pesos (rellena o recorta si hace falta). Nunca devuelve null.</summary>
        private static float[] SafeWeights(float[] source)
        {
            float[] w = new float[LevelDNA.QuestTypeCount];
            if (source == null) return LevelValidator.DefaultWeights();
            for (int i = 0; i < w.Length; i++) w[i] = i < source.Length ? source[i] : 0f;
            return w;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        private static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0.5f;
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        private static float Jitter(float value, Random rng, float amount)
        {
            double unit = rng.NextDouble() * 2.0 - 1.0;   // -1..1
            return value * (1f + (float)unit * amount);
        }

        // ------------------------------------------------------------------ textos

        private static readonly Dictionary<string, string[]> FragmentBank = new Dictionary<string, string[]>
        {
            { "esperanzado", new[] { "Hoy las ventanas se acuerdan solas", "Alguien dijo un nombre en voz alta", "El mar trae buenas noticias", "Una luz llama a otra luz" } },
            { "melancolico", new[] { "Lo que aún respira", "La ciudad guarda silencio", "Una luz por cada nombre", "Los cajones también recuerdan" } },
            { "tenso", new[] { "La niebla llega antes de lo previsto", "Nadie duerme en Vesper", "El viento trae un susurro", "Cada ventana cuenta" } },
            { "climatico", new[] { "La última noche larga", "Todo lo que se recuerda arde", "Hoy no se apaga nadie", "El Faro no mira hacia otro lado" } }
        };

        private static string[] PickFragments(string tone, Random rng)
        {
            string[] bank;
            if (!FragmentBank.TryGetValue(tone, out bank)) bank = FragmentBank["melancolico"];
            List<string> pool = new List<string>(bank);
            List<string> picked = new List<string>();
            while (picked.Count < 3 && pool.Count > 0)
            {
                int index = rng.Next(pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked.ToArray();
        }
    }
}
