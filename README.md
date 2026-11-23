# Ascua

Juego en Unity 6 (6000.0.85f1). La documentación de la IA de niveles, los Recuerdos y la escena final está en [README_IA_NIVELES.md](README_IA_NIVELES.md).

## Pruebas

Hay **198 pruebas** (NUnit) sobre la lógica pura del juego, la que vive en los ensamblados sin referencias a Unity (`noEngineReferences`): `Ascua.LevelGen`, `Ascua.Endings` y `Ascua.Recuerdos`. Están en `Assets/_Project/Tests/EditMode/`.

### Qué cubren

| Archivo | Módulo |
|---|---|
| `LevelGenTests`, `LevelValidatorTests` | Validador del ADN de niveles: rangos, NaN, texto para TextMeshPro, tonos |
| `LevelDnaTests` | ADN: copia profunda y `SameAs` (protege que la base sea inmutable) |
| `ProceduralFallbackTests` | Generador local de niveles: dificultad, tono, entradas raras |
| `LevelDnaRowsTests` | Tabla "Ver ADN del nivel" |
| `HuntScheduleTests` | Momentos de las cacerías del Heraldo |
| `SnuffPlannerTests` | Cantidad, momentos y blanco de los Apagavelas |
| `NightEventRulesTests` | Cuándo empieza el evento de cada noche |
| `EndingResolverTests`, `EndingCatalogTests` | Qué final ocurre y sus textos |
| `RecuerdoTests`, `RecuerdoRulesTests`, `UpgradeLedgerTests` | Recuerdos: oferta, estado y guardado |

### Cómo correrlas

**Sin abrir Unity** (necesita el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)):

```bash
npm test        # o: dotnet test LogicTests/Ascua.Logic.Tests
npm run lint    # compila con los analizadores de .NET como errores
```

`LogicTests/` contiene proyectos `.csproj` que enlazan los mismos `.cs` del juego y de las pruebas, así que no hay código duplicado. Un pequeño sustituto de `JsonUtility` (`LogicTests/Ascua.Logic.Tests/UnityShims/`) permite correr las pruebas de guardado fuera del editor.

**Dentro de Unity:** `Window > General > Test Runner > EditMode`.

En cada push, GitHub Actions ejecuta el lint y las pruebas (`.github/workflows/tests.yml`).

### Qué no cubren

No hay pruebas PlayMode ni de interfaz: nada de escenas, `MonoBehaviour`, menús, pausa ni resoluciones. Tampoco compila el proyecto Unity completo; eso requiere el editor (o GameCI con una licencia de Unity).
