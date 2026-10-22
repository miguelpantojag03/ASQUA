using System;
using UnityEngine;

namespace Ascua.Core
{
    /// <summary>
    /// Ajustes de accesibilidad mínimos, guardados en PlayerPrefs:
    ///  - Tamaño del texto de los diálogos.
    ///  - Sacudidas de cámara (activar/desactivar).
    ///  - Subtítulos de los sonidos narrativos clave.
    ///  - Reducir destellos (viñeta de daño, aberración cromática y destellos del combate).
    /// Los sistemas leen estos valores y escuchan Changed para reaccionar en vivo.
    /// </summary>
    public static class AccessibilitySettings
    {
        private const string TextScaleKey = "ascua_texto_dialogo";
        private const string ShakeKey = "ascua_sacudidas";
        private const string SubtitlesKey = "ascua_subtitulos_sfx";
        private const string FlashesKey = "ascua_reducir_destellos";

        public const float MinTextScale = 0.8f;
        public const float MaxTextScale = 1.5f;

        /// <summary>Se dispara al cambiar cualquier ajuste.</summary>
        public static event Action Changed;

        private static bool loaded;
        private static float dialogueTextScale = 1f;
        private static bool cameraShake = true;
        private static bool sfxSubtitles;
        private static bool reduceFlashes;

        public static float DialogueTextScale { get { Load(); return dialogueTextScale; } }
        public static bool CameraShake { get { Load(); return cameraShake; } }
        public static bool SfxSubtitles { get { Load(); return sfxSubtitles; } }

        /// <summary>True = efectos de pantalla del combate suavizados.</summary>
        public static bool ReduceFlashes { get { Load(); return reduceFlashes; } }

        /// <summary>Multiplicador para destellos y efectos de pantalla (1, o 0.3 si se reducen).</summary>
        public static float FlashScale => ReduceFlashes ? 0.3f : 1f;

        public static void SetReduceFlashes(bool value)
        {
            Load();
            reduceFlashes = value;
            PlayerPrefs.SetInt(FlashesKey, value ? 1 : 0);
            Changed?.Invoke();
        }

        public static void SetDialogueTextScale(float value)
        {
            Load();
            dialogueTextScale = Mathf.Clamp(value, MinTextScale, MaxTextScale);
            PlayerPrefs.SetFloat(TextScaleKey, dialogueTextScale);
            Changed?.Invoke();
        }

        public static void SetCameraShake(bool value)
        {
            Load();
            cameraShake = value;
            PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
            Changed?.Invoke();
        }

        public static void SetSfxSubtitles(bool value)
        {
            Load();
            sfxSubtitles = value;
            PlayerPrefs.SetInt(SubtitlesKey, value ? 1 : 0);
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            dialogueTextScale = PlayerPrefs.GetFloat(TextScaleKey, 1f);
            cameraShake = PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            sfxSubtitles = PlayerPrefs.GetInt(SubtitlesKey, 0) == 1;
            reduceFlashes = PlayerPrefs.GetInt(FlashesKey, 0) == 1;
        }
    }
}
