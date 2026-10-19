using System;
using Ascua.Lighting;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Luciérnaga recogible: flota, brilla y al tocarla devuelve energía al Farol.
    /// La gestiona FireflyPool: nunca se instancia ni destruye en tiempo de juego.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Firefly : MonoBehaviour
    {
        [SerializeField] private float energyAmount = 8f;
        [Tooltip("Segundos que permanece antes de desvanecerse si nadie la recoge.")]
        [SerializeField] private float lifetime = 20f;
        [SerializeField] private float bobAmplitude = 0.25f;
        [SerializeField] private float bobSpeed = 1.6f;
        [SerializeField] private float driftRadius = 0.5f;
        [SerializeField] private ParticleSystem sparkles;
        [Tooltip("Malla emisiva central; se escala para aparecer y desvanecerse.")]
        [SerializeField] private Transform glow;

        /// <summary>Se dispara al recoger cualquier luciérnaga (sonido, logros... en el futuro).</summary>
        public static event Action<Firefly> AnyCollected;

        private FireflyPool pool;
        private Vector3 anchor;
        private Vector3 glowBaseScale = Vector3.one;
        private float age;
        private float seed;

        public float EnergyAmount => energyAmount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyCollected = null;
        }

        private void Awake()
        {
            if (glow != null) glowBaseScale = glow.localScale;
        }

        /// <summary>La activa en una posición (llamado por el pool).</summary>
        public void Activate(FireflyPool owner, Vector3 position)
        {
            pool = owner;
            anchor = position;
            age = 0f;
            seed = UnityEngine.Random.value * 100f;
            transform.position = position;
            gameObject.SetActive(true);

            if (sparkles != null)
            {
                sparkles.Clear(true);
                sparkles.Play(true);
            }
        }

        private void Update()
        {
            age += Time.deltaTime;

            // Aparece y se desvanece suavemente.
            float fade = Mathf.Clamp01(age / 0.6f) * Mathf.Clamp01((lifetime - age) / 1.5f);

            float t = Time.time * bobSpeed + seed;
            Vector3 offset = new Vector3(
                Mathf.Sin(t * 0.7f) * driftRadius,
                Mathf.Sin(t) * bobAmplitude,
                Mathf.Cos(t * 0.53f) * driftRadius);
            transform.position = anchor + offset;

            if (glow != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f + seed);
                glow.localScale = glowBaseScale * (fade * pulse);
            }

            if (age >= lifetime) ReturnToPool();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerEnergy energy = other.GetComponentInParent<PlayerEnergy>();
            if (energy == null) return;

            energy.Restore(energyAmount);
            AnyCollected?.Invoke(this);
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (pool != null) pool.Release(this);
            else gameObject.SetActive(false);
        }
    }
}
