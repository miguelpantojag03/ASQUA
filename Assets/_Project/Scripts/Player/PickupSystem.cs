using System;
using System.Collections.Generic;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.UI;
using Ascua.World;
using UnityEngine;

namespace Ascua.Player
{
    /// <summary>Un objeto guardado en el inventario.</summary>
    public struct InventoryItem
    {
        public string Id;
        public string Name;
        public Color Color;
    }

    /// <summary>
    /// Recoger objetos: Ilse extiende la mano (estado "Recoger" del Animator), el objeto vuela hasta
    /// el Farol y estalla en chispas. Según su tipo, va al inventario (misiones "Lleva X a Y"),
    /// al Diario, o da energía/Esperanza. Inventario pequeño (4 huecos) para objetos de misión.
    /// </summary>
    [DisallowMultipleComponent]
    public class PickupSystem : MonoBehaviour
    {
        private static readonly int PickTrigger = Animator.StringToHash("Recoger");

        [SerializeField] private int slots = 4;
        [Tooltip("Punto al que vuelan los objetos (el Farol).")]
        [SerializeField] private Transform lanternPoint;
        [SerializeField] private Animator animator;
        [SerializeField] private ParticleSystem collectBurst;
        [SerializeField] private PlayerEnergy energy;
        [SerializeField] private float flyDuration = 0.45f;

        private readonly List<InventoryItem> items = new List<InventoryItem>();

        /// <summary>Se dispara cuando cambia el contenido del inventario.</summary>
        public event Action InventoryChanged;

        public static PickupSystem Instance { get; private set; }
        public IReadOnlyList<InventoryItem> Items => items;
        public int Slots => slots;

        private void Awake()
        {
            Instance = this;
            if (energy == null) energy = GetComponent<PlayerEnergy>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool Has(string itemId)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].Id == itemId) return true;
            return false;
        }

        /// <summary>Quita un objeto (al entregarlo). Devuelve true si estaba.</summary>
        public bool Remove(string itemId)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id != itemId) continue;
                items.RemoveAt(i);
                InventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>Empieza a recoger un objeto.</summary>
        public void Collect(PickupItem item)
        {
            if (item == null || !item.IsAvailable) return;

            if (item.Kind == PickupKind.ObjetoMision && items.Count >= slots)
            {
                Toast("Llevas demasiadas cosas: entrega algo primero.");
                return;
            }

            if (animator != null) animator.SetTrigger(PickTrigger);
            item.FlyTo(lanternPoint != null ? lanternPoint : transform, flyDuration, OnArrived);
        }

        private void OnArrived(PickupItem item)
        {
            if (collectBurst != null)
            {
                ParticleSystem.MainModule main = collectBurst.main;
                main.startColor = item.Color;
                collectBurst.Emit(18);
            }

            switch (item.Kind)
            {
                case PickupKind.ObjetoMision:
                    items.Add(new InventoryItem { Id = item.ItemId, Name = item.DisplayName, Color = item.Color });
                    InventoryChanged?.Invoke();
                    Toast("Recogiste: " + item.DisplayName);
                    break;

                case PickupKind.PaginaDiario:
                    if (GameProgress.AddPage(item.DiaryPageId)) Toast("Nueva página del Diario de Ilse · J para leer");
                    break;

                case PickupKind.Vela:
                case PickupKind.LuciernagaDorada:
                    if (energy != null && item.EnergyAmount > 0f) energy.Restore(item.EnergyAmount);
                    ZoneHope zone = string.IsNullOrEmpty(item.HopeZoneId) ? NearestZone(item.transform.position) : ZoneHope.Find(item.HopeZoneId);
                    if (zone != null && item.HopeAmount > 0f) zone.AddHope(item.HopeAmount);
                    if (item.Kind == PickupKind.LuciernagaDorada) Toast("Una luciérnaga dorada · el Farol se llena de luz");
                    break;

                case PickupKind.Recuerdo:
                    // El id del Recuerdo viaja en el mismo campo que el de las páginas.
                    if (GameProgress.UnlockRecuerdo(item.DiaryPageId))
                    {
                        Toast(RecuerdoText(item.DiaryPageId, item.DisplayName));
                        EventBus.RaiseRecuerdoUnlocked(item.DiaryPageId);
                    }
                    break;
            }

            EventBus.RaiseItemPickedUp(item.ItemId, item.DisplayName);
            EventBus.RaiseCelebration("objeto");
        }

        /// <summary>Qué enseña cada Recuerdo (texto corto y claro).</summary>
        private static string RecuerdoText(string id, string name)
        {
            switch (id)
            {
                case "lupa_mina": return "Recuerdo: " + name + " · mantén clic izquierdo y suelta: haz de luz";
                case "reloj_nadir": return "Recuerdo: " + name + " · ataca justo antes de un golpe: contraataque";
                default: return "Recuerdo: " + name;
            }
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

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.2f);
        }
    }
}
