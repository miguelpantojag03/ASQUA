using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascua.Characters;
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
            if (EditorApplication.timeSinceStartup - realStart > 600.0)
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
            steps.Add(() => { Capture("f3_05_dialogo_mina"); Runner().SkipToEnd(); return true; });
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
            steps.Add(() => { KeepZonesAlive(); return Elapsed() > 1.2; });
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
                Log("Audio: AudioManager " + (Ascua.Audio.AudioManager.Instance != null ? "activo" : "ausente"));
                return Check(PhotoMode.Instance != null && PhotoMode.Instance.IsActive, "Modo foto desde el resumen (HUD oculto, cámara libre)");
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
