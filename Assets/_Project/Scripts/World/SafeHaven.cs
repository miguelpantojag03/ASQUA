using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Quests;
using Ascua.UI;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Refugio de la Cacería del Heraldo: el círculo de luz del farolillo de un NPC (Mina, Teo, Perpetua).
    /// Se crea por código al empezar la escena (EnsureCreated), sin tocar escenas ni prefabs.
    ///  - Cuenta como refugio si el Vínculo con ese NPC es &gt;= minBond. Si ninguno llega,
    ///    vale el de mayor Vínculo: siempre hay al menos uno.
    ///  - Solo se ve durante una cacería: anillo ámbar en el suelo + luz cálida, para leerlo de lejos.
    ///  - Si Ilse se queda holdSeconds dentro, el Heraldo se detiene al borde y la cacería termina
    ///    con éxito: +energía, +1 de Vínculo con el NPC y un aviso ("Mina te cubre con su luz").
    /// </summary>
    public class SafeHaven : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>NPC con farolillo propio que pueden dar refugio.</summary>
        public static readonly string[] LanternNpcIds = { "mina", "teo", "perpetua" };
        private const string HeraldoId = "heraldo";

        private static readonly List<SafeHaven> all = new List<SafeHaven>();
        private static Material sharedMaterial;
        private static Mesh sharedRing;
        private static bool huntVisible;

        [SerializeField] private float radius = 4.5f;
        [Tooltip("Vínculo mínimo para que este NPC dé refugio.")]
        [SerializeField] private int minBond = 1;
        [Tooltip("Segundos que Ilse debe quedarse dentro para terminar la cacería.")]
        [SerializeField] private float holdSeconds = 2f;
        [SerializeField] private float energyReward = 20f;
        [SerializeField] private int bondReward = 1;
        [SerializeField] private Color warmColor = new Color(1f, 0.62f, 0.22f);

        private NpcInteractable npc;
        private GameObject visual;
        private Light glow;
        private Transform player;
        private PlayerEnergy playerEnergy;
        private float stayTimer;

        public static IReadOnlyList<SafeHaven> All => all;
        public string NpcId => npc != null ? npc.NpcId : name;
        public string DisplayName => npc != null ? npc.DisplayName : name;
        public float Radius => radius;
        public Vector3 Center => transform.position;
        /// <summary>0-1: cuánto lleva Ilse dentro respecto a lo necesario.</summary>
        public float StayProgress => holdSeconds > 0f ? Mathf.Clamp01(stayTimer / holdSeconds) : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            huntVisible = false;
        }

        /// <summary>Añade un SafeHaven a cada NPC con farolillo de la escena (si aún no lo tiene).</summary>
        public static void EnsureCreated()
        {
            IReadOnlyList<NpcInteractable> npcs = NpcInteractable.All;
            for (int i = 0; i < npcs.Count; i++)
            {
                NpcInteractable candidate = npcs[i];
                if (candidate == null || candidate.NpcId == HeraldoId) continue;
                if (System.Array.IndexOf(LanternNpcIds, candidate.NpcId) < 0) continue;
                if (candidate.GetComponent<SafeHaven>() == null) candidate.gameObject.AddComponent<SafeHaven>();
            }
        }

        /// <summary>Muestra u oculta los refugios (solo se ven durante una cacería).</summary>
        public static void SetHuntVisible(bool visible)
        {
            huntVisible = visible;
            for (int i = 0; i < all.Count; i++) all[i].RefreshVisual();
        }

        /// <summary>True si este NPC da refugio ahora (Vínculo suficiente, o el de mayor Vínculo si nadie llega).</summary>
        public bool IsRefuge
        {
            get
            {
                QuestManager manager = QuestManager.Instance;
                if (manager == null) return true;
                if (manager.GetBond(NpcId) >= minBond) return true;
                return BestFallback() == this;
            }
        }

        /// <summary>El refugio válido más cercano a 'position' (o null si no hay NPC con farolillo).</summary>
        public static SafeHaven NearestRefuge(Vector3 position)
        {
            SafeHaven best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsRefuge) continue;
                float d = FlatDistance(all[i].Center, position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = all[i];
                }
            }
            return best;
        }

        /// <summary>El refugio válido que contiene 'position', o null.</summary>
        public static SafeHaven RefugeContaining(Vector3 position)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Contains(position) && all[i].IsRefuge) return all[i];
            }
            return null;
        }

        public bool Contains(Vector3 position)
        {
            return FlatDistance(Center, position) <= radius;
        }

        /// <summary>Punto del borde del refugio más cercano a 'from' (ahí se detiene el Heraldo).</summary>
        public Vector3 EdgePointToward(Vector3 from, float margin)
        {
            Vector3 dir = from - Center;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            return Center + dir.normalized * (radius + margin);
        }

        /// <summary>Si nadie llega al Vínculo mínimo, el de mayor Vínculo (empate: el primero).</summary>
        private static SafeHaven BestFallback()
        {
            QuestManager manager = QuestManager.Instance;
            SafeHaven best = null;
            int bestBond = int.MinValue;
            for (int i = 0; i < all.Count; i++)
            {
                int bond = manager != null ? manager.GetBond(all[i].NpcId) : 0;
                if (bond >= all[i].minBond) return null;   // hay un refugio "de verdad": no hace falta suplente
                if (bond > bestBond)
                {
                    bestBond = bond;
                    best = all[i];
                }
            }
            return best;
        }

        private void Awake()
        {
            npc = GetComponent<NpcInteractable>();
            BuildVisual();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            RefreshVisual();
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        private void Start()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                playerEnergy = playerObject.GetComponent<PlayerEnergy>();
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // en pausa no avanza nada

            RefreshVisual();
            UpdateGlow();

            Heraldo_AI heraldo = Heraldo_AI.Instance;
            bool hunting = heraldo != null && heraldo.IsHunting;
            if (!hunting || player == null || !IsRefuge || !Contains(player.position))
            {
                stayTimer = 0f;
                return;
            }

            stayTimer += dt;
            if (stayTimer >= holdSeconds) Shelter(heraldo);
        }

        /// <summary>Ilse aguantó dentro: fin de la cacería con recompensa.</summary>
        private void Shelter(Heraldo_AI heraldo)
        {
            stayTimer = 0f;
            heraldo.EndHunt(true);
            if (playerEnergy != null) playerEnergy.Restore(energyReward);
            if (QuestManager.Instance != null && bondReward != 0) QuestManager.Instance.AddBond(NpcId, bondReward);
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(DisplayName + " te cubre con su luz", 2.8f);
        }

        // ================================================================== Visual

        private void RefreshVisual()
        {
            bool show = huntVisible && isActiveAndEnabled && IsRefuge;
            if (visual != null && visual.activeSelf != show) visual.SetActive(show);
        }

        private void UpdateGlow()
        {
            if (glow == null || visual == null || !visual.activeSelf) return;
            // Late despacio (sin destellos: con "reducir destellos" queda fijo).
            float pulse = AccessibilitySettings.ReduceFlashes ? 1f : 0.85f + 0.15f * Mathf.Sin(Time.time * 2f);
            glow.intensity = 3.2f * pulse;
        }

        private void BuildVisual()
        {
            visual = new GameObject("Refugio_Visual");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.up * 0.06f;
            // El anillo no gira con el NPC ni hereda su escala.
            visual.transform.localRotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            visual.transform.localScale = new Vector3(
                radius / Mathf.Max(0.01f, parentScale.x), 1f / Mathf.Max(0.01f, parentScale.y), radius / Mathf.Max(0.01f, parentScale.z));

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = RingMesh();
            MeshRenderer ringRenderer = visual.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = RingMaterial(warmColor);
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            GameObject lightObject = new GameObject("Refugio_Luz");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 2.2f;
            lightObject.transform.SetParent(visual.transform, true);
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = warmColor;
            glow.range = radius * 2.2f;
            glow.intensity = 3.2f;
            glow.shadows = LightShadows.None;

            visual.SetActive(false);
        }

        /// <summary>Color cálido por defecto de los anillos.</summary>
        public static readonly Color WarmColor = new Color(1f, 0.62f, 0.22f);

        /// <summary>Malla del anillo (radio 1) para otros círculos de luz (p. ej. los farolillos de la Noche 5).</summary>
        public static Mesh SharedRingMesh() => RingMesh();

        /// <summary>Material ámbar emisivo compartido (el mismo de los refugios).</summary>
        public static Material SharedWarmMaterial() => RingMaterial(WarmColor);

        /// <summary>Material ámbar emisivo creado una sola vez y compartido por todos los refugios.</summary>
        private static Material RingMaterial(Color color)
        {
            if (sharedMaterial != null) return sharedMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            sharedMaterial = new Material(shader) { name = "Refugio_Ambar (runtime)" };
            Color hdr = color * 2.5f;
            hdr.a = 1f;
            if (sharedMaterial.HasProperty(BaseColorId)) sharedMaterial.SetColor(BaseColorId, hdr);
            if (sharedMaterial.HasProperty(ColorId)) sharedMaterial.SetColor(ColorId, hdr);
            if (sharedMaterial.HasProperty(EmissionColorId))
            {
                sharedMaterial.EnableKeyword("_EMISSION");
                sharedMaterial.SetColor(EmissionColorId, hdr);
            }
            return sharedMaterial;
        }

        /// <summary>Anillo plano de radio 1 (se escala al radio del refugio), creado una vez.</summary>
        private static Mesh RingMesh()
        {
            if (sharedRing != null) return sharedRing;
            const int segments = 64;
            const float inner = 0.9f;
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = dir * inner;
                vertices[i * 2 + 1] = dir;

                int a = i * 2, b = i * 2 + 1;
                int c = ((i + 1) % segments) * 2, d = ((i + 1) % segments) * 2 + 1;
                int t = i * 6;
                // Cara hacia arriba.
                triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
                triangles[t + 3] = b; triangles[t + 4] = c; triangles[t + 5] = d;
            }
            sharedRing = new Mesh { name = "Refugio_Anillo" };
            sharedRing.vertices = vertices;
            sharedRing.triangles = triangles;
            sharedRing.RecalculateNormals();
            sharedRing.RecalculateBounds();
            return sharedRing;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
