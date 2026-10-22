using System.Collections.Generic;
using Ascua.Combat;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// UNA NOCHE = UN NIVEL. En Ascua no hay mapas distintos por nivel: el mapa (La Plaza, El Mercado...)
    /// es siempre el mismo y lo que define el reto y el ritmo de una noche es este asset:
    ///   1) QUÉ misiones hay (y en qué orden se encadenan, vía Quest.prerequisite),
    ///   2) con QUÉ dificultad (duración, Esperanza, Penumbra, luciérnagas, Heraldo).
    ///
    /// Diseñar una noche nueva = crear un NightQuestSet nuevo (Create > Ascua > Noche) y asignarlo al
    /// NightManager. No hay que tocar escenas ni código.
    ///
    /// FASE 4 (IA): el AIDirector generará o combinará NightQuestSets en tiempo de ejecución
    /// (ScriptableObject.CreateInstance + rellenar estos campos) a partir de las misiones existentes y
    /// del historial del jugador (Vínculos, zonas perdidas). Por eso todo lo que define una noche vive
    /// aquí, como datos planos y serializables (también exportables a JSON con JsonUtility).
    /// </summary>
    [CreateAssetMenu(fileName = "Noche_Nueva", menuName = "Ascua/Noche (set de misiones)", order = 2)]
    public class NightQuestSet : ScriptableObject
    {
        [Header("Identidad")]
        public string setId = "noche_nueva";
        public int nightNumber = 1;
        public string title = "Noche nueva";
        [TextArea(3, 6)]
        [Tooltip("Notas de diseño: qué tensión busca esta noche. También servirá de contexto para la IA.")]
        public string designNotes;

        [Header("Misiones de esta noche")]
        public List<Quest> quests = new List<Quest>();

        [Header("Dificultad · Noche")]
        [Tooltip("Duración en segundos (los diálogos pausan el reloj).")]
        public float nightDuration = 240f;

        [Header("Dificultad · Esperanza de las zonas")]
        [Range(0f, 100f)] public float startHope = 70f;
        public float hopeDecayPerSecond = 0.6f;
        public float hopeRisePerSecond = 4f;

        [Header("Dificultad · Penumbra")]
        public float penumbraStartRadius = 60f;
        public float penumbraEndRadius = 16f;
        [Tooltip("Curva casi lineal (entra pronto) en vez de la curva sutil (acelera al final).")]
        public bool fastPenumbraCurve = false;

        [Header("Dificultad · Luciérnagas")]
        public float fireflySpawnInterval = 5f;
        public int fireflyMaxActive = 5;

        [Header("Dificultad · Ráfaga de urgencia de la Penumbra")]
        public bool surgeEnabled = true;
        [Range(0f, 1f)] public float surgeAt = 0.55f;
        public float surgeSeconds = 10f;
        public float surgeMeters = 7f;
        public float surgeDecayMultiplier = 1.7f;

        [Header("Progresión entre noches")]
        [Tooltip("Cada noche que se repite este set, la caída de Esperanza sube este porcentaje.")]
        [Range(0f, 0.5f)] public float decayIncreasePerNight = 0.15f;

        [Header("Combate · oleadas de sombras (1-2 por noche)")]
        [Tooltip("Picos de intensidad: 3-4 criaturas que atacan una zona en un momento concreto.")]
        public List<CombatWave> waves = new List<CombatWave>();

        [Header("Dificultad · Heraldo")]
        public bool heraldoEnabled = true;
        [Tooltip("Momento de la noche (0-1) en que aparece.")]
        [Range(0f, 1f)] public float heraldoAppearAt = 0.25f;
        public float heraldoChaseSpeed = 1.3f;
    }
}
