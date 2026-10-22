using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Aplica el UIStyleKit a un panel: fondo de vidrio esmerilado + filo angular con rombos.
    ///  - Si el panel ya tiene Image propia, esa Image es el fondo.
    ///  - Si no la tiene (p. ej. la barra de energía), crea un hijo "Fondo" algo más grande que el bloque.
    ///  - El filo es siempre un hijo "Marco" estirado (se reutiliza si ya existe).
    /// Se aplica al construir la escena y otra vez al arrancar, así editar el kit actualiza toda la UI.
    /// </summary>
    [ExecuteAlways]
    public class UIStyledPanel : MonoBehaviour
    {
        [SerializeField] private UIStyleKit kit;
        [SerializeField] private UIRole role = UIRole.Panel;
        [Tooltip("Para botones: no se toca el color del fondo (lo gestiona el Button con sus tintes).")]
        [SerializeField] private bool keepBackgroundColor;

        public UIRole Role => role;
        public UIStyleKit Kit => kit;

        public void Configure(UIStyleKit styleKit, UIRole styleRole, bool keepBackground = false)
        {
            kit = styleKit;
            role = styleRole;
            keepBackgroundColor = keepBackground;
            Apply();
        }

        private void Awake()
        {
            Apply();
        }

        /// <summary>Aplica fondo y marco según el kit.</summary>
        public void Apply()
        {
            if (kit == null) return;
            RectTransform rt = transform as RectTransform;
            if (rt == null) return;

            // --- Fondo
            Image background = GetComponent<Image>();
            bool ownBackground = background != null;
            if (!ownBackground)
            {
                Transform existing = transform.Find("Fondo");
                RectTransform fondo = existing != null ? (RectTransform)existing : CreateChild("Fondo", rt);
                fondo.SetAsFirstSibling();
                float pad = kit.hudPadding;
                fondo.offsetMin = new Vector2(-pad, -pad * 0.6f);
                fondo.offsetMax = new Vector2(pad, pad * 0.6f);
                background = fondo.GetComponent<Image>();
                if (background == null) background = fondo.gameObject.AddComponent<Image>();
            }
            if (!keepBackgroundColor)
            {
                Color bg = kit.panelColor;
                bg.a = Mathf.Clamp01(bg.a * kit.BackgroundAlpha(role));
                background.color = bg;
            }
            if (kit.panelSprite != null && role != UIRole.Button)
            {
                background.sprite = kit.panelSprite;
                background.type = Image.Type.Sliced;
            }
            background.raycastTarget = role == UIRole.Button;

            // --- Marco (encima del fondo, debajo del contenido)
            Transform frameTransform = transform.Find("Marco");
            RectTransform frame = frameTransform != null ? (RectTransform)frameTransform : CreateChild("Marco", rt);
            if (frame.GetComponent<LayoutElement>() == null) frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image frameImage = frame.GetComponent<Image>();
            if (frameImage == null) frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = kit.frameSprite;
            frameImage.type = Image.Type.Sliced;
            frameImage.color = kit.FrameColor(role);
            frameImage.raycastTarget = false;
            if (!ownBackground)
            {
                // Mismo tamaño que el fondo añadido.
                frame.offsetMin = new Vector2(-kit.hudPadding, -kit.hudPadding * 0.6f);
                frame.offsetMax = new Vector2(kit.hudPadding, kit.hudPadding * 0.6f);
                frame.SetSiblingIndex(1);
            }
            else
            {
                frame.offsetMin = Vector2.zero;
                frame.offsetMax = Vector2.zero;
                frame.SetAsFirstSibling();
            }
        }

        private static RectTransform CreateChild(string name, RectTransform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform child = (RectTransform)go.transform;
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.pivot = new Vector2(0.5f, 0.5f);
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            // Que los paneles con VerticalLayoutGroup (Esperanza) no lo traten como una fila más.
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return child;
        }
    }
}
