using System.Text;
using Ascua.Characters;
using Ascua.Core;
using Ascua.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

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

            report = sb.ToString();
            return ok;
        }

        private static bool Check(StringBuilder sb, bool condition, string message)
        {
            sb.AppendLine((condition ? "OK    " : "FALLO ") + message);
            return condition;
        }
    }
}
