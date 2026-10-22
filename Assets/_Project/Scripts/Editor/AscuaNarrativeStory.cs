using System.Collections.Generic;
using Ascua.Narrative;
using Ascua.Quests;
using UnityEditor;
using UnityEngine;
using static Ascua.EditorTools.AscuaNarrativeContent;

namespace Ascua.EditorTools
{
    /// <summary>Assets de la Parte D (trama).</summary>
    public class StoryAssets
    {
        public Quest MinaFarolillo, TeoTodaviaSirvo;
        public DiaryPage OmenPage, FarolilloPage, TeoPage;
    }

    /// <summary>
    /// Parte D · Trama.
    ///  D.1 Contexto emocional: 1-2 frases de "por qué me importa" en la oferta de cada misión y diálogos de
    ///      progreso nervioso / aliviado según el avance real (QuestManager.GetMood).
    ///  D.3 Misiones secundarias: el farolillo de papel del padre de Mina (Noches 2-3) y "Todavía sirvo" de Teo (Noches 3-4).
    ///  D.4 Diario: páginas 4, 5 y 6.
    /// Igual que el resto: los assets solo se crean si no existen, y en las misiones existentes solo se
    /// rellenan los campos VACÍOS (nunca se pisa lo que hayas escrito a mano).
    /// El Heraldo se revela como Corvin en la Noche 4: aquí solo hay pistas (un pájaro negro, una «C.», alguien que se cansó).
    /// </summary>
    public static class AscuaNarrativeStory
    {
        private const string Ilse = "Ilse";
        private const string Mina = "Mina";
        private const string Teo = "Teo";
        private const string Perpetua = "Perpetua";
        private const string PagesFolder = AscuaAssetFactory.Root + "/Data/Diario";

        public static StoryAssets CreateAll(NarrativeAssets n)
        {
            StoryAssets s = new StoryAssets();

            // ============================================================ D.4 · Diario (páginas 4-6)
            s.OmenPage = Page("pagina_04", 4, "Una luz que no era de nadie",
                "Esta noche vi un farol encendido donde no hay ningún farol. Violeta. Quieto. Como un ojo.\n\n" +
                "Los faroles se encienden para que alguien encuentre el camino. Entonces, ¿a quién llamaba esa luz?\n\n" +
                "No se lo he contado a nadie. Todavía no sé si me dio miedo… o pena.");
            s.FarolilloPage = Page("pagina_05", 5, "Pájaros de papel",
                "El farolillo del padre de Mina tiene un pájaro negro pintado y una sola letra: C.\n\n" +
                "Alguien en esta ciudad doblaba papel mejor que nadie y regalaba luces que no eran para él. Un día dejó de venir.\n\n" +
                "Me pregunto si alguien le dio las gracias antes de que se cansara. Por si acaso, se las doy yo: gracias, C., por una luz que todavía alumbra.");
            s.TeoPage = Page("pagina_06", 6, "Lo que pesa una vela",
                "Teo subió la escalera despacio, con la mecha en la mano y las rodillas temblando. No le ayudé. Me pidió que no lo hiciera.\n\n" +
                "Aprendí algo: a veces alumbrar a alguien no es darle tu luz. Es quedarte cerca mientras enciende la suya.\n\n" +
                "Una vela pesa muy poco. Lo que pesa es que nadie te vea encenderla.");

            // ============================================================ D.3 · Mina · El farolillo de papel
            s.MinaFarolillo = QuestAsset("Q_Mina_Farolillo", q =>
            {
                q.questId = "mina_farolillo";
                q.giverNpcId = "mina";
                q.title = "El farolillo de papel";
                q.description = "El padre de Mina colgaba un farolillo de papel rojo en el callejón de los faroleros. Encuéntralo y llévaselo.";
                q.type = QuestType.Entrega;
                q.shortInstruction = "Busca el farolillo de papel en el callejón de los faroleros y llévaselo a Mina.";
                q.deliverItemId = "farolillo_papel";
                q.deliverItemName = "el farolillo de papel";
                q.deliverToNpcId = "mina";
                q.failIfTargetZoneDies = false;
                q.rewardEnergy = 15f;
                q.rewardHope = 8f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("mina_farolillo_oferta", new[]
                {
                    L(Mina, "¿Puedo pedirte una cosa que no es urgente? Bueno… para mí sí lo es."),
                    L(Mina, "Mi papá tenía un farolillo de papel. Rojo, con un pájaro negro pintado en un lado."),
                    L(Mina, "Se lo regaló un amigo suyo, otro farolero. Papá decía que nadie doblaba el papel como él."),
                    L(Ilse, "¿Dónde lo perdió?"),
                    L(Mina, "Lo colgaba en el callejón de los faroleros, detrás de la plaza. Pero hace mucho que no voy. Allí está muy oscuro.")
                }, Accept("Lo buscaré en el callejón"), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("mina_farolillo_progreso", new[]
                {
                    L(Mina, "El callejón tiene cajas en la entrada. Papá saltaba por encima como si nada."),
                    L(Mina, "Busca un poquito de rojo entre el hollín.")
                });
                q.completedDialogue = Dialogue("mina_farolillo_hecho", new[]
                {
                    L(Mina, "¡Es este! Mira el pájaro… está un poco borrado, pero es él."),
                    L(Mina, "Por dentro hay algo escrito. «Para que la tuya no se apague nunca. — C.»"),
                    L(Ilse, "¿C.? ¿Quién era?"),
                    L(Mina, "No lo sé. Papá nunca decía su nombre. Decía que tenía nombre de pájaro… y que un día se cansó de que nadie le diera las gracias."),
                    L(Mina, "Después dejó de venir. Y los faroles del callejón se fueron apagando uno a uno."),
                    L(Mina, "Gracias, Ilse. Ahora ya sé dónde poner a Brasa cuando tenga frío.")
                });
            });

            // ============================================================ D.3 · Teo · Todavía sirvo
            s.TeoTodaviaSirvo = QuestAsset("Q_Teo_TodaviaSirvo", q =>
            {
                q.questId = "teo_todavia_sirvo";
                q.giverNpcId = "teo";
                q.title = "Todavía sirvo";
                q.description = "Teo quiere encender con sus manos el farol de su padre. Vigila: mantén El Mercado sobre 50 de Esperanza y disuelve las 3 Polillas que vendrán a por la llama.";
                q.type = QuestType.DobleObjetivo;
                q.shortInstruction = "Vigila a Teo: El Mercado ≥ 50 mientras disuelves las Polillas que vienen a por su farol.";
                q.targetZoneId = "Mercado";
                q.hopeThreshold = 50f;
                q.defeatZoneId = "Mercado";
                q.defeatMoths = 3;
                q.defeatEchoes = 0;
                q.timeLimit = 45f;
                q.holdGraceSeconds = 2.5f;
                q.failIfTargetZoneDies = true;
                q.rewardEnergy = 20f;
                q.rewardHope = 15f;
                q.rewardHopeZoneId = "Mercado";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("teo_todavia_sirvo_oferta", new[]
                {
                    L(Teo, "¿Ves ese farol grande, a la entrada del mercado? Era el de mi padre. Lleva apagado desde antes de que llegaras."),
                    L(Teo, "Quiero encenderlo yo. Con mis manos. Ya sé lo que vas a decir: las rodillas, la escalera, la edad."),
                    L(Ilse, "No iba a decir nada de eso."),
                    L(Teo, "Pues mejor. Solo te pido que vigiles. Las sombras vendrán a por la luz en cuanto prenda la mecha.")
                }, Accept("Tú enciendes. Yo vigilo."), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("teo_todavia_sirvo_progreso", new[]
                {
                    L(Teo, "¡No me mires a mí, mira a las sombras! Yo con la mecha me apaño.")
                });
                q.completedDialogue = Dialogue("teo_todavia_sirvo_hecho", new[]
                {
                    L(Teo, "Ahí está. El farol de los Bravo, encendido otra vez."),
                    L(Teo, "Me temblaban las manos, ¿lo has visto? Cuarenta años y me temblaban como el primer día."),
                    L(Ilse, "Lo he visto todo, Teo. Lo has encendido tú."),
                    L(Teo, "…Gracias por no hacerlo por mí. Eso es lo que más necesitaba."),
                    L(Teo, "Toma. Un farolero siempre comparte la primera luz de la noche.")
                });
            });

            SetPage(s.MinaFarolillo, s.FarolilloPage);
            SetPage(s.TeoTodaviaSirvo, s.TeoPage);

            // Las secundarias aparecen dos noches seguidas (si no se hizo en la primera, hay otra oportunidad).
            AddToSet(n.Nights.Night2, s.MinaFarolillo);
            AddToSet(n.Nights.Night3, s.MinaFarolillo);
            AddToSet(n.Nights.Night3, s.TeoTodaviaSirvo);
            AddToSet(n.Nights.Night4, s.TeoTodaviaSirvo);

            // ============================================================ D.1 · Contexto emocional + progreso según avance
            Context(n.MinaBrasa,
                "Brasa es lo último que me queda de cuando la plaza estaba llena de gente.\nSi ella se apaga, ya no sé a quién cuidar.",
                Nervous("mina_brasa", L(Mina, "Ya casi no queda noche… ¿y si Brasa se queda sola ahí fuera?"), L(Ilse, "La encontraré antes. Te lo prometo.")),
                Relief("mina_brasa", L(Mina, "¡Ya casi! He visto una chispa roja moverse entre las otras."), L(Mina, "Una más y vuelve a casa. Lo noto aquí, en el pecho.")));

            Context(n.MinaMalecon,
                "Mi abuela me prometió que un día veríamos el mar juntas. Ella ya no puede.\nQuiero cumplirlo yo, aunque sea con Brasa.",
                Nervous("mina_malecon", L(Mina, "¡Espérame! Si te alejas, la niebla se me echa encima."), L(Ilse, "Estoy aquí. Mira mi luz y sígueme.")),
                Relief("mina_malecon", L(Mina, "Ya huele a sal. ¡Huele a sal, Brasa!"), L(Mina, "Gracias por ir a mi paso.")));

            Context(n.TeoVelas,
                "Mi padre abrió este mercado cuando aquí no había nada. Si se apaga mientras yo estoy, será culpa mía.\nNo quiero ser el último Bravo que baja la persiana.",
                Nervous("teo_velas", L(Teo, "¡Se me apagan los puestos del fondo! Más luz, que no llego."), L(Ilse, "Voy. Tú no te pares.")),
                Relief("teo_velas", L(Teo, "Mira, ya se asoma la señora del tercero. Eso es que funciona."), L(Teo, "Un poco más y lo tenemos. Ya casi.")));

            Context(n.TeoUltimasVelas,
                "Las hizo mi padre el último invierno que pudo. No me atrevo a decidir solo dónde se gastan.", null, null);

            // El Heraldo no explica quién es: solo deja ver que conoce ese cansancio (pista, no revelación).
            Context(n.HeraldoTrato,
                "No lo hago por el mercado. Lo hago porque conozco ese cansancio.\nLo he visto antes. Demasiadas veces.", null, null);

            Context(n.TeoPostal,
                "Esa mujer me fió pan el año que no vendí nada. Se lo debo.\nY Mina necesita algo de su abuela más que yo una caja llena de papeles.",
                Nervous("teo_postal", L(Teo, "Date prisa, que la noche se acaba y ese papel no aguanta otra humedad."), L(Ilse, "Ya voy. No se va a mojar.")),
                null);

            NightAssets nights = n.Nights;
            Quest dosFrentes = FindQuest(nights.Night2, "teo_dos_frentes");
            Quest camino = FindQuest(nights.Night2, "perpetua_camino");
            Quest velasReserva = FindQuest(nights.Night2, "teo_velas_reserva");
            Quest ninguna = FindQuest(nights.Night3, "mina_ninguna_se_apaga");
            Quest otraVez = FindQuest(nights.Night3, "perpetua_otra_vez");
            Quest vigilia = FindQuest(nights.Night4, "teo_vigilia");
            Quest dosLuces = FindQuest(nights.Night4, "mina_dos_luces");

            Context(dosFrentes,
                "Si pierdo el mercado, pierdo lo único que sé hacer. Si pierdo a Mina, pierdo algo peor.",
                Nervous("teo_dos_frentes", L(Teo, "¡El mercado se me apaga y la plaza sigue llena de alas! Corre, corre."), L(Ilse, "Voy y vuelvo. Aguanta.")),
                Relief("teo_dos_frentes", L(Teo, "¡Ya quedan menos polillas! Lo oigo desde aquí: la plaza respira.")));

            Context(camino,
                "Desde que se fue Ramón, nadie me espera despierto. Pero mi ventana, sí.\nMe gusta llegar y encenderla, como si él fuera a verla.",
                Nervous("perpetua_camino", L(Perpetua, "Criatura… no te veo bien. Y esa luz tuya se está cansando."), L(Perpetua, "Ve despacio, pero no me dejes.")),
                Relief("perpetua_camino", L(Perpetua, "Ya se ve la Casa. ¿Ves? La ventana de las flores."), L(Perpetua, "Ramón decía que el camino de vuelta siempre es más corto.")));

            Context(velasReserva,
                "Mi padre decía que las velas buenas se guardan para la peor noche. Lo que nunca me dijo es cómo se sabe cuál es.", null, null);

            Context(ninguna,
                "Desde mi ventana veo la fuente y el mercado. Si se apaga uno, ya no podré asomarme sin pensar en eso.",
                Nervous("mina_ninguna", L(Mina, "¡La fuente se está oscureciendo! ¡Y el mercado sigue lleno de sombras!"), L(Ilse, "Lo sé. Una más y vuelvo.")),
                Relief("mina_ninguna", L(Mina, "¡Casi no quedan! Mi padre habría aplaudido. Siempre aplaudía a las luces que no se rinden.")));

            Context(otraVez,
                "Bajo cada noche porque arriba la casa está muy callada. Abajo, al menos, te encuentro a ti.",
                Nervous("perpetua_otra_vez", L(Perpetua, "Este viento me empuja hacia atrás, criatura. Y tu luz parpadea."), L(Perpetua, "No la gastes toda en mí.")),
                Relief("perpetua_otra_vez", L(Perpetua, "Ya casi. Huelo el jazmín del portal: siempre huele así cuando estoy cerca de casa.")));

            Context(vigilia,
                "Llevo noches preguntándome si sirve de algo. Si el mercado aguanta esta, sabré que sí.",
                Nervous("teo_vigilia", L(Teo, "¡Se apagan los puestos! No, no, no… Ilse, ¡más luz!"), L(Ilse, "Estoy aquí, Teo. No se apaga nada.")),
                Relief("teo_vigilia", L(Teo, "Ya pasa lo peor, lo noto en el aire. Sigue así, sigue así.")));

            Context(dosLuces,
                "Esta noche no quiero ser la niña que espera en la fuente. Quiero ser la que ayuda.",
                Nervous("mina_dos_luces", L(Mina, "¡Me cuesta mantener la fuente! Son muchas… ¿vas a volver?"), L(Ilse, "Siempre vuelvo, Mina.")),
                Relief("mina_dos_luces", L(Mina, "¡Ya quedan pocas! La fuente brilla como antes, ¿lo ves desde ahí?")));

            Context(s.MinaFarolillo,
                "Papá ya no está para contármelo otra vez. Si tengo su farolillo, por lo menos tengo algo que me lo recuerde.\nNo quiero que se me olvide su voz.",
                Nervous("mina_farolillo", L(Mina, "Se está acabando la noche… Si no lo encuentras hoy, no pasa nada. ¿Verdad que no pasa nada?"), L(Ilse, "Sí que pasa. Por eso lo voy a encontrar.")),
                null);

            Context(s.TeoTodaviaSirvo,
                "Desde que no puedo salir a alumbrar calles, me siento como un farol sin mecha: un estorbo en la esquina.\nNecesito que alguien vea que todavía sirvo para algo.",
                Nervous("teo_todavia_sirvo", L(Teo, "¡Se me tuerce la mecha y el mercado se oscurece! No te vayas, Ilse, que solo no llego…"), L(Ilse, "No me voy. Sigue. Te estoy viendo.")),
                Relief("teo_todavia_sirvo", L(Teo, "¡Ya prende! ¿La oyes? Esa mecha siempre chisporroteaba así con mi padre."), L(Teo, "Unas pocas sombras más y es nuestro.")));

            return s;
        }

        // ================================================================== Utilidades

        private static DialogueSequence Nervous(string id, params DialogueLine[] lines) => Dialogue(id + "_progreso_nerviosa", lines);

        private static DialogueSequence Relief(string id, params DialogueLine[] lines) => Dialogue(id + "_progreso_aliviada", lines);

        /// <summary>Rellena el contexto emocional y los diálogos de progreso SOLO si están vacíos.</summary>
        private static void Context(Quest quest, string context, DialogueSequence nervous, DialogueSequence relief)
        {
            if (quest == null) return;
            bool dirty = false;
            if (string.IsNullOrEmpty(quest.emotionalContext))
            {
                quest.emotionalContext = context;
                dirty = true;
            }
            if (quest.progressNervousDialogue == null && nervous != null)
            {
                quest.progressNervousDialogue = nervous;
                dirty = true;
            }
            if (quest.progressReliefDialogue == null && relief != null)
            {
                quest.progressReliefDialogue = relief;
                dirty = true;
            }
            if (dirty) EditorUtility.SetDirty(quest);
        }

        private static void SetPage(Quest quest, DiaryPage page)
        {
            if (quest == null || quest.rewardDiaryPage != null) return;
            quest.rewardDiaryPage = page;
            EditorUtility.SetDirty(quest);
        }

        private static void AddToSet(NightQuestSet set, Quest quest)
        {
            if (set == null || quest == null || set.quests.Contains(quest)) return;
            set.quests.Add(quest);
            EditorUtility.SetDirty(set);
        }

        private static Quest FindQuest(NightQuestSet set, string questId)
        {
            if (set == null) return null;
            foreach (Quest q in set.quests)
            {
                if (q != null && q.questId == questId) return q;
            }
            return null;
        }

        private static DiaryPage Page(string id, int number, string title, string text)
        {
            return Load<DiaryPage>($"{PagesFolder}/P_{number:00}_{id}.asset", p =>
            {
                p.pageId = id;
                p.number = number;
                p.title = title;
                p.text = text;
            });
        }
    }
}
