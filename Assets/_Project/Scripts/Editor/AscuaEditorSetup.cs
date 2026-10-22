using UnityEditor;
using UnityEngine;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Ajustes del editor que evitan que el juego "se rompa" al cambiar código con el Play activo.
    /// Por defecto Unity recompila y SIGUE jugando: los objetos que no se pueden serializar
    /// (MaterialPropertyBlock, singletons, registros estáticos) se pierden y aparecen errores en
    /// cada frame. Con esto, si cambian los scripts durante el Play, Unity para el juego y recompila.
    /// Se aplica solo al abrir el proyecto (es una preferencia del editor de este equipo).
    /// </summary>
    [InitializeOnLoad]
    public static class AscuaEditorSetup
    {
        // 0 = recompilar y seguir jugando · 1 = recompilar al terminar · 2 = parar y recompilar.
        private const string ScriptChangesKey = "ScriptCompilationDuringPlay";
        private const int StopPlayingAndRecompile = 2;

        static AscuaEditorSetup()
        {
            if (EditorPrefs.GetInt(ScriptChangesKey, 0) == StopPlayingAndRecompile) return;
            EditorPrefs.SetInt(ScriptChangesKey, StopPlayingAndRecompile);
            Debug.Log("[Ascua] Preferencia del editor: si cambian los scripts durante el Play, Unity parará el juego y recompilará (evita errores por recarga en caliente).");
        }
    }
}
