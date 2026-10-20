using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Conversación completa (asset): una lista de líneas y, opcionalmente, elecciones al final.
    /// Crear: clic derecho en Project > Create > Ascua > Diálogo.
    /// </summary>
    [CreateAssetMenu(fileName = "D_NuevoDialogo", menuName = "Ascua/Diálogo", order = 1)]
    public class DialogueSequence : ScriptableObject
    {
        public string dialogueId = "nuevo_dialogo";
        public List<DialogueLine> lines = new List<DialogueLine>();
        [Tooltip("Si hay elecciones, se muestran tras la última línea. Si no, el diálogo se cierra.")]
        public List<DialogueChoice> choices = new List<DialogueChoice>();

        public bool HasChoices => choices != null && choices.Count > 0;
    }
}
