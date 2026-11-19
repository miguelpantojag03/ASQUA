using System.Collections;
using Ascua.Characters;
using Ascua.Core;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Noche 4 · "El nombre" (la revelación: el Heraldo es Corvin, handoff §3.4).
    /// El Heraldo deja de perseguir, se detiene a ~6 m de Ilse, gira su farol dos veces y habla.
    /// Diálogo creado por código con 3 opciones:
    ///  - "Corvin."           → marca "nombro_corvin", +10 de energía, +1 de Vínculo con el personaje más
    ///                          cercano, aviso, y su farol se vuelve dorado desde ese momento.
    ///  - "No sé quién eres." / "Aléjate." → sin marca.
    /// La marca la exige el final Constelación (EndingResolver.RequireCorvinName).
    /// </summary>
    public partial class NightEventDirector
    {
        private const float NombreStopDistance = 6f;
        private const float NombreEnergy = 10f;

        partial void BeginNombre()
        {
            StartCoroutine(NombreRoutine());
        }

        private IEnumerator NombreRoutine()
        {
            Heraldo_AI heraldo = Heraldo_AI.Instance;
            DialogueRunner runner = DialogueRunner.Instance;
            if (heraldo == null || runner == null || player == null)
            {
                FinishEvent();
                yield break;
            }

            // Si aún no había aparecido esta noche, aparece ya.
            if (!heraldo.IsPresent) heraldo.ForceAppear();
            yield return null;

            if (!heraldo.StopBefore(NombreStopDistance, 60f))
            {
                FinishEvent();
                yield break;
            }

            // Gira su farolillo dos veces (Ilse lo reconoce) y, después, habla.
            heraldo.TwirlLantern();
            float wait = 2.2f;
            while (wait > 0f)
            {
                wait -= Time.deltaTime;   // la pausa lo congela
                yield return null;
            }
            while (runner.IsOpen) yield return null;

            bool namedBefore = GameProgress.HasFlag(EndingContext.NamedCorvinFlag);
            NpcInteractable heraldoNpc = heraldo.GetComponent<NpcInteractable>();
            runner.Play(BuildNombreDialogue(), heraldoNpc, null, player);
            // El diálogo para el tiempo (timeScale 0): esta espera avanza igual, frame a frame.
            while (runner.IsOpen) yield return null;

            bool named = GameProgress.HasFlag(EndingContext.NamedCorvinFlag);
            if (named)
            {
                heraldo.SetGoldenLantern();
                if (!namedBefore) BondWithNearestCompanion();
            }
            // Se aleja despacio; esta noche ya no insiste tanto.
            heraldo.RetreatFor(named ? 45f : 25f);
            FinishEvent();
        }

        /// <summary>Diálogo de la revelación. La elección "Corvin." deja la marca, la energía y el aviso.</summary>
        private static DialogueSequence BuildNombreDialogue()
        {
            DialogueSequence dialogue = ScriptableObject.CreateInstance<DialogueSequence>();
            dialogue.name = "D_Noche4_ElNombre (runtime)";
            dialogue.dialogueId = NombreId;
            dialogue.lines.Add(new DialogueLine("El Heraldo", "No corras. Esta noche no vengo a apagar nada."));
            dialogue.lines.Add(new DialogueLine("Ilse", "Ese farolillo... lo giraban dos veces antes de encenderlo."));
            dialogue.lines.Add(new DialogueLine("El Heraldo", "¿Cómo sabes eso? Nadie se acuerda. Nadie se acuerda de mí."));
            dialogue.lines.Add(new DialogueLine("Ilse", "Yo sí. Creo que yo sí."));

            DialogueChoice sayName = new DialogueChoice("Corvin.", ChoiceAction.Ninguna)
            {
                energyDelta = NombreEnergy,
                toast = "Dijiste su nombre en voz alta.",
                setFlag = EndingContext.NamedCorvinFlag
            };
            dialogue.choices.Add(sayName);
            dialogue.choices.Add(new DialogueChoice("No sé quién eres.", ChoiceAction.Ninguna));
            dialogue.choices.Add(new DialogueChoice("Aléjate.", ChoiceAction.Ninguna));
            return dialogue;
        }

        /// <summary>+1 de Vínculo con Mina, Teo o Perpetua: quien esté más cerca de Ilse.</summary>
        private void BondWithNearestCompanion()
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null || player == null) return;

            NpcInteractable best = null;
            float bestDistance = float.MaxValue;
            var npcs = NpcInteractable.All;
            for (int i = 0; i < npcs.Count; i++)
            {
                NpcInteractable npc = npcs[i];
                if (npc == null) continue;
                string id = npc.NpcId;
                if (id != EndingContext.MinaId && id != EndingContext.TeoId && id != EndingContext.PerpetuaId) continue;
                float d = FlatDistance(npc.transform.position, player.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = npc;
                }
            }
            if (best != null) manager.AddBond(best.NpcId, 1);
        }
    }
}
