using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Player;
using Ascua.UI;
using Ascua.World;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Construye automáticamente las escenas de la Fase 1 (Título y Plaza),
    /// los prefabs de Ilse y Mina y la configuración del proyecto ("Ascua").
    /// Menú: Ascua > Construir Fase 1.
    /// </summary>
    public static class AscuaSceneBuilder
    {
        private const string TitleScenePath = AscuaAssetFactory.ScenesFolder + "/Title.unity";
        private const string PlazaScenePath = AscuaAssetFactory.ScenesFolder + "/Plaza.unity";
        private const int IgnoreRaycastLayer = 2;
        private const int UILayer = 5;

        private enum Col { None, Keep, Mesh }

        private class ChibiParts
        {
            public GameObject Root;
            public Transform Visual, Rig, Head;
            public Animator Animator;
        }

        // ================================================================== Menú

        [MenuItem("Ascua/Construir Fase 1 (assets + escenas)", priority = 0)]
        public static void BuildAll()
        {
            if (!Validate()) return;

            AscuaAssets assets = AscuaAssetFactory.CreateAll();
            BuildPlaza(assets);
            BuildTitle(assets);
            ConfigureProject();

            EditorSceneManager.OpenScene(TitleScenePath);
            EditorUtility.DisplayDialog("Ascua", "Fase 1 construida.\n\nEscenas: Title y Plaza (Assets/_Project/Scenes).\nPulsa Play desde Title.", "Vale");
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
            Debug.Log("[Ascua] Fase 1 construida correctamente.");
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

            // Alturas contrastadas (7.5 / 16 / 13 m) para que el skyline tenga lectura escalonada.
            BuildBuilding(env, "Edificio_N1", new Vector3(-6f, 0f, 14.5f), 0f, 10f, 7f, 7.5f, m.Buildings[0], m, rng, 0);
            BuildBuilding(env, "Edificio_N2", new Vector3(6.5f, 0f, 14.5f), 0f, 11f, 7f, 16f, m.Buildings[1], m, rng, -1);
            BuildBuilding(env, "Edificio_E", new Vector3(14.5f, 0f, 1f), 90f, 13f, 7f, 13f, m.Buildings[2], m, rng, 1);
            BuildBuilding(env, "Edificio_W", new Vector3(-14.5f, 0f, 2f), -90f, 12f, 7f, 8f, m.Buildings[3], m, rng, 3);
            BuildBuilding(env, "Edificio_SE", new Vector3(12.5f, 0f, -11f), 90f, 8f, 6f, 6f, m.Buildings[4], m, rng, -1);

            BuildStreetFurniture(env, m);
            BuildMalecon(env, m);
            BuildDistantSkyline(env, m, rng);
            BuildBoundaries(env);
            MarkStatic(env.gameObject);

            // --- Niebla baja a ras de suelo
            BuildGroundMist(m);

            // --- Personajes
            GameObject player = BuildPlayer(assets, out Transform cameraTarget, out PlayerInputReader input);
            BuildMina(assets);

            // --- Cámara en tercera persona
            BuildThirdPersonCamera(cameraTarget, input);

            // --- Sesión + HUD
            new GameObject("GameSession").AddComponent<GameSession>();
            BuildHud(player, assets.FlameSprite);

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
        /// reactiveColumn = columna de la planta baja con ventana de prueba (-1 = ninguna).
        /// </summary>
        private static void BuildBuilding(Transform env, string name, Vector3 position, float yaw,
            float width, float depth, float height, Material wall, AscuaMaterials m, System.Random rng, int reactiveColumn)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(env, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

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

                    bool reactive = f == 0 && c == reactiveColumn;
                    Material windowMaterial = reactive ? m.WindowReactive : PickWindowMaterial(m, rng);

                    Prim(PrimitiveType.Cube, "Marco", root, new Vector3(x, y, -0.01f), new Vector3(1.25f, 1.55f, 0.04f), m.Trim, Col.None);
                    Prim(PrimitiveType.Cube, "Alfeizar", root, new Vector3(x, y - 0.75f, -0.1f), new Vector3(1.35f, 0.08f, 0.22f), m.Trim, Col.None);

                    // La ventana mira hacia fuera (forward = normal de la fachada).
                    GameObject window = Prim(PrimitiveType.Cube, reactive ? "VentanaPrueba" : "Ventana", root,
                        new Vector3(x, y, -0.03f), new Vector3(1.05f, 1.35f, 0.08f), windowMaterial, Col.None, Quaternion.Euler(0f, 180f, 0f));

                    if (reactive) SetupReactiveWindow(window, root, new Vector3(x, y, -0.9f));
                }
            }
        }

        private static Material PickWindowMaterial(AscuaMaterials m, System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.55) return m.WindowDark;
            if (r < 0.85) return m.WindowCold;
            return m.WindowWarm;
        }

        private static void SetupReactiveWindow(GameObject window, Transform buildingRoot, Vector3 glowLocalPosition)
        {
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

            WindowLight windowLight = window.AddComponent<WindowLight>();
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
            foreach (Vector3 p in lamps) LampPost(furniture, m, p);

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

        private static void LampPost(Transform parent, AscuaMaterials m, Vector3 position)
        {
            GameObject lamp = new GameObject("Farola_Apagada");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = position;

            Prim(PrimitiveType.Cylinder, "Base", lamp.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.35f, 0.15f, 0.35f), m.Metal, Col.None);
            Prim(PrimitiveType.Cylinder, "Poste", lamp.transform, new Vector3(0f, 1.8f, 0f), new Vector3(0.12f, 1.8f, 0.12f), m.Metal);
            Prim(PrimitiveType.Cube, "Cabeza", lamp.transform, new Vector3(0f, 3.75f, 0f), new Vector3(0.42f, 0.3f, 0.42f), m.Metal, Col.None);
            GameObject glass = Prim(PrimitiveType.Cube, "Cristal", lamp.transform, new Vector3(0f, 3.5f, 0f), new Vector3(0.34f, 0.3f, 0.34f), m.LampGlass, Col.None);
            Prim(PrimitiveType.Cube, "Remate", lamp.transform, new Vector3(0f, 3.95f, 0f), new Vector3(0.2f, 0.12f, 0.2f), m.Metal, Col.None);

            // Luz preparada pero apagada: StreetLampToggle la encenderá en la Fase 2.
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
            Wire(toggle, "glassRenderer", glass.GetComponent<Renderer>());
            Wire(toggle, "lampLight", light);
        }

        private static void BuildMalecon(Transform env, AscuaMaterials m)
        {
            Transform malecon = new GameObject("Malecon").transform;
            malecon.SetParent(env, false);

            for (float x = -24f; x <= 24f; x += 2f)
            {
                Prim(PrimitiveType.Cube, "Poste", malecon, new Vector3(x, 0.55f, -16f), new Vector3(0.1f, 1.1f, 0.1f), m.Metal, Col.None);
            }
            Prim(PrimitiveType.Cube, "Baranda", malecon, new Vector3(0f, 1.08f, -16f), new Vector3(48.2f, 0.07f, 0.09f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "BarandaMedia", malecon, new Vector3(0f, 0.6f, -16f), new Vector3(48.2f, 0.05f, 0.06f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "Rompeolas", malecon, new Vector3(0f, -0.8f, -16.4f), new Vector3(60f, 1.6f, 0.8f), m.Stone, Col.None);
            Prim(PrimitiveType.Plane, "Mar", malecon, new Vector3(0f, -1.1f, -70f), new Vector3(20f, 1f, 10.5f), m.Water, Col.None);
        }

        private static void BuildDistantSkyline(Transform env, AscuaMaterials m, System.Random rng)
        {
            Transform skyline = new GameObject("Skyline_Lejano").transform;
            skyline.SetParent(env, false);

            for (float x = -40f; x <= 40f; x += 7f)
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
                Prim(PrimitiveType.Cube, "Silueta", skyline, new Vector3(-34f, hW * 0.5f, z), new Vector3(6f, hW, 6f), m.Skyline, Col.None);
            }
        }

        private static void BuildBoundaries(Transform env)
        {
            Transform bounds = new GameObject("Limites_Invisibles").transform;
            bounds.SetParent(env, false);
            InvisibleWall(bounds, new Vector3(0f, 1.5f, -15.8f), new Vector3(50f, 3f, 0.3f));
            InvisibleWall(bounds, new Vector3(0f, 1.5f, 24f), new Vector3(50f, 3f, 0.3f));
            InvisibleWall(bounds, new Vector3(23.5f, 1.5f, 4f), new Vector3(0.3f, 3f, 42f));
            InvisibleWall(bounds, new Vector3(-23.5f, 1.5f, 4f), new Vector3(0.3f, 3f, 42f));
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
            Vector3 center = new Vector3(0f, 0.35f, 4f);
            Vector3 area = new Vector3(48f, 0f, 44f);
            System.Random rng = new System.Random(21);

            for (int i = 0; i < 9; i++)
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

            // Silueta: melena larga y faldón del abrigo.
            Prim(PrimitiveType.Sphere, "Melena", parts.Head, new Vector3(0f, 0.15f, 0.14f), new Vector3(0.42f, 0.6f, 0.3f), m.IlseBody, Col.None, null, false);
            Prim(PrimitiveType.Capsule, "Abrigo", parts.Rig, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.25f, 0.42f), m.IlseBody, Col.None, null, false);

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

        private static void BuildMina(AscuaAssets assets)
        {
            AscuaMaterials m = assets.Materials;

            // Mina: silueta pequeña, ropa neutra fría y bufanda roja emisiva.
            ChibiParts parts = BuildChibi("NPC_Mina", new Vector3(3.6f, 0.12f, -4.2f), 205f, 0.78f, 0.6f, m.MinaBody, assets.ChibiController, true);

            Prim(PrimitiveType.Cylinder, "Bufanda", parts.Rig, new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 0.06f, 0.36f), m.MinaAccent, Col.None);
            Prim(PrimitiveType.Cube, "BufandaCola", parts.Rig, new Vector3(0.1f, 0.45f, 0.17f), new Vector3(0.1f, 0.32f, 0.04f), m.MinaAccent, Col.None, Quaternion.Euler(12f, 0f, -8f));
            Prim(PrimitiveType.Sphere, "Gorro", parts.Head, new Vector3(0f, 0.5f, 0f), new Vector3(0.62f, 0.32f, 0.62f), m.MinaBody, Col.None);

            CapsuleCollider capsule = parts.Root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.66f, 0f);
            capsule.height = 1.32f;
            capsule.radius = 0.3f;

            parts.Root.AddComponent<NpcIdentity>().Configure("mina", "Mina", AscuaPalette.RojoMina);
            HeadLookAt look = parts.Root.AddComponent<HeadLookAt>();
            Wire(look, "head", parts.Head);

            PrefabUtility.SaveAsPrefabAssetAndConnect(parts.Root, AscuaAssetFactory.PrefabsFolder + "/NPC_Mina.prefab", InteractionMode.AutomatedAction);
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

            CinemachineDeoccluder deoccluder = go.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = LayerMask.GetMask("Default");

            ThirdPersonCameraRig rig = go.AddComponent<ThirdPersonCameraRig>();
            Wire(rig, "input", input);
        }

        // ================================================================== HUD

        private static void BuildHud(GameObject player, Sprite flameSprite)
        {
            Canvas canvas = CreateCanvas("HUD", 10);
            Transform root = canvas.transform;
            Color amber = AscuaPalette.Ambar;
            Color cold = AscuaPalette.TextoFrio;

            // Logo
            TextMeshProUGUI logo = UIText("Logo", root, "Ascua", 46f, amber, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(400f, 60f));
            logo.characterSpacing = 14f;
            logo.fontStyle = FontStyles.Bold;
            UIText("Lugar", root, "Vesper · La Plaza", 18f, new Color(cold.r, cold.g, cold.b, 0.7f), TextAlignmentOptions.TopLeft,
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

            // Ayuda de controles
            RectTransform hint = UIRect("Ayuda", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(620f, 120f));
            CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            UIText("Texto", hint,
                "WASD / stick  moverse  ·  Shift  correr\nMantener click izq. / F / RT  avivar el Farol\nQ · E / LB · RB  tamaño de la luz  ·  Rueda  distancia de cámara\nEsc  liberar ratón  ·  F3  FPS",
                17f, new Color(cold.r, cold.g, cold.b, 0.75f), TextAlignmentOptions.BottomRight,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).lineSpacing = 12f;

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
            Wire(hud, "hintGroup", hintGroup);
            Wire(hud, "fadeGroup", fade);
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

        private static void WireArray(Object target, string propertyName, IList<Image> values)
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
