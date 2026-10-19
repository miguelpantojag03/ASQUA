using System;
using Ascua.Controls;
using Ascua.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ascua.Core
{
    /// <summary>Resultado de una noche, para la pantalla de resumen (y futuras mejoras entre noches).</summary>
    public struct NightResult
    {
        public int NightNumber;
        public int ZonesSaved;
        public int ZonesTotal;
        public bool EndedEarly;
    }

    /// <summary>
    /// Ciclo de una noche con temporizador. Al acabar (o si todas las zonas mueren antes)
    /// pausa el juego, libera el cursor y emite NightEnded con el resultado.
    /// La UI de resumen escucha ese evento; este script no conoce la UI.
    /// </summary>
    public class NightManager : MonoBehaviour
    {
        public static NightManager Instance { get; private set; }

        [SerializeField] private int nightNumber = 1;
        [Tooltip("Duración de la noche en segundos.")]
        [SerializeField] private float nightDuration = 60f;
        [Tooltip("Termina la noche antes si todas las zonas mueren.")]
        [SerializeField] private bool endWhenAllZonesDead = true;
        [SerializeField] private string titleSceneName = "Title";

        [Header("Referencias (se buscan solas si están vacías)")]
        [SerializeField] private PlayerInputReader playerInput;
        [SerializeField] private GameSession gameSession;

        public event Action<NightManager> NightStarted;
        public event Action<NightResult> NightEnded;

        public int NightNumber => nightNumber;
        public float Duration => nightDuration;
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, nightDuration - Elapsed);
        public float Progress01 => Mathf.Clamp01(Elapsed / nightDuration);
        public bool IsRunning { get; private set; }

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputReader>();
            if (gameSession == null) gameSession = FindFirstObjectByType<GameSession>();
        }

        private void Start()
        {
            Elapsed = 0f;
            IsRunning = true;
            NightStarted?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Nunca dejar el juego en pausa al salir de la escena.
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (!IsRunning) return;

            Elapsed += Time.deltaTime;
            bool timeUp = Elapsed >= nightDuration;
            bool allDead = endWhenAllZonesDead && AllZonesDead();
            if (timeUp || allDead) EndNight(allDead && !timeUp);
        }

        /// <summary>Termina la noche ahora mismo.</summary>
        public void EndNight(bool endedEarly)
        {
            if (!IsRunning) return;
            IsRunning = false;

            NightResult result = new NightResult
            {
                NightNumber = nightNumber,
                ZonesTotal = ZoneHope.All.Count,
                EndedEarly = endedEarly
            };
            foreach (ZoneHope zone in ZoneHope.All)
            {
                if (!zone.IsDead) result.ZonesSaved++;
            }

            // Pausa: se congela todo lo que usa Time.deltaTime; la UI usa tiempo sin escalar.
            Time.timeScale = 0f;
            if (playerInput != null) playerInput.SetGameplayEnabled(false);
            if (gameSession != null) gameSession.enabled = false;
            GameSession.SetCursorLocked(false);

            NightEnded?.Invoke(result);
        }

        /// <summary>Vuelve a cargar la escena actual para repetir la noche.</summary>
        public void RestartNight()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToTitle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
        }

        private static bool AllZonesDead()
        {
            if (ZoneHope.All.Count == 0) return false;
            foreach (ZoneHope zone in ZoneHope.All)
            {
                if (!zone.IsDead) return false;
            }
            return true;
        }
    }
}
