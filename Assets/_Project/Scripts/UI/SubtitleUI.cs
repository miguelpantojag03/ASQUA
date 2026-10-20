using TMPro;
using UnityEngine;

namespace Ascua.UI
{
    /// <summary>Subtítulos de sonidos narrativos clave ("[Acorde grave: El Mercado se ha apagado]"). Accesibilidad.</summary>
    public class SubtitleUI : MonoBehaviour
    {
        public static SubtitleUI Instance { get; private set; }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text text;
        [SerializeField] private float holdSeconds = 2.8f;

        private float timer;

        private void Awake()
        {
            Instance = this;
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show(string message)
        {
            if (text != null) text.text = message;
            timer = holdSeconds;
        }

        private void Update()
        {
            if (group == null) return;
            timer -= Time.unscaledDeltaTime;
            group.alpha = Mathf.MoveTowards(group.alpha, timer > 0f ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }
    }
}
