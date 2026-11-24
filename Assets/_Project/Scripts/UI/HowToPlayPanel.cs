using System;
using Ascua.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Pantalla "Cómo jugar": controles de teclado y ratón y de mando (los de PlayerInputReader y los menús)
    /// y el interruptor "Saltar tutorial" (SaveSystem, vale para todas las partidas).
    /// Se abre desde el menú de pausa y desde el título. Se construye por código, como la galería de finales:
    /// copia la tipografía de un texto y el estilo de un botón ya existentes.
    /// Colocación (referencia 1920×1080): título arriba (0.9), dos columnas (0.29 / 0.71) entre y 0.22 y 0.82,
    /// una línea sobre la luz (0.18) y los botones abajo (0.08).
    /// </summary>
    public class HowToPlayPanel
    {
        private const string KeyboardControls =
            "<b><color=#FFC46B>TECLADO Y RATÓN</color></b>\n" +
            "WASD / flechas · moverse\n" +
            "Ratón · cámara      Rueda · acercar\n" +
            "Shift · correr      Espacio · saltar\n" +
            "Ctrl · esquivar (a tiempo: esquiva perfecta)\n" +
            "Clic izquierdo · barrido de luz (combo de 3)\n" +
            "Mantener clic izquierdo · golpe cargado <size=80%>(Lupa de Mina)</size>\n" +
            "Atacar justo antes del golpe · contraataque <size=80%>(Reloj de Nadir)</size>\n" +
            "Clic derecho / F · avivar el Farol\n" +
            "Q / E · tamaño de la luz\n" +
            "R · Pulso de luz      G · habilidad de Vínculo\n" +
            "T / clic central · fijar objetivo\n" +
            "Mantener E · hablar e interactuar\n" +
            "Tab · misiones      J · Diario\n" +
            "H · indicador de la noche      Esc · pausa";

        private const string GamepadControls =
            "<b><color=#FFC46B>MANDO</color></b>\n" +
            "Stick izquierdo · moverse      L3 · correr\n" +
            "Stick derecho · cámara\n" +
            "Cruceta arriba / abajo · acercar\n" +
            "A · saltar      B · esquivar\n" +
            "RT · barrido de luz (mantener: cargado)\n" +
            "LT · avivar el Farol\n" +
            "LB / RB · tamaño de la luz\n" +
            "Y · Pulso de luz\n" +
            "Cruceta arriba · habilidad de Vínculo\n" +
            "Mantener X · hablar e interactuar\n" +
            "R3 · fijar objetivo\n" +
            "Select · misiones\n" +
            "Cruceta izquierda · indicador de la noche\n" +
            "Start · pausa      B · volver";

        private const string LightLine =
            "<i>La luz deshace las sombras. Las veladas, y La Marea, solo se hieren de verdad bajo el Farol.</i>";

        private readonly CanvasGroup overlay;
        private readonly Button skipButton;
        private readonly Button closeButton;
        private Action onClosed;

        public bool IsOpen => overlay != null && overlay.alpha > 0.5f;

        /// <param name="parent">Dónde se crea la capa (un Canvas o un panel a pantalla completa).</param>
        /// <param name="textTemplate">Texto del que se copia la tipografía.</param>
        /// <param name="buttonTemplate">Botón del que se copia el estilo.</param>
        public HowToPlayPanel(Transform parent, TMP_Text textTemplate, Button buttonTemplate)
        {
            overlay = EndingUi.CreateOverlay(parent, "Como_Jugar");

            TMP_Text heading = EndingUi.CloneText(textTemplate, overlay.transform, "Titulo_ComoJugar", 52f, EndingUi.AmberText);
            EndingUi.Place(heading, new Vector2(0.5f, 0.9f), new Vector2(1200f, 80f));
            heading.text = "Cómo jugar";

            Column(textTemplate, "Teclado", new Vector2(0.29f, 0.52f), KeyboardControls);
            Column(textTemplate, "Mando", new Vector2(0.71f, 0.52f), GamepadControls);

            TMP_Text light = EndingUi.CloneText(textTemplate, overlay.transform, "Luz", 24f, EndingUi.ColdText);
            EndingUi.Place(light, new Vector2(0.5f, 0.18f), new Vector2(1500f, 44f));
            light.text = LightLine;

            skipButton = EndingUi.CloneButton(buttonTemplate, overlay.transform, "Boton_SaltarTutorial", "");
            EndingUi.Place(skipButton, new Vector2(0.4f, 0.08f), new Vector2(440f, 60f));
            skipButton.onClick.AddListener(ToggleSkip);

            closeButton = EndingUi.CloneButton(buttonTemplate, overlay.transform, "Boton_CerrarComoJugar", "Cerrar");
            EndingUi.Place(closeButton, new Vector2(0.62f, 0.08f), new Vector2(300f, 60f));
            closeButton.onClick.AddListener(Close);

            RefreshSkipLabel();
        }

        private void Column(TMP_Text template, string name, Vector2 anchor, string content)
        {
            TMP_Text column = EndingUi.CloneText(template, overlay.transform, "Controles_" + name, 23f, EndingUi.ColdText);
            EndingUi.Place(column, anchor, new Vector2(760f, 640f));
            column.alignment = TextAlignmentOptions.TopLeft;
            column.lineSpacing = 6f;
            column.text = content;
        }

        /// <summary>Muestra la pantalla encima de todo. onClosed se llama al cerrarla.</summary>
        public void Open(Action onClosed)
        {
            this.onClosed = onClosed;
            RefreshSkipLabel();
            overlay.transform.SetAsLastSibling();
            EndingUi.SetVisible(overlay, true);
            EndingUi.Select(closeButton);
        }

        public void Close()
        {
            if (!IsOpen) return;
            EndingUi.Click();
            EndingUi.SetVisible(overlay, false);
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private void ToggleSkip()
        {
            EndingUi.Click();
            SaveSystem.SetSkipTutorial(!SaveSystem.SkipTutorial);
            RefreshSkipLabel();
        }

        private void RefreshSkipLabel()
        {
            EndingUi.SetLabel(skipButton, "Saltar tutorial: " + (SaveSystem.SkipTutorial ? "Sí" : "No"));
        }
    }
}
