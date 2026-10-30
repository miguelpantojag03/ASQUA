using System.Collections.Generic;
using Ascua.Combat;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.AILevelGen
{
    /// <summary>
    /// Convierte un ADN validado en un NightQuestSet jugable. Solo usa lo que ya existe: las misiones
    /// del juego (nunca inventa misiones, escenas ni geometría) y los campos de dificultad del set.
    /// Es determinista: el mismo ADN y las mismas misiones dan siempre el mismo nivel.
    /// </summary>
    public static class QuestSetBuilder
    {
        // Orden en que se reparte la mezcla de criaturas de una oleada.
        private enum Creature { Moth, Echo, Whisper, VeiledBrute }

        private static readonly Creature[] CompositionCycle =
        {
            Creature.Moth, Creature.Echo, Creature.Moth, Creature.Whisper, Creature.Moth, Creature.VeiledBrute
        };

        /// <summary>
        /// Crea el NightQuestSet de la noche. pool = misiones existentes de las que elegir;
        /// template = una noche diseñada a mano (se usa para saber qué zonas existen).
        /// </summary>
        public static NightQuestSet Build(LevelDNA dna, int night, IList<Quest> pool, NightQuestSet template)
        {
            NightQuestSet set = ScriptableObject.CreateInstance<NightQuestSet>();
            // Que Unity no lo descargue al cambiar de escena (la noche siguiente reutiliza el mismo objeto).
            set.hideFlags = HideFlags.DontUnloadUnusedAsset;
            set.name = "Noche_" + night.ToString("00") + "_generada";

            set.setId = "gen_n" + night + "_s" + dna.seed;
            set.nightNumber = night;
            string subtitle = dna.narrativeFragments != null && dna.narrativeFragments.Length > 0 ? dna.narrativeFragments[0] : "";
            set.title = "Noche " + night + (subtitle.Length > 0 ? " · " + subtitle : "");
            set.designNotes = "Nivel generado a partir del ADN de la Noche 1 (origen: " + dna.origin + ", semilla " + dna.seed + "). Tono: "
                              + dna.tone + ". " + string.Join(" / ", dna.narrativeFragments ?? new string[0]);

            set.nightDuration = dna.nightDuration;
            set.startHope = dna.startHope;
            set.hopeDecayPerSecond = dna.hopeDecayPerSecond;
            set.hopeRisePerSecond = dna.hopeRisePerSecond;
            set.penumbraStartRadius = dna.penumbraStartRadius;
            set.penumbraEndRadius = dna.penumbraEndRadius;
            set.fastPenumbraCurve = dna.fastPenumbraCurve;
            set.fireflySpawnInterval = dna.fireflySpawnInterval;
            set.fireflyMaxActive = dna.fireflyMaxActive;
            set.surgeEnabled = dna.surgeEnabled;
            set.surgeAt = dna.surgeAt;
            set.surgeSeconds = dna.surgeSeconds;
            set.surgeMeters = dna.surgeMeters;
            set.surgeDecayMultiplier = dna.surgeDecayMultiplier;
            set.heraldoEnabled = dna.heraldoEnabled;
            set.heraldoAppearAt = dna.heraldoAppearAt;
            set.heraldoChaseSpeed = dna.heraldoChaseSpeed;
            // La dificultad ya viene escalada en el propio ADN: el set no la vuelve a subir por noche.
            set.decayIncreasePerNight = 0f;

            System.Random rng = new System.Random(dna.seed ^ 0x5EED);
            set.quests = PickQuests(dna, night, pool, rng);
            set.waves = BuildWaves(dna, template, rng);
            return set;
        }

        // ------------------------------------------------------------------ misiones

        private static List<Quest> PickQuests(LevelDNA dna, int night, IList<Quest> pool, System.Random rng)
        {
            List<Quest> fresh = new List<Quest>();        // originales que aún no se han cumplido
            List<Quest> replayable = new List<Quest>();   // ya cumplidas, pero se pueden repetir en versión nueva
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; pool != null && i < pool.Count; i++)
            {
                Quest quest = pool[i];
                if (quest == null || !seen.Add(quest.questId)) continue;
                if (!GameProgress.IsQuestCompleted(quest.questId)) fresh.Add(quest);
                else if (IsReplayable(quest)) replayable.Add(quest);
            }

            List<Quest> chosen = new List<Quest>();
            HashSet<string> ids = new HashSet<string>();
            int guard = 0;
            while (chosen.Count < dna.questCount && guard++ < 64)
            {
                if (fresh.Count > 0)
                {
                    Quest pick = TakeWeighted(fresh, dna.questTypeWeights, rng);
                    AddWithPrerequisites(pick, chosen, ids, fresh, 0);
                }
                else if (replayable.Count > 0)
                {
                    Quest source = TakeWeighted(replayable, dna.questTypeWeights, rng);
                    chosen.Add(CloneForNight(source, night));
                }
                else break;
            }
            return chosen;
        }

        /// <summary>Solo las misiones que no dependen de un momento de la historia se pueden repetir.</summary>
        private static bool IsReplayable(Quest quest)
        {
            return quest.type == QuestType.RescateEsperanza
                || quest.type == QuestType.Recoleccion
                || quest.type == QuestType.DobleObjetivo;
        }

        /// <summary>Saca de la lista una misión al azar, con más probabilidad para los tipos de más peso.</summary>
        private static Quest TakeWeighted(List<Quest> list, float[] weights, System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += WeightOf(list[i], weights);
            double roll = rng.NextDouble() * total;
            int index = list.Count - 1;
            for (int i = 0; i < list.Count; i++)
            {
                roll -= WeightOf(list[i], weights);
                if (roll <= 0.0)
                {
                    index = i;
                    break;
                }
            }
            Quest result = list[index];
            list.RemoveAt(index);
            return result;
        }

        /// <summary>Peso del tipo de la misión (con un mínimo para que ningún tipo desaparezca del todo).</summary>
        private static float WeightOf(Quest quest, float[] weights)
        {
            int index = (int)quest.type;
            float w = weights != null && index >= 0 && index < weights.Length ? weights[index] : 0f;
            return w + 0.02f;
        }

        /// <summary>Añade la misión y, antes, la que necesita cumplida (cadenas como Brasa -> Malecón).</summary>
        private static void AddWithPrerequisites(Quest quest, List<Quest> chosen, HashSet<string> ids, List<Quest> fresh, int depth)
        {
            if (quest == null || ids.Contains(quest.questId) || depth > 4) return;
            Quest requirement = quest.prerequisite;
            if (requirement != null && !GameProgress.IsQuestCompleted(requirement.questId))
            {
                fresh.Remove(requirement);
                AddWithPrerequisites(requirement, chosen, ids, fresh, depth + 1);
            }
            ids.Add(quest.questId);
            chosen.Add(quest);
        }

        /// <summary>Copia de una misión ya cumplida con otro id, para poder repetirla esta noche.</summary>
        private static Quest CloneForNight(Quest source, int night)
        {
            Quest clone = Object.Instantiate(source);
            clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
            clone.name = source.name + "_n" + night;
            clone.questId = source.questId + "_n" + night;
            clone.prerequisite = null;
            clone.rewardDiaryPage = null;   // la página del Diario se gana una sola vez
            return clone;
        }

        // ------------------------------------------------------------------ oleadas

        private static List<CombatWave> BuildWaves(LevelDNA dna, NightQuestSet template, System.Random rng)
        {
            List<string> zones = new List<string>();
            for (int i = 0; template != null && template.waves != null && i < template.waves.Count; i++)
            {
                CombatWave wave = template.waves[i];
                if (wave != null && !string.IsNullOrEmpty(wave.zoneId) && !zones.Contains(wave.zoneId)) zones.Add(wave.zoneId);
            }
            if (zones.Count == 0) zones.Add("Plaza");

            List<CombatWave> waves = new List<CombatWave>();
            for (int i = 0; i < dna.waveCount; i++)
            {
                float t = dna.waveCount <= 1 ? 0.5f : (float)i / (dna.waveCount - 1);
                float at = Mathf.Lerp(0.28f, 0.72f, t) + ((float)rng.NextDouble() - 0.5f) * 0.06f;

                CombatWave wave = new CombatWave();
                wave.zoneId = zones[i % zones.Count];
                wave.label = "Oleada " + (i + 1) + " · " + wave.zoneId;
                wave.atProgress = Mathf.Clamp(at, 0.2f, 0.85f);
                wave.hopeDrainPerCreature = dna.hopeDrainPerCreature;
                wave.moths = 0;
                wave.echoes = 0;
                wave.whispers = 0;
                wave.brutes = 0;
                wave.veiledBrutes = 0;
                for (int c = 0; c < dna.creaturesPerWave; c++)
                {
                    switch (CompositionCycle[c % CompositionCycle.Length])
                    {
                        case Creature.Moth: wave.moths++; break;
                        case Creature.Echo: wave.echoes++; break;
                        case Creature.Whisper: wave.whispers++; break;
                        default: wave.veiledBrutes++; break;
                    }
                }
                waves.Add(wave);
            }
            return waves;
        }
    }
}
