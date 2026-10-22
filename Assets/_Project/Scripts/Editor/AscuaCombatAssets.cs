using System.Collections.Generic;
using Ascua.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ascua.EditorTools
{
    /// <summary>Assets del combate y de la mejora gráfica de personajes y misiones.</summary>
    public class CombatAssets
    {
        // Criaturas (shader Ascua/SombraCriatura): mismo lenguaje, un solo acento cada una.
        public Material ShadowBody, ShadowVeiled, WhisperBody, SentinelBody, EchoBody;
        public Material ShadowAccent, ShadowBolt, TelegraphStripe;
        // Luz de Ilse.
        public Material Sweep, Beam, Staff, StaffCore;
        // Recuerdos y vecinos.
        public Material Brass, Silver, NpcEye, CartWood;
        public Mesh ArcMesh, BeamMesh, EyeMesh;
        /// <summary>Iconos de línea por tipo de misión (orden de QuestType) + combate.</summary>
        public Sprite[] QuestIcons;
        public Sprite CombatIcon, FrameSprite, DotSprite;
    }

    /// <summary>
    /// Genera los assets del sistema de combate de luz y de la mejora gráfica:
    ///  - Materiales de criaturas (silueta oscura + acento + emisión), barrido de luz, haz cargado y Vara.
    ///  - Mallas: arco del barrido, haz recto y ojo de luz con blend shapes (Alegre / Cansado / Parpadeo).
    ///  - Mina y Teo pasan al shader Ascua/PersonajeTela (tela con trama, borde de luz, sombras reales).
    ///  - Iconos de misión dibujados por código (línea de luz) y marco ámbar 9-slice para los paneles.
    /// </summary>
    public static class AscuaCombatAssets
    {
        private const string MeshesFolder = AscuaAssetFactory.Root + "/Art/Meshes";

        public static CombatAssets Create(AscuaMaterials m, CharacterAssets ch)
        {
            AscuaAssetFactory.EnsureFolder(MeshesFolder);
            Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(AscuaAssetFactory.TexturesFolder + "/T_RuidoSuperficie.png");
            CombatAssets c = new CombatAssets();

            Shader creature = Shader.Find("Ascua/SombraCriatura");
            Shader sweep = Shader.Find("Ascua/BarridoLuz");
            Shader fabric = Shader.Find("Ascua/PersonajeTela");

            // --- Criaturas: cuerpo índigo casi negro; cambia SOLO el acento.
            c.ShadowBody = Creature("M_Sombra_Cuerpo", creature, noise, new Color(0.9f, 0.65f, 2.2f), 0f);
            c.ShadowVeiled = Creature("M_Sombra_Velada", creature, noise, new Color(1.6f, 0.8f, 1.8f), 1f);
            c.WhisperBody = Creature("M_Susurro", creature, noise, new Color(0.7f, 1.05f, 2.2f), 0f);
            c.SentinelBody = Creature("M_Centinela", creature, noise, new Color(1.3f, 0.5f, 2.6f), 0f);
            c.EchoBody = Creature("M_Eco", creature, noise, new Color(1.1f, 0.85f, 2.4f), 0f);
            c.EchoBody.SetFloat("_InnerGlow", 0.3f);
            c.EchoBody.SetFloat("_RimPower", 3.5f);
            c.EchoBody.SetFloat("_RimIntensity", 0.8f);

            c.ShadowAccent = Unlit("M_Sombra_Ojos", new Color(2.2f, 1.6f, 4f, 1f));
            c.ShadowBolt = Unlit("M_ProyectilSombra", new Color(1.1f, 0.5f, 2.6f, 1f));
            c.TelegraphStripe = Unlit("M_AvisoEmbestida", new Color(0.65f, 0.35f, 1.4f, 0.5f));
            AscuaAssetFactory.MakeTransparent(c.TelegraphStripe, false, true);

            // --- Luz de Ilse.
            c.Sweep = AscuaAssetFactory.GetOrCreateMaterial("M_BarridoLuz", sweep);
            c.Sweep.SetColor("_Color", new Color(3.2f, 2.2f, 1.1f, 1f));
            c.Sweep.SetFloat("_Uniform", 0f);
            c.Beam = AscuaAssetFactory.GetOrCreateMaterial("M_HazCargado", sweep);
            c.Beam.SetColor("_Color", new Color(3.6f, 2.6f, 1.3f, 1f));
            c.Beam.SetFloat("_Uniform", 1f);
            EditorUtility.SetDirty(c.Sweep);
            EditorUtility.SetDirty(c.Beam);
            c.Staff = AscuaAssetFactory.Lit("M_VaraLuz", new Color(0.9f, 0.75f, 0.5f), 0.6f, AscuaPalette.Hdr(AscuaPalette.Ambar, 1.4f), 0.3f);
            c.StaffCore = AscuaAssetFactory.Lit("M_VaraLuz_Nucleo", AscuaPalette.Ambar, 0.4f, AscuaPalette.Hdr(AscuaPalette.Ambar, 4f));

            // --- Recuerdos (latón de la lupa, plata del reloj) y vecinos.
            c.Brass = AscuaAssetFactory.Lit("M_Recuerdo_Laton", new Color(0.85f, 0.65f, 0.3f), 0.75f, AscuaPalette.Hdr(new Color(1f, 0.75f, 0.35f), 0.6f), 0.8f);
            c.Silver = AscuaAssetFactory.Lit("M_Recuerdo_Plata", new Color(0.8f, 0.84f, 0.92f), 0.8f, AscuaPalette.Hdr(new Color(0.8f, 0.86f, 1f), 0.5f), 0.85f);
            c.NpcEye = AscuaAssetFactory.Lit("M_Ojo_Vecino", new Color(0.05f, 0.05f, 0.06f), 0.5f, AscuaPalette.Hdr(new Color(1f, 0.93f, 0.8f), 2f));
            c.CartWood = AscuaAssetFactory.Lit("M_Carrito_Madera", new Color(0.42f, 0.33f, 0.28f), 1f);
            if (m.Surfaces != null) AscuaSurfaceTextures.Apply(c.CartWood, m.Surfaces.Wood, Vector2.one);

            // --- Vecinos: tela con trama, borde de luz y sombras reales (antes: color plano).
            Fabric(m.MinaBody, fabric, noise, ch.Fabric, new Color(0.36f, 0.39f, 0.47f), Color.black, 0.45f, 7f, new Color(0.45f, 0.55f, 0.95f));
            Fabric(m.TeoBody, fabric, noise, ch.Fabric, new Color(0.34f, 0.38f, 0.43f), Color.black, 0.4f, 5f, new Color(0.45f, 0.55f, 0.9f));
            // Acentos de silueta (bufanda de Mina, delantal de Teo): tela gruesa de punto + emisión cálida.
            Fabric(m.MinaAccent, fabric, noise, ch.Fabric, AscuaPalette.RojoMina, AscuaPalette.Hdr(AscuaPalette.RojoMina, 0.55f), 0.55f, 12f, new Color(1f, 0.45f, 0.5f));
            Fabric(m.TeoAccent, fabric, noise, ch.Fabric, AscuaPalette.TurquesaTeo, AscuaPalette.Hdr(AscuaPalette.TurquesaTeo, 0.45f), 0.5f, 10f, new Color(0.5f, 1f, 0.95f));

            // --- Mallas.
            c.ArcMesh = BuildMesh("M_ArcoBarrido", BuildArc);
            c.BeamMesh = BuildMesh("M_HazRecto", BuildBeam);
            c.EyeMesh = BuildEyeMesh();

            // --- Sprites de interfaz.
            c.QuestIcons = new[]
            {
                Icon("UI_Icono_Rescate", IconRescue),
                Icon("UI_Icono_Recoleccion", IconGather),
                Icon("UI_Icono_Escolta", IconEscort),
                Icon("UI_Icono_Dialogo", IconDialogue),
                Icon("UI_Icono_Trato", IconDeal),
                Icon("UI_Icono_Entrega", IconDelivery)
            };
            c.CombatIcon = Icon("UI_Icono_Combate", IconCombat);
            c.FrameSprite = CreateFrameSprite();
            c.DotSprite = CreateDotSprite();
            return c;
        }

        // ================================================================== Materiales

        private static Material Creature(string name, Shader shader, Texture2D noise, Color accent, float cracks)
        {
            Material material = AscuaAssetFactory.GetOrCreateMaterial(name, shader);
            material.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.15f, 1f));
            material.SetColor("_AccentColor", accent);
            material.SetTexture("_NoiseMap", noise);
            material.SetFloat("_NoiseScale", 1.6f);
            material.SetFloat("_RimPower", 2.2f);
            material.SetFloat("_RimIntensity", 1.4f);
            material.SetFloat("_InnerGlow", 0.12f);
            material.SetFloat("_CrackStrength", cracks);
            material.SetFloat("_Dissolve", 0f);
            material.SetFloat("_Reveal", 1f);
            material.SetFloat("_FlashAmount", 0f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Unlit(string name, Color color)
        {
            Material material = AscuaAssetFactory.GetOrCreateMaterial(name, AscuaAssetFactory.UnlitShader);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Fabric(Material material, Shader shader, Texture2D noise, Texture2D weave, Color baseColor, Color emission, float weaveStrength, float tiling, Color rim)
        {
            if (material == null || shader == null) return;
            material.shader = shader;
            material.SetColor("_BaseColor", baseColor);
            material.SetTexture("_BaseMap", noise);
            material.SetFloat("_NoiseStrength", 0.3f);
            material.SetTexture("_DetailMap", weave);
            material.SetFloat("_DetailStrength", weaveStrength);
            material.SetFloat("_DetailTiling", tiling);
            material.SetColor("_EmissionColor", emission);
            material.SetColor("_RimColor", rim);
            material.SetFloat("_RimPower", 3f);
            material.SetFloat("_RimIntensity", 0.55f);
            material.SetFloat("_Wrap", 0.35f);
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
        }

        // ================================================================== Mallas

        private static Mesh BuildMesh(string name, System.Action<Mesh> build)
        {
            string path = $"{MeshesFolder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.ClearBlendShapes();
            build(mesh);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Sector de anillo de 150° en el plano XZ (hacia +Z). UV.x a lo largo, UV.y de dentro a fuera.</summary>
        private static void BuildArc(Mesh mesh)
        {
            const int segments = 28;
            const float inner = 0.55f, outer = 1.55f, arc = 150f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = Mathf.Deg2Rad * Mathf.Lerp(-arc * 0.5f, arc * 0.5f, u);
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                vertices.Add(dir * inner);
                vertices.Add(dir * outer);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                if (i == segments) continue;
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
        }

        /// <summary>Haz recto: plano XZ de 1 m de ancho y 1 m de largo hacia +Z (se escala por código).</summary>
        private static void BuildBeam(Mesh mesh)
        {
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            mesh.RecalculateNormals();
        }

        /// <summary>
        /// Ojo de luz: disco elíptico ligeramente abombado que mira a +Z, con 3 blend shapes:
        /// "Alegre" (el borde inferior sube: ojo en arco), "Cansado" (el superior baja) y "Parpadeo".
        /// </summary>
        private static Mesh BuildEyeMesh()
        {
            return BuildMesh("M_OjoLuz", mesh =>
            {
                const int rings = 4, segments = 20;
                List<Vector3> vertices = new List<Vector3> { new Vector3(0f, 0f, 0.12f) };
                List<int> triangles = new List<int>();
                for (int r = 1; r <= rings; r++)
                {
                    float radius = (float)r / rings;
                    for (int s = 0; s < segments; s++)
                    {
                        float a = Mathf.PI * 2f * s / segments;
                        float z = 0.12f * (1f - radius * radius);
                        vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f * radius, Mathf.Sin(a) * 0.5f * radius, z));
                    }
                }
                for (int s = 0; s < segments; s++)
                {
                    triangles.AddRange(new[] { 0, 1 + (s + 1) % segments, 1 + s });
                }
                for (int r = 1; r < rings; r++)
                {
                    int inner = 1 + (r - 1) * segments, outer = 1 + r * segments;
                    for (int s = 0; s < segments; s++)
                    {
                        int s1 = (s + 1) % segments;
                        triangles.AddRange(new[] { inner + s, inner + s1, outer + s, inner + s1, outer + s1, outer + s });
                    }
                }
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();

                Vector3[] joy = new Vector3[vertices.Count];
                Vector3[] tired = new Vector3[vertices.Count];
                Vector3[] blink = new Vector3[vertices.Count];
                for (int i = 0; i < vertices.Count; i++)
                {
                    float y = vertices[i].y;
                    // Alegre: la mitad inferior se pliega hacia arriba → "^" (arco de luz).
                    joy[i] = y < 0f ? new Vector3(0f, -y * 1.6f, 0f) : new Vector3(0f, y * 0.1f, 0f);
                    // Cansado: el párpado superior cae hasta casi el centro.
                    tired[i] = y > 0f ? new Vector3(0f, -y * 0.82f, 0f) : Vector3.zero;
                    // Parpadeo: todo se cierra en una línea.
                    blink[i] = new Vector3(0f, -y * 0.94f, 0f);
                }
                mesh.AddBlendShapeFrame("Alegre", 100f, joy, null, null);
                mesh.AddBlendShapeFrame("Cansado", 100f, tired, null, null);
                mesh.AddBlendShapeFrame("Parpadeo", 100f, blink, null, null);
            });
        }

        // ================================================================== Iconos (línea de luz)

        private delegate float Shape(Vector2 p);

        /// <summary>Icono blanco de 64 px con trazo de ~3 px y antialias (se tiñe con el color de estado).</summary>
        private static Sprite Icon(string name, Shape distance)
        {
            const int size = 64;
            const float stroke = 2.6f / 64f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float d = distance(p);
                    float alpha = Mathf.Clamp01((stroke - d) * size * 0.9f + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            return SaveSprite(name, size, pixels, Vector4.zero);
        }

        private static float Ring(Vector2 p, Vector2 c, float r) => Mathf.Abs((p - c).magnitude - r);

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude;
        }

        private static float Polyline(Vector2 p, params Vector2[] points)
        {
            float d = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++) d = Mathf.Min(d, Segment(p, points[i], points[i + 1]));
            return d;
        }

        /// <summary>Rescate de Esperanza: una llama.</summary>
        private static float IconRescue(Vector2 p)
        {
            return Polyline(p, new Vector2(0f, 0.38f), new Vector2(0.17f, 0.08f), new Vector2(0.2f, -0.12f), new Vector2(0.1f, -0.3f),
                new Vector2(-0.1f, -0.3f), new Vector2(-0.2f, -0.12f), new Vector2(-0.12f, 0.06f), new Vector2(0f, 0.38f));
        }

        /// <summary>Recolección: luciérnaga (punto con destellos).</summary>
        private static float IconGather(Vector2 p)
        {
            float d = Ring(p, Vector2.zero, 0.11f);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI * 0.25f + i * Mathf.PI * 0.5f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d = Mathf.Min(d, Segment(p, dir * 0.2f, dir * 0.34f));
            }
            return d;
        }

        /// <summary>Escolta: dos figuras (cabezas) y un camino punteado.</summary>
        private static float IconEscort(Vector2 p)
        {
            float d = Mathf.Min(Ring(p, new Vector2(-0.18f, 0.18f), 0.08f), Ring(p, new Vector2(0.18f, 0.12f), 0.07f));
            d = Mathf.Min(d, Polyline(p, new Vector2(-0.3f, -0.08f), new Vector2(-0.18f, 0.06f), new Vector2(-0.06f, -0.08f)));
            d = Mathf.Min(d, Polyline(p, new Vector2(0.08f, -0.1f), new Vector2(0.18f, 0.02f), new Vector2(0.28f, -0.1f)));
            for (int i = 0; i < 4; i++) d = Mathf.Min(d, Segment(p, new Vector2(-0.3f + i * 0.18f, -0.3f), new Vector2(-0.22f + i * 0.18f, -0.3f)));
            return d;
        }

        /// <summary>Diálogo: bocadillo.</summary>
        private static float IconDialogue(Vector2 p)
        {
            return Polyline(p, new Vector2(-0.32f, 0.26f), new Vector2(0.32f, 0.26f), new Vector2(0.32f, -0.12f), new Vector2(-0.04f, -0.12f),
                new Vector2(-0.18f, -0.3f), new Vector2(-0.16f, -0.12f), new Vector2(-0.32f, -0.12f), new Vector2(-0.32f, 0.26f));
        }

        /// <summary>Trato del Heraldo: rombo con un punto.</summary>
        private static float IconDeal(Vector2 p)
        {
            float d = Polyline(p, new Vector2(0f, 0.36f), new Vector2(0.3f, 0f), new Vector2(0f, -0.36f), new Vector2(-0.3f, 0f), new Vector2(0f, 0.36f));
            return Mathf.Min(d, Ring(p, Vector2.zero, 0.05f));
        }

        /// <summary>Entrega: sobre.</summary>
        private static float IconDelivery(Vector2 p)
        {
            float d = Polyline(p, new Vector2(-0.32f, 0.22f), new Vector2(0.32f, 0.22f), new Vector2(0.32f, -0.22f), new Vector2(-0.32f, -0.22f), new Vector2(-0.32f, 0.22f));
            return Mathf.Min(d, Polyline(p, new Vector2(-0.32f, 0.22f), new Vector2(0f, -0.04f), new Vector2(0.32f, 0.22f)));
        }

        /// <summary>Combate: sol de luz (anillo con 8 rayos).</summary>
        private static float IconCombat(Vector2 p)
        {
            float d = Ring(p, Vector2.zero, 0.13f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d = Mathf.Min(d, Segment(p, dir * 0.22f, dir * (i % 2 == 0 ? 0.38f : 0.31f)));
            }
            return d;
        }

        // ================================================================== Marco y punto

        /// <summary>Marco 9-slice de 64 px: filo ámbar de 2 px con pequeños rombos en las esquinas.</summary>
        private static Sprite CreateFrameSprite()
        {
            const int size = 64;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float inset = 5f;
                    float dx = Mathf.Min(x - inset, size - 1 - inset - x);
                    float dy = Mathf.Min(y - inset, size - 1 - inset - y);
                    float line = 0f;
                    if (dx >= -0.5f && dy >= -0.5f) line = Mathf.Clamp01(2f - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)));
                    // Rombos en las esquinas (5 px).
                    float cx = Mathf.Min(x, size - 1 - x) - inset;
                    float cy = Mathf.Min(y, size - 1 - y) - inset;
                    float diamond = Mathf.Clamp01(3.5f - (Mathf.Abs(cx) + Mathf.Abs(cy)));
                    float alpha = Mathf.Max(line * 0.9f, diamond);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            return SaveSprite("UI_MarcoAmbar", size, pixels, new Vector4(16f, 16f, 16f, 16f));
        }

        private static Sprite CreateDotSprite()
        {
            const int size = 32;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude;
                    float alpha = Mathf.Clamp01((0.42f - d) * size);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            return SaveSprite("UI_Punto", size, pixels, Vector4.zero);
        }

        private static Sprite SaveSprite(string name, int size, Color32[] pixels, Vector4 border)
        {
            string path = $"{AscuaAssetFactory.TexturesFolder}/{name}.png";
            AscuaAssetFactory.SaveTexture(name, size, pixels, false);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
