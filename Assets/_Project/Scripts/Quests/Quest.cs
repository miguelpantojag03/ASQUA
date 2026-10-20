using Ascua.Narrative;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>Los tipos de misión reutilizables. Una misión nueva casi nunca necesita código: se elige un tipo y se rellenan sus campos.</summary>
    public enum QuestType
    {
        /// <summary>Mantener la Esperanza de una zona por encima de X durante Y segundos.</summary>
        RescateEsperanza,
        /// <summary>Recoger N luciérnagas (en una zona o en cualquier sitio) antes de que acabe la noche.</summary>
        Recoleccion,
        /// <summary>Acompañar a un NPC hasta un punto sin alejarse ni dejar que la Penumbra lo alcance.</summary>
        Escolta,
        /// <summary>Hablar y elegir: la elección completa la misión y aplica sus efectos.</summary>
        Dialogo,
        /// <summary>"Misión inversa" del Heraldo: aceptar da un beneficio inmediato y penaliza otra zona.</summary>
        TratoHeraldo,
        /// <summary>"Lleva X a Y": recoger un objeto (PickupItem) y entregárselo a un personaje.</summary>
        Entrega
    }

    /// <summary>Estado de una misión durante la noche (vive en QuestManager, nunca en el asset).</summary>
    public enum QuestState
    {
        /// <summary>Aún no se cumple su requisito (cadena de misiones).</summary>
        Bloqueada,
        Disponible,
        Activa,
        /// <summary>Objetivo cumplido: falta hablar con quien la dio para recibir la recompensa.</summary>
        PorEntregar,
        Completada,
        Fallida,
        /// <summary>La noche terminó sin completarla.</summary>
        Expirada
    }

    /// <summary>
    /// Definición de una misión (asset de datos). El progreso y el estado los lleva QuestManager,
    /// así el asset nunca se modifica al jugar. Crear: Create > Ascua > Misión.
    /// </summary>
    [CreateAssetMenu(fileName = "Q_NuevaMision", menuName = "Ascua/Misión", order = 0)]
    public class Quest : ScriptableObject
    {
        [Header("Identidad")]
        public string questId = "nueva_mision";
        [Tooltip("Id del NPC que la ofrece y la recibe (NpcIdentity): mina, teo, heraldo...")]
        public string giverNpcId = "mina";
        public string title = "Nueva misión";
        [TextArea(2, 4)] public string description;
        public QuestType type = QuestType.Recoleccion;
        [Tooltip("UNA frase, UNA acción. Es lo que se ve siempre en el HUD mientras la misión está activa.")]
        public string shortInstruction;
        [Tooltip("Misión que debe estar completada antes de que esta aparezca (cadenas). Vacío = disponible desde el inicio.")]
        public Quest prerequisite;

        [Header("Objetivo · Rescate de Esperanza (y zona de Recolección)")]
        [Tooltip("Zona objetivo (Plaza, Mercado...).")]
        public string targetZoneId = "Plaza";
        [Range(0f, 100f)] public float hopeThreshold = 50f;
        public float holdSeconds = 20f;

        [Header("Objetivo · Recolección")]
        public int requiredCount = 3;
        [Tooltip("Solo cuentan las luciérnagas recogidas dentro de la zona objetivo (radio de la zona x este factor). 0 = en cualquier sitio.")]
        public float collectRadiusMultiplier = 1.8f;

        [Header("Objetivo · Escolta")]
        public string escortNpcId = "mina";
        [Tooltip("Id de un QuestPoint de la escena (destino del NPC).")]
        public string escortDestinationId;
        [Tooltip("Distancia máxima jugador-NPC.")]
        public float escortMaxDistance = 8f;
        [Tooltip("Segundos que se tolera estar lejos antes de fallar.")]
        public float escortGraceSeconds = 6f;

        [Header("Objetivo · Entrega (Lleva X a Y)")]
        [Tooltip("Id del PickupItem que hay que recoger.")]
        public string deliverItemId;
        [Tooltip("Nombre del objeto tal cual se muestra.")]
        public string deliverItemName;
        [Tooltip("A quién se entrega (id de NpcIdentity). Vacío = a quien dio la misión.")]
        public string deliverToNpcId;

        [Header("Recompensa")]
        public float rewardEnergy;
        public float rewardHope;
        public string rewardHopeZoneId;
        [Tooltip("Vínculo que gana el NPC que la dio (0-5 en total).")]
        public int rewardBond = 1;

        [Header("Penalización (Tratos del Heraldo)")]
        public float penaltyHope;
        public string penaltyZoneId;

        [Header("Fallo")]
        [Tooltip("La misión falla si su zona objetivo muere.")]
        public bool failIfTargetZoneDies = true;

        [Header("Diálogos")]
        [Tooltip("Se muestra cuando está Disponible. Debe terminar con elecciones (Aceptar / Ahora no, o las de un Trato).")]
        public DialogueSequence offerDialogue;
        [Tooltip("Se muestra si se habla con el NPC mientras está Activa.")]
        public DialogueSequence inProgressDialogue;
        [Tooltip("Al entregarla (o justo después de elegir, en Diálogo/Trato).")]
        public DialogueSequence completedDialogue;

        /// <summary>Diálogo y Trato se completan en el momento de elegir, sin objetivo en el mundo.</summary>
        public bool CompletesOnChoice => type == QuestType.Dialogo || type == QuestType.TratoHeraldo;

        /// <summary>Con quién hay que hablar para entregarla.</summary>
        public string TurnInNpcId => type == QuestType.Entrega && !string.IsNullOrEmpty(deliverToNpcId) ? deliverToNpcId : giverNpcId;
    }
}
