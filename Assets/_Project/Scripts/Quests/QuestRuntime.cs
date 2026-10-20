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

        public QuestRuntime(Quest data, QuestState initialState)
        {
            Data = data;
            State = initialState;
        }

        public bool IsOpen => State == QuestState.Disponible || State == QuestState.Activa || State == QuestState.PorEntregar;
    }
}
