using Ascua.Core;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Vela explosiva de Teo (Vínculo 5): una vela que vuela en arco y estalla en un estallido de luz
    /// cálida (radio 4 m, 3 de daño, aturde 3 s). Se reutiliza (un único objeto).
    /// </summary>
    public class TeoCandleBomb : MonoBehaviour
    {
        [SerializeField] private float flightSeconds = 0.6f;
        [SerializeField] private float arcHeight = 2.2f;
        [SerializeField] private float radius = 4f;
        [SerializeField] private int damage = 3;
        [SerializeField] private float stunSeconds = 3f;
        [SerializeField] private ParticleSystem burst;
        [SerializeField] private Light burstLight;
        [SerializeField] private Transform candleVisual;

        private Vector3 start;
        private Vector3 end;
        private float timer = -1f;
        private float lightTimer;

        public bool IsBusy => timer >= 0f || lightTimer > 0f;

        private void Awake()
        {
            if (burstLight != null) burstLight.enabled = false;
            if (candleVisual != null) candleVisual.gameObject.SetActive(false);
        }

        public void Throw(Vector3 from, Vector3 to)
        {
            start = from;
            end = to;
            timer = 0f;
            transform.position = from;
            gameObject.SetActive(true);
            if (candleVisual != null) candleVisual.gameObject.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (timer >= 0f)
            {
                timer += dt;
                float t = Mathf.Clamp01(timer / flightSeconds);
                Vector3 p = Vector3.Lerp(start, end, t);
                p.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                transform.position = p;
                if (candleVisual != null) candleVisual.Rotate(400f * dt, 0f, 0f, Space.Self);
                if (t >= 1f) Explode();
            }

            if (lightTimer > 0f && burstLight != null)
            {
                lightTimer -= dt;
                burstLight.intensity = Mathf.Max(0f, lightTimer / 0.5f) * 6f * AccessibilitySettings.FlashScale;
                if (lightTimer <= 0f) burstLight.enabled = false;
            }
        }

        private void Explode()
        {
            timer = -1f;
            if (candleVisual != null) candleVisual.gameObject.SetActive(false);
            if (burst != null) burst.Emit(60);
            if (burstLight != null)
            {
                burstLight.enabled = true;
                lightTimer = 0.5f;
            }

            HitInfo hit = new HitInfo(damage, 3f, transform.position, HitKind.VelaTeo, stunSeconds);
            var targets = LightTargetRegistry.InSphere(transform.position, radius);
            for (int i = targets.Count - 1; i >= 0; i--) targets[i].ReceiveLight(hit);
            if (CombatFx.Instance != null) CombatFx.Instance.Flash(transform.position, 3f, new Color(1f, 0.75f, 0.45f));
            EventBus.RaiseAbilityUsed("vela_teo_estallido");
        }
    }
}
