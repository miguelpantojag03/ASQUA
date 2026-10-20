using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.World
{
    /// <summary>
    /// Pool de velas de Teo (habilidad de Vínculo). Las velas son PickupItem de tipo Vela que existen
    /// en la escena desactivados; con la habilidad activa, cada intervalo aparece una en un punto
    /// caminable de El Mercado, y Ilse la recoge con E (energía + Esperanza para el Mercado).
    /// </summary>
    public class CandleSpawner : MonoBehaviour
    {
        [SerializeField] private float interval = 30f;
        [SerializeField] private float radius = 9f;
        [SerializeField] private float lifetime = 45f;
        [SerializeField] private bool activeOnStart = false;

        private readonly List<PickupItem> candles = new List<PickupItem>();
        private readonly List<float> spawnTimes = new List<float>();
        private bool spawning;
        private float timer;

        public bool IsActive => spawning;

        private void Awake()
        {
            foreach (PickupItem candle in GetComponentsInChildren<PickupItem>(true))
            {
                candle.gameObject.SetActive(false);
                candles.Add(candle);
                spawnTimes.Add(-999f);
            }
            spawning = activeOnStart;
        }

        /// <summary>Activa o desactiva la habilidad. Al activarse aparece una vela enseguida.</summary>
        public void SetActive(bool value)
        {
            if (value && !spawning) timer = interval;
            spawning = value;
        }

        private void Update()
        {
            // Las velas que nadie recoge se consumen solas.
            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].gameObject.activeSelf && Time.time - spawnTimes[i] > lifetime) candles[i].gameObject.SetActive(false);
            }

            if (!spawning) return;
            timer += Time.deltaTime;
            if (timer < interval) return;

            int free = candles.FindIndex(c => !c.gameObject.activeSelf);
            if (free < 0) return;

            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return;

            timer = 0f;
            spawnTimes[free] = Time.time;
            candles[free].ResetPickup(hit.position + Vector3.up * 0.05f);
        }
    }
}
