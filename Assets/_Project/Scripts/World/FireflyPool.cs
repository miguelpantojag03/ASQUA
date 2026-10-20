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

        [Header("Zonas preferentes (misiones, Vínculo con Mina)")]
        [Tooltip("Probabilidad de que una aparición se haga dentro de una zona preferente, si hay alguna.")]
        [SerializeField, Range(0f, 1f)] private float hotspotChance = 0.7f;

        private readonly Queue<Firefly> available = new Queue<Firefly>();
        private readonly List<Firefly> active = new List<Firefly>();
        // Zonas preferentes: clave -> (centro x, centro z, radio).
        private readonly Dictionary<string, Vector3> hotspots = new Dictionary<string, Vector3>();
        private readonly List<Vector3> hotspotList = new List<Vector3>();
        private Transform player;
        private float timer;

        public int ActiveCount => active.Count;

        /// <summary>Ajusta el ritmo de aparición (lo usa el NightQuestSet de cada noche).</summary>
        public void Configure(float interval, int maxSimultaneous)
        {
            spawnInterval = Mathf.Max(0.5f, interval);
            maxActive = Mathf.Max(1, maxSimultaneous);
        }

        /// <summary>Añade o actualiza una zona donde las luciérnagas prefieren aparecer.</summary>
        public void SetHotspot(string key, Vector3 center, float radius)
        {
            hotspots[key] = new Vector3(center.x, center.z, radius);
            RebuildHotspotList();
        }

        public void ClearHotspot(string key)
        {
            if (hotspots.Remove(key)) RebuildHotspotList();
        }

        private void RebuildHotspotList()
        {
            hotspotList.Clear();
            foreach (Vector3 h in hotspots.Values) hotspotList.Add(h);
        }

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

        /// <summary>Hace aparecer una luciérnaga en un punto concreto (eventos de misión, pruebas, IA).</summary>
        public bool SpawnAt(Vector3 position)
        {
            if (available.Count == 0) return false;
            Firefly firefly = available.Dequeue();
            active.Add(firefly);
            firefly.Activate(this, position);
            return true;
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
                Vector3 candidate;
                if (hotspotList.Count > 0 && Random.value < hotspotChance)
                {
                    // Punto aleatorio dentro de una zona preferente.
                    Vector3 h = hotspotList[Random.Range(0, hotspotList.Count)];
                    Vector2 offset = Random.insideUnitCircle * h.z;
                    candidate = new Vector3(h.x + offset.x, 0f, h.y + offset.y);
                }
                else
                {
                    candidate = areaCenter + new Vector3(
                        (Random.value - 0.5f) * areaSize.x,
                        0f,
                        (Random.value - 0.5f) * areaSize.z);
                }

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
