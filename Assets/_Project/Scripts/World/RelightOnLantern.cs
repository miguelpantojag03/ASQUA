using Ascua.Core;
using Ascua.Lighting;
using UnityEngine;

namespace Ascua.World
{
    /// <summary>
    /// Farola apagada por un Apagavelas: se queda bloqueada hasta que el Farol de Ilse la ilumine
    /// 1.2 s seguidos. Entonces se desbloquea, se enciende y su zona recupera +4 de Esperanza,
    /// con una celebración corta. Si la zona muere antes, la farola sigue apagada (como el resto).
    /// Se añade por código (SnuffTarget.Snuff) y se quita sola al reencender.
    /// </summary>
    [RequireComponent(typeof(StreetLampToggle))]
    public class RelightOnLantern : MonoBehaviour
    {
        [SerializeField] private float holdSeconds = 1.2f;
        [SerializeField] private float hopeBack = 4f;

        private StreetLampToggle lamp;
        private Light lampLight;
        private float timer;

        /// <summary>0-1: cuánto falta para reencenderla.</summary>
        public float Progress01 => holdSeconds > 0f ? Mathf.Clamp01(timer / holdSeconds) : 1f;

        /// <summary>Marca una farola como "apagada por un Apagavelas" (reencendible con el Farol).</summary>
        public static RelightOnLantern Attach(StreetLampToggle lamp)
        {
            if (lamp == null) return null;
            RelightOnLantern relight = lamp.GetComponent<RelightOnLantern>();
            if (relight == null) relight = lamp.gameObject.AddComponent<RelightOnLantern>();
            relight.timer = 0f;
            relight.enabled = true;
            return relight;
        }

        private void Awake()
        {
            lamp = GetComponent<StreetLampToggle>();
            lampLight = GetComponentInChildren<Light>(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // pausa
            if (lamp == null)
            {
                Destroy(this);
                return;
            }

            ZoneHope zone = ZoneHope.Find(lamp.ZoneId);
            if (zone != null && zone.IsDead)
            {
                // Zona apagada esta noche: la farola ya no vuelve (la bloquea la propia zona).
                Destroy(this);
                return;
            }

            if (IsLitByLantern()) timer += dt;
            else timer = 0f;
            if (timer < holdSeconds) return;

            lamp.SetLocked(false);
            lamp.TurnOn();
            if (zone != null) zone.AddHope(hopeBack);
            EventBus.RaiseCelebration("farola_reencendida");
            Destroy(this);
        }

        /// <summary>El Farol alcanza la farola: su cristal o, por si queda alto, el poste a la altura de Ilse.</summary>
        private bool IsLitByLantern()
        {
            Vector3 glass = lampLight != null ? lampLight.transform.position : transform.position + Vector3.up * 3f;
            Vector3 post = transform.position + Vector3.up * 1.5f;
            return IlluminationRegistry.IsPointIlluminated(glass, out _, false)
                || IlluminationRegistry.IsPointIlluminated(post, out _, false);
        }
    }
}
