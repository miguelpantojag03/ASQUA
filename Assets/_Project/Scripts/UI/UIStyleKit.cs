using UnityEngine;

namespace Ascua.UI
{
    /// <summary>Papel de un elemento en el sistema de diseño (define fondo, marco y énfasis).</summary>
    public enum UIRole
    {
        /// <summary>Bloques fijos del HUD (energía, noche, Esperanza, objetivo).</summary>
        Hud,
        /// <summary>Paneles grandes que se abren (misiones, accesibilidad, índice del Diario).</summary>
        Panel,
        /// <summary>Caja de diálogo: el marco más visible, en dorado.</summary>
        Dialogue,
        /// <summary>Avisos pequeños (interacción, subtítulos, controles del modo foto).</summary>
        Prompt,
        /// <summary>Aviso central grande (zona apagada, misión completada...).</summary>
        Toast,
        /// <summary>Huecos del inventario.</summary>
        Slot,
        /// <summary>Botones (menús, elecciones, mejoras).</summary>
        Button
    }

    /// <summary>
    /// Sistema de diseño de Ascua: UNA sola fuente de verdad para toda la interfaz (el equivalente
    /// a una hoja de estilos compartida). Decisiones únicas para todo el juego:
    ///  - Esquinas ANGULARES con un pequeño rombo en cada esquina (el lenguaje del Farol).
    ///  - Filo de 2 px en ámbar #FFC46B; la caja de diálogo, en dorado #FFE3A0.
    ///  - Fondo índigo casi negro, semitransparente, con textura de "vidrio esmerilado".
    /// Cada panel lleva un UIStyledPanel que lee este asset al arrancar: cambiar un color aquí
    /// cambia toda la interfaz a la vez.
    /// </summary>
    [CreateAssetMenu(menuName = "Ascua/Kit de estilo de UI", fileName = "UIStyleKit")]
    public class UIStyleKit : ScriptableObject
    {
        [Header("Colores")]
        public Color amber = new Color(1f, 0.769f, 0.42f, 1f);
        public Color gold = new Color(1f, 0.89f, 0.627f, 1f);
        public Color panelColor = new Color(0.02f, 0.03f, 0.08f, 0.78f);
        public Color textCold = new Color(0.72f, 0.77f, 0.93f, 1f);

        [Header("Sprites (9-slice)")]
        [Tooltip("Filo angular con rombos en las esquinas.")]
        public Sprite frameSprite;
        [Tooltip("Fondo de vidrio esmerilado (se tiñe con panelColor).")]
        public Sprite panelSprite;

        [Header("Medidas")]
        [Tooltip("Margen del fondo alrededor de los bloques del HUD que no tienen fondo propio.")]
        public float hudPadding = 14f;

        /// <summary>Opacidad del fondo según el papel (multiplica a panelColor.a).</summary>
        public float BackgroundAlpha(UIRole role)
        {
            switch (role)
            {
                case UIRole.Hud: return 0.75f;
                case UIRole.Panel: return 1.12f;
                case UIRole.Dialogue: return 1.15f;
                case UIRole.Prompt: return 0.85f;
                case UIRole.Toast: return 0.95f;
                case UIRole.Slot: return 0.8f;
                default: return 0.35f;
            }
        }

        /// <summary>Color del filo según el papel.</summary>
        public Color FrameColor(UIRole role)
        {
            Color c = role == UIRole.Dialogue ? gold : amber;
            switch (role)
            {
                case UIRole.Hud: c.a = 0.55f; break;
                case UIRole.Panel: c.a = 0.85f; break;
                case UIRole.Dialogue: c.a = 0.95f; break;
                case UIRole.Prompt: c.a = 0.5f; break;
                case UIRole.Toast: c.a = 0.7f; break;
                case UIRole.Slot: c.a = 0.45f; break;
                default: c.a = 0.6f; break;
            }
            return c;
        }
    }
}
