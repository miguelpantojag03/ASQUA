using Ascua.Controls;
using Ascua.Core;
using Ascua.Narrative;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>
    /// Elige lo más cercano con lo que se puede interactuar (personajes y objetos recogibles, vía
    /// InteractableRegistry) y muestra su aviso: "Mantén E · Hablar con Mina" o "E · Recoger Postal".
    /// Hablar requiere mantener la tecla un instante; recoger es una pulsación.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        private IInteractable current;
        private float holdTimer;
        private bool waitForRelease;

        public IInteractable Current => current;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            InteractionPrompt prompt = InteractionPrompt.Instance;
            DialogueRunner runner = DialogueRunner.Instance;
            if ((runner != null && runner.IsOpen) || GameSession.IsModalOpen)
            {
                if (prompt != null) prompt.Hide();
                current = null;
                waitForRelease = true;
                return;
            }

            IInteractable nearest = FindNearest();
            if (nearest != current)
            {
                current = nearest;
                holdTimer = 0f;
                if (prompt != null)
                {
                    if (current != null)
                    {
                        string key = current.InteractionHoldTime > 0f ? "Mantén  E  ·  " : "E  ·  ";
                        prompt.Show(key + current.InteractionVerb + " " + current.InteractionName);
                    }
                    else
                    {
                        prompt.Hide();
                    }
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
                if (holdTimer >= current.InteractionHoldTime)
                {
                    holdTimer = 0f;
                    waitForRelease = true;
                    IInteractable target = current;
                    current = null; // Se recalcula el siguiente frame (el objeto puede desaparecer).
                    if (prompt != null) prompt.Hide();
                    target.Interact(transform);
                    return;
                }
            }

            if (prompt != null) prompt.SetFill(current.InteractionHoldTime > 0f ? holdTimer / current.InteractionHoldTime : 0f);
        }

        private IInteractable FindNearest()
        {
            IInteractable best = null;
            float bestDistance = float.MaxValue;
            var all = InteractableRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                IInteractable candidate = all[i];
                if (candidate == null || !candidate.CanInteract) continue;
                Vector3 offset = candidate.InteractionPoint - transform.position;
                offset.y = 0f;
                float d = offset.sqrMagnitude;
                float r = candidate.InteractionRadius;
                if (d <= r * r && d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
