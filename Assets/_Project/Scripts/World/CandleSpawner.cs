using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.World
{
    /// <summary>
    /// Pool de velas de Teo (habilidad de Vínculo). Las velas existen en la escena desactivadas;
    /// cuando la habilidad está activa, cada intervalo enciende una en un punto caminable de su zona.
    /// </summary>
    public class CandleSpawner : MonoBehaviour
    {
        [SerializeField] private float interval = 30f;
        [SerializeField] private float radius = 9f;
        [SerializeField] private bool activeOnStart = false;

        private readonly Queue<CandlePickup> available = new Queue<CandlePickup>();
        private readonly List<CandlePickup> active = new List<CandlePickup>();
        private bool spawning;
        private float timer;

        private void Awake()
        {
            foreach (CandlePickup candle in GetComponentsInChildren<CandlePickup>(true))
            {
                candle.gameObject.SetActive(false);
                available.Enqueue(candle);
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
            if (!spawning) return;
            timer += Time.deltaTime;
            if (timer < interval || available.Count == 0) return;
            timer = 0f;

            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return;

            CandlePickup candle = available.Dequeue();
            active.Add(candle);
            candle.Activate(this, hit.position);
        }

        public void Release(CandlePickup candle)
        {
            if (!active.Remove(candle)) return;
            candle.gameObject.SetActive(false);
            available.Enqueue(candle);
        }
    }
}
