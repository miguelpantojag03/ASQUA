using Ascua.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Inventario en el HUD: huecos pequeños abajo a la derecha. Cada objeto de misión ocupa uno,
    /// con su color y su inicial; el nombre aparece unos segundos al recogerlo.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private PickupSystem pickupSystem;
        [SerializeField] private Image[] slotIcons;
        [SerializeField] private TMP_Text[] slotLetters;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.08f);

        private float nameTimer;

        private void Awake()
        {
            if (pickupSystem == null) pickupSystem = FindFirstObjectByType<PickupSystem>();
        }

        private void OnEnable()
        {
            if (pickupSystem != null) pickupSystem.InventoryChanged += Refresh;
        }

        private void OnDisable()
        {
            if (pickupSystem != null) pickupSystem.InventoryChanged -= Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            if (nameLabel == null) return;
            nameTimer -= Time.unscaledDeltaTime;
            nameLabel.alpha = Mathf.MoveTowards(nameLabel.alpha, nameTimer > 0f ? 1f : 0f, Time.unscaledDeltaTime * 2f);
        }

        private void Refresh()
        {
            if (pickupSystem == null || slotIcons == null) return;
            var items = pickupSystem.Items;
            for (int i = 0; i < slotIcons.Length; i++)
            {
                bool used = i < items.Count;
                if (slotIcons[i] != null) slotIcons[i].color = used ? items[i].Color : emptyColor;
                if (slotLetters != null && i < slotLetters.Length && slotLetters[i] != null)
                {
                    slotLetters[i].text = used && !string.IsNullOrEmpty(items[i].Name) ? Initial(items[i].Name) : "";
                }
            }

            if (items.Count > 0 && nameLabel != null)
            {
                nameLabel.text = items[items.Count - 1].Name;
                nameTimer = 3f;
            }
        }

        /// <summary>Inicial del objeto sin contar el artículo ("la postal" -> "P").</summary>
        private static string Initial(string name)
        {
            string[] words = name.Trim().Split(' ');
            string word = words[words.Length - 1];
            foreach (string w in words)
            {
                string lower = w.ToLowerInvariant();
                if (lower == "el" || lower == "la" || lower == "los" || lower == "las" || lower == "un" || lower == "una") continue;
                word = w;
                break;
            }
            return word.Length > 0 ? word.Substring(0, 1).ToUpperInvariant() : "";
        }
    }
}
