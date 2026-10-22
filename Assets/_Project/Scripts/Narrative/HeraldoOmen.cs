using System.Collections;
using Ascua.Characters;
using Ascua.Combat;
using Ascua.Controls;
using Ascua.Core;
using Ascua.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

namespace Ascua.Narrative
{
    /// <summary>
    /// Parte D.2 · Primer indicio (Noche 2). Escena breve (~14 s) a mitad de la noche:
    /// sobre los tejados del norte se enciende un farol de luz VIOLETA, suena un susurro sin palabras
    /// y Ilse piensa en voz baja, inquieta sin saber por qué. Siembra la pregunta
    /// "¿por qué encendería un farol quien quiere que todo se apague?" sin revelar quién es el Heraldo
    /// (eso llega en la Noche 4).
    ///
    /// Es una secuencia guionizada por código (como el momento de Brasa) en vez de un asset de Timeline:
    /// la escena se genera entera por script y así no hay enlaces de Timeline que se rompan al reconstruirla.
    ///
    /// Reglas para que sea justa y accesible:
    ///  - Solo empieza si no hay combate, ni diálogo abierto, ni el Heraldo persiguiendo a Ilse.
    ///    Si a partir del 80 % de la noche sigue sin darse, se reproduce igual pero SIN mover la cámara.
    ///  - Con "Sacudidas de cámara" desactivadas (quien se marea), la cámara tampoco se mueve:
    ///    solo aparecen la luz, el susurro y los pensamientos.
    ///  - El control se devuelve en cuanto la cámara vuelve (≈6 s); el resto son subtítulos.
    ///  - Al terminar, se desbloquea la página 4 del Diario ("Una luz que no era de nadie").
    /// </summary>
    public class HeraldoOmen : MonoBehaviour
    {
        [System.Serializable]
        public class TimedThought
        {
            [Tooltip("Segundo de la escena en que aparece.")]
            public float at;
            [TextArea(1, 3)] public string text;
            [Tooltip("Segundos en pantalla.")]
            public float hold = 3.2f;
        }

        [Header("Cuándo")]
        [SerializeField] private string eventId = "noche2_presagio";
        [SerializeField] private int night = 2;
        [Tooltip("Momento de la noche (0-1) a partir del cual puede ocurrir.")]
        [SerializeField, Range(0f, 1f)] private float atProgress = 0.5f;
        [Tooltip("A partir de aquí ocurre aunque haya combate (pero sin mover la cámara).")]
        [SerializeField, Range(0f, 1f)] private float forceAtProgress = 0.8f;

        [Header("La luz violeta")]
        [SerializeField] private Transform omenRoot;
        [SerializeField] private Light omenLight;
        [SerializeField] private Renderer omenGlow;
        [SerializeField] private float lightIntensity = 9f;
        [Tooltip("Puntos posibles (tejados). Se usa el más cercano a 26 m de Ilse para que se vea lejos pero se vea.")]
        [SerializeField] private Transform[] spots;

        [Header("Puesta en escena")]
        [SerializeField] private CinemachineCamera omenCamera;
        [SerializeField] private CanvasGroup thoughtGroup;
        [SerializeField] private TMP_Text thoughtText;
        [SerializeField] private CanvasGroup hudGroup;
        [Tooltip("Segundos que la cámara mira la luz (el control vuelve después).")]
        [SerializeField] private float cameraSeconds = 6f;
        [SerializeField] private float duration = 14.5f;

        [Header("Pensamientos de Ilse (no habla con nadie: piensa)")]
        [SerializeField] private TimedThought[] thoughts =
        {
            new TimedThought { at = 0.8f, text = "Allí, sobre los tejados del norte… alguien ha encendido un farol.", hold = 3.2f },
            new TimedThought { at = 4.2f, text = "Pero esa luz no calienta. Es violeta, como la suya.", hold = 3f },
            new TimedThought { at = 7.4f, text = "[Un susurro lejano, sin palabras]", hold = 2f },
            new TimedThought { at = 9.6f, text = "¿Por qué iba a encender un farol quien quiere que todo se apague?", hold = 2.4f },
            new TimedThought { at = 12.1f, text = "Se ha ido. Y tengo frío, como si hubiera olvidado algo importante.", hold = 2.3f }
        };

        [Header("Recompensa")]
        [SerializeField] private string diaryPageId = "pagina_04";

        private bool playing;
        private PlayerInputReader input;

        public bool IsPlaying => playing;
        public bool HasPlayed => GameProgress.HasSeenMoment(eventId);
        /// <summary>True si la última vez se movió la cámara (para la prueba automática).</summary>
        public bool UsedCamera { get; private set; }

        private void Awake()
        {
            if (omenCamera != null) omenCamera.gameObject.SetActive(false);
            if (thoughtGroup != null) thoughtGroup.alpha = 0f;
            SetOmen(0f);
            if (omenRoot != null) omenRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (playing || HasPlayed || GameProgress.CurrentNight != night) return;
            NightManager nightManager = NightManager.Instance;
            if (nightManager == null || !nightManager.IsRunning || nightManager.Progress01 < atProgress) return;
            if (DialogueRunner.Instance != null && DialogueRunner.Instance.IsOpen) return;

            bool calm = IsCalm();
            if (!calm && nightManager.Progress01 < forceAtProgress) return;
            Begin(calm);
        }

        /// <summary>Lanza la escena ya (pruebas, depuración). allowCamera = false la reproduce sin mover la cámara.</summary>
        public void PlayNow(bool allowCamera = true)
        {
            if (playing) return;
            Begin(allowCamera);
        }

        private void Begin(bool allowCamera)
        {
            GameProgress.MarkMomentSeen(eventId);
            StartCoroutine(Play(allowCamera && AccessibilitySettings.CameraShake));
        }

        /// <summary>Sin sombras vivas, sin el Heraldo persiguiendo y sin otro momento narrativo en curso.</summary>
        private static bool IsCalm()
        {
            CombatWaveDirector director = CombatWaveDirector.Instance;
            if (director != null && director.Alive.Count > 0) return false;
            Heraldo_AI heraldo = FindFirstObjectByType<Heraldo_AI>();
            if (heraldo != null && heraldo.IsChasing) return false;
            return true;
        }

        private IEnumerator Play(bool moveCamera)
        {
            playing = true;
            UsedCamera = moveCamera;
            EventBus.RaiseNarrativeMoment(eventId, true);

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            PlaceOmen(playerPos);
            if (omenRoot != null) omenRoot.gameObject.SetActive(true);

            CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            CinemachineBlendDefinition previousBlend = brain != null ? brain.DefaultBlend : default;
            if (moveCamera)
            {
                input = FindFirstObjectByType<PlayerInputReader>();
                if (input != null) input.SetGameplayEnabled(false);
                if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.1f);
                FrameCamera(player != null ? player.transform : null);
            }

            bool whisperPlayed = false;
            bool cameraReleased = !moveCamera;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;

                // La luz: aparece en 2.5 s, tiembla como una llama y se apaga en los últimos 3.5 s.
                float appear = Mathf.SmoothStep(0f, 1f, t / 2.5f);
                float fade = Mathf.SmoothStep(0f, 1f, (duration - 1.5f - t) / 3.5f);
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(t * 3f, 0.4f);
                SetOmen(appear * fade * flicker);
                if (omenRoot != null) omenRoot.position += Vector3.up * (Mathf.Sin(t * 0.9f) * 0.002f);

                if (!whisperPlayed && t >= 7f)
                {
                    whisperPlayed = true;
                    EventBus.RaiseAbilityUsed("presagio_susurro");
                }

                if (!cameraReleased && t >= cameraSeconds)
                {
                    cameraReleased = true;
                    ReleaseCamera();
                }

                if (hudGroup != null) hudGroup.alpha = cameraReleased ? Mathf.MoveTowards(hudGroup.alpha, 1f, Time.unscaledDeltaTime * 2f) : Mathf.MoveTowards(hudGroup.alpha, 0.25f, Time.unscaledDeltaTime * 2f);
                UpdateThought(t);
                yield return null;
            }

            if (!cameraReleased) ReleaseCamera();
            if (thoughtGroup != null) thoughtGroup.alpha = 0f;
            if (hudGroup != null) hudGroup.alpha = 1f;
            SetOmen(0f);
            if (omenRoot != null) omenRoot.gameObject.SetActive(false);

            if (!string.IsNullOrEmpty(diaryPageId) && GameProgress.AddPage(diaryPageId) && ZoneLostNotification.Instance != null)
            {
                ZoneLostNotification.Instance.Show("Nueva página del Diario de Ilse · J para leer", 2.6f);
            }

            playing = false;
            EventBus.RaiseNarrativeMoment(eventId, false);

            yield return new WaitForSecondsRealtime(1.2f);
            if (brain != null && moveCamera) brain.DefaultBlend = previousBlend;
        }

        private void ReleaseCamera()
        {
            if (omenCamera != null) omenCamera.gameObject.SetActive(false);
            if (input != null) input.SetGameplayEnabled(true);
            input = null;
        }

        /// <summary>
        /// Elige dónde se enciende la luz: el tejado más cercano a 26 m de Ilse DESDE EL QUE SE VEA.
        /// Para cada tejado prueba varias alturas (+0, +3, +6, +9 m) y se queda con la primera sin nada
        /// en medio (ni desde los ojos de Ilse ni desde la cámara de la escena). Si ninguno se ve,
        /// la enciende en el cielo, delante de Ilse, a 22 m y 15 m de altura.
        /// </summary>
        private void PlaceOmen(Vector3 from)
        {
            if (omenRoot == null) return;
            Vector3 eye = from + Vector3.up * 1.6f;
            System.Collections.Generic.List<Transform> ordered = new System.Collections.Generic.List<Transform>();
            if (spots != null) foreach (Transform spot in spots) if (spot != null) ordered.Add(spot);
            ordered.Sort((a, b) => Mathf.Abs(FlatDistance(a.position, from) - 26f).CompareTo(Mathf.Abs(FlatDistance(b.position, from) - 26f)));

            foreach (Transform spot in ordered)
            {
                for (int step = 0; step < 4; step++)
                {
                    Vector3 candidate = spot.position + Vector3.up * (3f * step);
                    if (IsVisible(eye, candidate) && IsVisible(CameraPositionFor(from, candidate), candidate))
                    {
                        omenRoot.position = candidate;
                        return;
                    }
                }
            }

            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            omenRoot.position = from + forward.normalized * 22f + Vector3.up * 15f;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>True si no hay geometría del escenario entre los dos puntos (Ilse y los objetos sin colisión no cuentan).</summary>
        private static bool IsVisible(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f) return true;
            RaycastHit[] hits = Physics.RaycastAll(from, dir / length, length - 0.5f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.root.CompareTag("Player")) continue;
                return false;
            }
            return true;
        }

        /// <summary>Cámara de la escena: detrás y a la derecha de Ilse, algo baja, mirando la luz.</summary>
        private static Vector3 CameraPositionFor(Vector3 playerPos, Vector3 omenPos)
        {
            Vector3 flat = omenPos - playerPos;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            return playerPos - flat * 3.4f + right * 1.15f + Vector3.up * 1.25f;
        }

        /// <summary>Plano por encima del hombro de Ilse mirando la luz (Ilse de espaldas, pequeña en un lado: está sola).</summary>
        private void FrameCamera(Transform player)
        {
            if (omenCamera == null || player == null || omenRoot == null) return;
            omenCamera.transform.position = CameraPositionFor(player.position, omenRoot.position);
            omenCamera.transform.rotation = Quaternion.LookRotation(omenRoot.position - omenCamera.transform.position, Vector3.up);
            omenCamera.gameObject.SetActive(true);
        }

        private void UpdateThought(float t)
        {
            if (thoughtGroup == null || thoughtText == null || thoughts == null) return;
            TimedThought current = null;
            foreach (TimedThought thought in thoughts)
            {
                if (t >= thought.at && t < thought.at + thought.hold) current = thought;
            }
            if (current == null)
            {
                thoughtGroup.alpha = Mathf.MoveTowards(thoughtGroup.alpha, 0f, Time.unscaledDeltaTime * 4f);
                return;
            }
            if (thoughtText.text != current.text) thoughtText.text = current.text;
            float local = t - current.at;
            thoughtGroup.alpha = Mathf.Clamp01(local / 0.4f) * Mathf.Clamp01((current.hold - local) / 0.4f);
        }

        private void SetOmen(float amount)
        {
            if (omenLight != null) omenLight.intensity = lightIntensity * amount;
            if (omenGlow != null) omenGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 1.15f, amount);
        }
    }
}
