using Ascua.Core;
using Ascua.UI;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Camino secundario que premia explorar con el movimiento (saltar, trepar, correr): un callejón
    /// tapado por cajas, un tejado al que se sube por etapas... Al llegar, se "descubre": aviso con su
    /// nombre, se enciende una lucecita de recompensa y queda registrado en el progreso.
    /// El premio en sí (página del Diario, luciérnaga dorada) son PickupItem colocados en el sitio.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ExplorationShortcut : MonoBehaviour
    {
        [SerializeField] private string shortcutId = "atajo";
        [SerializeField] private string displayName = "Un rincón olvidado";
        [Tooltip("Luz que se enciende al descubrirlo (opcional).")]
        [SerializeField] private Light revealLight;
        [SerializeField] private float revealIntensity = 1.6f;
        [SerializeField] private float revealSpeed = 1.2f;

        private bool discovered;
        private float level;

        public string ShortcutId => shortcutId;
        public bool Discovered => discovered;

        /// <summary>Configuración rápida desde el constructor de escenas.</summary>
        public void Configure(string id, string nameToShow, Light light)
        {
            shortcutId = id;
            displayName = nameToShow;
            revealLight = light;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            discovered = GameProgress.IsShortcutDiscovered(shortcutId);
            level = discovered ? 1f : 0f;
            ApplyLight();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (discovered || !other.CompareTag("Player")) return;
            discovered = true;
            if (GameProgress.DiscoverShortcut(shortcutId))
            {
                if (ZoneLostNotification.Instance != null) ZoneLostNotification.Instance.Show("Descubriste: " + displayName, 2.8f);
                EventBus.RaiseShortcutDiscovered(shortcutId, displayName);
                EventBus.RaiseCelebration("atajo");
            }
        }

        private void Update()
        {
            if (!discovered || level >= 1f) return;
            level = Mathf.MoveTowards(level, 1f, revealSpeed * Time.deltaTime);
            ApplyLight();
        }

        private void ApplyLight()
        {
            if (revealLight == null) return;
            revealLight.intensity = revealIntensity * level;
            revealLight.enabled = level > 0.01f;
        }
    }
}
