using System.Collections;
using Ascua.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Pantalla de título: fundido de entrada, título "Ascua" que respira como una brasa,
    /// deriva lenta de cámara sobre el skyline de Vesper y botones Jugar / Continuar / Finales / Salir.
    /// "Continuar" y "Finales" se crean por código copiando el botón Jugar (la escena no cambia).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Escena jugable")]
        [SerializeField] private string gameplaySceneName = "Plaza";

        [Header("UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private float fadeDuration = 1.2f;

        [Header("Brasa del título")]
        [SerializeField] private Color emberColor = new Color(1f, 0.769f, 0.42f);
        [SerializeField] private Color emberDimColor = new Color(0.85f, 0.55f, 0.3f);
        [SerializeField] private float breathSpeed = 0.6f;

        [Header("Cámara")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Vector3 driftAmplitude = new Vector3(0.6f, 0.15f, 0f);
        [SerializeField] private float driftSpeed = 0.08f;

        private Vector3 cameraStartPosition;
        private Quaternion cameraStartRotation;
        private bool loading;

        // Creados por código (copias de playButton).
        private Button continueButton;
        private Button endingsButton;
        private EndingGallery gallery;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(OnPlay);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            BuildExtraButtons();
        }

        /// <summary>Añade "Continuar" y "Finales" y reparte los cuatro botones en columna.</summary>
        private void BuildExtraButtons()
        {
            if (playButton == null) return;
            Transform parent = playButton.transform.parent;

            continueButton = EndingUi.CloneButton(playButton, parent, "Boton_Continuar", "Continuar");
            continueButton.onClick.AddListener(OnContinue);
            continueButton.gameObject.SetActive(SaveSystem.HasProgress);

            endingsButton = EndingUi.CloneButton(playButton, parent, "Boton_Finales", "Finales");
            endingsButton.onClick.AddListener(OnEndings);

            SetAnchorY(playButton, 0.42f);
            SetAnchorY(continueButton, 0.33f);
            SetAnchorY(endingsButton, 0.24f);
            SetAnchorY(quitButton, 0.15f);

            // La galería usa la tipografía del título y el estilo de los botones.
            if (titleText != null) gallery = new EndingGallery(transform, titleText, playButton);
        }

        private static void SetAnchorY(Button button, float y)
        {
            if (button == null) return;
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, y);
        }

        private void Start()
        {
            GameSession.SetCursorLocked(false);

            if (cameraTransform != null)
            {
                cameraStartPosition = cameraTransform.position;
                cameraStartRotation = cameraTransform.rotation;
            }

            // Selección inicial para poder navegar con mando (Continuar si hay partida guardada).
            Button first = continueButton != null && continueButton.gameObject.activeSelf ? continueButton : playButton;
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }

            if (fadeGroup != null) StartCoroutine(Fade(1f, 0f));
        }

        private void Update()
        {
            if (titleText != null)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f);
                titleText.color = Color.Lerp(emberDimColor, emberColor, breath);
            }

            if (cameraTransform != null)
            {
                float t = Time.time * driftSpeed * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Sin(t) * driftAmplitude.x, Mathf.Sin(t * 1.37f) * driftAmplitude.y, 0f);
                cameraTransform.position = cameraStartPosition + offset;
                cameraTransform.rotation = cameraStartRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.8f) * 1.2f, 0f);
            }
        }

        private void OnPlay()
        {
            if (loading) return;
            loading = true;
            // "Jugar" desde el título empieza una partida nueva: noche 1, sin mejoras ni Vínculos.
            // Los finales vistos no se tocan: viven solo en SaveSystem.
            GameProgress.ResetAll();
            Ascua.Player.PlayerUpgrades.ResetAll();
            Ascua.Quests.QuestManager.ClearPendingBonds();
            if (Ascua.Quests.QuestManager.Instance != null) Ascua.Quests.QuestManager.Instance.ResetAll();
            StartCoroutine(LoadGameplay());
        }

        /// <summary>"Continuar": restaura la partida guardada y carga la escena jugable.</summary>
        private void OnContinue()
        {
            if (loading) return;
            if (!SaveSystem.RestoreProgress())
            {
                // Sin partida válida: se oculta el botón y se vuelve a "Jugar".
                continueButton.gameObject.SetActive(false);
                if (playButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
                return;
            }
            loading = true;
            StartCoroutine(LoadGameplay());
        }

        private void OnEndings()
        {
            if (loading || gallery == null) return;
            EndingUi.Click();
            gallery.Open(() => EndingUi.Select(endingsButton));
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator LoadGameplay()
        {
            if (playButton != null) playButton.interactable = false;
            if (quitButton != null) quitButton.interactable = false;
            if (continueButton != null) continueButton.interactable = false;
            if (endingsButton != null) endingsButton.interactable = false;
            if (fadeGroup != null) yield return Fade(fadeGroup.alpha, 1f);
            SceneManager.LoadScene(gameplaySceneName);
        }

        private IEnumerator Fade(float from, float to)
        {
            fadeGroup.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = to;
            fadeGroup.blocksRaycasts = to > 0.01f;
        }
    }
}
