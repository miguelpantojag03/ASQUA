using Ascua.Lighting;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Vela de Teo: al recogerla da algo de energía al Farol y Esperanza a su zona.
    /// La gestiona CandleSpawner (pool, sin Instantiate/Destroy en juego).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class CandlePickup : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private float energyAmount = 6f;
        [SerializeField] private float hopeAmount = 8f;
        [SerializeField] private string zoneId = "Mercado";
        [SerializeField] private float lifetime = 40f;
        [SerializeField] private Renderer flame;
        [SerializeField] private Color flameColor = new Color(1f, 0.769f, 0.42f);

        private CandleSpawner owner;
        private float age;
        private float seed;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Activate(CandleSpawner spawner, Vector3 position)
        {
            owner = spawner;
            age = 0f;
            seed = Random.value * 50f;
            transform.position = position;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (flame != null)
            {
                float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 8f, seed);
                float fade = Mathf.Clamp01((lifetime - age) / 2f);
                flame.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, flameColor * (4f * flicker * fade));
                flame.SetPropertyBlock(propertyBlock);
            }
            if (age >= lifetime) Release();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerEnergy energy = other.GetComponentInParent<PlayerEnergy>();
            if (energy == null) return;

            energy.Restore(energyAmount);
            ZoneHope zone = ZoneHope.Find(zoneId);
            if (zone != null) zone.AddHope(hopeAmount);
            Release();
        }

        private void Release()
        {
            if (owner != null) owner.Release(this);
            else gameObject.SetActive(false);
        }
    }
}
