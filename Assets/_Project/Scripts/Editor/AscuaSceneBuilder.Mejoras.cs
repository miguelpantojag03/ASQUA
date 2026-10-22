using Ascua.Audio;
using Ascua.Controls;
using Ascua.Core;
using Ascua.Lighting;
using Ascua.Narrative;
using Ascua.Player;
using Ascua.Quests;
using Ascua.UI;
using Ascua.World;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ascua.EditorTools
{
    /// <summary>
    /// Ronda de mejoras: recogida de objetos, luz de relleno por zona, movilidad (salto, trepar,
    /// esquiva, sprint), atajos de exploración, ráfaga de la Penumbra, momento de la Noche 1,
    /// audio adaptativo y las piezas de juego completo (pausa, diario, modo foto, resumen con mejoras).
    /// Se separa del constructor principal solo para que cada archivo sea legible.
    /// </summary>
    public static partial class AscuaSceneBuilder
    {
        private static readonly Color Ember = new Color(1f, 0.48f, 0.24f);
        private static readonly Color Gold = new Color(1f, 0.89f, 0.63f);

        // ================================================================== Zonas

        /// <summary>Relleno frío que sube con la Esperanza + chispas al alcanzar cada 25 %.</summary>
        private static void BuildZoneExtras(ZoneHope zone, float radius, AscuaMaterials m)
        {
            GameObject fillObject = new GameObject("Luz_Relleno_Fria");
            fillObject.transform.SetParent(zone.transform, false);
            fillObject.transform.localPosition = new Vector3(0f, 9f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 26f;
            fill.intensity = 0f;
            fill.shadows = LightShadows.None;
            fill.color = new Color(0.62f, 0.72f, 1f);
            ZoneAmbientLight ambient = fillObject.AddComponent<ZoneAmbientLight>();
            Wire(ambient, "zone", zone);

            GameObject sparksObject = new GameObject("Chispas_Esperanza");
            sparksObject.transform.SetParent(zone.transform, false);
            sparksObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            ParticleSystem sparks = sparksObject.AddComponent<ParticleSystem>();
            ConfigureBurst(sparks, m.FireflyParticle, AscuaPalette.Ambar, 0.09f, 0.6f, 2.6f, 160, -0.12f);
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.55f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            HopeFeedback feedback = zone.gameObject.AddComponent<HopeFeedback>();
            Wire(feedback, "zone", zone);
            Wire(feedback, "sparks", sparks);
        }

        /// <summary>Partículas de ráfaga (no se emiten solas: los scripts llaman a Emit).</summary>
        private static void ConfigureBurst(ParticleSystem ps, Material material, Color color, float size, float speed, float lifetime, int max, float gravity)
        {
            ConfigureFireflyParticles(ps, material);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.maxParticles = max;
            main.gravityModifier = gravity;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
        }

        // ================================================================== Jugador

        /// <summary>Stamina, salto, trepar, esquiva, recogida y el broche ámbar de Ilse.</summary>
        private static void AddPlayerImprovements(AscuaAssets assets, ChibiParts parts, PlayerInputReader input, PlayerController controller,
            PlayerEnergy energy, Transform lanternPivot, Transform lanternLight)
        {
            AscuaMaterials m = assets.Materials;
            GameObject player = parts.Root;

            // Broche ámbar que cierra la capa (punto cálido en el pecho; el resto de Ilse es frío).
            Prim(PrimitiveType.Cube, "Broche", parts.Rig, new Vector3(0f, 0.52f, 0.19f), Vector3.one * 0.07f, assets.Character.Brooch,
                Col.None, Quaternion.Euler(0f, 0f, 45f), false);

            PlayerStamina stamina = player.AddComponent<PlayerStamina>();
            Wire(controller, "stamina", stamina);

            // Polvo al aterrizar (a los pies, en espacio mundo).
            GameObject dustObject = new GameObject("Polvo_Aterrizaje");
            dustObject.transform.SetParent(player.transform, false);
            dustObject.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
            ConfigureBurst(dust, m.FireflyParticle, new Color(0.62f, 0.68f, 0.85f, 0.55f), 0.22f, 1.1f, 0.6f, 40, 0.05f);
            ParticleSystem.ShapeModule dustShape = dust.shape;
            dustShape.shapeType = ParticleSystemShapeType.Circle;
            dustShape.radius = 0.25f;
            dustShape.rotation = new Vector3(90f, 0f, 0f);

            // Estela de la esquiva (brasas que se quedan atrás).
            GameObject trailObject = new GameObject("Estela_Esquiva");
            trailObject.transform.SetParent(player.transform, false);
            trailObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            ParticleSystem dashTrail = trailObject.AddComponent<ParticleSystem>();
            ConfigureBurst(dashTrail, m.FireflyParticle, Ember, 0.1f, 0.3f, 0.5f, 60, -0.1f);

            // Destello al recoger (en el Farol, adonde vuela el objeto).
            GameObject burstObject = new GameObject("Destello_Recoger");
            burstObject.transform.SetParent(lanternPivot, false);
            burstObject.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            ParticleSystem collectBurst = burstObject.AddComponent<ParticleSystem>();
            ConfigureBurst(collectBurst, m.FireflyParticle, AscuaPalette.Ambar, 0.07f, 1.4f, 0.8f, 60, -0.15f);

            PlayerJump jump = player.AddComponent<PlayerJump>();
            PlayerMantle mantle = player.AddComponent<PlayerMantle>();
            PlayerDash dash = player.AddComponent<PlayerDash>();

            Wire(jump, "input", input);
            Wire(jump, "controller", controller);
            Wire(jump, "mantle", mantle);
            Wire(jump, "visual", parts.Visual);
            Wire(jump, "landingDust", dust);

            Wire(mantle, "controller", controller);
            Wire(mantle, "jumpFx", jump);

            Wire(dash, "input", input);
            Wire(dash, "controller", controller);
            Wire(dash, "stamina", stamina);
            Wire(dash, "jumpFx", jump);
            Wire(dash, "trail", dashTrail);

            PickupSystem pickups = player.AddComponent<PickupSystem>();
            Wire(pickups, "lanternPoint", lanternLight);
            Wire(pickups, "animator", parts.Animator);
            Wire(pickups, "collectBurst", collectBurst);
            Wire(pickups, "energy", energy);
        }

        // ================================================================== Objetos recogibles

        /// <summary>Raíz de un objeto recogible con su "Visual" (que flota y gira).</summary>
        private static PickupItem CreatePickup(string name, Transform parent, Vector3 position, out Transform visual)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            visual = Pivot("Visual", root.transform, Vector3.zero);
            PickupItem item = root.AddComponent<PickupItem>();
            Wire(item, "visual", visual);
            return item;
        }

        private static PickupItem DiaryPagePickup(string name, Transform parent, Vector3 position, string pageId, CharacterAssets ch)
        {
            PickupItem item = CreatePickup(name, parent, position, out Transform visual);
            Prim(PrimitiveType.Cube, "Hoja", visual, Vector3.zero, new Vector3(0.26f, 0.34f, 0.015f), ch.Paper, Col.None, Quaternion.Euler(0f, 0f, -8f), false);
            Prim(PrimitiveType.Cube, "Hoja_Doblez", visual, new Vector3(0.02f, -0.02f, -0.012f), new Vector3(0.24f, 0.3f, 0.01f), ch.Paper, Col.None, Quaternion.Euler(0f, 0f, 5f), false);
            item.Configure(pageId, "una página del Diario", PickupKind.PaginaDiario, Gold, null, pageId);
            SmallGlow(item.transform, Gold, 0.7f, 2f);
            SetLayerRecursively(item.gameObject, IgnoreRaycastLayer);
            return item;
        }

        private static PickupItem GoldenFireflyPickup(string name, Transform parent, Vector3 position, string zoneId, AscuaAssets assets)
        {
            PickupItem item = CreatePickup(name, parent, position, out Transform visual);
            Prim(PrimitiveType.Sphere, "Brillo", visual, Vector3.zero, Vector3.one * 0.17f, assets.Character.GoldenFirefly, Col.None, null, false);
            GameObject sparklesObject = new GameObject("Chispas");
            sparklesObject.transform.SetParent(visual, false);
            ParticleSystem sparkles = sparklesObject.AddComponent<ParticleSystem>();
            ConfigureFireflyParticles(sparkles, assets.Materials.FireflyParticle);
            ParticleSystem.MainModule main = sparkles.main;
            main.startColor = Gold;
            item.Configure("luciernaga_dorada", "una luciérnaga dorada", PickupKind.LuciernagaDorada, Gold, null, null, 40f, 10f, zoneId);
            SmallGlow(item.transform, Gold, 1.2f, 2.6f);
            SetLayerRecursively(item.gameObject, IgnoreRaycastLayer);
            return item;
        }

        private static void SmallGlow(Transform parent, Color color, float intensity, float range)
        {
            Light light = new GameObject("Brillo_Luz").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>Página 1 junto al inicio (tutorial) y la postal de Teo sobre su puesto.</summary>
        private static void BuildPickups(AscuaAssets assets)
        {
            Transform root = new GameObject("Objetos_Recogibles").transform;
            CharacterAssets ch = assets.Character;

            DiaryPagePickup("Pagina_01", root, new Vector3(0.9f, 0.6f, -5.6f), "pagina_01", ch);

            PickupItem postal = CreatePickup("Postal_Abuela", root, new Vector3(-40.05f, 1.06f, 5.75f), out Transform visual);
            Prim(PrimitiveType.Cube, "Postal", visual, Vector3.zero, new Vector3(0.24f, 0.016f, 0.16f), ch.Postal, Col.None, Quaternion.Euler(0f, 18f, 0f), false);
            postal.Configure("postal_abuela", "la postal", PickupKind.ObjetoMision, AscuaPalette.TurquesaTeo, "teo_postal");
            SetLayerRecursively(postal.gameObject, IgnoreRaycastLayer);
        }

        // ================================================================== Atajos de exploración

        /// <summary>
        /// Dos caminos opcionales que premian explorar con salto + trepar:
        ///  1) Callejón de los Faroleros: entre los dos edificios del norte de La Plaza, tapado por cajas.
        ///  2) Tejado del quiosco: en la calle entre La Plaza y El Mercado; se sube por una caja.
        /// Cada uno guarda una página del Diario y una luciérnaga dorada.
        /// </summary>
        private static void BuildExplorationShortcuts(AscuaAssets assets)
        {
            AscuaMaterials m = assets.Materials;
            CharacterAssets ch = assets.Character;
            Transform root = new GameObject("Rincones").transform;

            // --- 1) Callejón de los Faroleros (x -1..1, z 14.5..21.5)
            Transform alley = new GameObject("Callejon_Faroleros").transform;
            alley.SetParent(root, false);
            Prim(PrimitiveType.Cube, "Cajas_Entrada", alley, new Vector3(0f, 0.625f, 15.1f), new Vector3(2f, 1.25f, 0.9f), m.Wood);
            // Caja alta pegada a la pared izquierda: deja libre el centro para trepar y pasar.
            Prim(PrimitiveType.Cube, "Caja_Entrada_Alta", alley, new Vector3(-0.68f, 1.52f, 15.25f), new Vector3(0.6f, 0.55f, 0.6f), m.Wood);
            Prim(PrimitiveType.Cube, "Cajas_Salida", alley, new Vector3(0f, 0.625f, 21.1f), new Vector3(2f, 1.25f, 0.9f), m.Wood);
            // Hollín de los faroleros en las paredes y faroles viejos apagados.
            for (int i = 0; i < 4; i++)
            {
                float z = 16.4f + i * 1.3f;
                Prim(PrimitiveType.Quad, "Hollin", alley, new Vector3(-0.98f, 1.6f + (i % 2) * 0.5f, z), new Vector3(0.7f, 0.9f, 1f), ch.Soot, Col.None, Quaternion.Euler(0f, 90f, 0f), false);
                Prim(PrimitiveType.Quad, "Hollin", alley, new Vector3(0.98f, 1.9f - (i % 2) * 0.4f, z + 0.5f), new Vector3(0.6f, 0.8f, 1f), ch.Soot, Col.None, Quaternion.Euler(0f, -90f, 0f), false);
            }
            Prim(PrimitiveType.Cube, "Farol_Viejo", alley, new Vector3(-0.88f, 2.3f, 17.6f), new Vector3(0.16f, 0.26f, 0.16f), m.LanternFrame, Col.None, null, false);
            Prim(PrimitiveType.Cube, "Farol_Viejo", alley, new Vector3(0.88f, 2.1f, 19.4f), new Vector3(0.16f, 0.26f, 0.16f), m.LanternFrame, Col.None, null, false);

            Light alleyLight = RevealLight(alley, new Vector3(0f, 2.4f, 18.2f));
            Shortcut(alley, "callejon_faroleros", "El callejón de los faroleros", new Vector3(0f, 1.5f, 18.2f), new Vector3(2f, 3f, 3.5f), alleyLight);
            DiaryPagePickup("Pagina_02", alley, new Vector3(0f, 0.55f, 18.9f), "pagina_02", ch);
            GoldenFireflyPickup("Luciernaga_Dorada_Callejon", alley, new Vector3(0f, 1.5f, 17.3f), PlazaZone, assets);

            // --- 2) Tejado del quiosco
            Transform kiosk = new GameObject("Quiosco").transform;
            kiosk.SetParent(root, false);
            kiosk.gameObject.AddComponent<PenumbraFade>();
            Prim(PrimitiveType.Cube, "Cuerpo", kiosk, new Vector3(-30f, 1.1f, -11f), new Vector3(3.4f, 2.2f, 3.4f), m.Buildings[3]);
            Prim(PrimitiveType.Cube, "Tejado", kiosk, new Vector3(-30f, 2.275f, -11f), new Vector3(3.9f, 0.15f, 3.9f), m.Roof);
            Prim(PrimitiveType.Cube, "Mostrador", kiosk, new Vector3(-30f, 1.1f, -12.75f), new Vector3(2.4f, 0.12f, 0.2f), m.Trim, Col.None);
            Prim(PrimitiveType.Cube, "Persiana", kiosk, new Vector3(-30f, 1.7f, -12.72f), new Vector3(2.2f, 0.9f, 0.04f), m.Door, Col.None);
            Prim(PrimitiveType.Cube, "Caja_Escalon", kiosk, new Vector3(-27.85f, 0.475f, -11f), new Vector3(0.95f, 0.95f, 0.95f), m.Wood, Col.Keep, Quaternion.Euler(0f, 8f, 0f));
            Prim(PrimitiveType.Cube, "Barandilla", kiosk, new Vector3(-31.9f, 2.55f, -11f), new Vector3(0.06f, 0.4f, 3.9f), m.Metal, Col.None);

            Light roofLight = RevealLight(kiosk, new Vector3(-30f, 3.4f, -11f));
            Shortcut(kiosk, "tejado_quiosco", "El tejado del quiosco", new Vector3(-30f, 3.2f, -11f), new Vector3(3.6f, 1.6f, 3.6f), roofLight);
            DiaryPagePickup("Pagina_03", kiosk, new Vector3(-30.6f, 2.85f, -11.4f), "pagina_03", ch);
            GoldenFireflyPickup("Luciernaga_Dorada_Quiosco", kiosk, new Vector3(-29.3f, 3.1f, -10.5f), MercadoZone, assets);

            MarkStatic(root.gameObject);
        }

        private static Light RevealLight(Transform parent, Vector3 position)
        {
            Light light = new GameObject("Luz_Descubrimiento").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = Gold;
            light.range = 6f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static void Shortcut(Transform parent, string id, string displayName, Vector3 center, Vector3 size, Light revealLight)
        {
            GameObject go = new GameObject("Atajo_" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.layer = IgnoreRaycastLayer;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            go.AddComponent<ExplorationShortcut>().Configure(id, displayName, revealLight);
        }

        // ================================================================== Penumbra, momento y audio

        private static void BuildSurge(CanvasGroup vignette)
        {
            GameObject go = new GameObject("Penumbra_Rafaga");
            CinemachineImpulseSource impulse = go.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble;
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseDuration = 1.2f;
            PenumbraSurge surge = go.AddComponent<PenumbraSurge>();
            Wire(surge, "vignette", vignette);
            Wire(surge, "impulse", impulse);
        }

        /// <summary>Noche 1: Brasa vuelve con Mina (cámara corta, barras de cine y frase).</summary>
        private static void BuildNightEvent(AscuaAssets assets, Canvas hud)
        {
            // El HUD se puede ocultar entero durante el momento; las barras van en su propio canvas.
            CanvasGroup hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            Transform hudRoot = CreateCanvas("UI_Momento", 25).transform;

            // Cámara del momento: prioridad alta, apagada hasta que el evento la activa.
            GameObject cameraObject = new GameObject("CM_Momento");
            CinemachineCamera momentCamera = cameraObject.AddComponent<CinemachineCamera>();
            momentCamera.Priority = 50;
            LensSettings lens = momentCamera.Lens;
            lens.FieldOfView = 38f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 250f;
            momentCamera.Lens = lens;
            cameraObject.SetActive(false);

            // Brasa: chispa de color brasa que orbita la cabeza de Mina.
            GameObject brasaObject = new GameObject("Brasa_Chispa");
            brasaObject.transform.position = new Vector3(3.6f, 1.6f, -4.2f);
            GameObject glow = Prim(PrimitiveType.Sphere, "Brillo", brasaObject.transform, Vector3.zero, Vector3.one * 0.13f, assets.Character.CandleFlame, Col.None, null, false);
            GameObject trailObject = new GameObject("Estela");
            trailObject.transform.SetParent(brasaObject.transform, false);
            ParticleSystem trail = trailObject.AddComponent<ParticleSystem>();
            ConfigureFireflyParticles(trail, assets.Materials.FireflyParticle);
            ParticleSystem.MainModule trailMain = trail.main;
            trailMain.startColor = Ember;
            trailMain.startLifetime = 0.8f;
            ParticleSystem.EmissionModule trailEmission = trail.emission;
            trailEmission.rateOverTime = 18f;
            SmallGlow(brasaObject.transform, Ember, 1.4f, 3f);
            BrasaSpark brasa = brasaObject.AddComponent<BrasaSpark>();
            Wire(brasa, "glow", glow.GetComponent<Renderer>());
            SetLayerRecursively(brasaObject, IgnoreRaycastLayer);

            // Barras de cine + frase (en el HUD).
            RectTransform topBar = UIRect("Barra_Cine_Sup", hudRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            topBar.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.03f, 1f);
            topBar.GetComponent<Image>().raycastTarget = false;
            RectTransform bottomBar = UIRect("Barra_Cine_Inf", hudRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 0f));
            bottomBar.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.03f, 1f);
            bottomBar.GetComponent<Image>().raycastTarget = false;

            RectTransform captionRect = UIRect("Frase_Momento", hudRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1400f, 60f));
            CanvasGroup captionGroup = captionRect.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0f;
            captionGroup.blocksRaycasts = false;
            TextMeshProUGUI caption = UIText("Texto", captionRect, "Brasa ha vuelto a casa.", 34f, new Color(1f, 0.9f, 0.75f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            caption.fontStyle = FontStyles.Italic;
            caption.characterSpacing = 3f;

            NightEvent nightEvent = new GameObject("Momento_Noche1").AddComponent<NightEvent>();
            Wire(nightEvent, "momentCamera", momentCamera);
            Wire(nightEvent, "brasa", brasa);
            Wire(nightEvent, "topBar", topBar);
            Wire(nightEvent, "bottomBar", bottomBar);
            Wire(nightEvent, "captionGroup", captionGroup);
            Wire(nightEvent, "caption", caption);
            Wire(nightEvent, "hudGroup", hudGroup);
        }

        private static void BuildAudio(AudioAssets a)
        {
            AudioManager audio = new GameObject("AudioManager").AddComponent<AudioManager>();
            if (a.Mixer != null)
            {
                Wire(audio, "mixer", a.Mixer);
                Wire(audio, "musicGroup", a.MusicGroup);
                Wire(audio, "ambientGroup", a.AmbientGroup);
                Wire(audio, "sfxGroup", a.SfxGroup);
            }
            Wire(audio, "musicBase", a.MusicBase);
            Wire(audio, "musicHope", a.MusicHope);
            Wire(audio, "musicTension", a.MusicTension);
            Wire(audio, "windLoop", a.Wind);
            Wire(audio, "waterLoop", a.Water);
            Wire(audio, "sfxPickup", a.Pickup);
            Wire(audio, "sfxQuestComplete", a.QuestComplete);
            Wire(audio, "sfxWindowOn", a.WindowOn);
            Wire(audio, "sfxZoneLost", a.ZoneLost);
            Wire(audio, "sfxNpcTalk", a.NpcTalk);
            Wire(audio, "sfxPulse", a.Pulse);
            Wire(audio, "sfxHopeLevel", a.HopeLevel);
            Wire(audio, "sfxJump", a.Jump);
            Wire(audio, "sfxLand", a.Land);
            Wire(audio, "sfxDash", a.Dash);
            Wire(audio, "sfxLanternResize", a.LanternResize);
            Wire(audio, "sfxSurge", a.Surge);
            Wire(audio, "sfxBond", a.Bond);
            Wire(audio, "sfxMoment", a.Moment);
            Wire(audio, "sfxUiClick", a.UiClick);
            Wire(audio, "sfxSwing", a.Swing);
            Wire(audio, "sfxLightHit", a.LightHit);
            Wire(audio, "sfxDissolve", a.Dissolve);
            Wire(audio, "sfxHurt", a.Hurt);
            Wire(audio, "sfxPerfectDodge", a.PerfectDodge);
            Wire(audio, "sfxWaveStart", a.WaveStart);
            Wire(audio, "sfxWaveCleared", a.WaveCleared);
            Wire(audio, "sfxBruteWarning", a.BruteWarning);
            Wire(audio, "sfxShadowBolt", a.ShadowBolt);
            Wire(audio, "sfxComboFinisher", a.ComboFinisher);
            Wire(audio, "sfxPowerDown", a.PowerDown);
            Wire(audio, "sfxCriticalEnergy", a.CriticalEnergy);
            Wire(audio, "sfxThreat", a.Threat);
        }

        // ================================================================== HUD nuevo

        /// <summary>Objetivo actual, flecha, tutorial, stamina, inventario, subtítulos y viñeta de la ráfaga.</summary>
        private static CanvasGroup BuildHudExtras(Canvas canvas, GameObject player, AscuaAssets assets)
        {
            Transform root = canvas.transform;
            Color amber = AscuaPalette.Ambar;
            Color cold = AscuaPalette.TextoFrio;
            CharacterAssets ch = assets.Character;

            // --- Viñeta de la ráfaga de la Penumbra (debajo de todo lo demás del HUD).
            RectTransform vignetteRect = UIRect("Vineta_Penumbra", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            vignetteRect.SetAsFirstSibling();
            Image vignetteImage = vignetteRect.gameObject.AddComponent<Image>();
            vignetteImage.sprite = ch.VignetteSprite;
            vignetteImage.color = new Color(0.08f, 0.04f, 0.16f, 0.92f);
            vignetteImage.raycastTarget = false;
            CanvasGroup vignette = vignetteRect.gameObject.AddComponent<CanvasGroup>();
            vignette.alpha = 0f;
            vignette.blocksRaycasts = false;

            // --- Objetivo actual (arriba a la izquierda, bajo el logo): siempre visible.
            // Pivote a la izquierda: la animación de cambio "crece" desde el borde de la pantalla.
            RectTransform objective = UIRect("Objetivo_Actual", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -128f), new Vector2(620f, 120f));
            Image objectiveBack = objective.gameObject.AddComponent<Image>();
            objectiveBack.color = new Color(0.02f, 0.03f, 0.08f, 0.62f);
            objectiveBack.raycastTarget = false;
            Image objectiveFrame = UIRect("Marco", objective, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            objectiveFrame.sprite = assets.Combat.FrameSprite;
            objectiveFrame.type = Image.Type.Sliced;
            objectiveFrame.color = new Color(amber.r, amber.g, amber.b, 0.75f);
            objectiveFrame.raycastTarget = false;
            CanvasGroup objectiveGroup = objective.gameObject.AddComponent<CanvasGroup>();
            objectiveGroup.alpha = 0f;
            objectiveGroup.blocksRaycasts = false;
            RectTransform iconRect = UIRect("Icono", objective, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(40f, 40f));
            Image objectiveIcon = iconRect.gameObject.AddComponent<Image>();
            objectiveIcon.sprite = assets.Combat.QuestIcons[1];
            objectiveIcon.color = amber;
            objectiveIcon.preserveAspect = true;
            objectiveIcon.raycastTarget = false;
            TextMeshProUGUI objectiveTitle = UIText("Titulo", objective, "Brasa", 26f, amber, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(70f, -12f), new Vector2(-90f, 34f));
            objectiveTitle.fontStyle = FontStyles.Bold;
            TextMeshProUGUI objectiveAction = UIText("Accion", objective, "Busca a la gata de Mina", 23f, cold, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(70f, -46f), new Vector2(-90f, 44f));
            objectiveAction.richText = true;
            RectTransform progressBack = UIRect("Progreso", objective, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            progressBack.offsetMin = new Vector2(70f, 12f);
            progressBack.offsetMax = new Vector2(-24f, 16f);
            Image progressBackImage = progressBack.gameObject.AddComponent<Image>();
            progressBackImage.color = new Color(0.08f, 0.1f, 0.2f, 0.9f);
            progressBackImage.raycastTarget = false;
            Image progressFill = UIRect("Relleno", progressBack, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            progressFill.sprite = assets.Character.WhiteSprite;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.color = amber;
            progressFill.raycastTarget = false;
            progressBack.gameObject.SetActive(false);

            // Parte C: segunda barra (objetivo B del doble objetivo, o energía gastada en un presupuesto).
            RectTransform secondaryBack = UIRect("Progreso_B", objective, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            secondaryBack.offsetMin = new Vector2(70f, 5f);
            secondaryBack.offsetMax = new Vector2(-24f, 9f);
            Image secondaryBackImage = secondaryBack.gameObject.AddComponent<Image>();
            secondaryBackImage.color = new Color(0.08f, 0.1f, 0.2f, 0.9f);
            secondaryBackImage.raycastTarget = false;
            Image secondaryFill = UIRect("Relleno", secondaryBack, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            secondaryFill.sprite = assets.Character.WhiteSprite;
            secondaryFill.type = Image.Type.Filled;
            secondaryFill.fillMethod = Image.FillMethod.Horizontal;
            secondaryFill.color = new Color(0.72f, 0.8f, 1f);
            secondaryFill.raycastTarget = false;
            secondaryBack.gameObject.SetActive(false);

            ObjectiveTrackerUI tracker = objective.gameObject.AddComponent<ObjectiveTrackerUI>();
            Wire(tracker, "group", objectiveGroup);
            Wire(tracker, "titleText", objectiveTitle);
            Wire(tracker, "actionText", objectiveAction);
            Wire(tracker, "popRoot", objective);
            Wire(tracker, "icon", objectiveIcon);
            Wire(tracker, "progressRoot", progressBack.gameObject);
            Wire(tracker, "progressFill", progressFill);
            WireArray(tracker, "typeIcons", assets.Combat.QuestIcons);
            Wire(tracker, "combatIcon", assets.Combat.CombatIcon);
            Wire(tracker, "secondaryRoot", secondaryBack.gameObject);
            Wire(tracker, "secondaryFill", secondaryFill);

            // --- Flecha de dirección: marcador centrado que el script recoloca cada frame.
            RectTransform marker = UIRect("Flecha_Objetivo", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 110f));
            CanvasGroup markerGroup = marker.gameObject.AddComponent<CanvasGroup>();
            markerGroup.alpha = 0f;
            markerGroup.blocksRaycasts = false;
            RectTransform arrow = UIRect("Flecha", marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(54f, 54f));
            Image arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.sprite = ch.ArrowSprite;
            arrowImage.color = amber;
            arrowImage.preserveAspect = true;
            arrowImage.raycastTarget = false;
            TextMeshProUGUI distance = UIText("Distancia", marker, "12 m", 22f, new Color(1f, 0.93f, 0.8f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(120f, 30f));
            distance.fontStyle = FontStyles.Bold;
            ObjectiveArrow objectiveArrow = marker.gameObject.AddComponent<ObjectiveArrow>();
            Wire(objectiveArrow, "canvasRect", root);
            Wire(objectiveArrow, "marker", marker);
            Wire(objectiveArrow, "arrowGraphic", arrow);
            Wire(objectiveArrow, "distanceText", distance);
            Wire(objectiveArrow, "group", markerGroup);

            // --- Pistas del tutorial diegético (voz interior de Ilse, en cursiva).
            RectTransform hint = UIRect("Tutorial", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 240f), new Vector2(1100f, 70f));
            CanvasGroup hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintGroup.blocksRaycasts = false;
            TextMeshProUGUI hintText = UIText("Texto", hint, "«Algo brilla a mis pies...»", 28f, new Color(0.88f, 0.9f, 1f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            hintText.fontStyle = FontStyles.Italic;
            hintText.richText = true;
            TutorialHints tutorial = hint.gameObject.AddComponent<TutorialHints>();
            Wire(tutorial, "group", hintGroup);
            Wire(tutorial, "text", hintText);

            // --- Stamina: barra fina y fría bajo el aviso de interacción (solo al usarla).
            RectTransform staminaRect = UIRect("Stamina", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(280f, 10f));
            CanvasGroup staminaGroup = staminaRect.gameObject.AddComponent<CanvasGroup>();
            staminaGroup.alpha = 0f;
            staminaGroup.blocksRaycasts = false;
            Image staminaBack = staminaRect.gameObject.AddComponent<Image>();
            staminaBack.color = new Color(0.05f, 0.07f, 0.15f, 0.7f);
            staminaBack.raycastTarget = false;
            RectTransform staminaFill = UIRect("Relleno", staminaRect, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            staminaFill.offsetMin = Vector2.zero;
            staminaFill.offsetMax = Vector2.zero;
            Image staminaFillImage = staminaFill.gameObject.AddComponent<Image>();
            staminaFillImage.color = new Color(0.72f, 0.8f, 1f);
            staminaFillImage.raycastTarget = false;
            StaminaUI staminaUI = staminaRect.gameObject.AddComponent<StaminaUI>();
            Wire(staminaUI, "stamina", player.GetComponent<PlayerStamina>());
            Wire(staminaUI, "group", staminaGroup);
            Wire(staminaUI, "fill", staminaFill);
            Wire(staminaUI, "fillImage", staminaFillImage);

            // --- Inventario: 4 huecos abajo a la derecha.
            RectTransform inventory = UIRect("Inventario", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(4 * 74f, 110f));
            Image[] icons = new Image[4];
            TextMeshProUGUI[] letters = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform slot = UIRect("Hueco_" + i, inventory, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(i * 74f, 0f), new Vector2(64f, 64f));
                Image frame = slot.gameObject.AddComponent<Image>();
                frame.color = new Color(0.02f, 0.03f, 0.08f, 0.6f);
                frame.raycastTarget = false;
                RectTransform icon = UIRect("Icono", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                icon.localRotation = Quaternion.Euler(0f, 0f, 45f);
                icons[i] = icon.gameObject.AddComponent<Image>();
                icons[i].color = new Color(1f, 1f, 1f, 0.08f);
                icons[i].raycastTarget = false;
                letters[i] = UIText("Letra", slot, "", 26f, new Color(0.05f, 0.05f, 0.1f), TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                letters[i].fontStyle = FontStyles.Bold;
            }
            TextMeshProUGUI itemName = UIText("Nombre", inventory, "", 20f, new Color(cold.r, cold.g, cold.b, 0.85f), TextAlignmentOptions.BottomRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(400f, 34f));
            InventoryUI inventoryUI = inventory.gameObject.AddComponent<InventoryUI>();
            Wire(inventoryUI, "pickupSystem", player.GetComponent<PickupSystem>());
            WireArray(inventoryUI, "slotIcons", icons);
            WireArray(inventoryUI, "slotLetters", letters);
            Wire(inventoryUI, "nameLabel", itemName);

            // --- Subtítulos de efectos (accesibilidad).
            RectTransform subtitle = UIRect("Subtitulos", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(900f, 44f));
            Image subtitleBack = subtitle.gameObject.AddComponent<Image>();
            subtitleBack.color = new Color(0f, 0f, 0f, 0.55f);
            subtitleBack.raycastTarget = false;
            CanvasGroup subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            subtitleGroup.alpha = 0f;
            subtitleGroup.blocksRaycasts = false;
            TextMeshProUGUI subtitleText = UIText("Texto", subtitle, "", 24f, new Color(0.95f, 0.95f, 1f), TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            SubtitleUI subtitles = subtitle.gameObject.AddComponent<SubtitleUI>();
            Wire(subtitles, "group", subtitleGroup);
            Wire(subtitles, "text", subtitleText);

            return vignette;
        }

        // ================================================================== Pausa, Diario y Modo foto

        private static void BuildPauseAndCodex(AscuaAssets assets)
        {
            Canvas canvas = CreateCanvas("UI_Pausa", 30);
            Transform root = canvas.transform;
            Color amber = AscuaPalette.Ambar;
            Color cold = AscuaPalette.TextoFrio;

            // --- Menú de pausa
            RectTransform pause = UIRect("Pausa", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pause.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.8f);
            CanvasGroup pauseGroup = pause.gameObject.AddComponent<CanvasGroup>();
            pauseGroup.alpha = 0f;
            pauseGroup.interactable = false;
            pauseGroup.blocksRaycasts = false;

            TextMeshProUGUI title = UIText("Titulo", pause, "Pausa", 64f, amber, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.84f), new Vector2(0.5f, 0.84f), Vector2.zero, new Vector2(800f, 90f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 10f;

            Button resume = UIButtonSized("Boton_Reanudar", pause, "Reanudar", new Vector2(0.32f, 0.66f), new Vector2(440f, 66f), 30f);
            Button retry = UIButtonSized("Boton_Reintentar", pause, "Reintentar noche", new Vector2(0.32f, 0.56f), new Vector2(440f, 66f), 30f);
            Button diary = UIButtonSized("Boton_Diario", pause, "Diario  (J)", new Vector2(0.32f, 0.46f), new Vector2(440f, 66f), 30f);
            Button accessibility = UIButtonSized("Boton_Accesibilidad", pause, "Accesibilidad", new Vector2(0.32f, 0.36f), new Vector2(440f, 66f), 30f);
            Button toTitle = UIButtonSized("Boton_Titulo", pause, "Salir al título", new Vector2(0.32f, 0.26f), new Vector2(440f, 66f), 30f);

            UIText("Volumen", pause, "VOLUMEN", 24f, new Color(cold.r, cold.g, cold.b, 0.8f), TextAlignmentOptions.Left,
                new Vector2(0.56f, 0.68f), new Vector2(0.56f, 0.68f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(400f, 36f)).characterSpacing = 10f;
            Slider music = UISlider("Musica", pause, "Música", new Vector2(0.56f, 0.6f), 0f, 1f);
            Slider ambient = UISlider("Ambiente", pause, "Ambiente", new Vector2(0.56f, 0.5f), 0f, 1f);
            Slider sfx = UISlider("Efectos", pause, "Efectos", new Vector2(0.56f, 0.4f), 0f, 1f);

            UIText("Ayuda", pause, "Esc · volver al juego", 20f, new Color(cold.r, cold.g, cold.b, 0.55f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(800f, 30f));

            // --- Subpanel de accesibilidad
            // Cubre las dos columnas de la pausa para que no se mezclen los controles.
            RectTransform access = UIRect("Accesibilidad", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1320f, 620f));
            access.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.1f, 1f);
            CanvasGroup accessGroup = access.gameObject.AddComponent<CanvasGroup>();
            accessGroup.alpha = 0f;
            accessGroup.interactable = false;
            accessGroup.blocksRaycasts = false;
            UIText("Titulo", access, "Accesibilidad", 40f, amber, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(700f, 56f)).fontStyle = FontStyles.Bold;
            Slider textScale = UISlider("TamanoTexto", access, "Tamaño del texto de diálogo", new Vector2(0.25f, 0.68f), 0.8f, 1.5f);
            TextMeshProUGUI textScaleLabel = UIText("Valor", access, "100 %", 28f, amber, TextAlignmentOptions.Left,
                new Vector2(0.25f, 0.68f), new Vector2(0.25f, 0.68f), new Vector2(0f, 0.5f), new Vector2(550f, -14f), new Vector2(160f, 40f));
            textScaleLabel.fontStyle = FontStyles.Bold;
            Toggle shake = UIToggle("Sacudidas", access, "Sacudidas de cámara", new Vector2(0.25f, 0.5f));
            Toggle subtitles = UIToggle("Subtitulos", access, "Subtítulos de efectos de sonido", new Vector2(0.25f, 0.38f));
            Toggle flashes = UIToggle("Destellos", access, "Reducir destellos (daño, aberración, combate)", new Vector2(0.25f, 0.26f));
            flashes.isOn = false;
            Toggle rumble = UIToggle("Vibracion", access, "Vibración del mando al recibir daño", new Vector2(0.25f, 0.14f));
            UIText("Ayuda", access, "Esc · volver", 20f, new Color(cold.r, cold.g, cold.b, 0.55f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(600f, 30f));

            PauseMenu menu = pause.gameObject.AddComponent<PauseMenu>();
            Wire(menu, "group", pauseGroup);
            Wire(menu, "accessibilityPanel", accessGroup);
            Wire(menu, "resumeButton", resume);
            Wire(menu, "retryButton", retry);
            Wire(menu, "diaryButton", diary);
            Wire(menu, "accessibilityButton", accessibility);
            Wire(menu, "titleButton", toTitle);
            Wire(menu, "musicSlider", music);
            Wire(menu, "ambientSlider", ambient);
            Wire(menu, "sfxSlider", sfx);
            Wire(menu, "textScaleSlider", textScale);
            Wire(menu, "textScaleLabel", textScaleLabel);
            Wire(menu, "shakeToggle", shake);
            Wire(menu, "subtitlesToggle", subtitles);
            Wire(menu, "flashesToggle", flashes);
            Wire(menu, "rumbleToggle", rumble);

            // --- Diario de Ilse (Codex)
            RectTransform codex = UIRect("Diario", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            codex.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.97f);
            CanvasGroup codexGroup = codex.gameObject.AddComponent<CanvasGroup>();
            codexGroup.alpha = 0f;
            codexGroup.interactable = false;
            codexGroup.blocksRaycasts = false;

            UIText("Titulo", codex, "Diario de Ilse", 52f, amber, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.9f), new Vector2(0.5f, 0.9f), Vector2.zero, new Vector2(900f, 80f)).fontStyle = FontStyles.Bold;

            RectTransform indexPanel = UIRect("Indice", codex, new Vector2(0.14f, 0.5f), new Vector2(0.14f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 10f), new Vector2(420f, 620f));
            indexPanel.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.12f, 0.8f);
            TextMeshProUGUI index = UIText("Lista", indexPanel, "", 24f, cold, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            index.margin = new Vector4(24f, 20f, 20f, 20f);
            index.richText = true;
            index.lineSpacing = 8f;

            RectTransform pagePanel = UIRect("Pagina", codex, new Vector2(0.4f, 0.5f), new Vector2(0.4f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 10f), new Vector2(880f, 620f));
            pagePanel.gameObject.AddComponent<Image>().color = new Color(0.93f, 0.88f, 0.78f, 0.96f);
            TextMeshProUGUI pageTitle = UIText("TituloPagina", pagePanel, "???", 36f, new Color(0.35f, 0.2f, 0.1f), TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(-80f, 50f));
            pageTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;
            TextMeshProUGUI pageBody = UIText("Texto", pagePanel, "", 28f, new Color(0.18f, 0.14f, 0.12f), TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pageBody.margin = new Vector4(40f, 96f, 40f, 30f);
            pageBody.lineSpacing = 10f;
            pageBody.fontStyle = FontStyles.Italic;

            Button previous = UIButtonSized("Boton_Anterior", codex, "« Anterior", new Vector2(0.45f, 0.1f), new Vector2(280f, 60f), 26f);
            Button next = UIButtonSized("Boton_Siguiente", codex, "Siguiente »", new Vector2(0.65f, 0.1f), new Vector2(280f, 60f), 26f);
            Button close = UIButtonSized("Boton_Cerrar", codex, "Cerrar", new Vector2(0.86f, 0.1f), new Vector2(220f, 60f), 26f);

            CodexUI codexUI = codex.gameObject.AddComponent<CodexUI>();
            Wire(codexUI, "group", codexGroup);
            Wire(codexUI, "indexText", index);
            Wire(codexUI, "pageTitle", pageTitle);
            Wire(codexUI, "pageBody", pageBody);
            Wire(codexUI, "previousButton", previous);
            Wire(codexUI, "nextButton", next);
            Wire(codexUI, "closeButton", close);
            WireArray(codexUI, "pages", FreshPages());
        }

        private static void BuildPhotoMode(AscuaAssets assets)
        {
            Volume volume = new GameObject("Volume_FiltroFoto").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = assets.PhotoFilterProfile;

            Canvas canvas = CreateCanvas("UI_Foto", 50);
            RectTransform ui = UIRect("Controles", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1500f, 70f));
            ui.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.6f);
            ui.GetComponent<Image>().raycastTarget = false;
            CanvasGroup group = ui.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            TextMeshProUGUI info = UIText("Texto", ui, "", 22f, AscuaPalette.TextoFrio, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            info.richText = true;

            PhotoMode photo = canvas.gameObject.AddComponent<PhotoMode>();
            Wire(photo, "photoCanvas", canvas);
            Wire(photo, "photoUI", group);
            Wire(photo, "infoText", info);
            Wire(photo, "filterVolume", volume);
        }

        /// <summary>Resumen de la noche: datos, 1 de 3 mejoras, siguiente noche, foto, reintentar y título.</summary>
        private static void BuildNightSummaryPanel(Transform canvasRoot, NightManager night)
        {
            Color cold = AscuaPalette.TextoFrio;
            Color amber = AscuaPalette.Ambar;

            RectTransform panel = UIRect("Resumen_Noche", canvasRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.07f, 0.86f);
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI title = UIText("Titulo", panel, "Amanece en Vesper", 66f, amber, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(1400f, 100f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 6f;
            TextMeshProUGUI body = UIText("Cuerpo", panel, "Zonas salvadas: 0/2", 26f, cold, TextAlignmentOptions.Top,
                new Vector2(0.5f, 0.79f), new Vector2(0.5f, 0.79f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1300f, 300f));
            body.richText = true;
            body.lineSpacing = 4f;

            // Mejoras: 3 tarjetas, se elige una (solo si la ciudad sobrevivió).
            RectTransform upgrades = UIRect("Mejoras", panel, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 230f));
            CanvasGroup upgradeGroup = upgrades.gameObject.AddComponent<CanvasGroup>();
            upgradeGroup.alpha = 0f;
            upgradeGroup.interactable = false;
            upgradeGroup.blocksRaycasts = false;
            UIText("Titulo", upgrades, "Elige qué se lleva tu Farol a la próxima noche", 26f, new Color(cold.r, cold.g, cold.b, 0.85f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200f, 40f));
            Button[] upgradeButtons = new Button[3];
            TextMeshProUGUI[] upgradeLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                upgradeButtons[i] = UIButtonSized("Mejora_" + i, upgrades, "", new Vector2(0.5f, 0.42f), new Vector2(420f, 160f), 24f);
                ((RectTransform)upgradeButtons[i].transform).anchoredPosition = new Vector2((i - 1) * 450f, 0f);
                upgradeLabels[i] = upgradeButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                upgradeLabels[i].characterSpacing = 1f;
                upgradeLabels[i].richText = true;
                upgradeLabels[i].margin = new Vector4(18f, 12f, 18f, 12f);
            }

            Button nextNight = UIButtonSized("Boton_SiguienteNoche", panel, "Siguiente noche", new Vector2(0.5f, 0.22f), new Vector2(460f, 72f), 34f);
            Button photo = UIButtonSized("Boton_Foto", panel, "Modo foto", new Vector2(0.32f, 0.09f), new Vector2(300f, 60f), 26f);
            Button retry = UIButtonSized("Boton_Reintentar", panel, "Reintentar", new Vector2(0.5f, 0.09f), new Vector2(300f, 60f), 26f);
            Button toTitle = UIButtonSized("Boton_Titulo", panel, "Título", new Vector2(0.68f, 0.09f), new Vector2(300f, 60f), 26f);

            NightSummaryPanel summary = panel.gameObject.AddComponent<NightSummaryPanel>();
            Wire(summary, "night", night);
            Wire(summary, "group", group);
            Wire(summary, "titleText", title);
            Wire(summary, "bodyText", body);
            Wire(summary, "retryButton", retry);
            Wire(summary, "titleButton", toTitle);
            Wire(summary, "upgradeGroup", upgradeGroup);
            WireArray(summary, "upgradeButtons", upgradeButtons);
            WireArray(summary, "upgradeLabels", upgradeLabels);
            Wire(summary, "nextNightButton", nextNight);
            Wire(summary, "photoButton", photo);
        }

        // ================================================================== Assets narrativos recargados

        /// <summary>
        /// Recarga desde disco un asset de Data por nombre: las referencias en memoria pueden quedar
        /// invalidadas si el asset se reimporta durante la construcción (mismo caso que el NightQuestSet).
        /// </summary>
        private static T FreshAsset<T>(string assetName) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name, new[] { AscuaAssetFactory.Root + "/Data" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName) return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            Debug.LogError("[Ascua] No se encontró el asset " + assetName);
            return null;
        }

        /// <summary>Todas las páginas del Diario (Data/Diario), ordenadas por número.</summary>
        private static DiaryPage[] FreshPages()
        {
            System.Collections.Generic.List<DiaryPage> pages = new System.Collections.Generic.List<DiaryPage>();
            foreach (string guid in AssetDatabase.FindAssets("t:DiaryPage", new[] { AscuaAssetFactory.Root + "/Data" }))
            {
                DiaryPage page = AssetDatabase.LoadAssetAtPath<DiaryPage>(AssetDatabase.GUIDToAssetPath(guid));
                if (page != null) pages.Add(page);
            }
            pages.Sort((a, b) => a.number.CompareTo(b.number));
            return pages.ToArray();
        }

        // ================================================================== Utilidades de UI nuevas

        private static Button UIButtonSized(string name, Transform parent, string label, Vector2 anchor, Vector2 size, float fontSize)
        {
            Button button = UIButton(name, parent, label, anchor);
            RectTransform rt = (RectTransform)button.transform;
            rt.sizeDelta = size;
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.characterSpacing = 4f;
            return button;
        }

        /// <summary>Slider con etiqueta encima (pivote a la izquierda).</summary>
        private static Slider UISlider(string name, Transform parent, string label, Vector2 anchor, float min, float max)
        {
            RectTransform root = UIRect("Slider_" + name, parent, anchor, anchor, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(520f, 70f));
            UIText("Etiqueta", root, label, 26f, AscuaPalette.TextoFrio, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 34f));

            RectTransform track = UIRect("Pista", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(0f, 22f));
            Image back = track.gameObject.AddComponent<Image>();
            back.color = new Color(0.08f, 0.1f, 0.2f, 0.9f);

            RectTransform fillArea = UIRect("Relleno_Area", track, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillArea.offsetMin = new Vector2(4f, 4f);
            fillArea.offsetMax = new Vector2(-4f, -4f);
            RectTransform fill = UIRect("Relleno", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = AscuaPalette.Ambar;
            fillImage.raycastTarget = false;

            RectTransform handleArea = UIRect("Tirador_Area", track, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = UIRect("Tirador", handleArea, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 14f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(1f, 0.95f, 0.85f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = max;
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
            colors.selectedColor = new Color(1f, 0.85f, 0.6f);
            slider.colors = colors;
            return slider;
        }

        private static Toggle UIToggle(string name, Transform parent, string label, Vector2 anchor)
        {
            RectTransform root = UIRect("Toggle_" + name, parent, anchor, anchor, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(640f, 48f));
            RectTransform box = UIRect("Caja", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = new Color(0.08f, 0.1f, 0.2f, 0.95f);
            RectTransform check = UIRect("Marca", box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            Image checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = AscuaPalette.Ambar;
            checkImage.raycastTarget = false;
            UIText("Etiqueta", root, label, 26f, AscuaPalette.TextoFrio, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(580f, 40f));

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;
            ColorBlock colors = toggle.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
            colors.selectedColor = new Color(1f, 0.85f, 0.6f);
            toggle.colors = colors;
            return toggle;
        }
    }
}
