using System;
using System.Collections.Generic;
using System.Text;
using Ascua.Endings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Pantalla de final a pantalla completa: título del final, epílogo línea a línea y una línea
    /// por cada personaje con Vínculo alto. Después, "Galería de finales" y "Volver al título".
    /// El juego está en pausa (timeScale = 0): todo usa Time.unscaledDeltaTime.
    /// Se construye por código con EndingScreen.Create (sin tocar escenas ni prefabs).
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        [Tooltip("Segundos entre una línea del epílogo y la siguiente.")]
        [SerializeField] private float lineInterval = 2.4f;
        [Tooltip("Segundos que tarda cada línea en aparecer del todo.")]
        [SerializeField] private float lineFade = 1f;

        private CanvasGroup overlay;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private Button galleryButton;
        private Button titleButton;
        private EndingGallery gallery;
        private Action onReturnToTitle;

        private readonly List<string> lines = new List<string>();
        private int companionStart;
        private int shown;
        private float timer;
        private bool revealing;

        /// <summary>Crea la pantalla (oculta) dentro de parent, copiando el estilo de los textos y el botón dados.</summary>
        public static EndingScreen Create(Transform parent, TMP_Text titleTemplate, TMP_Text bodyTemplate, Button buttonTemplate, Action onReturnToTitle)
        {
            CanvasGroup overlay = EndingUi.CreateOverlay(parent, "Pantalla_Final");
            EndingScreen screen = overlay.gameObject.AddComponent<EndingScreen>();
            screen.Build(overlay, titleTemplate, bodyTemplate, buttonTemplate);
            screen.onReturnToTitle = onReturnToTitle;
            return screen;
        }

        private void Build(CanvasGroup group, TMP_Text titleTemplate, TMP_Text bodyTemplate, Button buttonTemplate)
        {
            overlay = group;

            titleText = EndingUi.CloneText(titleTemplate, transform, "Titulo_Final", 72f, EndingUi.AmberText);
            EndingUi.Place(titleText, new Vector2(0.5f, 0.8f), new Vector2(1400f, 200f));

            bodyText = EndingUi.CloneText(bodyTemplate, transform, "Epilogo", 30f, EndingUi.ColdText);
            EndingUi.Place(bodyText, new Vector2(0.5f, 0.42f), new Vector2(1300f, 600f));

            galleryButton = EndingUi.CloneButton(buttonTemplate, transform, "Boton_GaleriaFinales", "Galería de finales");
            EndingUi.Place(galleryButton, new Vector2(0.36f, 0.08f), new Vector2(360f, 60f));
            galleryButton.onClick.AddListener(OnGallery);

            titleButton = EndingUi.CloneButton(buttonTemplate, transform, "Boton_VolverTitulo", "Volver al título");
            EndingUi.Place(titleButton, new Vector2(0.64f, 0.08f), new Vector2(360f, 60f));
            titleButton.onClick.AddListener(OnTitle);

            // La galería va en el mismo panel, por encima de esta pantalla al abrirse.
            gallery = new EndingGallery(transform.parent, bodyTemplate, buttonTemplate);
            SetButtonsVisible(false);
        }

        /// <summary>Muestra el final y empieza a revelar el epílogo.</summary>
        public void Show(EndingInfo info, List<string> companionLines)
        {
            titleText.text = info.Name + "\n<size=45%><i>" + info.ShortTitle + "</i></size>";

            lines.Clear();
            lines.AddRange(info.Epilogue);
            companionStart = lines.Count;
            if (companionLines != null) lines.AddRange(companionLines);

            shown = lines.Count > 0 ? 1 : 0;
            timer = 0f;
            revealing = true;
            bodyText.text = BuildBody();

            transform.SetAsLastSibling();
            EndingUi.SetVisible(overlay, true);
            SetButtonsVisible(false);
        }

        private void Update()
        {
            if (!revealing) return;

            timer += Time.unscaledDeltaTime;
            if (shown < lines.Count && timer >= lineInterval)
            {
                shown++;
                timer = 0f;
            }
            bodyText.text = BuildBody();

            if (shown >= lines.Count && timer >= lineFade) FinishReveal();
        }

        private void FinishReveal()
        {
            revealing = false;
            SetButtonsVisible(true);
            EndingUi.Select(galleryButton);
        }

        /// <summary>Las líneas ya reveladas; la última aparece con un fundido (etiquetas de TextMeshPro).</summary>
        private string BuildBody()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < shown; i++)
            {
                bool newest = i == shown - 1 && revealing;
                byte alpha = newest ? (byte)(255f * Mathf.Clamp01(timer / Mathf.Max(0.01f, lineFade))) : (byte)255;
                string alphaHex = alpha.ToString("X2");

                if (i == companionStart) sb.Append('\n');
                if (i >= companionStart) sb.Append("<color=#FFC46B").Append(alphaHex).Append("><i>").Append(lines[i]).Append("</i></color>");
                else sb.Append("<alpha=#").Append(alphaHex).Append('>').Append(lines[i]);
                sb.Append("\n\n");
            }
            return sb.ToString();
        }

        private void SetButtonsVisible(bool visible)
        {
            galleryButton.gameObject.SetActive(visible);
            titleButton.gameObject.SetActive(visible);
        }

        private void OnGallery()
        {
            EndingUi.Click();
            gallery.Open(() => EndingUi.Select(galleryButton));
        }

        private void OnTitle()
        {
            // El sonido de clic lo pone quien recibe la llamada (NightSummaryPanel.OnTitle).
            titleButton.interactable = false;
            galleryButton.interactable = false;
            onReturnToTitle?.Invoke();
        }
    }
}
