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
- **Pruebas automáticas:** `Window > General > Test Runner > EditMode` → `Ascua.LevelGen.Tests` (16 pruebas: validador, fallback determinista, base inmutable, etc.).

## Archivos
| Ruta | Qué es |
|---|---|
| `Scripts/AI_LevelGen/Logica/` | Lógica pura sin Unity (`LevelDNA`, `LevelValidator`, `ProceduralFallback`, `LevelDnaRows`). Assembly `Ascua.LevelGen`. |
| `Scripts/AI_LevelGen/` | Pegamento con el juego (`AIDirector`, `QuestSetBuilder`, `PlayerProfileTracker`, `LevelDnaBank`, `LevelDnaConverter`). |
| `Resources/LevelDNA/banco_ia.json` | Banco de ADN propuestos por IA. |
| `Tests/EditMode/` | Pruebas del Test Runner. |

## Pendiente (opcional)
Llamada en vivo a una API de IA con `UnityWebRequest` (máx. 1 por noche, timeout corto, cae al banco/reglas locales). La clave **nunca** va en el repositorio.
