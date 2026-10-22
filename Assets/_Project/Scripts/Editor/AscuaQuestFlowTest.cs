using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ascua.Characters;
using Ascua.Combat;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Player;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Prueba automática del flujo de misiones de la Fase 3, de principio a fin:
    /// habla con Mina y Teo, acepta y completa una misión de cada tipo (Recolección, Escolta,
    /// Rescate de Esperanza, Diálogo, Entrega), acepta el Trato del Heraldo, lanza un Pulso y
    /// fotografía los estados del rostro de Ilse. Ronda de mejoras: recoger con E, momento de
    /// la Noche 1, salto, trepar, esquiva, atajos, ráfaga de la Penumbra, pausa, Diario,
    /// mejoras del resumen y modo foto. Guarda capturas e informe en la carpeta "Capturas".
    /// Menú: Ascua > Prueba automática de misiones.
    /// </summary>
    public static class AscuaQuestFlowTest
    {
        private const string PlazaScenePath = AscuaAssetFactory.ScenesFolder + "/Plaza.unity";
        private const int Width = 1600;
        private const int Height = 900;

        private static readonly List<Func<bool>> steps = new List<Func<bool>>();
        private static readonly StringBuilder report = new StringBuilder();
        private static int stepIndex;
        private static double stepStart;
        private static double realStart;
        private static string outputDir;
        private static bool batch;
        private static bool failed;
        private static bool originalOptionsEnabled;
        private static EnterPlayModeOptions originalOptions;
        private static readonly List<float> frameTimes = new List<float>();

        [MenuItem("Ascua/Prueba automática de misiones", priority = 42)]
        public static void RunFromMenu() => Begin(false);

        public static void RunBatch() => Begin(true);

        private static void Begin(bool isBatch)
        {
            batch = isBatch;
            failed = false;
            outputDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Capturas");
            Directory.CreateDirectory(outputDir);
            report.Clear();
            frameTimes.Clear();
            BuildSteps();

            EditorSceneManager.OpenScene(PlazaScenePath);
            originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            originalOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            stepIndex = 0;
            realStart = EditorApplication.timeSinceStartup;
            stepStart = realStart;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - realStart > 900.0)
            {
                Fail("se superó el tiempo máximo de la prueba (paso " + stepIndex + ")");
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying) return;

            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            if (NightManager.Instance == null || QuestManager.Instance == null) return;

            frameTimes.Add(Time.unscaledDeltaTime * 1000f);

            if (stepIndex >= steps.Count)
            {
                Finish();
                return;
            }

            bool done;
            try
            {
                done = steps[stepIndex]();
            }
            catch (Exception e)
            {
                Fail("excepción en el paso " + stepIndex + ": " + e.Message);
                Finish();
                return;
            }

            if (done)
            {
                stepIndex++;
                stepStart = EditorApplication.timeSinceStartup;
            }
        }

        // ================================================================== Guion de la prueba

        private static void BuildSteps()
        {
            steps.Clear();

            // --- Preparación
            steps.Add(() =>
            {
                Time.timeScale = 3f;
                // El Heraldo queda quieto hasta su parte de la prueba (si no, interrumpe otras misiones al azar).
                if (Heraldo_AI.Instance != null) Heraldo_AI.Instance.enabled = false;
                // Las oleadas y el Centinela se prueban en su propia sección (si no, interrumpen las misiones).
                if (CombatWaveDirector.Instance != null) CombatWaveDirector.Instance.Configure(new List<CombatWave>(), 0);
                foreach (PenumbraSentinel s in UnityEngine.Object.FindObjectsByType<PenumbraSentinel>(FindObjectsSortMode.None)) s.gameObject.SetActive(false);
                EventBus.PerfectDodge += _ => perfectDodges++;
                EventBus.WaveCleared += (zone, rating) => { wavesCleared++; lastRating = rating; };
                EventBus.AbilityUsed += id => { if (id == "embestida_aviso") bruteWarnings++; };
                Log("Set de la noche: " + QuestManager.Instance.CurrentSet?.title);
                return true;
            });
            steps.Add(() => Elapsed() > 1.5);
            steps.Add(() => { Capture("f3_01_inicio"); return true; });
            steps.Add(() =>
            {
                Log("Tutorial pendiente: " + GameProgress.TutorialPending + " · objetivo: " + (QuestManager.Instance.TrackedQuest?.Data.title ?? "ninguno"));
                Capture("f4_01_hud_tutorial");
                PickupItem page = FindPickup("pagina_01");
                Teleport(new Vector3(page.transform.position.x, 0.12f, page.transform.position.z - 1.3f), 0f);
                page.Interact(Player());
                return true;
            });
            steps.Add(() => Elapsed() > 0.12);
            steps.Add(() => { Capture("f4_02_recoger"); return true; });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() => Check(GameProgress.HasPage("pagina_01"), "Página 1 recogida con E: vuela al Farol y se guarda en el Diario"));

            // --- Rostro: normal, alegre y cansado (cámara cerca de la cara)
            steps.Add(() => { FaceCamera(true); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Capture("f3_02_rostro_normal"); Face().TriggerJoy(4f); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => { Capture("f3_03_rostro_alegre"); return true; });
            steps.Add(() =>
            {
                // Se mantiene la energía en 10% mientras pasa la alegría (si no, regenera sola).
                if (Energy().Current > 10f) Energy().Consume(Energy().Current - 10f);
                return Elapsed() > 4.5;
            });
            steps.Add(() =>
            {
                Capture("f3_04_rostro_cansado");
                Log("Rostro con 10% de energía: " + Face().Mood);
                Energy().Restore(100f);
                FaceCamera(false);
                return true;
            });

            // --- Mina · Recolección (Brasa)
            steps.Add(() => { Talk("mina"); return true; });
            steps.Add(() => Elapsed() > 1.5);
            steps.Add(() =>
            {
                HudFocus focus = UnityEngine.Object.FindFirstObjectByType<HudFocus>();
                Check(focus != null && focus.Mode == "Dialogo" && focus.SecondaryAlpha < 0.5f,
                    $"Diálogo abierto: el resto del HUD se atenúa (capa secundaria {(focus != null ? focus.SecondaryAlpha : 1f):0.00})");
                Capture("f3_05_dialogo_mina");
                Runner().SkipToEnd();
                return true;
            });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Capture("f3_06_elecciones"); Runner().SelectChoice(0); return true; });
            steps.Add(() => Expect("mina_brasa", QuestState.Activa));
            steps.Add(() => { CollectFireflyNear(new Vector3(4f, 0.1f, 2f)); return true; });
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() => { CollectFireflyNear(new Vector3(-5f, 0.1f, 4f)); return true; });
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() => { CollectFireflyNear(new Vector3(6f, 0.1f, -6f)); return true; });
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() => Expect("mina_brasa", QuestState.PorEntregar));
            steps.Add(() => { QuestLog().SetOpen(true); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => { Capture("f3_07_registro_misiones"); QuestLog().SetOpen(false); return true; });
            steps.Add(() => TurnIn("mina", "mina_brasa"));
            steps.Add(() => Elapsed() > 0.25);
            steps.Add(() => { Capture("f3_08_celebracion"); return true; });
            steps.Add(() => MomentPlaying() || TimedOut(5));
            steps.Add(() => Elapsed() > 1.4);
            steps.Add(() => { Capture("f4_03_momento_brasa"); return Check(MomentPlaying(), "Momento de la Noche 1 (Brasa vuelve con Mina) en curso"); });
            steps.Add(() => !MomentPlaying() || TimedOut(15));
            steps.Add(() => Check(GameProgress.HasSeenMoment("noche1_brasa"), "Momento de la Noche 1 terminado y marcado como visto"));

            // --- Mina · Escolta (Malecón)
            steps.Add(() => Elapsed() > 1.0);
            steps.Add(() => { Talk("mina"); return true; });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.2);
            steps.Add(() => { Runner().SelectChoice(0); return true; });
            steps.Add(() => Expect("mina_malecon", QuestState.Activa));
            steps.Add(() => FollowNpc("mina", "mina_malecon", "f3_09_escolta"));
            steps.Add(() => TurnIn("mina", "mina_malecon"));

            // --- Teo · Rescate de Esperanza (Velas)
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() => { Talk("teo"); return true; });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() => { Capture("f3_10_dialogo_teo"); Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.2);
            steps.Add(() => { Runner().SelectChoice(0); return true; });
            steps.Add(() => Expect("teo_velas", QuestState.Activa));
            steps.Add(() =>
            {
                // Simula al jugador alumbrando el Mercado: la Esperanza se sostiene por encima de 55.
                ZoneHope mercado = ZoneHope.Find("Mercado");
                if (mercado.Hope < 70f) mercado.AddHope(70f - mercado.Hope);
                return State("teo_velas") == QuestState.PorEntregar || TimedOut(40);
            });
            steps.Add(() => Expect("teo_velas", QuestState.PorEntregar));
            steps.Add(() => TurnIn("teo", "teo_velas"));

            // --- Teo · Diálogo con elección (Últimas velas)
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() => { Talk("teo"); return true; });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Capture("f3_11_eleccion_teo"); Runner().SelectChoice(0); return true; });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Expect("teo_ultimas_velas", QuestState.Completada));
            steps.Add(() =>
            {
                Log("Vínculos: Mina " + QuestManager.Instance.GetBond("mina") + "/5 · Teo " + QuestManager.Instance.GetBond("teo") + "/5");
                CandleSpawner candles = UnityEngine.Object.FindFirstObjectByType<CandleSpawner>();
                Log("Habilidad de Teo (velas) activa: " + (candles != null && QuestManager.Instance.GetBond("teo") >= 3));
                return true;
            });

            // --- Teo · Entrega ("Lleva la postal a Mina")
            steps.Add(() => CloseDialogues(1.0));
            steps.Add(() => { Talk("teo"); return true; });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() => { Runner().SelectChoice(0); return true; });
            steps.Add(() => Expect("teo_postal", QuestState.Activa));
            steps.Add(() => CloseDialogues(0.3));
            steps.Add(() =>
            {
                PickupItem postal = FindPickup("postal_abuela");
                Teleport(new Vector3(postal.transform.position.x, 0.12f, postal.transform.position.z - 1.7f), 0f);
                postal.Interact(Player());
                return true;
            });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() =>
            {
                Capture("f4_04_inventario_postal");
                return Check(PickupSystem.Instance.Has("postal_abuela"), "La postal está en el inventario");
            });
            steps.Add(() => Expect("teo_postal", QuestState.PorEntregar));
            steps.Add(() => TurnIn("mina", "teo_postal"));
            steps.Add(() => Check(!PickupSystem.Instance.Has("postal_abuela"), "La postal se entregó a Mina (sale del inventario)"));
            steps.Add(() => CloseDialogues(1.5));
            steps.Add(() =>
            {
                LanternController lantern = UnityEngine.Object.FindFirstObjectByType<LanternController>();
                Log("Vínculo con Teo: " + QuestManager.Instance.GetBond("teo") + "/5 · Farol grande desbloqueado: " + PlayerUpgrades.LargeLanternUnlocked +
                    " · tamaños de luz disponibles: " + lantern.UnlockedCount);
                return true;
            });

            // --- Heraldo · Trato (misión inversa)
            steps.Add(() => { Heraldo_AI.Instance.enabled = true; Heraldo_AI.Instance.ForceAppear(); return true; });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() =>
            {
                // Retrato del Heraldo a media distancia, antes de que se acerque.
                Transform h = Heraldo_AI.Instance.transform;
                Teleport(h.position + h.forward * 8f, h.eulerAngles.y + 180f);
                return true;
            });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() => { Capture("f3_12a_heraldo"); return true; });
            steps.Add(() =>
            {
                Teleport(Heraldo_AI.Instance.transform.position + Heraldo_AI.Instance.transform.forward * 2.2f, Heraldo_AI.Instance.transform.eulerAngles.y + 180f);
                return true;
            });
            steps.Add(() => Runner().IsOpen || TimedOut(10));
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() =>
            {
                Capture("f3_12_trato_heraldo");
                Log("Antes del Trato: energía " + Energy().Current.ToString("0") + " · Mercado " + ZoneHope.Find("Mercado").Hope.ToString("0"));
                Energy().Consume(40f);
                Runner().SkipToEnd();
                return true;
            });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() =>
            {
                float energyBefore = Energy().Current;
                float hopeBefore = ZoneHope.Find("Mercado").Hope;
                Runner().SelectChoice(0);
                Log($"Trato aceptado: energía {energyBefore:0} -> {Energy().Current:0} · Mercado {hopeBefore:0} -> {ZoneHope.Find("Mercado").Hope:0}");
                return true;
            });
            steps.Add(() => Expect("heraldo_trato_mercado", QuestState.Completada));
            steps.Add(() => { Runner().SkipToEnd(); return true; });

            // --- Pulso de luz
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() =>
            {
                Energy().Restore(100f);
                bool fired = UnityEngine.Object.FindFirstObjectByType<LightPulse>().TryFire();
                Log("Pulso de luz lanzado: " + fired);
                return true;
            });
            steps.Add(() => Elapsed() > 0.05);
            steps.Add(() => { Capture("f3_13_pulso"); return true; });

            // --- Salto (1.1 m)
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => { Teleport(new Vector3(6f, 0.12f, -2f), 0f); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                // El salto se mide a velocidad normal (a x3 los frames son largos y se pierde el punto más alto).
                Time.timeScale = 1f;
                jumpBaseY = Player().position.y;
                jumpPeakY = jumpBaseY;
                Controller().Jump(Mathf.Sqrt(2f * 20f * 1.1f));
                return true;
            });
            steps.Add(() =>
            {
                // Ápice exacto aunque el muestreo sea grueso: y + v²/2g mientras sube (arco balístico).
                float vy = Controller().VerticalVelocity;
                float apex = Player().position.y + (vy > 0f ? vy * vy / (2f * 20f) : 0f);
                if (!Controller().IsGrounded) jumpPeakY = Mathf.Max(jumpPeakY, apex);
                if (!captured.Contains("f4_05_salto") && Player().position.y - jumpBaseY > 0.8f)
                {
                    captured.Add("f4_05_salto");
                    Capture("f4_05_salto");
                }
                return Elapsed() > 1.6;
            });
            steps.Add(() =>
            {
                Time.timeScale = 3f;
                return Check(jumpPeakY - jumpBaseY > 0.95f && jumpPeakY - jumpBaseY < 1.25f, $"Salto: altura máxima {jumpPeakY - jumpBaseY:0.00} m (objetivo 1.1 m)");
            });

            // --- Trepar las cajas del callejón (1.25 m)
            steps.Add(() => { Teleport(new Vector3(0f, 0.12f, 14.15f), 0f); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => Check(Mantle().TryMantle(), "Trepar: detecta el borde de las cajas del callejón"));
            steps.Add(() => Elapsed() > 0.06);
            steps.Add(() => { Capture("f4_06_trepar"); return true; });
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() => Check(Player().position.y > 1.0f, $"Trepar: Ilse queda encima de las cajas (y = {Player().position.y:0.00})"));

            // --- Atajo 1: Callejón de los Faroleros
            steps.Add(() => { Teleport(new Vector3(0f, 0.12f, 16.9f), 0f); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() =>
            {
                FindPickup("pagina_02").Interact(Player());
                return Check(GameProgress.IsShortcutDiscovered("callejon_faroleros"), "Atajo descubierto: El callejón de los faroleros");
            });
            steps.Add(() => Elapsed() > 0.9);
            steps.Add(() => { FindPickupOfKind(PickupKind.LuciernagaDorada, new Vector3(0f, 1.5f, 17.3f)).Interact(Player()); return true; });
            steps.Add(() => Elapsed() > 0.25);
            steps.Add(() => { Capture("f4_07_callejon"); return true; });
            steps.Add(() => Elapsed() > 1.0);
            steps.Add(() => Check(GameProgress.HasPage("pagina_02"), "Página 2 recogida en el callejón"));

            // --- Atajo 2: Tejado del quiosco
            steps.Add(() => { Teleport(new Vector3(-30.9f, 2.42f, -11.6f), 45f); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() =>
            {
                FindPickup("pagina_03").Interact(Player());
                return Check(GameProgress.IsShortcutDiscovered("tejado_quiosco"), "Atajo descubierto: El tejado del quiosco");
            });
            steps.Add(() => Elapsed() > 0.9);
            steps.Add(() => { FindPickupOfKind(PickupKind.LuciernagaDorada, new Vector3(-29.3f, 3.1f, -10.5f)).Interact(Player()); return true; });
            steps.Add(() => Elapsed() > 0.25);
            steps.Add(() => { Capture("f4_08_tejado"); return true; });
            steps.Add(() => Elapsed() > 1.0);
            steps.Add(() => Check(GameProgress.HasPage("pagina_03"), "Página 3 recogida en el tejado"));

            // --- Esquiva (3.2 m)
            steps.Add(() => { Teleport(new Vector3(-4f, 0.12f, -6f), 90f); return true; });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() => { dashStart = Player().position; return Check(Dash().TryDash(), "Esquiva lanzada"); });
            steps.Add(() => Elapsed() > 0.03);
            steps.Add(() => { Capture("f4_09_esquiva"); return true; });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() =>
            {
                float moved = Vector3.Distance(Flat(dashStart), Flat(Player().position));
                PlayerStamina stamina = UnityEngine.Object.FindFirstObjectByType<PlayerStamina>();
                return Check(moved > 2.4f && moved < 4f, $"Esquiva: recorre {moved:0.00} m (objetivo 3.2 m) · stamina {stamina.Current:0}/{stamina.Max:0}");
            });

            // --- Ráfaga de la Penumbra
            steps.Add(() => { UnityEngine.Object.FindFirstObjectByType<PenumbraSurge>().TriggerNow(); Teleport(new Vector3(0f, 0.12f, -6f), 0f); return true; });
            steps.Add(() => { KeepZonesAlive(); return UnityEngine.Object.FindFirstObjectByType<PenumbraSurge>().IsSurging || TimedOut(8); });
            steps.Add(() => { KeepZonesAlive(); return Elapsed() > 0.3; });
            steps.Add(() =>
            {
                Capture("f4_10_rafaga");
                return Check(UnityEngine.Object.FindFirstObjectByType<PenumbraSurge>().IsSurging, "Ráfaga de la Penumbra activa (10 s, anunciada)");
            });

            // --- Pausa, accesibilidad y Diario
            steps.Add(() => { KeepZonesAlive(); return CloseDialogues(0.3); });
            steps.Add(() => { PauseMenu.Instance.Pause(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                Capture("f4_11_pausa");
                Check(PauseMenu.IsPaused && Time.timeScale == 0f, "Pausa real (timeScale 0)");
                GetButton(PauseMenu.Instance, "accessibilityButton").onClick.Invoke();
                return true;
            });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                Capture("f4_12_accesibilidad");
                GetButton(PauseMenu.Instance, "accessibilityButton").onClick.Invoke();
                CodexUI.Instance.Open(true);
                return true;
            });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() =>
            {
                Capture("f4_13_diario");
                CodexUI.Instance.Close();
                PauseMenu.Instance.Resume();
                Time.timeScale = 3f;
                return Check(!PauseMenu.IsPaused, "Reanudar desde la pausa");
            });

            AddCombatSteps();

            // --- Fin de la noche y resumen
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() => { NightManager.Instance.EndNight(false); return true; });
            steps.Add(() => Elapsed() > 1.6);
            steps.Add(() =>
            {
                Capture("f3_14_resumen");
                foreach (QuestRuntime q in QuestManager.Instance.Quests) Log($"Estado final · {q.Data.title} ({q.Data.type}): {q.State}");
                return true;
            });
            steps.Add(() =>
            {
                NightSummaryPanel summary = UnityEngine.Object.FindFirstObjectByType<NightSummaryPanel>();
                SerializedProperty buttons = new SerializedObject(summary).FindProperty("upgradeButtons");
                ((Button)buttons.GetArrayElementAtIndex(0).objectReferenceValue).onClick.Invoke();
                return true;
            });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() =>
            {
                Capture("f4_14_resumen_mejora");
                return Check(PlayerUpgrades.Chosen.Count == 1, "Mejora elegida para la próxima noche: " + (PlayerUpgrades.Chosen.Count > 0 ? PlayerUpgrades.Describe(PlayerUpgrades.Chosen[0]).Item1 : "ninguna"));
            });
            steps.Add(() => { GetButton(UnityEngine.Object.FindFirstObjectByType<NightSummaryPanel>(), "photoButton").onClick.Invoke(); return true; });
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() =>
            {
                Capture("f4_15_modo_foto");
                AddLaterNightSteps();
                Log("Audio: AudioManager " + (Ascua.Audio.AudioManager.Instance != null ? "activo" : "ausente"));
                return Check(PhotoMode.Instance != null && PhotoMode.Instance.IsActive, "Modo foto desde el resumen (HUD oculto, cámara libre)");
            });
        }

        // ================================================================== Parte C: noches 2 y 4

        private static string SummaryText()
        {
            SerializedObject so = new SerializedObject(Runner());
            TMPro.TMP_Text text = so.FindProperty("offerSummaryText").objectReferenceValue as TMPro.TMP_Text;
            CanvasGroup group = so.FindProperty("offerSummaryGroup").objectReferenceValue as CanvasGroup;
            return group != null && group.alpha > 0.5f && text != null ? text.text : "";
        }

        private static string TrackerTitle()
        {
            ObjectiveTrackerUI tracker = UnityEngine.Object.FindFirstObjectByType<ObjectiveTrackerUI>();
            TMPro.TMP_Text text = new SerializedObject(tracker).FindProperty("titleText").objectReferenceValue as TMPro.TMP_Text;
            return text != null ? text.text : "";
        }

        /// <summary>Tras el resumen de la Noche 1: siguiente noche, doble objetivo, fallo explicado y consecuencia en la Noche 4.</summary>
        private static void AddLaterNightSteps()
        {
            steps.Add(() => { Log("--- Noches 2-4 (misiones difíciles pero claras) ---"); NightManager.Instance.StartNextNight(); return true; });
            steps.Add(() => (NightManager.Instance != null && NightManager.Instance.NightNumber == 2 && NightManager.Instance.IsRunning && Elapsed() > 2.5) || TimedOut(40));
            steps.Add(() =>
            {
                Time.timeScale = 1f;
                if (CombatWaveDirector.Instance != null) CombatWaveDirector.Instance.Configure(new List<CombatWave>(), 0);
                foreach (PenumbraSentinel s in UnityEngine.Object.FindObjectsByType<PenumbraSentinel>(FindObjectsSortMode.None)) s.gameObject.SetActive(false);
                if (Heraldo_AI.Instance != null) Heraldo_AI.Instance.enabled = false;
                return Check(QuestManager.Instance.CurrentSet != null && QuestManager.Instance.CurrentSet.nightNumber == 2, "Noche 2 con su propio set: " + QuestManager.Instance.CurrentSet?.title);
            });

            // --- Doble objetivo: resumen claro al ofrecer, dos barras y victoria.
            steps.Add(() => { HoldNight(0.3f); Talk("teo"); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                string summary = SummaryText();
                Capture("f7_01_resumen_doble");
                Check(summary.Contains("todo el rato") && summary.Contains("a la vez") && summary.Contains("Límite"), "Resumen al ofrecer: objetivo A, objetivo B y límite de tiempo");
                Runner().SelectChoice(0);
                return true;
            });
            steps.Add(() => Expect("teo_dos_frentes", QuestState.Activa));
            steps.Add(() => { HoldNight(0.3f); return Elapsed() > 1.5; });
            steps.Add(() => { Capture("f7_02_doble_progreso"); return true; });
            steps.Add(() =>
            {
                HoldNight(0.3f);
                if (State("teo_dos_frentes") != QuestState.Activa || TimedOut(45)) return true;
                ShadowCreature next = null;
                float best = float.MaxValue;
                foreach (ShadowCreature c in UnityEngine.Object.FindObjectsByType<ShadowCreature>(FindObjectsSortMode.None))
                {
                    if (!c.IsAlive || c.ZoneId != "mision:teo_dos_frentes") continue;
                    float d = (c.transform.position - Player().position).sqrMagnitude;
                    if (d < best) { best = d; next = c; }
                }
                if (next != null) StrikeAt(next, 1.1f);
                return false;
            });
            steps.Add(() => Expect("teo_dos_frentes", QuestState.PorEntregar));
            steps.Add(() => TurnIn("teo", "teo_dos_frentes"));

            // --- Elección con consecuencia tardía (Noche 2 -> Noche 4).
            steps.Add(() => CloseDialogues(0.6));
            steps.Add(() => { Talk("teo"); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Runner().SelectChoice(0); return true; });
            steps.Add(() => CloseDialogues(0.4));
            steps.Add(() => Check(GameProgress.HasFlag(AscuaNarrativeNights.FlagVelasMercado), "Decisión guardada: las velas de reserva se quedan en el Mercado"));

            // --- Recurso limitado: el fallo explica el porqué.
            steps.Add(() => { HoldNight(0.3f); Talk("perpetua"); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                Check(SummaryText().Contains("Energía"), "Resumen de la escolta: límite de 30 de Energía explicado");
                Runner().SelectChoice(0);
                return true;
            });
            steps.Add(() => Expect("perpetua_camino", QuestState.Activa));
            steps.Add(() => Elapsed() > 1.0);
            steps.Add(() => { Energy().ApplyDamage(35f); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() =>
            {
                Capture("f7_03_fallo_explicado");
                QuestRuntime q = QuestManager.Instance.FindById("perpetua_camino");
                bool explained = q != null && q.State == QuestState.Fallida && q.FailReason.Contains("Energía") && TrackerTitle().Contains("fallida");
                Energy().Restore(100f);
                return Check(explained, "Fallo explicado: «" + q?.FailReason + "»");
            });

            AddStoryNight2Steps();

            // --- Noche 4: la vigilia cambia por la decisión de la Noche 2.
            steps.Add(() => { GameProgress.AdvanceNight(); NightManager.Instance.StartNextNight(); return true; });
            steps.Add(() => (NightManager.Instance != null && NightManager.Instance.NightNumber == 4 && NightManager.Instance.IsRunning && Elapsed() > 2.5) || TimedOut(40));
            steps.Add(() =>
            {
                Time.timeScale = 1f;
                if (CombatWaveDirector.Instance != null) CombatWaveDirector.Instance.Configure(new List<CombatWave>(), 0);
                if (Heraldo_AI.Instance != null) Heraldo_AI.Instance.enabled = false;
                QuestRuntime vigilia = QuestManager.Instance.FindById("teo_vigilia");
                Check(QuestManager.Instance.CurrentSet != null && QuestManager.Instance.CurrentSet.nightNumber == 4, "Noche 4 con su propio set: " + QuestManager.Instance.CurrentSet?.title);
                return Check(vigilia != null && Mathf.Approximately(vigilia.EffThreshold, 50f) && vigilia.ConditionNote.Contains("Noche 2"),
                    $"Consecuencia tardía aplicada: la vigilia pide {vigilia?.EffThreshold:0} (no 60) por las velas de la Noche 2");
            });
            steps.Add(() => { HoldNight(0.3f); Talk("teo"); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                Capture("f7_04_consecuencia_explicada");
                bool ok = Check(SummaryText().Contains("Noche 2"), "Al ofrecerla, la vigilia explica por qué cambió (decisión de la Noche 2)");
                Runner().SelectChoice(1);
                return ok;
            });
            steps.Add(() => CloseDialogues(0.3));
            AddStoryNight4Steps();
        }

        // ================================================================== Parte D: trama + lectura del combate

        private static HeraldoOmen Omen() => UnityEngine.Object.FindFirstObjectByType<HeraldoOmen>();
        private static PlayerInputReader InputReader() => UnityEngine.Object.FindFirstObjectByType<PlayerInputReader>();
        private static ShadowCreature lockedCreature;
        private static bool reliefSeen;

        /// <summary>Noche 2: presagio del Heraldo, contexto emocional, ánimo nervioso, el farolillo y el combate nuevo.</summary>
        private static void AddStoryNight2Steps()
        {
            // --- D.2 Presagio: empieza solo a mitad de la noche, si no hay combate ni diálogo.
            steps.Add(() => CloseDialogues(0.3));
            steps.Add(() => { Log("--- Parte D: presagio de la Noche 2 ---"); Teleport(new Vector3(0f, 0.12f, -7f), 0f); HoldNight(0.55f); return true; });
            steps.Add(() => (Omen() != null && Omen().IsPlaying) || TimedOut(6));
            steps.Add(() => Check(Omen() != null && Omen().IsPlaying, "D.2 El presagio empieza solo a mitad de la Noche 2 (sin combate ni diálogo)"));
            steps.Add(() => Elapsed() > 3.0);
            steps.Add(() => { Capture("f8_01_presagio"); return true; });
            steps.Add(() => !Omen().IsPlaying || TimedOut(20));
            steps.Add(() => Check(!Omen().IsPlaying && Omen().HasPlayed && GameProgress.HasPage("pagina_04") && InputReader().GameplayEnabled,
                "Presagio terminado: control devuelto, no se repite y página 4 del Diario («Una luz que no era de nadie»)"));

            // --- D.1 + D.3 Mina: oferta con contexto emocional.
            steps.Add(() => { HoldNight(0.3f); Talk("mina"); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Runner().SkipToEnd(); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                Capture("f8_02_oferta_farolillo");
                QuestRuntime q = QuestManager.Instance.FindById("mina_farolillo");
                DialogueSequence offer = q != null ? QuestDialogues.Offer(q, "Mina") : null;
                bool context = offer != null && offer.lines.Count == q.Data.offerDialogue.lines.Count + 2
                               && offer.lines[offer.lines.Count - 1].speaker == "Mina" && offer.lines[offer.lines.Count - 1].text.Contains("voz");
                Check(context, "D.1 La oferta termina con 2 frases de por qué le importa a Mina (contexto emocional)");
                Runner().SelectChoice(0);
                return true;
            });
            steps.Add(() => Expect("mina_farolillo", QuestState.Activa));
            steps.Add(() => CloseDialogues(0.3));

            // --- D.1 Ánimo: con la noche a punto de acabar, el diálogo de progreso es el nervioso.
            steps.Add(() =>
            {
                HoldNight(0.92f);
                QuestRuntime q = QuestManager.Instance.FindById("mina_farolillo");
                bool nervous = QuestManager.Instance.GetMood(q) == QuestMood.Nerviosa && QuestDialogues.Progress(q, null) == q.Data.progressNervousDialogue;
                HoldNight(0.3f);
                bool normal = QuestManager.Instance.GetMood(q) == QuestMood.Normal && QuestDialogues.Progress(q, null) == q.Data.inProgressDialogue;
                return Check(nervous && normal, "D.1 Progreso según el avance real: nerviosa con poca noche, normal con tiempo de sobra");
            });

            // --- D.3 El farolillo: en el callejón de los faroleros, se entrega a Mina (página 5).
            steps.Add(() =>
            {
                PickupItem lantern = FindPickup("farolillo_papel");
                Teleport(new Vector3(0f, 0.12f, 19f), 0f);
                lantern.Interact(Player());
                return true;
            });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() => { Capture("f8_03_farolillo"); return Check(PickupSystem.Instance.Has("farolillo_papel"), "El farolillo de papel está en el inventario"); });
            steps.Add(() => Expect("mina_farolillo", QuestState.PorEntregar));
            steps.Add(() => TurnIn("mina", "mina_farolillo"));
            steps.Add(() => CloseDialogues(0.5));
            steps.Add(() => Check(State("mina_farolillo") == QuestState.Completada && GameProgress.HasPage("pagina_05"), "D.3 Farolillo entregado a Mina y página 5 del Diario («Pájaros de papel»)"));

            // --- Combate: turnos de ataque (máx. 2), fijar objetivo, grietas y salto de objetivo.
            steps.Add(() =>
            {
                Log("--- Combate: turnos, objetivo fijado y grietas ---");
                Time.timeScale = 1f;
                Teleport(new Vector3(0f, 0.12f, -7f), 0f);
                HoldNight(0.6f);
                List<ShadowCreature> group = CombatWaveDirector.Instance.SpawnGroup("Plaza", 4, 0, "prueba_turnos");
                EnemyAttackTokens.ResetPeak();
                return Check(group.Count == 4, "4 Polillas a la vez para probar los turnos");
            });
            steps.Add(() => { Energy().Restore(100f); KeepZonesAlive(); return Elapsed() > 8.0; });
            steps.Add(() => Check(EnemyAttackTokens.PeakCount >= 1 && EnemyAttackTokens.PeakCount <= 2,
                $"Turnos de ataque: como mucho {EnemyAttackTokens.PeakCount} sombras atacaron a la vez (máx. 2 de 4)"));
            steps.Add(() => { TargetLock.Instance.Toggle(); return true; });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() =>
            {
                Capture("f8_04_objetivo_fijado");
                lockedCreature = TargetLock.Instance.Current as ShadowCreature;
                Check(lockedCreature != null, "Fijar objetivo (T / clic central / R3) marca una sombra");
                if (lockedCreature != null) lockedCreature.ReceiveLight(new HitInfo(1, 0f, Player().position, HitKind.Barrido));
                return true;
            });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() =>
            {
                bool cracks = lockedCreature != null && lockedCreature.IsAlive && lockedCreature.Damage01 > 0.2f;
                Check(cracks, $"Daño visible sin barra de vida: grietas al {(lockedCreature != null ? lockedCreature.Damage01 : 0f):P0}");
                if (lockedCreature != null) lockedCreature.ReceiveLight(new HitInfo(5, 0f, Player().position, HitKind.Remate));
                return true;
            });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() => Check(TargetLock.Instance.HasTarget && !ReferenceEquals(TargetLock.Instance.Current, lockedCreature),
                "Al disolverse la sombra fijada, el objetivo salta solo a la siguiente"));
            steps.Add(() =>
            {
                TargetLock.Instance.SetTarget(null);
                CombatWaveDirector.Instance.VanishGroup("prueba_turnos");
                Energy().Restore(100f);
                return Elapsed() > 0.8;
            });
        }

        /// <summary>Noche 4: la secundaria de Teo (vigilar mientras enciende el farol de su padre).</summary>
        private static void AddStoryNight4Steps()
        {
            steps.Add(() =>
            {
                Log("--- Parte D: «Todavía sirvo» (Teo) ---");
                Time.timeScale = 1f;
                HoldNight(0.3f);
                reliefSeen = false;
                QuestRuntime q = QuestManager.Instance.FindById("teo_todavia_sirvo");
                Check(q != null && q.State == QuestState.Disponible && !string.IsNullOrEmpty(q.Data.emotionalContext), "La secundaria de Teo está disponible en la Noche 4, con su contexto emocional");
                QuestManager.Instance.Accept(q);
                return true;
            });
            steps.Add(() => Expect("teo_todavia_sirvo", QuestState.Activa));
            steps.Add(() =>
            {
                HoldNight(0.3f);
                ZoneHope mercado = ZoneHope.Find("Mercado");
                if (mercado != null && mercado.Hope < 75f) mercado.AddHope(75f - mercado.Hope);
                QuestRuntime q = QuestManager.Instance.FindById("teo_todavia_sirvo");
                if (q == null || q.State != QuestState.Activa || TimedOut(45)) return true;
                if (QuestManager.Instance.GetMood(q) == QuestMood.Aliviada) reliefSeen = true;
                ShadowCreature next = null;
                float best = float.MaxValue;
                foreach (ShadowCreature c in UnityEngine.Object.FindObjectsByType<ShadowCreature>(FindObjectsSortMode.None))
                {
                    if (!c.IsAlive || c.ZoneId != "mision:teo_todavia_sirvo") continue;
                    float d = (c.transform.position - Player().position).sqrMagnitude;
                    if (d < best) { best = d; next = c; }
                }
                if (next != null) StrikeAt(next, 1.1f);
                return false;
            });
            steps.Add(() => Expect("teo_todavia_sirvo", QuestState.PorEntregar));
            steps.Add(() => Check(reliefSeen, "D.1 Ánimo aliviado de Teo al pasar de la mitad (diálogo de progreso aliviado)"));
            steps.Add(() => TurnIn("teo", "teo_todavia_sirvo"));
            steps.Add(() => CloseDialogues(1.6));
            steps.Add(() =>
            {
                QuestLitObject farol = UnityEngine.Object.FindFirstObjectByType<QuestLitObject>();
                Transform t = farol != null ? farol.transform : null;
                if (t != null) Teleport(t.position + new Vector3(-6f, 0f, 1.5f), 100f);
                return Check(GameProgress.HasPage("pagina_06") && farol != null && farol.IsLit, "D.3 Teo enciende el farol de los Bravo (queda encendido) y página 6 del Diario");
            });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() => { Capture("f8_05_farol_bravo"); CodexUI.Instance.Open(true); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => { Capture("f8_06_diario_paginas"); CodexUI.Instance.Close(); return true; });
        }

        // ================================================================== Combate de luz

        private static void AddCombatSteps()
        {
            // --- Preparación: velocidad normal, noche congelada, Plaza despejada.
            steps.Add(() => CloseDialogues(0.5));
            steps.Add(() =>
            {
                Time.timeScale = 1f;
                HoldNight(0.5f);
                Teleport(new Vector3(0f, 0.12f, -6f), 0f);
                Log("--- Combate de luz ---");
                return true;
            });
            steps.Add(() => { HoldNight(0.5f); return Elapsed() > 0.6; });
            steps.Add(() => { Capture("f5_00_graficos_plaza"); return true; });

            // --- 1) Polilla: combo de 3 barridos.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                target = SpawnAhead<PenumbraMoth>(1.5f, 0.9f);
                swings = 0;
                return true;
            });
            steps.Add(() => { HoldNight(0.5f); return Elapsed() > 0.55; });
            steps.Add(() => { StrikeAt(target, 1.1f); return Elapsed() > 0.05; });
            steps.Add(() => { HoldNight(0.5f); return Elapsed() > 0.36; });
            steps.Add(() => { if (target.IsAlive) StrikeAt(target, 1.1f); return true; });
            steps.Add(() => { HoldNight(0.5f); return Elapsed() > 0.36; });
            steps.Add(() => { if (target.IsAlive) StrikeAt(target, 1.1f); return true; });
            steps.Add(() => Elapsed() > 0.08);
            steps.Add(() => { Capture("f5_01_combo"); return true; });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                if (target.IsAlive && Elapsed() < 4) { StrikeAt(target, 1.1f); return false; }
                return Elapsed() > 0.6;
            });
            steps.Add(() => Check(!target.IsAlive, $"Polilla de Olvido disuelta en luz ({swings} barridos, combo hasta el paso {Mathf.Max(Attack().ComboStep, 1)})"));
            steps.Add(() =>
            {
                HitFeedback fb = HitFeedback.Instance;
                ThreatIndicator threat = UnityEngine.Object.FindFirstObjectByType<ThreatIndicator>();
                Check(fb != null && fb.FreezeCount > 0 && fb.ShakeCount > 0, $"Impacto: freeze-frame ({fb?.FreezeCount}) y sacudida ({fb?.ShakeCount}) en cada golpe conectado");
                return Check(threat != null && threat.ShownCount > 0, $"Amenaza detectada: pulso en el borde de la pantalla ({threat?.ShownCount})");
            });

            // --- 2) Sombra Mayor: embestida telegrafiada y esquiva perfecta.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Teleport(new Vector3(0f, 0.12f, -7f), 0f);
                perfectDodges = 0;
                bruteWarnings = 0;
                target = SpawnAhead<ShadowBrute>(5.5f, 0f, b => b.GetComponent<LitVulnerable>() == null);
                return true;
            });
            steps.Add(() => { HoldNight(0.5f); return bruteWarnings > 0 || TimedOut(8); });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => { Capture("f5_02_embestida_aviso"); return Check(bruteWarnings > 0, "Sombra Mayor: aviso de embestida (0.8 s, franja en el suelo)"); });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                // Esquiva en el último momento: cuando la carga llega a 2.3 m.
                Vector3 d = target.transform.position - Player().position;
                d.y = 0f;
                if (d.magnitude < 2.3f)
                {
                    UnityEngine.Object.FindFirstObjectByType<PlayerDash>().TryDash();
                    return true;
                }
                return TimedOut(3);
            });
            steps.Add(() => Elapsed() > 0.05);
            steps.Add(() => { Capture("f5_03_esquiva_perfecta"); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => Check(perfectDodges > 0, $"Esquiva perfecta durante los i-frames ({perfectDodges})"));
            steps.Add(() =>
            {
                HoldNight(0.5f);
                if (target.IsAlive && !TimedOut(15)) { StrikeAt(target, 1.6f); return false; }
                return true;
            });
            steps.Add(() => Check(!target.IsAlive, "Sombra Mayor disuelta (7 de vida)"));
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() =>
            {
                PickupItem lupa = PickupItem.All.FirstOrDefault(p => p.Kind == PickupKind.Recuerdo && p.DiaryPageId == LightAttack.LupaMinaId);
                if (lupa != null) lupa.Interact(Player());
                return Check(lupa != null, "La primera Sombra Mayor suelta el Recuerdo «La lupa de Mina»");
            });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() => Check(GameProgress.HasRecuerdo(LightAttack.LupaMinaId), "Recuerdo desbloqueado: ataque cargado"));

            // --- 3) Eco: el haz cargado (golpe fuerte) lo divide en dos copias.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Teleport(new Vector3(0f, 0.12f, -6f), 0f);
                target = SpawnAhead<EchoShade>(3f, 0f, e => !e.IsSplitCopy);
                return true;
            });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => Check(Attack().TryChargedAttack(), "Haz cargado (Lupa de Mina) lanzado"));
            steps.Add(() => Elapsed() > 0.15);
            steps.Add(() =>
            {
                Capture("f5_04_eco_dividido");
                int copies = UnityEngine.Object.FindObjectsByType<EchoShade>(FindObjectsSortMode.None).Count(e => e.IsSplitCopy && e.IsAlive);
                return Check(copies == 2, $"El Eco se divide en {copies} copias al recibir un golpe fuerte");
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                EchoShade copy = UnityEngine.Object.FindObjectsByType<EchoShade>(FindObjectsSortMode.None).FirstOrDefault(e => e.IsAlive);
                if (copy != null && !TimedOut(12)) { StrikeAt(copy, 1.1f); return false; }
                return true;
            });
            steps.Add(() => Check(!UnityEngine.Object.FindObjectsByType<EchoShade>(FindObjectsSortMode.None).Any(e => e.IsAlive), "Copias del Eco disueltas (1 golpe cada una)"));

            // --- 4) Susurro: roba la última página y se recupera al disolverlo.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Teleport(new Vector3(0f, 0.12f, -6f), 0f);
                target = SpawnAhead<WhisperWraith>(2f);
                return true;
            });
            steps.Add(() => { HoldNight(0.5f); return ((WhisperWraith)target).IsCarryingPage || TimedOut(6); });
            steps.Add(() =>
            {
                Capture("f5_05_susurro_robo");
                return Check(((WhisperWraith)target).IsCarryingPage && !GameProgress.HasPage("pagina_03"), "Un Susurro roba la última página de la noche y huye");
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                if (target.IsAlive && !TimedOut(12)) { StrikeAt(target, 1.0f); return false; }
                return true;
            });
            steps.Add(() => Check(GameProgress.HasPage("pagina_03"), "Al disolver al Susurro la página vuelve al Diario"));

            // --- 6) Sombra velada: inmune hasta estar 2 s bajo el Farol.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Teleport(new Vector3(0f, 0.12f, -6f), 0f);
                target = SpawnAhead<ShadowBrute>(2.2f, 0f, b => b.GetComponent<LitVulnerable>() != null);
                return true;
            });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => { StrikeAt(target, 1.6f); return true; });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() => Check(target.Health == 7, $"Sombra velada: el barrido le resbala sin luz (vida {target.Health}/7)"));
            steps.Add(() =>
            {
                testLight.LightPosition = Player().position + Vector3.up * 1.2f;
                Lighting.IlluminationRegistry.Register(testLight);
                return true;
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                testLight.LightPosition = Player().position + Vector3.up * 1.2f;
                return target.GetComponent<LitVulnerable>().IsRevealed || TimedOut(6);
            });
            steps.Add(() => { Capture("f5_07_sombra_revelada"); return Check(target.GetComponent<LitVulnerable>().IsRevealed, "Sombra velada revelada tras 2 s bajo la luz"); });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                testLight.LightPosition = Player().position + Vector3.up * 1.2f;
                if (target.IsAlive && !TimedOut(14)) { StrikeAt(target, 1.6f); return false; }
                Lighting.IlluminationRegistry.Unregister(testLight);
                return true;
            });
            steps.Add(() => Check(!target.IsAlive, "Sombra velada disuelta una vez revelada"));

            // --- 7) Centinela: dispara si ve a Ilse; el quiosco la cubre; el barrido rompe proyectiles.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                foreach (PenumbraSentinel s in UnityEngine.Object.FindObjectsByType<PenumbraSentinel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) s.gameObject.SetActive(true);
                Teleport(new Vector3(-29.5f, 0.12f, -14.3f), -90f);
                return true;
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                ShadowBolt bolt = UnityEngine.Object.FindObjectsByType<ShadowBolt>(FindObjectsSortMode.None).FirstOrDefault();
                return (bolt != null && (bolt.transform.position - Player().position).magnitude < 4f) || TimedOut(6);
            });
            steps.Add(() => { Capture("f5_08_centinela_proyectil"); return true; });
            steps.Add(() =>
            {
                ShadowBolt bolt = UnityEngine.Object.FindObjectsByType<ShadowBolt>(FindObjectsSortMode.None).OrderBy(b => (b.transform.position - Player().position).sqrMagnitude).FirstOrDefault();
                if (bolt == null) return Check(false, "Proyectil del Centinela en vuelo");
                if ((bolt.transform.position - Player().position).magnitude > 1.6f && !TimedOut(2)) return false;
                Attack().TryStartSwing();
                return true;
            });
            steps.Add(() => Elapsed() > 0.25);
            steps.Add(() =>
            {
                // A cubierto tras el quiosco: no debe seguir disparando.
                Teleport(new Vector3(-27.2f, 0.12f, -11.2f), -90f);
                return true;
            });
            // Se espera a que caigan los proyectiles ya disparados (viven 2.5 s) y se mira si salen nuevos.
            steps.Add(() => { HoldNight(0.5f); return Elapsed() > 3f; });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                if (UnityEngine.Object.FindObjectsByType<ShadowBolt>(FindObjectsSortMode.None).Length > 0) sawBoltBehindCover = true;
                return Elapsed() > 2.5;
            });
            steps.Add(() => Check(!sawBoltBehindCover, "Tras el quiosco, el Centinela deja de disparar (cobertura)"));
            steps.Add(() =>
            {
                HoldNight(0.5f);
                target = UnityEngine.Object.FindFirstObjectByType<PenumbraSentinel>();
                if (target != null && target.IsAlive && !TimedOut(15)) { StrikeAt(target, 1.2f); return false; }
                return true;
            });
            steps.Add(() => Check(target == null || !target.IsAlive, "Centinela disuelto"));
            steps.Add(() => Elapsed() > 0.8);
            steps.Add(() =>
            {
                PickupItem reloj = PickupItem.All.FirstOrDefault(p => p.Kind == PickupKind.Recuerdo && p.DiaryPageId == PlayerCombatState.RelojNadirId);
                return Check(reloj != null, "El Centinela suelta el Recuerdo «El reloj de Nadir»");
            });

            // --- 7b) Contraataque (Reloj de Nadir): se concede para probarlo aquí.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                // Se recoge el Reloj que soltó el Centinela (si no, se concede para poder probarlo).
                PickupItem reloj = PickupItem.All.FirstOrDefault(p => p.Kind == PickupKind.Recuerdo && p.DiaryPageId == PlayerCombatState.RelojNadirId);
                if (reloj != null) reloj.Interact(Player());
                else GameProgress.UnlockRecuerdo(PlayerCombatState.RelojNadirId);
                Teleport(new Vector3(0f, 0.12f, -6f), 0f);
                return true;
            });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() =>
            {
                Check(GameProgress.HasRecuerdo(PlayerCombatState.RelojNadirId), "Recuerdo desbloqueado: contraataque");
                target = SpawnAhead<PenumbraMoth>(1.2f, 0.8f);
                return true;
            });
            steps.Add(() => Elapsed() > 0.4);
            steps.Add(() =>
            {
                PlayerCombatState state = PlayerCombatState.Instance;
                state.NotifyAttackPressed();
                IncomingHitResult r = state.ReceiveHit(6f, target.transform.position, target);
                return Check(r == IncomingHitResult.Parried && target.IsStunned, "Contraataque: atacar justo antes del golpe lo anula y aturde a la sombra");
            });
            steps.Add(() => Elapsed() > 0.05);
            steps.Add(() => { Capture("f5_06_contraataque"); return true; });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                if (target.IsAlive && !TimedOut(6)) { StrikeAt(target, 1.1f); return false; }
                return true;
            });

            // --- 8) Oleada: 3 sombras en La Plaza, la última con finisher.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Teleport(new Vector3(0f, 0.12f, -2f), 0f);
                CombatWaveDirector director = CombatWaveDirector.Instance;
                director.Configure(QuestManager.Instance.CurrentSet.waves, 0);
                wavesCleared = 0;
                return Check(director.StartWaveNow(0), "Oleada 1 lanzada: " + QuestManager.Instance.CurrentSet.waves[0].label);
            });
            steps.Add(() => { HoldNight(0.5f); return Elapsed() > 1.4; });
            steps.Add(() =>
            {
                Capture("f5_09_oleada");
                HudFocus focus = UnityEngine.Object.FindFirstObjectByType<HudFocus>();
                return Check(focus != null && focus.Mode == "Combate", "Combate: energía y combo al frente, lo secundario atenuado");
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                CombatWaveDirector director = CombatWaveDirector.Instance;
                if (!director.HasActiveWave) return true;
                ShadowCreature next = director.Alive.OrderBy(c => (c.transform.position - Player().position).sqrMagnitude).FirstOrDefault();
                if (next != null) StrikeAt(next, 1.1f);
                if (director.AliveCount == 1 && !captured.Contains("f5_10_finisher") && next != null && next.Health <= 1)
                {
                    captured.Add("f5_10_finisher");
                }
                return TimedOut(45);
            });
            steps.Add(() => Elapsed() > 0.12);
            steps.Add(() => { Capture("f5_10_finisher"); return true; });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() => Check(wavesCleared == 1, $"Oleada superada · valoración: «{lastRating}» · Vínculo Mina {QuestManager.Instance.GetBond("mina")}/5"));

            // --- 9) Vara de luz.
            steps.Add(() => { UnityEngine.Object.FindFirstObjectByType<LanternStaff>().UnlockNow(); return true; });
            steps.Add(() => Elapsed() > 0.7);
            steps.Add(() =>
            {
                Capture("f5_11_vara_luz");
                return Check(Mathf.Approximately(UnityEngine.Object.FindFirstObjectByType<LanternStaff>().RangeMultiplier, 1.45f), "Vara de luz: el barrido llega un 45 % más lejos");
            });

            // --- 10) Vínculo 5: vela explosiva de Teo y Brasa guardiana.
            steps.Add(() =>
            {
                HoldNight(0.5f);
                QuestManager.Instance.AddBond("teo", 5);
                QuestManager.Instance.AddBond("mina", 5);
                Teleport(new Vector3(0f, 0.12f, -6f), 0f);
                target = SpawnAhead<PenumbraMoth>(5f, 0.6f);
                return true;
            });
            steps.Add(() => Elapsed() > 0.3);
            steps.Add(() => Check(UnityEngine.Object.FindFirstObjectByType<BondCombatAbilities>().TryThrowCandle(), "Vínculo 5 con Teo: Vela explosiva lanzada"));
            steps.Add(() => Elapsed() > 0.65);
            steps.Add(() => { Capture("f5_12_vela_teo"); return true; });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() => Check(!target.IsAlive, "La vela de Teo disuelve a la sombra (área de 4 m)"));
            steps.Add(() =>
            {
                HoldNight(0.5f);
                target = SpawnAhead<PenumbraMoth>(3.5f, 0.6f);
                return true;
            });
            steps.Add(() => { HoldNight(0.5f); return target.Health < 3 || TimedOut(7); });
            steps.Add(() =>
            {
                Capture("f5_13_brasa_guardiana");
                bool active = UnityEngine.Object.FindFirstObjectByType<BrasaGuardian>() != null;
                return Check(active && target.Health < 3, $"Vínculo 5 con Mina: Brasa guardiana golpea sola (vida de la polilla {target.Health}/3)");
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                if (target.IsAlive && !TimedOut(6)) { StrikeAt(target, 1.1f); return false; }
                return true;
            });

            // --- 11) Energía a 0 en combate: no muere, pierde la luz un momento.
            steps.Add(() =>
            {
                Energy().ApplyDamage(200f);
                bool blocked = !Attack().TryStartSwing();
                Capture("f5_14_farol_apagado");
                bool ok = Check(Energy().IsExhausted && blocked, "Energía 0: Ilse no muere, pero no puede atacar ni iluminar hasta recuperar el 25 %");
                Energy().Restore(100f);
                return ok;
            });

            // --- 11b) Energía crítica (< 20 %): borde que late, Farol que parpadea y latido sonoro.
            steps.Add(() =>
            {
                Energy().Restore(100f);
                Energy().ApplyDamage(Energy().Current - 12f);
                return true;
            });
            steps.Add(() => Elapsed() > 0.5);
            steps.Add(() =>
            {
                Capture("f6_energia_critica");
                bool ok = Check(Energy().IsCritical, "Energía crítica: aviso visual y sonoro activo bajo el 20 %");
                Energy().Restore(100f);
                return ok;
            });
            steps.Add(() => Check(!Energy().IsCritical, "Al recuperar energía (> 25 %) el aviso crítico se apaga"));

            // --- 11c) Daño recibido: destello breve, apagón y empujón.
            steps.Add(() =>
            {
                float before = Energy().Current;
                IncomingHitResult r = PlayerCombatState.Instance.ReceiveHit(10f, Player().position + Player().forward * 1.5f);
                Check(r == IncomingHitResult.Hit && Energy().Current < before, $"Daño recibido: {before - Energy().Current:0} de energía, destello y apagón");
                return true;
            });
            steps.Add(() => Elapsed() > 0.05);
            steps.Add(() => { Capture("f6_dano_recibido"); Energy().Restore(100f); return true; });

            // --- 12) Reducir destellos (accesibilidad).
            steps.Add(() =>
            {
                AccessibilitySettings.SetReduceFlashes(true);
                bool ok = Check(Mathf.Approximately(AccessibilitySettings.FlashScale, 0.3f), "Accesibilidad: «Reducir destellos» escala viñeta, aberración y destellos al 30 %");
                AccessibilitySettings.SetReduceFlashes(false);
                return ok;
            });

            // --- 13) El Heraldo: enfrentamiento narrativo (4 golpes, retrocede y revela una pista).
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Heraldo_AI h = Heraldo_AI.Instance;
                h.enabled = true;
                // Fuerza la Persecución Activa (el estado es privado: solo en la prueba).
                FieldInfo field = typeof(Heraldo_AI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(h, Enum.ToObject(field.FieldType, 2));
                // Ilse cerca (8 m) para que la persecución no se pierda al instante.
                Vector3 spot = h.transform.position + h.transform.forward * 8f;
                if (UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) Teleport(hit.position, h.transform.eulerAngles.y + 180f);
                return true;
            });
            steps.Add(() =>
            {
                HoldNight(0.5f);
                Heraldo_AI h = Heraldo_AI.Instance;
                HeraldoConfrontation c = h.GetComponent<HeraldoConfrontation>();
                if (c.CompletedCount > 0 || Runner().IsOpen) return true;
                if (TimedOut(60)) return true;
                if (!h.IsChasing)
                {
                    // Persecución Activa: Ilse se deja ver a 8 m para que el Heraldo la persiga.
                    if (h.DistanceToPlayer > 10f)
                    {
                        Vector3 away = (Player().position - h.transform.position);
                        away.y = 0f;
                        Vector3 spot = h.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 8f;
                        if (UnityEngine.AI.NavMesh.SamplePosition(spot, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) Teleport(hit.position, 0f);
                    }
                    return false;
                }
                if (!c.CanBeHit) return false;
                Vector3 to = h.transform.position - Player().position;
                to.y = 0f;
                Teleport(h.transform.position - to.normalized * 1.4f, Quaternion.LookRotation(to.normalized).eulerAngles.y);
                if (!Attack().IsAttacking) Attack().TryStartSwing();
                if (c.Hits == 2 && !captured.Contains("f5_15_heraldo_enfrentamiento"))
                {
                    captured.Add("f5_15_heraldo_enfrentamiento");
                    Capture("f5_15_heraldo_enfrentamiento");
                }
                return false;
            });
            steps.Add(() => Elapsed() > 1.2);
            steps.Add(() =>
            {
                Capture("f5_16_heraldo_revela");
                HeraldoConfrontation c = Heraldo_AI.Instance.GetComponent<HeraldoConfrontation>();
                return Check(c.CompletedCount == 1 && Runner().IsOpen, "Heraldo: tras 4 golpes se aleja (sin ser derrotado) y revela una pista");
            });
            steps.Add(() => CloseDialogues(0.5));

            // --- Vecinos: ojos de luz con blend shapes.
            steps.Add(() =>
            {
                NpcFace face = UnityEngine.Object.FindObjectsByType<NpcFace>(FindObjectsSortMode.None).First(f => f.name == "NPC_Mina");
                face.TriggerJoy(5f);
                Transform head = face.transform.Find("Visual/Rig/Head");
                Camera cam = Camera.main;
                cam.GetComponent<CinemachineBrain>().enabled = false;
                Vector3 eye = head.position + Vector3.up * 0.25f;
                cam.transform.position = eye + face.transform.forward * 1.1f + Vector3.up * 0.05f;
                cam.transform.LookAt(eye);
                return true;
            });
            steps.Add(() => Elapsed() > 0.6);
            steps.Add(() =>
            {
                Capture("f5_17_mina_ojos");
                NpcFace face = UnityEngine.Object.FindObjectsByType<NpcFace>(FindObjectsSortMode.None).First(f => f.name == "NPC_Mina");
                Camera.main.GetComponent<CinemachineBrain>().enabled = true;
                return Check(face.MoodName == "Alegre", "Ojos de Mina en estado Alegre (blend shape)");
            });
            steps.Add(() =>
            {
                Time.timeScale = 3f;
                Log($"CPU por frame durante el combate: ver media global al final");
                return true;
            });
        }

        // ================================================================== Acciones

        private static double Elapsed() => EditorApplication.timeSinceStartup - stepStart;
        private static bool TimedOut(double seconds) => Elapsed() > seconds;

        private static DialogueRunner Runner() => DialogueRunner.Instance;
        private static PlayerEnergy Energy() => UnityEngine.Object.FindFirstObjectByType<PlayerEnergy>();
        private static CharacterFace Face() => UnityEngine.Object.FindFirstObjectByType<CharacterFace>();
        private static QuestLogUI QuestLog() => UnityEngine.Object.FindFirstObjectByType<QuestLogUI>();
        private static Transform Player() => GameObject.FindGameObjectWithTag("Player").transform;

        private static PlayerController Controller() => UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        private static LightAttack Attack() => UnityEngine.Object.FindFirstObjectByType<LightAttack>();
        private static int perfectDodges, wavesCleared, bruteWarnings, swings;
        private static bool sawBoltBehindCover;
        private static string lastRating;
        private static ShadowCreature target;
        private static readonly List<ShadowCreature> group = new List<ShadowCreature>();

        private static T Pooled<T>(Func<T, bool> filter = null) where T : ShadowCreature
        {
            foreach (T c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.gameObject.activeSelf && (filter == null || filter(c))) return c;
            }
            throw new Exception("no queda ninguna criatura libre de tipo " + typeof(T).Name);
        }

        /// <summary>Hace aparecer una criatura del pool delante de Ilse.</summary>
        private static T SpawnAhead<T>(float distance, float height = 0f, Func<T, bool> filter = null) where T : ShadowCreature
        {
            T c = Pooled(filter);
            Transform p = Player();
            c.Spawn(p.position + p.forward * distance + Vector3.up * height, "prueba");
            return c;
        }

        /// <summary>La noche se queda quieta en un punto (la sección de combate es larga a velocidad x1).</summary>
        private static void HoldNight(float progress)
        {
            NightManager night = NightManager.Instance;
            typeof(NightManager).GetProperty("Elapsed").GetSetMethod(true).Invoke(night, new object[] { night.Duration * progress });
            KeepZonesAlive();
            Energy().Restore(100f);
        }

        /// <summary>Se coloca a 'gap' m de la criatura mirándola y lanza un barrido si puede.</summary>
        private static void StrikeAt(ShadowCreature c, float gap = 1.3f)
        {
            Vector3 to = c.transform.position - Player().position;
            to.y = 0f;
            if (to.magnitude > gap + 0.4f || to.magnitude < 0.3f)
            {
                Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward;
                Vector3 standAt = c.transform.position - dir * gap;
                Teleport(new Vector3(standAt.x, Player().position.y, standAt.z), Quaternion.LookRotation(dir).eulerAngles.y);
            }
            if (!Attack().IsAttacking && Attack().TryStartSwing()) swings++;
        }

        /// <summary>Fuente de luz de prueba (como si Ilse mantuviera el Farol encendido sobre un punto).</summary>
        private class TestLight : Lighting.IIlluminationSource
        {
            public Vector3 LightPosition { get; set; }
            public float IlluminationRadius => 6f;
            public bool IsIlluminating => true;
        }

        private static readonly TestLight testLight = new TestLight();
        private static PlayerMantle Mantle() => UnityEngine.Object.FindFirstObjectByType<PlayerMantle>();
        private static PlayerDash Dash() => UnityEngine.Object.FindFirstObjectByType<PlayerDash>();
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        private static float jumpBaseY, jumpPeakY;
        private static Vector3 dashStart;

        private static bool MomentPlaying()
        {
            NightEvent moment = UnityEngine.Object.FindFirstObjectByType<NightEvent>();
            return moment != null && moment.IsPlaying;
        }

        private static PickupItem FindPickup(string itemId)
        {
            foreach (PickupItem item in PickupItem.All)
            {
                if (item.ItemId == itemId && item.IsAvailable) return item;
            }
            throw new Exception("no se encontró el objeto recogible '" + itemId + "'");
        }

        private static PickupItem FindPickupOfKind(PickupKind kind, Vector3 near)
        {
            PickupItem best = null;
            float bestDistance = float.MaxValue;
            foreach (PickupItem item in PickupItem.All)
            {
                if (item.Kind != kind || !item.IsAvailable) continue;
                float d = Vector3.Distance(item.transform.position, near);
                if (d < bestDistance) { bestDistance = d; best = item; }
            }
            if (best == null) throw new Exception("no queda ningún recogible de tipo " + kind);
            return best;
        }

        private static Button GetButton(UnityEngine.Object owner, string field)
        {
            return (Button)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        }

        /// <summary>Cierra cualquier diálogo abierto (p. ej. el regalo de Teo) y espera un poco.</summary>
        private static bool CloseDialogues(double minSeconds)
        {
            if (Runner().IsOpen)
            {
                if (Runner().IsChoosing) Runner().SelectChoice(0);
                else Runner().SkipToEnd();
                return false;
            }
            return Elapsed() > minSeconds;
        }

        /// <summary>Evita que la prueba termine la noche antes de tiempo por zonas apagadas.</summary>
        private static void KeepZonesAlive()
        {
            foreach (string id in new[] { "Plaza", "Mercado" })
            {
                ZoneHope zone = ZoneHope.Find(id);
                if (zone != null && zone.Hope < 50f) zone.AddHope(50f - zone.Hope);
            }
        }

        private static bool Check(bool condition, string message)
        {
            Log((condition ? "OK    " : "FALLO ") + message);
            if (!condition) failed = true;
            return true;
        }

        private static QuestState State(string questId)
        {
            QuestRuntime q = QuestManager.Instance.FindById(questId);
            return q != null ? q.State : QuestState.Bloqueada;
        }

        private static bool Expect(string questId, QuestState expected)
        {
            QuestState actual = State(questId);
            bool ok = actual == expected;
            Log((ok ? "OK    " : "FALLO ") + questId + " -> " + expected + (ok ? "" : " (está en " + actual + ")"));
            if (!ok) failed = true;
            return true;
        }

        /// <summary>Se coloca frente al NPC y le habla.</summary>
        private static void Talk(string npcId)
        {
            NpcInteractable npc = NpcInteractable.Find(npcId);
            Vector3 front = npc.transform.position + npc.transform.forward * 1.6f;
            Teleport(front, npc.transform.eulerAngles.y + 180f);
            npc.Interact(Player());
        }

        /// <summary>Habla con el NPC para entregar la misión y termina el diálogo de agradecimiento.</summary>
        private static bool TurnIn(string npcId, string questId)
        {
            if (Runner().IsOpen)
            {
                if (Runner().IsChoosing) Runner().SelectChoice(0);
                else Runner().SkipToEnd();
                return false;
            }
            if (State(questId) == QuestState.PorEntregar)
            {
                Talk(npcId);
                return false;
            }
            return Expect(questId, QuestState.Completada);
        }

        private static void CollectFireflyNear(Vector3 point)
        {
            FireflyPool pool = UnityEngine.Object.FindFirstObjectByType<FireflyPool>();
            pool.SpawnAt(point + Vector3.up * 1.1f);
            Teleport(point, 0f);
        }

        /// <summary>Sigue al NPC escoltado a 2 m (simula al jugador) hasta que llega.</summary>
        private static bool FollowNpc(string npcId, string questId, string captureName)
        {
            CharacterAI npc = CharacterAI.Find(npcId);
            Vector3 behind = npc.transform.position - npc.transform.forward * 2f;
            Teleport(behind, npc.transform.eulerAngles.y);

            if (npc.EscortProgress01 > 0.45f && !captured.Contains(captureName))
            {
                captured.Add(captureName);
                Capture(captureName);
            }

            QuestState state = State(questId);
            if (state == QuestState.PorEntregar) return Expect(questId, QuestState.PorEntregar);
            if (state == QuestState.Fallida || TimedOut(60)) return Expect(questId, QuestState.PorEntregar);
            return false;
        }

        private static readonly HashSet<string> captured = new HashSet<string>();

        private static void Teleport(Vector3 position, float yaw)
        {
            Transform player = Player();
            CharacterController cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.SetPositionAndRotation(position + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;

            CinemachineOrbitalFollow orbital = UnityEngine.Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
            if (orbital != null)
            {
                InputAxis horizontal = orbital.HorizontalAxis;
                horizontal.Value = yaw;
                orbital.HorizontalAxis = horizontal;
            }
        }

        /// <summary>Cámara de retrato frente a la cara de Ilse (desactiva Cinemachine mientras tanto).</summary>
        private static void FaceCamera(bool portrait)
        {
            Camera cam = Camera.main;
            CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
            brain.enabled = !portrait;
            if (!portrait) return;

            Transform player = Player();
            Vector3 head = player.position + Vector3.up * 1.4f;
            cam.transform.position = head + player.forward * 1.25f + Vector3.up * 0.05f;
            cam.transform.LookAt(head - Vector3.up * 0.12f);
        }

        // ================================================================== Captura e informe

        private static void Capture(string name)
        {
            Camera cam = Camera.main;
            RenderTexture rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            RenderMode[] modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (!canvases[i].isRootCanvas) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 0.3f;
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].isRootCanvas) canvases[i].renderMode = modes[i];
            }

            File.WriteAllBytes(Path.Combine(outputDir, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Log("Captura " + name);
        }

        private static void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[Ascua][Misiones] " + line);
        }

        private static void Fail(string reason)
        {
            failed = true;
            Log("FALLO " + reason);
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            if (frameTimes.Count > 0)
            {
                frameTimes.Sort();
                float sum = 0f;
                foreach (float t in frameTimes) sum += t;
                Log($"CPU por frame: media {sum / frameTimes.Count:0.00} ms · p95 {frameTimes[(int)(frameTimes.Count * 0.95f)]:0.00} ms ({frameTimes.Count} frames)");
            }
            Log(failed ? "RESULTADO: HAY FALLOS" : "RESULTADO: TODO CORRECTO");
            File.WriteAllText(Path.Combine(outputDir, "resultado_misiones.txt"), report.ToString());

            Time.timeScale = 1f;
            EditorSettings.enterPlayModeOptionsEnabled = originalOptionsEnabled;
            EditorSettings.enterPlayModeOptions = originalOptions;

            if (batch) EditorApplication.Exit(failed ? 1 : 0);
            else EditorApplication.ExitPlaymode();
        }
    }
}
