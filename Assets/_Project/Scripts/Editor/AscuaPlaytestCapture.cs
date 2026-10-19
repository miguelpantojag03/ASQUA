using System.Collections.Generic;
using System.IO;
using System.Text;
using Ascua.Core;
using Ascua.World;
using Unity.Cinemachine;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Prueba automática de una noche: entra en Play, acelera el tiempo, fuerza la muerte de
    /// El Mercado y guarda capturas (con HUD) en la carpeta "Capturas" del proyecto, además de
    /// un informe de tiempo de CPU por frame y memoria asignada.
    /// Menú: Ascua > Prueba automática con capturas. También funciona en modo -batchmode.
    /// </summary>
    public static class AscuaPlaytestCapture
    {
        private const string PlazaScenePath = AscuaAssetFactory.ScenesFolder + "/Plaza.unity";
        private const float TimeScale = 4f;
        private const int Width = 1600;
        private const int Height = 900;

        private static int step;
        private static float stepStartTime;
        private static double realStart;
        private static double lastTrace;
        private static double summaryShownAt;
        private static string outputDir;
        private static bool batch;
        private static bool originalOptionsEnabled;
        private static EnterPlayModeOptions originalOptions;
        private static readonly StringBuilder report = new StringBuilder();
        private static readonly List<float> frameTimes = new List<float>();
        private static ProfilerRecorder gcRecorder;
        private static long gcTotal;
        private static int gcFrames;

        [MenuItem("Ascua/Prueba automática con capturas", priority = 41)]
        public static void RunFromMenu()
        {
            Begin(false);
        }

        /// <summary>Unity.exe -batchmode -projectPath ... -executeMethod Ascua.EditorTools.AscuaPlaytestCapture.RunBatch</summary>
        public static void RunBatch()
        {
            Begin(true);
        }

        private static void Begin(bool isBatch)
        {
            batch = isBatch;
            outputDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Capturas");
            Directory.CreateDirectory(outputDir);
            report.Clear();
            frameTimes.Clear();
            gcTotal = 0;
            gcFrames = 0;

            EditorSceneManager.OpenScene(PlazaScenePath);

            // Sin recarga de dominio, para que este script siga vivo al entrar en Play.
            originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            originalOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            step = 0;
            realStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - realStart > 600.0)
            {
                Log("FALLO: la prueba superó el tiempo máximo.");
                Finish(1);
                return;
            }
            if (!EditorApplication.isPlaying) return;

            // Sin ventana con foco (o en -batchmode) el editor apenas avanza el juego: se fuerza cada frame.
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();

            NightManager night = NightManager.Instance;
            if (night == null) return;

            // Traza periódica para diagnosticar si el juego avanza.
            if (EditorApplication.timeSinceStartup - lastTrace > 10.0)
            {
                lastTrace = EditorApplication.timeSinceStartup;
                Debug.Log($"[Ascua][Traza] paso {step} · noche {night.Progress01 * 100f:0.0}% · Time.time {Time.time:0.0} · timeScale {Time.timeScale} · frame {Time.frameCount} · corriendo {night.IsRunning}");
            }

            if (step > 0)
            {
                frameTimes.Add(Time.unscaledDeltaTime * 1000f);
                if (gcRecorder.Valid)
                {
                    gcTotal += gcRecorder.LastValue;
                    gcFrames++;
                }
            }

            float now = Time.time;

            // Si la noche termina antes (todas las zonas muertas), se salta directamente al resumen.
            if (!night.IsRunning && step > 0 && step < 7)
            {
                Log($"La noche terminó antes de tiempo ({night.Progress01 * 100f:0}%): todas las zonas murieron.");
                step = 7;
            }

            // Tras apagar El Mercado se simula a un jugador que cuida La Plaza,
            // para poder ver el avance de la Penumbra hasta el final.
            if (step >= 4 && step < 7)
            {
                ZoneHope plaza = FindZone("Plaza");
                if (plaza != null && !plaza.IsDead && plaza.Hope < 45f) plaza.AddHope(45f - plaza.Hope);
            }

            switch (step)
            {
                case 0:
                    Time.timeScale = TimeScale;
                    gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                    Log($"Ritmo: {night.Pacing} · duración {night.Duration:0} s · escala de tiempo x{TimeScale}");
                    NextStep(now);
                    break;

                case 1:
                    if (night.Progress01 >= 0.12f)
                    {
                        Capture("01_inicio_plaza");
                        // Mirar hacia El Mercado desde su entrada este.
                        Teleport(new Vector3(-31f, 0.12f, 2f), -90f);
                        NextStep(now);
                    }
                    break;

                case 2:
                    if (now - stepStartTime >= 1.5f)
                    {
                        ZoneHope mercado = FindZone("Mercado");
                        Log($"El Mercado antes de forzar su muerte: Esperanza {mercado.Hope:0.0}");
                        mercado.AddHope(-1000f);
                        NextStep(now);
                    }
                    break;

                case 3:
                    if (now - stepStartTime >= 1.0f)
                    {
                        Capture("02_mercado_apagandose");
                        NextStep(now);
                    }
                    break;

                case 4:
                    if (now - stepStartTime >= 4f)
                    {
                        Capture("03_mercado_muerto");
                        // Volver a La Plaza mirando al edificio este (de los primeros que alcanza la Penumbra).
                        Teleport(new Vector3(0f, 0.12f, -3f), 90f);
                        NextStep(now);
                    }
                    break;

                case 5:
                    if (night.Progress01 >= 0.73f)
                    {
                        Capture("04_penumbra_escarcha");
                        NextStep(now);
                    }
                    break;

                case 6:
                    if (night.Progress01 >= 0.9f)
                    {
                        Capture("05_penumbra_90");
                        NextStep(now);
                    }
                    break;

                case 7:
                    if (!night.IsRunning)
                    {
                        // El resumen aparece con un fundido en tiempo real: se espera a que termine.
                        summaryShownAt = EditorApplication.timeSinceStartup;
                        step = 8;
                    }
                    break;

                case 8:
                    if (EditorApplication.timeSinceStartup - summaryShownAt >= 1.5)
                    {
                        Capture("06_resumen");
                        WriteStats();
                        Finish(0);
                    }
                    break;
            }
        }

        private static void NextStep(float now)
        {
            step++;
            stepStartTime = now;
        }

        private static ZoneHope FindZone(string id)
        {
            foreach (ZoneHope zone in ZoneHope.All)
            {
                if (zone.ZoneId == id) return zone;
            }
            return null;
        }

        /// <summary>Mueve a Ilse y coloca la cámara orbital detrás de ella.</summary>
        private static void Teleport(Vector3 position, float yaw)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (cc != null) cc.enabled = true;

            CinemachineOrbitalFollow orbital = Object.FindFirstObjectByType<CinemachineOrbitalFollow>();
            if (orbital != null)
            {
                InputAxis horizontal = orbital.HorizontalAxis;
                horizontal.Value = yaw;
                orbital.HorizontalAxis = horizontal;
            }
        }

        private static void Capture(string name)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Log($"FALLO: sin cámara para la captura {name}");
                return;
            }

            RenderTexture rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // Los Canvas "Overlay" no salen en Camera.Render: se pasan temporalmente a modo cámara.
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            RenderMode[] modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (!canvases[i].isRootCanvas) continue;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = cam;
                canvases[i].planeDistance = 0.5f;
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

            string path = Path.Combine(outputDir, name + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            NightManager night = NightManager.Instance;
            Penumbra penumbra = Penumbra.Instance;
            StringBuilder zones = new StringBuilder();
            foreach (ZoneHope zone in ZoneHope.All)
            {
                zones.Append(zone.DisplayName).Append('=').Append(zone.IsDead ? "apagada" : zone.Hope.ToString("0")).Append(' ');
            }
            Log($"Captura {name}: noche {night.Progress01 * 100f:0}% · Penumbra radio {(penumbra != null ? penumbra.CurrentRadius : 0f):0.0} m · {zones}");
        }

        private static void WriteStats()
        {
            if (frameTimes.Count == 0) return;
            frameTimes.Sort();
            float sum = 0f;
            foreach (float t in frameTimes) sum += t;
            float average = sum / frameTimes.Count;
            float p95 = frameTimes[Mathf.Min(frameTimes.Count - 1, Mathf.FloorToInt(frameTimes.Count * 0.95f))];
            float max = frameTimes[frameTimes.Count - 1];

            Log($"Frames medidos: {frameTimes.Count} · media {average:0.00} ms · p95 {p95:0.00} ms · máx {max:0.00} ms");
            Log(gcFrames > 0
                ? $"Memoria asignada (GC) media por frame: {gcTotal / (float)gcFrames:0} bytes"
                : "Memoria asignada (GC): no disponible en este modo");
        }

        private static void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[Ascua][Prueba] " + line);
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            if (gcRecorder.Valid) gcRecorder.Dispose();
            Time.timeScale = 1f;

            File.WriteAllText(Path.Combine(outputDir, "resultado.txt"), report.ToString());
            EditorSettings.enterPlayModeOptionsEnabled = originalOptionsEnabled;
            EditorSettings.enterPlayModeOptions = originalOptions;

            if (batch)
            {
                EditorApplication.Exit(exitCode);
            }
            else
            {
                EditorApplication.ExitPlaymode();
                EditorUtility.RevealInFinder(Path.Combine(outputDir, "resultado.txt"));
            }
        }
    }
}
