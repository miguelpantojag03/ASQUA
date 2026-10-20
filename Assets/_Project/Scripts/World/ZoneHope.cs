using System;
using System.Collections;
using System.Collections.Generic;
using Ascua.Lighting;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Esperanza de una zona (0-100). Sube mientras una fuente de luz activa (el Farol)
    /// está dentro de la zona y baja cuando nadie la ilumina; la Penumbra acelera la caída.
    /// Las ventanas y farolas de la zona se encienden por umbrales: a más Esperanza, más luces.
    /// Si llega a 0 la zona muere: todo se apaga y queda bloqueado hasta la siguiente noche.
    /// </summary>
    public class ZoneHope : MonoBehaviour
    {
        private static readonly List<ZoneHope> all = new List<ZoneHope>();

        [Header("Identidad")]
        [SerializeField] private string zoneId = "Plaza";
        [SerializeField] private string displayName = "La Plaza";
        [Tooltip("Radio (m) del área de la zona, medido desde este objeto.")]
        [SerializeField] private float radius = 14f;

        [Header("Esperanza")]
        [SerializeField, Range(0f, 100f)] private float startHope = 60f;
        [SerializeField] private float maxHope = 100f;
        [Tooltip("Esperanza perdida por segundo cuando la zona está a oscuras.")]
        [SerializeField] private float decayPerSecond = 1.5f;
        [Tooltip("Esperanza ganada por segundo mientras el Farol activo está dentro de la zona.")]
        [SerializeField] private float risePerSecond = 4f;
        [Tooltip("Multiplicador de la caída cuando la Penumbra cubre la zona por completo.")]
        [SerializeField] private float penumbraDecayMultiplier = 2f;

        [Header("Umbrales de luces (Esperanza 0-100)")]
        [SerializeField] private Vector2 windowThresholdRange = new Vector2(10f, 95f);
        [SerializeField] private Vector2 lampThresholdRange = new Vector2(45f, 85f);
        [Tooltip("Margen para que las luces no parpadeen al oscilar alrededor de un umbral.")]
        [SerializeField] private float hysteresis = 3f;

        [Header("Muerte de la zona")]
        [Tooltip("Tiempo total de la cascada de apagado (las luces mueren una tras otra).")]
        [SerializeField] private float deathCascadeDuration = 2.5f;
        [Tooltip("Duración del fundido de cada ventana/farola al morir la zona.")]
        [SerializeField] private float deathFadeDuration = 1.8f;

        /// <summary>(zona, esperanza actual)</summary>
        public event Action<ZoneHope, float> HopeChanged;
        public event Action<ZoneHope> Died;
        public event Action<ZoneHope, bool> IlluminatedChanged;
        public static event Action<ZoneHope> AnyZoneDied;

        private readonly List<WindowLight> windows = new List<WindowLight>();
        private readonly List<float> windowThresholds = new List<float>();
        private readonly List<StreetLampToggle> lamps = new List<StreetLampToggle>();
        private readonly List<float> lampThresholds = new List<float>();

        /// <summary>Multiplicador global de la caída (lo sube la ráfaga de la Penumbra).</summary>
        public static float GlobalDecayMultiplier { get; set; } = 1f;

        /// <summary>(zona, nivel 1-4) cuando la Esperanza cruza 25/50/75/100 hacia arriba.</summary>
        public event Action<ZoneHope, int> HopeLevelReached;

        private float hope;
        private int hopeLevel;
        private bool isDead;
        private bool isIlluminated;
        private float penumbraOverlap;

        public static IReadOnlyList<ZoneHope> All => all;

        /// <summary>Busca una zona por id (Plaza, Mercado...). Null si no existe.</summary>
        public static ZoneHope Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].zoneId == id) return all[i];
            }
            return null;
        }

        public string ZoneId => zoneId;
        public string DisplayName => displayName;
        public float Radius => radius;
        public Vector3 Center => transform.position;
        public float Hope => hope;
        public float MaxHope => maxHope;
        public float Normalized => hope / maxHope;
        public bool IsDead => isDead;
        public bool IsIlluminated => isIlluminated;
        /// <summary>0 = la Penumbra no toca la zona, 1 = la cubre entera.</summary>
        public float PenumbraOverlap => penumbraOverlap;
        public int WindowCount => windows.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            AnyZoneDied = null;
            GlobalDecayMultiplier = 1f;
        }

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(string id, string nameToShow, float zoneRadius)
        {
            zoneId = id;
            displayName = nameToShow;
            radius = zoneRadius;
        }

        /// <summary>
        /// Ajusta el ritmo (lo llama NightManager según el preset Sutil / Demo rápida).
        /// Debe llamarse antes de Start para que la Esperanza inicial se aplique.
        /// </summary>
        public void ApplyPacing(float initialHope, float decay, float rise)
        {
            startHope = initialHope;
            decayPerSecond = decay;
            risePerSecond = rise;
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        private void Start()
        {
            hope = Mathf.Clamp(startHope, 0f, maxHope);
            hopeLevel = Mathf.FloorToInt(hope / maxHope * 4f + 0.001f);
            GlobalDecayMultiplier = 1f;
            CollectLights();
            ApplyThresholds(true);
            HopeChanged?.Invoke(this, hope);
        }

        private void Update()
        {
            if (isDead) return;

            bool illuminated = IsLitByAnySource();
            if (illuminated != isIlluminated)
            {
                isIlluminated = illuminated;
                IlluminatedChanged?.Invoke(this, illuminated);
            }

            penumbraOverlap = Penumbra.Instance != null ? Penumbra.Instance.GetOverlap(Center, radius) : 0f;

            float rate = illuminated
                ? risePerSecond
                : -decayPerSecond * Mathf.Lerp(1f, penumbraDecayMultiplier, penumbraOverlap) * GlobalDecayMultiplier;

            SetHope(hope + rate * Time.deltaTime);
        }

        /// <summary>Suma (o resta) Esperanza desde otros sistemas (Fase 3: Pulso, Vínculos...).</summary>
        public void AddHope(float amount)
        {
            if (isDead) return;
            SetHope(hope + amount);
        }

        private void SetHope(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, maxHope);
            if (Mathf.Approximately(clamped, hope)) return;

            hope = clamped;
            HopeChanged?.Invoke(this, hope);

            if (hope <= 0f) Die();
            else
            {
                ApplyThresholds(false);
                CheckHopeLevel();
            }
        }

        /// <summary>
        /// Micro-recompensa: al subir de nivel (cada 25%) avisa una vez. Para volver a avisar del mismo
        /// nivel, la Esperanza tiene que bajar 5 puntos por debajo (evita repeticiones al oscilar).
        /// </summary>
        private void CheckHopeLevel()
        {
            float levelSize = maxHope / 4f;
            int level = Mathf.FloorToInt(hope / levelSize + 0.001f);
            if (level > hopeLevel)
            {
                hopeLevel = level;
                HopeLevelReached?.Invoke(this, level);
                Ascua.Core.EventBus.RaiseHopeLevelReached(zoneId, level, Center);
            }
            else if (hope < hopeLevel * levelSize - 5f)
            {
                hopeLevel = Mathf.Max(0, hopeLevel - 1);
            }
        }

        private bool IsLitByAnySource()
        {
            IReadOnlyList<IIlluminationSource> sources = IlluminationRegistry.Sources;
            for (int i = 0; i < sources.Count; i++)
            {
                IIlluminationSource source = sources[i];
                if (source == null || !source.IsIlluminating) continue;

                Vector3 offset = source.LightPosition - Center;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            isIlluminated = false;

            // Se bloquea todo ya, para que nada vuelva a encenderse durante la cascada.
            foreach (WindowLight window in windows) window.SetLocked(true);
            foreach (StreetLampToggle lamp in lamps) lamp.SetLocked(true);
            StartCoroutine(DeathCascade());

            Died?.Invoke(this);
            AnyZoneDied?.Invoke(this);
        }

        /// <summary>Las luces encendidas mueren una tras otra, en orden aleatorio, y las farolas al final.</summary>
        private IEnumerator DeathCascade()
        {
            List<WindowLight> litWindows = new List<WindowLight>();
            foreach (WindowLight window in windows)
            {
                if (window.IsLit) litWindows.Add(window);
            }
            for (int i = litWindows.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (litWindows[i], litWindows[j]) = (litWindows[j], litWindows[i]);
            }

            int steps = Mathf.Max(1, litWindows.Count + 1);
            WaitForSeconds wait = new WaitForSeconds(deathCascadeDuration / steps);

            foreach (WindowLight window in litWindows)
            {
                window.Extinguish(deathFadeDuration);
                yield return wait;
            }

            foreach (StreetLampToggle lamp in lamps) lamp.TurnOff(deathFadeDuration);

            // Por si alguna ventana se estaba encendiendo justo en ese momento.
            foreach (WindowLight window in windows) window.Extinguish(deathFadeDuration);
        }

        /// <summary>Busca las ventanas y farolas con el mismo ZoneId y les reparte umbrales.</summary>
        private void CollectLights()
        {
            windows.Clear();
            lamps.Clear();

            foreach (WindowLight window in FindObjectsByType<WindowLight>(FindObjectsSortMode.None))
            {
                if (window.ZoneId != zoneId) continue;
                window.SetHopeControlled(true);
                windows.Add(window);
            }
            foreach (StreetLampToggle lamp in FindObjectsByType<StreetLampToggle>(FindObjectsSortMode.None))
            {
                if (lamp.ZoneId == zoneId) lamps.Add(lamp);
            }

            // Orden estable pero "salteado" para que las luces se repartan entre edificios.
            windows.Sort((a, b) => PositionHash(a.transform.position).CompareTo(PositionHash(b.transform.position)));
            lamps.Sort((a, b) => PositionHash(a.transform.position).CompareTo(PositionHash(b.transform.position)));

            FillThresholds(windowThresholds, windows.Count, windowThresholdRange);
            FillThresholds(lampThresholds, lamps.Count, lampThresholdRange);
        }

        private static void FillThresholds(List<float> thresholds, int count, Vector2 range)
        {
            thresholds.Clear();
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                thresholds.Add(Mathf.Lerp(range.x, range.y, t));
            }
        }

        private static int PositionHash(Vector3 p)
        {
            unchecked
            {
                int h = Mathf.RoundToInt(p.x * 10f) * 73856093 ^ Mathf.RoundToInt(p.y * 10f) * 19349663 ^ Mathf.RoundToInt(p.z * 10f) * 83492791;
                return h & 0x7fffffff;
            }
        }

        /// <summary>Enciende o apaga cada luz según la Esperanza actual.</summary>
        private void ApplyThresholds(bool instant)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                float threshold = windowThresholds[i];
                WindowLight window = windows[i];
                if (!window.IsLit && hope >= threshold + hysteresis) window.Ignite();
                else if (window.IsLit && hope < threshold - hysteresis) window.Extinguish();
            }

            for (int i = 0; i < lamps.Count; i++)
            {
                float threshold = lampThresholds[i];
                StreetLampToggle lamp = lamps[i];
                if (!lamp.IsOn && hope >= threshold + hysteresis) lamp.SetOn(true, instant);
                else if (lamp.IsOn && hope < threshold - hysteresis) lamp.SetOn(false, instant);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.77f, 0.42f, 0.8f);
            const int segments = 48;
            Vector3 previous = Center + new Vector3(radius, 0.2f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = Center + new Vector3(Mathf.Cos(a) * radius, 0.2f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
