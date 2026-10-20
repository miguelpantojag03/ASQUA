using Ascua.Core;
using Ascua.UI;
using Ascua.World;
using UnityEngine;

namespace Ascua.Quests
{
    /// <summary>
    /// Habilidades pasivas por Vínculo (nivel 3 o más):
    ///  - Teo: deja una vela recogible en El Mercado cada 30 s.
    ///  - Mina: las luciérnagas aparecen más a menudo cerca de La Plaza.
    /// Se activan al llegar al nivel y siguen activas en noches futuras (el Vínculo persiste).
    /// </summary>
    public class BondPerks : MonoBehaviour
    {
        [SerializeField] private int unlockLevel = 3;

        [Header("Teo · Velas")]
        [SerializeField] private string teoId = "teo";
        [SerializeField] private CandleSpawner candleSpawner;

        [Header("Mina · Luciérnagas")]
        [SerializeField] private string minaId = "mina";
        [SerializeField] private FireflyPool fireflyPool;
        [SerializeField] private string minaZoneId = "Plaza";

        private bool teoActive;
        private bool minaActive;

        private void OnEnable()
        {
            EventBus.BondChanged += OnBondChanged;
        }

        private void OnDisable()
        {
            EventBus.BondChanged -= OnBondChanged;
        }

        private void Start()
        {
            if (fireflyPool == null) fireflyPool = FindFirstObjectByType<FireflyPool>();
            if (candleSpawner == null) candleSpawner = FindFirstObjectByType<CandleSpawner>();

            // Vínculos ganados en noches anteriores: habilidades activas desde el principio, sin aviso.
            QuestManager manager = QuestManager.Instance;
            if (manager != null)
            {
                Apply(teoId, manager.GetBond(teoId), false);
                Apply(minaId, manager.GetBond(minaId), false);
            }
        }

        private void OnBondChanged(string npcId, int level)
        {
            Apply(npcId, level, true);
        }

        private void Apply(string npcId, int level, bool announce)
        {
            bool unlocked = level >= unlockLevel;

            if (npcId == teoId && unlocked != teoActive)
            {
                teoActive = unlocked;
                if (candleSpawner != null) candleSpawner.SetActive(unlocked);
                if (unlocked && announce) Toast("Vínculo con Teo · dejará velas en El Mercado");
            }
            else if (npcId == minaId && unlocked != minaActive)
            {
                minaActive = unlocked;
                ZoneHope zone = ZoneHope.Find(minaZoneId);
                if (fireflyPool != null && zone != null)
                {
                    if (unlocked) fireflyPool.SetHotspot("vinculo_mina", zone.Center, zone.Radius);
                    else fireflyPool.ClearHotspot("vinculo_mina");
                }
                if (unlocked && announce) Toast("Vínculo con Mina · las luciérnagas la buscan");
            }
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 2.6f);
        }
    }
}
