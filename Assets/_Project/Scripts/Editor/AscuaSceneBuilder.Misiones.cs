using Ascua.Characters;
using Ascua.Core;
using Ascua.Quests;
using Ascua.World;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Parte C: Perpetua (vecina nueva, protagonista de las escoltas con energía limitada),
    /// la Casa de ancianos (destino de esas escoltas) y los sets de las noches 2-4.
    /// </summary>
    public static partial class AscuaSceneBuilder
    {
        /// <summary>Perpetua: anciana pequeña y algo encorvada, chal lila y bastón. Vive en la Casa de ancianos.</summary>
        private static void BuildPerpetua(AscuaAssets assets, ZoneHope zone)
        {
            CombatAssets c = assets.Combat;
            Vector3 start = MercadoCenter + new Vector3(7.5f, 0.12f, -1.5f);
            ChibiParts parts = BuildChibi("NPC_Perpetua", start, 120f, 0.74f, 0.52f, c.PerpetuaBody, assets.ChibiController, true,
                new Vector3(0.4f, 0.27f, 0.33f), 0.9f);

            // Encorvada: el torso se inclina un poco hacia delante (la animación sigue funcionando encima).
            parts.Rig.localRotation = Quaternion.Euler(10f, 0f, 0f);
            GameObject shawl = Prim(PrimitiveType.Cylinder, "Chal", parts.Rig, new Vector3(0f, 0.52f, 0f), new Vector3(0.5f, 0.08f, 0.44f), c.PerpetuaShawl, Col.None);
            GameObject shawlTail = Prim(PrimitiveType.Cube, "ChalCaida", parts.Rig, new Vector3(0f, 0.36f, -0.2f), new Vector3(0.38f, 0.3f, 0.04f), c.PerpetuaShawl, Col.None, Quaternion.Euler(-10f, 0f, 0f));
            Prim(PrimitiveType.Sphere, "Mono", parts.Head, new Vector3(0f, 0.42f, -0.16f), Vector3.one * 0.2f, c.PerpetuaBody, Col.None);
            Transform armR = parts.Rig.Find("ArmR");
            if (armR != null)
            {
                Prim(PrimitiveType.Cylinder, "Baston", armR, new Vector3(0.02f, -0.62f, 0.08f), new Vector3(0.035f, 0.36f, 0.035f), assets.Materials.Wood, Col.None);
            }

            Renderer[] accents = { shawl.GetComponent<Renderer>(), shawlTail.GetComponent<Renderer>() };
            Vector3 m = MercadoCenter;
            Transform[] patrol = CreateWaypoints("Waypoints_Perpetua", new[]
            {
                m + new Vector3(7f, 0.1f, -2f), m + new Vector3(8.5f, 0.1f, 1f), m + new Vector3(6f, 0.1f, 2.5f)
            });

            Color lilac = new Color(0.78f, 0.64f, 0.85f);
            SetupNpc(parts, "perpetua", "Perpetua", lilac, accents, 1.2f, 0.28f, assets.Narrative.Nights.PerpetuaIdle, assets.Character.Indicator);
            AddNpcEyes(parts, c, 0.52f, new Color(1f, 0.92f, 0.85f));

            CharacterAI ai = parts.Root.GetComponent<CharacterAI>();
            SerializedObject so = new SerializedObject(ai);
            so.FindProperty("walkSpeed").floatValue = 0.85f; // a su ritmo: la escolta se nota
            so.FindProperty("worriedSpeed").floatValue = 0.55f;
            so.ApplyModifiedPropertiesWithoutUndo();
            ai.Configure(zone, patrol, accents, lilac);
        }

        /// <summary>La Casa de ancianos: puerta del edificio este de La Plaza con letrero y luz cálida.</summary>
        private static void BuildCasaAncianos(AscuaMaterials m)
        {
            Transform casa = new GameObject("Casa_Ancianos").transform;

            // Letrero de madera sobre la puerta del Edificio_E (fachada hacia la plaza, x = 14.5).
            Prim(PrimitiveType.Cube, "Letrero", casa, new Vector3(14.32f, 2.95f, 1f), new Vector3(0.06f, 0.42f, 2.2f), m.Wood, Col.None);
            GameObject textObject = new GameObject("Texto_Letrero");
            textObject.transform.SetParent(casa, false);
            textObject.transform.position = new Vector3(14.27f, 2.95f, 1f);
            textObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.text = "Casa de ancianos";
            text.fontSize = 2.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.88f, 0.65f);
            text.rectTransform.sizeDelta = new Vector2(2.1f, 0.4f);

            // Farolillo sobre la puerta: siempre encendido (la casa nunca apaga del todo).
            Light lamp = new GameObject("Luz_Casa").AddComponent<Light>();
            lamp.transform.SetParent(casa, false);
            lamp.transform.position = new Vector3(13.7f, 2.6f, 1f);
            lamp.type = LightType.Point;
            lamp.color = AscuaPalette.Ambar;
            lamp.range = 4.5f;
            lamp.intensity = 1.1f;
            lamp.shadows = LightShadows.None;

            // Destino de las escoltas (en la acera, delante de la puerta).
            QuestPoint point = new GameObject("Punto_Casa_Ancianos").AddComponent<QuestPoint>();
            point.transform.SetParent(casa, false);
            point.transform.position = new Vector3(12.9f, 0.1f, 1f);
            point.Configure("casa_ancianos");
            MarkStatic(casa.gameObject);
        }

        /// <summary>Sets de las noches siguientes (recargados desde disco).</summary>
        private static NightQuestSet[] LaterNightSets()
        {
            return new[]
            {
                FreshAsset<NightQuestSet>("Noche_02_LoQueSeGuarda"),
                FreshAsset<NightQuestSet>("Noche_03_VientoDelNorte"),
                FreshAsset<NightQuestSet>("Noche_04_LaVigilia")
            };
        }
    }
}
