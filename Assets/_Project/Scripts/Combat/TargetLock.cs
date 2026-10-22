using System;
using Ascua.Controls;
using Ascua.Core;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Fijar objetivo (T / clic central / R3). Pensado para peleas con varias sombras:
    ///  - Al pulsar, fija la sombra más cercana al centro de la pantalla (dentro de 'lockRange').
    ///  - Pulsar otra vez la suelta.
    ///  - Si la sombra fijada se disuelve, salta sola a la siguiente más cercana (la pelea no se corta).
    ///  - El Barrido y el ataque cargado apuntan a la sombra fijada aunque esté fuera del cono normal.
    /// La cámara NO gira sola (más cómodo y respeta a quien se marea): solo se marca el objetivo
    /// con una flecha ámbar sobre la criatura.
    /// </summary>
    public class TargetLock : MonoBehaviour
    {
        public static TargetLock Instance { get; private set; }

        [Header("Reglas")]
        [Tooltip("Distancia máxima para fijar una sombra.")]
        [SerializeField] private float lockRange = 12f;
        [Tooltip("Si la sombra fijada se aleja más que esto, se suelta sola.")]
        [SerializeField] private float breakRange = 16f;
        [Tooltip("Al disolverse la fijada, busca otra en este radio.")]
        [SerializeField] private float autoSwitchRange = 9f;

        [Header("Marcador (HUD)")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform marker;
        [SerializeField] private CanvasGroup markerGroup;
        [Tooltip("Altura del marcador sobre el centro de la criatura.")]
        [SerializeField] private float markerHeight = 1.1f;

        [Header("Referencias (se buscan solas)")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Camera viewCamera;

        /// <summary>Se dispara al fijar (objetivo) o soltar (null).</summary>
        public event Action<ILightTarget> TargetChanged;

        public ILightTarget Current { get; private set; }
        public bool HasTarget => Current != null && Current.CanBeHit;
        /// <summary>Veces que se ha fijado un objetivo (para la prueba automática).</summary>
        public int LockCount { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (markerGroup != null) markerGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (input != null && input.LockOnPressed) Toggle();

            if (Current != null)
            {
                float distance = Current.CanBeHit ? FlatDistance(Current.TargetPosition) : float.MaxValue;
                if (!Current.CanBeHit)
                {
                    // Se disolvió: seguir con la más cercana, si hay pelea alrededor.
                    SetTarget(FindBest(autoSwitchRange, false));
                }
                else if (distance > breakRange)
                {
                    SetTarget(null);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateMarker();
        }

        /// <summary>Fija la mejor sombra o suelta la actual.</summary>
        public void Toggle()
        {
            if (HasTarget) SetTarget(null);
            else SetTarget(FindBest(lockRange, true));
        }

        /// <summary>Fija un objetivo concreto (lo usa la prueba automática).</summary>
        public void SetTarget(ILightTarget target)
        {
            if (target == Current) return;
            Current = target;
            if (target != null)
            {
                LockCount++;
                EventBus.RaiseAbilityUsed("fijar_objetivo");
            }
            TargetChanged?.Invoke(target);
        }

        /// <summary>
        /// Puntuación = distancia + penalización por estar lejos del centro de la pantalla.
        /// Así se fija la sombra que el jugador está mirando, no la que tiene a la espalda.
        /// </summary>
        private ILightTarget FindBest(float range, bool preferScreenCenter)
        {
            Camera cam = GetCamera();
            ILightTarget best = null;
            float bestScore = float.MaxValue;
            var all = LightTargetRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                ILightTarget t = all[i];
                if (t == null || !t.CanBeHit || t is ShadowBolt) continue;
                float d = FlatDistance(t.TargetPosition);
                if (d > range) continue;
                float score = d;
                if (preferScreenCenter && cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(t.TargetPosition);
                    if (vp.z <= 0f) score += 30f; // detrás de la cámara: solo si no hay otra
                    else score += Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f)) * 10f;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            return best;
        }

        private float FlatDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private Camera GetCamera()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            return viewCamera;
        }

        private void UpdateMarker()
        {
            if (marker == null || markerGroup == null) return;
            Camera cam = GetCamera();
            bool show = HasTarget && cam != null;
            Vector3 screen = Vector3.zero;
            if (show)
            {
                screen = cam.WorldToScreenPoint(Current.TargetPosition + Vector3.up * markerHeight);
                show = screen.z > 0f;
            }
            // Aparece y desaparece rápido (tiempo real: también se ve en el freeze-frame).
            float target = show ? 1f : 0f;
            markerGroup.alpha = Mathf.MoveTowards(markerGroup.alpha, target, Time.unscaledDeltaTime * 8f);
            if (!show) return;

            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
            {
                marker.anchoredPosition = local;
            }
            // Latido suave (sin destello: con "reducir destellos" casi no se mueve).
            float beat = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f * AccessibilitySettings.FlashScale;
            marker.localScale = new Vector3(beat, beat, 1f);
        }
    }
}
