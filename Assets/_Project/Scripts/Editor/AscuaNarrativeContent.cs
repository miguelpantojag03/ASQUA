using System.Collections.Generic;
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

            return n;
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
