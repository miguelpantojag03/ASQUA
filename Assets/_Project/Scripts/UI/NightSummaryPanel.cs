using System.Text;
using Ascua.AILevelGen;
using Ascua.Audio;
using Ascua.Core;
using Ascua.LevelGen;
using Ascua.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Pantalla al terminar la noche:
    ///  1) Resumen (NightSummary): zonas salvadas/perdidas, misiones y Vínculos.
    ///  2) Elegir 1 de 3 mejoras del Farol (PlayerUpgrades), que se conserva en las noches siguientes.
    ///  3) Siguiente noche · Modo foto · Reintentar · Título.
    /// Si Vesper se apagó entera no hay mejora: solo reintentar o volver.
    /// </summary>
    public class NightSummaryPanel : MonoBehaviour
    {
        [SerializeField] private NightManager night;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private float fadeDuration = 0.8f;

        [Header("Mejoras (1 de 3)")]
        [SerializeField] private CanvasGroup upgradeGroup;
        [SerializeField] private Button[] upgradeButtons;
        [SerializeField] private TMP_Text[] upgradeLabels;

        [Header("Después de elegir")]
        [SerializeField] private Button nextNightButton;
        [SerializeField] private Button photoButton;

        private static readonly UpgradeType[] Offer = { UpgradeType.RadioAmplio, UpgradeType.Eficiencia, UpgradeType.Regeneracion };

        private bool showing;
        private bool upgradeChosen;

        // "Ver ADN de este nivel": panel de transparencia de la IA de niveles. Se construye por código
        // (copiando el estilo del botón "Modo foto"), así no hace falta tocar la escena.
        private Button dnaButton;
        private Button dnaCloseButton;
        private CanvasGroup dnaViewer;
        private TMP_Text dnaText;

        private void Awake()
        {
            if (night == null) night = FindFirstObjectByType<NightManager>();
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (titleButton != null) titleButton.onClick.AddListener(OnTitle);
            if (nextNightButton != null) nextNightButton.onClick.AddListener(OnNextNight);
            if (photoButton != null) photoButton.onClick.AddListener(OnPhoto);
            for (int i = 0; upgradeButtons != null && i < upgradeButtons.Length; i++)
            {
                int index = i;
                upgradeButtons[i].onClick.AddListener(() => ChooseUpgrade(index));
            }
            SetVisible(group, 0f);
            SetVisible(upgradeGroup, 0f);
            BuildDnaUi();
        }

        private void OnEnable()
        {
            if (night != null) night.NightEnded += Show;
        }

        private void OnDisable()
        {
            if (night != null) night.NightEnded -= Show;
        }

        private void Update()
        {
            // El juego está en pausa (timeScale = 0): se usa tiempo sin escalar.
            if (!showing || group == null || group.alpha >= 1f) return;
            SetVisible(group, Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / fadeDuration));
        }

        private void Show(NightResult result)
        {
            showing = true;
            upgradeChosen = false;
            bool survived = result.ZonesSaved > 0;

            if (titleText != null) titleText.text = survived ? "Amanece en Vesper" : "Vesper se apagó";

            if (bodyText != null)
            {
                string reason = result.EndedEarly ? "La Penumbra se lo llevó todo antes del alba." : "Sobreviviste a la noche " + result.NightNumber + ".";
                NightSummary summary = night != null ? night.LastSummary : null;
                bodyText.text = reason + "\n\n" + (summary != null ? summary.ToDisplayText() : "Zonas salvadas: " + result.ZonesSaved + "/" + result.ZonesTotal);
                string seal = BuildSeal();
                if (seal.Length > 0) bodyText.text += "\n\n<color=#FFC46B>" + seal + "</color>";
            }
            if (dnaButton != null) dnaButton.gameObject.SetActive(night != null && night.CurrentDna != null);

            // Mejoras solo si amaneció.
            for (int i = 0; upgradeLabels != null && i < upgradeLabels.Length && i < Offer.Length; i++)
            {
                (string title, string description) = PlayerUpgrades.Describe(Offer[i]);
                upgradeLabels[i].text = "<b>" + title + "</b>\n<size=75%>" + description + "</size>";
            }
            SetVisible(upgradeGroup, survived ? 1f : 0f);
            SetActive(nextNightButton, false);
            SetActive(photoButton, true);

            Select(survived && upgradeButtons != null && upgradeButtons.Length > 0 ? upgradeButtons[0] : retryButton);
        }

        private void ChooseUpgrade(int index)
        {
            if (upgradeChosen || index < 0 || index >= Offer.Length) return;
            upgradeChosen = true;
            PlayerUpgrades.Choose(Offer[index]);
            Click();

            (string title, _) = PlayerUpgrades.Describe(Offer[index]);
            if (bodyText != null) bodyText.text += "\n\n<color=#FFC46B>Mejora elegida: " + title + "</color>";
            SetVisible(upgradeGroup, 0f);
            SetActive(nextNightButton, true);
            Select(nextNightButton);
        }

        private void OnNextNight()
        {
            Click();
            if (night != null) night.StartNextNight();
        }

        private void OnPhoto()
        {
            Click();
            if (PhotoMode.Instance == null) return;
            CanvasGroup summary = group;
            SetVisible(summary, 0f);
            PhotoMode.Instance.Enter(() =>
            {
                SetVisible(summary, 1f);
                Select(upgradeChosen ? nextNightButton : retryButton);
            });
        }

        // ================================================================== Ver ADN del nivel

        private void BuildDnaUi()
        {
            if (photoButton == null || bodyText == null || group == null) return;

            // Botón: copia del de "Modo foto" para heredar exactamente su estilo.
            dnaButton = Instantiate(photoButton, photoButton.transform.parent);
            dnaButton.name = "Boton_VerADN";
            dnaButton.gameObject.SetActive(true);
            dnaButton.onClick.RemoveAllListeners();
            RectTransform buttonRect = (RectTransform)dnaButton.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.86f, 0.09f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(340f, 60f);
            SetLabel(dnaButton, "Ver ADN del nivel");
            dnaButton.onClick.AddListener(OnShowDna);
            dnaButton.gameObject.SetActive(false);

            // Visor: pantalla completa por encima del resumen, con la tabla de ADN.
            GameObject viewer = new GameObject("ADN_Visor", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            viewer.transform.SetParent(group.transform, false);
            RectTransform viewerRect = (RectTransform)viewer.transform;
            viewerRect.anchorMin = Vector2.zero;
            viewerRect.anchorMax = Vector2.one;
            viewerRect.offsetMin = Vector2.zero;
            viewerRect.offsetMax = Vector2.zero;
            viewer.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.97f);
            dnaViewer = viewer.GetComponent<CanvasGroup>();
            dnaViewer.ignoreParentGroups = true;   // sigue activo aunque el resumen de debajo se desactive

            // Texto: copia del cuerpo del resumen (misma tipografía).
            dnaText = Instantiate(bodyText, viewer.transform);
            dnaText.name = "Tabla_ADN";
            RectTransform textRect = (RectTransform)dnaText.transform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.95f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(1500f, 820f);
            dnaText.alignment = TextAlignmentOptions.TopLeft;
            dnaText.fontSize = 24f;
            dnaText.richText = true;

            dnaCloseButton = Instantiate(photoButton, viewer.transform);
            dnaCloseButton.name = "Boton_CerrarADN";
            dnaCloseButton.gameObject.SetActive(true);
            dnaCloseButton.onClick.RemoveAllListeners();
            RectTransform closeRect = (RectTransform)dnaCloseButton.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0.06f);
            closeRect.anchoredPosition = Vector2.zero;
            closeRect.sizeDelta = new Vector2(300f, 60f);
            SetLabel(dnaCloseButton, "Cerrar");
            dnaCloseButton.onClick.AddListener(OnCloseDna);

            SetViewer(false);
        }

        private void OnShowDna()
        {
            Click();
            if (dnaViewer == null || night == null || night.CurrentDna == null) return;
            dnaText.text = BuildDnaText();
            group.interactable = false;   // el teclado y el mando no deben "caer" en los botones de debajo
            SetViewer(true);
            Select(dnaCloseButton);
        }

        private void OnCloseDna()
        {
            Click();
            SetViewer(false);
            group.interactable = true;
            Select(dnaButton);
        }

        private void SetViewer(bool visible)
        {
            if (dnaViewer == null) return;
            dnaViewer.alpha = visible ? 1f : 0f;
            dnaViewer.interactable = visible;
            dnaViewer.blocksRaycasts = visible;
        }

        /// <summary>Sello del nivel: de dónde sale esta noche respecto a la Noche 1.</summary>
        private string BuildSeal()
        {
            if (night == null || night.CurrentDna == null) return "";
            if (night.IsGeneratedNight)
            {
                string line = "Nivel generado a partir de tu Noche 1";
                if (night.CurrentDna.origin == LevelDnaOrigin.Local) line += "  <size=75%>· generado con reglas locales</size>";
                return line;
            }
            return night.NightNumber == 1
                ? "Noche 1 · el ADN base de los niveles que vendrán"
                : "Noche diseñada a mano · comparada con tu Noche 1";
        }

        private static string OriginText(string origin)
        {
            switch (origin)
            {
                case LevelDnaOrigin.Base: return "ADN base (Noche 1, curada a mano)";
                case LevelDnaOrigin.Manual: return "Diseñada a mano";
                case LevelDnaOrigin.AiBank: return "Propuesta por una IA y validada por código";
                case LevelDnaOrigin.Local: return "Generada con reglas locales (sin internet)";
                default: return origin;
            }
        }

        /// <summary>Tabla legible (nunca JSON crudo): parámetro, valor en la Noche 1, valor en este nivel y cambio.</summary>
        private string BuildDnaText()
        {
            LevelDNA dna = night.CurrentDna;
            StringBuilder sb = new StringBuilder();
            sb.Append("<size=150%><b>ADN de este nivel</b></size>\n");
            sb.Append("<color=#FFC46B>").Append(BuildSeal()).Append("</color>\n");
            sb.Append("<color=#B8C4EE>Origen: ").Append(OriginText(dna.origin)).Append("  ·  Semilla ").Append(dna.seed);
            if (night.GeneratedLevel != null) sb.Append("  ·  Valores corregidos por el validador: ").Append(night.GeneratedLevel.RepairLog.Count);
            sb.Append("</color>\n\n");

            sb.Append("<b><color=#FFE3A0><pos=0%>Parámetro<pos=50%>Noche 1 (base)<pos=70%>Este nivel<pos=88%>Cambio</color></b>\n");
            foreach (DnaRow row in LevelDnaRows.Compare(night.BaseDna, dna))
            {
                sb.Append("<pos=0%>").Append(row.Label)
                  .Append("<pos=50%>").Append(row.BaseText)
                  .Append("<pos=70%>").Append(row.CurrentText)
                  .Append("<pos=88%><color=#FFC46B>").Append(row.DeltaText).Append("</color>\n");
            }

            if (dna.narrativeFragments != null && dna.narrativeFragments.Length > 0)
            {
                sb.Append("\n<color=#B8C4EE><i>").Append(string.Join("  ·  ", dna.narrativeFragments)).Append("</i></color>");
            }
            return sb.ToString();
        }

        private static void SetLabel(Button button, string text)
        {
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
            if (label != null) label.text = text;
        }

        private void OnRetry()
        {
            Click();
            if (night != null) night.RestartNight();
        }

        private void OnTitle()
        {
            Click();
            if (night != null) night.ReturnToTitle();
        }

        private static void Click()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUiClick();
        }

        private static void Select(Button button)
        {
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }

        private static void SetVisible(CanvasGroup g, float alpha)
        {
            if (g == null) return;
            g.alpha = alpha;
            g.interactable = alpha > 0.5f;
            g.blocksRaycasts = alpha > 0.01f;
        }
    }
}
