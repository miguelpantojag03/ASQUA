using UnityEngine;
using UnityEngine.InputSystem;

namespace Ascua.Core
{
    /// <summary>
    /// Estado básico de la sesión en la escena jugable: bloqueo del cursor.
    /// Esc libera el cursor; un click en la ventana lo vuelve a bloquear.
    /// Mientras haya una ventana "modal" abierta (diálogo, panel), el cursor queda libre y el click no lo bloquea.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [SerializeField] private bool lockCursorOnStart = true;

        private static int modalCount;

        /// <summary>Si hay menú de pausa, él gestiona Esc (este script solo bloquea el cursor con clic).</summary>
        public static bool EscapeHandledExternally { get; set; }
        private static bool lockWhenModalsClose = true;

        /// <summary>True si hay al menos una ventana modal abierta.</summary>
        public static bool IsModalOpen => modalCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            modalCount = 0;
            lockWhenModalsClose = true;
        }

        private void Start()
        {
            modalCount = 0;
            lockWhenModalsClose = lockCursorOnStart;
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        private void Update()
        {
            if (IsModalOpen) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !EscapeHandledExternally)
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

        /// <summary>Abre una ventana modal: libera el cursor hasta que se cierre.</summary>
        public static void PushModal()
        {
            modalCount++;
            SetCursorLocked(false);
        }

        /// <summary>Cierra una ventana modal; al cerrarse la última, el cursor vuelve a bloquearse.</summary>
        public static void PopModal()
        {
            modalCount = Mathf.Max(0, modalCount - 1);
            if (modalCount == 0 && lockWhenModalsClose) SetCursorLocked(true);
        }

        /// <summary>Bloquea u oculta el cursor del sistema.</summary>
        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
