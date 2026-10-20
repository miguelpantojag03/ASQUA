using System.Collections.Generic;
using System.IO;
using Ascua.Core;
using UnityEditor;
using UnityEngine;

namespace Ascua.EditorTools
{
    /// <summary>Materiales, mallas y sprites de personajes y narrativa (Fase 3).</summary>
    public class CharacterAssets
    {
        public Material IlseBody, IlseCape, IlseHood, IlseEyes;
        public Material Pulse;
        public Material HeraldoBody, HeraldoCape, HeraldoLantern, HeraldoEyes;
        public Material Indicator, CandleWax, CandleFlame;
        public Mesh CapeMesh, HoodMesh;
        public Sprite WhiteSprite;
    }

    /// <summary>
    /// Genera lo que necesita la Fase 3 sin modelar nada:
    ///  - Cuerpo de Ilse con shader "Ascua/RecuerdoTranslucido" (ruido + Fresnel).
    ///  - Capa y capucha: mallas procedurales con shader "Ascua/CapaViento" (viento + latigazo).
    ///  - Ojos de luz, Pulso, Heraldo, indicador de misión y velas.
    /// </summary>
    public static class AscuaCharacterAssets
    {
        private const string MeshesFolder = AscuaAssetFactory.Root + "/Art/Meshes";

        public static CharacterAssets Create(AscuaMaterials m)
        {
            AscuaAssetFactory.EnsureFolder(MeshesFolder);
            CharacterAssets c = new CharacterAssets();
            Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(AscuaAssetFactory.TexturesFolder + "/T_RuidoSuperficie.png");

            Shader bodyShader = Shader.Find("Ascua/RecuerdoTranslucido");
            Shader capeShader = Shader.Find("Ascua/CapaViento");
            if (bodyShader == null || capeShader == null)
            {
                Debug.LogError("[Ascua] No se encontraron los shaders de Ascua (Assets/_Project/Shaders). Se usan materiales Lit de respaldo.");
            }

            // --- Cuerpo translúcido con borde luminoso (sustituye al Lit de la Fase 1, mismo asset)
            c.IlseBody = AscuaAssetFactory.GetOrCreateMaterial("M_Ilse_Base", bodyShader != null ? bodyShader : AscuaAssetFactory.LitShader);
            c.IlseBody.SetColor("_BaseColor", new Color(0.75f, 0.84f, 1f, 0.5f));
            c.IlseBody.SetTexture("_BaseMap", noise);
            c.IlseBody.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
            c.IlseBody.SetFloat("_NoiseStrength", 0.35f);
            c.IlseBody.SetColor("_EmissionColor", AscuaPalette.Hdr(AscuaPalette.Hex("#5A74C4"), 0.45f));
            c.IlseBody.SetColor("_RimColor", new Color(0.65f, 0.78f, 1.2f, 1f));
            c.IlseBody.SetFloat("_RimPower", 2.6f);
            c.IlseBody.SetFloat("_RimIntensity", 1.6f);
            c.IlseBody.SetFloat("_RimOpacity", 0.35f);
            EditorUtility.SetDirty(c.IlseBody);

            // --- Capa y capucha doradas pálidas (#FFE3A0) con viento
            c.IlseCape = CapeMaterial("M_Ilse_Capa", capeShader, noise, AscuaPalette.Hex("#FFE3A0"), 0.9f, 0.06f);
            c.IlseHood = CapeMaterial("M_Ilse_Capucha", capeShader, noise, AscuaPalette.Hex("#FFE3A0"), 0.92f, 0.012f);

            // --- Ojos de luz (la intensidad la anima CharacterFace)
            c.IlseEyes = AscuaAssetFactory.Lit("M_Ilse_Ojos", new Color(1f, 0.96f, 0.86f), 0.2f, AscuaPalette.Hdr(new Color(1f, 0.95f, 0.82f), 2.4f));

            // --- Pulso de luz: esfera ámbar transparente de doble cara
            c.Pulse = AscuaAssetFactory.GetOrCreateMaterial("M_Pulso", AscuaAssetFactory.UnlitShader);
            c.Pulse.SetColor("_BaseColor", new Color(1f, 0.769f, 0.42f, 0.45f));
            AscuaAssetFactory.MakeTransparent(c.Pulse, false, true);
            // Aditivo: la onda suma luz (y con color HDR, el bloom la hace brillar).
            c.Pulse.SetFloat("_Blend", 2f);
            c.Pulse.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);

            // --- Heraldo: casi negro con capa que ondea y farol violeta (#3B2A6B)
            Color violet = AscuaPalette.Hex("#3B2A6B");
            c.HeraldoBody = AscuaAssetFactory.Lit("M_Heraldo_Base", AscuaPalette.Hex("#151225"), 0.35f);
            c.HeraldoCape = CapeMaterial("M_Heraldo_Capa", capeShader, noise, AscuaPalette.Hex("#1B1730"), 0.95f, 0.09f);
            if (c.HeraldoCape.HasProperty("_RimColor")) c.HeraldoCape.SetColor("_RimColor", AscuaPalette.Hdr(violet, 2.2f));
            c.HeraldoLantern = AscuaAssetFactory.Lit("M_Heraldo_Farol", violet, 0.3f, AscuaPalette.Hdr(violet, 4f));
            c.HeraldoEyes = AscuaAssetFactory.Lit("M_Heraldo_Ojos", violet, 0.2f, AscuaPalette.Hdr(new Color(0.62f, 0.45f, 1f), 2.5f));

            // --- Indicador de misión y velas
            c.Indicator = AscuaAssetFactory.Lit("M_IndicadorMision", AscuaPalette.Ambar, 0.3f, new Color(0.01f, 0.01f, 0.01f));
            c.CandleWax = AscuaAssetFactory.Lit("M_Vela_Cera", AscuaPalette.Hex("#EDE3CF"), 0.35f);
            c.CandleFlame = AscuaAssetFactory.Lit("M_Vela_Llama", AscuaPalette.Ambar, 0.2f, AscuaPalette.Hdr(AscuaPalette.Ambar, 4f));

            c.CapeMesh = SaveMesh(CreateCapeMesh(), "MESH_Capa");
            c.HoodMesh = SaveMesh(CreateHoodMesh(), "MESH_Capucha");
            c.WhiteSprite = CreateWhiteSprite();
            return c;
        }

        private static Material CapeMaterial(string name, Shader shader, Texture2D noise, Color color, float alpha, float wind)
        {
            Material material = AscuaAssetFactory.GetOrCreateMaterial(name, shader != null ? shader : AscuaAssetFactory.LitShader);
            material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            material.SetTexture("_BaseMap", noise);
            if (shader != null)
            {
                material.SetFloat("_NoiseStrength", 0.4f);
                material.SetColor("_EmissionColor", new Color(color.r * 0.22f, color.g * 0.2f, color.b * 0.15f, 1f));
                material.SetColor("_RimColor", new Color(1.2f, 1f, 0.6f, 1f));
                material.SetFloat("_RimPower", 3f);
                material.SetFloat("_RimIntensity", 0.9f);
                material.SetFloat("_WindStrength", wind);
                material.SetFloat("_WindSpeed", 2.2f);
                material.SetFloat("_WindFrequency", 3f);
                material.SetFloat("_Flick", 0f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Capa: banda curva que rodea la espalda (de -110º a 110º), más ancha abajo.
        /// uv.y = 1 en los hombros y 0 en las puntas (el shader mueve más las puntas).
        /// </summary>
        private static Mesh CreateCapeMesh()
        {
            const int columns = 9;
            const int rows = 10;
            const float length = 0.62f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int r = 0; r < rows; r++)
            {
                float v = r / (rows - 1f);
                float radius = Mathf.Lerp(0.2f, 0.34f, v);
                for (int col = 0; col < columns; col++)
                {
                    float u = col / (columns - 1f);
                    float angle = Mathf.Lerp(-110f, 110f, u) * Mathf.Deg2Rad;
                    Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                    vertices.Add(outward * radius + Vector3.down * (v * length));
                    normals.Add(outward);
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }

            for (int r = 0; r < rows - 1; r++)
            {
                for (int col = 0; col < columns - 1; col++)
                {
                    int a = r * columns + col;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    // Orden horario visto desde fuera (cara frontal en Unity).
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh { name = "MESH_Capa" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Capucha: casquete esférico abierto por delante (deja ver los ojos).</summary>
        private static Mesh CreateHoodMesh()
        {
            const int longitudeSteps = 14;
            const int latitudeSteps = 8;
            const float radius = 0.31f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int la = 0; la <= latitudeSteps; la++)
            {
                // De -25º (bajo la barbilla, por detrás) a 90º (coronilla).
                float lat = Mathf.Lerp(-25f, 90f, la / (float)latitudeSteps) * Mathf.Deg2Rad;
                for (int lo = 0; lo <= longitudeSteps; lo++)
                {
                    // Longitud medida desde el frente (+Z); se deja abierto un hueco de ±55º.
                    float lon = Mathf.Lerp(55f, 305f, lo / (float)longitudeSteps) * Mathf.Deg2Rad;
                    Vector3 n = new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
                    vertices.Add(n * radius);
                    normals.Add(n);
                    uvs.Add(new Vector2(lo / (float)longitudeSteps, la / (float)latitudeSteps));
                }
            }

            int stride = longitudeSteps + 1;
            for (int la = 0; la < latitudeSteps; la++)
            {
                for (int lo = 0; lo < longitudeSteps; lo++)
                {
                    int a = la * stride + lo;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh { name = "MESH_Capucha" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshesFolder}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            // Se reutiliza el asset (mismo GUID) copiando la geometría nueva.
            existing.Clear();
            existing.SetVertices(new List<Vector3>(mesh.vertices));
            existing.SetNormals(new List<Vector3>(mesh.normals));
            existing.SetUVs(0, new List<Vector2>(mesh.uv));
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>Sprite blanco de 4x4 para barras rellenables (Image tipo Filled).</summary>
        private static Sprite CreateWhiteSprite()
        {
            string path = AscuaAssetFactory.TexturesFolder + "/UI_Blanco.png";
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path)))
            {
                Color32[] pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                AscuaAssetFactory.SaveTexture("UI_Blanco", 4, pixels, false);
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
