using Ascua.Core;
using Ascua.World;
using UnityEngine;

namespace Ascua.Characters
{
    /// <summary>
    /// Ojos de luz de los vecinos (Mina, Teo) con los mismos 3 estados que Ilse, hechos con
    /// blend shapes reales en una malla generada (AscuaCharacterAssets.EyeMesh):
    ///  - "Alegre": el párpado inferior sube → ojo en arco "^" (zona sana, celebración, Vínculo que sube).
    ///  - "Cansado": el párpado superior baja → ojo entrecerrado (zona por debajo del 30 % o agobio).
    ///  - "Parpadeo": se cierran del todo un instante, al azar.
    /// El brillo de los ojos también sube y baja con el estado (MaterialPropertyBlock, sin duplicar material).
    /// </summary>
    public class NpcFace : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private CharacterAI character;
        [SerializeField] private SkinnedMeshRenderer[] eyes;
        [SerializeField] private Color eyeColor = new Color(1f, 0.93f, 0.8f);
        [SerializeField] private float normalIntensity = 1.4f;
        [SerializeField] private float joyIntensity = 2.6f;
        [SerializeField] private float tiredIntensity = 0.6f;
        [SerializeField, Range(0f, 100f)] private float tiredHope = 30f;
        [SerializeField, Range(0f, 100f)] private float joyfulHope = 78f;
        [SerializeField] private float joyBurstSeconds = 2.5f;
        [SerializeField] private Vector2 blinkInterval = new Vector2(2.5f, 5.5f);
        [SerializeField] private float transitionSpeed = 5f;

        private int joyIndex = -1, tiredIndex = -1, blinkIndex = -1;
        private float joy, tired, blink;
        private float joyBurst;
        private float blinkTimer;
        private float blinkPhase = -1f;
        private string npcId;
        private MaterialPropertyBlock block;

        /// <summary>Estado dominante (para pruebas y depuración).</summary>
        public string MoodName => joy > 0.5f ? "Alegre" : tired > 0.5f ? "Cansado" : "Normal";

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (character == null) character = GetComponent<CharacterAI>();
            NpcIdentity identity = GetComponent<NpcIdentity>();
            npcId = identity != null ? identity.NpcId : null;

            if (eyes != null && eyes.Length > 0 && eyes[0] != null && eyes[0].sharedMesh != null)
            {
                Mesh mesh = eyes[0].sharedMesh;
                joyIndex = mesh.GetBlendShapeIndex("Alegre");
                tiredIndex = mesh.GetBlendShapeIndex("Cansado");
                blinkIndex = mesh.GetBlendShapeIndex("Parpadeo");
            }
            blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        }

        private void OnEnable()
        {
            EventBus.HopeLevelReached += OnHopeLevel;
            EventBus.BondChanged += OnBond;
            EventBus.WaveCleared += OnWaveCleared;
        }

        private void OnDisable()
        {
            EventBus.HopeLevelReached -= OnHopeLevel;
            EventBus.BondChanged -= OnBond;
            EventBus.WaveCleared -= OnWaveCleared;
        }

        private void OnHopeLevel(string zoneId, int level, Vector3 position)
        {
            if (character != null && character.Zone != null && character.Zone.ZoneId == zoneId) joyBurst = joyBurstSeconds;
        }

        private void OnBond(string id, int level)
        {
            if (id == npcId) joyBurst = joyBurstSeconds;
        }

        private void OnWaveCleared(string zoneId, string rating)
        {
            if (character != null && character.Zone != null && character.Zone.ZoneId == zoneId) joyBurst = joyBurstSeconds;
        }

        /// <summary>Alegría inmediata (diálogos, pruebas).</summary>
        public void TriggerJoy(float seconds)
        {
            joyBurst = Mathf.Max(joyBurst, seconds);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (joyBurst > 0f) joyBurst -= dt;

            // Estado objetivo según la Esperanza de su zona y su ánimo.
            ZoneHope zone = character != null ? character.Zone : null;
            float hope = zone != null ? zone.Hope : 60f;
            bool worried = character != null && character.Mood != CharacterMood.Calm;
            float targetTired = (hope < tiredHope || worried) && joyBurst <= 0f ? 1f : 0f;
            float targetJoy = joyBurst > 0f || (hope >= joyfulHope && !worried) ? 1f : 0f;

            float k = 1f - Mathf.Exp(-transitionSpeed * dt);
            joy = Mathf.Lerp(joy, targetJoy, k);
            tired = Mathf.Lerp(tired, targetTired, k);

            UpdateBlink(dt);

            float intensity = normalIntensity;
            intensity = Mathf.Lerp(intensity, joyIntensity, joy);
            intensity = Mathf.Lerp(intensity, tiredIntensity, tired);

            if (eyes == null) return;
            foreach (SkinnedMeshRenderer eye in eyes)
            {
                if (eye == null) continue;
                if (joyIndex >= 0) eye.SetBlendShapeWeight(joyIndex, joy * 100f);
                if (tiredIndex >= 0) eye.SetBlendShapeWeight(tiredIndex, tired * 100f);
                if (blinkIndex >= 0) eye.SetBlendShapeWeight(blinkIndex, blink * 100f);
                eye.GetPropertyBlock(block ??= new MaterialPropertyBlock());
                block.SetColor(EmissionColorId, eyeColor * intensity);
                eye.SetPropertyBlock(block);
            }
        }

        private void UpdateBlink(float dt)
        {
            if (blinkPhase < 0f)
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f) blinkPhase = 0f;
                blink = 0f;
                return;
            }
            blinkPhase += dt / 0.14f;
            blink = Mathf.Sin(Mathf.Clamp01(blinkPhase) * Mathf.PI);
            if (blinkPhase >= 1f)
            {
                blinkPhase = -1f;
                blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
            }
        }
    }
}
