using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Piezas de HUD creadas por código (barra de La Marea, indicador compacto de la noche), sin tocar escenas.
    /// Mismo lienzo que el HUD de la escena: 1920×1080 de referencia y mezcla 0.5 (CanvasScaler), así lo que
    /// no se solapa a 1080p tampoco se solapa a 720p (misma proporción, todo escala igual).
    /// </summary>
    public static class HudBuild
    {
        /// <summary>Lienzo propio a pantalla completa con su CanvasGroup (no recibe clics).</summary>
        public static CanvasGroup CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            go.layer = 5;   // UI
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        /// <summary>Rect con anclas, pivote, posición y tamaño (como UIRect del constructor de escenas).</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Rect que llena a su padre con márgenes (izquierda, abajo, derecha, arriba).</summary>
        public static RectTransform Fill(string name, Transform parent, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TMP_Text AddText(RectTransform rect, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        /// <summary>
        /// Fondo y filo del sistema de diseño (UIStyleKit) con el papel indicado. El kit se toma de cualquier
        /// panel ya estilizado de la escena; si no hay ninguno, un fondo oscuro sencillo con el mismo color.
        /// </summary>
        public static void Style(RectTransform panel, UIRole role)
        {
            UIStyledPanel reference = Object.FindFirstObjectByType<UIStyledPanel>();
            UIStyleKit kit = reference != null ? reference.Kit : null;
            if (kit != null)
            {
                panel.gameObject.AddComponent<UIStyledPanel>().Configure(kit, role);
                return;
            }
            Image back = AddImage(Fill("Fondo", panel, -14f, -8f, -14f, -8f), new Color(0.02f, 0.03f, 0.08f, 0.6f));
            back.transform.SetAsFirstSibling();
        }
    }
}
