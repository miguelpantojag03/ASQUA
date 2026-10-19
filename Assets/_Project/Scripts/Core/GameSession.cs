using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.Core
{
    /// <summary>
    /// Estado básico de la sesión en la escena jugable: bloqueo del cursor.
    /// Esc libera el cursor; un click en la ventana lo vuelve a bloquear.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [SerializeField] private bool lockCursorOnStart = true;

        private void Start()
        {
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
            }
        }

        private void OnDestroy()
        {
            SetCursorLocked(false);
        }

        /// <summary>Bloquea u oculta el cursor del sistema.</summary>
        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
