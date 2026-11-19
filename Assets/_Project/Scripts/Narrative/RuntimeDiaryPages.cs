using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Páginas del Diario creadas por código (no hay asset en la escena que cablear).
    /// El Codex las consulta junto a las suyas, así se leen en cualquier noche posterior.
    /// </summary>
    public static class RuntimeDiaryPages
    {
        /// <summary>Página 7 (Noche 3, el tren fantasma): presagio obligatorio de Corvin (handoff §3.4).</summary>
        public const string TrenPageId = "pagina_07";

        private static List<DiaryPage> pages;

        public static IReadOnlyList<DiaryPage> All
        {
            get
            {
                Ensure();
                return pages;
            }
        }

        /// <summary>La página con ese id, o null.</summary>
        public static DiaryPage Find(string pageId)
        {
            Ensure();
            for (int i = 0; i < pages.Count; i++) if (pages[i].pageId == pageId) return pages[i];
            return null;
        }

        private static void Ensure()
        {
            if (pages != null && !pages.Exists(p => p == null)) return;
            pages = new List<DiaryPage>
            {
                Create(TrenPageId, 7, "Un tren sin nadie",
                    "Esta noche pasó un tren hecho de luz por el borde de Vesper. Nadie bajó. Nadie subió.\n\n" +
                    "Tuve un hijo. Lo tuve. Y no puedo decir su nombre.")
            };
        }

        private static DiaryPage Create(string id, int number, string title, string text)
        {
            DiaryPage page = ScriptableObject.CreateInstance<DiaryPage>();
            page.name = "P_" + number.ToString("00") + "_" + id + " (runtime)";
            // Que no se descargue al cambiar de escena ni se guarde en ningún asset.
            page.hideFlags = HideFlags.HideAndDontSave;
            page.pageId = id;
            page.number = number;
            page.title = title;
            page.text = text;
            return page;
        }
    }
}
