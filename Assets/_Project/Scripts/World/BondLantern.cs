using Ascua.Core;
using Ascua.Narrative;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Farolillo propio de un personaje con Vínculo alto (Noche 5, "Los farolillos"). Se crea por código
    /// junto al NPC: un farolillo que brilla, una luz cálida de ~6 m y el mismo anillo ámbar de los refugios.
    /// Mientras Ilse está dentro, la Esperanza de la zona más cercana sube (+1.5/s).
    /// Se enciende despacio (sin destellos) y con "reducir destellos" no titila.
    /// </summary>
    public class BondLantern : MonoBehaviour
    {
        [SerializeField] private float radius = 6f;
        [SerializeField] private float hopePerSecond = 1.5f;
        [SerializeField] private float lightIntensity = 2.6f;
        [SerializeField] private float fadeInSeconds = 1.5f;

        private Transform player;
        private Light glow;
        private Transform orb;
        private float lit;

        public float Radius => radius;
        public bool PlayerInside { get; private set; }

        /// <summary>Enciende el farolillo de ese NPC (si ya lo tenía, devuelve el mismo).</summary>
        public static BondLantern LightFor(NpcInteractable npc)
        {
            if (npc == null) return null;
            BondLantern existing = npc.GetComponent<BondLantern>();
            return existing != null ? existing : npc.gameObject.AddComponent<BondLantern>();
        }

        private void Awake()
        {
            BuildVisual();
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // pausa

            lit = Mathf.MoveTowards(lit, 1f, dt / Mathf.Max(0.05f, fadeInSeconds));
            float flicker = AccessibilitySettings.ReduceFlashes ? 1f : 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 1.7f, transform.position.x);
            if (glow != null) glow.intensity = lightIntensity * lit * flicker;
            if (orb != null) orb.localScale = Vector3.one * (0.22f * lit);

            PlayerInside = player != null && FlatDistance(player.position, transform.position) <= radius;
            if (!PlayerInside) return;
            ZoneHope zone = NearestLiveZone(player.position);
            if (zone != null) zone.AddHope(hopePerSecond * dt);
        }

        private void BuildVisual()
        {
            // El farolillo, junto a la mano del personaje.
            GameObject lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lantern.name = "Farolillo_Propio";
            Collider collider = lantern.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            lantern.transform.SetParent(transform, false);
            lantern.transform.localPosition = new Vector3(0.45f, 1.55f, 0.15f);
            lantern.transform.localScale = Vector3.zero;
            Renderer lanternRenderer = lantern.GetComponent<Renderer>();
            lanternRenderer.sharedMaterial = SafeHaven.SharedWarmMaterial();
            lanternRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            orb = lantern.transform;

            glow = lantern.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = SafeHaven.WarmColor;
            glow.range = radius;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;

            // El círculo donde la Esperanza sube (mismo anillo que los refugios, sin heredar la escala del NPC).
            GameObject ring = new GameObject("Farolillo_Anillo");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * 0.05f;
            Vector3 parentScale = transform.lossyScale;
            ring.transform.localScale = new Vector3(
                radius / Mathf.Max(0.01f, parentScale.x), 1f / Mathf.Max(0.01f, parentScale.y), radius / Mathf.Max(0.01f, parentScale.z));
            ring.AddComponent<MeshFilter>().sharedMesh = SafeHaven.SharedRingMesh();
            MeshRenderer ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = SafeHaven.SharedWarmMaterial();
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
        }

        private static ZoneHope NearestLiveZone(Vector3 position)
        {
            ZoneHope best = null;
            float bestDistance = float.MaxValue;
            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] == null || zones[i].IsDead) continue;
                float d = (zones[i].Center - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = zones[i];
                }
            }
            return best;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
