#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Video;
using Object = UnityEngine.Object;

// Idempotent authoring tools. Existing characters, Animator Controllers and UI roots are retained.
public static class BunnyPopPolishSetup
{
    const string Folder = "Assets/Presentation";
    static readonly Color Cream = new Color(1, 0.95f, 0.79f);
    static readonly Color Ink = new Color(0.055f, 0.1f, 0.075f, 0.92f);
    static readonly Color Gold = new Color(1, 0.75f, 0.25f);
    static readonly Color Green = new Color(0.27f, 0.58f, 0.12f);
    static readonly Color Blue = new Color(0.13f, 0.48f, 0.73f);
    static readonly Color Red = new Color(0.74f, 0.2f, 0.13f);
    const string Instructions = "RESTORE CARROT HOLLOW\n\nRecover 5 magic seeds, defeat the invading enemies, and return the seeds to the camp altar before 5:00 runs out.\n\nWASD  Move    MOUSE  Look    SPACE  Jump\nSHIFT  Sprint    CTRL  Crouch\nLEFT CLICK  Punch\nHOLD Q + LEFT CLICK  Aim and cast a bubble\nE  Open chest / unlock gate / collect / restore\nESC or P  Pause    TAB  Objectives\n\nOpen the chest near camp to unlock your Carrot Wand. Bubbles deal 20 damage; a standard enemy takes three hits. Collect blue bubbles with E to refill your five charges. Orcs, slimes and bees will chase and attack you; keep moving and use trees for cover.";

    [MenuItem("BunnyPop/Polish/Apply Gameplay and Menus")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var bunny = Object.FindAnyObjectByType<BunnyController>();
        var game = Object.FindAnyObjectByType<GameManager>();
        var audio = ConfigureAudio(game.gameObject, false);
        ConfigureCombat(bunny);
        ConfigureBees();
        ConfigureWorldGoal(bunny, game);
        ConfigureGameplayUI(bunny, game, audio);
        ConfigureShadows();
        RecordScene();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        RebuildNavigation();
        // Preserve the original MainMenu scene.
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("POLISH_SETUP_OK");
    }

    static T Add<T>(GameObject obj) where T : Component
    {
        var component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }
    static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AudioAssets/" + path);
    static GameAudio ConfigureAudio(GameObject root, bool menu)
    {
        var audio = Add<GameAudio>(root);
        AudioSource Source(string name)
        {
            var child = root.transform.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(root.transform, false); }
            var source = Add<AudioSource>(child.gameObject);
            source.playOnAwake = false; source.spatialBlend = 0;
            return source;
        }
        audio.exploration = Source("Exploration Music"); audio.combat = Source("Combat Music"); audio.ui = Source("UI Sounds");
        audio.exploringMusic = menu ? Clip("000 New/12 Main MEnu BGM.mp3") : Clip("background musics/Background Music (Exploring) (2) .wav");
        audio.combatMusic = menu ? null : Clip("background musics/Background Music (Combat) (2) .mp3");
        audio.buttonClick = Clip("mouse click/mouse click.mp3");
        audio.musicVolume = menu ? 0.3f : 0.22f;
        return audio;
    }

    static void ConfigureCombat(BunnyController bunny)
    {
        var cast = Effect("Wand Cast", new Color(0.55f, 0.9f, 1f), 18, 9, 0.35f, 0.65f);
        var pop = Effect("Bubble Pop", new Color(0.65f, 0.93f, 1f), 28, 14, 0.5f, 0.6f);
        var prefab = PrefabUtility.LoadPrefabContents("Assets/ForestEncounter/Bubble Projectile.prefab");
        try
        {
            var shot = prefab.GetComponent<WandProjectile>();
            shot.speed = 78; shot.range = 160; shot.radius = 1.5f; shot.instantKill = false;
            shot.popEffect = pop; shot.popSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/Bubble Pop.wav");
            var shell = prefab.transform.Find("Bubble Shell");
            shell.localScale = Vector3.one * 3f;
            var renderer = shell.GetComponent<Renderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ForestEncounter/Bubble Shell.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var trail = Add<TrailRenderer>(prefab);
            trail.sharedMaterial = ParticleMaterial(); trail.time = 0.18f; trail.startWidth = 0.8f; trail.endWidth = 0;
            trail.startColor = new Color(0.55f, 0.9f, 1, 0.55f); trail.endColor = new Color(0.8f, 1, 1, 0);
            trail.minVertexDistance = 0.3f; trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/ForestEncounter/Bubble Projectile.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var attack = bunny.GetComponent<BunnyAttack>();
        attack.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ForestEncounter/Bubble Projectile.prefab").GetComponent<WandProjectile>();
        attack.projectileDamage = 20; attack.castEffect = cast;
        attack.wandAttackSound = Clip("player attack/casting magic.mp3");
        attack.prefixText = "BUBBLES  "; attack.requiresWandChest = true;
        attack.normalFOV = 60; attack.zoomedFOV = 48; attack.zoomSpeed = 8;
        foreach (var health in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
        {
            var ai = health.GetComponent<EnemyAI>();
            ai.attackSound = Clip(ai is BeeAI ? "enemy attacks/bees attack.mp3" : ai is SlimeAI ? "enemy attacks/Attack (Slime Enemy).mp3" : "enemy attacks/orc attack.mp3");
            health.seedDropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ForestEncounter/Magical Seed Loot.prefab").GetComponent<Seed>();
            health.wandChargeDropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ForestEncounter/Wand Charge Loot.prefab").GetComponent<Seed>();
            // Clearing twelve enemies must be possible with five initial shots.
            // Every defeat gives a refill; five authored world seeds make the seed goal deterministic.
            health.bonusSeedDropPrefab = health.seedDropPrefab; health.bonusSeedChance = 0.35f;
            health.seedDropPrefab = null;
            health.seedDropChance = 1;
        }
    }
    static Material ParticleMaterial()
    {
        string path = Folder + "/Magic Particles.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.renderQueue = 3000; material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
            pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Clamp01((1 - radius) * 3));
        }
        texture.SetPixels(pixels); texture.Apply(); texture.name = "Soft Magic Spark";
        AssetDatabase.CreateAsset(texture, Folder + "/Soft Magic Spark.asset");
        material.SetTexture("_BaseMap", texture);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
    static ParticleSystem Effect(string name, Color color, int count, float speed, float size, float lifetime)
    {
        string path = Folder + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<ParticleSystem>();
        var root = new GameObject(name);
        var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false; main.playOnAwake = false; main.duration = 0.5f;
        main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = size; main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60;
        var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.6f;
        var overLife = particles.colorOverLifetime; overLife.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        overLife.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = ParticleMaterial(); renderer.shadowCastingMode = ShadowCastingMode.Off;
        var result = PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<ParticleSystem>();
        Object.DestroyImmediate(root);
        return result;
    }

    static void ConfigureBees()
    {
        foreach (var bee in Object.FindObjectsByType<BeeAI>(FindObjectsInactive.Include))
        {
            bee.moveSpeed = 24; bee.chaseSpeed = 34; bee.detectionRadius = 70; bee.detectionHeight = 55;
            bee.attackRadius = 13; bee.roamRadius = 42; bee.memoryDuration = 5;
            bee.attackWindup = 0.4f; bee.attackCooldown = 1.4f;
            bee.minIdleTime = 0.35f; bee.maxIdleTime = 1.1f;
            var agent = bee.GetComponent<NavMeshAgent>();
            agent.speed = bee.moveSpeed; agent.acceleration = 70;
            // Keep the visual feet just above the ground, regardless of the model's pivot.
            var renderers = bee.GetComponentsInChildren<Renderer>().Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToArray();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                agent.baseOffset = Mathf.Max(2f, bee.transform.position.y - bounds.min.y + 2f);
            }
            var capsule = bee.GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
            {
                float bottomFromRoot = capsule.bounds.min.y - bee.transform.position.y;
                float lift = 1.5f - agent.baseOffset - bottomFromRoot;
                capsule.center += Vector3.up * (lift / Mathf.Abs(capsule.transform.lossyScale.y));
            }
            Debug.Log($"POLISH_BEE {bee.name} hover={agent.baseOffset} patrol={bee.moveSpeed} chase={bee.chaseSpeed}");
        }
    }

    static Vector3 Ground(Vector3 p)
    {
        foreach (var terrain in Terrain.activeTerrains)
        {
            var local = p - terrain.transform.position; var size = terrain.terrainData.size;
            if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y; break;
        }
        return p;
    }
    static void ConfigureWorldGoal(BunnyController bunny, GameManager game)
    {
        var root = GameObject.Find("Camp Objectives") ?? new GameObject("Camp Objectives");
        Add<NavMeshModifier>(root).ignoreFromBuild = true;
        var chestRoot = root.transform.Find("Wand Chest");
        if (chestRoot == null)
        {
            chestRoot = new GameObject("Wand Chest").transform; chestRoot.SetParent(root.transform, false);
            chestRoot.position = Ground(bunny.transform.position + new Vector3(-13, 0, 6));
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/ForestEncounter/Gate Wood.mat");
            var gold = SolidMaterial("Wand Gold", new Color(0.82f, 0.51f, 0.12f));
            Cube("Chest", chestRoot, new Vector3(0, 2, 0), new Vector3(7, 4, 4), wood);
            var lid = new GameObject("Lid Hinge").transform; lid.SetParent(chestRoot, false); lid.localPosition = new Vector3(0, 4, 2);
            Cube("Lid", lid, new Vector3(0, 0.35f, -2), new Vector3(7.4f, 0.7f, 4.5f), gold);
            var chest = chestRoot.gameObject.AddComponent<WandChest>(); chest.lid = lid;
            var trigger = chestRoot.gameObject.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.center = Vector3.up * 3; trigger.size = new Vector3(9, 6, 6);
            chest.openSound = Clip("collectibles and chests/opening treasure chest.mp3");
        }
        game.wandChest = chestRoot.GetComponent<WandChest>();
        if (chestRoot.Find("Latch") == null)
        {
            var gold = SolidMaterial("Wand Gold", new Color(0.82f, 0.51f, 0.12f));
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/ForestEncounter/Gate Wood.mat");
            Cube("Latch", chestRoot, new Vector3(0, 3.3f, -2.2f), new Vector3(1, 1.5f, 0.5f), gold);
            foreach(float x in new[]{-2.4f,2.4f})
            {
                Cube("Chest band " + x, chestRoot, new Vector3(x, 2, 0), new Vector3(0.45f, 4.1f, 4.1f), gold);
                Cube("Lid band " + x, game.wandChest.lid, new Vector3(x,0.8f,-2), new Vector3(0.5f,0.25f,4.6f), gold);
            }
            game.wandChest.lid.Find("Lid").GetComponent<Renderer>().sharedMaterial=wood;
        }
        var altarRoot = root.transform.Find("Seed Altar");
        if (altarRoot == null)
        {
            altarRoot = new GameObject("Seed Altar").transform; altarRoot.SetParent(root.transform, false);
            altarRoot.position = Ground(bunny.transform.position + new Vector3(13, 0, 4));
            var stone = SolidMaterial("Altar Stone", new Color(0.43f, 0.48f, 0.38f));
            var glow = SolidMaterial("Altar Magic", new Color(0.36f, 0.91f, 0.63f));
            Cube("Stone base", altarRoot, Vector3.up * 1, new Vector3(8, 2, 8), stone);
            Cube("Pedestal", altarRoot, Vector3.up * 3, new Vector3(5, 3, 5), stone);
            Cube("Seed bed", altarRoot, Vector3.up * 4.6f, new Vector3(6, 0.4f, 6), glow);
            var trigger = altarRoot.gameObject.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.center = Vector3.up * 3; trigger.size = new Vector3(10, 6, 10);
            altarRoot.gameObject.AddComponent<SeedAltar>();
        }
        game.altar = altarRoot.GetComponent<SeedAltar>();
        var existing = Object.FindObjectsByType<Seed>().Where(s => s.carrotCharges == 0).OrderBy(s => s.name, StringComparer.Ordinal).ToList();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ForestEncounter/Magical Seed Loot.prefab");
        while (existing.Count < 5)
        {
            var seed = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<Seed>();
            seed.transform.SetParent(GameObject.Find("Seeds").transform, true); existing.Add(seed);
        }
        Vector3[] positions = { new Vector3(228,0,145), new Vector3(202,0,130), new Vector3(160,0,80), new Vector3(275,0,30), new Vector3(305,0,140) };
        var gate = Object.FindAnyObjectByType<ForestGate>();
        Vector3 routeOrigin = gate != null && gate.sentry != null ? gate.sentry.transform.position : bunny.transform.position;
        var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(Ground(routeOrigin), out var start, 15, filter)) throw new InvalidOperationException("Forest route has no NavMesh start.");
        var route = new NavMeshPath();
        var chosen = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < existing.Count; i++)
        {
            if (i >= 5) continue;
            var seed = existing[i]; seed.name = "Magic Seed " + (i + 1);
            Vector3 point = default; bool found = false;
            for (int ring = 0; ring <= 9 && !found; ring++) for (int j = 0; j < (ring == 0 ? 1 : 16); j++)
            {
                Vector3 candidate = Ground(positions[i] + Quaternion.Euler(0, j * 22.5f, 0) * Vector3.forward * (ring * 10));
                if (!NavMesh.SamplePosition(candidate, out var hit, 5, filter)
                    || chosen.Any(p => (p - hit.position).sqrMagnitude < 225)
                    || !NavMesh.CalculatePath(start.position, hit.position, filter, route) || route.status != NavMeshPathStatus.PathComplete) continue;
                point = hit.position; found = true; break;
            }
            if (!found) throw new InvalidOperationException("No reachable position for seed " + (i + 1));
            chosen.Add(point);
            seed.transform.position = point + Vector3.up * 3;
            seed.SettleOnGround();
            Debug.Log("POLISH_SEED " + seed.name + " " + seed.transform.position);
        }
        game.allSeeds = 5; game.seeds = 0; game.stageDuration = 300; game.requireAltar = true; game.requireAllEnemies = true;
        game.winSound = Clip("WIN or LOSE/victory.mp3"); game.loseSound = Clip("WIN or LOSE/game over.mp3");
    }

    [MenuItem("BunnyPop/Polish/Rebuild Navigation After World Changes")]
    public static void RebuildNavigation()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SampleScene") scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        foreach (var surface in Object.FindObjectsByType<NavMeshSurface>())
        {
            var saved = surface.navMeshData;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake failed: " + surface.name);
            if (saved != null && AssetDatabase.Contains(saved))
            {
                var generated = surface.navMeshData;
                surface.RemoveData(); EditorUtility.CopySerialized(generated, saved);
                surface.navMeshData = saved; surface.AddData(); EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(generated);
            }
            Debug.Log("POLISH_NAV_BAKED " + surface.name);
        }
        RecordScene(); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    static void ConfigureShadows()
    {
        foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            asset.shadowDistance = path.Contains("PC_") ? 220 : 130;
            asset.shadowCascadeCount = path.Contains("PC_") ? 4 : 2;
            EditorUtility.SetDirty(asset);
        }
        foreach (var light in Object.FindObjectsByType<Light>())
            if (light.type == LightType.Directional) { light.shadows = LightShadows.Soft; light.shadowStrength = 0.85f; }
        foreach (var terrain in Terrain.activeTerrains) { terrain.treeDistance = 600; terrain.treeBillboardDistance = 150; }
    }
    static Material SolidMaterial(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); return material;
    }
    static void Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position; cube.transform.localScale = size; cube.GetComponent<Renderer>().sharedMaterial = material;
    }
    static void RecordScene()
    {
        foreach (var component in Object.FindObjectsByType<Component>(FindObjectsInactive.Include))
        {
            if (!component.gameObject.scene.IsValid()) continue;
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position, Vector2? anchor = null)
    {
        var existing = parent.Find(name);
        var rect = existing as RectTransform;
        if (existing != null && rect == null) throw new InvalidOperationException(name + " is not a UI object");
        if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); }
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor ?? Vector2.one * 0.5f;
        rect.sizeDelta = size; rect.anchoredPosition3D = new Vector3(position.x, position.y, 0);
        return rect;
    }
    static void Stretch(RectTransform r)
    {
        r.localScale = Vector3.one; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = Vector2.one * 0.5f; r.offsetMin = r.offsetMax = Vector2.zero;
        var position = r.anchoredPosition3D; position.z = 0; r.anchoredPosition3D = position;
    }
    static Sprite Art(string name)
    {
        // Create trimmed sprite assets while preserving the supplied image and its importer.
        // Several originals contain >60% empty space, which caused undersized buttons.
        string asset = Folder + "/UI " + name + ".asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset);
        if (sprite != null) return sprite;
        string path = "Assets/UI Assets/" + name + ".png";
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (source == null) throw new InvalidOperationException("Missing UI art: " + path);
        var readable = new Texture2D(2, 2); readable.LoadImage(File.ReadAllBytes(path));
        var pixels = readable.GetPixels32();
        int minX = readable.width, minY = readable.height, maxX = 0, maxY = 0;
        for (int y = 0; y < readable.height; y++) for (int x = 0; x < readable.width; x++)
        {
            if (pixels[y * readable.width + x].a < 12) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        var bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        // Sprite coordinates use the source dimensions, not a platform-downscaled texture.
        float sx = (float)source.width / readable.width, sy = (float)source.height / readable.height;
        bounds = new Rect(bounds.x * sx, bounds.y * sy, bounds.width * sx, bounds.height * sy);
        sprite = Sprite.Create(source, bounds, Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
        sprite.name = name; AssetDatabase.CreateAsset(sprite, asset); Object.DestroyImmediate(readable);
        return sprite;
    }
    static UnityEngine.UI.Image Image(RectTransform rect, Sprite sprite, Color color, bool raycast = false)
    {
        var image = Add<UnityEngine.UI.Image>(rect.gameObject);
        image.sprite = sprite; image.color = color; image.raycastTarget = raycast;
        image.type = UnityEngine.UI.Image.Type.Simple; image.preserveAspect = sprite != null;
        return image;
    }
    static TMP_Text Text(Transform parent, string name, string content, Vector2 size, Vector2 position, float fontSize = 24, TextAlignmentOptions alignment = TextAlignmentOptions.Center, Vector2? anchor = null)
    {
        var rect = Rect(parent, name, size, position, anchor);
        var text = Add<TextMeshProUGUI>(rect.gameObject);
        text.gameObject.SetActive(true);
        text.text = content; text.fontSize = fontSize; text.fontStyle = FontStyles.Normal; text.color = Cream;
        text.alignment = alignment; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = false; text.margin = Vector4.zero;
        return text;
    }
    static void OldLabels(Transform root)
    {
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
    }
    static UnityEngine.UI.Button Button(Transform parent, string name, string label, Sprite sprite, Vector2 size, Vector2 position, Color color, UnityAction action, GameAudio audio, Vector2? anchor = null)
    {
        var rect = Rect(parent, name, size, position, anchor);
        var image = Image(rect, sprite, color, true);
        if (sprite == null)
        {
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 0.35f;
        }
        var button = Add<UnityEngine.UI.Button>(rect.gameObject); button.targetGraphic = image; button.interactable = true;
        var buttonShadow = Add<UnityEngine.UI.Shadow>(rect.gameObject);
        buttonShadow.effectColor = new Color(0.05f, 0.025f, 0.01f, 0.55f); buttonShadow.effectDistance = new Vector2(0, -5);
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, 0.94f, 0.73f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.65f); button.colors = colors;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
        if (audio != null) UnityEventTools.AddPersistentListener(button.onClick, audio.Click);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        OldLabels(rect);
        if (!string.IsNullOrEmpty(label))
        {
            var text = Text(rect, "Label", label, size - new Vector2(16, 8), Vector2.zero, size.x < 200 ? 20 : 24);
            text.gameObject.SetActive(true); text.fontStyle = FontStyles.Bold;
            var shadow = Add<UnityEngine.UI.Shadow>(text.gameObject); shadow.effectColor = new Color(0.1f, 0.07f, 0.03f, 0.8f); shadow.effectDistance = new Vector2(1, -2);
        }
        rect.gameObject.SetActive(true);
        return button;
    }
    static RectTransform Overlay(Transform root, string name)
    {
        var rect = Rect(root, name, Vector2.zero, Vector2.zero); Stretch(rect);
        Image(rect, null, new Color(0.015f, 0.035f, 0.02f, 0.78f), true);
        return rect;
    }
    static RectTransform Board(Transform root, string name, Vector2 size, Vector2 position)
    {
        var rect = Rect(root, name, size, position);
        Image(rect, Art("container"), new Color(0.6f, 0.52f, 0.4f)).preserveAspect = false;
        return rect;
    }
    static void CanvasSetup(Canvas canvas)
    {
        var scaler = Add<UnityEngine.UI.CanvasScaler>(canvas.gameObject);
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; Add<UnityEngine.UI.GraphicRaycaster>(canvas.gameObject);
        // Labels and decorative images must not intercept button clicks.
        foreach (var graphic in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            if (graphic.GetComponent<UnityEngine.UI.Selectable>() == null) graphic.raycastTarget = false;
    }
    public static void ApplyResponsiveScaling()
    {
        foreach(string name in new[]{"MainMenu","SampleScene"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity");
            foreach(var scaler in Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsInactive.Include))
            {
                scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1280,720);
                scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
                EditorUtility.SetDirty(scaler);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
    static void ConfigureGameplayUI(BunnyController bunny, GameManager game, GameAudio audio)
    {
        var canvas = game.winPanel.GetComponentInParent<Canvas>(true); CanvasSetup(canvas);
        var root = canvas.transform;
        var playerUI = Rect(root, "PlayerUI", Vector2.zero, Vector2.zero); Stretch(playerUI);
        var inventory = Rect(root, "BunnyInventory", Vector2.zero, Vector2.zero); Stretch(inventory);
        var healthPanel = Rect(playerUI, "HealthFrame", new Vector2(290, 92), new Vector2(24, -22), new Vector2(0, 1));
        Image(healthPanel, null, Ink);
        Text(healthPanel, "Name", "BUNNY KEEPER", new Vector2(250, 24), new Vector2(0, 25), 17, TextAlignmentOptions.Left);
        var healthSlider = bunny.GetComponent<BunnyHealth>().healthBar;
        healthSlider.transform.SetParent(healthPanel, false);
        Rect(healthPanel, healthSlider.name, new Vector2(250, 27), new Vector2(0, -3));
        healthSlider.interactable = false;
        var handle = healthSlider.transform.Find("Handle Slide Area"); if (handle != null) handle.gameObject.SetActive(false);
        if (healthSlider.fillRect != null) healthSlider.fillRect.GetComponent<UnityEngine.UI.Image>().color = new Color(0.45f, 0.8f, 0.25f);
        game.healthText = Text(healthPanel, "HealthValue", "100 / 100", new Vector2(250, 25), new Vector2(0, -30), 16);
        var oldAvatar = playerUI.Find("Image"); if (oldAvatar != null) oldAvatar.gameObject.SetActive(false);
        var seedsImage = Rect(inventory, "Seeds Collected", new Vector2(245, 64), new Vector2(-105, -24), new Vector2(1, 1));
        Image(seedsImage, null, Ink); OldLabels(seedsImage);
        Image(Rect(seedsImage, "SeedIcon", new Vector2(42, 53), new Vector2(-86, 0)), Art("collectible"), Color.white);
        game.seedsText = Text(seedsImage, "Count", "0 / 5", new Vector2(140, 42), new Vector2(20, 0), 30);
        var bubbles = Rect(inventory, "Bubbles", new Vector2(245, 42), new Vector2(-105, -92), new Vector2(1, 1));
        Image(bubbles, null, Ink); OldLabels(bubbles);
        bunny.GetComponent<BunnyAttack>().chargesText = (TextMeshProUGUI)Text(bubbles, "Count", "Find the wand chest", new Vector2(225, 32), Vector2.zero, 19);
        game.seedSlots = new UnityEngine.UI.Image[5];
        for (int i = 0; i < 5; i++) game.seedSlots[i] = Image(Rect(inventory, "SeedSlot" + i, new Vector2(30, 38), new Vector2(-316 + i * 43, -148), new Vector2(1, 1)), Art("collectible"), game.emptySeedColor);
        game.pauseButton = Button(playerUI, "PauseButton", null, Art("PauseButton"), new Vector2(62, 62), new Vector2(-24, -24), Color.white, game.PauseGame, audio, new Vector2(1, 1)).gameObject;
        var objectiveCard = Rect(root, "Objective Card", new Vector2(540, 80), new Vector2(0, -22), new Vector2(0.5f, 1));
        Image(objectiveCard, null, Ink);
        game.objectiveText = Text(objectiveCard, "Objective", "Open the chest at camp  [E]", new Vector2(500, 40), new Vector2(0, 12), 22);
        game.timerText = Text(objectiveCard, "Timer", "05:00", new Vector2(110, 30), new Vector2(-130, -22), 23);
        game.enemyText = Text(objectiveCard, "Enemies", "12 enemies left", new Vector2(230, 30), new Vector2(80, -22), 17);
        Text(root, "Controls Hint", "WASD Move   SPACE Jump   E Interact   Q + Click Bubble   TAB Objectives", new Vector2(880, 28), new Vector2(24, 22), 17, TextAlignmentOptions.Left, Vector2.zero);
        var objectiveButton = root.Find("ObjectiveButton");
        if (objectiveButton != null) objectiveButton.gameObject.SetActive(false);
        var prompt = bunny.GetComponent<BunnyInteraction>().prompt;
        var promptRect = Rect(root, prompt.name, new Vector2(720, 54), new Vector2(0, 92), new Vector2(0.5f, 0));
        prompt.fontSize = 23; prompt.fontStyle = FontStyles.Bold; prompt.color = Cream;
        var promptShadow = Add<UnityEngine.UI.Shadow>(prompt.gameObject); promptShadow.effectColor = Color.black; promptShadow.effectDistance = new Vector2(1, -2);
        var marker = Rect(root, "Goal Marker", new Vector2(250, 60), Vector2.zero);
        Image(Rect(marker, "Diamond", new Vector2(17, 17), new Vector2(0, 17)), null, Gold).rectTransform.localRotation = Quaternion.Euler(0,0,45);
        var markerText = Text(marker, "Label", "MAGIC SEED", new Vector2(250, 30), new Vector2(0,-14), 16);
        markerText.fontStyle = FontStyles.Bold; Add<UnityEngine.UI.Shadow>(markerText.gameObject).effectColor = Color.black;
        var goalMarker = Add<GoalMarker>(root.gameObject); goalMarker.marker = marker; goalMarker.label = markerText; goalMarker.worldCamera = Camera.main; goalMarker.canvas = (RectTransform)root;
        PauseUI(game, root, audio);
        OutcomeUI(root, game.winPanel, "FOREST RESTORED", "All seeds restored. Carrot Hollow is blooming again!", game, audio, true);
        OutcomeUI(root, game.losePanel, "TRY ANOTHER HOP", "Your adventure isn't over. Give it another try!", game, audio, false);
        var objectives = Overlay(root, "Objectives Panel");
        Board(objectives, "Board", new Vector2(840, 650), Vector2.zero);
        Text(objectives, "Instructions", Instructions, new Vector2(680, 485), new Vector2(0, 30), 20, TextAlignmentOptions.TopLeft);
        Button(objectives, "Back", "BACK TO GAME", null, new Vector2(300, 54), new Vector2(0, -255), Green, game.ResumeGame, audio);
        game.objectivePanel = objectives.gameObject;
        game.settingsPanel = Settings(root, audio, game.CloseSettings).gameObject;
        game.objectivePanel.SetActive(false); game.settingsPanel.SetActive(false);
        game.winPanel.SetActive(false); game.losePanel.SetActive(false); game.pausePanel.SetActive(false);
        // Overlay panels must render after HUD, prompt and waypoint.
        foreach (var panel in new[] { game.pausePanel, game.winPanel, game.losePanel, game.objectivePanel, game.settingsPanel }) panel.transform.SetAsLastSibling();
    }
    static void PauseUI(GameManager game, Transform root, GameAudio audio)
    {
        var panel = Overlay(root, game.pausePanel.name);
        var board = Rect(panel, "PauseMenu", new Vector2(610, 510), Vector2.zero);
        // Center the wood, allowing the rabbit illustration to extend to the right.
        board.pivot = new Vector2(0.36f, 0.5f);
        Image(board, Art("01a1a9b0-8b00-7314-9fb4-a6f111c35abd"), Color.white);
        Button(panel, "Resume", null, Art("Gemini_Generated_Image_rygxnfrygxnfrygx-removebg-preview"), new Vector2(330, 80), new Vector2(0, 55), Color.white, game.ResumeGame, audio);
        Button(panel, "Restart", "RETRY", null, new Vector2(330, 70), new Vector2(0, -38), Blue, game.RestartGame, audio);
        Button(panel, "MainMenu", "MAIN MENU", null, new Vector2(330, 70), new Vector2(0, -126), Red, game.LoadMainMenu, audio);
        var exit = panel.Find("Exit"); if (exit != null) exit.gameObject.SetActive(false);
        Button(panel, "Settings", null, Art("setting"), new Vector2(54, 54), new Vector2(-40, -218), Color.white, game.OpenSettings, audio);
        Button(panel, "Help", "?", null, new Vector2(54, 54), new Vector2(40, -218), Blue, game.ToggleObjectives, audio);
    }
    static void OutcomeUI(Transform root, GameObject original, string title, string message, GameManager game, GameAudio audio, bool victory)
    {
        var panel = Overlay(root, original.name); OldLabels(panel);
        Board(panel, "OutcomeBoard", new Vector2(740, 550), Vector2.zero);
        Text(panel, "OutcomeTitle", title, new Vector2(620, 65), new Vector2(0, 160), 38).fontStyle = FontStyles.Bold;
        Text(panel, "OutcomeMessage", message, new Vector2(560, 100), new Vector2(0, 60), 23);
        Button(panel, "Button", "PLAY AGAIN", null, new Vector2(350, 64), new Vector2(0, -60), victory ? Green : Blue, game.RestartGame, audio);
        Button(panel, "MainMenuButton", "MAIN MENU", null, new Vector2(350, 64), new Vector2(0, -145), Red, game.LoadMainMenu, audio);
    }
    static RectTransform Settings(Transform root, GameAudio audio, UnityAction back)
    {
        var panel = Overlay(root, "Settings Panel");
        Board(panel, "Board", new Vector2(680, 520), Vector2.zero);
        Text(panel, "Title", "SOUND SETTINGS", new Vector2(540, 60), new Vector2(0, 155), 32).fontStyle = FontStyles.Bold;
        Text(panel, "VolumeLabel", "MASTER VOLUME", new Vector2(350, 40), new Vector2(0, 70), 22);
        var options = Add<AudioOptions>(panel.gameObject); options.audioController = audio;
        var sliderRect = Rect(panel, "Volume", new Vector2(370, 36), new Vector2(0, 12));
        var slider = Add<UnityEngine.UI.Slider>(sliderRect.gameObject); slider.minValue = 0; slider.maxValue = 1; slider.value = GameAudio.MasterVolume;
        Image(Rect(sliderRect, "Track", new Vector2(370, 12), Vector2.zero), null, Ink);
        var fill = Rect(sliderRect, "Fill", new Vector2(0, 12), Vector2.zero); Stretch(fill); fill.offsetMin = new Vector2(0, 12); fill.offsetMax = new Vector2(0, -12);
        Image(fill, null, Gold); slider.fillRect = fill;
        var handle = Rect(sliderRect, "Handle", new Vector2(26, 36), Vector2.zero); Image(handle, null, Cream, true);
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>(); slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        options.slider = slider; options.soundOn = Art("volume"); options.soundOff = Art("mute");
        var mute = Button(panel, "Mute", null, options.soundOn, new Vector2(65, 65), new Vector2(0, -70), Color.white, options.ToggleMute, audio);
        options.muteImage = mute.GetComponent<UnityEngine.UI.Image>();
        for (int i = slider.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(slider.onValueChanged, i);
        UnityEventTools.AddPersistentListener(slider.onValueChanged, options.SetVolume);
        Button(panel, "Back", "BACK", null, new Vector2(280, 58), new Vector2(0, -170), Green, back, audio);
        return panel;
    }
    static void ConfigureMainMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var menu = Object.FindAnyObjectByType<MainMenuManager>();
        var canvas = Object.FindAnyObjectByType<Canvas>(); CanvasSetup(canvas);
        var root = canvas.transform;
        foreach (var video in Object.FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include)) { video.playOnAwake = false; video.enabled = false; }
        foreach (var loader in Object.FindObjectsByType<LoadingScreenManager>(FindObjectsInactive.Include)) loader.enabled = false;
        foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)) { source.playOnAwake = false; source.enabled = false; }
        var audio = ConfigureAudio(menu.gameObject, true);
        foreach (var source in new[] { audio.exploration, audio.combat, audio.ui }) source.enabled = true;
        menu.audioSource = audio.ui; menu.backgroundMusic = null; menu.buttonClickSound = null;
        menu.loadingScreenUI = null; menu.loadingVideoPlayer = null;
        var background = Rect(root, "RawImage", Vector2.zero, Vector2.zero); Stretch(background);
        var raw = Add<UnityEngine.UI.RawImage>(background.gameObject);
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Carrot Hollow Menu.png"); raw.color = Color.white; raw.raycastTarget = false;
        var aspect = Add<UnityEngine.UI.AspectRatioFitter>(background.gameObject); aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent; aspect.aspectRatio = 16f/9;
        background.SetAsFirstSibling();
        var shade = Rect(root, "Menu Shade", Vector2.zero, Vector2.zero); Stretch(shade); Image(shade, null, new Color(0.015f, 0.08f, 0.025f, 0.22f)); shade.SetSiblingIndex(1);
        var buttons = Rect(root, "Buttons", Vector2.zero, Vector2.zero); Stretch(buttons);
        Image(Rect(buttons, "Image", new Vector2(510, 275), new Vector2(-330, 150)), Art("logo"), Color.white);
        Text(buttons, "Tagline", "AN ENCHANTED GARDEN ADVENTURE", new Vector2(510, 36), new Vector2(-330, -2), 18).fontStyle = FontStyles.Bold;
        Button(buttons, "Play", null, Art("play"), new Vector2(300, 96), new Vector2(-330, -95), Color.white, menu.PlayGame, audio);
        Button(buttons, "How To Play", "HOW TO PLAY", null, new Vector2(300, 62), new Vector2(-330, -180), Green, menu.OpenInstructions, audio);
        Button(buttons, "ExitGame", null, Art("leave"), new Vector2(230, 74), new Vector2(-330, -265), Color.white, menu.OpenLeaveConfirm, audio);
        Button(buttons, "Settings", null, Art("setting"), new Vector2(68, 68), new Vector2(-32, -30), Color.white, menu.OpenSettings, audio, new Vector2(1, 1));
        Text(buttons, "Quest Teaser", "THE FOREST NEEDS ITS KEEPER", new Vector2(520, 50), new Vector2(290, -235), 26).fontStyle = FontStyles.Bold;
        Text(buttons, "Quest Detail", "Five lost seeds. One enchanted wand.\nBring the garden back to life.", new Vector2(520, 75), new Vector2(290, -292), 23);
        var confirm = Overlay(root, "LeaveConfirm"); OldLabels(confirm);
        Board(confirm, "Board", new Vector2(620, 440), Vector2.zero);
        Text(confirm, "Question", "Hop away for now?", new Vector2(510, 60), new Vector2(0, 80), 32).fontStyle = FontStyles.Bold;
        var oldConfirm = confirm.Find("Confirm"); if (oldConfirm != null) oldConfirm.gameObject.SetActive(false);
        Button(confirm, "Yes", "QUIT GAME", null, new Vector2(220, 65), new Vector2(-125, -55), Red, menu.ConfirmLeaveGame, audio);
        Button(confirm, "No", "STAY", null, new Vector2(220, 65), new Vector2(125, -55), Green, menu.CloseLeaveConfirm, audio);
        menu.leaveConfirmPanel = confirm.gameObject;
        menu.settingsPanel = Settings(root, audio, menu.CloseSettings).gameObject;
        var instructions = Overlay(root, "How To Play Panel"); Board(instructions, "Board", new Vector2(840, 650), Vector2.zero);
        Text(instructions, "Instructions", Instructions, new Vector2(680, 485), new Vector2(0, 30), 20, TextAlignmentOptions.TopLeft);
        Button(instructions, "Back", "BACK", null, new Vector2(300, 54), new Vector2(0, -255), Green, menu.CloseInstructions, audio);
        menu.howToPlayPanel = instructions.gameObject;
        foreach (var panel in new[] { menu.leaveConfirmPanel, menu.settingsPanel, menu.howToPlayPanel }) { panel.transform.SetAsLastSibling(); panel.SetActive(false); }
        RecordScene(); EditorSceneManager.SaveScene(scene);
    }
}
#endif
