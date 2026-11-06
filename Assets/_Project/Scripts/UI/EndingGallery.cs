using System;
using System.Text;
using Ascua.Core;
using Ascua.Endings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Galería de finales: los seis finales, con "???" en los que aún no se han visto.
    /// Se usa en el título y en la pantalla de final. Se construye por código.
    /// </summary>
    public class EndingGallery
    {
        private readonly CanvasGroup overlay;
        private readonly TMP_Text listText;
        private readonly Button closeButton;
        private Action onClosed;

        /// <param name="parent">Dónde se crea la capa (un Canvas o un panel a pantalla completa).</param>
        /// <param name="textTemplate">Texto del que se copia la tipografía.</param>
        /// <param name="buttonTemplate">Botón del que se copia el estilo.</param>
        public EndingGallery(Transform parent, TMP_Text textTemplate, Button buttonTemplate)
        {
            overlay = EndingUi.CreateOverlay(parent, "Galeria_Finales");

            TMP_Text heading = EndingUi.CloneText(textTemplate, overlay.transform, "Titulo_Galeria", 56f, EndingUi.AmberText);
            EndingUi.Place(heading, new Vector2(0.5f, 0.88f), new Vector2(1200f, 90f));
            heading.text = "Galería de finales";

            listText = EndingUi.CloneText(textTemplate, overlay.transform, "Lista_Finales", 28f, EndingUi.ColdText);
            EndingUi.Place(listText, new Vector2(0.5f, 0.48f), new Vector2(1300f, 680f));

            closeButton = EndingUi.CloneButton(buttonTemplate, overlay.transform, "Boton_CerrarGaleria", "Cerrar");
            EndingUi.Place(closeButton, new Vector2(0.5f, 0.08f), new Vector2(300f, 60f));
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>Muestra la galería encima de todo. onClosed se llama al pulsar "Cerrar".</summary>
        public void Open(Action onClosed)
        {
            this.onClosed = onClosed;
            listText.text = BuildList();
            overlay.transform.SetAsLastSibling();
            EndingUi.SetVisible(overlay, true);
            EndingUi.Select(closeButton);
        }

        public void Close()
        {
            EndingUi.Click();
            EndingUi.SetVisible(overlay, false);
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        /// <summary>Una entrada por final: nombre y título corto si se vio; "???" si no.</summary>
        private static string BuildList()
        {
            StringBuilder sb = new StringBuilder();
            int seen = 0;
            foreach (EndingInfo info in EndingCatalog.All)
            {
                if (SaveSystem.IsEndingSeen(info.SaveKey))
                {
                    seen++;
                    sb.Append("<b><color=#FFC46B>").Append(info.Name).Append("</color></b>\n")
                      .Append("<size=75%><i>").Append(info.ShortTitle).Append("</i></size>\n\n");
                }
                else
                {
                    sb.Append("<b><color=#6B7390>???</color></b>\n<size=75%> </size>\n\n");
                }
            }
            sb.Append("<size=80%><color=#B8C4EE>Finales vistos: ").Append(seen).Append('/').Append(EndingCatalog.All.Count).Append("</color></size>");
            return sb.ToString();
        }
    }
}
