using System.Collections.Generic;
using Ascua.Combat;
using Ascua.Narrative;
using Ascua.Quests;
using UnityEditor;
using UnityEngine;
using static Ascua.EditorTools.AscuaNarrativeContent;

namespace Ascua.EditorTools
{
    /// <summary>Assets de las noches 2-4 (Parte C).</summary>
    public class NightAssets
    {
        public NightQuestSet Night2, Night3, Night4;
        public DialogueSequence PerpetuaIdle;
    }

    /// <summary>
    /// Noches 2, 3 y 4: misiones más difíciles pero explicadas (doble objetivo, recurso limitado y
    /// una elección cuya consecuencia llega dos noches después). Personaje nuevo: Perpetua, una anciana
    /// de la Casa de ancianos que baja al mercado aunque se le haga de noche.
    /// Como el resto del contenido narrativo, solo se crea si no existe (no pisa tus ediciones).
    /// </summary>
    public static class AscuaNarrativeNights
    {
        private const string Ilse = "Ilse";
        private const string Mina = "Mina";
        private const string Teo = "Teo";
        private const string Perpetua = "Perpetua";

        public const string FlagVelasMercado = "velas_mercado";
        public const string FlagVelasCasa = "velas_casa";

        public static NightAssets CreateAll()
        {
            NightAssets a = new NightAssets();

            a.PerpetuaIdle = Dialogue("perpetua_ambiente", new[]
            {
                L(Perpetua, "Las noches de antes eran igual de oscuras, ¿sabes? Pero había más ventanas encendidas."),
                L(Perpetua, "Mi Ramón decía que la oscuridad no da miedo. Lo que da miedo es estar solo en ella.")
            });

            a.Night2 = CreateNight2();
            a.Night3 = CreateNight3();
            a.Night4 = CreateNight4();
            return a;
        }

        // ================================================================== Noche 2 · Lo que se guarda

        private static NightQuestSet CreateNight2()
        {
            // --- Teo · Dos frentes (doble objetivo)
            Quest dosFrentes = QuestAsset("Q_Teo_DosFrentes", q =>
            {
                q.questId = "teo_dos_frentes";
                q.giverNpcId = "teo";
                q.title = "Dos frentes";
                q.description = "Mantén El Mercado sobre 45 de Esperanza mientras disuelves las 3 Polillas que acosan La Plaza.";
                q.type = QuestType.DobleObjetivo;
                q.shortInstruction = "Mantén El Mercado ≥ 45 mientras disuelves las Polillas de La Plaza.";
                q.targetZoneId = "Mercado";
                q.hopeThreshold = 45f;
                q.defeatZoneId = "Plaza";
                q.defeatMoths = 3;
                q.defeatEchoes = 0;
                q.timeLimit = 50f;
                q.holdGraceSeconds = 2f;
                q.failIfTargetZoneDies = true;
                q.rewardEnergy = 15f;
                q.rewardHope = 10f;
                q.rewardHopeZoneId = "Mercado";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("teo_dos_frentes_oferta", new[]
                {
                    L(Teo, "¿Las ves? Polillas. Han vuelto a la plaza, y Mina está sola allí."),
                    L(Teo, "Pero si te vas, mi mercado se queda a oscuras. Y si el mercado se apaga, ya no hay adonde volver."),
                    L(Ilse, "¿Y si hago las dos cosas?"),
                    L(Teo, "Entonces hazlas a la vez. Enciende antes un par de puestos y luego corre. Yo cuento los segundos.")
                }, Accept("Las dos cosas a la vez"), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("teo_dos_frentes_progreso", new[]
                {
                    L(Teo, "¡La plaza! Y no te olvides de mirar atrás: mis farolillos se cansan muy rápido.")
                });
                q.completedDialogue = Dialogue("teo_dos_frentes_hecho", new[]
                {
                    L(Teo, "Lo has hecho. Las dos luces a la vez."),
                    L(Teo, "Mi padre decía que un farolero no elige calles: las alumbra todas, aunque sea un poco cada una.")
                });
            });

            // --- Perpetua · El camino de vuelta (escolta con energía limitada)
            Quest camino = QuestAsset("Q_Perpetua_Camino", q =>
            {
                q.questId = "perpetua_camino";
                q.giverNpcId = "perpetua";
                q.title = "El camino de Perpetua";
                q.description = "Acompaña a Perpetua desde El Mercado hasta la Casa de ancianos gastando 30 de Energía como máximo.";
                q.type = QuestType.Escolta;
                q.shortInstruction = "Acompaña a Perpetua a la Casa de ancianos sin gastar más de 30 de Energía.";
                q.escortNpcId = "perpetua";
                q.escortDestinationId = "casa_ancianos";
                q.escortDestinationName = "la Casa de ancianos";
                q.escortMaxDistance = 7f;
                q.escortGraceSeconds = 6f;
                q.energyBudget = 30f;
                q.failIfTargetZoneDies = false;
                q.rewardEnergy = 20f;
                q.rewardHope = 10f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("perpetua_camino_oferta", new[]
                {
                    L(Perpetua, "Ay, criatura. Bajé por pan y se me hizo de noche sin darme cuenta. Como siempre."),
                    L(Perpetua, "La Casa de ancianos está al otro lado de la plaza. Sola no llego: la oscuridad me confunde las calles."),
                    L(Perpetua, "Pero no gastes tu luz en mí, ¿eh? Con un poquito basta. Yo veo con poco."),
                    L(Ilse, "Te acompaño. Con poquito.")
                }, Accept("Vamos, despacio"), Decline("Espera un momento"));
                q.inProgressDialogue = Dialogue("perpetua_camino_progreso", new[]
                {
                    L(Perpetua, "Despacio, criatura. A mi edad cada adoquín es una montaña."),
                    L(Perpetua, "Y no enciendas tanto ese farol, que me deslumbras y te cansas.")
                });
                q.completedDialogue = Dialogue("perpetua_camino_hecho", new[]
                {
                    L(Perpetua, "Ya estamos. Mira mi ventana: la de las cortinas de flores."),
                    L(Perpetua, "Mi Ramón también salía de noche con la luz por delante. Tenía tu misma manera de andar."),
                    L(Ilse, "¿Era farolero?"),
                    L(Perpetua, "Era muchas cosas, criatura. Pero sí… también eso.")
                });
            });

            // --- Teo · Las velas de reserva (elección con consecuencia en la Noche 4)
            DialogueChoice keep = new DialogueChoice("Guárdalas en el Mercado", ChoiceAction.CompletarMision)
            {
                setFlag = FlagVelasMercado,
                hopeDelta = 5f,
                hopeZoneId = "Mercado",
                toast = "Teo guarda las velas de reserva en el Mercado. Algún día harán falta."
            };
            DialogueChoice give = new DialogueChoice("Llévalas a la Casa de ancianos", ChoiceAction.CompletarMision)
            {
                setFlag = FlagVelasCasa,
                hopeDelta = 5f,
                hopeZoneId = "Plaza",
                toast = "Las velas de reserva irán a las ventanas de la Casa de ancianos."
            };
            Quest velas = QuestAsset("Q_Teo_VelasReserva", q =>
            {
                q.questId = "teo_velas_reserva";
                q.giverNpcId = "teo";
                q.title = "Las velas de reserva";
                q.description = "Teo no sabe qué hacer con sus últimas velas buenas. La decisión es tuya, y se notará más adelante.";
                q.type = QuestType.Dialogo;
                q.shortInstruction = "Habla con Teo y decide adónde van sus velas de reserva.";
                q.prerequisite = dosFrentes;
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("teo_velas_reserva_oferta", new[]
                {
                    L(Teo, "Me quedan seis velas de reserva. Las buenas, las de cera de verdad."),
                    L(Teo, "Podría guardarlas aquí, en el mercado, para cuando la noche apriete de verdad."),
                    L(Teo, "O podrías llevárselas a la Casa de ancianos. Perpetua y los demás pasan las noches a oscuras y no se quejan nunca."),
                    L(Teo, "Decide tú. Yo ya no sé qué es lo justo.")
                }, keep, give);
                q.completedDialogue = Dialogue("teo_velas_reserva_hecho", new[]
                {
                    L(Teo, "Hecho, entonces. Ojalá no tengamos que acordarnos de esta conversación.")
                });
            });

            return Night("Noche_02_LoQueSeGuarda", 2, "Noche 2 · Lo que se guarda",
                "Dificultad: doble objetivo (Mercado + Plaza), primera escolta con energía limitada y la decisión de las velas (consecuencia en la Noche 4).",
                new List<Quest> { dosFrentes, camino, velas },
                260f, 65f, 0.7f, 0.5f, 1.4f,
                new List<CombatWave>
                {
                    new CombatWave { label = "Polillas sobre la fuente", atProgress = 0.3f, zoneId = "Plaza", moths = 3, echoes = 1 },
                    new CombatWave { label = "Robo en el Mercado", atProgress = 0.62f, zoneId = "Mercado", moths = 2, brutes = 1, whispers = 1, echoes = 0 }
                });
        }

        // ================================================================== Noche 3 · Viento del norte

        private static NightQuestSet CreateNight3()
        {
            Quest ningunaSeApaga = QuestAsset("Q_Mina_NingunaSeApaga", q =>
            {
                q.questId = "mina_ninguna_se_apaga";
                q.giverNpcId = "mina";
                q.title = "Ninguna se apaga";
                q.description = "Mantén La Plaza sobre 55 de Esperanza mientras disuelves las sombras que han entrado en El Mercado.";
                q.type = QuestType.DobleObjetivo;
                q.shortInstruction = "Mantén La Plaza ≥ 55 mientras disuelves las sombras del Mercado.";
                q.targetZoneId = "Plaza";
                q.hopeThreshold = 55f;
                q.defeatZoneId = "Mercado";
                q.defeatMoths = 3;
                q.defeatEchoes = 1;
                q.timeLimit = 55f;
                q.holdGraceSeconds = 2f;
                q.rewardEnergy = 15f;
                q.rewardHope = 10f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("mina_ninguna_oferta", new[]
                {
                    L(Mina, "El viento ha apagado dos farolas de la plaza. Si cae otra, ya no veré la fuente desde mi ventana."),
                    L(Mina, "Y Teo… he oído ruido en el mercado. Sombras."),
                    L(Mina, "No puedo pedirte que elijas. Así que no elijas, ¿vale?")
                }, Accept("No elijo: las dos"), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("mina_ninguna_progreso", new[]
                {
                    L(Mina, "¡La fuente sigue ahí! Corre, yo vigilo desde aquí.")
                });
                q.completedDialogue = Dialogue("mina_ninguna_hecho", new[]
                {
                    L(Mina, "Ninguna se ha apagado. Ninguna."),
                    L(Mina, "Mi padre decía que eso era lo difícil: no salvar una luz, sino no dejar caer ninguna.")
                });
            });

            Quest otraVez = QuestAsset("Q_Perpetua_OtraVez", q =>
            {
                q.questId = "perpetua_otra_vez";
                q.giverNpcId = "perpetua";
                q.title = "Otra vez de noche";
                q.description = "Perpetua ha vuelto a bajar. Acompáñala a casa con solo 22 de Energía: el viento lo pone más difícil.";
                q.type = QuestType.Escolta;
                q.shortInstruction = "Acompaña a Perpetua a la Casa de ancianos sin gastar más de 22 de Energía.";
                q.escortNpcId = "perpetua";
                q.escortDestinationId = "casa_ancianos";
                q.escortDestinationName = "la Casa de ancianos";
                q.escortMaxDistance = 7f;
                q.escortGraceSeconds = 5f;
                q.energyBudget = 22f;
                q.failIfTargetZoneDies = false;
                q.rewardEnergy = 20f;
                q.rewardHope = 12f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("perpetua_otra_vez_oferta", new[]
                {
                    L(Perpetua, "No me mires así. Ya sé que he vuelto a bajar. El pan no se compra solo."),
                    L(Perpetua, "Hoy sopla fuerte. Tu luz se va a cansar antes, criatura. Úsala solo cuando haga falta.")
                }, Accept("Te llevo a casa"), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("perpetua_otra_vez_progreso", new[]
                {
                    L(Perpetua, "Agárrate el farol, que este viento se lleva hasta los recuerdos.")
                });
                q.completedDialogue = Dialogue("perpetua_otra_vez_hecho", new[]
                {
                    L(Perpetua, "Gracias, criatura. Mañana no bajo. …Bueno. Ya veremos.")
                });
            });

            return Night("Noche_03_VientoDelNorte", 3, "Noche 3 · Viento del norte",
                "Más presión: doble objetivo inverso (Plaza + Mercado), escolta con menos margen de energía y oleadas con Sombra velada.",
                new List<Quest> { ningunaSeApaga, otraVez },
                270f, 62f, 0.8f, 0.45f, 1.5f,
                new List<CombatWave>
                {
                    new CombatWave { label = "El viento trae Susurros", atProgress = 0.28f, zoneId = "Mercado", moths = 2, whispers = 1, echoes = 1 },
                    new CombatWave { label = "Sombras veladas en la fuente", atProgress = 0.6f, zoneId = "Plaza", moths = 2, brutes = 1, veiledBrutes = 1, echoes = 0 }
                });
        }

        // ================================================================== Noche 4 · La vigilia

        private static NightQuestSet CreateNight4()
        {
            Quest vigilia = QuestAsset("Q_Teo_Vigilia", q =>
            {
                q.questId = "teo_vigilia";
                q.giverNpcId = "teo";
                q.title = "La vigilia del Mercado";
                q.description = "Mantén El Mercado con Esperanza ≥ 60 durante 35 segundos. Lo que decidiste en la Noche 2 cambia las condiciones.";
                q.type = QuestType.RescateEsperanza;
                q.shortInstruction = "Mantén El Mercado con Esperanza suficiente hasta que pase lo peor.";
                q.targetZoneId = "Mercado";
                q.hopeThreshold = 60f;
                q.holdSeconds = 35f;
                q.rewardEnergy = 20f;
                q.rewardHope = 15f;
                q.rewardHopeZoneId = "Mercado";
                q.rewardBond = 1;
                q.conditions = new List<QuestCondition>
                {
                    new QuestCondition
                    {
                        flag = FlagVelasMercado,
                        thresholdDelta = -10f,
                        startHopeBonus = 15f,
                        note = "En la Noche 2 dejaste las velas de reserva en el Mercado: hoy arden en los puestos. El Mercado empieza con más luz y basta con sostenerlo en 50."
                    },
                    new QuestCondition
                    {
                        flag = FlagVelasCasa,
                        holdSecondsDelta = -12f,
                        note = "En la Noche 2 llevaste las velas a la Casa de ancianos: Perpetua y los demás las han puesto en las ventanas que dan al Mercado. Basta con aguantar 23 s."
                    }
                };
                q.offerDialogue = Dialogue("teo_vigilia_oferta", new[]
                {
                    L(Teo, "Esta noche no voy a cerrar. Si el mercado aguanta encendido hasta que pase lo peor, aguantará siempre."),
                    L(Teo, "Lo dice la gente mayor. Y yo ya soy gente mayor.")
                }, Accept("Aguantaremos"), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("teo_vigilia_progreso", new[]
                {
                    L(Teo, "No mires el reloj. Mira los puestos. Mientras haya uno encendido, seguimos.")
                });
                q.completedDialogue = Dialogue("teo_vigilia_hecho", new[]
                {
                    L(Teo, "Ya está. Ha pasado lo peor."),
                    L(Teo, "Cuarenta años abriendo este mercado, y es la primera vez que no lo he abierto solo.")
                });
            });

            Quest dosLuces = QuestAsset("Q_Mina_DosLuces", q =>
            {
                q.questId = "mina_dos_luces";
                q.giverNpcId = "mina";
                q.title = "Dos luces, una noche";
                q.description = "Mantén La Plaza sobre 55 mientras disuelves las 5 sombras del Mercado en 60 segundos.";
                q.type = QuestType.DobleObjetivo;
                q.shortInstruction = "Mantén La Plaza ≥ 55 mientras disuelves las 5 sombras del Mercado.";
                q.targetZoneId = "Plaza";
                q.hopeThreshold = 55f;
                q.defeatZoneId = "Mercado";
                q.defeatMoths = 4;
                q.defeatEchoes = 1;
                q.timeLimit = 60f;
                q.holdGraceSeconds = 2f;
                q.rewardEnergy = 20f;
                q.rewardHope = 12f;
                q.rewardHopeZoneId = "Plaza";
                q.rewardBond = 1;
                q.offerDialogue = Dialogue("mina_dos_luces_oferta", new[]
                {
                    L(Mina, "Esta noche hay más sombras que nunca. Y todas quieren lo mismo: que nos olvidemos unos de otros."),
                    L(Mina, "Yo cuido la fuente. Tú cuida que no tenga que cuidarla sola.")
                }, Accept("Hasta el final"), Decline("Ahora no"));
                q.inProgressDialogue = Dialogue("mina_dos_luces_progreso", new[]
                {
                    L(Mina, "¡Sigo aquí! ¿Y tú?")
                });
                q.completedDialogue = Dialogue("mina_dos_luces_hecho", new[]
                {
                    L(Mina, "Las dos luces. Una noche entera."),
                    L(Mina, "Creo que ya no me da miedo la oscuridad. Me da miedo que te vayas.")
                });
            });

            return Night("Noche_04_LaVigilia", 4, "Noche 4 · La vigilia",
                "Cierre del primer arco: la vigilia del Mercado cambia según la decisión de la Noche 2; doble objetivo más exigente; oleadas de 4 criaturas.",
                new List<Quest> { vigilia, dosLuces },
                280f, 60f, 0.9f, 0.5f, 1.6f,
                new List<CombatWave>
                {
                    new CombatWave { label = "La última vigilia", atProgress = 0.25f, zoneId = "Plaza", moths = 3, echoes = 1 },
                    new CombatWave { label = "Todas las sombras", atProgress = 0.55f, zoneId = "Mercado", moths = 1, brutes = 1, veiledBrutes = 1, whispers = 1, echoes = 0 }
                });
        }

        // ================================================================== Utilidades

        private static NightQuestSet Night(string file, int number, string title, string notes, List<Quest> quests,
            float duration, float startHope, float decay, float surgeAt, float heraldoSpeed, List<CombatWave> waves)
        {
            return Load<NightQuestSet>($"{NightsFolder}/{file}.asset", set =>
            {
                set.setId = file.ToLowerInvariant();
                set.nightNumber = number;
                set.title = title;
                set.designNotes = notes;
                set.quests = quests;
                set.nightDuration = duration;
                set.startHope = startHope;
                set.hopeDecayPerSecond = decay;
                set.hopeRisePerSecond = 4f;
                set.penumbraStartRadius = 60f;
                set.penumbraEndRadius = 16f;
                set.fastPenumbraCurve = number >= 4;
                set.fireflySpawnInterval = 5f;
                set.fireflyMaxActive = 5;
                set.heraldoEnabled = true;
                set.heraldoAppearAt = Mathf.Max(0.15f, 0.3f - 0.05f * (number - 2));
                set.heraldoChaseSpeed = heraldoSpeed;
                set.surgeEnabled = true;
                set.surgeAt = surgeAt;
                set.surgeSeconds = 10f + 2f * (number - 2);
                set.waves = waves;
            });
        }
    }
}
