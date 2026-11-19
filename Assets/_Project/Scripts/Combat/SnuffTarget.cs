using System.Collections.Generic;
using Ascua.World;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Una luz que el Apagavelas puede apagar: una farola (StreetLampToggle) o una vela de Teo (PickupItem Vela).
    /// La lista se reúne una vez al empezar la noche (Collect) e incluye las velas inactivas del pool,
    /// que solo cuentan como blanco mientras están en el suelo.
    /// </summary>
    public class SnuffTarget
    {
        private static readonly List<SnuffTarget> all = new List<SnuffTarget>();

        public readonly StreetLampToggle Lamp;
        public readonly PickupItem Candle;
        /// <summary>Orden fijo en la lista: desempate determinista.</summary>
        public readonly int Index;
        private readonly Transform lightPoint;

        /// <summary>Apagavelas que ya va a por esta luz (para no ir dos a la misma).</summary>
        public Apagavelas ClaimedBy;

        public static IReadOnlyList<SnuffTarget> All => all;

        private SnuffTarget(StreetLampToggle lamp, PickupItem candle, int index)
        {
            Lamp = lamp;
            Candle = candle;
            Index = index;
            if (lamp != null)
            {
                Light light = lamp.GetComponentInChildren<Light>(true);
                lightPoint = light != null ? light.transform : null;
            }
        }

        public bool IsLamp => Lamp != null;
        public bool Exists => Lamp != null || Candle != null;

        /// <summary>Encendida ahora (una farola encendida y sin bloquear, o una vela en el suelo).</summary>
        public bool IsLit
        {
            get
            {
                if (Lamp != null) return Lamp.isActiveAndEnabled && Lamp.IsOn && !Lamp.IsLocked;
                return Candle != null && Candle.gameObject.activeInHierarchy && Candle.IsAvailable;
            }
        }

        /// <summary>Base de la luz (en el suelo).</summary>
        public Vector3 BasePosition
        {
            get
            {
                if (Lamp != null) return Lamp.transform.position;
                return Candle != null ? Candle.transform.position : Vector3.zero;
            }
        }

        /// <summary>Donde está la llama o el cristal (ahí sopla el Apagavelas).</summary>
        public Vector3 LightPosition
        {
            get
            {
                if (lightPoint != null) return lightPoint.position;
                if (Lamp != null) return Lamp.transform.position + Vector3.up * 3f;
                return BasePosition + Vector3.up * 0.3f;
            }
        }

        /// <summary>Zona a la que pertenece (la de la farola; la vela, la indicada o la más cercana).</summary>
        public ZoneHope Zone
        {
            get
            {
                if (Lamp != null) return ZoneHope.Find(Lamp.ZoneId);
                if (Candle != null && !string.IsNullOrEmpty(Candle.HopeZoneId))
                {
                    ZoneHope zone = ZoneHope.Find(Candle.HopeZoneId);
                    if (zone != null) return zone;
                }
                return NearestZone(BasePosition);
            }
        }

        /// <summary>Apaga la luz: la farola se apaga y se bloquea (reencendible con el Farol); la vela desaparece.</summary>
        public void Snuff()
        {
            if (Lamp != null)
            {
                Lamp.TurnOff();
                Lamp.SetLocked(true);
                // Se puede deshacer: el Farol de Ilse la reenciende si la ilumina 1.2 s.
                RelightOnLantern.Attach(Lamp);
            }
            else if (Candle != null)
            {
                Candle.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ lista de la noche

        /// <summary>Reúne todas las farolas y velas de Teo de la escena (también las velas inactivas).</summary>
        public static void Collect()
        {
            all.Clear();
            StreetLampToggle[] lamps = Object.FindObjectsByType<StreetLampToggle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // Orden estable por posición: misma escena = mismos índices (desempates deterministas).
            System.Array.Sort(lamps, ComparePositions);
            foreach (StreetLampToggle lamp in lamps) all.Add(new SnuffTarget(lamp, null, all.Count));

            PickupItem[] items = Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(items, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (PickupItem item in items)
            {
                if (item.Kind == PickupKind.Vela) all.Add(new SnuffTarget(null, item, all.Count));
            }
        }

        /// <summary>La luz que este Apagavelas debería ir a apagar (o null si no queda ninguna).</summary>
        public static SnuffTarget PickBest(Vector3 from, Apagavelas who)
        {
            if (all.Count == 0) Collect();
            SnuffTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                SnuffTarget t = all[i];
                if (!t.Exists || !t.IsLit) continue;
                if (t.ClaimedBy != null && t.ClaimedBy != who && t.ClaimedBy.IsAlive) continue;
                float d = (t.BasePosition - from).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = t;
                }
            }
            return best;
        }

        private static ZoneHope NearestZone(Vector3 position)
        {
            ZoneHope best = null;
            float bestDistance = float.MaxValue;
            var zones = ZoneHope.All;
            for (int i = 0; i < zones.Count; i++)
            {
                float d = (zones[i].Center - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = zones[i];
                }
            }
            return best;
        }

        private static int ComparePositions(StreetLampToggle a, StreetLampToggle b)
        {
            Vector3 pa = a.transform.position, pb = b.transform.position;
            int byX = pa.x.CompareTo(pb.x);
            return byX != 0 ? byX : pa.z.CompareTo(pb.z);
        }
    }
}
