using System.Collections.Generic;
using Ascua.Characters;
using Ascua.Combat;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Player;
using Ascua.World;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Combate de luz y mejora gráfica integral: rig de combate de Ilse, criaturas (pools),
    /// Centinela, Recuerdos, efectos, director de oleadas, HUD de combate, vecinos con ojos de luz,
    /// carrito de Teo, post-procesado dinámico, sonda de reflejos y configuración de sombras/SSAO.
    /// </summary>
    public static partial class AscuaSceneBuilder
    {
        private static readonly Color VioletLight = new Color(0.55f, 0.4f, 1f);

        // ================================================================== Ilse: rig de combate

        private static void AddPlayerCombat(AscuaAssets assets, ChibiParts parts, PlayerInputReader input, PlayerController controller,
            PlayerEnergy energy, LanternController lantern, Transform lanternPivot)
        {
            CombatAssets c = assets.Combat;
            AscuaMaterials m = assets.Materials;
            GameObject player = parts.Root;

            // --- Daño recibido, i-frames, esquiva perfecta y contraataque.
            CinemachineImpulseSource impulse = player.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseDuration = 0.25f;
            PlayerCombatState combat = player.AddComponent<PlayerCombatState>();
            Wire(combat, "energy", energy);
            Wire(combat, "controller", controller);
            Wire(combat, "lantern", lantern);
            Wire(combat, "impulse", impulse);
            Wire(player.GetComponent<PlayerDash>(), "combatState", combat);

            // --- Barrido de luz: arco plano a la altura del pecho, haz recto y destello.
            Transform sweepPivot = Pivot("Barrido_Pivote", player.transform, new Vector3(0f, 0.95f, 0.1f));
            GameObject arc = MeshObject("Barrido_Arco", sweepPivot, Vector3.zero, Vector3.one, c.ArcMesh, c.Sweep);
            GameObject beam = MeshObject("Haz_Cargado", player.transform, new Vector3(0f, 0.9f, 0.35f), Vector3.one, c.BeamMesh, c.Beam);
            Light sweepLight = new GameObject("Luz_Barrido").AddComponent<Light>();
            sweepLight.transform.SetParent(player.transform, false);
            sweepLight.transform.localPosition = new Vector3(0f, 1f, 0.9f);
            sweepLight.type = LightType.Point;
            sweepLight.color = AscuaPalette.Ambar;
            sweepLight.range = 4.5f;
            sweepLight.intensity = 0f;
            sweepLight.shadows = LightShadows.None;

            GameObject chargeObject = new GameObject("Carga_Lupa");
            chargeObject.transform.SetParent(lanternPivot, false);
            chargeObject.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            ParticleSystem charge = chargeObject.AddComponent<ParticleSystem>();
            ConfigureFireflyParticles(charge, m.FireflyParticle);
            ParticleSystem.MainModule chargeMain = charge.main;
            chargeMain.startColor = new Color(1f, 0.85f, 0.55f);
            chargeMain.playOnAwake = false;
            chargeMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule chargeEmission = charge.emission;
            chargeEmission.rateOverTime = 40f;
            ParticleSystem.ShapeModule chargeShape = charge.shape;
            chargeShape.radius = 0.4f;
            charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // --- Vara de luz (oculta hasta 3 oleadas defendidas).
            Transform staff = Pivot("Vara_Luz", lanternPivot, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Vara", staff, new Vector3(0f, 0.15f, 0f), new Vector3(0.045f, 0.65f, 0.045f), c.Staff, Col.None, null, false);
            Prim(PrimitiveType.Sphere, "Corona", staff, new Vector3(0f, 0.85f, 0f), Vector3.one * 0.12f, c.StaffCore, Col.None, null, false);
            Light staffLight = new GameObject("Luz_Vara").AddComponent<Light>();
            staffLight.transform.SetParent(staff, false);
            staffLight.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            staffLight.type = LightType.Point;
            staffLight.color = AscuaPalette.Ambar;
            staffLight.range = 3f;
            staffLight.intensity = 0.8f;
            staffLight.shadows = LightShadows.None;
            GameObject evolveObject = new GameObject("Evolucion_Vara");
            evolveObject.transform.SetParent(staff, false);
            evolveObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            ParticleSystem evolve = evolveObject.AddComponent<ParticleSystem>();
            ConfigureFireflyParticles(evolve, m.FireflyParticle);
            ParticleSystem.MainModule evolveMain = evolve.main;
            evolveMain.startColor = AscuaPalette.Ambar;
            evolveMain.playOnAwake = false;
            evolveMain.maxParticles = 80;
            ParticleSystem.EmissionModule evolveEmission = evolve.emission;
            evolveEmission.rateOverTime = 60f;
            ParticleSystem.ShapeModule evolveShape = evolve.shape;
            evolveShape.shapeType = ParticleSystemShapeType.Circle;
            evolveShape.radius = 0.5f;
            evolve.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Light evolveLight = new GameObject("Luz_Evolucion").AddComponent<Light>();
            evolveLight.transform.SetParent(player.transform, false);
            evolveLight.transform.localPosition = new Vector3(0.3f, 1.4f, 0.3f);
            evolveLight.type = LightType.Point;
            evolveLight.color = AscuaPalette.Ambar;
            evolveLight.range = 6f;
            evolveLight.intensity = 0f;
            evolveLight.shadows = LightShadows.None;

            LanternStaff lanternStaff = player.AddComponent<LanternStaff>();
            Wire(lanternStaff, "staffVisual", staff);
            Wire(lanternStaff, "evolveParticles", evolve);
            Wire(lanternStaff, "evolveLight", evolveLight);

            LightAttack attack = player.AddComponent<LightAttack>();
            Wire(attack, "input", input);
            Wire(attack, "controller", controller);
            Wire(attack, "energy", energy);
            Wire(attack, "combatState", combat);
            Wire(attack, "staff", lanternStaff);
            Wire(attack, "animator", parts.Animator);
            Wire(attack, "sweepPivot", sweepPivot);
            Wire(attack, "sweepRenderer", arc.GetComponent<Renderer>());
            Wire(attack, "beamRenderer", beam.GetComponent<Renderer>());
            Wire(attack, "flashLight", sweepLight);
            Wire(attack, "chargeParticles", charge);

            // --- Habilidades de Vínculo 5 (objetos en la raíz de la escena: se mueven por el mundo).
            GameObject bombObject = new GameObject("Vela_Teo_Explosiva");
            Transform candle = Pivot("Vela", bombObject.transform, Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Cera", candle, Vector3.zero, new Vector3(0.09f, 0.12f, 0.09f), assets.Character.CandleWax, Col.None, null, false);
            Prim(PrimitiveType.Sphere, "Llama", candle, new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.1f, 0.06f), assets.Character.CandleFlame, Col.None, null, false);
            ParticleSystem bombBurst = new GameObject("Estallido").AddComponent<ParticleSystem>();
            bombBurst.transform.SetParent(bombObject.transform, false);
            ConfigureBurst(bombBurst, m.FireflyParticle, new Color(1f, 0.75f, 0.45f), 0.16f, 6f, 1.1f, 80, -0.05f);
            ParticleSystem.ShapeModule bombShape = bombBurst.shape;
            bombShape.shapeType = ParticleSystemShapeType.Sphere;
            bombShape.radius = 0.4f;
            Light bombLight = new GameObject("Luz_Estallido").AddComponent<Light>();
            bombLight.transform.SetParent(bombObject.transform, false);
            bombLight.type = LightType.Point;
            bombLight.color = new Color(1f, 0.72f, 0.42f);
            bombLight.range = 8f;
            bombLight.intensity = 0f;
            bombLight.shadows = LightShadows.None;
            TeoCandleBomb bomb = bombObject.AddComponent<TeoCandleBomb>();
            Wire(bomb, "burst", bombBurst);
            Wire(bomb, "burstLight", bombLight);
            Wire(bomb, "candleVisual", candle);
            SetLayerRecursively(bombObject, IgnoreRaycastLayer);

            GameObject brasaObject = new GameObject("Brasa_Guardiana");
            Prim(PrimitiveType.Sphere, "Brillo", brasaObject.transform, Vector3.zero, Vector3.one * 0.1f, assets.Character.CandleFlame, Col.None, null, false);
            ParticleSystem brasaTrail = new GameObject("Estela").AddComponent<ParticleSystem>();
            brasaTrail.transform.SetParent(brasaObject.transform, false);
            ConfigureFireflyParticles(brasaTrail, m.FireflyParticle);
            ParticleSystem.MainModule brasaMain = brasaTrail.main;
            brasaMain.startColor = Ember;
            SmallGlow(brasaObject.transform, Ember, 1f, 2.5f);
            BrasaGuardian guardian = brasaObject.AddComponent<BrasaGuardian>();
            Wire(guardian, "orbitCenter", player.transform);
            Wire(guardian, "trail", brasaTrail);
            SetLayerRecursively(brasaObject, IgnoreRaycastLayer);

            BondCombatAbilities abilities = player.AddComponent<BondCombatAbilities>();
            Wire(abilities, "input", input);
            Wire(abilities, "energy", energy);
            Wire(abilities, "combatState", combat);
            Wire(abilities, "candleBomb", bomb);
            Wire(abilities, "brasaGuardian", guardian);
        }

        // ================================================================== Mundo de combate

        /// <summary>Efectos, Recuerdos, pools de criaturas, Centinela y director de oleadas.</summary>
        private static void BuildCombatWorld(AscuaAssets assets)
        {
            CombatAssets c = assets.Combat;
            AscuaMaterials m = assets.Materials;
            assetsFireflyParticle = m.FireflyParticle;

            // --- Efectos compartidos.
            GameObject fxObject = new GameObject("Combate_Efectos");
            ParticleSystem dissolve = FxSystem("Disolucion", fxObject.transform, m.FireflyParticle, 0.12f, 1.6f, 1.4f, 300, -0.35f, 0.5f);
            ParticleSystem sparks = FxSystem("Chispas_Impacto", fxObject.transform, m.FireflyParticle, 0.07f, 4f, 0.35f, 120, 0.2f, 0.15f);
            ParticleSystem ring = FxSystem("Anillo_Esquiva", fxObject.transform, m.FireflyParticle, 0.09f, 5f, 0.5f, 120, 0f, 0.3f);
            ParticleSystem.ShapeModule ringShape = ring.shape;
            ringShape.shapeType = ParticleSystemShapeType.Circle;
            ringShape.rotation = new Vector3(90f, 0f, 0f);
            Light fxLight = new GameObject("Destello").AddComponent<Light>();
            fxLight.transform.SetParent(fxObject.transform, false);
            fxLight.type = LightType.Point;
            fxLight.range = 7f;
            fxLight.shadows = LightShadows.None;
            CombatFx fx = fxObject.AddComponent<CombatFx>();
            Wire(fx, "dissolveBurst", dissolve);
            Wire(fx, "impactSparks", sparks);
            Wire(fx, "dodgeRing", ring);
            Wire(fx, "flashLight", fxLight);

            // --- Recuerdos (desactivados: los suelta la primera Sombra Mayor / el primer Centinela).
            Transform recuerdos = new GameObject("Recuerdos").transform;
            PickupItem lupa = CreatePickup("Recuerdo_LupaMina", recuerdos, Vector3.zero, out Transform lupaVisual);
            Prim(PrimitiveType.Cylinder, "Lente", lupaVisual, Vector3.zero, new Vector3(0.22f, 0.012f, 0.22f), c.Brass, Col.None, Quaternion.Euler(90f, 0f, 0f), false);
            Prim(PrimitiveType.Cube, "Mango", lupaVisual, new Vector3(0f, -0.2f, 0f), new Vector3(0.04f, 0.2f, 0.04f), c.Brass, Col.None, null, false);
            lupa.Configure(LightAttack.LupaMinaId, "La lupa de Mina", PickupKind.Recuerdo, new Color(1f, 0.78f, 0.4f), null, LightAttack.LupaMinaId);
            SmallGlow(lupa.transform, new Color(1f, 0.8f, 0.45f), 1.2f, 2.5f);
            PickupItem reloj = CreatePickup("Recuerdo_RelojNadir", recuerdos, Vector3.zero, out Transform relojVisual);
            Prim(PrimitiveType.Cylinder, "Esfera", relojVisual, Vector3.zero, new Vector3(0.2f, 0.03f, 0.2f), c.Silver, Col.None, Quaternion.Euler(90f, 0f, 0f), false);
            Prim(PrimitiveType.Sphere, "Corona", relojVisual, new Vector3(0f, 0.13f, 0f), Vector3.one * 0.05f, c.Silver, Col.None, null, false);
            reloj.Configure(PlayerCombatState.RelojNadirId, "El reloj de Nadir", PickupKind.Recuerdo, new Color(0.8f, 0.88f, 1f), null, PlayerCombatState.RelojNadirId);
            SmallGlow(reloj.transform, new Color(0.8f, 0.88f, 1f), 1.2f, 2.5f);
            SetLayerRecursively(recuerdos.gameObject, IgnoreRaycastLayer);
            lupa.gameObject.SetActive(false);
            reloj.gameObject.SetActive(false);

            // --- Pools de criaturas (todas desactivadas; nada se instancia jugando).
            Transform pool = new GameObject("Sombras_Pool").transform;
            List<PenumbraMoth> moths = new List<PenumbraMoth>();
            for (int i = 0; i < 7; i++) moths.Add(BuildMoth(pool, c, i));
            List<ShadowBrute> brutes = new List<ShadowBrute>();
            for (int i = 0; i < 2; i++) brutes.Add(BuildBrute(pool, c, "Sombra_Mayor_" + i, false, lupa));
            List<ShadowBrute> veiled = new List<ShadowBrute> { BuildBrute(pool, c, "Sombra_Velada_0", true, lupa) };
            List<WhisperWraith> whispers = new List<WhisperWraith>();
            for (int i = 0; i < 2; i++) whispers.Add(BuildWhisper(pool, c, assets.Character, i));
            List<EchoShade> copies = new List<EchoShade>();
            for (int i = 0; i < 4; i++) copies.Add(BuildEcho(pool, c, "Eco_Copia_" + i, true, null));
            List<EchoShade> echoes = new List<EchoShade>();
            for (int i = 0; i < 2; i++) echoes.Add(BuildEcho(pool, c, "Eco_" + i, false, copies.ToArray()));

            CombatWaveDirector director = new GameObject("Combate_Oleadas").AddComponent<CombatWaveDirector>();
            WireArray(director, "moths", moths);
            WireArray(director, "brutes", brutes);
            WireArray(director, "veiledBrutes", veiled);
            WireArray(director, "whispers", whispers);
            WireArray(director, "echoes", echoes);

            // --- Centinela del mapa: en la calle entre La Plaza y El Mercado. El quiosco (x -31.7…-28.3)
            // es la cobertura natural al acercarse desde La Plaza.
            BuildSentinel(c, reloj, new Vector3(-36f, 0.05f, -12.2f), 90f);
        }

        private static ParticleSystem FxSystem(string name, Transform parent, Material material, float size, float speed, float lifetime, int max, float gravity, float radius)
        {
            ParticleSystem ps = new GameObject(name).AddComponent<ParticleSystem>();
            ps.transform.SetParent(parent, false);
            ConfigureBurst(ps, material, Color.white, size, speed, lifetime, max, gravity);
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            return ps;
        }

        /// <summary>Raíz común: componente de criatura, renderers del cuerpo y capa sin colisión con la cámara.</summary>
        private static T FinishCreature<T>(GameObject root, T creature, float centerHeight, string typeId) where T : ShadowCreature
        {
            List<Renderer> body = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Material shared = r.sharedMaterial;
                if (shared != null && shared.shader != null && shared.shader.name == "Ascua/SombraCriatura") body.Add(r);
            }
            WireArray(creature, "bodyRenderers", body);
            SerializedObject so = new SerializedObject(creature);
            so.FindProperty("centerHeight").floatValue = centerHeight;
            so.FindProperty("typeId").stringValue = typeId;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetLayerRecursively(root, IgnoreRaycastLayer);
            root.SetActive(false);
            return creature;
        }

        private static NavMeshAgent CreatureAgent(GameObject root, float radius, float height)
        {
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.angularSpeed = 540f;
            agent.acceleration = 14f;
            agent.autoBraking = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.enabled = false;
            return agent;
        }

        private static PenumbraMoth BuildMoth(Transform pool, CombatAssets c, int index)
        {
            GameObject root = new GameObject("Polilla_" + index);
            root.transform.SetParent(pool, false);
            Prim(PrimitiveType.Capsule, "Cuerpo", root.transform, Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), c.ShadowBody, Col.None, Quaternion.Euler(90f, 0f, 0f), true);
            Transform wingL = Pivot("AlaIzq", root.transform, new Vector3(-0.06f, 0.02f, 0f));
            Prim(PrimitiveType.Cube, "Ala", wingL, new Vector3(-0.22f, 0f, -0.02f), new Vector3(0.42f, 0.02f, 0.3f), c.ShadowBody, Col.None, Quaternion.Euler(0f, 12f, 0f), true);
            Transform wingR = Pivot("AlaDer", root.transform, new Vector3(0.06f, 0.02f, 0f));
            Prim(PrimitiveType.Cube, "Ala", wingR, new Vector3(0.22f, 0f, -0.02f), new Vector3(0.42f, 0.02f, 0.3f), c.ShadowBody, Col.None, Quaternion.Euler(0f, -12f, 0f), true);
            Prim(PrimitiveType.Sphere, "Ojos", root.transform, new Vector3(0f, 0.03f, 0.13f), new Vector3(0.1f, 0.03f, 0.03f), c.ShadowAccent, Col.None, null, false);

            PenumbraMoth moth = root.AddComponent<PenumbraMoth>();
            Wire(moth, "leftWing", wingL);
            Wire(moth, "rightWing", wingR);
            return FinishCreature(root, moth, 0f, "polilla");
        }

        private static ShadowBrute BuildBrute(Transform pool, CombatAssets c, string name, bool veiled, PickupItem drop)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(pool, false);
            Material body = veiled ? c.ShadowVeiled : c.ShadowBody;
            Transform shape = Pivot("Cuerpo", root.transform, Vector3.zero);
            Prim(PrimitiveType.Sphere, "Torso", shape, new Vector3(0f, 1.05f, 0f), new Vector3(1.3f, 1.5f, 1.1f), body, Col.None, null, true);
            Prim(PrimitiveType.Sphere, "Cabeza", shape, new Vector3(0f, 1.85f, 0.25f), new Vector3(0.62f, 0.55f, 0.6f), body, Col.None, null, true);
            Prim(PrimitiveType.Capsule, "Brazo", shape, new Vector3(-0.78f, 0.75f, 0.1f), new Vector3(0.36f, 0.62f, 0.36f), body, Col.None, Quaternion.Euler(0f, 0f, -10f), true);
            Prim(PrimitiveType.Capsule, "Brazo", shape, new Vector3(0.78f, 0.75f, 0.1f), new Vector3(0.36f, 0.62f, 0.36f), body, Col.None, Quaternion.Euler(0f, 0f, 10f), true);
            Prim(PrimitiveType.Cube, "Ojos", shape, new Vector3(0f, 1.9f, 0.53f), new Vector3(0.36f, 0.05f, 0.04f), c.ShadowAccent, Col.None, null, false);

            // Franja de aviso de la embestida: se estira en Z durante el telegrafiado.
            Transform stripe = Pivot("Aviso_Embestida", root.transform, new Vector3(0f, 0.06f, 0.4f));
            GameObject quad = Prim(PrimitiveType.Quad, "Franja", stripe, new Vector3(0f, 0f, 0.5f), new Vector3(1.2f, 1f, 1f), c.TelegraphStripe, Col.None, Quaternion.Euler(90f, 0f, 0f), false);
            quad.GetComponent<MeshRenderer>().receiveShadows = false;

            CreatureAgent(root, 0.55f, 2.1f);
            ShadowBrute brute = root.AddComponent<ShadowBrute>();
            Wire(brute, "telegraphStripe", stripe);
            Wire(brute, "body", shape);
            Wire(brute, "recuerdoDrop", drop);
            SerializedObject so = new SerializedObject(brute);
            so.FindProperty("maxHealth").intValue = 7;
            so.FindProperty("energyDrop").floatValue = 12f;
            so.FindProperty("contactDamage").floatValue = 8f;
            so.FindProperty("contactRange").floatValue = 1.3f;
            so.FindProperty("knockbackResistance").floatValue = 0.6f;
            so.FindProperty("accentColor").colorValue = veiled ? new Color(1f, 0.6f, 0.9f) : VioletLight;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (veiled) root.AddComponent<LitVulnerable>();
            return FinishCreature(root, brute, 1.1f, veiled ? "sombra_velada" : "sombra_mayor");
        }

        private static WhisperWraith BuildWhisper(Transform pool, CombatAssets c, CharacterAssets ch, int index)
        {
            GameObject root = new GameObject("Susurro_" + index);
            root.transform.SetParent(pool, false);
            Transform wisp = Pivot("Voz", root.transform, new Vector3(0f, 0.9f, 0f));
            Prim(PrimitiveType.Sphere, "Cuerpo", wisp, Vector3.zero, new Vector3(0.36f, 0.7f, 0.36f), c.WhisperBody, Col.None, null, true);
            Prim(PrimitiveType.Sphere, "Cola", wisp, new Vector3(0f, -0.42f, -0.12f), new Vector3(0.2f, 0.4f, 0.2f), c.WhisperBody, Col.None, Quaternion.Euler(-20f, 0f, 0f), true);
            Prim(PrimitiveType.Sphere, "Ojos", wisp, new Vector3(0f, 0.16f, 0.16f), new Vector3(0.16f, 0.035f, 0.03f), c.ShadowAccent, Col.None, null, false);
            GameObject page = Prim(PrimitiveType.Cube, "Pagina_Robada", wisp, new Vector3(0f, -0.1f, 0.22f), new Vector3(0.2f, 0.26f, 0.012f), ch.Paper, Col.None, Quaternion.Euler(0f, 0f, 12f), false);
            page.SetActive(false);
            ParticleSystem trail = new GameObject("Estela").AddComponent<ParticleSystem>();
            trail.transform.SetParent(wisp, false);
            ConfigureFireflyParticles(trail, assetsFireflyParticle);
            ParticleSystem.MainModule trailMain = trail.main;
            trailMain.startColor = new Color(0.6f, 0.75f, 1f, 0.7f);

            CreatureAgent(root, 0.3f, 1.4f);
            WhisperWraith whisper = root.AddComponent<WhisperWraith>();
            Wire(whisper, "carriedPage", page);
            Wire(whisper, "wisp", wisp);
            SerializedObject so = new SerializedObject(whisper);
            so.FindProperty("maxHealth").intValue = 2;
            so.FindProperty("energyDrop").floatValue = 5f;
            so.FindProperty("accentColor").colorValue = new Color(0.6f, 0.8f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return FinishCreature(root, whisper, 0.9f, "susurro");
        }

        private static EchoShade BuildEcho(Transform pool, CombatAssets c, string name, bool copy, EchoShade[] splitPool)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(pool, false);
            if (copy) root.transform.localScale = Vector3.one * 0.6f;
            Transform body = Pivot("Reflejo", root.transform, new Vector3(0f, 0.9f, 0f));
            Prim(PrimitiveType.Cube, "Cuerpo", body, Vector3.zero, new Vector3(0.62f, 0.9f, 0.62f), c.EchoBody, Col.None, Quaternion.Euler(0f, 45f, 0f), true);
            Prim(PrimitiveType.Cube, "Doble", body, new Vector3(0.12f, 0.05f, -0.06f), new Vector3(0.5f, 0.8f, 0.5f), c.EchoBody, Col.None, Quaternion.Euler(0f, 20f, 0f), true);
            Prim(PrimitiveType.Sphere, "Nucleo", body, new Vector3(0f, 0.15f, 0.3f), new Vector3(0.14f, 0.06f, 0.05f), c.ShadowAccent, Col.None, null, false);

            CreatureAgent(root, copy ? 0.22f : 0.4f, copy ? 1.1f : 1.7f);
            EchoShade echo = root.AddComponent<EchoShade>();
            Wire(echo, "body", body);
            SerializedObject so = new SerializedObject(echo);
            so.FindProperty("isSplitCopy").boolValue = copy;
            so.FindProperty("maxHealth").intValue = copy ? 1 : 3;
            so.FindProperty("moveSpeed").floatValue = copy ? 3.8f : 2.6f;
            so.FindProperty("contactDamage").floatValue = copy ? 3f : 5f;
            so.FindProperty("energyDrop").floatValue = copy ? 3f : 6f;
            so.FindProperty("accentColor").colorValue = new Color(0.7f, 0.6f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (splitPool != null) WireArray(echo, "splitPool", splitPool);
            return FinishCreature(root, echo, 0.9f, copy ? "eco_copia" : "eco");
        }

        private static void BuildSentinel(CombatAssets c, PickupItem drop, Vector3 position, float yaw)
        {
            GameObject root = new GameObject("Centinela_Penumbra");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.9f, 0.1f, 0.9f), c.SentinelBody, Col.None, null, true);
            Prim(PrimitiveType.Cube, "Obelisco", root.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 2.1f, 0.5f), c.SentinelBody, Col.None, Quaternion.Euler(0f, 45f, 0f), true);
            Transform eye = Pivot("Ojo", root.transform, new Vector3(0f, 2.65f, 0f));
            Prim(PrimitiveType.Cube, "Gema", eye, Vector3.zero, Vector3.one * 0.32f, c.ShadowAccent, Col.None, Quaternion.Euler(45f, 0f, 45f), false);
            Light eyeLight = new GameObject("Luz_Ojo").AddComponent<Light>();
            eyeLight.transform.SetParent(eye, false);
            eyeLight.type = LightType.Point;
            eyeLight.color = VioletLight;
            eyeLight.range = 4f;
            eyeLight.intensity = 1.2f;
            eyeLight.shadows = LightShadows.None;
            BoxCollider block = root.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 1.2f, 0f);
            block.size = new Vector3(0.8f, 2.4f, 0.8f);

            // Proyectiles (pool propio).
            Transform boltsRoot = new GameObject("Proyectiles_Centinela").transform;
            List<ShadowBolt> bolts = new List<ShadowBolt>();
            for (int i = 0; i < 6; i++)
            {
                GameObject bolt = new GameObject("Proyectil_" + i);
                bolt.transform.SetParent(boltsRoot, false);
                Prim(PrimitiveType.Sphere, "Nucleo", bolt.transform, Vector3.zero, Vector3.one * 0.24f, c.ShadowBolt, Col.None, null, false);
                ParticleSystem trail = new GameObject("Estela").AddComponent<ParticleSystem>();
                trail.transform.SetParent(bolt.transform, false);
                ConfigureFireflyParticles(trail, assetsFireflyParticle);
                ParticleSystem.MainModule main = trail.main;
                main.startColor = new Color(0.6f, 0.35f, 1f);
                main.playOnAwake = false;
                ParticleSystem.EmissionModule emission = trail.emission;
                emission.rateOverTime = 30f;
                ShadowBolt component = bolt.AddComponent<ShadowBolt>();
                Wire(component, "trail", trail);
                SetLayerRecursively(bolt, IgnoreRaycastLayer);
                bolt.SetActive(false);
                bolts.Add(component);
            }

            PenumbraSentinel sentinel = root.AddComponent<PenumbraSentinel>();
            Wire(sentinel, "eye", eye);
            Wire(sentinel, "recuerdoDrop", drop);
            WireArray(sentinel, "bolts", bolts);
            SerializedObject so = new SerializedObject(sentinel);
            so.FindProperty("maxHealth").intValue = 5;
            so.FindProperty("energyDrop").floatValue = 10f;
            so.FindProperty("contactDamage").floatValue = 0f;
            so.FindProperty("accentColor").colorValue = VioletLight;
            so.ApplyModifiedPropertiesWithoutUndo();
            FinishCreature(root, sentinel, 1.4f, "centinela");
            // El Centinela del mapa SÍ está activo desde el principio (Start lo hace aparecer).
            root.SetActive(true);
        }

        /// <summary>Material de partículas compartido (lo fija BuildCombatWorld antes de crear criaturas).</summary>
        private static Material assetsFireflyParticle;

        // ================================================================== HUD de combate

        private static void BuildCombatHud(Canvas canvas, GameObject player, AscuaAssets assets)
        {
            Transform root = canvas.transform;
            CombatAssets c = assets.Combat;

            // Viñeta de daño (rojo-violeta), por debajo del resto del HUD.
            RectTransform vignetteRect = UIRect("Vineta_Dano", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            vignetteRect.SetSiblingIndex(1);
            Image vignetteImage = vignetteRect.gameObject.AddComponent<Image>();
            vignetteImage.sprite = assets.Character.VignetteSprite;
            vignetteImage.raycastTarget = false;
            CanvasGroup vignetteGroup = vignetteRect.gameObject.AddComponent<CanvasGroup>();
            vignetteGroup.alpha = 0f;
            vignetteGroup.blocksRaycasts = false;
            DamageVignette damage = vignetteRect.gameObject.AddComponent<DamageVignette>();
            Wire(damage, "group", vignetteGroup);
            Wire(damage, "image", vignetteImage);
            Wire(damage, "energy", player.GetComponent<PlayerEnergy>());

            // Combo: 3 puntos de luz bajo el centro.
            RectTransform comboRect = UIRect("Combo", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(120f, 30f));
            CanvasGroup comboGroup = comboRect.gameObject.AddComponent<CanvasGroup>();
            comboGroup.alpha = 0f;
            comboGroup.blocksRaycasts = false;
            Image[] dots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform dot = UIRect("Punto_" + i, comboRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 34f, 0f), new Vector2(18f, 18f));
                dots[i] = dot.gameObject.AddComponent<Image>();
                dots[i].sprite = c.DotSprite;
                dots[i].raycastTarget = false;
            }
            ComboHUD combo = comboRect.gameObject.AddComponent<ComboHUD>();
            Wire(combo, "attack", player.GetComponent<LightAttack>());
            Wire(combo, "group", comboGroup);
            WireArray(combo, "dots", dots);

            // Habilidades de combate en la fila del Farol ("VELA · G · CARGA · CONTRA · VARA").
            Transform energyPanel = root.Find("Energia");
            if (energyPanel != null)
            {
                TextMeshProUGUI label = UIText("Habilidades", energyPanel, "", 16f, new Color(1f, 0.85f, 0.6f, 0.9f), TextAlignmentOptions.BottomLeft,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f + 340f, 0f), new Vector2(340f, 28f));
                label.characterSpacing = 5f;
                CombatAbilityHUD abilitiesHud = label.gameObject.AddComponent<CombatAbilityHUD>();
                Wire(abilitiesHud, "label", label);
                Wire(abilitiesHud, "abilities", player.GetComponent<BondCombatAbilities>());
                Wire(abilitiesHud, "staff", player.GetComponent<LanternStaff>());
            }
        }

        // ================================================================== Vecinos: ojos de luz y carrito

        /// <summary>Dos ojos de luz con blend shapes y el componente que los anima según su zona.</summary>
        private static void AddNpcEyes(ChibiParts parts, CombatAssets c, float headSize, Color eyeColor)
        {
            float centerY = headSize * 0.5f - 0.02f;
            float r = headSize * 0.5f;
            List<SkinnedMeshRenderer> eyes = new List<SkinnedMeshRenderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject eye = new GameObject(side < 0 ? "Ojo_Izq" : "Ojo_Der");
                eye.transform.SetParent(parts.Head, false);
                float x = side * r * 0.36f;
                float y = centerY + r * 0.12f;
                float z = Mathf.Sqrt(Mathf.Max(0.01f, r * r - x * x - (y - centerY) * (y - centerY))) - 0.004f;
                eye.transform.localPosition = new Vector3(x, y, z);
                eye.transform.localRotation = Quaternion.Euler(0f, side * 18f, side * -6f);
                eye.transform.localScale = new Vector3(headSize * 0.17f, headSize * 0.24f, headSize * 0.12f);
                SkinnedMeshRenderer smr = eye.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = c.EyeMesh;
                smr.sharedMaterial = c.NpcEye;
                smr.shadowCastingMode = ShadowCastingMode.Off;
                smr.receiveShadows = false;
                smr.updateWhenOffscreen = false;
                eyes.Add(smr);
            }
            NpcFace face = parts.Root.AddComponent<NpcFace>();
            Wire(face, "character", parts.Root.GetComponent<CharacterAI>());
            WireArray(face, "eyes", eyes);
            SerializedObject so = new SerializedObject(face);
            so.FindProperty("eyeColor").colorValue = eyeColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>El carrito de Teo: su silueta distintiva junto a los puestos (sirve también de cobertura).</summary>
        private static void BuildTeoCart(AscuaAssets assets, Vector3 position, float yaw)
        {
            AscuaMaterials m = assets.Materials;
            CombatAssets c = assets.Combat;
            Transform cart = new GameObject("Carrito_Teo").transform;
            cart.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Prim(PrimitiveType.Cube, "Caja", cart, new Vector3(0f, 0.65f, 0f), new Vector3(1.4f, 0.5f, 0.85f), c.CartWood);
            Prim(PrimitiveType.Cube, "Tela", cart, new Vector3(0f, 0.93f, 0f), new Vector3(1.32f, 0.06f, 0.8f), m.TeoAccent, Col.None, null, true);
            Prim(PrimitiveType.Cylinder, "Rueda", cart, new Vector3(-0.45f, 0.32f, 0.47f), new Vector3(0.6f, 0.04f, 0.6f), m.Metal, Col.None, Quaternion.Euler(90f, 0f, 0f), true);
            Prim(PrimitiveType.Cylinder, "Rueda", cart, new Vector3(-0.45f, 0.32f, -0.47f), new Vector3(0.6f, 0.04f, 0.6f), m.Metal, Col.None, Quaternion.Euler(90f, 0f, 0f), true);
            Prim(PrimitiveType.Cube, "Pata", cart, new Vector3(0.6f, 0.2f, 0f), new Vector3(0.08f, 0.4f, 0.6f), c.CartWood, Col.None, null, true);
            Prim(PrimitiveType.Cylinder, "Varal", cart, new Vector3(1.05f, 0.75f, 0.3f), new Vector3(0.05f, 0.4f, 0.05f), c.CartWood, Col.None, Quaternion.Euler(0f, 0f, 70f), true);
            Prim(PrimitiveType.Cylinder, "Varal", cart, new Vector3(1.05f, 0.75f, -0.3f), new Vector3(0.05f, 0.4f, 0.05f), c.CartWood, Col.None, Quaternion.Euler(0f, 0f, 70f), true);
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Cylinder, "Vela", cart, new Vector3(-0.35f + i * 0.3f, 1.06f, 0.1f * (i - 1)), new Vector3(0.07f, 0.1f, 0.07f), assets.Character.CandleWax, Col.None, null, true);
            }
            Prim(PrimitiveType.Cube, "Farolillo", cart, new Vector3(0.4f, 1.12f, 0f), new Vector3(0.12f, 0.16f, 0.12f), m.LanternFrame, Col.None, null, true);
            MarkStatic(cart.gameObject);
        }

        // ================================================================== Iluminación, reflejos y post dinámico

        private static void BuildRenderingExtras(GameObject player)
        {
            // Aberración cromática solo en momentos de urgencia (Volume propio, prioridad 5).
            GameObject post = new GameObject("PostFx_Dinamico");
            post.AddComponent<Volume>();
            PostFxDirector director = post.AddComponent<PostFxDirector>();
            Wire(director, "energy", player.GetComponent<PlayerEnergy>());

            // Sonda de reflejos en tiempo real (se renderiza UNA vez al cargar): los charcos del
            // asfalto y el adoquín reflejan ventanas y farolas. Alternativa barata a SSR (URP no tiene SSR).
            ReflectionProbe probe = new GameObject("Sonda_Reflejos").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(-20f, 6f, 4f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(100f, 30f, 50f);
            probe.resolution = 128;
            probe.boxProjection = false;
            probe.intensity = 0.6f;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = AscuaPalette.Niebla;
            probe.shadowDistance = 0f;
        }

        /// <summary>Tintes por instancia (SurfaceTint) para madera y metal repetidos: misma textura, matices distintos.</summary>
        private static void ApplyTintVariation(AscuaMaterials m)
        {
            System.Random rng = new System.Random(5);
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponent<SurfaceTint>() != null) continue;
                Material shared = r.sharedMaterial;
                Color baseColor;
                float spread;
                if (shared == m.Wood) { baseColor = Hex("#5A4A46"); spread = 0.22f; }
                else if (shared == m.Metal) { baseColor = Hex("#2E3346"); spread = 0.12f; }
                else continue;
                float k = 1f + ((float)rng.NextDouble() - 0.5f) * spread * 2f;
                float warm = ((float)rng.NextDouble() - 0.5f) * spread * 0.5f;
                r.gameObject.AddComponent<SurfaceTint>().Configure(new Color(baseColor.r * k + warm, baseColor.g * k, baseColor.b * k - warm, 1f));
            }
        }

        private static Color Hex(string hex) => AscuaPalette.Hex(hex);

        /// <summary>Sombras (cascadas, distancia, luces adicionales) y SSAO del renderer de URP.</summary>
        private static void ConfigureRendering()
        {
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;

            SerializedObject so = new SerializedObject(pipeline);
            SetFloat(so, "m_ShadowDistance", 45f);
            SetInt(so, "m_ShadowCascadeCount", 3);
            SerializedProperty split3 = so.FindProperty("m_Cascade3Split");
            if (split3 != null) split3.vector2Value = new Vector2(0.1f, 0.32f);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048);
            SetInt(so, "m_AdditionalLightsShadowResolutionTierLow", 256);
            SetInt(so, "m_AdditionalLightsShadowResolutionTierMedium", 512);
            SetInt(so, "m_AdditionalLightsShadowResolutionTierHigh", 1024);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 3);
            SetBool(so, "m_SupportsCameraDepthTexture", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            // SSAO del renderer (ya existe en la plantilla PC): más presencia, radio algo mayor.
            SerializedProperty renderers = so.FindProperty("m_RendererDataList");
            if (renderers == null || renderers.arraySize == 0) return;
            ScriptableRendererData data = renderers.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            if (data == null) return;
            foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                SerializedObject ssao = new SerializedObject(feature);
                SetFloat(ssao, "m_Settings.Intensity", 0.9f);
                SetFloat(ssao, "m_Settings.Radius", 0.4f);
                SetFloat(ssao, "m_Settings.DirectLightingStrength", 0.35f);
                SetFloat(ssao, "m_Settings.Falloff", 60f);
                SetBool(ssao, "m_Active", true);
                ssao.ApplyModifiedPropertiesWithoutUndo();
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(data);
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }

        /// <summary>Resolución de sombra por luz (URP): 0 = baja, 1 = media, 2 = alta.</summary>
        private static void SetShadowTier(Light light, int tier)
        {
            UniversalAdditionalLightData data = light.GetComponent<UniversalAdditionalLightData>();
            if (data == null) data = light.gameObject.AddComponent<UniversalAdditionalLightData>();
            SerializedObject so = new SerializedObject(data);
            SetInt(so, "m_AdditionalLightsShadowResolutionTier", tier);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
