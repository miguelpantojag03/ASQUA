using Ascua.Core;
using Ascua.Lighting;
using Ascua.UI;
using Ascua.World;
using UnityEngine;

namespace Ascua.Combat
{
    /// <summary>
    /// Apagavelas: sombra voladora que NO persigue a Ilse. Va a las luces (farolas y velas de Teo) y las apaga;
    /// cada luz apagada quita 8 de Esperanza a su zona. Obliga a repartirse por el mapa y decidir qué defender.
    /// Ciclo: elegir blanco → volar hasta él → "inhalar" 1.5 s (humo, subtítulo "[Susurro de humo]") →
    /// apagar y bloquear la luz → siguiente blanco.
    ///  - El Farol encendido a menos de 6 m mientras inhala lo espanta: se interrumpe y retrocede 3 s.
    ///  - No ataca a Ilse (contactDamage = 0). 3 de vida: un barrido fuerte o dos normales lo deshacen.
    ///  - No tiene modelo propio: SnuffDirector clona una Polilla, le quita PenumbraMoth y le pone esto
    ///    (SetupFromClone): gris humo y un 30 % más grande.
    /// Todo usa la base ShadowCreature (golpes, feedback, Defeated, Vanish) y Time.deltaTime.
    /// </summary>
    public class Apagavelas : ShadowCreature
    {
        public const string TypeIdValue = "apagavelas";
        /// <summary>"Zona" de sus apariciones: así nunca cuenta como oleada de CombatWaveDirector.</summary>
        public const string SpawnTag = "apagavelas";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
        private static readonly Color SmokeBody = new Color(0.22f, 0.22f, 0.25f, 1f);
        private static readonly Color SmokeAccent = new Color(0.75f, 0.75f, 0.82f, 1f);
        private static Material markerMaterial;
        private static Mesh markerMesh;
        private static bool firstSnuffAnnounced;

        private enum Phase { Seek, Fly, Inhale, Recoil }

        [Header("Apagavelas")]
        [SerializeField] private float flySpeed = 2.2f;
        [SerializeField] private float hoverHeight = 1.8f;
        [SerializeField] private float inhaleSeconds = 1.5f;
        [SerializeField] private float hopeLoss = 8f;
        [Tooltip("El Farol encendido a esta distancia lo espanta mientras inhala.")]
        [SerializeField] private float scareRadius = 6f;
        [SerializeField] private float recoilSeconds = 3f;
        [Tooltip("Sin luces que apagar durante este tiempo, se desvanece.")]
        [SerializeField] private float idleVanishSeconds = 12f;
        [SerializeField] private LayerMask obstacles = 1;

        private Phase phase;
        private float phaseTimer;
        private float idleTimer;
        private SnuffTarget target;
        private LanternController lantern;
        private ParticleSystem smoke;
        private Transform marker;
        private Transform wingLeft, wingRight;
        private MaterialPropertyBlock tintBlock;
        private float flapSeed;

        public bool IsInhaling => phase == Phase.Inhale && IsAlive;
        public SnuffTarget CurrentTarget => target;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            firstSnuffAnnounced = false;
        }

        /// <summary>
        /// Ajustes de un clon de Polilla (lo llama SnuffDirector justo tras AddComponent, con el objeto inactivo).
        /// Los valores van aquí y no en el inspector porque no hay prefab propio.
        /// </summary>
        public void SetupFromClone()
        {
            typeId = TypeIdValue;
            accentColor = SmokeAccent;
            maxHealth = 3;
            energyDrop = 6f;
            contactDamage = 0f;
            centerHeight = 0f;
            hitSquash = 0.25f;

            // Cuerpo: los renderers con el shader de las sombras (como hace el constructor de escenas).
            var body = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Material shared = r.sharedMaterial;
                if (shared != null && shared.shader != null && shared.shader.name == "Ascua/SombraCriatura") body.Add(r);
            }
            bodyRenderers = body.ToArray();
            transform.localScale *= 1.3f;
        }

        protected override void Awake()
        {
            base.Awake();
            typeId = TypeIdValue;
            flapSeed = Random.value * 10f;
            wingLeft = transform.Find("AlaIzq");
            wingRight = transform.Find("AlaDer");
            BuildSmoke();
            BuildMarker();
            ApplyTint();
        }

        protected override void OnSpawned()
        {
            target = null;
            idleTimer = 0f;
            SetPhase(Phase.Seek);
            if (lantern == null) lantern = FindFirstObjectByType<LanternController>();
            ApplyTint();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Release();
        }

        protected override void OnDefeated(HitInfo hit)
        {
            Release();
        }

        protected override void Update()
        {
            base.Update();
            if (Time.deltaTime <= 0f) return;
            // Aleteo lento y pesado (como humo que vuela); aturdido, aún más lento.
            float speed = IsStunned ? 4f : phase == Phase.Inhale ? 3f : 9f;
            float angle = Mathf.Sin((Time.time + flapSeed) * speed) * 40f;
            if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, 0f, -angle);
            UpdateMarker();
        }

        protected override void Tick(float dt)
        {
            phaseTimer += dt;
            switch (phase)
            {
                case Phase.Seek:
                    target = SnuffTarget.PickBest(transform.position, this);
                    if (target == null)
                    {
                        // Nada que apagar: flota en su sitio y, si dura, se va.
                        idleTimer += dt;
                        if (idleTimer >= idleVanishSeconds) Vanish();
                        break;
                    }
                    idleTimer = 0f;
                    target.ClaimedBy = this;
                    SetPhase(Phase.Fly);
                    break;

                case Phase.Fly:
                    if (!TargetStillLit())
                    {
                        Release();
                        SetPhase(Phase.Seek);
                        break;
                    }
                    Vector3 goal = BlowPoint();
                    MoveTowards(goal, flySpeed, dt);
                    if ((transform.position - goal).sqrMagnitude < 0.36f) StartInhale();
                    break;

                case Phase.Inhale:
                    if (!TargetStillLit())
                    {
                        StopSmoke();
                        Release();
                        SetPhase(Phase.Seek);
                        break;
                    }
                    MoveTowards(BlowPoint(), flySpeed * 0.3f, dt);
                    ShowWarning(phaseTimer / inhaleSeconds);
                    if (ScaredByLantern())
                    {
                        // La luz lo espanta: deja la luz a medias y retrocede.
                        StopSmoke();
                        Release();
                        SetPhase(Phase.Recoil);
                        break;
                    }
                    if (phaseTimer >= inhaleSeconds) SnuffNow();
                    break;

                case Phase.Recoil:
                    Vector3 away = transform.position - (player != null ? player.position : transform.position);
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                    MoveTowards(transform.position + away.normalized * 3f + Vector3.up * 0.4f, flySpeed * 1.4f, dt);
                    if (phaseTimer >= recoilSeconds) SetPhase(Phase.Seek);
                    break;
            }
        }

        // ------------------------------------------------------------------ ciclo

        private void StartInhale()
        {
            SetPhase(Phase.Inhale);
            if (smoke != null) smoke.Play();
            EventBus.RaiseAbilityUsed("apagavelas_humo");
            // Señala hacia dónde está pasando, aunque esté fuera de cámara.
            ThreatIndicator indicator = FindFirstObjectByType<ThreatIndicator>();
            if (indicator != null) indicator.PointAt(transform);
        }

        private void SnuffNow()
        {
            StopSmoke();
            SnuffTarget done = target;
            Release();
            SetPhase(Phase.Seek);
            if (done == null) return;

            ZoneHope zone = done.Zone;
            done.Snuff();
            if (zone != null && !zone.IsDead) zone.AddHope(-hopeLoss);

            // La primera vez en la partida, se explica qué pasa.
            if (!firstSnuffAnnounced && zone != null && ZoneLostNotification.Instance != null)
            {
                firstSnuffAnnounced = true;
                ZoneLostNotification.Instance.Show("Algo apaga las luces de " + zone.DisplayName + "...", 3f);
            }
        }

        private bool ScaredByLantern()
        {
            if (lantern == null) lantern = FindFirstObjectByType<LanternController>();
            if (lantern == null || !lantern.IsIlluminating) return false;
            return Vector3.Distance(lantern.LightPosition, transform.position) < scareRadius;
        }

        private bool TargetStillLit()
        {
            return target != null && target.Exists && target.IsLit;
        }

        /// <summary>Junto a la llama, un poco por encima (las velas están en el suelo: ahí se queda a ~1 m).</summary>
        private Vector3 BlowPoint()
        {
            Vector3 p = target.LightPosition;
            float ground = target.BasePosition.y;
            p.y = Mathf.Max(p.y + 0.4f, ground + 1f);
            return p;
        }

        private void MoveTowards(Vector3 goal, float speed, float dt)
        {
            // De camino vuela a su altura (~1.8 m) y solo baja/sube al llegar.
            Vector3 flat = goal - transform.position;
            flat.y = 0f;
            if (flat.magnitude > 2f && target != null) goal.y = Mathf.Max(goal.y, target.BasePosition.y + hoverHeight);

            Vector3 move = Vector3.MoveTowards(transform.position, goal, speed * dt) - transform.position;
            // Evitación mínima (como la Polilla): si hay un muro delante, sube.
            if (move.sqrMagnitude > 0.00001f && Physics.Raycast(transform.position, move.normalized, 0.8f, obstacles, QueryTriggerInteraction.Ignore))
            {
                move = Vector3.up * speed * dt;
            }
            transform.position += move;
            Vector3 look = move;
            look.y = 0f;
            if (look.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-6f * dt));
            }
        }

        private void SetPhase(Phase next)
        {
            phase = next;
            phaseTimer = 0f;
        }

        private void Release()
        {
            if (target != null && target.ClaimedBy == this) target.ClaimedBy = null;
            target = null;
            StopSmoke();
        }

        private void StopSmoke()
        {
            if (smoke != null && smoke.isPlaying) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ------------------------------------------------------------------ aspecto

        /// <summary>Gris humo (MaterialPropertyBlock: no toca el material compartido de las Polillas).</summary>
        private void ApplyTint()
        {
            if (bodyRenderers == null) return;
            tintBlock ??= new MaterialPropertyBlock();
            foreach (Renderer r in bodyRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(tintBlock);
                tintBlock.SetColor(BaseColorId, SmokeBody);
                tintBlock.SetColor(AccentColorId, SmokeAccent);
                r.SetPropertyBlock(tintBlock);
            }
        }

        /// <summary>Humo gris que sale mientras inhala (partículas creadas por código).</summary>
        private void BuildSmoke()
        {
            GameObject smokeObject = new GameObject("Humo");
            smokeObject.transform.SetParent(transform, false);
            smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startColor = new Color(0.55f, 0.55f, 0.6f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = 22f;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ParticleSystem.ColorOverLifetimeModule fade = smoke.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystemRenderer smokeRenderer = smokeObject.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null) smokeRenderer.sharedMaterial = new Material(shader) { name = "Humo_Apagavelas (runtime)" };
        }

        /// <summary>Marcador sobre la luz que está apagando: un aro gris que late (fijo con reducir destellos).</summary>
        private void BuildMarker()
        {
            GameObject markerObject = new GameObject("Apagavelas_Marcador");
            markerObject.AddComponent<MeshFilter>().sharedMesh = MarkerMesh();
            MeshRenderer renderer = markerObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MarkerMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            marker = markerObject.transform;
            markerObject.SetActive(false);
        }

        private void UpdateMarker()
        {
            if (marker == null) return;
            bool show = IsInhaling && target != null;
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;

            // Aro horizontal sobre la luz que mira a cámara de frente cuando se ve desde lejos.
            marker.position = target.LightPosition + Vector3.up * 0.9f;
            Camera cam = Camera.main;
            if (cam != null) marker.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up) * Quaternion.Euler(-90f, 0f, 0f);
            float beat = AccessibilitySettings.ReduceFlashes ? 1f : 1f + 0.25f * Mathf.Sin(Time.time * 6f);
            marker.localScale = Vector3.one * (0.55f * beat);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }

        private static Material MarkerMaterial()
        {
            if (markerMaterial != null) return markerMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            markerMaterial = new Material(shader) { name = "Apagavelas_Marcador (runtime)" };
            Color c = new Color(0.8f, 0.8f, 0.95f) * 1.6f;
            c.a = 1f;
            if (markerMaterial.HasProperty(BaseColorId)) markerMaterial.SetColor(BaseColorId, c);
            if (markerMaterial.HasProperty("_Color")) markerMaterial.SetColor("_Color", c);
            if (markerMaterial.HasProperty("_Cull")) markerMaterial.SetFloat("_Cull", 0f);
            return markerMaterial;
        }

        /// <summary>Aro plano de radio 1 (el mismo tipo de malla que los refugios).</summary>
        private static Mesh MarkerMesh()
        {
            if (markerMesh != null) return markerMesh;
            markerMesh = SafeHaven.SharedRingMesh();
            return markerMesh;
        }
    }
}
