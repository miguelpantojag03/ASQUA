using Ascua.Combat;
using Ascua.Core;
using Ascua.Narrative;
using Ascua.World;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Parte D: lo que la trama y el combate nuevo necesitan en la escena.
    ///  - Fijar objetivo: componente TargetLock en Ilse y su marcador ámbar en el HUD.
    ///  - D.2 Presagio de la Noche 2: luz violeta sobre los tejados, cámara propia y pensamientos de Ilse.
    ///  - D.3 El farolillo de papel (callejón de los faroleros) y el farol de los Bravo (entrada del Mercado).
    /// </summary>
    public static partial class AscuaSceneBuilder
    {
        private static readonly Color OmenViolet = new Color(0.55f, 0.4f, 1f);

        private static void BuildStoryAndCombatReading(AscuaAssets assets, Canvas hud, GameObject player)
        {
            BuildTargetLock(hud.transform, player, assets);
            BuildHeraldoOmen(assets, hud);
            BuildStoryProps(assets);
        }

        // ================================================================== Fijar objetivo

        private static void BuildTargetLock(Transform root, GameObject player, AscuaAssets assets)
        {
            // Marcador: halo suave + flecha ámbar apuntando hacia abajo, sobre la criatura fijada.
            RectTransform holder = UIRect("Objetivo_Fijado", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform marker = UIRect("Marcador", holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            CanvasGroup group = marker.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image halo = marker.gameObject.AddComponent<Image>();
            halo.sprite = assets.Combat.DotSprite;
            halo.color = new Color(1f, 0.77f, 0.42f, 0.28f);
            halo.raycastTarget = false;

            RectTransform arrow = UIRect("Flecha", marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(40f, 40f));
            arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = assets.Character.ArrowSprite;
            arrowImage.color = AscuaPalette.Hex("#FFC46B");
            arrowImage.raycastTarget = false;

            TargetLock targetLock = player.AddComponent<TargetLock>();
            Wire(targetLock, "canvasRect", root);
            Wire(targetLock, "marker", marker);
            Wire(targetLock, "markerGroup", group);
        }

        // ================================================================== D.2 · Presagio de la Noche 2

        private static void BuildHeraldoOmen(AscuaAssets assets, Canvas hud)
        {
            GameObject root = new GameObject("Presagio_Noche2");

            // La luz violeta: un "farol" sin farola (orbe + luz sin sombras). Se mueve al tejado elegido.
            Transform omen = new GameObject("Luz_Violeta").transform;
            omen.SetParent(root.transform, false);
            GameObject glow = Prim(PrimitiveType.Sphere, "Orbe", omen, Vector3.zero, Vector3.one * 0.6f, assets.Combat.ShadowBolt, Col.None, null, false);
            Prim(PrimitiveType.Cube, "Marco_Farol", omen, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.08f, 0.5f), assets.Materials.LanternFrame, Col.None, null, false);
            Light light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(omen, false);
            light.type = LightType.Point;
            light.color = OmenViolet;
            light.range = 16f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            SetLayerRecursively(omen.gameObject, IgnoreRaycastLayer);

            // Tejados posibles (norte de La Plaza, entre zonas y sobre el Mercado): se elige el que quede a ~26 m.
            Transform[] spots =
            {
                Pivot("Tejado_Norte_Plaza", root.transform, new Vector3(5f, 12.5f, 24f)),
                Pivot("Tejado_Entre_Zonas", root.transform, new Vector3(-22f, 12f, 22f)),
                Pivot("Tejado_Mercado", root.transform, new Vector3(-44f, 12f, 17f))
            };
            omen.position = spots[0].position;

            // Cámara propia (prioridad alta; apagada hasta la escena).
            GameObject cameraObject = new GameObject("CM_Presagio");
            cameraObject.transform.SetParent(root.transform, false);
            CinemachineCamera omenCamera = cameraObject.AddComponent<CinemachineCamera>();
            omenCamera.Priority = 50;
            LensSettings lens = omenCamera.Lens;
            lens.FieldOfView = 42f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 250f;
            omenCamera.Lens = lens;
            cameraObject.SetActive(false);

            // Pensamientos de Ilse: cursiva lila sobre la barra inferior del canvas de momentos.
            GameObject momentCanvas = GameObject.Find("UI_Momento");
            Transform uiRoot = momentCanvas != null ? momentCanvas.transform : hud.transform;
            RectTransform thoughtRect = UIRect("Pensamiento_Ilse", uiRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1300f, 90f));
            CanvasGroup thoughtGroup = thoughtRect.gameObject.AddComponent<CanvasGroup>();
            thoughtGroup.alpha = 0f;
            thoughtGroup.blocksRaycasts = false;
            TextMeshProUGUI thought = UIText("Texto", thoughtRect, "", 32f, new Color(0.88f, 0.82f, 1f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            thought.fontStyle = FontStyles.Italic;
            Shadow shadow = thought.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            HeraldoOmen omenEvent = root.AddComponent<HeraldoOmen>();
            Wire(omenEvent, "omenRoot", omen);
            Wire(omenEvent, "omenLight", light);
            Wire(omenEvent, "omenGlow", glow.GetComponent<Renderer>());
            WireArray(omenEvent, "spots", spots);
            Wire(omenEvent, "omenCamera", omenCamera);
            Wire(omenEvent, "thoughtGroup", thoughtGroup);
            Wire(omenEvent, "thoughtText", thought);
            Wire(omenEvent, "hudGroup", hud.GetComponent<CanvasGroup>());
        }

        // ================================================================== D.3 · Objetos de las secundarias

        private static void BuildStoryProps(AscuaAssets assets)
        {
            AscuaMaterials m = assets.Materials;
            CharacterAssets ch = assets.Character;
            Transform root = new GameObject("Historia_ParteD").transform;

            // --- El farolillo de papel del padre de Mina: al fondo del callejón, colgado junto al farol viejo.
            Material paper = AscuaAssetFactory.Lit("M_FarolilloPapel", AscuaPalette.Hex("#C8323C"), 0.15f, AscuaPalette.Hex("#E63946") * 0.9f);
            PickupItem lantern = CreatePickup("Farolillo_Papel", root, new Vector3(0.45f, 1.45f, 20.2f), out Transform visual);
            Prim(PrimitiveType.Cylinder, "Papel", visual, Vector3.zero, new Vector3(0.24f, 0.15f, 0.24f), paper, Col.None, null, false);
            Prim(PrimitiveType.Cylinder, "Aro_Sup", visual, new Vector3(0f, 0.155f, 0f), new Vector3(0.18f, 0.01f, 0.18f), m.LanternFrame, Col.None, null, false);
            Prim(PrimitiveType.Cylinder, "Aro_Inf", visual, new Vector3(0f, -0.155f, 0f), new Vector3(0.18f, 0.01f, 0.18f), m.LanternFrame, Col.None, null, false);
            // El pájaro negro pintado en un lado (la única pista: nadie dice su nombre).
            Prim(PrimitiveType.Quad, "Pajaro_Pintado", visual, new Vector3(0f, 0.02f, -0.122f), new Vector3(0.1f, 0.06f, 1f), ch.Soot, Col.None, Quaternion.Euler(0f, 0f, 12f), false);
            lantern.Configure("farolillo_papel", "el farolillo de papel", PickupKind.ObjetoMision, AscuaPalette.RojoMina, "mina_farolillo");
            SmallGlow(lantern.transform, AscuaPalette.RojoMina, 0.8f, 2.2f);
            SetLayerRecursively(lantern.gameObject, IgnoreRaycastLayer);

            // --- El farol de los Bravo: entrada este del Mercado, apagado hasta "Todavía sirvo".
            Vector3 basePos = MercadoCenter + new Vector3(9.4f, 0.1f, -2.6f);
            Transform farol = new GameObject("Farol_Bravo").transform;
            farol.SetParent(root, false);
            farol.position = basePos;
            Prim(PrimitiveType.Cylinder, "Poste", farol, new Vector3(0f, 2.1f, 0f), new Vector3(0.14f, 2.1f, 0.14f), m.Metal);
            Prim(PrimitiveType.Cube, "Brazo", farol, new Vector3(0f, 4.15f, 0.35f), new Vector3(0.07f, 0.07f, 0.8f), m.Metal, Col.None);
            Prim(PrimitiveType.Cube, "Farol_Marco", farol, new Vector3(0f, 3.85f, 0.7f), new Vector3(0.42f, 0.5f, 0.42f), m.LanternFrame, Col.None);
            GameObject flame = Prim(PrimitiveType.Sphere, "Llama", farol, new Vector3(0f, 3.85f, 0.7f), Vector3.one * 0.26f, ch.CandleFlame, Col.None, null, false);
            // La escalera de Teo apoyada en el poste.
            Prim(PrimitiveType.Cube, "Escalera_L", farol, new Vector3(-0.22f, 1.7f, -0.55f), new Vector3(0.06f, 3.5f, 0.06f), m.Wood, Col.None, Quaternion.Euler(-10f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Escalera_R", farol, new Vector3(0.22f, 1.7f, -0.55f), new Vector3(0.06f, 3.5f, 0.06f), m.Wood, Col.None, Quaternion.Euler(-10f, 0f, 0f));
            for (int i = 0; i < 6; i++)
            {
                float y = 0.4f + i * 0.55f;
                Prim(PrimitiveType.Cube, "Peldano", farol, new Vector3(0f, y, -0.55f - (y - 1.7f) * 0.176f), new Vector3(0.46f, 0.04f, 0.05f), m.Wood, Col.None);
            }
            Light farolLight = new GameObject("Luz_Farol").AddComponent<Light>();
            farolLight.transform.SetParent(farol, false);
            farolLight.transform.localPosition = new Vector3(0f, 3.75f, 0.7f);
            farolLight.type = LightType.Point;
            farolLight.color = AscuaPalette.Hex("#FFC46B");
            farolLight.range = 9f;
            farolLight.shadows = LightShadows.None;
            QuestLitObject lit = farol.gameObject.AddComponent<QuestLitObject>();
            Wire(lit, "lightSource", farolLight);
            WireArray(lit, "litRenderers", new[] { flame.GetComponent<Renderer>() });
            SetQuestId(lit, "teo_todavia_sirvo");
        }

        private static void SetQuestId(QuestLitObject lit, string questId)
        {
            UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(lit);
            so.FindProperty("questId").stringValue = questId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
