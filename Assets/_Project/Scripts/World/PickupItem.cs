using System;
using System.Collections;
using System.Collections.Generic;
using Ascua.Core;
using Ascua.Quests;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>Qué es un objeto recogible y qué hace al recogerlo.</summary>
    public enum PickupKind
    {
        /// <summary>Va al inventario (misiones "Lleva X a Y").</summary>
        ObjetoMision,
        /// <summary>Página del Diario de Ilse (va al Codex).</summary>
        PaginaDiario,
        /// <summary>Vela de Teo: energía + Esperanza a su zona.</summary>
        Vela,
        /// <summary>Luciérnaga dorada (premio de exploración): mucha energía + Esperanza a la zona más cercana.</summary>
        LuciernagaDorada,
        /// <summary>Recuerdo que enseña un movimiento de combate (lo suelta una sombra derrotada).</summary>
        Recuerdo
    }

    /// <summary>
    /// Objeto que Ilse recoge con E. Flota, brilla un poco y, al recogerlo, vuela hasta el Farol
    /// (PickupSystem gestiona el efecto y el inventario). Puede exigir una misión activa para aparecer.
    /// </summary>
    public class PickupItem : MonoBehaviour, IInteractable
    {
        private static readonly List<PickupItem> all = new List<PickupItem>();

        [Header("Identidad")]
        [SerializeField] private string itemId = "objeto";
        [SerializeField] private string displayName = "Objeto";
        [SerializeField] private PickupKind kind = PickupKind.ObjetoMision;
        [SerializeField] private Color color = new Color(1f, 0.769f, 0.42f);

        [Header("Condiciones")]
        [Tooltip("Si no está vacío, solo aparece mientras esa misión esté Activa.")]
        [SerializeField] private string requiresActiveQuestId;

        [Header("Efectos")]
        [Tooltip("Id de la página del Diario (PaginaDiario) o del Recuerdo (Recuerdo).")]
        [SerializeField] private string diaryPageId;
        [SerializeField] private float energyAmount;
        [SerializeField] private float hopeAmount;
        [Tooltip("Zona que recibe Esperanza. Vacío = la zona más cercana.")]
        [SerializeField] private string hopeZoneId;

        [Header("Visual")]
        [SerializeField] private Transform visual;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float spinSpeed = 40f;
        [SerializeField] private float interactionRadius = 2.2f;

        private Vector3 visualBase;
        private bool collected;
        private float seed;
        private Collider[] colliders;

        public static IReadOnlyList<PickupItem> All => all;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public PickupKind Kind => kind;
        public Color Color => color;
        public string DiaryPageId => diaryPageId;
        public float EnergyAmount => energyAmount;
        public float HopeAmount => hopeAmount;
        public string HopeZoneId => hopeZoneId;
        public bool IsAvailable => !collected && IsUnlocked();

        // IInteractable
        public Vector3 InteractionPoint => transform.position;
        public string InteractionVerb => "Recoger";
        public string InteractionName => displayName;
        public float InteractionHoldTime => 0f;
        public float InteractionRadius => interactionRadius;
        public bool CanInteract => IsAvailable && isActiveAndEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(string id, string nameToShow, PickupKind pickupKind, Color itemColor, string questId = null,
            string pageId = null, float energy = 0f, float hope = 0f, string zoneId = null)
        {
            itemId = id;
            displayName = nameToShow;
            kind = pickupKind;
            color = itemColor;
            requiresActiveQuestId = questId;
            diaryPageId = pageId;
            energyAmount = energy;
            hopeAmount = hope;
            hopeZoneId = zoneId;
        }

        /// <summary>Vuelve a dejarlo disponible (pools como las velas de Teo).</summary>
        public void ResetPickup(Vector3 position)
        {
            collected = false;
            transform.position = position;
            if (visual != null) visual.localPosition = visualBase;
            SetCollidersEnabled(true);
            gameObject.SetActive(true);
        }

        private void Awake()
        {
            seed = UnityEngine.Random.value * 10f;
            colliders = GetComponentsInChildren<Collider>(true);
            if (visual != null) visualBase = visual.localPosition;
        }

        private void Start()
        {
            // Páginas y Recuerdos ya conseguidos en otra noche no vuelven a aparecer.
            // (Una página robada por un Susurro que escapó vuelve a su sitio la noche siguiente.)
            if (kind == PickupKind.PaginaDiario && GameProgress.HasPage(diaryPageId)) gameObject.SetActive(false);
            if (kind == PickupKind.Recuerdo && GameProgress.HasRecuerdo(diaryPageId)) gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            InteractableRegistry.Register(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            InteractableRegistry.Unregister(this);
        }

        private void Update()
        {
            if (visual == null || collected) return;

            bool unlocked = IsUnlocked();
            if (visual.gameObject.activeSelf != unlocked) visual.gameObject.SetActive(unlocked);
            if (!unlocked) return;

            float t = Time.time + seed;
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(t * 2f) * bobAmplitude);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private bool IsUnlocked()
        {
            if (string.IsNullOrEmpty(requiresActiveQuestId)) return true;
            QuestManager manager = QuestManager.Instance;
            QuestRuntime q = manager != null ? manager.FindById(requiresActiveQuestId) : null;
            return q != null && q.State == QuestState.Activa;
        }

        public void Interact(Transform interactor)
        {
            if (!CanInteract) return;
            Ascua.Player.PickupSystem system = interactor != null ? interactor.GetComponent<Ascua.Player.PickupSystem>() : null;
            if (system != null) system.Collect(this);
        }

        /// <summary>Vuela hasta 'target' encogiéndose y avisa al llegar. Lo llama PickupSystem.</summary>
        public void FlyTo(Transform target, float duration, Action<PickupItem> onArrived)
        {
            if (collected) return;
            collected = true;
            SetCollidersEnabled(false);
            StartCoroutine(Fly(target, duration, onArrived));
        }

        private IEnumerator Fly(Transform target, float duration, Action<PickupItem> onArrived)
        {
            Vector3 start = transform.position;
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.05f, duration);
                float eased = t * t * (3f - 2f * t);
                Vector3 end = target != null ? target.position : start;
                // Arco suave hacia arriba antes de entrar en el Farol.
                Vector3 position = Vector3.Lerp(start, end, eased) + Vector3.up * (Mathf.Sin(eased * Mathf.PI) * 0.45f);
                transform.position = position;
                transform.localScale = startScale * Mathf.Lerp(1f, 0.15f, eased);
                yield return null;
            }

            transform.localScale = startScale;
            onArrived?.Invoke(this);
            gameObject.SetActive(false);
        }

        private void SetCollidersEnabled(bool value)
        {
            if (colliders == null) return;
            foreach (Collider c in colliders) if (c != null) c.enabled = value;
        }
    }
}
