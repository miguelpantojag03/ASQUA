using System.Collections.Generic;
using Ascua.Combat;
using Ascua.Narrative;
using Ascua.Quests;
using UnityEditor;
using UnityEngine;

namespace Ascua.EditorTools
{
    /// <summary>Assets narrativos generados (para cablearlos en la escena).</summary>
    public class NarrativeAssets
    {
        public NightQuestSet Night1;
        public Quest MinaBrasa, MinaMalecon, TeoVelas, TeoUltimasVelas, HeraldoTrato;
        public DialogueSequence MinaIdle, TeoIdle, HeraldoIdle;
        public Quest TeoPostal;
        public DialogueSequence TeoGift;
        public DiaryPage[] Pages;
        // Combate: lo que el Heraldo deja caer al alejarse tras un enfrentamiento.
        public DialogueSequence[] HeraldoRevelations;
    }

    /// <summary>
    /// Crea los diálogos, misiones y el set de la Noche 1 como assets en Assets/_Project/Data.
    /// IMPORTANTE: solo los crea si NO existen. Si los editas a mano en el Inspector, reconstruir
    /// la escena no los sobrescribe (borra el asset si quieres regenerarlo con estos textos).
    /// </summary>
    public static class AscuaNarrativeContent
    {
        private const string DataFolder = AscuaAssetFactory.Root + "/Data";
        private const string DialoguesFolder = DataFolder + "/Dialogos";
        private const string QuestsFolder = DataFolder + "/Misiones";
        private const string NightsFolder = DataFolder + "/Noches";
        public const string Night1Path = NightsFolder + "/Noche_01_LoQueTodaviaBrilla.asset";
        private const string PagesFolder = DataFolder + "/Diario";

        private const string Ilse = "Ilse";
        private const string Mina = "Mina";
        private const string Teo = "Teo";
        private const string Heraldo = "El Heraldo";

        public static NarrativeAssets CreateAll()
        {
            AscuaAssetFactory.EnsureFolder(DialoguesFolder);
            AscuaAssetFactory.EnsureFolder(QuestsFolder);
            AscuaAssetFactory.EnsureFolder(NightsFolder);

            NarrativeAssets n = new NarrativeAssets();

            // ============================================================ Diálogos de ambiente
            n.MinaIdle = Dialogue("mina_ambiente", new[]
            {
                L(Mina, "La plaza se oía antes. Ahora solo se oye el agua de la fuente… y ya ni eso."),
                L(Mina, "Si ves una lucecita roja, no la asustes. Es tímida.")
            });
            n.TeoIdle = Dialogue("teo_ambiente", new[]
            {
                L(Teo, "Si ves a alguien con frío, mándamelo. En este mercado siempre queda algo."),
                L(Teo, "Mientras quede un puesto abierto, Vesper no está cerrada.")
            });
            n.HeraldoIdle = Dialogue("heraldo_ambiente", new[]
            {
                L(Heraldo, "La noche es larga, pequeña luz. Ya cambiarás de opinión."),
                L(Heraldo, "Siempre volvemos a hablar.")
            });

            // ============================================================ Mina · Brasa (Recolección)
            DialogueSequence brasaOffer = Dialogue("mina_brasa_oferta", new[]
            {
                L(Mina, "¿Tú también llevas luz? Entonces quizá puedas ayudarme."),
                L(Mina, "Brasa se escapó cuando se apagó la última farola de la plaza."),
                L(Mina, "Es pequeñita. Una chispa que cabe en las manos… y que siempre va detrás de las luciérnagas."),
                L(Ilse, "Si junto algunas, ¿vendrá siguiéndolas?"),
                L(Mina, "Con tres, seguro. Sola le da miedo la oscuridad. Como a mí.")
            }, Accept("Las buscaré"), Decline("Ahora no puedo"));
            DialogueSequence brasaProgress = Dialogue("mina_brasa_progreso", new[]
            {
                L(Mina, "¿La has visto? Brilla un poquito más rojo que las demás."),
                L(Mina, "Las luciérnagas se esconden donde nadie mira. Donde todo está a oscuras."),
                L(Ilse, "Sigo buscando. No me he olvidado de ella."),
                L(Mina, "Gracias. Yo me quedo aquí, por si vuelve sola.")
            });
            DialogueSequence brasaDone = Dialogue("mina_brasa_completada", new[]
            {
                L(Mina, "¡Brasa! …Está tibia. Está bien."),
                L(Mina, "Pensé que se había ido con todo lo demás. Con las ventanas, con la gente."),
                L(Ilse, "Las cosas pequeñas también saben volver."),
                L(Mina, "Entonces yo también me quedo. Mientras haya una chispa, la plaza no está vacía."),
                L(Mina, "Toma. Brasa quiere que te lleves un poco de su calor.")
            });
            n.MinaBrasa = QuestAsset("Q_Mina_Brasa", q =>
            {
                q.questId = "mina_brasa";
                q.giverNpcId = "mina";
                q.title = "Una chispa llamada Brasa";
                q.description = "Brasa, la chispa de Mina, se escondió entre las luciérnagas. Reúne tres en La Plaza para que vuelva siguiendo su luz.";
                q.type = QuestType.Recoleccion;
                q.targetZoneId = "Plaza";
                q.requiredCount = 3;
                q.collectRadiusMultiplier = 1.8f;
                q.rewardEnergy = 20f;
                q.rewardBond = 2;
                q.offerDialogue = brasaOffer;
                q.inProgressDialogue = brasaProgress;
                q.completedDialogue = brasaDone;
            });

            // ============================================================ Mina · Malecón (Escolta)
            DialogueSequence maleconOffer = Dialogue("mina_malecon_oferta", new[]
            {
                L(Mina, "Brasa nunca ha visto el mar. Mi abuela decía que de noche se le ven estrellas dentro."),
                L(Mina, "Pero el malecón está lejos y la niebla se come el camino."),
                L(Ilse, "Iré contigo. No me separaré."),
                L(Mina, "¿Prometido? Si la Penumbra nos alcanza, Brasa se apaga.")
            }, Accept("Vamos, te acompaño"), Decline("Todavía no"));
            DialogueSequence maleconProgress = Dialogue("mina_malecon_progreso", new[]
            {
                L(Mina, "No te alejes, por favor. Contigo la niebla no se acerca tanto."),
                L(Mina, "Brasa lo va mirando todo. Creo que nunca había salido de la plaza."),
                L(Ilse, "Ya casi. Se oye el agua."),
                L(Mina, "Sí… se oye.")
            });
            DialogueSequence maleconDone = Dialogue("mina_malecon_completada", new[]
            {
                L(Mina, "Mira, Brasa. Ahí está. Todo negro, pero se mueve. Respira."),
                L(Mina, "La abuela tenía razón: hay luces dentro. Pequeñitas. Muy lejos."),
                L(Ilse, "Son las que todavía no se apagaron."),
                L(Mina, "Entonces vamos a cuidarlas. Tú las de arriba, yo las de aquí."),
                L(Mina, "Gracias por caminar conmigo. Ya no me da tanto miedo volver.")
            });
            n.MinaMalecon = QuestAsset("Q_Mina_Malecon", q =>
            {
                q.questId = "mina_malecon";
                q.giverNpcId = "mina";
                q.title = "Ver el mar con Brasa";
                q.description = "Acompaña a Mina hasta el malecón sin alejarte de ella y antes de que llegue la Penumbra.";
                q.type = QuestType.Escolta;
                q.prerequisite = n.MinaBrasa;
                q.escortNpcId = "mina";
                q.escortDestinationId = "malecon_mina";
                q.escortMaxDistance = 8f;
                q.escortGraceSeconds = 6f;
                q.rewardEnergy = 15f;
                q.rewardHope = 10f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = maleconOffer;
                q.inProgressDialogue = maleconProgress;
                q.completedDialogue = maleconDone;
            });

            // ============================================================ Teo · Velas (Rescate de Esperanza)
            DialogueSequence velasOffer = Dialogue("teo_velas_oferta", new[]
            {
                L(Teo, "Cuarenta años vendiendo en este mercado y nunca lo había visto tan callado."),
                L(Teo, "Me quedan velas. No muchas. Pero una vela en cada puesto y la gente vuelve a asomarse."),
                L(Teo, "El problema es que con esta oscuridad no encuentro ni mis propias manos."),
                L(Ilse, "Yo te alumbro. Tú repartes."),
                L(Teo, "Mantén vivo el mercado mientras doy la vuelta. Veinte segundos de luz, nada más. Ya es mucho pedir en estos tiempos.")
            }, Accept("Te alumbro"), Decline("Ahora no"));
            DialogueSequence velasProgress = Dialogue("teo_velas_progreso", new[]
            {
                L(Teo, "¡Más luz, que no veo dónde pongo la mecha!"),
                L(Teo, "Cada puesto encendido es una casa que se acuerda de abrir la ventana."),
                L(Ilse, "Aguanta, Teo. No me voy."),
                L(Teo, "Yo tampoco me voy a ningún lado. Este es mi sitio.")
            });
            DialogueSequence velasDone = Dialogue("teo_velas_completada", new[]
            {
                L(Teo, "Mira eso. Velas en todos los puestos. Hasta en el de la señora Amparo, que ya no viene."),
                L(Teo, "No es mucho. Pero se ve desde las ventanas, y eso basta para que alguien no cierre la suya."),
                L(Ilse, "Se ve desde muy lejos, Teo."),
                L(Teo, "Pues que se vea. Toma, para tu farol: las velas y los faroles somos familia."),
                L(Teo, "Si alguna noche vuelves, aquí habrá luz. Palabra de Bravo.")
            });
            n.TeoVelas = QuestAsset("Q_Teo_Velas", q =>
            {
                q.questId = "teo_velas";
                q.giverNpcId = "teo";
                q.title = "Una vela en cada puesto";
                q.description = "Mantén la Esperanza de El Mercado en 55 o más durante 20 segundos mientras Teo reparte sus velas.";
                q.type = QuestType.RescateEsperanza;
                q.targetZoneId = "Mercado";
                q.hopeThreshold = 55f;
                q.holdSeconds = 20f;
                q.rewardEnergy = 20f;
                q.rewardHope = 15f;
                q.rewardHopeZoneId = "Mercado";
                q.rewardBond = 2;
                q.offerDialogue = velasOffer;
                q.inProgressDialogue = velasProgress;
                q.completedDialogue = velasDone;
            });

            // ============================================================ Teo · Últimas velas (Diálogo / elección)
            DialogueChoice toPlaza = new DialogueChoice("Llévalas a La Plaza", ChoiceAction.CompletarMision)
            {
                hopeDelta = 18f,
                hopeZoneId = "Plaza",
                toast = "Teo llevó sus últimas velas a La Plaza"
            };
            DialogueChoice toMercado = new DialogueChoice("Que se queden en el Mercado", ChoiceAction.CompletarMision)
            {
                hopeDelta = 18f,
                hopeZoneId = "Mercado",
                toast = "Las últimas velas se quedan en El Mercado"
            };
            DialogueChoice keep = new DialogueChoice("Guárdalas para otra noche", ChoiceAction.CompletarMision)
            {
                energyDelta = 25f,
                toast = "Teo te guarda las velas · +25 de energía"
            };
            DialogueSequence lastOffer = Dialogue("teo_ultimas_oferta", new[]
            {
                L(Teo, "Me quedan las tres últimas velas. Las buenas, las de cera de verdad."),
                L(Teo, "La plaza está más sola que nunca. Pero el mercado es lo último que le queda a este barrio."),
                L(Teo, "Tú has visto las dos cosas esta noche. Decide tú.")
            }, toPlaza, toMercado, keep);
            DialogueSequence lastDone = Dialogue("teo_ultimas_completada", new[]
            {
                L(Teo, "Bien elegido. Las velas no sirven de nada en un cajón."),
                L(Teo, "Y tú no te apagues, ¿eh? Que me caes bien.")
            });
            n.TeoUltimasVelas = QuestAsset("Q_Teo_UltimasVelas", q =>
            {
                q.questId = "teo_ultimas_velas";
                q.giverNpcId = "teo";
                q.title = "Las tres últimas velas";
                q.description = "Teo te pregunta a dónde llevar sus últimas velas. Tu elección decide qué zona recibe Esperanza.";
                q.type = QuestType.Dialogo;
                q.prerequisite = n.TeoVelas;
                q.rewardBond = 1;
                q.offerDialogue = lastOffer;
                q.completedDialogue = lastDone;
            });

            // ============================================================ El Heraldo · Trato (misión inversa)
            DialogueChoice acceptDeal = new DialogueChoice("Acepto el trato", ChoiceAction.CompletarMision)
            {
                toast = "Trato aceptado · El Mercado pierde Esperanza"
            };
            DialogueChoice refuseDeal = new DialogueChoice("No. Ese mercado no es tuyo.", ChoiceAction.RechazarMision);
            DialogueSequence dealOffer = Dialogue("heraldo_trato_oferta", new[]
            {
                L(Heraldo, "Llevas mucho peso para ser solo un recuerdo, pequeña luz."),
                L(Heraldo, "Te veo correr de una calle a otra. Apagándote un poco en cada esquina."),
                L(Heraldo, "Puedo darte lo que te falta. Luz entera, ahora mismo."),
                L(Heraldo, "A cambio, el mercado duerme un poco antes. Nadie lo notará. Ya casi no queda nadie.")
            }, acceptDeal, refuseDeal);
            DialogueSequence dealDone = Dialogue("heraldo_trato_completada", new[]
            {
                L(Heraldo, "Sabia decisión. Las luces que se cuidan solas no necesitan a nadie."),
                L(Heraldo, "Volveremos a hablar. Siempre volvemos a hablar.")
            });
            n.HeraldoTrato = QuestAsset("Q_Heraldo_Trato", q =>
            {
                q.questId = "heraldo_trato_mercado";
                q.giverNpcId = "heraldo";
                q.title = "El Trato del Heraldo";
                q.description = "Misión inversa: aceptar llena tu Farol al instante, pero El Mercado pierde 35 de Esperanza.";
                q.type = QuestType.TratoHeraldo;
                q.rewardEnergy = 60f;
                q.rewardBond = 0;
                q.penaltyHope = 35f;
                q.penaltyZoneId = "Mercado";
                q.failIfTargetZoneDies = false;
                q.offerDialogue = dealOffer;
                q.completedDialogue = dealDone;
            });

            // ============================================================ Teo · La postal (Entrega: Lleva X a Y)
            DialogueSequence postalOffer = Dialogue("teo_postal_oferta", new[]
            {
                L(Teo, "Mira lo que apareció entre mis cajas: una postal. Es de la abuela de Mina, conozco esa letra."),
                L(Teo, "Siempre le escribía desde el puerto, aunque vivieran a dos calles."),
                L(Teo, "Yo ya no llego hasta la plaza con estas rodillas. ¿Se la llevas tú?"),
                L(Ilse, "Claro. ¿Dónde está?"),
                L(Teo, "Sobre el mostrador de mi puesto. Cógela con cuidado, que el papel ya no aguanta mucho.")
            }, Accept("Se la llevaré"), Decline("Ahora no"));
            DialogueSequence postalProgress = Dialogue("teo_postal_progreso", new[]
            {
                L(Teo, "¿Todavía la tienes? Mina vive pegada a la fuente. No tiene pérdida."),
                L(Teo, "Dile que es de parte de Teo. Bueno… y de su abuela.")
            });
            DialogueSequence postalDone = Dialogue("mina_postal_entrega", new[]
            {
                L(Mina, "¿Una postal? …Es su letra. «Para mi chispa: hoy el mar estaba de buen humor.»"),
                L(Mina, "Me llamaba así. Chispa. Por eso Brasa se llama Brasa."),
                L(Ilse, "Teo la guardó todo este tiempo."),
                L(Mina, "Dale las gracias. No… mejor se las doy yo. Mañana. Si hay mañana.")
            });
            n.TeoPostal = QuestAsset("Q_Teo_Postal", q =>
            {
                q.questId = "teo_postal";
                q.giverNpcId = "teo";
                q.title = "Una postal para Mina";
                q.description = "Teo encontró una postal de la abuela de Mina. Recógela en su puesto y llévasela a Mina.";
                q.type = QuestType.Entrega;
                q.shortInstruction = "Recoge la postal del puesto de Teo y llévasela a Mina.";
                q.deliverItemId = "postal_abuela";
                q.deliverItemName = "la postal";
                q.deliverToNpcId = "mina";
                q.prerequisite = n.TeoUltimasVelas;
                q.rewardEnergy = 15f;
                q.rewardHope = 10f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = postalOffer;
                q.inProgressDialogue = postalProgress;
                q.completedDialogue = postalDone;
            });

            // ============================================================ Teo · Regalo del Farol grande
            n.TeoGift = Dialogue("teo_regalo_farol", new[]
            {
                L(Teo, "Espera, no te vayas todavía. Tengo algo para ti."),
                L(Teo, "Era del farol de mi padre. El cristal grande, el de las noches de temporal."),
                L(Teo, "Yo ya no salgo a alumbrar a nadie. Pero tú sí."),
                L(Ilse, "Pesa… y brilla distinto."),
                L(Teo, "Así se ve más lejos. Úsalo cuando la noche apriete.")
            });

            // ============================================================ Diario de Ilse (3 de 12)
            AscuaAssetFactory.EnsureFolder(PagesFolder);
            n.Pages = new[]
            {
                Page("pagina_01", 1, "Lo primero que recuerdo",
                    "No recuerdo mi cara. Recuerdo el peso del farol y que alguien me dijo: «mientras alumbres, no te vas del todo».\n\nEscribo esto para no olvidarlo también."),
                Page("pagina_02", 2, "El callejón de los faroleros",
                    "Por aquí pasaban los faroleros con sus pértigas. Dejaban una marca de hollín en cada esquina, como quien firma.\n\nTodavía se ven, si acercas la luz. Alguien estuvo aquí antes que yo, haciendo lo mismo."),
                Page("pagina_03", 3, "Desde arriba",
                    "Desde el tejado del quiosco se ve toda la bahía. Las luces de Vesper parecen pocas desde aquí.\n\nPero no están todas apagadas. Todavía no.")
            };

            // ============================================================ Heraldo · revelaciones (enfrentamientos)
            // No son amenazas: son pistas. El Heraldo sabe quién era Ilse, y lo dice a medias.
            n.HeraldoRevelations = new[]
            {
                Dialogue("heraldo_revela_1", new[]
                {
                    L(Heraldo, "Tu luz quema, pequeña luz. Como la suya."),
                    L(Heraldo, "Él también encendía faroles en esta ciudad. Y también acabó cansándose."),
                    L(Ilse, "¿Quién? ¿De quién hablas?")
                }),
                Dialogue("heraldo_revela_2", new[]
                {
                    L(Heraldo, "No vengo a apagarte. Vengo a que descanses."),
                    L(Heraldo, "Todo lo que se olvida deja de doler. ¿No te lo prometieron así?"),
                    L(Ilse, "Nadie me prometió nada. Recuerdo un farol… y una voz.")
                }),
                Dialogue("heraldo_revela_3", new[]
                {
                    L(Heraldo, "La voz que recuerdas te pidió que alumbraras. No te dijo hasta cuándo."),
                    L(Heraldo, "Cuando la ciudad entera te olvide, ¿quién quedará para recordarte a ti?"),
                    L(Ilse, "Mina. Teo. Las ventanas que encendí. Eso queda.")
                })
            };

            // ============================================================ Actualización suave de datos existentes
            // Solo rellena campos vacíos: nunca pisa lo que hayas escrito a mano.
            FillInstruction(n.MinaBrasa, "Recoge 3 luciérnagas en La Plaza. Se esconden donde no hay luz.");
            FillInstruction(n.MinaMalecon, "Camina junto a Mina hasta el malecón. No te alejes de ella.");
            FillInstruction(n.TeoVelas, "Mantén El Mercado con Esperanza 55 o más durante 20 segundos.");
            FillInstruction(n.TeoUltimasVelas, "Habla con Teo y elige a dónde van sus velas.");
            FillInstruction(n.HeraldoTrato, "Escucha la oferta del Heraldo… o aléjate de él.");

            // ============================================================ Noche 1 (el "nivel")
            n.Night1 = Load<NightQuestSet>(Night1Path, set =>
            {
                set.setId = "noche_01";
                set.nightNumber = 1;
                set.title = "Noche 1 · Lo que todavía brilla";
                set.designNotes =
                    "Primera noche de prueba. Presenta los 4 tipos de misión y el primer Trato.\n" +
                    "Tensión: La Plaza (Mina) y El Mercado (Teo) piden cosas a la vez; el Heraldo ofrece un atajo que castiga al Mercado.\n" +
                    "Cadena: Brasa -> Malecón (Mina) y Velas -> Últimas velas (Teo).";
                set.quests = new List<Quest> { n.MinaBrasa, n.MinaMalecon, n.TeoVelas, n.TeoUltimasVelas, n.HeraldoTrato };
                set.nightDuration = 240f;
                set.startHope = 70f;
                set.hopeDecayPerSecond = 0.6f;
                set.hopeRisePerSecond = 4f;
                set.penumbraStartRadius = 60f;
                set.penumbraEndRadius = 16f;
                set.fastPenumbraCurve = false;
                set.fireflySpawnInterval = 5f;
                set.fireflyMaxActive = 5;
                set.heraldoEnabled = true;
                set.heraldoAppearAt = 0.25f;
                set.heraldoChaseSpeed = 1.3f;
            });

            // Si el set ya existía, se le añade la misión nueva (sin tocar el resto).
            if (!n.Night1.quests.Contains(n.TeoPostal))
            {
                n.Night1.quests.Add(n.TeoPostal);
                EditorUtility.SetDirty(n.Night1);
            }

            // Oleadas de sombras de la Noche 1 (solo si el set aún no tiene ninguna).
            if (n.Night1.waves == null || n.Night1.waves.Count == 0)
            {
                n.Night1.waves = new List<CombatWave>
                {
                    // 1ª: introduce el combate con lo más legible (Polillas) y el Eco que se divide.
                    new CombatWave { label = "Polillas en La Plaza", atProgress = 0.32f, zoneId = "Plaza", moths = 2, echoes = 1 },
                    // 2ª: más variada (embestida telegrafiada, robo de páginas, sombra velada).
                    new CombatWave { label = "La Sombra Mayor del Mercado", atProgress = 0.66f, zoneId = "Mercado", moths = 1, brutes = 1, whispers = 1, veiledBrutes = 1, echoes = 0 }
                };
                EditorUtility.SetDirty(n.Night1);
            }

            return n;
        }

        private static void FillInstruction(Quest quest, string instruction)
        {
            if (quest == null || !string.IsNullOrEmpty(quest.shortInstruction)) return;
            quest.shortInstruction = instruction;
            EditorUtility.SetDirty(quest);
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

        // ------------------------------------------------------------------ Utilidades

        private static DialogueLine L(string speaker, string text) => new DialogueLine(speaker, text);

        private static DialogueChoice Accept(string label) => new DialogueChoice(label, ChoiceAction.AceptarMision);

        private static DialogueChoice Decline(string label) => new DialogueChoice(label, ChoiceAction.RechazarMision);

        private static DialogueSequence Dialogue(string id, DialogueLine[] lines, params DialogueChoice[] choices)
        {
            return Load<DialogueSequence>($"{DialoguesFolder}/D_{id}.asset", d =>
            {
                d.dialogueId = id;
                d.lines = new List<DialogueLine>(lines);
                d.choices = new List<DialogueChoice>(choices);
            });
        }

        private static Quest QuestAsset(string fileName, System.Action<Quest> fill)
        {
            return Load<Quest>($"{QuestsFolder}/{fileName}.asset", fill);
        }

        /// <summary>Carga el asset si existe (sin tocarlo); si no, lo crea y lo rellena.</summary>
        private static T Load<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
