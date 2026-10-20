using System;
using System.Collections;
using System.IO;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ascua.UI
{
    /// <summary>
    /// Modo fotografía (desde el resumen de la noche): cámara libre, HUD oculto, filtros y captura.
    ///  WASD / stick izq.: mover · Ratón / stick der.: mirar · Q/E: bajar/subir · Rueda: zoom
    ///  F: filtro · Espacio / Enter / botón A: capturar · Esc / B: salir.
    /// Las fotos se guardan en la carpeta "Fotos" de los datos del juego (la ruta sale en pantalla).
    /// </summary>
    public class PhotoMode : MonoBehaviour
    {
        public static PhotoMode Instance { get; private set; }

        [SerializeField] private Canvas photoCanvas;
        [SerializeField] private CanvasGroup photoUI;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Volume filterVolume;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float lookSensitivity = 0.12f;

        private static readonly string[] FilterNames = { "Sin filtro", "Recuerdo cálido", "Blanco y negro", "Noche fría" };

        private bool active;
        private Camera cam;
        private CinemachineBrain brain;
        private Canvas[] hiddenCanvases;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedFov;
        private Action onExit;
        private float yaw, pitch;
        private int filter;
        private ColorAdjustments colorAdjustments;
        private Vignette vignette;
        private string lastSaved = "";

        public bool IsActive => active;

        private void Awake()
        {
            Instance = this;
            if (photoUI != null) photoUI.alpha = 0f;
            if (filterVolume != null)
            {
                filterVolume.weight = 0f;
                // .profile crea una copia en memoria: no se modifica el asset.
                filterVolume.profile.TryGet(out colorAdjustments);
                filterVolume.profile.TryGet(out vignette);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Entra en modo foto. onExitCallback se llama al salir (vuelve a mostrar el resumen).</summary>
        public void Enter(Action onExitCallback)
        {
            if (active) return;
            cam = Camera.main;
            if (cam == null) return;

            active = true;
            onExit = onExitCallback;
            brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            savedPosition = cam.transform.position;
            savedRotation = cam.transform.rotation;
            savedFov = cam.fieldOfView;
            Vector3 euler = cam.transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;

            hiddenCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (Canvas c in hiddenCanvases)
            {
                if (c != photoCanvas && c.isRootCanvas) c.enabled = false;
            }
            if (photoCanvas != null) photoCanvas.enabled = true;
            if (photoUI != null) photoUI.alpha = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetFilter(0);
        }

        private void Exit()
        {
            active = false;
            cam.transform.SetPositionAndRotation(savedPosition, savedRotation);
            cam.fieldOfView = savedFov;
            if (brain != null) brain.enabled = true;
            foreach (Canvas c in hiddenCanvases)
            {
                if (c != null) c.enabled = true;
            }
            if (photoUI != null) photoUI.alpha = 0f;
            SetFilter(0);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            onExit?.Invoke();
        }

        private void Update()
        {
            if (!active) return;
            float dt = Time.unscaledDeltaTime;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame))
            {
                Exit();
                return;
            }

            // Mirar
            Vector2 look = Vector2.zero;
            if (mouse != null) look += mouse.delta.ReadValue() * lookSensitivity;
            if (gamepad != null) look += gamepad.rightStick.ReadValue() * (120f * dt);
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Mover
            Vector3 move = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) move += Vector3.forward;
                if (keyboard.sKey.isPressed) move += Vector3.back;
                if (keyboard.aKey.isPressed) move += Vector3.left;
                if (keyboard.dKey.isPressed) move += Vector3.right;
                if (keyboard.eKey.isPressed) move += Vector3.up;
                if (keyboard.qKey.isPressed) move += Vector3.down;
            }
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                move += new Vector3(stick.x, gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue(), stick.y);
            }
            float speed = moveSpeed * (keyboard != null && keyboard.leftShiftKey.isPressed ? 3f : 1f);
            cam.transform.position += cam.transform.TransformDirection(move) * (speed * dt);

            // Zoom
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) cam.fieldOfView = Mathf.Clamp(cam.fieldOfView - Mathf.Sign(scroll) * 3f, 20f, 80f);
            }

            if ((keyboard != null && keyboard.fKey.wasPressedThisFrame) || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)) SetFilter((filter + 1) % FilterNames.Length);

            bool capture = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                           || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            if (capture) StartCoroutine(Capture());

            UpdateInfo();
        }

        private IEnumerator Capture()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Fotos");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "ascua_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

            // La foto sale sin la interfaz del modo foto.
            if (photoUI != null) photoUI.alpha = 0f;
            yield return null;
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            if (photoUI != null) photoUI.alpha = 1f;
            lastSaved = path;
        }

        private void SetFilter(int index)
        {
            filter = index;
            if (filterVolume == null) return;
            filterVolume.weight = index == 0 ? 0f : 1f;
            if (colorAdjustments == null) return;

            switch (index)
            {
                case 1: // Recuerdo cálido
                    colorAdjustments.saturation.value = -25f;
                    colorAdjustments.colorFilter.value = new Color(1f, 0.86f, 0.68f);
                    colorAdjustments.contrast.value = 8f;
                    if (vignette != null) vignette.intensity.value = 0.45f;
                    break;
                case 2: // Blanco y negro
                    colorAdjustments.saturation.value = -100f;
                    colorAdjustments.colorFilter.value = Color.white;
                    colorAdjustments.contrast.value = 22f;
                    if (vignette != null) vignette.intensity.value = 0.4f;
                    break;
                case 3: // Noche fría
                    colorAdjustments.saturation.value = -10f;
                    colorAdjustments.colorFilter.value = new Color(0.78f, 0.86f, 1.1f);
                    colorAdjustments.contrast.value = 12f;
                    if (vignette != null) vignette.intensity.value = 0.35f;
                    break;
            }
        }

        private void UpdateInfo()
        {
            if (infoText == null) return;
            string saved = string.IsNullOrEmpty(lastSaved) ? "" : "\n<color=#9FD8A0>Foto guardada: " + lastSaved + "</color>";
            infoText.text = "MODO FOTO · Filtro: " + FilterNames[filter] +
                            "\nWASD mover · Ratón mirar · Q/E bajar/subir · Rueda zoom · F filtro · Espacio capturar · Esc salir" + saved;
        }
    }
}
