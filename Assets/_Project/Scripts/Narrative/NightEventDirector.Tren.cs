using System.Collections;
using System.Collections.Generic;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.World;
using UnityEngine;
using UnityEngine.AI;

namespace Ascua.Narrative
{
    /// <summary>
    /// Noche 3 · "El tren fantasma". Por el borde norte, fuera de la zona jugable, pasa en silencio un tren
    /// hecho de luz (8 ventanas azul pálido) en ~14 s. Silbido grave con subtítulo "[Un tren lejano]".
    /// Mientras pasa aparecen 4 luciérnagas doradas en fila cerca de la vía: recogerlas todas da la
    /// página 7 del Diario y energía. Sin mover la cámara; con "reducir destellos", sin parpadeo.
    /// </summary>
    public partial class NightEventDirector
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Material trainMaterial;

        private const int TrainWindows = 8;
        private const float TrainSeconds = 14f;
        private const int TrainFireflies = 4;
        private const float FireflyEnergy = 8f;
        private const float PageEnergyBonus = 15f;
        private static readonly Color TrainColor = new Color(0.62f, 0.8f, 1f);
        private static readonly Color FireflyGold = new Color(1f, 0.82f, 0.35f);

        partial void BeginTren()
        {
            StartCoroutine(TrenRoutine());
        }

        private IEnumerator TrenRoutine()
        {
            ComputeMapBounds(out Vector3 min, out Vector3 max, out float groundY);

            // La vía: 22 m al norte del barrio más al norte, de lado a lado (y algo más) del mapa.
            float trackZ = max.z + 22f;
            Vector3 from = new Vector3(min.x - 40f, groundY + 3f, trackZ);
            Vector3 to = new Vector3(max.x + 40f, groundY + 3f, trackZ);

            GameObject train = BuildTrain(out List<Renderer> windows, out Light glow);
            train.transform.position = from;

            List<PickupItem> fireflies = SpawnTrainFireflies(min, max);
            StartCoroutine(WatchTrainFireflies(fireflies));

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            bool whistle = false, echo = false, thought = false;
            float t = 0f;
            while (t < TrainSeconds)
            {
                t += Time.deltaTime;   // la pausa congela el tren
                float k = Mathf.Clamp01(t / TrainSeconds);
                train.transform.position = Vector3.Lerp(from, to, k);

                // Aparece y se desvanece en 1.5 s por cada extremo (las ventanas "se encienden").
                float fade = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((TrainSeconds - t) / 1.5f);
                for (int i = 0; i < windows.Count; i++)
                {
                    // Un temblor muy suave por ventana; con "reducir destellos", luz fija.
                    float shimmer = AccessibilitySettings.ReduceFlashes ? 1f : 0.85f + 0.15f * Mathf.PerlinNoise(t * 1.3f, i * 0.37f);
                    windows[i].GetPropertyBlock(block);
                    Color c = TrainColor * (2.2f * fade * shimmer);
                    c.a = 1f;
                    block.SetColor(BaseColorId, c);
                    block.SetColor(ColorId, c);
                    windows[i].SetPropertyBlock(block);
                }
                if (glow != null) glow.intensity = 2.5f * fade;

                if (!whistle && t >= 1f)
                {
                    whistle = true;
                    EventBus.RaiseAbilityUsed("tren_silbido");
                }
                if (!echo && t >= 1.9f)
                {
                    echo = true;
                    EventBus.RaiseAbilityUsed("tren_silbido_eco");
                }
                if (!thought && t >= 8.5f)
                {
                    thought = true;
                    Toast("Nadie bajó. Nadie subió. Aun así, el tren pasó.", 3.4f);
                }
                yield return null;
            }

            Destroy(train);
            FinishEvent();
        }

        /// <summary>Hilera de ventanas de luz (quads sin colisión ni sombras) + una luz que viaja con ellas.</summary>
        private GameObject BuildTrain(out List<Renderer> windows, out Light glow)
        {
            GameObject root = new GameObject("TrenFantasma");
            windows = new List<Renderer>(TrainWindows);
            Material material = TrainMaterial();
            for (int i = 0; i < TrainWindows; i++)
            {
                GameObject window = GameObject.CreatePrimitive(PrimitiveType.Quad);
                window.name = "Ventana_" + (i + 1);
                Collider collider = window.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                window.transform.SetParent(root.transform, false);
                // De dos en dos, como vagones: 2.6 m entre ventanas y 1.6 m más entre vagones.
                float x = -i * 2.6f - (i / 2) * 1.6f;
                window.transform.localPosition = new Vector3(x, 0f, 0f);
                // El quad mira hacia el sur (hacia la ciudad).
                window.transform.localRotation = Quaternion.identity;
                window.transform.localScale = new Vector3(1.8f, 1.1f, 1f);
                Renderer renderer = window.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                windows.Add(renderer);
            }

            GameObject lightObject = new GameObject("TrenFantasma_Luz");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(-TrainWindows * 1.6f, 0.5f, -2f);
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = TrainColor;
            glow.range = 22f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            return root;
        }

        /// <summary>Material azul pálido sin iluminación, por las dos caras. Se crea una vez y se reutiliza.</summary>
        private static Material TrainMaterial()
        {
            if (trainMaterial != null) return trainMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            trainMaterial = new Material(shader) { name = "TrenFantasma (runtime)" };
            Color c = TrainColor * 2.2f;
            c.a = 1f;
            if (trainMaterial.HasProperty(BaseColorId)) trainMaterial.SetColor(BaseColorId, c);
            if (trainMaterial.HasProperty(ColorId)) trainMaterial.SetColor(ColorId, c);
            if (trainMaterial.HasProperty("_Cull")) trainMaterial.SetFloat("_Cull", 0f);   // se ve por las dos caras
            return trainMaterial;
        }

        /// <summary>Rectángulo que ocupan los barrios (centros ± radio) y la altura media del suelo.</summary>
        private static void ComputeMapBounds(out Vector3 min, out Vector3 max, out float groundY)
        {
            min = new Vector3(-30f, 0f, -30f);
            max = new Vector3(30f, 0f, 30f);
            groundY = 0f;
            var zones = ZoneHope.All;
            if (zones.Count == 0) return;

            min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            max = new Vector3(float.MinValue, 0f, float.MinValue);
            for (int i = 0; i < zones.Count; i++)
            {
                Vector3 c = zones[i].Center;
                float r = zones[i].Radius;
                min.x = Mathf.Min(min.x, c.x - r);
                min.z = Mathf.Min(min.z, c.z - r);
                max.x = Mathf.Max(max.x, c.x + r);
                max.z = Mathf.Max(max.z, c.z + r);
                groundY += c.y;
            }
            groundY /= zones.Count;
        }

        /// <summary>4 luciérnagas doradas en fila cerca del borde norte (dentro de lo caminable).</summary>
        private List<PickupItem> SpawnTrainFireflies(Vector3 min, Vector3 max)
        {
            List<PickupItem> result = new List<PickupItem>(TrainFireflies);
            PickupItem template = FindGoldenFireflyTemplate();
            Vector3 center = player != null ? player.position : (min + max) * 0.5f;

            for (int i = 0; i < TrainFireflies; i++)
            {
                // Fila a lo ancho del borde norte; si ahí no hay suelo, se acerca hacia Ilse.
                float x = Mathf.Lerp(min.x, max.x, (i + 1f) / (TrainFireflies + 1f));
                Vector3 wanted = new Vector3(x, center.y, max.z - 3f);
                if (!TryGround(wanted, center, out Vector3 ground)) continue;

                PickupItem item = template != null
                    ? Instantiate(template, ground + Vector3.up * 1f, Quaternion.identity)
                    : CreateSimpleFirefly(ground + Vector3.up * 1f);
                item.name = "Luciernaga_Tren_" + (i + 1);
                item.Configure("luciernaga_tren", "una luciérnaga del tren", PickupKind.LuciernagaDorada, FireflyGold, null, null, FireflyEnergy, 0f, null);
                item.gameObject.SetActive(true);
                result.Add(item);
            }
            return result;
        }

        /// <summary>Busca suelo caminable cerca de 'wanted'; si no hay, prueba acercándose a 'fallback'.</summary>
        private static bool TryGround(Vector3 wanted, Vector3 fallback, out Vector3 ground)
        {
            for (int step = 0; step <= 5; step++)
            {
                Vector3 p = Vector3.Lerp(wanted, fallback, step / 5f);
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    ground = hit.position;
                    return true;
                }
            }
            ground = fallback;
            return false;
        }

        /// <summary>Una luciérnaga dorada de la escena para copiar su aspecto (o null si no hay ninguna).</summary>
        private static PickupItem FindGoldenFireflyTemplate()
        {
            var items = PickupItem.All;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].Kind == PickupKind.LuciernagaDorada && items[i].IsAvailable) return items[i];
            }
            return null;
        }

        /// <summary>Si la escena no tiene luciérnagas doradas que copiar: esfera dorada + luz, sin colisión.</summary>
        private static PickupItem CreateSimpleFirefly(Vector3 position)
        {
            GameObject root = new GameObject("Luciernaga_Tren");
            root.transform.position = position;
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider collider = orb.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            orb.transform.SetParent(root.transform, false);
            orb.transform.localScale = Vector3.one * 0.18f;
            Renderer renderer = orb.GetComponent<Renderer>();
            renderer.sharedMaterial = TrainMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Color gold = FireflyGold * 2f;
            gold.a = 1f;
            block.SetColor(BaseColorId, gold);
            block.SetColor(ColorId, gold);
            renderer.SetPropertyBlock(block);

            Light light = new GameObject("Brillo_Luz").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Point;
            light.color = FireflyGold;
            light.intensity = 1.2f;
            light.range = 2.6f;
            light.shadows = LightShadows.None;
            return root.AddComponent<PickupItem>();
        }

        /// <summary>Cuando Ilse recoge las 4: página 7 del Diario y un extra de energía.</summary>
        private IEnumerator WatchTrainFireflies(List<PickupItem> fireflies)
        {
            if (fireflies.Count == 0) yield break;
            while (true)
            {
                int left = 0;
                for (int i = 0; i < fireflies.Count; i++)
                {
                    if (fireflies[i] != null && fireflies[i].IsAvailable) left++;
                }
                if (left == 0) break;
                yield return null;
            }

            // Espera a que la última llegue al Farol (vuela ~0.5 s).
            yield return new WaitForSeconds(0.8f);
            PlayerEnergy energy = player != null ? player.GetComponent<PlayerEnergy>() : null;
            if (energy != null) energy.Restore(PageEnergyBonus);
            if (GameProgress.AddPage(RuntimeDiaryPages.TrenPageId))
            {
                Toast("Nueva página del Diario de Ilse · J para leer", 2.6f);
            }
        }
    }
}
