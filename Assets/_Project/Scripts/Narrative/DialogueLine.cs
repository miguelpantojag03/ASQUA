using System;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>Una línea de diálogo: quién habla y qué dice.</summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("Nombre tal cual se muestra: Mina, Teo, Ilse, El Heraldo...")]
        public string speaker;
        [TextArea(2, 5)] public string text;

        public DialogueLine() { }

        public DialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    /// <summary>Qué hace una elección respecto a la misión que se está ofreciendo.</summary>
    public enum ChoiceAction
    {
        /// <summary>Solo aplica sus efectos (si tiene) y cierra.</summary>
        Ninguna,
        /// <summary>Acepta la misión ofrecida.</summary>
        AceptarMision,
        /// <summary>La rechaza por ahora (sigue disponible).</summary>
        RechazarMision,
        /// <summary>Completa la misión al elegir (misiones de diálogo y Tratos del Heraldo).</summary>
        CompletarMision
    }

    /// <summary>Opción que el jugador puede elegir al final de un diálogo, con sus efectos.</summary>
    [Serializable]
    public class DialogueChoice
    {
        public string label;
        public ChoiceAction action = ChoiceAction.Ninguna;

        [Header("Efectos de esta elección (además de la recompensa de la misión)")]
        [Tooltip("Esperanza que suma (o resta, si es negativa) a la zona indicada.")]
        public float hopeDelta;
        public string hopeZoneId;
        public float energyDelta;
        public int bondDelta;
        [Tooltip("Aviso en pantalla opcional al elegir.")]
        public string toast;
        [Tooltip("Marca que deja esta decisión para noches futuras (consecuencia tardía). Vacío = ninguna.")]
        public string setFlag;

        public DialogueChoice() { }

        public DialogueChoice(string label, ChoiceAction action)
        {
            this.label = label;
            this.action = action;
        }
    }
}
