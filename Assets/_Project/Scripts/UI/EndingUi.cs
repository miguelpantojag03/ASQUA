using Ascua.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Piezas de UI creadas por código para los finales y la galería (sin tocar escenas ni prefabs).
    /// Los botones y textos se clonan de otros ya existentes para heredar exactamente su estilo.
    /// </summary>
    public static class EndingUi
    {
        /// <summary>Fondo de las pantallas completas (el mismo que el visor de ADN).</summary>
        public static readonly Color OverlayColor = new Color(0.02f, 0.03f, 0.07f, 0.97f);
        public static readonly Color AmberText = new Color(1f, 0.769f, 0.42f);
        public static readonly Color ColdText = new Color(0.85f, 0.88f, 0.96f);

        /// <summary>Capa a pantalla completa con fondo oscuro. Empieza oculta.</summary>
        public static CanvasGroup CreateOverlay(Transform parent, string name)
        {
            GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlay.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = OverlayColor;

            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            group.ignoreParentGroups = true;   // sigue activa aunque el panel de debajo se desactive
            SetVisible(group, false);
            return group;
        }

        /// <summary>Copia un botón (con su estilo y su tamaño) y le cambia el texto.</summary>
        public static Button CloneButton(Button template, Transform parent, string name, string label)
        {
            Button button = Object.Instantiate(template, parent);
            button.name = name;
            button.gameObject.SetActive(true);
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            SetLabel(button, label);

            // Justo detrás de la plantilla: así queda por debajo de los fundidos que se dibujan encima.
            if (parent == template.transform.parent) button.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
            return button;
        }

        /// <summary>Copia un texto (misma tipografía) y le quita estilos propios del original.</summary>
        public static TMP_Text CloneText(TMP_Text template, Transform parent, string name, float fontSize, Color color)
        {
            TMP_Text text = Object.Instantiate(template, parent);
            text.name = name;
            text.gameObject.SetActive(true);
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.characterSpacing = 0f;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            text.color = color;
            text.text = "";
            return text;
        }

        /// <summary>Coloca un elemento en un punto de la pantalla (anclas relativas) con un tamaño fijo.</summary>
        public static void Place(Component element, Vector2 anchor, Vector2 size)
        {
            RectTransform rect = (RectTransform)element.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        public static void SetLabel(Button button, string text)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = text;
        }

        public static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        public static void Select(Button button)
        {
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public static void Click()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiClick();
        }
    }
}
