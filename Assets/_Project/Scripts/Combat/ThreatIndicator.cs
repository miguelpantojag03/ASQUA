using System.Collections.Generic;
using Ascua.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.Combat
{
    /// <summary>
    /// "Amenaza detectada": la primera vez que cada criatura entra en el radio de detección (14 m),
    /// un pulso violeta aparece en el borde de la pantalla en su dirección (aunque esté fuera de
    /// cámara o detrás) y suena un aviso grave (con subtítulo si está activado). Solo una vez por
    /// aparición de cada criatura: avisa sin saturar. Reutiliza 3 marcadores (sin instanciar).
    /// </summary>
    public class ThreatIndicator : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform[] markers;
        [SerializeField] private float detectRange = 14f;
        [SerializeField] private float pulseSeconds = 1.4f;
        [SerializeField, Range(0f, 0.3f)] private float edgeMargin = 0.06f;
        [SerializeField] private float scanInterval = 0.15f;

        private readonly HashSet<ShadowCreature> announced = new HashSet<ShadowCreature>();
        private readonly List<ShadowCreature> forget = new List<ShadowCreature>();
        private CanvasGroup[] groups;
        private Transform[] followTargets;
        private float[] timers;
        private Camera cam;
        private Transform player;
        private float scanTimer;

        /// <summary>Avisos mostrados en la partida (pruebas).</summary>
        public int ShownCount { get; private set; }

        private void Awake()
        {
            int n = markers != null ? markers.Length : 0;
            groups = new CanvasGroup[n];
            followTargets = new Transform[n];
            timers = new float[n];
            for (int i = 0; i < n; i++)
            {
                groups[i] = markers[i].GetComponent<CanvasGroup>();
                if (groups[i] != null) groups[i].alpha = 0f;
            }
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (player == null && PlayerCombatState.Instance != null) player = PlayerCombatState.Instance.transform;
            if (cam == null || player == null) return;

            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                Scan();
            }
            AnimateMarkers(Time.unscaledDeltaTime);
        }

        private void Scan()
        {
            // Olvida las criaturas que ya no están vivas (para avisar de nuevo si reaparecen).
            forget.Clear();
            foreach (ShadowCreature c in announced) if (c == null || !c.IsAlive) forget.Add(c);
            foreach (ShadowCreature c in forget) announced.Remove(c);

            var targets = LightTargetRegistry.All;
            float r2 = detectRange * detectRange;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!(targets[i] is ShadowCreature c) || !c.IsAlive || announced.Contains(c)) continue;
                if ((c.transform.position - player.position).sqrMagnitude > r2) continue;
                announced.Add(c);
                Show(c.transform);
            }
        }

        private void Show(Transform target)
        {
            int slot = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < timers.Length; i++)
            {
                if (timers[i] <= 0f) { slot = i; oldest = -1f; break; }
                if (timers[i] < oldest) { oldest = timers[i]; slot = i; }
            }
            if (timers.Length == 0) return;
            followTargets[slot] = target;
            timers[slot] = pulseSeconds;
            ShownCount++;
            EventBus.RaiseThreatDetected(target.position);
        }

        private void AnimateMarkers(float dt)
        {
            for (int i = 0; i < timers.Length; i++)
            {
                if (timers[i] <= 0f || followTargets[i] == null)
                {
                    if (groups[i] != null) groups[i].alpha = 0f;
                    continue;
                }
                timers[i] -= dt;
                float t = 1f - Mathf.Clamp01(timers[i] / pulseSeconds);
                // Dos latidos: crece y se desvanece.
                float beat = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f));
                if (groups[i] != null) groups[i].alpha = (1f - t) * Mathf.Lerp(0.45f, 1f, beat);
                markers[i].localScale = Vector3.one * Mathf.Lerp(0.7f, 1.25f, beat);
                Place(markers[i], followTargets[i].position + Vector3.up);
            }
        }

        /// <summary>Coloca el marcador en el borde de la pantalla, en la dirección de la criatura.</summary>
        private void Place(RectTransform marker, Vector3 world)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f) dir = -dir;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            float m = 0.5f - edgeMargin;
            float scale = m / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            Vector2 edge = new Vector2(0.5f, 0.5f) + dir * scale;
            Vector2 size = canvasRect.rect.size;
            marker.anchoredPosition = new Vector2((edge.x - 0.5f) * size.x, (edge.y - 0.5f) * size.y);
            marker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
