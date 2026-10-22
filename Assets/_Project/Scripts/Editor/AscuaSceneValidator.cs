using System.Linq;
using System.Text;
using Ascua.Audio;
using Ascua.Characters;
using Ascua.Combat;
using Ascua.Core;
using Ascua.Narrative;
using Ascua.Player;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Comprueba que la escena Plaza está bien montada: NavMesh, NPCs y sus waypoints,
    /// luces por zona, Penumbra, noche y luciérnagas. Menú: Ascua > Validar escena Plaza.
    /// </summary>
    public static class AscuaSceneValidator
    {
        private const string PlazaScenePath = AscuaAssetFactory.ScenesFolder + "/Plaza.unity";

        [MenuItem("Ascua/Validar escena Plaza", priority = 40)]
        public static void ValidateFromMenu()
        {
            bool ok = Run(out string report);
            EditorUtility.DisplayDialog(ok ? "Ascua: todo correcto" : "Ascua: hay problemas", report, "Vale");
        }

        /// <summary>Versión para línea de comandos: sale con código 1 si algo falla.</summary>
        public static void ValidateBatch()
        {
            bool ok = Run(out string report);
            Debug.Log("[Ascua][Validacion]\n" + report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(out string report)
        {
            EditorSceneManager.OpenScene(PlazaScenePath);
            StringBuilder sb = new StringBuilder();
            bool ok = true;

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            ok &= Check(sb, tri.indices.Length > 0, $"NavMesh horneado ({tri.indices.Length / 3} triángulos)");

            foreach (ZoneHope zone in Object.FindObjectsByType<ZoneHope>(FindObjectsSortMode.None))
            {
                int windows = 0, lamps = 0;
                foreach (WindowLight w in Object.FindObjectsByType<WindowLight>(FindObjectsSortMode.None)) if (w.ZoneId == zone.ZoneId) windows++;
                foreach (StreetLampToggle l in Object.FindObjectsByType<StreetLampToggle>(FindObjectsSortMode.None)) if (l.ZoneId == zone.ZoneId) lamps++;
                ok &= Check(sb, windows > 0 && lamps > 0, $"Zona '{zone.DisplayName}': {windows} ventanas reactivas, {lamps} farolas");
                ok &= Check(sb, NavMesh.SamplePosition(zone.Center, out _, 3f, NavMesh.AllAreas), $"Zona '{zone.DisplayName}': su centro es caminable");
            }

            foreach (CharacterAI ai in Object.FindObjectsByType<CharacterAI>(FindObjectsSortMode.None))
            {
                SerializedObject so = new SerializedObject(ai);
                SerializedProperty waypoints = so.FindProperty("waypoints");
                SerializedProperty zone = so.FindProperty("zone");
                ok &= Check(sb, zone.objectReferenceValue != null, $"{ai.name}: tiene zona asignada");
                ok &= Check(sb, NavMesh.SamplePosition(ai.transform.position, out _, 1f, NavMesh.AllAreas), $"{ai.name}: aparece sobre el NavMesh");

                int reachable = 0;
                for (int i = 0; i < waypoints.arraySize; i++)
                {
                    Transform wp = waypoints.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (wp == null || !NavMesh.SamplePosition(wp.position, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(ai.transform.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) reachable++;
                }
                ok &= Check(sb, waypoints.arraySize > 0 && reachable == waypoints.arraySize, $"{ai.name}: {reachable}/{waypoints.arraySize} waypoints alcanzables");
            }

            ok &= Check(sb, Object.FindFirstObjectByType<Penumbra>() != null, "Penumbra presente");
            ok &= Check(sb, Object.FindFirstObjectByType<NightManager>() != null, "NightManager presente");

            FireflyPool pool = Object.FindFirstObjectByType<FireflyPool>();
            int fireflies = pool != null ? pool.GetComponentsInChildren<Firefly>(true).Length : 0;
            ok &= Check(sb, fireflies > 0, $"Pool de luciérnagas con {fireflies} luciérnagas pre-creadas");

            // --- Fase 3: misiones, diálogo, Heraldo
            NightManager night = Object.FindFirstObjectByType<NightManager>();
            NightQuestSet set = night != null ? night.QuestSet : null;
            ok &= Check(sb, set != null && set.quests.Count > 0, $"NightManager con set de misiones ({(set != null ? set.title + ", " + set.quests.Count + " misiones" : "ninguno")})");
            if (set != null)
            {
                foreach (Quest quest in set.quests)
                {
                    bool hasOffer = quest != null && quest.offerDialogue != null && quest.offerDialogue.HasChoices;
                    bool giverExists = quest != null && Object.FindObjectsByType<NpcInteractable>(FindObjectsSortMode.None).Any(n => n.GetComponent<NpcIdentity>().NpcId == quest.giverNpcId);
                    ok &= Check(sb, hasOffer && giverExists, $"Misión '{(quest != null ? quest.title : "null")}' ({quest?.type}): diálogo de oferta con elecciones y NPC '{quest?.giverNpcId}' en escena");
                }
            }
            ok &= Check(sb, Object.FindFirstObjectByType<QuestManager>() != null, "QuestManager presente");
            ok &= Check(sb, Object.FindFirstObjectByType<DialogueRunner>() != null, "DialogueRunner (caja de diálogo) presente");
            ok &= Check(sb, Object.FindFirstObjectByType<Heraldo_AI>() != null, "Heraldo presente");

            QuestPoint malecon = Object.FindObjectsByType<QuestPoint>(FindObjectsSortMode.None).FirstOrDefault(p => p.PointId == "malecon_mina");
            CharacterAI mina = Object.FindObjectsByType<CharacterAI>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "NPC_Mina");
            bool escortReachable = false;
            if (malecon != null && mina != null && NavMesh.SamplePosition(malecon.transform.position, out NavMeshHit end, 1f, NavMesh.AllAreas))
            {
                NavMeshPath escortPath = new NavMeshPath();
                escortReachable = NavMesh.CalculatePath(mina.transform.position, end.position, NavMesh.AllAreas, escortPath) && escortPath.status == NavMeshPathStatus.PathComplete;
            }
            ok &= Check(sb, escortReachable, "Destino de escolta 'malecon_mina' alcanzable por Mina");

            Shader body = Shader.Find("Ascua/RecuerdoTranslucido");
            Shader cape = Shader.Find("Ascua/CapaViento");
            ok &= Check(sb, body != null && body.isSupported && cape != null && cape.isSupported, "Shaders de Ilse (cuerpo translúcido y capa con viento) compilados y soportados");

            ok &= ValidateImprovements(sb, set);
            ok &= ValidateCombat(sb, set);
            ok &= ValidateGraphics(sb);
            ok &= ValidateDesignAndImpact(sb);
            ok &= ValidateLaterNights(sb, night);
            ok &= ValidateStory(sb, night);

            report = sb.ToString();
            return ok;
        }

        // ================================================================== Ronda de mejoras

        private static bool ValidateImprovements(StringBuilder sb, NightQuestSet set)
        {
            bool ok = true;

            // Jugador: movilidad y recogida con todas sus referencias.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            ok &= Check(sb, player != null && player.GetComponent<PlayerStamina>() != null, "Ilse tiene PlayerStamina (sprint con stamina)");
            ok &= RefsOk(sb, player != null ? player.GetComponent<PlayerJump>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<PlayerMantle>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<PlayerDash>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<PickupSystem>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<PlayerController>() : null, "cameraTransform");

            // UI y sistemas nuevos: ninguna referencia vacía.
            ok &= RefsOk(sb, Object.FindFirstObjectByType<PauseMenu>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<CodexUI>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<PhotoMode>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<NightSummaryPanel>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<ObjectiveTrackerUI>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<ObjectiveArrow>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<TutorialHints>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<StaminaUI>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<InventoryUI>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<SubtitleUI>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<NightEvent>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<PenumbraSurge>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<BondPerks>(), "candleSpawner", "fireflyPool");
            ok &= RefsOk(sb, Object.FindFirstObjectByType<AudioManager>(), "mixer", "musicGroup", "ambientGroup", "sfxGroup");
            foreach (ZoneHope zone in Object.FindObjectsByType<ZoneHope>(FindObjectsSortMode.None))
            {
                ZoneAmbientLight fill = zone.GetComponentInChildren<ZoneAmbientLight>();
                ok &= Check(sb, fill != null && zone.GetComponent<HopeFeedback>() != null, $"Zona '{zone.DisplayName}': luz de relleno fría + chispas de Esperanza");
            }

            AudioManager audio = Object.FindFirstObjectByType<AudioManager>();
            bool hasMixer = audio != null && new SerializedObject(audio).FindProperty("mixer").objectReferenceValue != null;
            sb.AppendLine(hasMixer ? "OK    Audio Mixer AM_Ascua conectado (Musica/Ambiente/Efectos)" : "AVISO Sin Audio Mixer: el volumen se aplica en cada AudioSource");

            // Objetos recogibles.
            PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int pages = pickups.Count(p => KindOf(p) == PickupKind.PaginaDiario);
            int golden = pickups.Count(p => KindOf(p) == PickupKind.LuciernagaDorada);
            int candles = pickups.Count(p => KindOf(p) == PickupKind.Vela);
            ok &= Check(sb, pages >= 3 && golden >= 2 && candles == 3, $"Recogibles: {pages} páginas del Diario, {golden} luciérnagas doradas, {candles} velas de Teo");

            CodexUI codex = Object.FindFirstObjectByType<CodexUI>();
            int codexPages = codex != null ? new SerializedObject(codex).FindProperty("pages").arraySize : 0;
            ok &= Check(sb, codexPages >= 3, $"Diario de Ilse con {codexPages} páginas escritas (el resto se muestra como ???)");

            // Misión de entrega: su objeto existe en la escena.
            Quest delivery = set != null ? set.quests.FirstOrDefault(q => q != null && q.type == QuestType.Entrega) : null;
            bool itemInScene = delivery != null && pickups.Any(p => new SerializedObject(p).FindProperty("itemId").stringValue == delivery.deliverItemId);
            ok &= Check(sb, itemInScene, $"Misión de entrega '{delivery?.title}': '{delivery?.deliverItemName}' está en la escena y va a '{delivery?.deliverToNpcId}'");

            // Atajos: al menos 2, con su trigger y alturas que se pueden trepar.
            ExplorationShortcut[] shortcuts = Object.FindObjectsByType<ExplorationShortcut>(FindObjectsSortMode.None);
            bool triggers = shortcuts.All(s => s.GetComponent<BoxCollider>() != null && s.GetComponent<BoxCollider>().isTrigger);
            ok &= Check(sb, shortcuts.Length >= 2 && triggers, $"{shortcuts.Length} atajos de exploración con trigger");

            Physics.SyncTransforms();
            float alleyWall = TopHeight(new Vector3(0f, 5f, 15.1f));
            ok &= Check(sb, alleyWall > 0.35f && alleyWall < 1.7f, $"Callejón: cajas de {alleyWall:0.00} m (trepables: 0.35-1.7 m)");
            float crate = TopHeight(new Vector3(-27.85f, 5f, -11f));
            float roof = TopHeight(new Vector3(-30f, 5f, -11f));
            ok &= Check(sb, crate > 0.35f && crate < 1.1f && roof - crate < 1.7f && roof - crate > 0.35f,
                $"Quiosco: caja {crate:0.00} m (salto/trepar) y tejado a {roof:0.00} m (+{roof - crate:0.00} m desde la caja)");

            return ok;
        }

        // ================================================================== Combate de luz

        private static bool ValidateCombat(StringBuilder sb, NightQuestSet set)
        {
            bool ok = true;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            ok &= RefsOk(sb, player != null ? player.GetComponent<LightAttack>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<PlayerCombatState>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<LanternStaff>() : null);
            ok &= RefsOk(sb, player != null ? player.GetComponent<BondCombatAbilities>() : null);
            ok &= RefsOk(sb, Object.FindFirstObjectByType<CombatFx>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<CombatWaveDirector>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<ComboHUD>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<DamageVignette>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<CombatAbilityHUD>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<HeraldoConfrontation>());

            // Pools: los 5 tipos de criatura (+ la variante velada) existen y están desactivados.
            T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int moths = All<PenumbraMoth>().Length;
            ShadowBrute[] brutes = All<ShadowBrute>();
            int veiled = brutes.Count(b => b.GetComponent<LitVulnerable>() != null);
            int whispers = All<WhisperWraith>().Length;
            EchoShade[] echoes = All<EchoShade>();
            int copies = echoes.Count(e => e.IsSplitCopy);
            PenumbraSentinel[] sentinels = All<PenumbraSentinel>();
            int bolts = All<ShadowBolt>().Length;
            ok &= Check(sb, moths >= 6 && brutes.Length - veiled >= 2 && veiled >= 1 && whispers >= 2 && echoes.Length - copies >= 2 && copies >= 4 && sentinels.Length >= 1 && bolts >= 6,
                $"Criaturas: {moths} Polillas, {brutes.Length - veiled} Sombras Mayores, {veiled} velada, {whispers} Susurros, {echoes.Length - copies} Ecos (+{copies} copias), {sentinels.Length} Centinela ({bolts} proyectiles)");
            foreach (ShadowCreature creature in All<ShadowCreature>()) ok &= RefsOk(sb, creature, "recuerdoDrop");

            // El quiosco sirve de cobertura frente al Centinela (línea de visión bloqueada detrás de él).
            if (sentinels.Length > 0)
            {
                Transform eye = sentinels[0].transform.Find("Ojo");
                Physics.SyncTransforms();
                Vector3 behindKiosk = new Vector3(-27.3f, 1f, -11.2f);
                bool blocked = eye != null && Physics.Linecast(eye.position, behindKiosk, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
                Vector3 open = new Vector3(-30f, 1f, -14.6f);
                bool visible = eye != null && !Physics.Linecast(eye.position, open, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore);
                ok &= Check(sb, blocked && visible, "Centinela: el quiosco tapa su línea de visión (cobertura real) y la calle abierta queda a tiro");
                ok &= Check(sb, NavMesh.SamplePosition(sentinels[0].transform.position + sentinels[0].transform.forward * 2f, out _, 1.5f, NavMesh.AllAreas), "Centinela: se puede llegar hasta él caminando");
            }

            // Recuerdos que sueltan las sombras.
            PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int recuerdos = pickups.Count(p => KindOf(p) == PickupKind.Recuerdo);
            ok &= Check(sb, recuerdos >= 2, $"Recuerdos con movimiento de combate: {recuerdos} (Lupa de Mina, Reloj de Nadir)");

            // Oleadas de la noche.
            int waves = set != null && set.waves != null ? set.waves.Count : 0;
            bool zonesOk = waves > 0 && set.waves.All(w => Object.FindObjectsByType<ZoneHope>(FindObjectsSortMode.None).Any(z => z.ZoneId == w.zoneId) && w.Total >= 3 && w.Total <= 4);
            string detail = waves > 0 ? string.Join(", ", set.waves.Select(w => w.zoneId + ":" + w.Total)) : "-";
            ok &= Check(sb, waves >= 1 && waves <= 2 && zonesOk, $"Oleadas de la noche: {waves} (3-4 criaturas, zonas válidas) [{detail}]");

            // Enfrentamiento del Heraldo: 3 revelaciones.
            HeraldoConfrontation confrontation = Object.FindFirstObjectByType<HeraldoConfrontation>();
            int revelations = confrontation != null ? new SerializedObject(confrontation).FindProperty("revelations").arraySize : 0;
            ok &= Check(sb, revelations == 3, $"Enfrentamiento del Heraldo con {revelations} revelaciones narrativas");
            return ok;
        }

        // ================================================================== Mejora gráfica

        private static bool ValidateGraphics(StringBuilder sb)
        {
            bool ok = true;
            foreach (string name in new[] { "Ascua/PersonajeTela", "Ascua/BarridoLuz", "Ascua/SombraCriatura" })
            {
                Shader shader = Shader.Find(name);
                ok &= Check(sb, shader != null && shader.isSupported, $"Shader {name} compilado y soportado");
            }

            // Materiales base con relieve real y suavidad variable.
            foreach (string name in new[] { "M_Edificio_Ladrillo", "M_Asfalto", "M_PlazaPavimento", "M_Madera", "M_Metal", "M_Tejado" })
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AscuaAssetFactory.MaterialsFolder + "/" + name + ".mat");
                bool pbr = mat != null && mat.GetTexture("_BumpMap") != null && mat.GetTexture("_MetallicGlossMap") != null && mat.IsKeywordEnabled("_NORMALMAP") && mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP");
                ok &= Check(sb, pbr, $"Material {name}: normal map + máscara (suavidad/metálico/oclusión)");
            }

            int tinted = Object.FindObjectsByType<SurfaceTint>(FindObjectsSortMode.None).Length;
            ok &= Check(sb, tinted >= 20, $"Tintes por instancia (MaterialPropertyBlock): {tinted} objetos comparten material base");

            // Vecinos con ojos de luz (blend shapes) y tela.
            foreach (NpcFace face in Object.FindObjectsByType<NpcFace>(FindObjectsSortMode.None))
            {
                SkinnedMeshRenderer[] eyes = face.GetComponentsInChildren<SkinnedMeshRenderer>();
                bool shapes = eyes.Length == 2 && eyes.All(e => e.sharedMesh != null && e.sharedMesh.blendShapeCount == 3);
                ok &= Check(sb, shapes, $"{face.name}: 2 ojos de luz con blend shapes (Alegre / Cansado / Parpadeo)");
            }
            Material mina = AssetDatabase.LoadAssetAtPath<Material>(AscuaAssetFactory.MaterialsFolder + "/M_Mina_Base.mat");
            ok &= Check(sb, mina != null && mina.shader.name == "Ascua/PersonajeTela", "Mina y Teo con tela, borde de luz y sombras (Ascua/PersonajeTela)");
            ok &= Check(sb, GameObject.Find("Carrito_Teo") != null, "Carrito de Teo (silueta distintiva)");

            // Sombras y SSAO.
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            ok &= Check(sb, urp != null && Mathf.Approximately(urp.shadowDistance, 45f) && urp.shadowCascadeCount == 3 && urp.supportsAdditionalLightShadows,
                $"Sombras: distancia {urp?.shadowDistance}, {urp?.shadowCascadeCount} cascadas, luces adicionales con sombra");
            Light farol = GameObject.Find("Luz_Farol") != null ? GameObject.Find("Luz_Farol").GetComponent<Light>() : null;
            ok &= Check(sb, farol != null && farol.shadows == LightShadows.Soft, "El Farol proyecta sombras suaves en tiempo real");
            ok &= Check(sb, Object.FindFirstObjectByType<ReflectionProbe>() != null && Object.FindFirstObjectByType<PostFxDirector>() != null,
                "Sonda de reflejos (charcos) y post-procesado dinámico (aberración solo en urgencia)");

            // Misiones: iconos por tipo y filas.
            QuestLogUI log = Object.FindFirstObjectByType<QuestLogUI>();
            SerializedObject logSo = log != null ? new SerializedObject(log) : null;
            int icons = logSo != null ? logSo.FindProperty("typeIcons").arraySize : 0;
            int rows = logSo != null ? logSo.FindProperty("rows").arraySize : 0;
            ok &= Check(sb, icons == System.Enum.GetValues(typeof(QuestType)).Length && rows >= 6, $"Panel de misiones: {icons} iconos por tipo, {rows} filas con barra de progreso");
            ok &= RefsOk(sb, log);
            return ok;
        }

        // ================================================================== Partes A + B

        private static bool ValidateDesignAndImpact(StringBuilder sb)
        {
            bool ok = true;
            UIStyleKit kit = AssetDatabase.LoadAssetAtPath<UIStyleKit>(AscuaAssetFactory.SettingsFolder + "/UIStyleKit.asset");
            ok &= Check(sb, kit != null && kit.frameSprite != null && kit.panelSprite != null, "Kit de estilo de UI (marco angular ámbar + vidrio esmerilado)");

            UIStyledPanel[] styled = Object.FindObjectsByType<UIStyledPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool allKit = styled.All(p => p.Kit == kit && p.transform.Find("Marco") != null);
            ok &= Check(sb, styled.Length >= 20 && allKit, $"Sistema de diseño aplicado a {styled.Length} elementos (todos con el mismo kit y marco)");
            string[] mustHave = { "Energia", "Esperanza", "Objetivo_Actual", "Misiones", "Caja", "Aviso" };
            foreach (string name in mustHave)
            {
                bool has = styled.Any(p => p.name == name);
                ok &= Check(sb, has, $"Mismo marco en '{name}'");
            }
            GameObject box = GameObject.Find("Caja");
            ok &= Check(sb, box != null && box.transform.Find("Filo") == null, "Diálogo: marco dorado completo (sin la línea suelta anterior)");

            ok &= RefsOk(sb, Object.FindFirstObjectByType<HudFocus>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<ThreatIndicator>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<HitFeedback>());
            ok &= RefsOk(sb, Object.FindFirstObjectByType<DamageVignette>());

            Shader creature = Shader.Find("Ascua/SombraCriatura");
            ok &= Check(sb, creature != null && creature.FindPropertyIndex("_FarRimBoost") >= 0, "Criaturas: borde reforzado a distancia (legibilidad)");
            return ok;
        }

        // ================================================================== Parte C

        private static bool ValidateLaterNights(StringBuilder sb, NightManager night)
        {
            bool ok = true;
            SerializedProperty later = night != null ? new SerializedObject(night).FindProperty("laterNights") : null;
            int count = later != null ? later.arraySize : 0;
            ok &= Check(sb, count == 3, $"Sets de las noches 2-4 enlazados en NightManager ({count})");

            var npcs = Object.FindObjectsByType<NpcInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < count; i++)
            {
                NightQuestSet set = later.GetArrayElementAtIndex(i).objectReferenceValue as NightQuestSet;
                if (set == null) { ok &= Check(sb, false, "Set de noche vacío"); continue; }
                ok &= Check(sb, set.nightNumber == i + 2 && set.waves.Count == 2 && set.waves.All(w => w.Total >= 3 && w.Total <= 4),
                    $"{set.title}: noche {set.nightNumber}, {set.waves.Count} oleadas de 3-4 criaturas");
                foreach (Quest quest in set.quests)
                {
                    bool giver = npcs.Any(n => n.GetComponent<NpcIdentity>().NpcId == quest.giverNpcId);
                    bool offer = quest.offerDialogue != null && quest.offerDialogue.HasChoices;
                    bool doubleOk = quest.type != QuestType.DobleObjetivo
                                    || (ZoneById(quest.targetZoneId) != null && ZoneById(quest.defeatZoneId) != null && quest.defeatMoths + quest.defeatEchoes > 0 && quest.timeLimit > 0f);
                    bool escortOk = quest.type != QuestType.Escolta || PointById(quest.escortDestinationId) != null;
                    ok &= Check(sb, giver && offer && doubleOk && escortOk, $"  · {quest.title} ({quest.type}{(quest.energyBudget > 0f ? ", energía ≤ " + quest.energyBudget : "")}{(quest.conditions.Count > 0 ? ", " + quest.conditions.Count + " consecuencias" : "")})");
                }
            }

            // Perpetua puede llegar caminando a la Casa de ancianos.
            CharacterAI perpetua = Object.FindObjectsByType<CharacterAI>(FindObjectsSortMode.None).FirstOrDefault(c => c.GetComponent<NpcIdentity>().NpcId == "perpetua");
            QuestPoint casa = PointById("casa_ancianos");
            bool reachable = false;
            if (perpetua != null && casa != null && NavMesh.SamplePosition(casa.transform.position, out NavMeshHit end, 1.5f, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                reachable = NavMesh.CalculatePath(perpetua.transform.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            }
            ok &= Check(sb, reachable, "Perpetua llega caminando a la Casa de ancianos (NavMesh)");

            DialogueRunner runner = Object.FindFirstObjectByType<DialogueRunner>();
            ok &= Check(sb, runner != null && new SerializedObject(runner).FindProperty("offerSummaryText").objectReferenceValue != null, "Resumen multilínea al ofrecer misiones");
            ObjectiveTrackerUI tracker = Object.FindFirstObjectByType<ObjectiveTrackerUI>();
            ok &= Check(sb, tracker != null && new SerializedObject(tracker).FindProperty("secondaryFill").objectReferenceValue != null, "Progreso doble en el objetivo actual (barras A y B)");
            return ok;
        }

        // ================================================================== Parte D

        private static bool ValidateStory(StringBuilder sb, NightManager night)
        {
            bool ok = true;

            // --- Combate: fijar objetivo, turnos de ataque, aviso y grietas.
            TargetLock targetLock = Object.FindFirstObjectByType<TargetLock>();
            ok &= RefsOk(sb, targetLock, "input", "viewCamera");
            Shader creature = Shader.Find("Ascua/SombraCriatura");
            bool shaderOk = creature != null && creature.isSupported && creature.FindPropertyIndex("_Warn") >= 0 && creature.FindPropertyIndex("_Damage") >= 0;
            ok &= Check(sb, shaderOk, "Sombras: aviso de ataque (_Warn) y grietas de daño (_Damage) en el shader");
            CombatWaveDirector director = Object.FindFirstObjectByType<CombatWaveDirector>();
            int maxAttackers = director != null ? new SerializedObject(director).FindProperty("maxSimultaneousAttackers").intValue : 0;
            ok &= Check(sb, maxAttackers == 2, $"Turnos de ataque: como mucho {maxAttackers} sombras atacan a la vez");
            AudioManager audio = Object.FindFirstObjectByType<AudioManager>();
            SerializedObject audioSo = audio != null ? new SerializedObject(audio) : null;
            ok &= Check(sb, audioSo != null && audioSo.FindProperty("sfxLockOn").objectReferenceValue != null && audioSo.FindProperty("sfxWhisper").objectReferenceValue != null,
                "Sonidos de fijar objetivo y del susurro del presagio");

            // --- D.2 Presagio de la Noche 2.
            HeraldoOmen omen = Object.FindFirstObjectByType<HeraldoOmen>();
            ok &= RefsOk(sb, omen);

            // --- D.1 Contexto emocional en TODAS las misiones; progreso nervioso/aliviado en las que tienen avance.
            System.Collections.Generic.List<Quest> all = new System.Collections.Generic.List<Quest>();
            if (night != null && night.QuestSet != null) all.AddRange(night.QuestSet.quests);
            SerializedProperty later = night != null ? new SerializedObject(night).FindProperty("laterNights") : null;
            for (int i = 0; later != null && i < later.arraySize; i++)
            {
                if (later.GetArrayElementAtIndex(i).objectReferenceValue is NightQuestSet set) all.AddRange(set.quests);
            }
            all = all.Where(q => q != null).Distinct().ToList();
            int withContext = all.Count(q => !string.IsNullOrWhiteSpace(q.emotionalContext));
            ok &= Check(sb, withContext == all.Count, $"Contexto emocional al ofrecer: {withContext}/{all.Count} misiones");
            var progressable = all.Where(q => !q.CompletesOnChoice).ToList();
            int moods = progressable.Count(q => q.progressNervousDialogue != null);
            ok &= Check(sb, moods == progressable.Count, $"Diálogo de progreso nervioso: {moods}/{progressable.Count} misiones con avance");
            int relief = progressable.Count(q => q.progressReliefDialogue != null);
            ok &= Check(sb, relief >= progressable.Count - 3, $"Diálogo de progreso aliviado: {relief}/{progressable.Count} (las entregas no tienen avance intermedio)");

            // --- D.3 Secundarias.
            Quest farolillo = all.FirstOrDefault(q => q.questId == "mina_farolillo");
            Quest teo = all.FirstOrDefault(q => q.questId == "teo_todavia_sirvo");
            ok &= Check(sb, farolillo != null && farolillo.rewardDiaryPage != null && farolillo.completedDialogue != null, "Secundaria de Mina: «El farolillo de papel» (página 5 al completarla)");
            ok &= Check(sb, teo != null && teo.rewardDiaryPage != null && teo.completedDialogue != null, "Secundaria de Teo: «Todavía sirvo» (página 6 al completarla)");
            PickupItem lantern = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.ItemId == "farolillo_papel");
            ok &= Check(sb, lantern != null, "El farolillo de papel está en el callejón de los faroleros");
            QuestLitObject farol = Object.FindFirstObjectByType<QuestLitObject>();
            ok &= RefsOk(sb, farol);

            // --- D.4 Diario: al menos 6 páginas escritas.
            CodexUI codex = Object.FindFirstObjectByType<CodexUI>(FindObjectsInactive.Include);
            int pages = codex != null ? new SerializedObject(codex).FindProperty("pages").arraySize : 0;
            ok &= Check(sb, pages >= 6, $"Diario de Ilse: {pages} páginas (4-6 nuevas: presagio, farolillo, Teo)");
            return ok;
        }

        // En el editor (sin Play) los registros estáticos están vacíos: se busca en la escena.
        private static ZoneHope ZoneById(string id) => Object.FindObjectsByType<ZoneHope>(FindObjectsSortMode.None).FirstOrDefault(z => z.ZoneId == id);
        private static QuestPoint PointById(string id) => Object.FindObjectsByType<QuestPoint>(FindObjectsSortMode.None).FirstOrDefault(p => p.PointId == id);

        private static PickupKind KindOf(PickupItem item)
        {
            return (PickupKind)new SerializedObject(item).FindProperty("kind").enumValueIndex;
        }

        /// <summary>Altura del primer suelo bajo un punto (solo geometría Default).</summary>
        private static float TopHeight(Vector3 from)
        {
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 10f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) ? hit.point.y : -1f;
        }

        /// <summary>Comprueba que un componente existe y no tiene referencias de objeto vacías (salvo las opcionales).</summary>
        private static bool RefsOk(StringBuilder sb, Component component, params string[] optional)
        {
            if (component == null) return Check(sb, false, "Falta un componente de la ronda de mejoras");
            SerializedProperty it = new SerializedObject(component).GetIterator();
            System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || optional.Contains(it.name)) continue;
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null) missing.Add(it.name);
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic)
                {
                    for (int i = 0; i < it.arraySize; i++)
                    {
                        SerializedProperty e = it.GetArrayElementAtIndex(i);
                        if (e.propertyType == SerializedPropertyType.ObjectReference && e.objectReferenceValue == null)
                        {
                            missing.Add(it.name + "[" + i + "]");
                            break;
                        }
                    }
                }
            }
            string detail = missing.Count == 0 ? "completas" : "vacías: " + string.Join(", ", missing);
            return Check(sb, missing.Count == 0, $"{component.GetType().Name} en '{component.name}': referencias {detail}");
        }

        private static bool Check(StringBuilder sb, bool condition, string message)
        {
            sb.AppendLine((condition ? "OK    " : "FALLO ") + message);
            return condition;
        }
    }
}
