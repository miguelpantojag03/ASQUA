using Ascua.Core;
using Ascua.Narrative;
using Ascua.Player;
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

        [Header("Teo · Velas y Farol grande")]
        [SerializeField] private string teoId = "teo";
        [SerializeField] private CandleSpawner candleSpawner;
        [Tooltip("Teo regala el cristal grande del farol de su padre: desbloquea el 4º tamaño del Farol.")]
        [SerializeField] private DialogueSequence teoGiftDialogue;

        private bool giftPending;

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

        private void Update()
        {
            if (!giftPending) return;
            DialogueRunner runner = DialogueRunner.Instance;
            if (runner == null || runner.IsOpen) return;

            giftPending = false;
            PlayerUpgrades.UnlockLargeLantern();
            NpcInteractable teo = NpcInteractable.Find(teoId);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (teo != null) runner.Play(teoGiftDialogue, teo, null, player != null ? player.transform : null);
            Toast("Farol grande · pulsa E (o RB) para el 4º tamaño de luz");
        }

        /// <summary>Para pruebas e IA: fuerza la entrega pendiente del regalo.</summary>
        public bool GiftPending => giftPending;

        private void Apply(string npcId, int level, bool announce)
        {
            bool unlocked = level >= unlockLevel;

            if (npcId == teoId && unlocked != teoActive)
            {
                teoActive = unlocked;
                if (candleSpawner != null) candleSpawner.SetActive(unlocked);
                if (unlocked && announce) Toast("Vínculo con Teo · dejará velas en El Mercado");

                // El regalo de Teo: se entrega con su propio diálogo justo después de la conversación actual.
                if (unlocked && !PlayerUpgrades.LargeLanternUnlocked)
                {
                    if (announce && teoGiftDialogue != null) giftPending = true;
                    else PlayerUpgrades.UnlockLargeLantern();
                }
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
