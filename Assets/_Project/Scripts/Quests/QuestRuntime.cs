namespace Ascua.Quests
{
    /// <summary>
    /// Estado y progreso de una misión durante una noche concreta.
    /// Separado del asset Quest para que jugar nunca modifique los datos de diseño.
    /// </summary>
    public class QuestRuntime
    {
        public readonly Quest Data;
        public QuestState State;
        /// <summary>Segundos acumulados (Rescate) o segundos lejos del NPC (Escolta).</summary>
        public float Timer;
        /// <summary>Luciérnagas recogidas (Recolección).</summary>
        public int Count;
        /// <summary>0-1 para barras de progreso.</summary>
        public float Progress01;
        /// <summary>Texto corto de progreso para el panel de misiones.</summary>
        public string ProgressText = "";
        /// <summary>Motivo del fallo, si lo hay.</summary>
        public string FailReason = "";

        // ---- Valores efectivos (los del asset + las consecuencias de decisiones anteriores)
        public float EffThreshold;
        public float EffHoldSeconds;
        public float EffTimeLimit;
        public float EffEnergyBudget;
        /// <summary>Frases de las consecuencias aplicadas (se muestran al ofrecer la misión).</summary>
        public string ConditionNote = "";

        // ---- Progreso de los tipos nuevos
        /// <summary>Segundos desde que se aceptó (límite de tiempo del doble objetivo).</summary>
        public float Elapsed;
        /// <summary>Sombras del objetivo B disueltas.</summary>
        public int Defeated;
        /// <summary>Total de sombras del objetivo B.</summary>
        public int DefeatTarget;
        /// <summary>Segundos seguidos con la zona A bajo el umbral.</summary>
        public float BelowTimer;
        /// <summary>Energía gastada desde que se aceptó (presupuesto).</summary>
        public float EnergySpent;
        /// <summary>Etiqueta de las sombras invocadas para esta misión.</summary>
        public string CreatureTag;

        public QuestRuntime(Quest data, QuestState initialState)
        {
            Data = data;
            State = initialState;
        }

        public bool IsOpen => State == QuestState.Disponible || State == QuestState.Activa || State == QuestState.PorEntregar;
    }
}
