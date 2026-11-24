# IA de niveles · Ascua

**Qué hace:** a partir del ADN de la Noche 1 (`BASE_DNA`) genera las noches nuevas variando *solo parámetros*.
No genera geometría, prefabs ni código. Funciona 100 % sin internet.

## Cómo funciona

```
Noche 1 (NightQuestSet) ──> BASE_DNA ─┐
Perfil del jugador (PlayerProfileTracker) ─┤
                                           ├─> AIDirector ─> LevelValidator ─> QuestSetBuilder ─> NightQuestSet jugable
Banco de IA (Resources/LevelDNA/banco_ia.json) ─┘    (repara y anota)      (solo misiones existentes)
   o, si no hay: ProceduralFallback (reglas locales)
```

1. **ADN (`LevelDNA`)**: duración, Esperanza, Penumbra, luciérnagas, ráfaga, mezcla de misiones, oleadas, Heraldo, tono y textos.
2. **Fuente del ADN**
   - *Banco de IA*: variaciones de la Noche 1 que propuso una IA generativa **en tiempo de diseño** y quedaron como JSON. Cada una se usa una vez por partida.
   - *Reglas locales* (`ProceduralFallback`): sube o baja la dificultad según cómo le va al jugador, con una variación sembrada. Misma semilla = mismo nivel.
3. **`LevelValidator`**: todo ADN (de la IA o local) pasa por aquí. Repara con Clamp y **anota cada corrección** (queda en la consola y se cuenta en el panel). Nunca descarta en silencio.
4. **`QuestSetBuilder`**: convierte el ADN en un `NightQuestSet` con las misiones que ya existen. Las ya cumplidas se repiten como copias nuevas (solo Rescate, Recolección y Doble objetivo).
5. **Transparencia**: en el resumen de noche hay un sello ("Nivel generado a partir de tu Noche 1") y el botón **Ver ADN del nivel**, con una tabla *Noche 1 vs este nivel*.

## Cuándo se usa
- Noches 1-4: diseñadas a mano (el panel "Ver ADN" las compara con la Noche 1).
- **Noche 5 en adelante**: generadas por `AIDirector`. Cada noche se genera una vez, así que al reintentar se juega el mismo nivel.
- Se apaga desmarcando `generateNightsAfterAuthored` en el `NightManager`.

## Cómo probarlo
- **Rápido (con Unity):** juega, abre el panel de debug con `F1` y pulsa `F5` para saltar de noche. Al llegar a la Noche 5 se genera el nivel; al terminarla, el botón *Ver ADN del nivel*.
- **Pruebas automáticas:** `Window > General > Test Runner > EditMode` → `Ascua.LevelGen.Tests` (IA de niveles: validador, fallback determinista, base inmutable, etc.; además `EndingResolverTests` y `RecuerdoTests`).

## Archivos
| Ruta | Qué es |
|---|---|
| `Scripts/AI_LevelGen/Logica/` | Lógica pura sin Unity (`LevelDNA`, `LevelValidator`, `ProceduralFallback`, `LevelDnaRows`). Assembly `Ascua.LevelGen`. |
| `Scripts/AI_LevelGen/` | Pegamento con el juego (`AIDirector`, `QuestSetBuilder`, `PlayerProfileTracker`, `LevelDnaBank`, `LevelDnaConverter`). |
| `Resources/LevelDNA/banco_ia.json` | Banco de ADN propuestos por IA. |
| `Tests/EditMode/` | Pruebas del Test Runner. |

## Pendiente (opcional)
Llamada en vivo a una API de IA con `UnityWebRequest` (máx. 1 por noche, timeout corto, cae al banco/reglas locales). La clave **nunca** va en el repositorio.

---

# Recuerdos (mejoras entre noches)

Al terminar cada noche (si amanece y no es la última) el resumen ofrece **1 de 3 Recuerdos**, al azar entre los que el jugador **aún no eligió**. La oferta usa `System.Random` sembrado con el número de noche: al reintentar la misma noche sale la misma. Si quedan menos de 3 se ofrecen los que haya; si ya se eligieron los 7, se salta la elección y aparece directamente *Siguiente noche*.

| Recuerdo | `UpgradeType` | Efecto |
|---|---|---|
| Lupa de Mina | `RadioAmplio` | +15 % de alcance del Farol |
| Bata de Rueda | `Eficiencia` | −20 % de gasto de energía |
| Caja de música de Perpetua | `Regeneracion` | +35 % de regeneración de energía |
| Carrito de Teo | `CarritoTeo` | Las velas de Teo aparecen un 30 % más seguido (`CandleSpawner`) |
| Reloj de Nadir | `RelojNadir` | La Penumbra avanza un 15 % más lenta (`Penumbra`) |
| Libro de Amelia | `LibroAmelia` | Cada noche empieza con +5 de Esperanza en todas las zonas (`ZoneHope`) |
| Última vela | `UltimaVela` | Una vez por partida, la primera zona que fuera a apagarse se queda en 10 (`ZoneHope`) |

- Los 4 nuevos van **al final del enum**: los guardados escriben el enum como número y los viejos se leen igual.
- La Última vela gastada se guarda (`UpgradesData.ultimaVelaUsada`, dentro de `Snapshot`/`Restore`). Un guardado anterior sin ese campo la deja disponible.
- El Carrito de Teo solo se nota con la habilidad de velas de Teo activa (Vínculo), que es cuando `CandleSpawner` genera velas.
- No confundir con los Recuerdos de combate que sueltan las sombras (`GameProgress.recuerdos`: lupa y reloj que enseñan movimientos); son otro sistema.

**Archivos:** `Scripts/Player/Recuerdos/` (ensamblado puro `Ascua.Recuerdos`: `UpgradeType`, `RecuerdoRules`, `UpgradeLedger`, `UpgradesData`) y `Scripts/Player/PlayerUpgrades.cs` (envoltorio estático con los multiplicadores). Pruebas: `Tests/EditMode/RecuerdoTests.cs` (oferta sin repetidos y guardado que restaura los 7).

# Escena final

Al pulsar *Ver el final* en la última noche, `EndingScreen` reproduce primero `EndingSequence` (≈20 s, corrutina con tiempo sin escalar, sin Timeline) y después el título y el epílogo como antes.

1. Fundido a oscuro (1,5 s). Se ocultan el HUD y el resumen.
2. `Camera.main` (con el `CinemachineBrain` apagado) sube y se aleja despacio mirando al centro de las zonas; la niebla se aclara para ver la ciudad.
3. Velo de color por final: **Ceniza** gris frío · **Vigilia** azul · **Alba** ámbar · **Constelación** dorado · **El Trato** blanco vacío · **Farol Apagado** violeta suave.
4. Un **farolillo** (luz puntual) se enciende junto a cada NPC con **Vínculo ≥ 3**, uno tras otro.
5. **Constelación:** todas las ventanas se encienden doradas en cascada desde el centro (`WindowLight.SetEndingGlow`) y aparecen los nombres de **Mina, Teo y Perpetua** uno a uno.
6. Música con `AudioManager.SetEndingMusic(capaEsperanza, volumen)`: silencio en Ceniza y El Trato; capa de esperanza al máximo en Alba y Constelación; intermedia en Vigilia y Farol Apagado.
7. Fundido al fondo del epílogo.

- **Cualquier tecla** acelera (x4); otra pulsación **salta** la escena.
- **Reducir destellos** (accesibilidad): luces más tenues y que se encienden más despacio, y la aceleración baja a x2.

**Archivos:** `Scripts/UI/EndingSequence.cs` (la escena), `Scripts/UI/EndingScreen.cs` (la lanza), `Scripts/Audio/AudioManager.cs` y `Scripts/World/WindowLight.cs` (apoyos).

# Cacería del Heraldo

En momentos fijos de la noche el Heraldo deja de pasear y **corre** (4.8 m/s, casi como Ilse corriendo a 5.2) durante unos 15 s. Ilse tiene que llegar a un **refugio**. **Nunca hay Game Over.**

1. **Cuándo** (`HuntSchedule`, lógica pura en `Ascua.LevelGen`): momentos entre el 30 % y el 90 % de la noche, separados al menos un 15 %, sembrados por número de noche (al reintentar caen igual). Cantidad: noches 1-2 = 0, noche 3 = 1, noche 4+ = 2, salvo que el nivel fije `huntCount`.
2. **Solo empieza** con el Heraldo presente y sin diálogo, oleada, momento narrativo (`HeraldoOmen` / `EventBus.NarrativeMoment`) ni pausa. Si no se da, reintenta cada segundo hasta el 95 %.
3. **Aviso (4 s):** "Escucha… se acerca", `SFX_Amenaza` con subtítulo y el farol violeta parpadeando (con *reducir destellos*, sin parpadeo). Los refugios ya se ven.
4. **Cacería:** el Heraldo reaparece a ~18 m, del lado contrario al refugio más cercano. HUD con flecha (8 sentidos) y distancia al refugio, música en tensión máxima y subtítulo `[Pasos veloces]`. El Pulso solo lo frena 1.5 s; el dash sigue liberando de su abrazo.
   Con un diálogo abierto la cacería se congela (el Heraldo no avanza ni alcanza, el tiempo no corre), y los momentos narrativos (`NightEvent`, `HeraldoOmen`) esperan a que termine.
5. **Final:**
   - **Refugio** (2 s dentro del círculo): el Heraldo se detiene al borde. +20 de energía, +1 de Vínculo con ese NPC y "Mina te cubre con su luz".
   - **Te alcanza** (a 1.6 m): −25 de energía, −15 de Esperanza a la zona más cercana (nunca por debajo de 5), aviso corto y se retira 20 s.
   - **15 s sin alcanzarte:** pierde interés, sin recompensa.

**Refugios (`SafeHaven`):** se añaden por código a Mina, Teo y Perpetua (radio 4.5 m). Cuenta como refugio el NPC con Vínculo ≥ 1; si ninguno llega, el de mayor Vínculo (siempre hay uno). Solo se ven durante la cacería: anillo ámbar emisivo + luz cálida (malla y material creados una vez y compartidos).

**IA de niveles:** el ADN trae `huntCount` (−1 = automático, 0-3) y `huntSpeed` (3.5-6.0, por defecto 4.8); si faltan en el JSON valen eso. `ProceduralFallback` pone 3 cacerías si la noche es dura (`SignedHardness` > 0.6), ninguna si es fácil (< −0.2) y escala la velocidad de 4.2 a 5.4. Salen en *Ver ADN* como "Cacerías del Heraldo" y "Velocidad de la cacería".

**Probar:** `F1` y después `F6` lanza una cacería ahora (el panel muestra cuántas quedan y cuándo). Pruebas: `HuntScheduleTests` y las nuevas de `LevelGenTests`.

**Archivos:** `Scripts/Combat/HeraldoHuntDirector.cs`, `Scripts/World/SafeHaven.cs`, `Scripts/AI_LevelGen/Logica/HuntSchedule.cs`, `Scripts/Characters/Heraldo_AI.cs` (estado *Cazando*), `Scripts/Audio/AudioManager.cs` (`SetHuntTension`, `PlayHuntWarning`).

# Eventos por noche y Corvin

Cada noche diseñada tiene un momento memorable (handoff §8.6). Los de las noches 1 y 2 están en la escena (`NightEvent` de Brasa, `HeraldoOmen`). Los de las noches **3, 4 y 5** los crea por código `NightEventDirector`: `NightManager` lo crea en `Awake` según `GameProgress.CurrentNight`, sin tocar la escena.

- **Cuándo:** `NightEventRules` (lógica pura, `Ascua.LevelGen`) decide: solo en su noche, una vez por partida, cuando el progreso pasa su umbral y no hay diálogo, oleada, Cacería, pausa ni otro momento narrativo. Desde el **90 %** ya no ocurre. Las noches generadas posteriores (6+) no lo repiten.
- **Al reintentar la noche** el evento vuelve a ocurrir, igual que se deshacen sus decisiones (`GameProgress.MarkMomentSeenThisNight`).
- Al empezar y terminar publica `EventBus.RaiseNarrativeMoment`: la música lo marca y la Cacería del Heraldo espera.

| Noche | Evento | Qué pasa |
|---|---|---|
| 3 (55 %) | **El tren fantasma** | Por el borde norte pasa en 14 s un tren de 8 ventanas de luz azul pálido. Silbido grave con subtítulo `[Un tren lejano]`. Aparecen 4 luciérnagas doradas en fila: recogerlas todas da la **página 7** del Diario ("Tuve un hijo… Y no puedo decir su nombre.") y energía. Ilse piensa: "Nadie bajó. Nadie subió. Aun así, el tren pasó." |
| 4 (70 %) | **El nombre** | El Heraldo se detiene a ~6 m, gira su farol dos veces y habla. Opciones: **"Corvin."** (marca `nombro_corvin`, +10 de energía, +1 de Vínculo con quien esté más cerca y su farol se vuelve **dorado** desde entonces), "No sé quién eres." o "Aléjate." |
| 5 (5 %) | **Los farolillos** | Cada personaje con **Vínculo ≥ 3** enciende su farolillo (luz cálida de 6 m y anillo ámbar). Dentro, la Esperanza de la zona más cercana sube +1.5/s. Los de Vínculo bajo no lo encienden. |

**Corvin y los finales**
- `EndingContext.NamedCorvin` lee la marca `nombro_corvin` (se guarda con las demás en `GameProgressData.flags`).
- `EndingResolver.RequireCorvinName = true`: **Constelación exige haber nombrado a Corvin**. Todo perfecto sin nombrarlo = **Alba**.
- `EndingResolver.TotalDiaryPages = 7`: el final secreto pide también la página del tren.

**Accesibilidad:** sin mover la cámara; todo con `Time.deltaTime` (la pausa lo congela); con *reducir destellos* las luces no titilan; el silbido tiene subtítulo.

**Probar:** `F1` y después `F7` dispara ya el evento de la noche actual (aunque ya se haya visto). Para llegar a la noche: `F5`. Pruebas: `NightEventRulesTests` y `EndingResolverTests`.

**Archivos:** `Scripts/Narrative/NightEventDirector*.cs` (uno por evento), `Scripts/Narrative/RuntimeDiaryPages.cs`, `Scripts/World/BondLantern.cs`, `Scripts/AI_LevelGen/Logica/NightEventRules.cs`, `Scripts/Characters/Heraldo_AI.cs` (`StopBefore`, `TwirlLantern`, `SetGoldenLantern`).

# Apagavelas

Sombra voladora que **no persigue a Ilse**: va a las luces (farolas y velas de Teo) y las apaga. Cada luz apagada quita **8 de Esperanza** a su zona. Obliga a repartirse por el mapa y a decidir qué defender.

1. **Ciclo:** elige la mejor luz encendida (`SnuffPlanner`: la zona con menos Esperanza y, luego, la más cercana; empate por índice) → vuela hasta ella (~1.8 m de altura, 2.2 m/s) → **inhala 1.5 s** (humo y subtítulo `[Susurro de humo]`) → la farola se apaga y se bloquea (la vela desaparece) → siguiente luz.
2. **Defenderse:**
   - El **Farol encendido a menos de 6 m** mientras inhala lo espanta: se interrumpe y retrocede 3 s.
   - Tiene 3 de vida: un barrido fuerte o dos normales lo deshacen (suelta 6 de energía).
   - No ataca a Ilse.
3. **Deshacerlo:** una farola apagada por un Apagavelas se reenciende si el Farol la ilumina **1.2 s seguidos** (`RelightOnLantern`): se desbloquea, se enciende, la zona recupera **+4** y hay una celebración corta. Si la zona muere antes, sigue apagada. Las velas no vuelven: el `CandleSpawner` saca otras.
4. **Avisos:** un aro que late sobre la luz que está apagando (fijo con *reducir destellos*), un pulso en el borde de la pantalla hacia ella (`ThreatIndicator.PointAt`) y, la primera vez en la partida, "Algo apaga las luces de [zona]...".

**Cuántos y cuándo (`SnuffDirector`, creado por `NightManager`):**
- Automático: noche 1 = 0, 2 = 1, 3 = 2, 4 = 3, 5+ = 4. O lo que diga el ADN (`snuffers`, -1 a 8).
- Aparecen entre el 15 % y el 85 % de la noche (reutiliza `HuntSchedule`), a ~16 m de Ilse, del lado de la zona con más farolas encendidas.
- Nunca durante una Cacería, un momento narrativo, un diálogo o la pausa (reintenta cada segundo hasta el 90 %).
- Usan su propio tag (`apagavelas`): **no cuentan como oleada** de `CombatWaveDirector`.
- Pool de 6 al empezar la noche, clonados de una Polilla de la escena (gris humo, un 30 % más grandes). Si la escena no tiene Polillas, esa noche no hay Apagavelas (aviso en consola).

**IA de niveles:** `ProceduralFallback` da `2 + hardness*4` Apagavelas (redondeado) y ninguno si la noche es fácil (`SignedHardness` < -0.2). En *Ver ADN*: fila "Apagavelas". Si falta en el banco de IA vale -1 (automático).

**Probar:** `F1` y después `F9` hace aparecer uno ya; el panel muestra cuántos hay activos y cuándo sale el siguiente. Pruebas: `SnuffPlannerTests` y las nuevas de `LevelGenTests`.

**Archivos:** `Scripts/Combat/Apagavelas.cs`, `SnuffDirector.cs`, `SnuffTarget.cs`, `Scripts/World/RelightOnLantern.cs`, `Scripts/AI_LevelGen/Logica/SnuffPlanner.cs`.

# Jefe final, HUD y tutorial

## La Marea (jefe final)

Al terminar las misiones de la **Noche 5** (la última de la campaña), o al acabarse su tiempo si no se terminan, sale **La Marea** en el centro de La Plaza, **antes** del resumen y del final. No sale en las noches posteriores ("Noches sin fin") ni si ya se derrotó en esta partida.

1. **Entrada:** el reloj de la noche se detiene y se espera a que no haya diálogo, oleada, Cacería, momento narrativo ni pausa. Fundido a negro: Ilse aparece a ~9 m del centro de La Plaza, con el Farol lleno, y La Marea sale de la niebla. Los Apagavelas se retiran y el Heraldo se aleja mientras dure el combate.
2. **La luz manda:** solo recibe daño completo si el Farol (o el Pulso) la ilumina (×4 el daño del golpe); si no, 1 de daño. La barra brilla cuando está *bajo la luz*. La primera vez que un golpe no hace casi nada sale el aviso "solo se deshace bajo la luz".
3. **Fases (por vida, `FinalBossRules`):**
   - **100-66 %:** golpe lento en área. Un aro coral marca el suelo y otro se llena hasta el impacto (1.5 s). Tras el golpe queda quieta 1.3 s.
   - **66-33 %:** además invoca **2 Polillas cada 12 s** (de las inactivas de la escena, con su propio tag: no son oleada; máximo 4 vivas).
   - **33-0 %:** además, cada 14 s, un **pulso** que apaga las farolas a menos de 16 m (`StreetLampToggle.TurnOff` + `SetLocked`, y `RelightOnLantern` para reencenderlas con el Farol). Camina y golpea más rápido.
   - Cada cambio de fase: rugido de 1.2 s con subtítulo.
4. **Si Ilse cae** (un golpe la deja a 0 de energía): fundido y **reintento desde el inicio del combate**, sin perder nada de la noche (misiones, Vínculos, páginas). A la **3ª caída** La Marea se retira a la niebla y la noche sigue al final sin premio.
5. **Si se vence:** pausa dramática de 2 s (un instante a cámara lenta), subtítulo "La Marea se deshace en luz. Vesper respira.", **+10 de Esperanza** a las zonas vivas y se guarda al momento que ya se venció. Después, el flujo normal: resumen → *Ver el final*.
6. **No cambia qué final sale:** el final lo sigue decidiendo `EndingResolver` con las zonas salvadas, los Vínculos, el Diario y los Tratos. Durante el combate las zonas no pierden Esperanza (no puede apagarse ninguna) y el premio solo suma a zonas vivas.

**Cómo se hace sin escena:** `NightManager` crea `FinalBossDirector` en `Awake`. El director clona una **Sombra Mayor** (también de las inactivas del pool; mejor una sin velo) dentro de un padre inactivo, le quita `ShadowBrute` (y `LitVulnerable`) y le pone `MareaBoss` (hereda de `ShadowCreature`): escala ×2.5 y tinte casi negro con `MaterialPropertyBlock`. `ShadowCreature` tiene un método nuevo, `AdjustDamage`, que por defecto devuelve el daño del golpe (las demás criaturas no cambian).

**Guardado:** `GameProgressData.bossDefeated` (los guardados viejos no lo tienen y se leen como `false`). `SaveSystem.MarkBossDefeated` lo escribe en la partida guardada al vencer, así *Continuar* no repite el combate.

**Convivencia:** con el reloj detenido no empiezan oleadas, Apagavelas, Cacerías ni eventos (`FinalBossDirector.HoldsNight`). Música de combate con el mismo evento que una oleada (`EventBus.RaiseCombatWave("marea", ...)`).

**Accesibilidad:** todo con `Time.deltaTime` (la pausa lo congela); con *reducir destellos* los aros no laten y los destellos se atenúan (`AccessibilitySettings.FlashScale`); los ataques y fases tienen subtítulo.

**Probar:** `F1` y después `F10` invoca a La Marea en cualquier noche (fuera de la Noche 5 no guarda nada ni termina la noche). Para llegar a la Noche 5: `F5`. El panel muestra estado, vida, fase, si está iluminada y las caídas. Pruebas: `FinalBossRulesTests`.

## HUD

- **Barra de La Marea** (`BossHealthBarUI`): arriba al centro, solo durante el combate. Nombre, 3 rombos de fase, marcas en el 66 % y el 33 %, y el relleno brilla bajo la luz. Estilo del `UIStyleKit` (`UIStyledPanel`, rol *Hud*).
- **Indicador compacto** (`NightCompactIndicator`): "Noche N · objetivo actual" bajo el reloj. No guarda datos: lee `NightManager` y `QuestManager.TrackedQuest` (lo mismo que `ObjectiveTrackerUI`) o la oleada activa. **H** (o la cruceta izquierda) lo oculta y se recuerda. Sigue la opacidad de la capa secundaria de `HudFocus`; se oculta en diálogos, Cacerías y el combate con La Marea (usan ese hueco).
- **Sin solapes:** ambos se crean con un lienzo 1920×1080 y mezcla 0.5, como el HUD de la escena, así a 1080p y a 720p (misma proporción) todo escala igual. Miden 470 px (498 con su filo) y empiezan en y −140/−142: quedan bajo el reloj *Noche* (hasta −128 con su filo), a la derecha del objetivo actual (hasta x 652) y a la izquierda de la Esperanza (desde x 1418).

## Tutorial

- **Noche 2** (solo la primera vez en la partida), mismo estilo de pensamientos de Ilse: esquivar (Ctrl / B) → la luz contra las sombras (golpear una) → ataque cargado (solo si ya tiene la Lupa de Mina) → contraataque (solo si ya tiene el Reloj de Nadir). Cada paso cuenta desde que aparece y **avanza al hacerlo, no al leer** (`TutorialSequence`).
- **Pensamientos de una vez:** la primera Cacería del Heraldo ("Me busca. Corro a un refugio de luz.") y el primer Apagavelas ("Algo apaga las luces. Con el Farol las vuelvo a encender."). Se marcan como momentos vistos de la partida.
- **Cómo jugar** (`HowToPlayPanel`): controles de teclado y de mando, desde la pausa (bajo *Accesibilidad*; *Salir al título* baja un hueco) y desde el título (cinco botones). Esc / B la cierran.
- **Saltar tutorial:** interruptor en *Cómo jugar*. Se guarda en `SaveData.skipTutorial` (fuera del progreso, como los finales: vale para todas las partidas; los guardados viejos lo leen como `false`). Activado a mitad de un capítulo, lo termina en ese momento.
- Pruebas: `TutorialSequenceTests` (avance de pasos, pasos no disponibles, saltar, capítulo por noche).

**Archivos:** `Scripts/Combat/MareaBoss.cs`, `FinalBossDirector.cs`, `Scripts/AI_LevelGen/Logica/FinalBossRules.cs`, `TutorialSequence.cs`, `Scripts/UI/BossHealthBarUI.cs`, `NightCompactIndicator.cs`, `HudBuild.cs`, `HowToPlayPanel.cs`, `Scripts/Quests/TutorialHints.cs`, `Tests/EditMode/FinalBossRulesTests.cs`, `TutorialSequenceTests.cs`.
