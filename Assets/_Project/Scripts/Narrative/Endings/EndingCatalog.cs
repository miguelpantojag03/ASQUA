using System.Collections.Generic;

namespace Ascua.Endings
{
    /// <summary>
    /// Textos de los seis finales y de las líneas de los personajes.
    /// Tono: melancólico pero esperanzador, sin violencia.
    /// </summary>
    public static class EndingCatalog
    {
        private static readonly EndingInfo[] all =
        {
            new EndingInfo(EndingId.Ceniza, "Ceniza", "Lo que queda de una brasa",
                "Vesper amanece gris y callada. Las calles guardan el olor de los faroles que se fueron apagando.",
                "Ilse recoge un puñado de ceniza todavía tibia y lo guarda en el bolsillo.",
                "Mientras quede una brasa, alguien podrá volver a encenderla."),

            new EndingInfo(EndingId.Vigilia, "Vigilia", "Vesper espera despierta",
                "La Penumbra se retira sin prisa, como quien piensa volver.",
                "Unas pocas ventanas siguen encendidas y la gente se turna para cuidarlas.",
                "No es el alba que soñaban, pero nadie en Vesper vuelve a dormir a oscuras."),

            new EndingInfo(EndingId.Alba, "Alba", "La luz vuelve a las calles",
                "El sol encuentra a Vesper despierta y con los faroles todavía tibios.",
                "En la Plaza alguien barre la ceniza y en el Mercado vuelven a oírse voces.",
                "Ilse apaga su Farol. Por primera vez en mucho tiempo, no hace falta."),

            new EndingInfo(EndingId.Constelacion, "Constelación", "Cada farol, una estrella",
                "Desde el tejado más alto, Vesper parece un cielo puesto del revés.",
                "Cada luz que Ilse ayudó a encender la cuida ahora otra mano.",
                "La noche volverá, pero ya nadie la esperará solo."),

            new EndingInfo(EndingId.ElTrato, "El Trato", "Una luz prestada",
                "La ciudad amanece encendida, aunque una de sus luces no le pertenece.",
                "El Heraldo cobró su parte en silencio y Ilse aún siente el frío en las manos.",
                "Algunas deudas se pagan despacio, con cada noche que se elige no volver a pactar."),

            new EndingInfo(EndingId.FarolApagado, "Farol Apagado", "Ya no hace falta",
                "Mina, Teo y Perpetua esperan a Ilse en la Plaza, cada uno con su propia luz.",
                "Ilse lee la última página de su Diario y entiende por qué empezó a escribirlo.",
                "Deja el Farol en el suelo, apagado. Vesper sabe brillar sin él.")
        };

        /// <summary>Los seis finales, en el orden de la galería.</summary>
        public static IReadOnlyList<EndingInfo> All => all;

        public static EndingInfo Get(EndingId id)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Id == id) return all[i];
            }
            return all[0];
        }

        /// <summary>
        /// Una línea por personaje con Vínculo alto (por defecto, 3 o más).
        /// Es lo último que se ve en la pantalla de final.
        /// </summary>
        public static List<string> CompanionLines(int bondMina, int bondTeo, int bondPerpetua, int minBond = 3)
        {
            List<string> lines = new List<string>();
            if (bondMina >= minBond) lines.Add("Mina enciende su farolillo de papel.");
            if (bondTeo >= minBond) lines.Add("Teo deja encendido el farol de los Bravo.");
            if (bondPerpetua >= minBond) lines.Add("Perpetua vuelve a contar historias en la Casa de ancianos.");
            return lines;
        }
    }
}
