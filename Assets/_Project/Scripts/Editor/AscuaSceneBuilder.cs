using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Player;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using TMPro;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Construye automáticamente las escenas (Título y Plaza, que incluye La Plaza y El Mercado),
    /// los prefabs de Ilse, Mina y Teo, el NavMesh y la configuración del proyecto ("Ascua").
    /// Menú: Ascua > Construir juego.
    /// </summary>
    public static class AscuaSceneBuilder
    {
        private const string TitleScenePath = AscuaAssetFactory.ScenesFolder + "/Title.unity";
        private const string PlazaScenePath = AscuaAssetFactory.ScenesFolder + "/Plaza.unity";
        private const string NavMeshPath = AscuaAssetFactory.ScenesFolder + "/NavMesh_Plaza.asset";
        private const int IgnoreRaycastLayer = 2;
        private const int UILayer = 5;

        // Zonas jugables.
        private const string PlazaZone = "Plaza";
        private const string MercadoZone = "Mercado";
        private static readonly Vector3 PlazaCenter = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 MercadoCenter = new Vector3(-44f, 0f, 2f);
        // Centro del mapa completo: hacia aquí se cierra la Penumbra.
        private static readonly Vector3 MapCenter = new Vector3(-22f, 0f, 4f);

        private enum Col { None, Keep, Mesh }

        private class ChibiParts
        {
            public GameObject Root;
            public Transform Visual, Rig, Head;
            public Animator Animator;
        }

        // ================================================================== Menú

        [MenuItem("Ascua/Construir juego (assets + escenas + NavMesh)", priority = 0)]
        public static void BuildAll()
        {
            if (!Validate()) return;

            AscuaAssets assets = AscuaAssetFactory.CreateAll();
            BuildPlaza(assets);
            BuildTitle(assets);
            ConfigureProject();

            EditorSceneManager.OpenScene(TitleScenePath);
            EditorUtility.DisplayDialog("Ascua", "Juego construido.\n\nEscenas: Title y Plaza (Assets/_Project/Scenes).\nPulsa Play desde Title.", "Vale");
        }

        /// <summary>
        /// Punto de entrada para línea de comandos, sin ventanas de diálogo:
        /// Unity.exe -batchmode -projectPath ... -executeMethod Ascua.EditorTools.AscuaSceneBuilder.BuildAllBatch -quit
        /// </summary>
        public static void BuildAllBatch()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                Debug.LogError("[Ascua] El proyecto no usa URP.");
                EditorApplication.Exit(1);
                return;
            }
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                Debug.LogError("[Ascua] Faltan los TMP Essential Resources.");
                EditorApplication.Exit(1);
                return;
            }

            AscuaAssets assets = AscuaAssetFactory.CreateAll();
            BuildPlaza(assets);
            BuildTitle(assets);
            ConfigureProject();
            Debug.Log("[Ascua] Juego construido correctamente.");
        }

        [MenuItem("Ascua/Reconstruir solo Plaza", priority = 20)]
        public static void RebuildPlaza()
        {
            if (!Validate()) return;
            BuildPlaza(AscuaAssetFactory.CreateAll());
            ConfigureProject();
        }

        [MenuItem("Ascua/Reconstruir solo Título", priority = 21)]
        public static void RebuildTitle()
        {
            if (!Validate()) return;
            BuildTitle(AscuaAssetFactory.CreateAll());
            ConfigureProject();
        }

        private static bool Validate()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                EditorUtility.DisplayDialog("Ascua", "El proyecto no usa URP. Crea el proyecto con la plantilla 'Universal 3D' (URP).", "Vale");
                return false;
            }

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("Ascua", "Faltan los recursos de TextMeshPro.\n\nWindow > TextMeshPro > Import TMP Essential Resources\ny vuelve a ejecutar el menú.", "Vale");
                return false;
            }

#if !ENABLE_INPUT_SYSTEM
            EditorUtility.DisplayDialog("Ascua", "El nuevo Input System no está activo.\n\nEdit > Project Settings > Player > Active Input Handling = 'Input System Package (New)' o 'Both'.", "Vale");
            return false;
#else
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
#endif
        }

        private static void ConfigureProject()
        {
            PlayerSettings.productName = "Ascua";
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(PlazaScenePath, true)
            };
            AssetDatabase.SaveAssets();
        }

        // ================================================================== Plaza

        private static void BuildPlaza(AscuaAssets assets)
        {
            AscuaMaterials m = assets.Materials;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.Random rng = new System.Random(7);

            // --- Cámara principal + Cinemachine Brain
            Camera mainCamera = CreateMainCamera(new Vector3(0f, 2.5f, -14f), Quaternion.Euler(10f, 0f, 0f));
            mainCamera.gameObject.AddComponent<CinemachineBrain>();

            // --- Atmósfera, luz de luna y Volume
            AtmosphereSettings atmosphere = new GameObject("Atmosfera").AddComponent<AtmosphereSettings>();
            atmosphere.Configure(AscuaPalette.Niebla, 0.028f,
                new Color(0.16f, 0.2f, 0.38f), new Color(0.1f, 0.12f, 0.25f), new Color(0.05f, 0.06f, 0.12f), mainCamera);

            CreateDirectionalLight("Luna", Quaternion.Euler(35f, 20f, 0f), AscuaPalette.Hex("#8FA3E0"), 0.25f, 0.7f);
            CreateGlobalVolume(assets.VolumeProfile);

            // --- Entorno estático
            Transform env = new GameObject("Entorno").transform;
            BuildPlazaGround(env, m);
            BuildFountain(env, m);

            // --- La Plaza. Alturas contrastadas (7.5 / 16 / 13 m) para un skyline escalonado.
            BuildBuilding(env, "Edificio_N1", new Vector3(-6f, 0f, 14.5f), 0f, 10f, 7f, 7.5f, m.Buildings[0], m, rng, 0, PlazaZone);
            BuildBuilding(env, "Edificio_N2", new Vector3(6.5f, 0f, 14.5f), 0f, 11f, 7f, 16f, m.Buildings[1], m, rng, -1, PlazaZone);
            BuildBuilding(env, "Edificio_E", new Vector3(14.5f, 0f, 1f), 90f, 13f, 7f, 13f, m.Buildings[2], m, rng, 1, PlazaZone);
            BuildBuilding(env, "Edificio_W", new Vector3(-14.5f, 0f, 2f), -90f, 12f, 7f, 8f, m.Buildings[3], m, rng, 3, PlazaZone);
            BuildBuilding(env, "Edificio_SE", new Vector3(12.5f, 0f, -11f), 90f, 8f, 6f, 6f, m.Buildings[4], m, rng, -1, PlazaZone);

            BuildStreetFurniture(env, m);

            // --- El Mercado (mismo kit modular), al oeste de La Plaza.
            BuildMercado(env, m, rng);

            BuildMalecon(env, m);
            BuildDistantSkyline(env, m, rng);
            BuildBoundaries(env);
            MarkStatic(env.gameObject);

            // --- Niebla baja a ras de suelo
            BuildGroundMist(m);

            // --- Sistemas de la noche: zonas, Penumbra, temporizador y luciérnagas
            ZoneHope plazaZone = BuildZone("Zona_Plaza", PlazaZone, "La Plaza", PlazaCenter, 14f);
            ZoneHope mercadoZone = BuildZone("Zona_Mercado", MercadoZone, "El Mercado", MercadoCenter, 13f);
            BuildPenumbra(m);
            NightManager night = new GameObject("NightManager").AddComponent<NightManager>();
            // El set de misiones es el "nivel" de esta noche (Fase 3).
            // Se recarga desde disco: el asset pudo reimportarse tras crearse y la referencia en memoria quedaría vacía.
            AssetDatabase.SaveAssets();
            Wire(night, "questSet", AssetDatabase.LoadAssetAtPath<NightQuestSet>(AscuaNarrativeContent.Night1Path));
            BuildFireflies(m);

            // --- Misiones: gestor persistente, habilidades de Vínculo, velas de Teo y puntos de misión
            new GameObject("QuestManager").AddComponent<QuestManager>();
            new GameObject("BondPerks").AddComponent<BondPerks>();
            BuildCandles(assets);
            new GameObject("Punto_Malecon_Mina").AddComponent<QuestPoint>().Configure("malecon_mina");
            GameObject.Find("Punto_Malecon_Mina").transform.position = new Vector3(4f, 0.1f, -13.8f);

            // --- Personajes
            GameObject player = BuildPlayer(assets, out Transform cameraTarget, out PlayerInputReader input);
            BuildMina(assets, plazaZone);
            BuildTeo(assets, mercadoZone);
            BuildHeraldo(assets);

            // --- Cámara en tercera persona
            BuildThirdPersonCamera(cameraTarget, input);

            // --- Sesión + HUD
            new GameObject("GameSession").AddComponent<GameSession>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            BuildHud(player, assets, night, new[] { plazaZone, mercadoZone });

            // --- Impacto al morir una zona: sacudida de cámara + sonido
            GameObject feedback = new GameObject("Feedback_ZonaApagada");
            CinemachineImpulseSource impulse = feedback.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseDuration = 0.8f;
            ZoneDeathFeedback deathFeedback = feedback.AddComponent<ZoneDeathFeedback>();
            Wire(deathFeedback, "impulseSource", impulse);

            // --- NavMesh (al final, con toda la geometría ya colocada)
            BakeNavMesh();

            EditorSceneManager.SaveScene(scene, PlazaScenePath);
        }

        private static void BuildPlazaGround(Transform env, AscuaMaterials m)
        {
            Prim(PrimitiveType.Cube, "Suelo_Calles", env, new Vector3(0f, -0.05f, 12f), new Vector3(60f, 0.1f, 56f), m.Ground);
            Prim(PrimitiveType.Cube, "Plaza_Pavimento", env, new Vector3(0f, 0.05f, 0f), new Vector3(22f, 0.1f, 22f), m.Paving);
            Prim(PrimitiveType.Cylinder, "Plaza_Roseton", env, new Vector3(0f, 0.105f, 0f), new Vector3(11f, 0.005f, 11f), m.PavingInset, Col.None);

            // Bordillos de la plaza.
            Prim(PrimitiveType.Cube, "Bordillo_N", env, new Vector3(0f, 0.06f, 11.1f), new Vector3(22.4f, 0.12f, 0.3f), m.Trim);
            Prim(PrimitiveType.Cube, "Bordillo_S", env, new Vector3(0f, 0.06f, -11.1f), new Vector3(22.4f, 0.12f, 0.3f), m.Trim);
            Prim(PrimitiveType.Cube, "Bordillo_E", env, new Vector3(11.1f, 0.06f, 0f), new Vector3(0.3f, 0.12f, 22.4f), m.Trim);
            Prim(PrimitiveType.Cube, "Bordillo_W", env, new Vector3(-11.1f, 0.06f, 0f), new Vector3(0.3f, 0.12f, 22.4f), m.Trim);

            // Aceras frente a las fachadas.
            Prim(PrimitiveType.Cube, "Acera_N", env, new Vector3(0f, 0.075f, 13.85f), new Vector3(40f, 0.15f, 1.3f), m.Sidewalk);
            Prim(PrimitiveType.Cube, "Acera_E", env, new Vector3(13.85f, 0.075f, -2f), new Vector3(1.3f, 0.15f, 26f), m.Sidewalk);
            Prim(PrimitiveType.Cube, "Acera_W", env, new Vector3(-13.85f, 0.075f, 2f), new Vector3(1.3f, 0.15f, 20f), m.Sidewalk);

            // Marcas viales discontinuas.
            for (float x = -20f; x <= 20f; x += 3f)
            {
                Prim(PrimitiveType.Cube, "MarcaVial", env, new Vector3(x, 0.01f, 12.4f), new Vector3(1.4f, 0.02f, 0.15f), m.LaneMark, Col.None);
            }
            for (float z = -12f; z <= 10f; z += 3f)
            {
                Prim(PrimitiveType.Cube, "MarcaVial", env, new Vector3(12.4f, 0.01f, z), new Vector3(0.15f, 0.02f, 1.4f), m.LaneMark, Col.None);
            }
        }

        private static void BuildFountain(Transform env, AscuaMaterials m)
        {
            Transform fountain = new GameObject("Fuente_Seca").transform;
            fountain.SetParent(env, false);
            Prim(PrimitiveType.Cylinder, "Pila", fountain, new Vector3(0f, 0.4f, 0f), new Vector3(5f, 0.3f, 5f), m.Stone, Col.Mesh);
            Prim(PrimitiveType.Cylinder, "AguaQuieta", fountain, new Vector3(0f, 0.62f, 0f), new Vector3(4.5f, 0.02f, 4.5f), m.Water, Col.None);
            Prim(PrimitiveType.Cylinder, "Columna", fountain, new Vector3(0f, 1.4f, 0f), new Vector3(0.5f, 0.8f, 0.5f), m.Stone);
            Prim(PrimitiveType.Cylinder, "Cuenco", fountain, new Vector3(0f, 2.2f, 0f), new Vector3(1.8f, 0.1f, 1.8f), m.Stone, Col.None);
            Prim(PrimitiveType.Sphere, "Remate", fountain, new Vector3(0f, 2.45f, 0f), Vector3.one * 0.35f, m.Stone, Col.None);
        }

        /// <summary>
        /// Edificio modular: la raíz está en el centro inferior de la fachada, que mira a -Z local.
        /// Las ventanas de las dos primeras plantas son reactivas (las controla la Esperanza de zoneId).
        /// glowColumn = columna de la planta baja con ventana de prueba + luz cálida (-1 = ninguna).
        /// </summary>
        private static void BuildBuilding(Transform env, string name, Vector3 position, float yaw,
            float width, float depth, float height, Material wall, AscuaMaterials m, System.Random rng, int glowColumn, string zoneId)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(env, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.gameObject.AddComponent<PenumbraFade>();

            Prim(PrimitiveType.Cube, "Muro", root, new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, depth), wall);
            Prim(PrimitiveType.Cube, "Cornisa", root, new Vector3(0f, height + 0.12f, depth * 0.5f), new Vector3(width + 0.4f, 0.25f, depth + 0.4f), m.Trim, Col.None);

            // Tejado a dos aguas: cubo girado 45º y aplastado por su pivote.
            Transform roofPivot = new GameObject("TejadoPivote").transform;
            roofPivot.SetParent(root, false);
            roofPivot.localPosition = new Vector3(0f, height + 0.24f, depth * 0.5f);
            roofPivot.localScale = new Vector3(1f, 0.55f, 1f);
            float side = depth / 1.4142f;
            Prim(PrimitiveType.Cube, "Tejado", roofPivot, Vector3.zero, new Vector3(width + 0.2f, side, side), m.Roof, Col.None, Quaternion.Euler(45f, 0f, 0f));

            Prim(PrimitiveType.Cube, "Chimenea", root, new Vector3(width * 0.3f, height + 1.4f, depth * 0.65f), new Vector3(0.6f, 1.6f, 0.6f), m.Trim, Col.None);

            int floors = Mathf.Max(1, Mathf.FloorToInt((height - 1f) / 3f));
            int columns = Mathf.Max(2, Mathf.FloorToInt((width - 1.5f) / 2.2f));
            float spacing = (width - 1.5f) / columns;
            float startX = -(width - 1.5f) * 0.5f + spacing * 0.5f;
            int doorColumn = columns / 2;

            for (int f = 1; f < floors; f++)
            {
                Prim(PrimitiveType.Cube, "Moldura", root, new Vector3(0f, f * 3f, -0.05f), new Vector3(width + 0.15f, 0.12f, 0.25f), m.Trim, Col.None);
            }

            for (int f = 0; f < floors; f++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float x = startX + c * spacing;
                    float y = 1.7f + f * 3f;

                    if (f == 0 && c == doorColumn)
                    {
                        Prim(PrimitiveType.Cube, "Puerta", root, new Vector3(x, 1.15f, -0.04f), new Vector3(1.2f, 2.3f, 0.12f), m.Door, Col.None);
                        Prim(PrimitiveType.Cube, "Dintel", root, new Vector3(x, 2.4f, -0.08f), new Vector3(1.5f, 0.15f, 0.2f), m.Trim, Col.None);
                        continue;
                    }

                    bool reactive = f <= 1;
                    bool testWindow = f == 0 && c == glowColumn;
                    Material windowMaterial = reactive ? m.WindowReactive : PickWindowMaterial(m, rng);

                    Prim(PrimitiveType.Cube, "Marco", root, new Vector3(x, y, -0.01f), new Vector3(1.25f, 1.55f, 0.04f), m.Trim, Col.None);
                    Prim(PrimitiveType.Cube, "Alfeizar", root, new Vector3(x, y - 0.75f, -0.1f), new Vector3(1.35f, 0.08f, 0.22f), m.Trim, Col.None);

                    // La ventana mira hacia fuera (forward = normal de la fachada).
                    GameObject window = Prim(PrimitiveType.Cube, testWindow ? "VentanaPrueba" : "Ventana", root,
                        new Vector3(x, y, -0.03f), new Vector3(1.05f, 1.35f, 0.08f), windowMaterial, Col.None, Quaternion.Euler(0f, 180f, 0f));

                    if (reactive) SetupReactiveWindow(window, root, testWindow, new Vector3(x, y, -0.9f), zoneId);
                }
            }
        }

        /// <summary>
        /// Ventanas estáticas de los pisos altos: solo apagadas o frías.
        /// La luz cálida queda reservada para la Esperanza (ventanas reactivas).
        /// </summary>
        private static Material PickWindowMaterial(AscuaMaterials m, System.Random rng)
        {
            return rng.NextDouble() < 0.65 ? m.WindowDark : m.WindowCold;
        }

        private static void SetupReactiveWindow(GameObject window, Transform buildingRoot, bool withGlow, Vector3 glowLocalPosition, string zoneId)
        {
            WindowLight windowLight = window.AddComponent<WindowLight>();
            windowLight.ConfigureZone(zoneId);
            if (!withGlow) return;

            // Solo las ventanas de prueba proyectan luz a la calle (pocas luces = buen rendimiento).
            GameObject glow = new GameObject("Resplandor");
            glow.transform.SetParent(buildingRoot, false);
            glow.transform.localPosition = glowLocalPosition;
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = AscuaPalette.Ambar;
            light.range = 5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            Wire(windowLight, "glowLight", light);
        }

        private static void BuildStreetFurniture(Transform env, AscuaMaterials m)
        {
            Transform furniture = new GameObject("Mobiliario").transform;
            furniture.SetParent(env, false);

            // Bancas alrededor de la fuente y en los bordes de la plaza.
            Bench(furniture, m, new Vector3(-5.5f, 0.1f, 0f), 90f);
            Bench(furniture, m, new Vector3(5.5f, 0.1f, 0f), -90f);
            Bench(furniture, m, new Vector3(0f, 0.1f, 5.5f), 180f);
            Bench(furniture, m, new Vector3(-8f, 0.1f, -9.5f), 0f);
            Bench(furniture, m, new Vector3(8f, 0.1f, 9.5f), 180f);

            // Farolas apagadas.
            Vector3[] lamps =
            {
                new Vector3(-10f, 0.1f, -10f), new Vector3(10f, 0.1f, -10f),
                new Vector3(-10f, 0.1f, 10f), new Vector3(10f, 0.1f, 10f),
                new Vector3(-16f, 0f, -14.8f), new Vector3(-6f, 0f, -14.8f),
                new Vector3(6f, 0f, -14.8f), new Vector3(16f, 0f, -14.8f)
            };
            foreach (Vector3 p in lamps) LampPost(furniture, m, p, PlazaZone);

            // Cajas, bolardos y papelera para romper la regularidad.
            Prim(PrimitiveType.Cube, "Caja", furniture, new Vector3(13.4f, 0.55f, 9.2f), new Vector3(0.8f, 0.8f, 0.8f), m.Wood, Col.Keep, Quaternion.Euler(0f, 12f, 0f));
            Prim(PrimitiveType.Cube, "Caja", furniture, new Vector3(13.2f, 0.35f, 10.2f), new Vector3(0.6f, 0.6f, 0.6f), m.Wood, Col.Keep, Quaternion.Euler(0f, -20f, 0f));
            Prim(PrimitiveType.Cube, "Caja", furniture, new Vector3(13.3f, 1.2f, 9.3f), new Vector3(0.5f, 0.5f, 0.5f), m.Wood, Col.Keep, Quaternion.Euler(0f, 35f, 0f));
            Prim(PrimitiveType.Cylinder, "Papelera", furniture, new Vector3(-10.6f, 0.5f, 3f), new Vector3(0.45f, 0.4f, 0.45f), m.Metal);
            for (float x = -9f; x <= 9f; x += 3f)
            {
                Prim(PrimitiveType.Cylinder, "Bolardo", furniture, new Vector3(x, 0.4f, -11.6f), new Vector3(0.22f, 0.3f, 0.22f), m.Metal);
            }
        }

        private static void Bench(Transform parent, AscuaMaterials m, Vector3 position, float yaw)
        {
            GameObject bench = new GameObject("Banca");
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = position;
            bench.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Prim(PrimitiveType.Cube, "Asiento", bench.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), m.Wood, Col.None);
            Prim(PrimitiveType.Cube, "Respaldo", bench.transform, new Vector3(0f, 0.78f, 0.22f), new Vector3(1.8f, 0.42f, 0.06f), m.Wood, Col.None, Quaternion.Euler(-8f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Pata", bench.transform, new Vector3(-0.75f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.45f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "Pata", bench.transform, new Vector3(0.75f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.45f), m.Metal, Col.None);

            BoxCollider box = bench.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0.05f);
            box.size = new Vector3(1.8f, 1f, 0.6f);
        }

        private static void LampPost(Transform parent, AscuaMaterials m, Vector3 position, string zoneId)
        {
            GameObject lamp = new GameObject("Farola");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = position;
            lamp.AddComponent<PenumbraFade>();

            Prim(PrimitiveType.Cylinder, "Base", lamp.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.35f, 0.15f, 0.35f), m.Metal, Col.None);
            Prim(PrimitiveType.Cylinder, "Poste", lamp.transform, new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 1.8f, 0.12f), m.Metal);
            Prim(PrimitiveType.Cube, "Cabeza", lamp.transform, new Vector3(0f, 3.75f, 0f), new Vector3(0.42f, 0.3f, 0.42f), m.Metal, Col.None);
            GameObject glass = Prim(PrimitiveType.Cube, "Cristal", lamp.transform, new Vector3(0f, 3.5f, 0f), new Vector3(0.34f, 0.3f, 0.34f), m.LampGlass, Col.None);
            Prim(PrimitiveType.Cube, "Remate", lamp.transform, new Vector3(0f, 3.95f, 0f), new Vector3(0.2f, 0.12f, 0.2f), m.Metal, Col.None);

            // Luz apagada al inicio: ZoneHope la enciende con StreetLampToggle según la Esperanza.
            GameObject lightObject = new GameObject("Luz_Farola");
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3.3f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = AscuaPalette.Ambar;
            light.range = 7f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            StreetLampToggle toggle = lamp.AddComponent<StreetLampToggle>();
            toggle.ConfigureZone(zoneId);
            Wire(toggle, "glassRenderer", glass.GetComponent<Renderer>());
            Wire(toggle, "lampLight", light);
        }

        private static void BuildMalecon(Transform env, AscuaMaterials m)
        {
            Transform malecon = new GameObject("Malecon").transform;
            malecon.SetParent(env, false);

            // El paseo marítimo recorre La Plaza y El Mercado (x de -64 a 24).
            for (float x = -64f; x <= 24f; x += 2f)
            {
                Prim(PrimitiveType.Cube, "Poste", malecon, new Vector3(x, 0.55f, -16f), new Vector3(0.1f, 1.1f, 0.1f), m.Metal, Col.None);
            }
            Prim(PrimitiveType.Cube, "Baranda", malecon, new Vector3(-20f, 1.08f, -16f), new Vector3(88.2f, 0.07f, 0.09f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "BarandaMedia", malecon, new Vector3(-20f, 0.6f, -16f), new Vector3(88.2f, 0.05f, 0.06f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "Rompeolas", malecon, new Vector3(-20f, -0.8f, -16.4f), new Vector3(100f, 1.6f, 0.8f), m.Stone, Col.None);
            Prim(PrimitiveType.Plane, "Mar", malecon, new Vector3(-20f, -1.1f, -70f), new Vector3(24f, 1f, 10.5f), m.Water, Col.None);
        }

        private static void BuildDistantSkyline(Transform env, AscuaMaterials m, System.Random rng)
        {
            Transform skyline = new GameObject("Skyline_Lejano").transform;
            skyline.SetParent(env, false);

            for (float x = -76f; x <= 40f; x += 7f)
            {
                float h = 8f + (float)rng.NextDouble() * 18f;
                float z = 32f + (float)rng.NextDouble() * 18f;
                Prim(PrimitiveType.Cube, "Silueta", skyline, new Vector3(x, h * 0.5f, z), new Vector3(5f + (float)rng.NextDouble() * 3f, h, 6f), m.Skyline, Col.None);
            }
            for (float z = -8f; z <= 30f; z += 8f)
            {
                float hE = 10f + (float)rng.NextDouble() * 14f;
                float hW = 10f + (float)rng.NextDouble() * 14f;
                Prim(PrimitiveType.Cube, "Silueta", skyline, new Vector3(34f, hE * 0.5f, z), new Vector3(6f, hE, 6f), m.Skyline, Col.None);
                Prim(PrimitiveType.Cube, "Silueta", skyline, new Vector3(-74f, hW * 0.5f, z), new Vector3(6f, hW, 6f), m.Skyline, Col.None);
            }
        }

        private static void BuildBoundaries(Transform env)
        {
            Transform bounds = new GameObject("Limites_Invisibles").transform;
            bounds.SetParent(env, false);
            InvisibleWall(bounds, new Vector3(-20f, 1.5f, -15.8f), new Vector3(88f, 3f, 0.3f));
            InvisibleWall(bounds, new Vector3(-20f, 1.5f, 24f), new Vector3(88f, 3f, 0.3f));
            InvisibleWall(bounds, new Vector3(23.5f, 1.5f, 4f), new Vector3(0.3f, 3f, 42f));
            InvisibleWall(bounds, new Vector3(-63.5f, 1.5f, 4f), new Vector3(0.3f, 3f, 42f));
        }

        // ================================================================== El Mercado

        private static void BuildMercado(Transform env, AscuaMaterials m, System.Random rng)
        {
            Transform mercado = new GameObject("El_Mercado").transform;
            mercado.SetParent(env, false);
            Vector3 c = MercadoCenter;

            // Suelo, pavimento y bordillos.
            Prim(PrimitiveType.Cube, "Suelo_Calles", mercado, new Vector3(-47f, -0.05f, 4f), new Vector3(34f, 0.1f, 40f), m.Ground);
            Prim(PrimitiveType.Cube, "Mercado_Pavimento", mercado, c + new Vector3(0f, 0.05f, 0f), new Vector3(20f, 0.1f, 20f), m.Paving);
            Prim(PrimitiveType.Cube, "Bordillo_N", mercado, c + new Vector3(0f, 0.06f, 10.1f), new Vector3(20.4f, 0.12f, 0.3f), m.Trim);
            Prim(PrimitiveType.Cube, "Bordillo_S", mercado, c + new Vector3(0f, 0.06f, -10.1f), new Vector3(20.4f, 0.12f, 0.3f), m.Trim);
            Prim(PrimitiveType.Cube, "Bordillo_E", mercado, c + new Vector3(10.1f, 0.06f, 0f), new Vector3(0.3f, 0.12f, 20.4f), m.Trim);
            Prim(PrimitiveType.Cube, "Bordillo_W", mercado, c + new Vector3(-10.1f, 0.06f, 0f), new Vector3(0.3f, 0.12f, 20.4f), m.Trim);

            // Edificios alrededor (norte, oeste y sur), con una ventana de prueba cada uno.
            BuildBuilding(mercado, "Mercado_N", new Vector3(-44f, 0f, 14.5f), 0f, 14f, 7f, 10f, m.Buildings[2], m, rng, 1, MercadoZone);
            BuildBuilding(mercado, "Mercado_W", new Vector3(-56.5f, 0f, 2f), -90f, 12f, 7f, 8f, m.Buildings[4], m, rng, 0, MercadoZone);
            BuildBuilding(mercado, "Mercado_S", new Vector3(-44f, 0f, -9f), 180f, 12f, 6f, 7f, m.Buildings[1], m, rng, 3, MercadoZone);

            // Puestos con toldo en cuadrícula, dejando pasillos libres para Teo.
            Stall(mercado, m, c + new Vector3(-4f, 0.1f, 4f), 0f, m.AwningA);
            Stall(mercado, m, c + new Vector3(4f, 0.1f, 4f), 0f, m.AwningB);
            Stall(mercado, m, c + new Vector3(-4f, 0.1f, -4f), 180f, m.AwningB);
            Stall(mercado, m, c + new Vector3(4f, 0.1f, -4f), 180f, m.AwningA);

            // Farolas del Mercado.
            LampPost(mercado, m, c + new Vector3(-9f, 0.1f, -9f), MercadoZone);
            LampPost(mercado, m, c + new Vector3(9f, 0.1f, -9f), MercadoZone);
            LampPost(mercado, m, c + new Vector3(-9f, 0.1f, 9f), MercadoZone);
            LampPost(mercado, m, c + new Vector3(9f, 0.1f, 9f), MercadoZone);

            // Cajas apiladas junto a los puestos.
            Prim(PrimitiveType.Cube, "Caja", mercado, c + new Vector3(-7.5f, 0.5f, 6.5f), new Vector3(0.8f, 0.8f, 0.8f), m.Wood, Col.Keep, Quaternion.Euler(0f, 15f, 0f));
            Prim(PrimitiveType.Cube, "Caja", mercado, c + new Vector3(7.4f, 0.4f, -6.8f), new Vector3(0.7f, 0.6f, 0.7f), m.Wood, Col.Keep, Quaternion.Euler(0f, -25f, 0f));
        }

        private static void Stall(Transform parent, AscuaMaterials m, Vector3 position, float yaw, Material awning)
        {
            GameObject stall = new GameObject("Puesto");
            stall.transform.SetParent(parent, false);
            stall.transform.localPosition = position;
            stall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Prim(PrimitiveType.Cube, "Mostrador", stall.transform, new Vector3(0f, 0.45f, 0f), new Vector3(2.6f, 0.9f, 1.2f), m.Wood);
            Prim(PrimitiveType.Cube, "Poste", stall.transform, new Vector3(-1.2f, 1.15f, 0.5f), new Vector3(0.08f, 2.3f, 0.08f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "Poste", stall.transform, new Vector3(1.2f, 1.15f, 0.5f), new Vector3(0.08f, 2.3f, 0.08f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "Toldo", stall.transform, new Vector3(0f, 2.3f, 0f), new Vector3(2.9f, 0.06f, 1.7f), awning, Col.None, Quaternion.Euler(-12f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Mercancia", stall.transform, new Vector3(-0.6f, 1.02f, -0.1f), new Vector3(0.5f, 0.25f, 0.4f), m.Wood, Col.None);
            Prim(PrimitiveType.Cube, "Mercancia", stall.transform, new Vector3(0.5f, 1.0f, 0.1f), new Vector3(0.6f, 0.2f, 0.35f), awning, Col.None);
        }

        // ================================================================== Sistemas de la noche

        private static ZoneHope BuildZone(string name, string id, string displayName, Vector3 center, float radius)
        {
            ZoneHope zone = new GameObject(name).AddComponent<ZoneHope>();
            zone.transform.position = center;
            zone.Configure(id, displayName, radius);
            return zone;
        }

        private static void BuildPenumbra(AscuaMaterials m)
        {
            GameObject root = new GameObject("Penumbra");
            root.transform.position = MapCenter;
            Penumbra penumbra = root.AddComponent<Penumbra>();

            // 2 anillos x 24 tarjetas de niebla oscura; Penumbra las recoloca cada frame.
            Transform wall = new GameObject("Muro_Penumbra").transform;
            wall.SetParent(root.transform, false);
            List<Transform> cards = new List<Transform>();
            for (int i = 0; i < 48; i++)
            {
                // Anillo interior (frente de avance) = bruma pálida; anillo exterior = negro azulado.
                bool edge = i < 24;
                GameObject card = Prim(PrimitiveType.Quad, edge ? "Penumbra_Borde" : "Penumbra_Nucleo", wall, Vector3.zero, new Vector3(10f, 10f, 1f),
                    edge ? m.PenumbraEdge : m.PenumbraWall, Col.None, null, false);
                card.GetComponent<MeshRenderer>().receiveShadows = false;
                cards.Add(card.transform);
            }
            WireArray(penumbra, "wallCards", cards);
        }

        private static void BuildFireflies(AscuaMaterials m)
        {
            GameObject root = new GameObject("Luciernagas_Pool");
            root.AddComponent<FireflyPool>();

            // Todas se crean aquí, desactivadas: en juego solo se reciclan.
            for (int i = 0; i < 8; i++)
            {
                GameObject firefly = new GameObject("Luciernaga_" + i);
                firefly.transform.SetParent(root.transform, false);

                SphereCollider trigger = firefly.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.7f;
                Rigidbody body = firefly.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                GameObject glow = Prim(PrimitiveType.Sphere, "Brillo", firefly.transform, Vector3.zero, Vector3.one * 0.14f, m.Firefly, Col.None, null, false);

                GameObject sparklesObject = new GameObject("Chispas");
                sparklesObject.transform.SetParent(firefly.transform, false);
                ParticleSystem sparkles = sparklesObject.AddComponent<ParticleSystem>();
                ConfigureFireflyParticles(sparkles, m.FireflyParticle);

                Firefly component = firefly.AddComponent<Firefly>();
                Wire(component, "sparkles", sparkles);
                Wire(component, "glow", glow.transform);

                SetLayerRecursively(firefly, IgnoreRaycastLayer);
                firefly.SetActive(false);
            }
        }

        private static void ConfigureFireflyParticles(ParticleSystem ps, Material material)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.15f;
            main.startSize = 0.07f;
            main.startColor = AscuaPalette.Luciernaga;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 10f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Hornea el NavMesh de toda la escena (solo capa Default) y lo guarda como asset.</summary>
        private static void BakeNavMesh()
        {
            GameObject go = new GameObject("NavMesh");
            NavMeshSurface surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            surface.BuildNavMesh();

            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        private static void InvisibleWall(Transform parent, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject("Muro_Invisible");
            wall.layer = IgnoreRaycastLayer; // No bloquea la cámara ni la línea de visión del Farol.
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }

        private static void BuildGroundMist(AscuaMaterials m)
        {
            Transform mist = new GameObject("Bruma_Baja").transform;
            Vector3 center = new Vector3(-20f, 0.35f, 4f);
            Vector3 area = new Vector3(88f, 0f, 44f);
            System.Random rng = new System.Random(21);

            for (int i = 0; i < 14; i++)
            {
                Vector3 p = new Vector3(
                    center.x + ((float)rng.NextDouble() - 0.5f) * area.x,
                    center.y + i * 0.03f,
                    center.z + ((float)rng.NextDouble() - 0.5f) * area.z);

                GameObject quad = Prim(PrimitiveType.Quad, "Bruma", mist, p, new Vector3(14f, 14f, 1f), m.Mist, Col.None,
                    Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f), false);
                quad.AddComponent<FogDrift>().Configure(new Vector3(0.25f, 0f, 0.08f), center, area, 0.12f, 0.3f, new Color(0.45f, 0.52f, 0.75f, 1f));
            }
        }

        // ================================================================== Personajes

        private static ChibiParts BuildChibi(string name, Vector3 position, float yaw, float visualScale, float headSize,
            Material body, RuntimeAnimatorController controller, bool castShadows)
        {
            ChibiParts parts = new ChibiParts();
            parts.Root = new GameObject(name);
            parts.Root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            parts.Visual = new GameObject("Visual").transform;
            parts.Visual.SetParent(parts.Root.transform, false);
            parts.Visual.localScale = Vector3.one * visualScale;
            parts.Animator = parts.Visual.gameObject.AddComponent<Animator>();
            parts.Animator.runtimeAnimatorController = controller;
            parts.Animator.applyRootMotion = false;

            parts.Rig = Pivot("Rig", parts.Visual, new Vector3(0f, AscuaAssetFactory.HipsHeight, 0f));
            Prim(PrimitiveType.Capsule, "Torso", parts.Rig, new Vector3(0f, 0.27f, 0f), new Vector3(0.44f, 0.3f, 0.34f), body, Col.None, null, castShadows);

            parts.Head = Pivot("Head", parts.Rig, new Vector3(0f, 0.62f, 0f));
            Prim(PrimitiveType.Sphere, "Cabeza", parts.Head, new Vector3(0f, headSize * 0.5f - 0.02f, 0f), Vector3.one * headSize, body, Col.None, null, castShadows);

            Transform armL = Pivot("ArmL", parts.Rig, new Vector3(-0.27f, 0.5f, 0f));
            Prim(PrimitiveType.Capsule, "Brazo", armL, new Vector3(0f, -0.19f, 0f), new Vector3(0.13f, 0.2f, 0.13f), body, Col.None, null, castShadows);
            Transform armR = Pivot("ArmR", parts.Rig, new Vector3(0.27f, 0.5f, 0f));
            Prim(PrimitiveType.Capsule, "Brazo", armR, new Vector3(0f, -0.19f, 0f), new Vector3(0.13f, 0.2f, 0.13f), body, Col.None, null, castShadows);

            Transform legL = Pivot("LegL", parts.Rig, new Vector3(-0.11f, 0.02f, 0f));
            Prim(PrimitiveType.Capsule, "Pierna", legL, new Vector3(0f, -0.27f, 0f), new Vector3(0.17f, 0.28f, 0.17f), body, Col.None, null, castShadows);
            Transform legR = Pivot("LegR", parts.Rig, new Vector3(0.11f, 0.02f, 0f));
            Prim(PrimitiveType.Capsule, "Pierna", legR, new Vector3(0f, -0.27f, 0f), new Vector3(0.17f, 0.28f, 0.17f), body, Col.None, null, castShadows);

            return parts;
        }

        private static GameObject BuildPlayer(AscuaAssets assets, out Transform cameraTarget, out PlayerInputReader input)
        {
            AscuaMaterials m = assets.Materials;

            // Ilse-recuerdo: translúcida y sin sombra (es un recuerdo).
            ChibiParts parts = BuildChibi("Ilse", new Vector3(0f, 0.12f, -8f), 0f, 1f, 0.54f, m.IlseBody, assets.ChibiController, false);
            GameObject player = parts.Root;
            player.tag = "Player";

            // Silueta: faldón del abrigo, capucha y capa doradas (Fase 3: sustituyen a la melena).
            CharacterAssets ch = assets.Character;
            Prim(PrimitiveType.Capsule, "Abrigo", parts.Rig, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.25f, 0.42f), m.IlseBody, Col.None, null, false);
            GameObject hood = MeshObject("Capucha", parts.Head, new Vector3(0f, 0.25f, -0.01f), Vector3.one, ch.HoodMesh, ch.IlseHood);
            GameObject cape = MeshObject("Capa", parts.Rig, new Vector3(0f, 0.57f, -0.02f), Vector3.one, ch.CapeMesh, ch.IlseCape);

            // Ojos de luz: óvalos asimétricos (el izquierdo algo mayor e inclinado), sin boca.
            GameObject leftEye = Prim(PrimitiveType.Sphere, "Ojo_Izq", parts.Head, new Vector3(-0.088f, 0.245f, 0.252f), new Vector3(0.078f, 0.112f, 0.04f), ch.IlseEyes, Col.None, Quaternion.Euler(0f, -12f, -8f), false);
            GameObject rightEye = Prim(PrimitiveType.Sphere, "Ojo_Der", parts.Head, new Vector3(0.09f, 0.25f, 0.25f), new Vector3(0.07f, 0.102f, 0.04f), ch.IlseEyes, Col.None, Quaternion.Euler(0f, 12f, 6f), false);

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.86f, 0f);
            cc.height = 1.7f;
            cc.radius = 0.28f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;
            cc.slopeLimit = 45f;

            input = player.AddComponent<PlayerInputReader>();
            PlayerController controller = player.AddComponent<PlayerController>();
            PlayerAnimator playerAnimator = player.AddComponent<PlayerAnimator>();
            PlayerEnergy energy = player.AddComponent<PlayerEnergy>();
            LanternController lantern = player.AddComponent<LanternController>();

            cameraTarget = Pivot("CameraTarget", player.transform, new Vector3(0f, 1.35f, 0f));

            // --- Farol en la mano derecha
            Transform lanternPivot = Pivot("Farol", player.transform, new Vector3(0.36f, 0.8f, 0.12f));
            Prim(PrimitiveType.Cube, "Asa", lanternPivot, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.08f, 0.03f), m.LanternFrame, Col.None, null, false);
            Prim(PrimitiveType.Cube, "Tapa", lanternPivot, new Vector3(0f, -0.06f, 0f), new Vector3(0.15f, 0.03f, 0.15f), m.LanternFrame, Col.None, null, false);
            GameObject core = Prim(PrimitiveType.Cube, "Brasa", lanternPivot, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.15f, 0.09f), m.LanternCore, Col.None, null, false);
            Prim(PrimitiveType.Cube, "Base", lanternPivot, new Vector3(0f, -0.26f, 0f), new Vector3(0.15f, 0.03f, 0.15f), m.LanternFrame, Col.None, null, false);

            GameObject lightObject = new GameObject("Luz_Farol");
            lightObject.transform.SetParent(lanternPivot, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = AscuaPalette.Ambar;
            light.range = 4.2f;
            light.intensity = 0.8f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowNearPlane = 0.15f;

            // --- Luz de relleno sobre el personaje (sin sombras, no cuenta para la jugabilidad)
            GameObject fillObject = new GameObject("Luz_Relleno");
            fillObject.transform.SetParent(player.transform, false);
            fillObject.transform.localPosition = new Vector3(0f, 2.2f, 0.3f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.85f, 0.66f);
            fill.range = 3f;
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;

            // --- Estela de chispas del Farol al correr (mismo tipo de partículas que las luciérnagas)
            GameObject trailObject = new GameObject("Chispas_Carrera");
            trailObject.transform.SetParent(lanternPivot, false);
            trailObject.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            ParticleSystem trail = trailObject.AddComponent<ParticleSystem>();
            ConfigureFireflyParticles(trail, m.FireflyParticle);
            ParticleSystem.MainModule trailMain = trail.main;
            trailMain.startColor = AscuaPalette.Ambar;
            trailMain.startLifetime = 0.7f;
            trailMain.startSize = 0.05f;
            trailMain.maxParticles = 12;
            trailMain.gravityModifier = -0.05f;
            ParticleSystem.EmissionModule trailEmission = trail.emission;
            trailEmission.rateOverTime = 0f;

            // --- Pulso de luz: esfera que se expande + destello
            GameObject ring = Prim(PrimitiveType.Sphere, "Pulso_Onda", player.transform, new Vector3(0f, 0.9f, 0f), Vector3.one, ch.Pulse, Col.None, null, false);
            GameObject flashObject = new GameObject("Pulso_Destello");
            flashObject.transform.SetParent(player.transform, false);
            flashObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Light flash = flashObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = AscuaPalette.Ambar;
            flash.shadows = LightShadows.None;
            flash.intensity = 0f;
            LightPulse pulse = player.AddComponent<LightPulse>();
            Wire(pulse, "input", input);
            Wire(pulse, "energy", energy);
            Wire(pulse, "ringVisual", ring.transform);
            Wire(pulse, "flashLight", flash);

            // --- Rostro, gestos e interacción
            CharacterFace face = player.AddComponent<CharacterFace>();
            Wire(face, "leftEye", leftEye.transform);
            Wire(face, "rightEye", rightEye.transform);
            Wire(face, "energy", energy);

            CharacterGestures gestures = player.AddComponent<CharacterGestures>();
            Wire(gestures, "controller", controller);
            Wire(gestures, "animator", parts.Animator);
            Wire(gestures, "torso", parts.Rig.Find("Torso"));
            Wire(gestures, "cape", cape.transform);
            Wire(gestures, "capeRenderer", cape.GetComponent<Renderer>());
            Wire(gestures, "lanternPivot", lanternPivot);
            Wire(gestures, "runTrail", trail);

            PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
            Wire(interactor, "input", input);

            // --- Conexiones
            Wire(lantern, "fillLight", fill);
            Wire(controller, "input", input);
            Wire(playerAnimator, "animator", parts.Animator);
            Wire(playerAnimator, "controller", controller);
            Wire(lantern, "input", input);
            Wire(lantern, "energy", energy);
            Wire(lantern, "playerController", controller);
            Wire(lantern, "lanternLight", light);
            Wire(lantern, "coreRenderer", core.GetComponent<Renderer>());
            Wire(lantern, "swayPivot", lanternPivot);

            SetLayerRecursively(player, IgnoreRaycastLayer);
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, AscuaAssetFactory.PrefabsFolder + "/Ilse.prefab", InteractionMode.AutomatedAction);
            return player;
        }

        private static void BuildMina(AscuaAssets assets, ZoneHope zone)
        {
            AscuaMaterials m = assets.Materials;

            // Mina: silueta pequeña, ropa neutra fría y bufanda roja emisiva.
            ChibiParts parts = BuildChibi("NPC_Mina", new Vector3(3.6f, 0.12f, -4.2f), 205f, 0.78f, 0.6f, m.MinaBody, assets.ChibiController, true);

            GameObject scarf = Prim(PrimitiveType.Cylinder, "Bufanda", parts.Rig, new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 0.06f, 0.36f), m.MinaAccent, Col.None);
            GameObject scarfTail = Prim(PrimitiveType.Cube, "BufandaCola", parts.Rig, new Vector3(0.1f, 0.45f, 0.17f), new Vector3(0.1f, 0.32f, 0.04f), m.MinaAccent, Col.None, Quaternion.Euler(12f, 0f, -8f));
            Prim(PrimitiveType.Sphere, "Gorro", parts.Head, new Vector3(0f, 0.5f, 0f), new Vector3(0.62f, 0.32f, 0.62f), m.MinaBody, Col.None);

            Renderer[] accents = { scarf.GetComponent<Renderer>(), scarfTail.GetComponent<Renderer>() };
            Transform[] patrol = CreateWaypoints("Waypoints_Mina", new[]
            {
                new Vector3(4f, 0.1f, -4f), new Vector3(7f, 0.1f, 4f), new Vector3(-3f, 0.1f, 7.5f),
                new Vector3(-7.5f, 0.1f, -3f), new Vector3(1f, 0.1f, -8.5f)
            });

            SetupNpc(parts, "mina", "Mina", AscuaPalette.RojoMina, accents, 1.32f, 0.3f, assets.Narrative.MinaIdle, assets.Character.Indicator);
            CharacterAI ai = parts.Root.GetComponent<CharacterAI>();
            ai.Configure(null, null, accents, AscuaPalette.RojoMina);

            // Prefab reutilizable (sin referencias de escena); la instancia de la escena queda libre.
            PrefabUtility.SaveAsPrefabAsset(parts.Root, AscuaAssetFactory.PrefabsFolder + "/NPC_Mina.prefab");
            ai.Configure(zone, patrol, accents, AscuaPalette.RojoMina);
        }

        private static void BuildTeo(AscuaAssets assets, ZoneHope zone)
        {
            AscuaMaterials m = assets.Materials;
            Color accent = AscuaPalette.TurquesaTeo;

            // Teo Bravo: comerciante robusto (torso ancho, gorra) con delantal turquesa.
            ChibiParts parts = BuildChibi("NPC_Teo", MercadoCenter + new Vector3(2f, 0.12f, 0f), 180f, 0.95f, 0.5f, m.TeoBody, assets.ChibiController, true);

            Prim(PrimitiveType.Capsule, "Barriga", parts.Rig, new Vector3(0f, 0.22f, 0.02f), new Vector3(0.54f, 0.28f, 0.42f), m.TeoBody, Col.None);
            GameObject apron = Prim(PrimitiveType.Cube, "Delantal", parts.Rig, new Vector3(0f, 0.16f, 0.22f), new Vector3(0.38f, 0.44f, 0.03f), m.TeoAccent, Col.None, Quaternion.Euler(-6f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "Gorra", parts.Head, new Vector3(0f, 0.47f, 0f), new Vector3(0.5f, 0.06f, 0.5f), m.TeoBody, Col.None);
            Prim(PrimitiveType.Cube, "Visera", parts.Head, new Vector3(0f, 0.44f, 0.27f), new Vector3(0.3f, 0.03f, 0.18f), m.TeoBody, Col.None);
            GameObject capBand = Prim(PrimitiveType.Cylinder, "CintaGorra", parts.Head, new Vector3(0f, 0.43f, 0f), new Vector3(0.51f, 0.025f, 0.51f), m.TeoAccent, Col.None);

            Renderer[] accents = { apron.GetComponent<Renderer>(), capBand.GetComponent<Renderer>() };
            Vector3 c = MercadoCenter;
            Transform[] patrol = CreateWaypoints("Waypoints_Teo", new[]
            {
                c + new Vector3(0f, 0.1f, 0f), c + new Vector3(0f, 0.1f, 8f), c + new Vector3(8f, 0.1f, 0f),
                c + new Vector3(0f, 0.1f, -8f), c + new Vector3(-8f, 0.1f, 0f)
            });

            SetupNpc(parts, "teo", "Teo Bravo", accent, accents, 1.55f, 0.35f, assets.Narrative.TeoIdle, assets.Character.Indicator);
            CharacterAI ai = parts.Root.GetComponent<CharacterAI>();
            ai.Configure(null, null, accents, accent);

            PrefabUtility.SaveAsPrefabAsset(parts.Root, AscuaAssetFactory.PrefabsFolder + "/NPC_Teo.prefab");
            ai.Configure(zone, patrol, accents, accent);
        }

        /// <summary>Componentes comunes de un NPC: colisión, NavMeshAgent, identidad, mirada, IA, diálogo e indicador de misión.</summary>
        private static void SetupNpc(ChibiParts parts, string id, string displayName, Color accent, Renderer[] accents, float height, float radius,
            DialogueSequence idleDialogue, Material indicatorMaterial)
        {
            GameObject root = parts.Root;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            capsule.height = height;
            capsule.radius = radius;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = 1.1f;
            agent.angularSpeed = 300f;
            agent.acceleration = 6f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            root.AddComponent<NpcIdentity>().Configure(id, displayName, accent);
            HeadLookAt look = root.AddComponent<HeadLookAt>();
            Wire(look, "head", parts.Head);

            CharacterAI ai = root.AddComponent<CharacterAI>();
            Wire(ai, "animator", parts.Animator);
            Wire(ai, "visual", parts.Visual);

            // Se puede hablar con él (mantener E) y muestra un rombo si tiene misión.
            NpcInteractable interactable = root.AddComponent<NpcInteractable>();
            interactable.SetIdleDialogue(idleDialogue);
            GameObject diamond = Prim(PrimitiveType.Cube, "Indicador_Mision", root.transform, new Vector3(0f, height + 0.45f, 0f), Vector3.one * 0.14f,
                indicatorMaterial, Col.None, Quaternion.Euler(45f, 0f, 45f), false);
            QuestIndicator indicator = root.AddComponent<QuestIndicator>();
            Wire(indicator, "npc", interactable);
            Wire(indicator, "diamond", diamond.GetComponent<Renderer>());

            // Fuera de la capa Default: no recorta el NavMesh al hornear ni empuja la cámara.
            SetLayerRecursively(root, IgnoreRaycastLayer);
        }

        /// <summary>El Heraldo: figura alta y oscura, capa y capucha que ondean, ojos y farol violetas.</summary>
        private static void BuildHeraldo(AscuaAssets assets)
        {
            CharacterAssets ch = assets.Character;
            Transform[] lurk = CreateWaypoints("Heraldo_Acecho", new[]
            {
                new Vector3(-60f, 0.1f, -10f), new Vector3(-60f, 0.1f, 18f), new Vector3(-30f, 0.1f, 20f),
                new Vector3(20f, 0.1f, 20f), new Vector3(21.5f, 0.1f, -12f), new Vector3(-30f, 0.1f, -13f)
            });

            ChibiParts parts = BuildChibi("Heraldo", lurk[0].position, 90f, 1.18f, 0.46f, ch.HeraldoBody, assets.ChibiController, true);
            MeshObject("Capucha", parts.Head, new Vector3(0f, 0.24f, -0.01f), Vector3.one * 0.95f, ch.HoodMesh, ch.HeraldoCape);
            MeshObject("Capa", parts.Rig, new Vector3(0f, 0.57f, -0.02f), new Vector3(1.1f, 1.35f, 1.1f), ch.CapeMesh, ch.HeraldoCape);
            Prim(PrimitiveType.Sphere, "Ojo_Izq", parts.Head, new Vector3(-0.075f, 0.22f, 0.21f), new Vector3(0.05f, 0.03f, 0.03f), ch.HeraldoEyes, Col.None, null, false);
            Prim(PrimitiveType.Sphere, "Ojo_Der", parts.Head, new Vector3(0.075f, 0.22f, 0.21f), new Vector3(0.05f, 0.03f, 0.03f), ch.HeraldoEyes, Col.None, null, false);

            // Farol violeta en la mano.
            Transform lantern = Pivot("Farol_Violeta", parts.Root.transform, new Vector3(0.42f, 0.85f, 0.15f));
            GameObject core = Prim(PrimitiveType.Cube, "Brasa", lantern, Vector3.zero, new Vector3(0.1f, 0.16f, 0.1f), ch.HeraldoLantern, Col.None, null, false);
            Light violet = new GameObject("Luz_Violeta").AddComponent<Light>();
            violet.transform.SetParent(lantern, false);
            violet.type = LightType.Point;
            violet.color = new Color(0.55f, 0.4f, 1f);
            violet.range = 6f;
            violet.intensity = 1.6f;
            violet.shadows = LightShadows.None;

            GameObject root = parts.Root;
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.95f, 0f);
            capsule.height = 1.9f;
            capsule.radius = 0.35f;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.9f;
            agent.speed = 0.8f;
            agent.angularSpeed = 200f;
            agent.acceleration = 4f;
            agent.stoppingDistance = 1.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            root.AddComponent<NpcIdentity>().Configure("heraldo", "El Heraldo", new Color(0.55f, 0.4f, 1f));
            NpcInteractable interactable = root.AddComponent<NpcInteractable>();
            interactable.SetIdleDialogue(assets.Narrative.HeraldoIdle);

            Heraldo_AI ai = root.AddComponent<Heraldo_AI>();
            List<Renderer> body = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject != core) body.Add(r);
            }
            Wire(ai, "animator", parts.Animator);
            WireArray(ai, "bodyRenderers", body);
            Wire(ai, "lanternRenderer", core.GetComponent<Renderer>());
            Wire(ai, "violetLight", violet);
            WireArray(ai, "lurkPoints", lurk);

            SetLayerRecursively(root, IgnoreRaycastLayer);
        }

        /// <summary>Pool de 3 velas de Teo en El Mercado (desactivadas hasta Vínculo 3).</summary>
        private static void BuildCandles(AscuaAssets assets)
        {
            CharacterAssets ch = assets.Character;
            GameObject root = new GameObject("Velas_Teo");
            root.transform.position = MercadoCenter;
            root.AddComponent<CandleSpawner>();

            for (int i = 0; i < 3; i++)
            {
                GameObject candle = new GameObject("Vela_" + i);
                candle.transform.SetParent(root.transform, false);
                SphereCollider trigger = candle.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.6f;
                trigger.center = new Vector3(0f, 0.3f, 0f);
                Rigidbody body = candle.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                Prim(PrimitiveType.Cylinder, "Cera", candle.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.09f, 0.12f, 0.09f), ch.CandleWax, Col.None, null, false);
                GameObject flame = Prim(PrimitiveType.Sphere, "Llama", candle.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.09f, 0.05f), ch.CandleFlame, Col.None, null, false);

                CandlePickup pickup = candle.AddComponent<CandlePickup>();
                Wire(pickup, "flame", flame.GetComponent<Renderer>());
                SetLayerRecursively(candle, IgnoreRaycastLayer);
                candle.SetActive(false);
            }
        }

        /// <summary>Objeto con una malla generada (capa, capucha) y sin colisión.</summary>
        private static GameObject MeshObject(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Transform[] CreateWaypoints(string name, Vector3[] points)
        {
            Transform root = new GameObject(name).transform;
            Transform[] result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = Pivot("Punto_" + i, root, points[i]);
            }
            return result;
        }

        private static void BuildThirdPersonCamera(Transform cameraTarget, PlayerInputReader input)
        {
            GameObject go = new GameObject("CM_TerceraPersona");
            CinemachineCamera cmCamera = go.AddComponent<CinemachineCamera>();
            cmCamera.Follow = cameraTarget;

            LensSettings lens = cmCamera.Lens;
            lens.FieldOfView = 55f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 250f;
            cmCamera.Lens = lens;

            CinemachineOrbitalFollow orbital = go.AddComponent<CinemachineOrbitalFollow>();
            orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbital.Radius = 5f;
            Unity.Cinemachine.TargetTracking.TrackerSettings tracker = orbital.TrackerSettings;
            tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            orbital.TrackerSettings = tracker;

            go.AddComponent<CinemachineRotationComposer>();
            go.AddComponent<CinemachineImpulseListener>();

            CinemachineDeoccluder deoccluder = go.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = LayerMask.GetMask("Default");

            ThirdPersonCameraRig rig = go.AddComponent<ThirdPersonCameraRig>();
            Wire(rig, "input", input);
        }

        // ================================================================== HUD

        private static void BuildHud(GameObject player, AscuaAssets assets, NightManager night, ZoneHope[] zones)
        {
            Sprite flameSprite = assets.FlameSprite;
            Canvas canvas = CreateCanvas("HUD", 10);
            Transform root = canvas.transform;
            Color amber = AscuaPalette.Ambar;
            Color cold = AscuaPalette.TextoFrio;

            // Logo
            TextMeshProUGUI logo = UIText("Logo", root, "Ascua", 46f, amber, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(400f, 60f));
            logo.characterSpacing = 14f;
            logo.fontStyle = FontStyles.Bold;
            UIText("Lugar", root, "Vesper · La Plaza y El Mercado", 20f, new Color(cold.r, cold.g, cold.b, 0.9f), TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -86f), new Vector2(400f, 30f));

            // Panel de energía:  [llama] FAROL ◆◆◆
            //                     [llama] ███████████░░░░  100/100
            const float leftInset = 56f;   // espacio para la llama
            const float rightInset = 150f; // espacio para el número
            RectTransform panel = UIRect("Energia", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(520f, 70f));

            // Ícono de llama con pivote en la base para que "baile" desde abajo.
            RectTransform flame = UIRect("Llama", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(22f, 2f), new Vector2(34f, 44f));
            Image flameImage = flame.gameObject.AddComponent<Image>();
            flameImage.sprite = flameSprite;
            flameImage.color = amber;
            flameImage.preserveAspect = true;
            flameImage.raycastTarget = false;
            RectTransform flameCore = UIRect("Nucleo", flame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(16f, 22f));
            Image flameCoreImage = flameCore.gameObject.AddComponent<Image>();
            flameCoreImage.sprite = flameSprite;
            flameCoreImage.color = new Color(1f, 0.96f, 0.82f, 0.8f);
            flameCoreImage.preserveAspect = true;
            flameCoreImage.raycastTarget = false;

            UIText("Etiqueta", panel, "FAROL", 18f, new Color(cold.r, cold.g, cold.b, 0.85f), TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(leftInset, 0f), new Vector2(120f, 28f)).characterSpacing = 8f;

            // Número grande junto al extremo derecho de la barra.
            TextMeshProUGUI value = UIText("Valor", panel, "100/100", 32f, amber, TextAlignmentOptions.Left,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(rightInset - 12f, 40f));
            value.fontStyle = FontStyles.Bold;

            RectTransform barBg = UIRect("BarraFondo", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            barBg.offsetMin = new Vector2(leftInset, 14f);
            barBg.offsetMax = new Vector2(-rightInset, 28f);
            barBg.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.15f, 0.75f);
            barBg.GetComponent<Image>().raycastTarget = false;

            RectTransform fill = UIRect("Relleno", barBg, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = amber;
            fillImage.raycastTarget = false;

            // Indicadores de tamaño del Farol (3 puntos)
            RectTransform pipsRoot = UIRect("Radio", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(leftInset + 110f, -16f), new Vector2(80f, 16f));
            Image[] pips = new Image[3];
            for (int i = 0; i < pips.Length; i++)
            {
                float size = 8f + i * 3f;
                RectTransform pip = UIRect("Punto" + i, pipsRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 22f, 0f), new Vector2(size, size));
                pips[i] = pip.gameObject.AddComponent<Image>();
                pips[i].raycastTarget = false;
                pip.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // FPS
            TextMeshProUGUI fps = UIText("FPS", root, "60 FPS", 18f, new Color(cold.r, cold.g, cold.b, 0.8f), TextAlignmentOptions.TopRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(200f, 30f));

            // Fundido de entrada
            CanvasGroup fade = CreateFade(root);

            HUDController hud = canvas.gameObject.AddComponent<HUDController>();
            Wire(hud, "energy", player.GetComponent<PlayerEnergy>());
            Wire(hud, "lantern", player.GetComponent<LanternController>());
            Wire(hud, "energyFill", fill);
            Wire(hud, "energyFillImage", fillImage);
            Wire(hud, "energyValueText", value);
            Wire(hud, "flameIcon", flameImage);
            Wire(hud, "flameCore", flameCoreImage);
            WireArray(hud, "radiusPips", pips);
            Wire(hud, "fpsText", fps);
            Wire(hud, "fadeGroup", fade);

            // Indicador del Pulso de luz en la fila de "FAROL".
            TextMeshProUGUI pulseLabel = UIText("Pulso", panel, "PULSO · R", 18f, amber, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(leftInset + 210f, 0f), new Vector2(160f, 28f));
            pulseLabel.characterSpacing = 6f;
            PulseIndicator pulseIndicator = pulseLabel.gameObject.AddComponent<PulseIndicator>();
            Wire(pulseIndicator, "pulse", player.GetComponent<LightPulse>());
            Wire(pulseIndicator, "label", pulseLabel);

            BuildHopeHud(canvas, night, zones);
            BuildQuestLog(root);
            BuildInteractionPrompt(root, assets.Character.WhiteSprite);
            BuildDialogueBox(root);
            BuildToast(root);
            BuildDebugPanel(root);
            BuildNightSummary(root, night);
        }

        /// <summary>Panel de misiones (Tab) bajo la Esperanza, a la derecha, y su recordatorio compacto.</summary>
        private static void BuildQuestLog(Transform canvasRoot)
        {
            Color cold = AscuaPalette.TextoFrio;

            RectTransform compact = UIRect("Misiones_Compacto", canvasRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -296f), new Vector2(470f, 80f));
            TextMeshProUGUI compactText = compact.gameObject.AddComponent<TextMeshProUGUI>();
            compactText.font = TMP_Settings.defaultFontAsset;
            compactText.fontSize = 22f;
            compactText.color = cold;
            compactText.alignment = TextAlignmentOptions.TopRight;
            compactText.richText = true;
            compactText.raycastTarget = false;
            compactText.text = "Tab · Misiones";

            RectTransform panel = UIRect("Misiones", canvasRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -296f), new Vector2(560f, 560f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.08f, 0.82f);
            background.raycastTarget = false;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            TextMeshProUGUI list = UIText("Lista", panel, "", 22f, cold, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            list.margin = new Vector4(22f, 18f, 22f, 18f);
            list.richText = true;

            QuestLogUI log = panel.gameObject.AddComponent<QuestLogUI>();
            Wire(log, "panelGroup", group);
            Wire(log, "listText", list);
            Wire(log, "compactText", compactText);
        }

        /// <summary>Aviso "Mantén E · Hablar con..." con barra de mantener.</summary>
        private static void BuildInteractionPrompt(Transform canvasRoot, Sprite whiteSprite)
        {
            RectTransform panel = UIRect("Aviso_Interaccion", canvasRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(560f, 62f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.07f, 0.65f);
            background.raycastTarget = false;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            TextMeshProUGUI label = UIText("Texto", panel, "Mantén E", 26f, AscuaPalette.TextoFrio, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), Vector2.zero);

            RectTransform bar = UIRect("Barra", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            bar.offsetMin = new Vector2(0f, 0f);
            bar.offsetMax = new Vector2(0f, 5f);
            Image fill = bar.gameObject.AddComponent<Image>();
            fill.sprite = whiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.color = AscuaPalette.Ambar;
            fill.raycastTarget = false;

            InteractionPrompt prompt = panel.gameObject.AddComponent<InteractionPrompt>();
            Wire(prompt, "group", group);
            Wire(prompt, "label", label);
            Wire(prompt, "holdFill", fill);
        }

        /// <summary>Caja de diálogo inferior con nombre, texto y hasta 3 elecciones.</summary>
        private static void BuildDialogueBox(Transform canvasRoot)
        {
            Color amber = AscuaPalette.Ambar;
            Color cold = AscuaPalette.TextoFrio;

            RectTransform root = UIRect("Dialogo", canvasRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform box = UIRect("Caja", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(1180f, 230f));
            Image background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.08f, 0.88f);
            RectTransform line = UIRect("Filo", box, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 3f));
            Image lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = new Color(amber.r, amber.g, amber.b, 0.7f);
            lineImage.raycastTarget = false;

            TextMeshProUGUI speaker = UIText("Nombre", box, "Mina", 30f, amber, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -18f), new Vector2(600f, 40f));
            speaker.fontStyle = FontStyles.Bold;
            speaker.characterSpacing = 4f;

            TextMeshProUGUI body = UIText("Texto", box, "", 30f, new Color(0.9f, 0.92f, 1f), TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            body.margin = new Vector4(34f, 66f, 34f, 22f);
            body.lineSpacing = 6f;

            TextMeshProUGUI hint = UIText("Continuar", box, "E · Espacio  »", 20f, new Color(cold.r, cold.g, cold.b, 0.7f), TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(300f, 30f));

            // Elecciones: columna sobre la caja.
            Button[] buttons = new Button[3];
            TextMeshProUGUI[] labels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform rt = UIRect("Eleccion_" + (i + 1), root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 286f + (2 - i) * 70f), new Vector2(820f, 60f));
                Image image = rt.gameObject.AddComponent<Image>();
                image.color = amber;
                Button button = rt.gameObject.AddComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.38f);
                colors.selectedColor = new Color(1f, 1f, 1f, 0.38f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.6f);
                colors.fadeDuration = 0.1f;
                button.colors = colors;
                button.targetGraphic = image;

                labels[i] = UIText("Texto", rt, "Elección", 28f, new Color(1f, 0.93f, 0.8f), TextAlignmentOptions.Left,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                labels[i].margin = new Vector4(26f, 0f, 20f, 0f);
                buttons[i] = button;
            }

            DialogueRunner runner = root.gameObject.AddComponent<DialogueRunner>();
            Wire(runner, "group", group);
            Wire(runner, "speakerText", speaker);
            Wire(runner, "bodyText", body);
            Wire(runner, "continueHint", hint);
            WireArray(runner, "choiceButtons", buttons);
            WireArray(runner, "choiceLabels", labels);
        }

        /// <summary>Aviso central reutilizable ("El Mercado se apagó").</summary>
        private static void BuildToast(Transform canvasRoot)
        {
            RectTransform panel = UIRect("Aviso", canvasRoot, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 120f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.07f, 0.72f);
            background.raycastTarget = false;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            TextMeshProUGUI message = UIText("Mensaje", panel, "La zona se apagó", 54f, new Color(0.88f, 0.9f, 1f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            message.fontStyle = FontStyles.Bold;
            message.characterSpacing = 4f;

            ZoneLostNotification toast = panel.gameObject.AddComponent<ZoneLostNotification>();
            Wire(toast, "group", group);
            Wire(toast, "messageText", message);
            Wire(toast, "panel", panel);
        }

        /// <summary>Panel de depuración (F1), oculto por defecto.</summary>
        private static void BuildDebugPanel(Transform canvasRoot)
        {
            RectTransform panel = UIRect("Debug_F1", canvasRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 60f), new Vector2(640f, 440f));
            Image background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.03f, 0.07f, 0.8f);
            background.raycastTarget = false;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            TextMeshProUGUI text = UIText("Texto", panel, "", 21f, AscuaPalette.TextoFrio, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text.margin = new Vector4(20f, 16f, 20f, 16f);
            text.richText = true;

            DebugPanel debug = panel.gameObject.AddComponent<DebugPanel>();
            Wire(debug, "group", group);
            Wire(debug, "text", text);
        }

        /// <summary>Temporizador de noche (arriba al centro) y Esperanza por zona (arriba a la derecha).</summary>
        private static void BuildHopeHud(Canvas canvas, NightManager night, ZoneHope[] zones)
        {
            Transform root = canvas.transform;
            Color amber = AscuaPalette.Ambar;
            Color cold = AscuaPalette.TextoFrio;
            Color barBack = new Color(0.05f, 0.07f, 0.15f, 0.75f);

            // --- Temporizador (tamaños pensados para leerse en un proyector)
            RectTransform timer = UIRect("Noche", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(560f, 96f));
            TextMeshProUGUI nightLabel = UIText("Etiqueta", timer, "NOCHE 1", 24f, new Color(cold.r, cold.g, cold.b, 0.9f), TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(260f, 34f));
            nightLabel.characterSpacing = 8f;
            nightLabel.fontStyle = FontStyles.Bold;
            TextMeshProUGUI timerText = UIText("Tiempo", timer, "1:00", 46f, cold, TextAlignmentOptions.TopRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(200f, 56f));
            timerText.fontStyle = FontStyles.Bold;

            RectTransform timerBack = UIRect("BarraFondo", timer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            timerBack.offsetMin = new Vector2(0f, 6f);
            timerBack.offsetMax = new Vector2(0f, 20f);
            Image timerBackImage = timerBack.gameObject.AddComponent<Image>();
            timerBackImage.color = barBack;
            timerBackImage.raycastTarget = false;
            RectTransform timerFill = UIRect("Relleno", timerBack, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            timerFill.offsetMin = Vector2.zero;
            timerFill.offsetMax = Vector2.zero;
            Image timerFillImage = timerFill.gameObject.AddComponent<Image>();
            timerFillImage.color = cold;
            timerFillImage.raycastTarget = false;

            // --- Esperanza por zona.
            // Panel anclado arriba a la derecha con VerticalLayoutGroup + ContentSizeFitter:
            // cada zona nueva (Fase 3) añade una fila y el panel crece hacia abajo sin recolocar nada.
            RectTransform panel = UIRect("Esperanza", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -64f), new Vector2(470f, 0f));
            Image panelBack = panel.gameObject.AddComponent<Image>();
            panelBack.color = new Color(0.02f, 0.03f, 0.08f, 0.55f);
            panelBack.raycastTarget = false;
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 14, 16);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI hopeTitle = UIText("Titulo", panel, "ESPERANZA", 20f, new Color(cold.r, cold.g, cold.b, 0.75f), TextAlignmentOptions.Right,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 28f));
            hopeTitle.characterSpacing = 10f;
            hopeTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            TextMeshProUGUI[] names = new TextMeshProUGUI[zones.Length];
            TextMeshProUGUI[] values = new TextMeshProUGUI[zones.Length];
            RectTransform[] fills = new RectTransform[zones.Length];
            Image[] fillImages = new Image[zones.Length];

            for (int i = 0; i < zones.Length; i++)
            {
                RectTransform row = UIRect("Zona_" + zones[i].ZoneId, panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 68f));
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 68f;

                names[i] = UIText("Nombre", row, zones[i].DisplayName, 30f, cold, TextAlignmentOptions.TopLeft,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(280f, 40f));
                values[i] = UIText("Valor", row, "60", 32f, amber, TextAlignmentOptions.TopRight,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(150f, 40f));
                values[i].fontStyle = FontStyles.Bold;

                RectTransform back = UIRect("BarraFondo", row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
                back.offsetMin = new Vector2(0f, 2f);
                back.offsetMax = new Vector2(0f, 20f);
                Image backImage = back.gameObject.AddComponent<Image>();
                backImage.color = barBack;
                backImage.raycastTarget = false;

                fills[i] = UIRect("Relleno", back, Vector2.zero, new Vector2(0.6f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                fills[i].offsetMin = Vector2.zero;
                fills[i].offsetMax = Vector2.zero;
                fillImages[i] = fills[i].gameObject.AddComponent<Image>();
                fillImages[i].color = amber;
                fillImages[i].raycastTarget = false;
            }

            HopeHUD hopeHud = canvas.gameObject.AddComponent<HopeHUD>();
            Wire(hopeHud, "night", night);
            Wire(hopeHud, "timerFill", timerFill);
            Wire(hopeHud, "timerFillImage", timerFillImage);
            Wire(hopeHud, "timerText", timerText);
            Wire(hopeHud, "nightLabel", nightLabel);
            WireArray(hopeHud, "zones", zones);
            WireArray(hopeHud, "zoneNames", names);
            WireArray(hopeHud, "zoneFills", fills);
            WireArray(hopeHud, "zoneFillImages", fillImages);
            WireArray(hopeHud, "zoneValues", values);
        }

        /// <summary>Pantalla de resumen de la noche (oculta hasta que termina).</summary>
        private static void BuildNightSummary(Transform canvasRoot, NightManager night)
        {
            Color cold = AscuaPalette.TextoFrio;

            RectTransform panel = UIRect("Resumen_Noche", canvasRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.07f, 0.82f);
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI title = UIText("Titulo", panel, "Amanece en Vesper", 72f, AscuaPalette.Ambar, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(1400f, 110f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6f;
            TextMeshProUGUI body = UIText("Cuerpo", panel, "Zonas salvadas: 0/2", 32f, cold, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 160f));

            Button retry = UIButton("Boton_Reintentar", panel, "Reintentar", new Vector2(0.5f, 0.32f));
            Button toTitle = UIButton("Boton_Titulo", panel, "Título", new Vector2(0.5f, 0.23f));

            NightSummaryPanel summary = panel.gameObject.AddComponent<NightSummaryPanel>();
            Wire(summary, "night", night);
            Wire(summary, "group", group);
            Wire(summary, "titleText", title);
            Wire(summary, "bodyText", body);
            Wire(summary, "retryButton", retry);
            Wire(summary, "titleButton", toTitle);
        }

        // ================================================================== Título

        private static void BuildTitle(AscuaAssets assets)
        {
            AscuaMaterials m = assets.Materials;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.Random rng = new System.Random(3);

            Camera mainCamera = CreateMainCamera(new Vector3(0f, 1.9f, -8f), Quaternion.Euler(-4f, 0f, 0f));

            AtmosphereSettings atmosphere = new GameObject("Atmosfera").AddComponent<AtmosphereSettings>();
            atmosphere.Configure(AscuaPalette.Niebla, 0.011f,
                new Color(0.12f, 0.15f, 0.3f), new Color(0.08f, 0.1f, 0.2f), new Color(0.03f, 0.04f, 0.09f), mainCamera);

            CreateDirectionalLight("Luna", Quaternion.Euler(23.6f, 151.7f, 0f), AscuaPalette.Hex("#8FA3E0"), 0.35f, 0.5f);
            CreateGlobalVolume(assets.VolumeProfile);

            Transform env = new GameObject("Entorno").transform;

            // Muelle en primer plano + mar
            Prim(PrimitiveType.Cube, "Muelle", env, new Vector3(0f, -0.1f, -7f), new Vector3(30f, 0.2f, 6f), m.Stone, Col.None);
            for (float x = -12f; x <= 12f; x += 2f)
            {
                Prim(PrimitiveType.Cube, "Poste", env, new Vector3(x, 0.55f, -4.2f), new Vector3(0.1f, 1.1f, 0.1f), m.Metal, Col.None);
            }
            Prim(PrimitiveType.Cube, "Baranda", env, new Vector3(0f, 1.08f, -4.2f), new Vector3(24.2f, 0.07f, 0.09f), m.Metal, Col.None);
            Prim(PrimitiveType.Plane, "Mar", env, new Vector3(0f, -1f, 55f), new Vector3(30f, 1f, 12f), m.Water, Col.None);

            // Skyline de Vesper al otro lado de la bahía
            for (float x = -70f; x <= 70f; x += 4f + (float)rng.NextDouble() * 4f)
            {
                float centerBoost = 1f - Mathf.Abs(x) / 90f;
                float h = 6f + (float)rng.NextDouble() * 14f + centerBoost * 12f;
                float w = 4f + (float)rng.NextDouble() * 5f;
                float z = 45f + (float)rng.NextDouble() * 35f;
                Transform b = Prim(PrimitiveType.Cube, "Edificio", env, new Vector3(x, h * 0.5f - 1f, z), new Vector3(w, h, 6f), m.Skyline, Col.None).transform;

                int windows = 2 + rng.Next(6);
                for (int i = 0; i < windows; i++)
                {
                    float wx = x + ((float)rng.NextDouble() - 0.5f) * (w - 1f);
                    float wy = 1f + (float)rng.NextDouble() * (h - 3f);
                    Material wm = rng.NextDouble() < 0.3 ? m.SkylineWarm : m.SkylineCold;
                    Prim(PrimitiveType.Cube, "Ventana", b.parent, new Vector3(wx, wy, z - 3.05f), new Vector3(0.7f, 0.9f, 0.1f), wm, Col.None, null, false);
                }
            }

            Prim(PrimitiveType.Sphere, "Luna", env, new Vector3(-28f, 26f, 52f), Vector3.one * 5f, m.Moon, Col.None, null, false);

            // Un Farol apoyado en la baranda: la única luz cálida de la escena
            Transform lanternProp = Pivot("Farol_Apoyado", env, new Vector3(2.2f, 1.23f, -4.25f));
            Prim(PrimitiveType.Cube, "Tapa", lanternProp, new Vector3(0f, 0.1f, 0f), new Vector3(0.15f, 0.03f, 0.15f), m.LanternFrame, Col.None, null, false);
            Prim(PrimitiveType.Cube, "Brasa", lanternProp, new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.15f, 0.09f), m.LanternCore, Col.None, null, false);
            Prim(PrimitiveType.Cube, "Base", lanternProp, new Vector3(0f, -0.1f, 0f), new Vector3(0.15f, 0.03f, 0.15f), m.LanternFrame, Col.None, null, false);
            Light warm = new GameObject("Luz_Farol").AddComponent<Light>();
            warm.transform.SetParent(lanternProp, false);
            warm.type = LightType.Point;
            warm.color = AscuaPalette.Ambar;
            warm.range = 5f;
            warm.intensity = 2.5f;
            warm.shadows = LightShadows.Soft;

            MarkStatic(env.gameObject);

            // Bancos de niebla animados
            Transform fog = new GameObject("Niebla_Animada").transform;
            for (int i = 0; i < 10; i++)
            {
                bool near = i >= 7;
                Vector3 p = new Vector3(-60f + (float)rng.NextDouble() * 120f, near ? 0.5f : 3f, near ? 6f + i : 15f + i * 3f);
                Vector3 scale = near ? new Vector3(30f, 6f, 1f) : new Vector3(40f, 12f, 1f);
                GameObject quad = Prim(PrimitiveType.Quad, "Niebla", fog, p, scale, m.Mist, Col.None, null, false);
                quad.AddComponent<FogDrift>().Configure(new Vector3(near ? 0.9f : 0.6f, 0f, 0f), new Vector3(0f, p.y, p.z), new Vector3(140f, 0f, 0f),
                    0.18f, 0.4f, new Color(0.5f, 0.58f, 0.82f, 1f));
            }

            // UI
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            Canvas canvas = CreateCanvas("UI_Titulo", 0);
            Transform root = canvas.transform;
            Color cold = AscuaPalette.TextoFrio;

            TextMeshProUGUI title = UIText("Titulo", root, "Ascua", 200f, AscuaPalette.Ambar, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(1400f, 260f));
            title.characterSpacing = 20f;
            title.fontStyle = FontStyles.Bold;

            UIText("Subtitulo", root, "Lleva la luz a Vesper", 34f, new Color(cold.r, cold.g, cold.b, 0.8f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 60f)).characterSpacing = 6f;

            Button play = UIButton("Boton_Jugar", root, "Jugar", new Vector2(0.5f, 0.33f));
            Button quit = UIButton("Boton_Salir", root, "Salir", new Vector2(0.5f, 0.24f));

            UIText("Version", root, "Prototipo · Fase 1", 20f, new Color(cold.r, cold.g, cold.b, 0.5f), TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(400f, 30f));

            CanvasGroup fade = CreateFade(root);

            TitleScreen titleScreen = canvas.gameObject.AddComponent<TitleScreen>();
            Wire(titleScreen, "titleText", title);
            Wire(titleScreen, "playButton", play);
            Wire(titleScreen, "quitButton", quit);
            Wire(titleScreen, "fadeGroup", fade);
            Wire(titleScreen, "cameraTransform", mainCamera.transform);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        // ================================================================== Utilidades de escena

        private static Camera CreateMainCamera(Vector3 position, Quaternion rotation)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, rotation);

            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = AscuaPalette.Niebla;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            cam.fieldOfView = 50f;
            go.AddComponent<AudioListener>();

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return cam;
        }

        private static void CreateDirectionalLight(string name, Quaternion rotation, Color color, float intensity, float shadowStrength)
        {
            GameObject go = new GameObject(name);
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = shadowStrength;
        }

        private static void CreateGlobalVolume(VolumeProfile profile)
        {
            Volume volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, Col collider = Col.Keep, Quaternion? localRotation = null, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;

            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            if (!castShadows) meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            Collider existing = go.GetComponent<Collider>();
            if (collider == Col.None)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }
            else if (collider == Col.Mesh && !(existing is MeshCollider))
            {
                UnityEngine.Object.DestroyImmediate(existing);
                go.AddComponent<MeshCollider>();
            }
            return go;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>Marca como Batching Static todo lo que no se mueve ni cambia por script.</summary>
        private static void MarkStatic(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                // Lo que cambia por script (ventanas, luces, farolas) no se agrupa estáticamente.
                if (t.GetComponent<WindowLight>() != null || t.GetComponent<Light>() != null) continue;
                if (t.GetComponentInParent<StreetLampToggle>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void Wire(Object target, string propertyName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"[Ascua] No existe la propiedad '{propertyName}' en {target.GetType().Name}.");
                return;
            }
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireArray<T>(Object target, string propertyName, IList<T> values) where T : Object
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"[Ascua] No existe el array '{propertyName}' en {target.GetType().Name}.");
                return;
            }
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Utilidades de UI

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = UILayer;
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static RectTransform UIRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        private static TextMeshProUGUI UIText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            Vector2 pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            RectTransform rt = UIRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, size);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static TextMeshProUGUI UIText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rt = UIRect(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, size);
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button UIButton(string name, Transform parent, string label, Vector2 anchor)
        {
            RectTransform rt = UIRect(name, parent, anchor, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 72f));
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = AscuaPalette.Ambar;

            Button button = rt.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.08f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.3f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.3f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.55f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
            colors.fadeDuration = 0.15f;
            button.colors = colors;
            button.targetGraphic = image;

            TextMeshProUGUI text = UIText("Texto", rt, label, 38f, AscuaPalette.Ambar, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text.characterSpacing = 10f;
            return button;
        }

        private static CanvasGroup CreateFade(Transform canvasRoot)
        {
            RectTransform rt = UIRect("Fundido", canvasRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image black = rt.gameObject.AddComponent<Image>();
            black.color = new Color(0.02f, 0.03f, 0.07f, 1f);
            black.raycastTarget = true;
            CanvasGroup group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            return group;
        }
    }
}
