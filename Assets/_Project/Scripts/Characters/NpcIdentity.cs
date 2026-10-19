using UnityEngine;

namespace Ascua.Characters
{
    /// <summary>
    /// Identidad mínima de un personaje no jugable. En la Fase 2 el sistema de diálogos
    /// y Vínculos buscará NPCs por NpcId; el color de acento define su firma visual.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcIdentity : MonoBehaviour
    {
        [SerializeField] private string npcId = "mina";
        [SerializeField] private string displayName = "Mina";
        [SerializeField] private Color accentColor = new Color(0.902f, 0.224f, 0.275f);

        public string NpcId => npcId;
        public string DisplayName => displayName;
        public Color AccentColor => accentColor;

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(string id, string nameToShow, Color accent)
        {
            npcId = id;
            displayName = nameToShow;
            accentColor = accent;
        }
    }
}
