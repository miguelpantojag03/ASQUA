using Ascua.Controls;
using Ascua.Narrative;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Detecta al personaje con quien se puede hablar más cercano y muestra el aviso
    /// "Mantén E · Hablar con Mina" (InteractionPrompt del HUD). Al mantener E el tiempo
    /// suficiente, abre la conversación.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private float interactRadius = 2.8f;
        [Tooltip("Segundos que hay que mantener E (evita abrir diálogos por accidente).")]
        [SerializeField] private float holdTime = 0.35f;

        private NpcInteractable current;
        private float holdTimer;
        private bool waitForRelease;

        public NpcInteractable Current => current;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            InteractionPrompt prompt = InteractionPrompt.Instance;
            DialogueRunner runner = DialogueRunner.Instance;
            if (runner != null && runner.IsOpen)
            {
                if (prompt != null) prompt.Hide();
                current = null;
                waitForRelease = true;
                return;
            }

            NpcInteractable nearest = FindNearest();
            if (nearest != current)
            {
                current = nearest;
                holdTimer = 0f;
                if (prompt != null)
                {
                    if (current != null) prompt.Show("Mantén  E  ·  Hablar con " + current.DisplayName);
                    else prompt.Hide();
                }
            }
            if (input != null) input.BlockRadiusUp = current != null;
            if (current == null || input == null) return;

            if (!input.InteractHeld)
            {
                waitForRelease = false;
                holdTimer = 0f;
            }
            else if (!waitForRelease)
            {
                holdTimer += Time.unscaledDeltaTime;
                if (holdTimer >= holdTime)
                {
                    holdTimer = 0f;
                    waitForRelease = true;
                    current.Interact(transform);
                }
            }

            if (prompt != null) prompt.SetFill(holdTimer / holdTime);
        }

        private NpcInteractable FindNearest()
        {
            NpcInteractable best = null;
            float bestDistance = interactRadius * interactRadius;
            var all = NpcInteractable.All;
            for (int i = 0; i < all.Count; i++)
            {
                NpcInteractable npc = all[i];
                if (!npc.CanInteract) continue;
                Vector3 offset = npc.transform.position - transform.position;
                offset.y = 0f;
                float d = offset.sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = npc;
                }
            }
            return best;
        }
    }
}
