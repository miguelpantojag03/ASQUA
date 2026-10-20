using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Una página del Diario de Ilse (asset). Se encuentran como PickupItem de tipo PaginaDiario y
    /// se leen en el Codex. Crear: Create > Ascua > Página del Diario.
    /// </summary>
    [CreateAssetMenu(fileName = "P_NuevaPagina", menuName = "Ascua/Página del Diario", order = 3)]
    public class DiaryPage : ScriptableObject
    {
        public string pageId = "pagina_00";
        [Tooltip("Orden en el Diario (1-12).")]
        public int number = 1;
        public string title = "Sin título";
        [TextArea(4, 10)] public string text;
    }
}
