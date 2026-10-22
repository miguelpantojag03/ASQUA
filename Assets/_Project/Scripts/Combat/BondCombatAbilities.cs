using System;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Quests;
using Ascua.UI;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Habilidades de combate de Vínculo 5 (una por personaje, inspirada en él):
    ///  - Teo: "Vela explosiva" (G / cruceta arriba). Lanza una vela que estalla en luz: área de 4 m,
    ///    3 de daño y aturde 3 s. 12 de energía, 18 s de recarga.
    ///  - Mina: "Brasa guardiana" (pasiva). Brasa orbita el Farol de Ilse y golpea sola a las sombras.
    /// Se activan en cuanto el Vínculo llega a 5 (misiones, oleadas defendidas en su barrio).
    /// </summary>
    public class BondCombatAbilities : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerEnergy energy;
        [SerializeField] private PlayerCombatState combatState;

        [Header("Teo · Vela explosiva")]
        [SerializeField] private string teoId = "teo";
        [SerializeField] private TeoCandleBomb candleBomb;
        [SerializeField] private float candleCost = 12f;
        [SerializeField] private float candleCooldown = 18f;
        [SerializeField] private float throwDistance = 6f;

        [Header("Mina · Brasa guardiana")]
        [SerializeField] private string minaId = "mina";
        [SerializeField] private BrasaGuardian brasaGuardian;

        [SerializeField] private int requiredBond = 5;

        public event Action<string> AbilityUnlocked;

        private float cooldownRemaining;
        private bool candleUnlocked;
        private bool brasaUnlocked;

        public bool CandleUnlocked => candleUnlocked;
        public bool BrasaUnlocked => brasaUnlocked;
        public float CandleCooldownRemaining => Mathf.Max(0f, cooldownRemaining);

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (energy == null) energy = GetComponent<PlayerEnergy>();
            if (combatState == null) combatState = GetComponent<PlayerCombatState>();
            if (candleBomb != null) candleBomb.gameObject.SetActive(false);
            if (brasaGuardian != null) brasaGuardian.gameObject.SetActive(false);
        }

        private void Start()
        {
            // Si ya se tenía el Vínculo de otra noche, las habilidades están desde el principio (sin aviso).
            Refresh(false);
        }

        private void OnEnable()
        {
            EventBus.BondChanged += OnBondChanged;
        }

        private void OnDisable()
        {
            EventBus.BondChanged -= OnBondChanged;
        }

        private void OnBondChanged(string npcId, int level)
        {
            Refresh(true);
        }

        private void Refresh(bool announce)
        {
            QuestManager manager = QuestManager.Instance;
            if (manager == null) return;

            if (!candleUnlocked && manager.GetBond(teoId) >= requiredBond)
            {
                candleUnlocked = true;
                if (announce) Toast("Vínculo 5 con Teo · G: Vela explosiva");
                AbilityUnlocked?.Invoke(teoId);
            }
            if (!brasaUnlocked && manager.GetBond(minaId) >= requiredBond)
            {
                brasaUnlocked = true;
                if (brasaGuardian != null) brasaGuardian.gameObject.SetActive(true);
                if (announce) Toast("Vínculo 5 con Mina · Brasa te acompaña y protege tu Farol");
                AbilityUnlocked?.Invoke(minaId);
            }
        }

        private void Update()
        {
            if (cooldownRemaining > 0f) cooldownRemaining -= Time.deltaTime;
            if (input != null && input.AbilityPressed) TryThrowCandle();
        }

        /// <summary>Lanza la vela de Teo (si está desbloqueada, cargada y hay energía).</summary>
        public bool TryThrowCandle()
        {
            if (!candleUnlocked || candleBomb == null || cooldownRemaining > 0f || candleBomb.IsBusy) return false;
            if (combatState != null && !combatState.CanFight) return false;
            if (energy == null || !energy.Consume(candleCost)) return false;

            // Apunta a la sombra más cercana delante; si no hay, a 6 m al frente.
            Vector3 from = transform.position + Vector3.up * 1.2f + transform.right * 0.3f;
            ILightTarget target = LightTargetRegistry.NearestInCone(transform.position, transform.forward, throwDistance + 2f, 60f);
            Vector3 to = target != null ? target.TargetPosition - Vector3.up * 0.5f : transform.position + transform.forward * throwDistance;
            candleBomb.Throw(from, to);
            cooldownRemaining = candleCooldown;
            EventBus.RaiseAbilityUsed("vela_teo");
            return true;
        }

        private static void Toast(string message)
        {
            if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show(message, 3f);
        }
    }
}
