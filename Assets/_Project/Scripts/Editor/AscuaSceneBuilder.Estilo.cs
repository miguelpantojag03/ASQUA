using Ascua.Combat;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Partes A + B: sistema de diseño único para toda la UI, jerarquía visual (diálogo / combate),
    /// indicador de amenaza, destello de daño e impacto de los golpes (freeze-frame + sacudida).
    /// </summary>
    public static partial class AscuaSceneBuilder
    {
        private const string StyleKitPath = AscuaAssetFactory.SettingsFolder + "/UIStyleKit.asset";

        /// <summary>Kit de estilo (se crea una vez; si lo editas en el Inspector, se respeta).</summary>
        private static UIStyleKit GetStyleKit(AscuaAssets assets)
        {
            UIStyleKit kit = AssetDatabase.LoadAssetAtPath<UIStyleKit>(StyleKitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<UIStyleKit>();
                AssetDatabase.CreateAsset(kit, StyleKitPath);
            }
            // Los sprites son generados: se vuelven a enlazar siempre (por si se regeneraron).
            kit.frameSprite = assets.Combat.FrameSprite;
            kit.panelSprite = assets.Combat.PanelSprite;
            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static void Style(Transform target, UIStyleKit kit, UIRole role, bool keepBackground = false)
        {
            if (target == null) return;
            UIStyledPanel panel = target.GetComponent<UIStyledPanel>();
            if (panel == null) panel = target.gameObject.AddComponent<UIStyledPanel>();
            panel.Configure(kit, role, keepBackground);
        }

        /// <summary>Todos los botones bajo 'root' con el mismo marco (sus tintes de Button se mantienen).</summary>
        private static void StyleButtons(Transform root, UIStyleKit kit)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) Style(button.transform, kit, UIRole.Button, true);
        }

        // ================================================================== HUD

        /// <summary>Aplica el sistema de diseño al HUD y monta las capas de prioridad visual.</summary>
        private static void ApplyHudDesign(Canvas canvas, GameObject player, AscuaAssets assets)
        {
            UIStyleKit kit = GetStyleKit(assets);
            Transform root = canvas.transform;

            // --- Mismo marco para todo: energía, noche, Esperanza, objetivo, misiones, avisos, diálogo.
            Style(root.Find("Energia"), kit, UIRole.Hud);
            Style(root.Find("Noche"), kit, UIRole.Hud);
            Style(root.Find("Esperanza"), kit, UIRole.Hud);
            Style(root.Find("Objetivo_Actual"), kit, UIRole.Hud);
            Style(root.Find("Misiones"), kit, UIRole.Panel);
            Style(root.Find("Aviso_Interaccion"), kit, UIRole.Prompt);
            Style(root.Find("Aviso"), kit, UIRole.Toast);
            Style(root.Find("Subtitulos"), kit, UIRole.Prompt);
            Style(root.Find("Debug_F1"), kit, UIRole.Panel);
            Transform box = root.Find("Dialogo/Caja");
            if (box != null)
            {
                // La línea suelta de antes se sustituye por el marco dorado completo.
                Transform oldLine = box.Find("Filo");
                if (oldLine != null) Object.DestroyImmediate(oldLine.gameObject);
                Style(box, kit, UIRole.Dialogue);
            }
            Transform inventory = root.Find("Inventario");
            if (inventory != null)
            {
                foreach (Transform slot in inventory)
                {
                    if (slot.name.StartsWith("Hueco_")) Style(slot, kit, UIRole.Slot);
                }
            }
            Style(root.Find("Dialogo/Resumen_Mision"), kit, UIRole.Panel);
            Transform dialogue = root.Find("Dialogo");
            if (dialogue != null) StyleButtons(dialogue, kit);
            Transform summary = root.Find("Resumen_Noche");
            if (summary != null) StyleButtons(summary, kit);

            BuildDamageFlash(root);
            BuildThreatIndicator(root, assets);
            BuildHudFocus(root);
        }

        /// <summary>Destello de pantalla completa (0.15 s) al recibir daño, sobre la viñeta.</summary>
        private static void BuildDamageFlash(Transform root)
        {
            Transform vignette = root.Find("Vineta_Dano");
            if (vignette == null) return;
            RectTransform flash = UIRect("Destello_Dano", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            flash.SetSiblingIndex(vignette.GetSiblingIndex() + 1);
            Image image = flash.gameObject.AddComponent<Image>();
            image.color = new Color(0.85f, 0.7f, 1f, 1f);
            image.raycastTarget = false;
            CanvasGroup group = flash.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Wire(vignette.GetComponent<DamageVignette>(), "flashGroup", group);
        }

        /// <summary>3 marcadores de "amenaza detectada" para el borde de la pantalla.</summary>
        private static void BuildThreatIndicator(Transform root, AscuaAssets assets)
        {
            RectTransform holder = UIRect("Amenazas", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform[] markers = new RectTransform[3];
            for (int i = 0; i < markers.Length; i++)
            {
                RectTransform marker = UIRect("Amenaza_" + i, holder, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
                CanvasGroup group = marker.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                Image glow = marker.gameObject.AddComponent<Image>();
                glow.sprite = assets.Combat.DotSprite;
                glow.color = new Color(0.62f, 0.42f, 1f, 0.55f);
                glow.raycastTarget = false;
                RectTransform arrow = UIRect("Flecha", marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(38f, 38f));
                Image arrowImage = arrow.gameObject.AddComponent<Image>();
                arrowImage.sprite = assets.Character.ArrowSprite;
                arrowImage.color = new Color(0.85f, 0.75f, 1f, 1f);
                arrowImage.raycastTarget = false;
                markers[i] = marker;
            }
            ThreatIndicator indicator = holder.gameObject.AddComponent<ThreatIndicator>();
            Wire(indicator, "canvasRect", root);
            WireArray(indicator, "markers", markers);
        }

        /// <summary>
        /// Reparte el HUD en dos capas con su propia opacidad (sin tocar la de cada panel):
        /// secundaria (objetivo, Esperanza, noche, misiones, inventario, flecha, tutorial) y combate (energía, combo).
        /// </summary>
        private static void BuildHudFocus(Transform root)
        {
            string[] secondaryNames = { "Objetivo_Actual", "Esperanza", "Noche", "Misiones_Compacto", "Misiones", "Inventario", "Flecha_Objetivo", "Tutorial" };
            string[] combatNames = { "Energia", "Combo" };

            CanvasGroup secondary = Layer(root, "Capa_Secundaria", secondaryNames);
            CanvasGroup combat = Layer(root, "Capa_Combate", combatNames);

            HudFocus focus = root.gameObject.AddComponent<HudFocus>();
            Wire(focus, "secondaryLayer", secondary);
            Wire(focus, "combatLayer", combat);
            Wire(focus, "energyBlock", combat.transform.Find("Energia"));
        }

        private static CanvasGroup Layer(Transform root, string name, string[] children)
        {
            RectTransform layer = UIRect(name, root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            int index = int.MaxValue;
            foreach (string child in children)
            {
                Transform t = root.Find(child);
                if (t != null) index = Mathf.Min(index, t.GetSiblingIndex());
            }
            if (index != int.MaxValue) layer.SetSiblingIndex(index);
            foreach (string child in children)
            {
                Transform t = root.Find(child);
                if (t != null) t.SetParent(layer, false);
            }
            CanvasGroup group = layer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            return group;
        }

        // ================================================================== Pausa, Diario, foto, título

        private static void ApplyMenuDesign(AscuaAssets assets)
        {
            UIStyleKit kit = GetStyleKit(assets);
            GameObject pause = GameObject.Find("UI_Pausa");
            if (pause != null)
            {
                Style(pause.transform.Find("Pausa/Accesibilidad"), kit, UIRole.Panel);
                Style(pause.transform.Find("Diario/Indice"), kit, UIRole.Panel);
                StyleButtons(pause.transform, kit);
            }
            GameObject photo = GameObject.Find("UI_Foto");
            if (photo != null) Style(photo.transform.Find("Controles"), kit, UIRole.Prompt);
        }

        // ================================================================== Impacto de los golpes

        private static void BuildHitFeedback()
        {
            GameObject go = new GameObject("Combate_Impacto");
            CinemachineImpulseSource impulse = go.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseDuration = 0.18f;
            HitFeedback feedback = go.AddComponent<HitFeedback>();
            Wire(feedback, "impulse", impulse);
        }
    }
}
