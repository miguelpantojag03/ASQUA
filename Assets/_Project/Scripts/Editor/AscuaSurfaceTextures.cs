using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ascua.EditorTools
{
    /// <summary>Un juego de texturas PBR tileables (todas del mismo tamaño y sin costuras).</summary>
    public class SurfaceSet
    {
        /// <summary>Color base en gris claro: lo tiñe el _BaseColor del material (o el SurfaceTint).</summary>
        public Texture2D Albedo;
        public Texture2D Normal;
        /// <summary>Máscara URP: R = metálico, G = oclusión, A = suavidad (smoothness).</summary>
        public Texture2D Mask;
        public float NormalStrength;
    }

    /// <summary>Todas las superficies del entorno.</summary>
    public class SurfaceTextures
    {
        public SurfaceSet Cobble, Asphalt, Brick, Wood, Metal, RoofTiles, CarvedStone;
    }

    /// <summary>
    /// Genera por código las texturas de los materiales base del entorno (estilizadas, no fotorrealistas):
    /// adoquín, asfalto mojado con charcos, ladrillo, madera en tablas, metal cepillado con óxido,
    /// tejas y piedra labrada. Cada juego trae color, normal map (relieve real) y máscara con suavidad
    /// variable (los charcos y el metal brillan; el mortero y la madera no).
    /// Se generan una sola vez (si el PNG ya existe se reutiliza): puedes sustituirlos por texturas
    /// de ambientCG / Poly Haven con el MISMO nombre y el material los usará.
    /// </summary>
    public static class AscuaSurfaceTextures
    {
        private const int Size = 512;
        private const string Folder = AscuaAssetFactory.TexturesFolder + "/Superficies";

        /// <summary>Muestra de una superficie en (u, v) ∈ [0,1): altura, color (gris), suavidad, metálico.</summary>
        private struct Sample
        {
            public float Height;
            public float Albedo;
            public float Smoothness;
            public float Metallic;
        }

        private delegate Sample Sampler(float u, float v);

        public static SurfaceTextures CreateAll()
        {
            AscuaAssetFactory.EnsureFolder(Folder);
            return new SurfaceTextures
            {
                Cobble = Create("T_Adoquin", 6f, 0.6f, Cobble),
                Asphalt = Create("T_AsfaltoMojado", 5f, 0.55f, Asphalt),
                Brick = Create("T_Ladrillo", 8f, 0.9f, Brick),
                Wood = Create("T_MaderaTablas", 6f, 0.6f, Wood),
                Metal = Create("T_MetalCepillado", 4f, 0.35f, Metal),
                RoofTiles = Create("T_Tejas", 8f, 0.8f, RoofTiles),
                CarvedStone = Create("T_PiedraLabrada", 7f, 0.8f, CarvedStone)
            };
        }

        // ================================================================== Patrones

        /// <summary>Adoquines en hileras desplazadas, bordes redondeados, juntas oscuras y desgastadas.</summary>
        private static Sample Cobble(float u, float v)
        {
            const int rows = 8, cols = 6;
            float y = v * rows;
            int row = Mathf.FloorToInt(y);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = y - row;
            float jitter = Hash(Wrap(col, cols), row, 3) * 0.08f;
            float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy)) - jitter;
            float stone = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.16f));
            float wear = Fbm(u, v, 16, 3, 5);
            float tone = 0.78f + 0.2f * Hash(Wrap(col, cols), row, 9);
            return new Sample
            {
                Height = stone * (0.75f + 0.25f * wear),
                Albedo = Mathf.Lerp(0.62f, tone * (0.9f + 0.1f * wear), stone),
                Smoothness = Mathf.Lerp(0.12f, 0.38f + 0.15f * wear, stone),
                Metallic = 0f
            };
        }

        /// <summary>Asfalto: grano fino, grietas y charcos lisos y brillantes (calle "después de llover").</summary>
        private static Sample Asphalt(float u, float v)
        {
            float grain = Fbm(u, v, 64, 3, 11);
            float blotch = Fbm(u, v, 4, 4, 21);
            float crackNoise = Fbm(u, v, 8, 3, 33);
            float crack = 1f - Mathf.Clamp01(Mathf.Abs(crackNoise - 0.5f) / 0.012f);
            float puddle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((blotch - 0.6f) / 0.06f));
            float height = Mathf.Lerp(0.5f + 0.35f * grain - 0.4f * crack, 0.42f, puddle);
            return new Sample
            {
                Height = height,
                Albedo = Mathf.Lerp(0.82f + 0.18f * grain - 0.3f * crack, 0.62f, puddle),
                Smoothness = Mathf.Lerp(0.22f + 0.1f * grain, 0.78f, puddle),
                Metallic = 0f
            };
        }

        /// <summary>Ladrillo en aparejo a soga, con mortero rehundido y piezas de tono distinto.</summary>
        private static Sample Brick(float u, float v)
        {
            const int rows = 10, cols = 4;
            float y = v * rows;
            int row = Mathf.FloorToInt(y);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = y - row;
            float mortarX = 0.035f, mortarY = 0.09f;
            float inside = Mathf.Min(Mathf.Clamp01(Mathf.Min(fx, 1f - fx) / mortarX), Mathf.Clamp01(Mathf.Min(fy, 1f - fy) / mortarY));
            float brick = Mathf.SmoothStep(0f, 1f, inside);
            float chip = Fbm(u, v, 32, 2, 41);
            float tone = 0.72f + 0.26f * Hash(Wrap(col, cols), row, 17);
            return new Sample
            {
                Height = brick * (0.82f + 0.18f * chip),
                Albedo = Mathf.Lerp(0.55f, tone * (0.92f + 0.08f * chip), brick),
                Smoothness = Mathf.Lerp(0.08f, 0.24f, brick),
                Metallic = 0f
            };
        }

        /// <summary>Tablas verticales con veta y juntas.</summary>
        private static Sample Wood(float u, float v)
        {
            const int planks = 4;
            float x = u * planks;
            int plank = Mathf.FloorToInt(x);
            float fx = x - plank;
            float gap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(fx, 1f - fx) / 0.04f));
            float warp = Fbm(u, v, 4, 3, 51 + plank);
            float grain = 0.5f + 0.5f * Mathf.Sin((fx * 18f + warp * 6f + v * 2f) * Mathf.PI);
            float tone = 0.78f + 0.2f * Hash(plank, 0, 61);
            return new Sample
            {
                Height = gap * (0.85f + 0.15f * grain),
                Albedo = gap * tone * (0.85f + 0.15f * grain) + (1f - gap) * 0.35f,
                Smoothness = 0.18f + 0.1f * grain,
                Metallic = 0f
            };
        }

        /// <summary>Metal cepillado en horizontal con manchas de óxido mate.</summary>
        private static Sample Metal(float u, float v)
        {
            float brushed = Fbm(u, v, 128, 2, 71) * 0.5f + Fbm(u, v, 4, 2, 73) * 0.5f;
            // Vetas del cepillado: solo dependen de v (líneas horizontales) y se repiten sin costura.
            float streak = Fbm(0f, v, 128, 2, 77) * 0.5f + 0.5f * Fbm(u, v, 16, 2, 75);
            float rust = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Fbm(u, v, 6, 4, 79) - 0.62f) / 0.08f));
            return new Sample
            {
                Height = 0.5f + 0.1f * streak - 0.15f * rust,
                Albedo = Mathf.Lerp(0.85f + 0.1f * brushed, 0.55f, rust),
                Smoothness = Mathf.Lerp(0.55f + 0.15f * streak, 0.15f, rust),
                Metallic = Mathf.Lerp(0.85f, 0.15f, rust)
            };
        }

        /// <summary>Tejas en escama: hileras de arcos superpuestos.</summary>
        private static Sample RoofTiles(float u, float v)
        {
            const int rows = 8, cols = 8;
            float y = v * rows;
            int row = Mathf.FloorToInt(y);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col - 0.5f, fy = y - row;
            float arc = 1f - Mathf.Clamp01((fx * fx * 2.2f + (1f - fy) * 0.9f));
            float tile = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arc * 3f));
            float tone = 0.75f + 0.22f * Hash(Wrap(col, cols), row, 83);
            return new Sample
            {
                Height = fy * 0.6f * tile + 0.2f * tile,
                Albedo = Mathf.Lerp(0.45f, tone, tile),
                Smoothness = Mathf.Lerp(0.1f, 0.3f, tile),
                Metallic = 0f
            };
        }

        /// <summary>Sillares grandes con marcas de cincel (fuente, bases).</summary>
        private static Sample CarvedStone(float u, float v)
        {
            const int rows = 3, cols = 2;
            float y = v * rows;
            int row = Mathf.FloorToInt(y);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = y - row;
            float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
            float block = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.05f));
            float chisel = Fbm(u, v, 32, 3, 91);
            float tone = 0.8f + 0.15f * Hash(Wrap(col, cols), row, 93);
            return new Sample
            {
                Height = block * (0.7f + 0.3f * chisel),
                Albedo = Mathf.Lerp(0.5f, tone * (0.88f + 0.12f * chisel), block),
                Smoothness = Mathf.Lerp(0.1f, 0.28f, block),
                Metallic = 0f
            };
        }

        // ================================================================== Generación

        private static SurfaceSet Create(string name, float normalSlope, float normalStrength, Sampler sampler)
        {
            string albedoPath = $"{Folder}/{name}_A.png";
            string normalPath = $"{Folder}/{name}_N.png";
            string maskPath = $"{Folder}/{name}_M.png";

            bool exists = File.Exists(FullPath(albedoPath)) && File.Exists(FullPath(normalPath)) && File.Exists(FullPath(maskPath));
            if (!exists)
            {
                float[] height = new float[Size * Size];
                Color32[] albedo = new Color32[Size * Size];
                Color32[] mask = new Color32[Size * Size];

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Sample s = sampler((float)x / Size, (float)y / Size);
                        int i = y * Size + x;
                        height[i] = s.Height;
                        byte g = (byte)(Mathf.Clamp01(s.Albedo) * 255f);
                        albedo[i] = new Color32(g, g, g, 255);
                        // AO aproximada por la altura: las juntas y grietas quedan en sombra.
                        byte ao = (byte)(Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(s.Height * 1.4f)) * 255f);
                        mask[i] = new Color32((byte)(Mathf.Clamp01(s.Metallic) * 255f), ao, 0, (byte)(Mathf.Clamp01(s.Smoothness) * 255f));
                    }
                }

                Color32[] normal = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float left = height[y * Size + (x - 1 + Size) % Size];
                        float right = height[y * Size + (x + 1) % Size];
                        float down = height[((y - 1 + Size) % Size) * Size + x];
                        float up = height[((y + 1) % Size) * Size + x];
                        Vector3 n = new Vector3((left - right) * normalSlope, (down - up) * normalSlope, 1f).normalized;
                        normal[y * Size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
                    }
                }

                Write(albedoPath, albedo);
                Write(normalPath, normal);
                Write(maskPath, mask);
            }

            return new SurfaceSet
            {
                Albedo = Import(albedoPath, TextureImporterType.Default, true),
                Normal = Import(normalPath, TextureImporterType.NormalMap, false),
                Mask = Import(maskPath, TextureImporterType.Default, false),
                NormalStrength = normalStrength
            };
        }

        /// <summary>Aplica un juego de texturas a un material URP/Lit (relieve + suavidad + oclusión).</summary>
        public static void Apply(Material material, SurfaceSet set, Vector2 tiling)
        {
            if (material == null || set == null) return;
            material.SetTexture("_BaseMap", set.Albedo);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTexture("_BumpMap", set.Normal);
            material.SetFloat("_BumpScale", set.NormalStrength);
            material.EnableKeyword("_NORMALMAP");
            // Suavidad y metálico salen de la máscara (alfa y rojo); _Smoothness actúa de multiplicador.
            material.SetTexture("_MetallicGlossMap", set.Mask);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", set.Mask);
            material.SetFloat("_OcclusionStrength", 0.8f);
            material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
        }

        private static void Write(string path, Color32[] pixels)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(FullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static Texture2D Import(string path, TextureImporterType type, bool sRGB)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.textureType != type || importer.sRGBTexture != sRGB || importer.wrapMode != TextureWrapMode.Repeat))
            {
                importer.textureType = type;
                importer.sRGBTexture = sRGB;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = Size;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string FullPath(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        // ================================================================== Ruido periódico

        private static int Wrap(int a, int p) => ((a % p) + p) % p;

        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, total = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u * period, v * period, period, seed + o * 101) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return sum / total;
        }

        private static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period);
            int ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx), Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx), fy);
        }

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
    }
}
