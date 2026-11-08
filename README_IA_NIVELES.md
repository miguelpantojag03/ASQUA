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
