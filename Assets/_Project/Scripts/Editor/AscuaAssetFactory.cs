using System.IO;
using Ascua.Core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ascua.EditorTools
{
    /// <summary>Materiales compartidos de la Fase 1.</summary>
    public class AscuaMaterials
    {
        public Material Ground, Paving, PavingInset, Sidewalk, Trim, Roof, Door;
        public Material WindowDark, WindowCold, WindowWarm, WindowReactive;
        public Material Wood, Metal, LampGlass, Water, Stone, LaneMark;
        public Material Skyline, SkylineWarm, SkylineCold, Moon;
        public Material IlseBody, LanternFrame, LanternCore;
        public Material MinaBody, MinaAccent;
        public Material TeoBody, TeoAccent;
        public Material AwningA, AwningB;
        public Material Firefly, FireflyParticle;
        public Material Mist, PenumbraWall, PenumbraEdge;
        /// <summary>Paleta de colores de edificio (se aplica por instancia con SurfaceTint sobre BrickWall).</summary>
        public Material[] Buildings;
        /// <summary>Ladrillo compartido por TODOS los edificios.</summary>
        public Material BrickWall;
        public SurfaceTextures Surfaces;
    }

    /// <summary>Todo lo que generan las fábricas de assets.</summary>
    public class AscuaAssets
    {
        public AscuaMaterials Materials;
        public RuntimeAnimatorController ChibiController;
        public VolumeProfile VolumeProfile;
        public Sprite FlameSprite;
        public CharacterAssets Character;
        public NarrativeAssets Narrative;
        public AudioAssets Audio;
        public VolumeProfile PhotoFilterProfile;
        public CombatAssets Combat;
    }

    /// <summary>
    /// Genera los assets de la Fase 1: carpetas, materiales URP/Lit, textura de niebla,
    /// clips de animación chibi + Animator Controller y el perfil de post-procesado.
    /// </summary>
    public static class AscuaAssetFactory
    {
        public const string Root = "Assets/_Project";
        public const string MaterialsFolder = Root + "/Art/Materials";
        public const string TexturesFolder = Root + "/Art/Textures";
        public const string AnimationFolder = Root + "/Animation";
        public const string SettingsFolder = Root + "/Settings";
        public const string ScenesFolder = Root + "/Scenes";
        public const string PrefabsFolder = Root + "/Prefabs";

        // Proporciones del rig chibi (compartidas con el constructor de escenas).
        public const float HipsHeight = 0.55f;
        public const string RigPath = "Rig";
        public const string HeadPath = "Rig/Head";
        public const string ArmLPath = "Rig/ArmL";
        public const string ArmRPath = "Rig/ArmR";
        public const string LegLPath = "Rig/LegL";
        public const string LegRPath = "Rig/LegR";

        internal static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");
        internal static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");

        public static AscuaAssets CreateAll()
        {
            EnsureFolders();
            AscuaAssets assets = new AscuaAssets
            {
                Materials = CreateMaterials(),
                ChibiController = CreateChibiAnimator(),
                VolumeProfile = CreateVolumeProfile(),
                FlameSprite = CreateFlameSprite()
            };
            assets.Character = AscuaCharacterAssets.Create(assets.Materials);
            assets.Narrative = AscuaNarrativeContent.CreateAll();
            assets.Audio = AscuaAudioFactory.CreateAll();
            assets.PhotoFilterProfile = CreatePhotoFilterProfile();
            assets.Combat = AscuaCombatAssets.Create(assets.Materials, assets.Character);
            AssetDatabase.SaveAssets();
            return assets;
        }

        // ------------------------------------------------------------------ Carpetas

        public static void EnsureFolders()
        {
            string[] folders = { MaterialsFolder, TexturesFolder, AnimationFolder, SettingsFolder, ScenesFolder, PrefabsFolder };
            foreach (string folder in folders) EnsureFolder(folder);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------ Materiales

        private static AscuaMaterials CreateMaterials()
        {
            Color amber = AscuaPalette.Ambar;
            // Emisión "casi negra" para que URP no quite la keyword _EMISSION al inspeccionar el material.
            Color nearBlack = new Color(0.01f, 0.01f, 0.01f);

            AscuaMaterials m = new AscuaMaterials
            {
                Ground = Lit("M_Asfalto", Hex("#161B2E"), 0.45f),
                Paving = Lit("M_PlazaPavimento", Hex("#283052"), 0.25f),
                PavingInset = Lit("M_PlazaRoseton", Hex("#323B63"), 0.3f),
                Sidewalk = Lit("M_Acera", Hex("#2E3554"), 0.2f),
                Trim = Lit("M_Moldura", Hex("#151A30"), 0.2f),
                Roof = Lit("M_Tejado", Hex("#1E2440"), 0.15f),
                Door = Lit("M_Puerta", Hex("#2A2230"), 0.2f),
                WindowDark = Lit("M_VentanaApagada", Hex("#0A0E1C"), 0.92f),
                WindowCold = Lit("M_VentanaFria", Hex("#0A0E1C"), 0.9f, AscuaPalette.Hdr(Hex("#3A55A8"), 0.6f)),
                WindowWarm = Lit("M_VentanaTibia", Hex("#0A0E1C"), 0.9f, AscuaPalette.Hdr(amber, 0.45f)),
                WindowReactive = Lit("M_VentanaPrueba", Hex("#0A0E1C"), 0.92f, nearBlack),
                Wood = Lit("M_Madera", Hex("#3F3433"), 0.15f),
                Metal = Lit("M_Metal", Hex("#22263A"), 0.5f, null, 0.6f),
                LampGlass = Lit("M_FarolaCristalApagado", Hex("#2C3350"), 0.9f, nearBlack),
                Water = Lit("M_Agua", Hex("#0C1430"), 0.96f),
                Stone = Lit("M_Piedra", Hex("#4A5172"), 0.2f),
                LaneMark = Lit("M_MarcaVial", Hex("#5E6688"), 0.2f),
                Skyline = Lit("M_Skyline", Hex("#121933"), 0.1f),
                SkylineWarm = Lit("M_SkylineVentanaTibia", Hex("#0A0E1C"), 0.5f, AscuaPalette.Hdr(amber, 2.5f)),
                SkylineCold = Lit("M_SkylineVentanaFria", Hex("#0A0E1C"), 0.5f, AscuaPalette.Hdr(Hex("#5A78D0"), 1.4f)),
                Moon = Lit("M_Luna", Hex("#DCE4FF"), 0f, AscuaPalette.Hdr(Hex("#DCE4FF"), 1.6f)),
                IlseBody = Lit("M_Ilse_Base", new Color(0.75f, 0.84f, 1f, 0.5f), 0.6f, AscuaPalette.Hdr(Hex("#5A74C4"), 0.55f)),
                LanternFrame = Lit("M_Farol_Marco", Hex("#2A2A33"), 0.5f, null, 0.7f),
                LanternCore = Lit("M_Farol_Brasa", amber, 0.3f, AscuaPalette.Hdr(amber, 2f)),
                MinaBody = Lit("M_Mina_Base", Hex("#5B6479"), 0.25f),
                MinaAccent = Lit("M_Mina_Acento", AscuaPalette.RojoMina, 0.3f, AscuaPalette.Hdr(AscuaPalette.RojoMina, 0.9f)),
                TeoBody = Lit("M_Teo_Base", Hex("#56606E"), 0.25f),
                TeoAccent = Lit("M_Teo_Acento", AscuaPalette.TurquesaTeo, 0.3f, AscuaPalette.Hdr(AscuaPalette.TurquesaTeo, 0.8f)),
                AwningA = Lit("M_Toldo_A", Hex("#5A4A6A"), 0.1f),
                AwningB = Lit("M_Toldo_B", Hex("#4A5A6A"), 0.1f),
                Firefly = Lit("M_Luciernaga", AscuaPalette.Luciernaga, 0.2f, AscuaPalette.Hdr(AscuaPalette.Luciernaga, 3.5f)),
                Buildings = new[]
                {
                    Lit("M_Edificio_A", Hex("#39426B"), 0.15f),
                    Lit("M_Edificio_B", Hex("#4A4566"), 0.15f),
                    Lit("M_Edificio_C", Hex("#34506A"), 0.15f),
                    Lit("M_Edificio_D", Hex("#4D5374"), 0.15f),
                    Lit("M_Edificio_E", Hex("#3E3A5C"), 0.15f)
                }
            };

            // Ilse-recuerdo es translúcida.
            MakeTransparent(m.IlseBody, true, false);

            // Ruido genérico (lo siguen usando los shaders de personajes como textura de matiz).
            CreateSurfaceNoiseTextures(out _, out _);

            // Relieve real por superficie: 7 juegos PBR tileables (color + normal map + máscara con
            // suavidad, metálico y oclusión). 4-6 materiales base reutilizables; el color de cada
            // edificio/caja va por instancia (SurfaceTint) sin duplicar materiales.
            SurfaceTextures surfaces = AscuaSurfaceTextures.CreateAll();
            m.Surfaces = surfaces;
            AscuaSurfaceTextures.Apply(m.Ground, surfaces.Asphalt, new Vector2(14f, 14f));
            AscuaSurfaceTextures.Apply(m.Paving, surfaces.Cobble, new Vector2(11f, 11f));
            AscuaSurfaceTextures.Apply(m.PavingInset, surfaces.Cobble, new Vector2(5f, 5f));
            AscuaSurfaceTextures.Apply(m.Sidewalk, surfaces.Cobble, new Vector2(8f, 8f));
            AscuaSurfaceTextures.Apply(m.Stone, surfaces.CarvedStone, new Vector2(2f, 2f));
            AscuaSurfaceTextures.Apply(m.Trim, surfaces.CarvedStone, new Vector2(4f, 1f));
            AscuaSurfaceTextures.Apply(m.Roof, surfaces.RoofTiles, new Vector2(4f, 4f));
            AscuaSurfaceTextures.Apply(m.Wood, surfaces.Wood, Vector2.one);
            AscuaSurfaceTextures.Apply(m.Door, surfaces.Wood, Vector2.one);
            AscuaSurfaceTextures.Apply(m.Metal, surfaces.Metal, Vector2.one);
            AscuaSurfaceTextures.Apply(m.LanternFrame, surfaces.Metal, Vector2.one);

            m.BrickWall = Lit("M_Edificio_Ladrillo", Color.white, 1f);
            AscuaSurfaceTextures.Apply(m.BrickWall, surfaces.Brick, new Vector2(5f, 6f));

            // Niebla: Unlit transparente con textura suave, doble cara.
            Texture2D fogTexture = CreateFogTexture();
            m.Mist = GetOrCreateMaterial("M_Niebla", UnlitShader);
            m.Mist.SetTexture("_BaseMap", fogTexture);
            m.Mist.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.85f, 0.25f));
            MakeTransparent(m.Mist, false, true);

            // Muro de la Penumbra: misma textura suave, casi negra y más opaca.
            m.PenumbraWall = GetOrCreateMaterial("M_Penumbra", UnlitShader);
            m.PenumbraWall.SetTexture("_BaseMap", fogTexture);
            m.PenumbraWall.SetColor("_BaseColor", new Color(0.04f, 0.05f, 0.12f, 0.9f));
            MakeTransparent(m.PenumbraWall, false, true);

            // Borde de la Penumbra: bruma fría y pálida que se lee contra la noche oscura.
            m.PenumbraEdge = GetOrCreateMaterial("M_PenumbraBorde", UnlitShader);
            m.PenumbraEdge.SetTexture("_BaseMap", fogTexture);
            m.PenumbraEdge.SetColor("_BaseColor", new Color(0.5f, 0.56f, 0.78f, 0.55f));
            MakeTransparent(m.PenumbraEdge, false, true);

            // Chispas de luciérnaga: partículas aditivas con un punto suave.
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            m.FireflyParticle = GetOrCreateMaterial("M_ParticulaLuciernaga", particleShader != null ? particleShader : UnlitShader);
            m.FireflyParticle.SetTexture("_BaseMap", CreateSoftDotTexture());
            m.FireflyParticle.SetColor("_BaseColor", Color.white);
            MakeTransparent(m.FireflyParticle, false, true);
            m.FireflyParticle.SetFloat("_Blend", 2f); // Aditivo en el shader de partículas de URP.
            m.FireflyParticle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.FireflyParticle.SetFloat("_DstBlend", (float)BlendMode.One);

            return m;
        }

        internal static Color Hex(string hex) => AscuaPalette.Hex(hex);

        internal static Material GetOrCreateMaterial(string name, Shader shader)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }

        internal static Material Lit(string name, Color baseColor, float smoothness, Color? emission = null, float metallic = 0f)
        {
            Material material = GetOrCreateMaterial(name, LitShader);
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Configura un material URP (Lit o Unlit) como transparente con mezcla alfa.</summary>
        internal static void MakeTransparent(Material material, bool zWrite, bool twoSided)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", zWrite ? 1f : 0f);
            material.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------ Textura de niebla

        private static Texture2D CreateFogTexture()
        {
            string path = TexturesFolder + "/T_NieblaSuave.png";
            const int size = 256;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f);
                    float v = y / (size - 1f);

                    // Ruido en dos octavas multiplicado por una caída radial suave.
                    float noise = 0.6f * Mathf.PerlinNoise(u * 3f + 11.3f, v * 3f + 7.1f)
                                + 0.4f * Mathf.PerlinNoise(u * 7f + 3.7f, v * 7f + 1.9f);
                    float dx = u - 0.5f;
                    float dy = v - 0.5f;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    falloff = falloff * falloff * (3f - 2f * falloff);

                    float alpha = Mathf.Clamp01((noise - 0.25f) * 1.6f) * falloff;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Ruido de superficie

        /// <summary>Asigna ruido (color + relieve) a un material URP/Lit. El tiling se aplica a ambos mapas.</summary>
        private static void ApplySurfaceNoise(Material material, Texture2D albedo, Texture2D normal, Vector2 tiling, float bumpScale)
        {
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", bumpScale);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Genera un ruido fBm periódico (se repite sin costuras) y guarda dos PNG:
        /// T_RuidoSuperficie (gris claro que modula el color base) y T_RuidoSuperficie_N (normal map).
        /// </summary>
        private static void CreateSurfaceNoiseTextures(out Texture2D albedoTexture, out Texture2D normalTexture)
        {
            const int size = 512;
            float[] height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    // Manchas grandes (desgaste) + grano fino (textura del material).
                    float blotches = TileableFbm(u, v, 4, 4, 11);
                    float grain = TileableFbm(u, v, 64, 2, 37);
                    height[y * size + x] = blotches * 0.75f + grain * 0.25f;
                }
            }

            Color32[] albedo = new Color32[size * size];
            Color32[] normal = new Color32[size * size];
            const float strength = 6f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;

                    // Color: entre 0.84 y 1.0 para que solo matice el color base.
                    byte g = (byte)(Mathf.Lerp(0.84f, 1f, height[i]) * 255f);
                    albedo[i] = new Color32(g, g, g, 255);

                    // Normal por diferencias centrales con envoltura (sin costuras).
                    float left = height[y * size + (x - 1 + size) % size];
                    float right = height[y * size + (x + 1) % size];
                    float down = height[((y - 1 + size) % size) * size + x];
                    float up = height[((y + 1) % size) * size + x];
                    Vector3 n = new Vector3((left - right) * strength, (down - up) * strength, 1f).normalized;
                    normal[i] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }

            albedoTexture = SaveTexture("T_RuidoSuperficie", size, albedo, false);
            normalTexture = SaveTexture("T_RuidoSuperficie_N", size, normal, true);
        }

        internal static Texture2D SaveTexture(string name, int size, Color32[] pixels, bool isNormalMap)
        {
            string path = $"{TexturesFolder}/{name}.png";
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Ruido fBm de valor que se repite exactamente cada 1 unidad en u y v.</summary>
        private static float TileableFbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            int period = basePeriod;

            for (int o = 0; o < octaves; o++)
            {
                sum += PeriodicValueNoise(u * period, v * period, period, seed + o * 101) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return sum / total;
        }

        private static float PeriodicValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            int xa = Mod(x0, period), xb = Mod(x0 + 1, period);
            int ya = Mod(y0, period), yb = Mod(y0 + 1, period);

            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed);
            float c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Mod(int a, int p) => ((a % p) + p) % p;

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        /// <summary>Punto blanco de bordes suaves para partículas (64 px).</summary>
        private static Texture2D CreateSoftDotTexture()
        {
            string path = TexturesFolder + "/T_PuntoSuave.png";
            const int size = 64;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(d * d * 255f));
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Ícono de llama

        /// <summary>Dibuja una llama (gota con punta curvada) en blanco con antialiasing y la importa como Sprite.</summary>
        private static Sprite CreateFlameSprite()
        {
            string path = TexturesFolder + "/UI_Llama.png";
            const int size = 128;
            Color32[] pixels = new Color32[size * size];
            const float baseY = 0.35f;
            const float radius = 0.27f;
            float aa = 1.5f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;

                    // La punta se curva ligeramente hacia un lado.
                    float t = Mathf.Clamp01((v - baseY) / (0.95f - baseY));
                    float centerX = 0.5f + 0.07f * Mathf.Sin(t * Mathf.PI) * t;

                    float halfWidth;
                    if (v < baseY)
                    {
                        float dy = v - baseY;
                        halfWidth = radius * radius - dy * dy > 0f ? Mathf.Sqrt(radius * radius - dy * dy) : -1f;
                    }
                    else
                    {
                        halfWidth = radius * Mathf.Pow(1f - t, 1.4f);
                    }

                    float distance = halfWidth - Mathf.Abs(u - centerX);
                    float alpha = Mathf.Clamp01(distance / aa + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ Animación chibi

        private struct Gait
        {
            public float Period, LegAmp, ArmAmp, ArmOut, Bob, Lean, Roll, HeadNod;
            public bool IsIdle;
        }

        private static RuntimeAnimatorController CreateChibiAnimator()
        {
            AnimationClip idle = CreateClip("Chibi_Idle", new Gait { Period = 3f, LegAmp = 0f, ArmAmp = 3f, ArmOut = 7f, Bob = 0.024f, Lean = 0f, Roll = 0f, HeadNod = 2f, IsIdle = true });
            AnimationClip walk = CreateClip("Chibi_Walk", new Gait { Period = 1f, LegAmp = 28f, ArmAmp = 22f, ArmOut = 8f, Bob = 0.035f, Lean = 4f, Roll = 2.5f, HeadNod = 2f });
            AnimationClip run = CreateClip("Chibi_Run", new Gait { Period = 0.62f, LegAmp = 48f, ArmAmp = 45f, ArmOut = 12f, Bob = 0.07f, Lean = 12f, Roll = 4f, HeadNod = 3f });

            AnimationClip start = CreateOneShotClip("Chibi_Arranque", 0.32f, ArranquePose);
            AnimationClip celebrate = CreateOneShotClip("Chibi_Celebracion", 0.9f, CelebracionPose);
            AnimationClip pick = CreateOneShotClip("Chibi_Recoger", 0.5f, RecogerPose);
            AnimationClip attack1 = CreateOneShotClip("Chibi_Ataque1", 0.32f, t => AtaquePose(t, 1));
            AnimationClip attack2 = CreateOneShotClip("Chibi_Ataque2", 0.32f, t => AtaquePose(t, 2));
            AnimationClip attack3 = CreateOneShotClip("Chibi_Ataque3", 0.4f, t => AtaquePose(t, 3));

            // Si ya existe con los estados de combate se reutiliza (no rompe referencias);
            // si es de una fase anterior (sin "Ataque1"), se rehace.
            string path = AnimationFolder + "/AC_Chibi.controller";
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null)
            {
                foreach (AnimatorControllerParameter parameter in existing.parameters)
                {
                    if (parameter.name == "Ataque1") return existing;
                }
                AssetDatabase.DeleteAsset(path);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Arrancar", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Celebrar", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Recoger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Ataque1", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Ataque2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Ataque3", AnimatorControllerParameterType.Trigger);

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomocion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 0.5f);
            tree.AddChild(run, 1f);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            // Anticipación: medio paso con pequeño impulso antes del ciclo de caminar.
            AnimatorState startState = machine.AddState("Arranque");
            startState.motion = start;
            AnimatorStateTransition toStart = locomotion.AddTransition(startState);
            toStart.AddCondition(AnimatorConditionMode.If, 0f, "Arrancar");
            toStart.hasExitTime = false;
            toStart.duration = 0.05f;
            AnimatorStateTransition fromStart = startState.AddTransition(locomotion);
            fromStart.hasExitTime = true;
            fromStart.exitTime = 0.9f;
            fromStart.duration = 0.12f;

            // Celebración: levanta el Farol con un saltito (desde cualquier estado).
            AnimatorState celebrateState = machine.AddState("Celebracion");
            celebrateState.motion = celebrate;
            AnimatorStateTransition toCelebrate = machine.AddAnyStateTransition(celebrateState);
            toCelebrate.AddCondition(AnimatorConditionMode.If, 0f, "Celebrar");
            toCelebrate.hasExitTime = false;
            toCelebrate.duration = 0.08f;
            toCelebrate.canTransitionToSelf = false;
            AnimatorStateTransition fromCelebrate = celebrateState.AddTransition(locomotion);
            fromCelebrate.hasExitTime = true;
            fromCelebrate.exitTime = 0.92f;
            fromCelebrate.duration = 0.15f;

            // Recoger: la mano se extiende hacia el objeto (desde cualquier estado).
            AnimatorState pickState = machine.AddState("Recoger");
            pickState.motion = pick;
            AnimatorStateTransition toPick = machine.AddAnyStateTransition(pickState);
            toPick.AddCondition(AnimatorConditionMode.If, 0f, "Recoger");
            toPick.hasExitTime = false;
            toPick.duration = 0.06f;
            toPick.canTransitionToSelf = false;
            AnimatorStateTransition fromPick = pickState.AddTransition(locomotion);
            fromPick.hasExitTime = true;
            fromPick.exitTime = 0.9f;
            fromPick.duration = 0.12f;

            // Combate: 3 barridos de luz desde cualquier estado. Transición casi instantánea
            // (0.04 s) para que el golpe se sienta ágil; salida al 85 % para encadenar el combo.
            AddAttackState(machine, locomotion, "Ataque1", attack1);
            AddAttackState(machine, locomotion, "Ataque2", attack2);
            AddAttackState(machine, locomotion, "Ataque3", attack3);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddAttackState(AnimatorStateMachine machine, AnimatorState locomotion, string trigger, AnimationClip clip)
        {
            AnimatorState state = machine.AddState(trigger);
            state.motion = clip;
            state.speed = 1f;
            AnimatorStateTransition to = machine.AddAnyStateTransition(state);
            to.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            to.hasExitTime = false;
            to.duration = 0.04f;
            to.canTransitionToSelf = true;
            AnimatorStateTransition back = state.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.85f;
            back.duration = 0.1f;
        }

        /// <summary>
        /// Barrido de luz: el brazo del Farol cruza por delante del cuerpo.
        /// 1: de derecha a izquierda · 2: de izquierda a derecha · 3: remate de arriba abajo con un saltito.
        /// </summary>
        private static GesturePose AtaquePose(float t, int step)
        {
            // Anticipación corta (20 %), golpe rápido (35 %) y recuperación.
            float strike = t < 0.2f ? -0.25f * Mathf.Sin(Mathf.PI * 0.5f * t / 0.2f) : t < 0.55f ? Mathf.SmoothStep(-0.25f, 1f, (t - 0.2f) / 0.35f) : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
            float body = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.7f));
            GesturePose pose = new GesturePose
            {
                HipsOffset = -0.03f * body,
                Lean = 8f * body,
                HeadPitch = 4f * body,
                LegL = -10f * body,
                LegR = 8f * body,
                ArmLPitch = 10f * body,
                ArmLRoll = -14f * body
            };
            switch (step)
            {
                case 1:
                    pose.ArmRPitch = -78f * Mathf.Abs(strike);
                    pose.ArmRRoll = Mathf.Lerp(40f, -35f, Mathf.Clamp01(strike));
                    break;
                case 2:
                    pose.ArmRPitch = -72f * Mathf.Abs(strike);
                    pose.ArmRRoll = Mathf.Lerp(-30f, 42f, Mathf.Clamp01(strike));
                    break;
                default:
                    float hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.1f) / 0.4f));
                    pose.HipsOffset = 0.05f * hop - 0.03f * body;
                    pose.Lean = 14f * body;
                    pose.ArmRPitch = Mathf.Lerp(-155f, -35f, Mathf.Clamp01(strike));
                    pose.ArmRRoll = 10f;
                    break;
            }
            return pose;
        }

        /// <summary>Pose del rig en un instante t (0-1) de un gesto.</summary>
        private struct GesturePose
        {
            public float HipsOffset, Lean, HeadPitch, LegL, LegR, ArmLPitch, ArmLRoll, ArmRPitch, ArmRRoll;
        }

        /// <summary>Medio paso de anticipación: se agacha, impulsa y entra inclinada al caminar.</summary>
        private static GesturePose ArranquePose(float t)
        {
            float s = Mathf.Sin(Mathf.PI * t);
            return new GesturePose
            {
                HipsOffset = t < 0.45f ? -0.05f * Mathf.Sin(Mathf.PI * t / 0.45f) : 0.035f * Mathf.Sin(Mathf.PI * (t - 0.45f) / 0.55f),
                Lean = t < 0.4f ? 10f * Mathf.Sin(Mathf.PI * 0.5f * t / 0.4f) : Mathf.Lerp(10f, 4f, (t - 0.4f) / 0.6f),
                HeadPitch = -6f * s,
                LegL = -18f * s,
                LegR = 10f * s,
                ArmLPitch = 15f * s,
                ArmLRoll = -8f,
                ArmRPitch = -12f * s,
                ArmRRoll = 8f
            };
        }

        /// <summary>Celebración corta: saltito, mirada arriba y brazo del Farol en alto.</summary>
        private static GesturePose CelebracionPose(float t)
        {
            float raise = t < 0.25f ? t / 0.25f : t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
            raise = raise * raise * (3f - 2f * raise);
            float hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.1f) / 0.35f));
            return new GesturePose
            {
                HipsOffset = 0.06f * hop,
                Lean = -4f * raise,
                HeadPitch = -14f * raise,
                LegL = 0f,
                LegR = 0f,
                ArmLPitch = -35f * raise,
                ArmLRoll = -7f - 12f * raise,
                ArmRPitch = -160f * raise,
                ArmRRoll = 8f + 10f * raise
            };
        }

        /// <summary>Recoger: se inclina un poco y extiende la mano derecha hacia delante y abajo.</summary>
        private static GesturePose RecogerPose(float t)
        {
            float reach = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.85f));
            return new GesturePose
            {
                HipsOffset = -0.04f * reach,
                Lean = 14f * reach,
                HeadPitch = 12f * reach,
                LegL = -6f * reach,
                LegR = 4f * reach,
                ArmLPitch = 8f * reach,
                ArmLRoll = -8f,
                ArmRPitch = -62f * reach,
                ArmRRoll = 8f - 4f * reach
            };
        }

        /// <summary>Crea (o regenera) un clip de un solo uso a partir de una función de pose.</summary>
        private static AnimationClip CreateOneShotClip(string name, float duration, System.Func<float, GesturePose> pose)
        {
            string path = $"{AnimationFolder}/{name}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = name, frameRate = 30f };
            else clip.ClearCurves();
            const int samples = 16;
            const float tau = Mathf.PI * 2f;

            // AddCurves trabaja con fase 0-2π: se convierte a t 0-1.
            AddCurves(clip, RigPath, duration, samples,
                p => new Vector3(0f, HipsHeight + pose(p / tau).HipsOffset, 0f),
                p => Quaternion.Euler(pose(p / tau).Lean, 0f, 0f));
            AddCurves(clip, HeadPath, duration, samples, null, p => Quaternion.Euler(pose(p / tau).HeadPitch, 0f, 0f));
            AddCurves(clip, LegLPath, duration, samples, null, p => Quaternion.Euler(pose(p / tau).LegL, 0f, 0f));
            AddCurves(clip, LegRPath, duration, samples, null, p => Quaternion.Euler(pose(p / tau).LegR, 0f, 0f));
            AddCurves(clip, ArmLPath, duration, samples, null, p =>
            {
                GesturePose g = pose(p / tau);
                return Quaternion.Euler(g.ArmLPitch, 0f, g.ArmLRoll);
            });
            AddCurves(clip, ArmRPath, duration, samples, null, p =>
            {
                GesturePose g = pose(p / tau);
                return Quaternion.Euler(g.ArmRPitch, 0f, g.ArmRRoll);
            });

            if (isNew) AssetDatabase.CreateAsset(clip, path);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip CreateClip(string name, Gait g)
        {
            // Se reutiliza el asset existente (mismo GUID) y solo se regeneran sus curvas.
            string path = $"{AnimationFolder}/{name}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = name, frameRate = 30f };
            else clip.ClearCurves();
            const int samples = 16;

            // Caderas: rebote vertical, inclinación hacia delante y balanceo lateral.
            AddCurves(clip, RigPath, g.Period, samples,
                p =>
                {
                    float bob = g.IsIdle ? g.Bob * 0.5f * Mathf.Sin(p) : g.Bob * (Mathf.Abs(Mathf.Cos(p)) - 0.5f);
                    return new Vector3(0f, HipsHeight + bob, 0f);
                },
                p => Quaternion.Euler(g.Lean, 0f, g.Roll * Mathf.Sin(p)));

            AddCurves(clip, HeadPath, g.Period, samples, null,
                p => Quaternion.Euler(g.HeadNod * (g.IsIdle ? Mathf.Sin(p) : Mathf.Sin(2f * p)), 0f, 0f));

            AddCurves(clip, LegLPath, g.Period, samples, null, p => Quaternion.Euler(g.LegAmp * Mathf.Sin(p), 0f, 0f));
            AddCurves(clip, LegRPath, g.Period, samples, null, p => Quaternion.Euler(-g.LegAmp * Mathf.Sin(p), 0f, 0f));

            // Brazos opuestos a la pierna del mismo lado; en Idle "respiran" ligeramente.
            AddCurves(clip, ArmLPath, g.Period, samples, null, p => Quaternion.Euler(-g.ArmAmp * Mathf.Sin(p), 0f, -g.ArmOut));
            AddCurves(clip, ArmRPath, g.Period, samples, null,
                p => Quaternion.Euler((g.IsIdle ? -1f : 1f) * g.ArmAmp * Mathf.Sin(p), 0f, g.ArmOut));

            if (isNew) AssetDatabase.CreateAsset(clip, path);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void AddCurves(AnimationClip clip, string path, float period, int samples,
            System.Func<float, Vector3> position, System.Func<float, Quaternion> rotation)
        {
            if (position != null)
            {
                AnimationCurve x = new AnimationCurve(), y = new AnimationCurve(), z = new AnimationCurve();
                for (int i = 0; i <= samples; i++)
                {
                    float t = period * i / samples;
                    Vector3 v = position(Mathf.PI * 2f * i / samples);
                    x.AddKey(t, v.x);
                    y.AddKey(t, v.y);
                    z.AddKey(t, v.z);
                }
                SetCurve(clip, path, "m_LocalPosition.x", x);
                SetCurve(clip, path, "m_LocalPosition.y", y);
                SetCurve(clip, path, "m_LocalPosition.z", z);
            }

            if (rotation != null)
            {
                AnimationCurve x = new AnimationCurve(), y = new AnimationCurve(), z = new AnimationCurve(), w = new AnimationCurve();
                for (int i = 0; i <= samples; i++)
                {
                    float t = period * i / samples;
                    Quaternion q = rotation(Mathf.PI * 2f * i / samples);
                    x.AddKey(t, q.x);
                    y.AddKey(t, q.y);
                    z.AddKey(t, q.z);
                    w.AddKey(t, q.w);
                }
                SetCurve(clip, path, "m_LocalRotation.x", x);
                SetCurve(clip, path, "m_LocalRotation.y", y);
                SetCurve(clip, path, "m_LocalRotation.z", z);
                SetCurve(clip, path, "m_LocalRotation.w", w);
            }
        }

        private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        // ------------------------------------------------------------------ Post-procesado

        private static VolumeProfile CreateVolumeProfile()
        {
            // Se reutiliza el perfil existente (mismo GUID) vaciando sus overrides.
            string path = SettingsFolder + "/VP_Ascua_Vesper.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                foreach (VolumeComponent old in new System.Collections.Generic.List<VolumeComponent>(profile.components))
                {
                    Object.DestroyImmediate(old, true);
                }
                profile.components.Clear();
            }

            // Bloom calibrado: umbral 1.0 (solo lo que de verdad emite: Farol, ventanas, luciérnagas),
            // más suave y amplio, con filtrado de alta calidad para que no "queme" la imagen.
            Bloom bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.72f));
            bloom.highQualityFiltering.Override(true);

            Tonemapping tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments color = AddComponent<ColorAdjustments>(profile);
            color.postExposure.Override(0.45f);
            color.contrast.Override(14f);
            color.colorFilter.Override(new Color(0.92f, 0.95f, 1f));
            color.saturation.Override(-8f);

            WhiteBalance whiteBalance = AddComponent<WhiteBalance>(profile);
            whiteBalance.temperature.Override(-12f);
            whiteBalance.tint.Override(4f);

            SplitToning splitToning = AddComponent<SplitToning>(profile);
            splitToning.shadows.Override(AscuaPalette.Azul);
            splitToning.highlights.Override(AscuaPalette.Ambar);
            splitToning.balance.Override(-25f);

            // Curva de contraste "cinematográfica": negros algo hundidos y azulados, altas luces cálidas.
            LiftGammaGain lgg = AddComponent<LiftGammaGain>(profile);
            lgg.lift.Override(new Vector4(0.98f, 0.99f, 1.04f, -0.03f));
            lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
            lgg.gain.Override(new Vector4(1.03f, 1f, 0.96f, 0.02f));

            Vignette vignette = AddComponent<Vignette>(profile);
            vignette.color.Override(Hex("#070A1A"));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.4f);

            // Grano fino y sutil (antes era ruidoso).
            FilmGrain grain = AddComponent<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.85f);

            // Sin aberración cromática permanente: PostFxDirector la añade solo con energía baja,
            // ráfaga de la Penumbra, oleadas y golpes recibidos.

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>Perfil del modo foto: ajustes de color y viñeta que PhotoMode modifica según el filtro.</summary>
        private static VolumeProfile CreatePhotoFilterProfile()
        {
            string path = SettingsFolder + "/VP_ModoFoto_Filtros.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            ColorAdjustments color = AddComponent<ColorAdjustments>(profile);
            color.saturation.Override(0f);
            color.colorFilter.Override(Color.white);
            color.contrast.Override(0f);
            Vignette vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
