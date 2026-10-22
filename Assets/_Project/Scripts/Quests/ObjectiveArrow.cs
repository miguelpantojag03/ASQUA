using Ascua.Core;
using Ascua.Narrative;
using TMPro;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Indicador de dirección hacia el objetivo actual: si el objetivo está en pantalla, un pequeño
    /// rombo flota sobre él; si no, una flecha se pega al borde de la pantalla apuntando hacia él.
    /// Muestra la distancia en metros. Se oculta en diálogos y menús.
    /// </summary>
    public class ObjectiveArrow : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform arrowGraphic;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Margen respecto al borde de la pantalla (fracción).")]
        [SerializeField] private float edgeMargin = 0.07f;
        [SerializeField] private float heightOffset = 2.1f;
        [Tooltip("Por debajo de esta distancia el indicador se desvanece (ya llegaste).")]
        [SerializeField] private float hideDistance = 3f;

        private Camera cam;
        private Transform player;
        private int shownMeters = -1;

        private void Start()
        {
            cam = Camera.main;
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        private void LateUpdate()
        {
            QuestManager manager = QuestManager.Instance;
            bool blocked = GameSession.IsModalOpen || (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen);
            QuestRuntime q = manager != null && !blocked ? manager.TrackedQuest : null;

            // Durante una oleada de sombras la flecha guía a la sombra más cercana (prioridad sobre misiones).
            Vector3 target = Vector3.zero;
            Ascua.Combat.CombatWaveDirector waves = Ascua.Combat.CombatWaveDirector.Instance;
            bool hasTarget = !blocked && player != null && waves != null && waves.TryGetObjective(player.position, out target);
            if (!hasTarget) hasTarget = q != null && manager.TryGetObjectivePosition(q, out target);

            if (!hasTarget || cam == null || player == null)
            {
                SetAlpha(0f);
                return;
            }

            float distance = Vector3.Distance(player.position, target);
            SetAlpha(Mathf.Clamp01((distance - hideDistance) / 2f));

            Vector3 viewport = cam.WorldToViewportPoint(target + Vector3.up * heightOffset);
            bool behind = viewport.z < 0f;
            if (behind) viewport = new Vector3(1f - viewport.x, 1f - viewport.y, -viewport.z);

            bool onScreen = !behind && viewport.x > edgeMargin && viewport.x < 1f - edgeMargin && viewport.y > edgeMargin && viewport.y < 1f - edgeMargin;

            Vector2 v = new Vector2(viewport.x, viewport.y);
            if (!onScreen)
            {
                // Proyectar sobre el borde desde el centro de la pantalla.
                Vector2 dir = v - new Vector2(0.5f, 0.5f);
                if (behind && dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                float scale = (0.5f - edgeMargin) / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                v = new Vector2(0.5f, 0.5f) + dir * scale;
                if (arrowGraphic != null)
                {
                    arrowGraphic.gameObject.SetActive(true);
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    arrowGraphic.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
            else if (arrowGraphic != null)
            {
                arrowGraphic.gameObject.SetActive(true);
                arrowGraphic.localRotation = Quaternion.Euler(0f, 0f, 180f); // apunta hacia abajo, al objetivo
            }

            if (marker != null && canvasRect != null)
            {
                Vector2 size = canvasRect.rect.size;
                marker.anchoredPosition = new Vector2((v.x - 0.5f) * size.x, (v.y - 0.5f) * size.y);
            }

            int meters = Mathf.RoundToInt(distance);
            if (distanceText != null && meters != shownMeters)
            {
                shownMeters = meters;
                distanceText.text = meters + " m";
            }
        }

        private void SetAlpha(float alpha)
        {
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, alpha, Time.unscaledDeltaTime * 4f);
        }
    }
}
