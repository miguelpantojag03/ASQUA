namespace Ascua.Endings
{
    /// <summary>Textos de un final: nombre, título corto (también la línea de la galería) y epílogo.</summary>
    public class EndingInfo
    {
        public readonly EndingId Id;
        public readonly string Name;
        public readonly string ShortTitle;
        public readonly string[] Epilogue;

        public EndingInfo(EndingId id, string name, string shortTitle, params string[] epilogue)
        {
            Id = id;
            Name = name;
            ShortTitle = shortTitle;
            Epilogue = epilogue;
        }

        /// <summary>Clave estable para guardar el final en disco (el nombre del enum).</summary>
        public string SaveKey => Id.ToString();
    }
}
