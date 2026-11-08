using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Ascua.Audio;
using Ascua.Characters;
using Ascua.Core;
using Ascua.Endings;
using Ascua.Quests;
using Ascua.World;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Ascua.UI
{
    /// <summary>
    /// Escena del final hecha por código (sin Timeline), unos 20 s con tiempo sin escalar (el juego está en pausa):
    ///  1) Fundido a oscuro.
    ///  2) La cámara (Camera.main) sube y se aleja despacio sobre la ciudad, con el tono del final.
    ///  3) Un farolillo (luz puntual) junto a cada personaje con Vínculo 3 o más.
    ///     En Constelación todas las ventanas se encienden doradas y aparecen Mina, Teo y Perpetua.
    ///  4) Fundido al fondo del epílogo y aviso a EndingScreen.
    /// Música con AudioManager.SetEndingMusic. Cualquier tecla acelera; otra vez, salta.
    /// Con "Reducir destellos" las luces son más suaves, se encienden más despacio y acelera menos.
    /// Todo se calcula a partir del tiempo transcurrido (Apply), así saltar es poner el tiempo al final.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        // Duraciones a velocidad normal (s). Total: 19.5 s.
        private const float FadeOutTime = 1.5f;
        private const float FadeInTime = 2f;
        private const float FlyoverTime = 14f;
        private const float FadeEndTime = 2f;
        private const float SetupTime = FadeOutTime;
        private const float FlyoverEnd = FadeOutTime + FadeInTime + FlyoverTime;
        private const float TotalTime = FlyoverEnd + FadeEndTime;

        private const int MinBond = 3;
        private static readonly string[] ConstellationNames = { "Mina", "Teo", "Perpetua" };
        private static readonly Color Gold = new Color(1f, 0.8f, 0.35f);
        private static readonly Color LanternColor = new Color(1f, 0.74f, 0.42f);

        private Image overlay;
        private TMP_Text namesText;
        private TMP_Text hintText;

        private EndingId ending;
        private Action onFinished;
        private bool listening;
        private bool skip;
        private float speed = 1f;
        private float elapsed;
        private bool setupDone;

        // Ajustes de accesibilidad (se leen al empezar).
        private bool reduceFlashes;
        private float lightFade;
        private float lightScale;

        // Cámara.
        private Camera cam;
        private Vector3 camFrom, camTo, focus;
        private Quaternion rotFrom;

        // Mundo.
        private Color tint;
        private readonly List<Light> lanterns = new List<Light>();
        private readonly List<WindowLight> windows = new List<WindowLight>();

        /// <summary>Añade la secuencia a la pantalla de final. overlay = fondo de esa pantalla; textTemplate da la tipografía.</summary>
        public static EndingSequence Create(GameObject host, Image overlay, TMP_Text textTemplate)
        {
            EndingSequence sequence = host.AddComponent<EndingSequence>();
            sequence.overlay = overlay;

            sequence.namesText = EndingUi.CloneText(textTemplate, host.transform, "Nombres_Constelacion", 60f, Gold);
            EndingUi.Place(sequence.namesText, new Vector2(0.5f, 0.24f), new Vector2(1400f, 140f));

            sequence.hintText = EndingUi.CloneText(textTemplate, host.transform, "Pista_Saltar", 22f, new Color(0.85f, 0.88f, 0.96f, 0.6f));
            EndingUi.Place(sequence.hintText, new Vector2(0.5f, 0.04f), new Vector2(900f, 50f));
            return sequence;
        }

        /// <summary>Empieza la secuencia del final; al acabar (o al saltarla) llama a onDone.</summary>
        public void Play(EndingId id, Action onDone)
        {
            ending = id;
            onFinished = onDone;
            skip = false;
            listening = false;
            speed = 1f;
            elapsed = 0f;
            setupDone = false;

            reduceFlashes = AccessibilitySettings.ReduceFlashes;
            lightFade = reduceFlashes ? 2.5f : 1.2f;
            lightScale = reduceFlashes ? 0.55f : 1f;

            overlay.color = new Color(0f, 0f, 0f, 0f);
            namesText.text = "";
            hintText.text = "Cualquier tecla: acelerar";
            StartCoroutine(Run());
        }

        private void Update()
        {
            if (!listening || !Input.anyKeyDown) return;

            // Primera pulsación: acelera. Segunda: salta al final.
            if (speed <= 1f)
            {
                speed = reduceFlashes ? 2f : 4f;
                hintText.text = "Otra vez: saltar";
            }
            else skip = true;
        }

        private IEnumerator Run()
        {
            // La pulsación que abrió el final no cuenta como "acelerar".
            yield return null;
            listening = true;

            while (elapsed < TotalTime && !skip)
            {
                elapsed += Time.unscaledDeltaTime * speed;
                Apply(Mathf.Min(elapsed, TotalTime));
                yield return null;
            }

            Apply(TotalTime);
            listening = false;
            hintText.text = "";
            namesText.text = "";
            onFinished?.Invoke();
        }

        /// <summary>Pone todo en el estado que toca en el segundo 'time' de la secuencia.</summary>
        private void Apply(float time)
        {
            if (time < SetupTime)
            {
                // 1) Fundido a oscuro (sobre el resumen de la noche).
                overlay.color = new Color(0f, 0f, 0f, Smooth(time / FadeOutTime));
                return;
            }

            if (!setupDone) Setup();

            // 2) Del negro al tono del final; al acabar, al fondo del epílogo.
            float fadeIn = Smooth((time - SetupTime) / FadeInTime);
            Color toned = Color.Lerp(Color.black, tint, fadeIn);
            float fadeEnd = Smooth((time - FlyoverEnd) / FadeEndTime);
            overlay.color = Color.Lerp(toned, EndingUi.OverlayColor, fadeEnd);

            // Cámara: sube y se aleja despacio, siempre mirando al centro de la ciudad.
            if (cam != null)
            {
                float u = Smooth((time - SetupTime) / (FadeInTime + FlyoverTime));
                Vector3 position = Vector3.Lerp(camFrom, camTo, u);
                Quaternion look = Quaternion.LookRotation(focus - position, Vector3.up);
                cam.transform.SetPositionAndRotation(position, Quaternion.Slerp(rotFrom, look, Smooth(u * 3f)));
            }

            // 3) Farolillos de los personajes con Vínculo alto, uno tras otro.
            float lanternStart = SetupTime + FadeInTime + 1f;
            for (int i = 0; i < lanterns.Count; i++)
            {
                float on = Mathf.Clamp01((time - lanternStart - i * 1.2f) / lightFade);
                lanterns[i].intensity = 4f * lightScale * Smooth(on);
                lanterns[i].enabled = on > 0f;
            }

            if (ending == EndingId.Constelacion) ApplyConstellation(time, fadeEnd);
        }

        /// <summary>Constelación: ventanas doradas en cascada y los nombres de Mina, Teo y Perpetua.</summary>
        private void ApplyConstellation(float time, float fadeEnd)
        {
            float windowStart = SetupTime + FadeInTime + 2f;
            float cascade = 4f;
            for (int i = 0; i < windows.Count; i++)
            {
                float delay = windows.Count > 1 ? cascade * i / (windows.Count - 1) : 0f;
                float on = Mathf.Clamp01((time - windowStart - delay) / lightFade);
                if (on > 0f) windows[i].SetEndingGlow(Gold, Smooth(on) * (reduceFlashes ? 0.7f : 1f));
            }

            float nameStart = SetupTime + FadeInTime + 5f;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ConstellationNames.Length; i++)
            {
                float a = Mathf.Clamp01((time - nameStart - i * 2.2f) / 1.2f) * (1f - fadeEnd);
                sb.Append("<alpha=#").Append(((byte)(255f * Smooth(a))).ToString("X2")).Append('>').Append(ConstellationNames[i]);
                if (i < ConstellationNames.Length - 1) sb.Append("      ");
            }
            namesText.text = sb.ToString();
        }

        /// <summary>En la oscuridad: oculta la UI del juego, toma la cámara, pone el tono, la música y los farolillos.</summary>
        private void Setup()
        {
            setupDone = true;
            HideGameUi();

            tint = ToneFor(ending);
            if (AudioManager.Instance != null)
            {
                Vector2 music = MusicFor(ending);
                AudioManager.Instance.SetEndingMusic(music.x, music.y);
            }

            // La Penumbra deja de tocar la niebla y la niebla se aclara para ver la ciudad desde arriba.
            if (Penumbra.Instance != null) Penumbra.Instance.enabled = false;
            RenderSettings.fogDensity *= 0.35f;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, tint, 0.25f);

            SetupCamera();
            SpawnLanterns();
            if (ending == EndingId.Constelacion) CollectWindows();
        }

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null) return;

            CinemachineBrain brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            // Centro de la ciudad = media de las zonas (o lo que mira la cámara si no hay zonas).
            var zones = ZoneHope.All;
            if (zones.Count > 0)
            {
                focus = Vector3.zero;
                for (int i = 0; i < zones.Count; i++) focus += zones[i].Center;
                focus /= zones.Count;
            }
            else focus = cam.transform.position + cam.transform.forward * 10f;

            camFrom = cam.transform.position;
            rotFrom = cam.transform.rotation;
            // Destino: detrás de donde ya está la cámara, más lejos del centro y bastante más alto.
            Vector3 away = camFrom - focus;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance < 0.1f) away = -cam.transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.back;
            camTo = focus + away * Mathf.Max(38f, distance * 1.5f) + Vector3.up * 32f;
        }

        private void SpawnLanterns()
        {
            QuestManager quests = QuestManager.Instance;
            if (quests == null) return;

            foreach (NpcIdentity npc in FindObjectsByType<NpcIdentity>(FindObjectsSortMode.InstanceID))
            {
                if (quests.GetBond(npc.NpcId) < MinBond) continue;

                GameObject lantern = new GameObject("Farolillo_" + npc.NpcId);
                lantern.transform.position = npc.transform.position + npc.transform.right * 0.5f + Vector3.up * 1.7f;
                Light light = lantern.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 8f;
                light.color = Color.Lerp(LanternColor, npc.AccentColor, 0.25f);
                light.shadows = LightShadows.None;
                light.intensity = 0f;
                light.enabled = false;
                lanterns.Add(light);
            }
        }

        private void CollectWindows()
        {
            windows.AddRange(FindObjectsByType<WindowLight>(FindObjectsSortMode.InstanceID));
            // Orden de cerca a lejos del centro: la luz se extiende desde el corazón de la ciudad.
            windows.Sort((a, b) => (a.transform.position - focus).sqrMagnitude.CompareTo((b.transform.position - focus).sqrMagnitude));
        }

        /// <summary>Oculta todo lo que no es esta pantalla: el resto de canvases y, en el nuestro, el HUD y el resumen.</summary>
        private void HideGameUi()
        {
            Canvas own = overlay.canvas != null ? overlay.canvas.rootCanvas : null;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas && canvas != own) canvas.enabled = false;
            }
            if (own == null) return;

            // Hermanos de la rama del resumen (HUD, diálogos, avisos...) se desactivan; el resumen queda transparente.
            Transform branch = transform;
            while (branch.parent != null && branch.parent != own.transform) branch = branch.parent;
            foreach (Transform child in own.transform)
            {
                if (child != branch) child.gameObject.SetActive(false);
            }
            CanvasGroup summary = transform.parent != null ? transform.parent.GetComponent<CanvasGroup>() : null;
            if (summary != null)
            {
                summary.alpha = 0f;
                summary.interactable = false;
            }
        }

        /// <summary>Tono del final (color del velo sobre la ciudad, con su transparencia).</summary>
        private static Color ToneFor(EndingId id)
        {
            switch (id)
            {
                case EndingId.Ceniza: return new Color(0.5f, 0.55f, 0.62f, 0.3f);        // gris frío
                case EndingId.Vigilia: return new Color(0.2f, 0.33f, 0.75f, 0.25f);      // azul
                case EndingId.Alba: return new Color(1f, 0.62f, 0.25f, 0.22f);           // ámbar
                case EndingId.Constelacion: return new Color(1f, 0.8f, 0.35f, 0.16f);    // dorado
                case EndingId.ElTrato: return new Color(0.95f, 0.95f, 0.95f, 0.38f);     // blanco vacío
                default: return new Color(0.62f, 0.5f, 0.88f, 0.2f);                     // Farol Apagado: violeta suave
            }
        }

        /// <summary>x = capa de esperanza, y = volumen de la música (0 = silencio).</summary>
        private static Vector2 MusicFor(EndingId id)
        {
            switch (id)
            {
                case EndingId.Ceniza:
                case EndingId.ElTrato: return new Vector2(0f, 0f);
                case EndingId.Alba:
                case EndingId.Constelacion: return new Vector2(1f, 1f);
                case EndingId.Vigilia: return new Vector2(0.4f, 0.8f);
                default: return new Vector2(0.6f, 0.7f);
            }
        }

        private static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        private void OnDestroy()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.ClearEndingMusic();
        }
    }
}
