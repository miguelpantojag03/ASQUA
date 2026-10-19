using System.Collections.Generic;
using Ascua.Lighting;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.World
{
    /// <summary>
    /// Pool de luciérnagas. Las luciérnagas ya existen en la escena como hijos inactivos
    /// (las crea el constructor), así que en juego solo se activan y desactivan: cero
    /// Instantiate/Destroy y cero basura. Aparecen en puntos caminables y a oscuras.
    /// </summary>
    public class FireflyPool : MonoBehaviour
    {
        [Header("Ritmo")]
        [SerializeField] private float initialDelay = 3f;
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private int maxActive = 5;

        [Header("Área de aparición (mundo)")]
        [SerializeField] private Vector3 areaCenter = new Vector3(-22f, 0f, 4f);
        [SerializeField] private Vector3 areaSize = new Vector3(80f, 0f, 36f);
        [SerializeField] private float spawnHeight = 1.1f;
        [Tooltip("No aparecer pegadas al jugador.")]
        [SerializeField] private float minDistanceFromPlayer = 6f;
        [SerializeField] private int attemptsPerSpawn = 10;

        private readonly Queue<Firefly> available = new Queue<Firefly>();
        private readonly List<Firefly> active = new List<Firefly>();
        private Transform player;
        private float timer;

        public int ActiveCount => active.Count;

        private void Awake()
        {
            foreach (Firefly firefly in GetComponentsInChildren<Firefly>(true))
            {
                firefly.gameObject.SetActive(false);
                available.Enqueue(firefly);
            }
            timer = spawnInterval - initialDelay;
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < spawnInterval) return;
            if (active.Count >= maxActive || available.Count == 0) return;

            if (TryFindSpawnPoint(out Vector3 point))
            {
                Firefly firefly = available.Dequeue();
                active.Add(firefly);
                firefly.Activate(this, point);
                timer = 0f;
            }
        }

        /// <summary>Devuelve una luciérnaga al pool (recogida o caducada).</summary>
        public void Release(Firefly firefly)
        {
            if (!active.Remove(firefly)) return;
            firefly.gameObject.SetActive(false);
            available.Enqueue(firefly);
        }

        private bool TryFindSpawnPoint(out Vector3 point)
        {
            for (int i = 0; i < attemptsPerSpawn; i++)
            {
                Vector3 candidate = areaCenter + new Vector3(
                    (Random.value - 0.5f) * areaSize.x,
                    0f,
                    (Random.value - 0.5f) * areaSize.z);

                // Solo sobre suelo caminable (el NavMesh descarta interiores de edificios).
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                candidate = hit.position + Vector3.up * spawnHeight;

                if (player != null && (candidate - player.position).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer) continue;

                // Solo en zonas oscuras: ni el Farol ni la Penumbra (allí ya no queda nada).
                if (IlluminationRegistry.IsPointIlluminated(candidate, out _, false)) continue;
                if (Penumbra.Instance != null && Penumbra.Instance.IsCovered(candidate)) continue;

                point = candidate;
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 1f, 0.55f, 0.6f);
            Gizmos.DrawWireCube(areaCenter + Vector3.up * spawnHeight, new Vector3(areaSize.x, 0.1f, areaSize.z));
        }
    }
}
