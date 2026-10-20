using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>Mejoras simples que se eligen al final de cada noche (1 de 3).</summary>
    public enum UpgradeType
    {
        /// <summary>+15% de radio del Farol en todos los tamaños.</summary>
        RadioAmplio,
        /// <summary>−20% de gasto de energía del Farol.</summary>
        Eficiencia,
        /// <summary>+35% de regeneración de energía.</summary>
        Regeneracion
    }

    /// <summary>
    /// Mejoras permanentes del Farol (sobreviven entre noches y al recargar la escena).
    /// LanternController y PlayerEnergy leen estos multiplicadores. También guarda el desbloqueo
    /// del "Farol grande" (regalo de Teo). En la Fase 4 los Recuerdos se apoyarán en este mismo sistema.
    /// </summary>
    public static class PlayerUpgrades
    {
        private static readonly List<UpgradeType> chosen = new List<UpgradeType>();

        /// <summary>Se dispara al elegir una mejora o desbloquear el Farol grande.</summary>
        public static event Action Changed;

        public static bool LargeLanternUnlocked { get; private set; }
        public static IReadOnlyList<UpgradeType> Chosen => chosen;

        public static float RadiusMultiplier => 1f + 0.15f * Count(UpgradeType.RadioAmplio);
        public static float DrainMultiplier => Mathf.Pow(0.8f, Count(UpgradeType.Eficiencia));
        public static float RegenMultiplier => 1f + 0.35f * Count(UpgradeType.Regeneracion);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            chosen.Clear();
            LargeLanternUnlocked = false;
            Changed = null;
        }

        /// <summary>Partida nueva (desde el título).</summary>
        public static void ResetAll()
        {
            chosen.Clear();
            LargeLanternUnlocked = false;
            Changed?.Invoke();
        }

        public static void Choose(UpgradeType upgrade)
        {
            chosen.Add(upgrade);
            Changed?.Invoke();
        }

        public static void UnlockLargeLantern()
        {
            if (LargeLanternUnlocked) return;
            LargeLanternUnlocked = true;
            Changed?.Invoke();
        }

        public static int Count(UpgradeType upgrade)
        {
            int n = 0;
            for (int i = 0; i < chosen.Count; i++) if (chosen[i] == upgrade) n++;
            return n;
        }

        /// <summary>Título y descripción para la pantalla de elección.</summary>
        public static (string title, string description) Describe(UpgradeType upgrade)
        {
            switch (upgrade)
            {
                case UpgradeType.RadioAmplio: return ("Halo amplio", "+15% de alcance del Farol en todos sus tamaños.");
                case UpgradeType.Eficiencia: return ("Mecha paciente", "El Farol gasta un 20% menos de energía.");
                default: return ("Brasa viva", "La energía se recupera un 35% más rápido.");
            }
        }
    }
}
