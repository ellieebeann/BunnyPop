#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static class BunnyPopSetup
{
    static void PrepareTreeColliders()
    {
        var paths = Terrain.activeTerrains.SelectMany(t => t.terrainData.treePrototypes)
            .Where(p => p.prefab != null).Select(p => AssetDatabase.GetAssetPath(p.prefab)).Distinct();
        foreach (string path in paths.Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool tree = root.name.StartsWith("Tree", StringComparison.OrdinalIgnoreCase)
                    || root.name.Contains("Pine") || root.name.Contains("DeadTree");
                if (!tree) continue;
                var capsules = root.GetComponentsInChildren<CapsuleCollider>();
                foreach (var mesh in root.GetComponentsInChildren<MeshCollider>()) UnityEngine.Object.DestroyImmediate(mesh);
                if (capsules.Length == 0)
                {
                    var renderers = root.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    var trunk = root.AddComponent<CapsuleCollider>();
                    trunk.direction = 1;
                    trunk.radius = Mathf.Max(0.1f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.1f);
                    trunk.height = Mathf.Max(trunk.radius * 2f, bounds.size.y * 0.7f);
                    trunk.center = root.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + trunk.height * 0.5f, bounds.center.z));
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var terrain in Terrain.activeTerrains) terrain.Flush();
    }
    [MenuItem("BunnyPop/Prepare Enemy Behaviour")]
    public static void PrepareEnemies()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        int priority = 30;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyAI>())
        {
            var body = enemy.GetComponentInChildren<Collider>();
            if (body == null)
            {
                var renderers = enemy.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException(enemy.name + " has no body or renderers.");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var capsule = enemy.gameObject.AddComponent<CapsuleCollider>();
                Vector3 scale = enemy.transform.lossyScale;
                capsule.height = Mathf.Max(1f, bounds.size.y / Mathf.Abs(scale.y));
                capsule.radius = Mathf.Clamp(Mathf.Min(bounds.extents.x / Mathf.Abs(scale.x),
                    bounds.extents.z / Mathf.Abs(scale.z)) * 0.65f, 0.2f, 0.8f);
                capsule.center = Vector3.up * capsule.height * 0.5f;
                body = capsule;
            }
            body.isTrigger = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.avoidancePriority = priority++;
            agent.speed = enemy is BeeAI bee ? bee.moveSpeed : enemy.walkSpeed;
            agent.height = Mathf.Max(2f, body.bounds.size.y);
            agent.radius = Mathf.Max(0.5f, Mathf.Min(body.bounds.extents.x, body.bounds.extents.z));
            agent.agentTypeID = agent.radius <= 4f ? 0 : agent.radius <= 8.5f ? 74002 : 74003;
            agent.autoRepath = true;
            enemy.minIdleTime = 0.7f;
            enemy.maxIdleTime = 2f;
            enemy.memoryDuration = 3f;
            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(agent);
        }
        ConfigureEnemyAnimations();
        BakeEnemySurfaces();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY_SETUP_OK");
    }

    static void ConfigureEnemyAnimations()
    {
        foreach (string controllerPath in new[] { "Assets/Orc/OrcAI.controller", "Assets/Slime/SlimeAnimation.controller" })
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var walk = machine.states.Select(s => s.state).First(s => s.name.EndsWith("|Walk"));
            var idle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name.EndsWith("|Idle"));
            if (idle == null)
            {
                string modelPath = controllerPath.Contains("/Orc/") ? "Assets/Orc/Orc.fbx" : "Assets/Slime/Enemy.fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().First(c => c.name.EndsWith("|Idle"));
                idle = machine.AddState(clip.name);
                idle.motion = clip;
            }
            if (!walk.transitions.Any(t => t.destinationState == idle))
            {
                var transition = walk.AddTransition(idle);
                transition.hasExitTime = false;
                transition.duration = 0.15f;
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            }
            if (!idle.transitions.Any(t => t.destinationState == walk))
            {
                var transition = idle.AddTransition(walk);
                transition.hasExitTime = false;
                transition.duration = 0.15f;
                transition.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
                transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            }
            machine.defaultState = idle;
            foreach (var transition in machine.anyStateTransitions)
            {
                bool death = transition.conditions.Any(c => c.parameter == "IsDead" && c.mode == AnimatorConditionMode.If);
                transition.canTransitionToSelf = false;
                if (!death && !transition.conditions.Any(c => c.parameter == "IsDead"))
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            }
            EditorUtility.SetDirty(controller);
        }
    }

    static void BakeEnemySurfaces()
    {
        foreach (int type in new[] { 0, 74002, 74003 })
        {
            var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>().FirstOrDefault(s => s.agentTypeID == type);
            if (surface == null) surface = new GameObject("Enemy Navigation " + type).AddComponent<NavMeshSurface>();
            surface.agentTypeID = type;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.All;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.6f;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Enemy navigation bake failed: " + type);
            string assetPath = "Assets/Scenes/SampleScene/" + (type == 0 ? "GameplayNavigation" : "EnemyNavigation" + type) + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            else
            {
                var generated = surface.navMeshData;
                surface.RemoveData();
                EditorUtility.CopySerialized(generated, existing);
                surface.navMeshData = existing;
                surface.AddData();
                UnityEngine.Object.DestroyImmediate(generated);
            }
        }
    }

    [MenuItem("BunnyPop/Prepare Gameplay Scene")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before preparing the scene.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var bunny = UnityEngine.Object.FindAnyObjectByType<BunnyController>();
        if (bunny == null) throw new InvalidOperationException("Player missing.");
        bunny.walkSpeed = 14f;
        bunny.sprintSpeed = 26f;
        bunny.crouchSpeed = 7f;
        bunny.rotationSpeed = 12f;
        bunny.jumpHeight = 8f;
        bunny.gravity = -60f;
        bunny.standingHeight = 2.69f;
        bunny.crouchingHeight = 1.5f;
        var capsule = bunny.GetComponent<CharacterController>();
        capsule.height = bunny.standingHeight;
        capsule.center = new Vector3(-0.04f, 1.31f, 0.2f);
        var attack = bunny.GetComponent<BunnyAttack>();
        var point = bunny.transform.Find("AttackPoint");
        if (point == null)
        {
            point = new GameObject("AttackPoint").transform;
            point.SetParent(bunny.transform, false);
        }
        point.localPosition = new Vector3(0f, 1.1f, 1f);
        attack.attackPoint = point;
        attack.enemyLayers = ~0;
        attack.attackRange = 9f;
        var interaction = bunny.GetComponent<BunnyInteraction>() ?? bunny.gameObject.AddComponent<BunnyInteraction>();
        var canvas = UnityEngine.Object.FindObjectsByType<Canvas>().First(c => c.isRootCanvas);
        var existing = canvas.transform.Find("InteractionPrompt");
        var prompt = existing != null ? existing.GetComponent<TextMeshProUGUI>() : new GameObject("InteractionPrompt", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        prompt.transform.SetParent(canvas.transform, false);
        prompt.fontSize = 26;
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.raycastTarget = false;
        prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(0.5f, 0.2f);
        prompt.rectTransform.anchoredPosition = Vector2.zero;
        prompt.rectTransform.sizeDelta = new Vector2(500, 60);
        prompt.text = "";
        interaction.prompt = prompt;
        var camera = UnityEngine.Object.FindAnyObjectByType<CameraController>();
        camera.minPitch = -35f;
        camera.maxPitch = 70f;
        // The custom camera is the sole transform driver.
        var brain = camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        if (brain != null) brain.enabled = false;

        var playerModifier = bunny.GetComponent<NavMeshModifier>() ?? bunny.gameObject.AddComponent<NavMeshModifier>();
        playerModifier.ignoreFromBuild = true;
        PrepareTreeColliders();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        PrepareEnemies();
        Debug.Log("BUNNYPOP_SETUP_OK: enemies=" + UnityEngine.Object.FindObjectsByType<EnemyAI>().Length
            + ", capsule colliders=" + UnityEngine.Object.FindObjectsByType<CapsuleCollider>().Length);
    }
}
#endif
