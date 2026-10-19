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
        public Material Mist;
        public Material[] Buildings;
    }

    /// <summary>Todo lo que generan las fábricas de assets.</summary>
    public class AscuaAssets
    {
        public AscuaMaterials Materials;
        public RuntimeAnimatorController ChibiController;
        public VolumeProfile VolumeProfile;
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

        private static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");
        private static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");

        public static AscuaAssets CreateAll()
        {
            EnsureFolders();
            AscuaAssets assets = new AscuaAssets
            {
                Materials = CreateMaterials(),
                ChibiController = CreateChibiAnimator(),
                VolumeProfile = CreateVolumeProfile()
            };
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
                LampGlass = Lit("M_FarolaCristalApagado", Hex("#2C3350"), 0.9f),
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

            // Niebla: Unlit transparente con textura suave, doble cara.
            Texture2D fogTexture = CreateFogTexture();
            m.Mist = GetOrCreateMaterial("M_Niebla", UnlitShader);
            m.Mist.SetTexture("_BaseMap", fogTexture);
            m.Mist.SetColor("_BaseColor", new Color(0.55f, 0.62f, 0.85f, 0.25f));
            MakeTransparent(m.Mist, false, true);

            return m;
        }

        private static Color Hex(string hex) => AscuaPalette.Hex(hex);

        private static Material GetOrCreateMaterial(string name, Shader shader)
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

        private static Material Lit(string name, Color baseColor, float smoothness, Color? emission = null, float metallic = 0f)
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
        private static void MakeTransparent(Material material, bool zWrite, bool twoSided)
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

            // Si ya existe se reutiliza para no romper referencias en escenas y prefabs.
            string path = AnimationFolder + "/AC_Chibi.controller";
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            controller.CreateBlendTreeInController("Locomocion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 0.5f);
            tree.AddChild(run, 1f);

            EditorUtility.SetDirty(controller);
            return controller;
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

            Bloom bloom = AddComponent<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.72f));

            Tonemapping tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments color = AddComponent<ColorAdjustments>(profile);
            color.postExposure.Override(0.15f);
            color.contrast.Override(12f);
            color.colorFilter.Override(new Color(0.92f, 0.95f, 1f));
            color.saturation.Override(-8f);

            WhiteBalance whiteBalance = AddComponent<WhiteBalance>(profile);
            whiteBalance.temperature.Override(-12f);
            whiteBalance.tint.Override(4f);

            SplitToning splitToning = AddComponent<SplitToning>(profile);
            splitToning.shadows.Override(AscuaPalette.Azul);
            splitToning.highlights.Override(AscuaPalette.Ambar);
            splitToning.balance.Override(-25f);

            Vignette vignette = AddComponent<Vignette>(profile);
            vignette.color.Override(Hex("#070A1A"));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.36f);
            vignette.smoothness.Override(0.42f);

            FilmGrain grain = AddComponent<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.75f);

            ChromaticAberration aberration = AddComponent<ChromaticAberration>(profile);
            aberration.intensity.Override(0.06f);

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
