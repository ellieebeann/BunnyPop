#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class BunnyPopForestSetup
{
    const string Folder = "Assets/ForestEncounter";

    public static void Prepare()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "ForestEncounter");
        var player = UnityEngine.Object.FindAnyObjectByType<BunnyController>();
        var template = UnityEngine.Object.FindObjectsByType<OrcAI>().First(a => a.GetComponent<NavMeshAgent>().agentTypeID == 0);
        var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        var terrain = Terrain.activeTerrain;
        Vector3 Ground(Vector3 point)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            return point;
        }

        Vector3 gatePosition = default, sentryPosition = default, direction = default;
        if (!NavMesh.SamplePosition(Ground(player.transform.position), out var start, 8f, filter))
            throw new InvalidOperationException("Bunny spawn is not near the forest NavMesh.");
        var route = new NavMeshPath();
        for (int i = 0; i < 16 && direction == Vector3.zero; i++)
        {
            Vector3 forward = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
            if (!NavMesh.SamplePosition(Ground(start.position + forward * 27f), out var gateHit, 3f, filter)
                || !NavMesh.SamplePosition(Ground(gateHit.position + forward * 27f), out var enemyHit, 5f, filter)
                || NavMesh.Raycast(start.position, gateHit.position, out _, filter)
                || NavMesh.Raycast(gateHit.position, enemyHit.position, out _, filter)
                || !NavMesh.CalculatePath(gateHit.position, enemyHit.position, filter, route)
                || route.status != NavMeshPathStatus.PathComplete) continue;
            if (Mathf.Abs(gateHit.position.y - start.position.y) > 7f) continue;
            gatePosition = gateHit.position;
            sentryPosition = enemyHit.position;
            direction = forward;
        }
        if (direction == Vector3.zero) throw new InvalidOperationException("No suitable entrance route was found.");

        var previous = GameObject.Find("Forest Encounter");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        var encounter = new GameObject("Forest Encounter");
        var gate = new GameObject("Wooden Forest Gate").AddComponent<ForestGate>();
        gate.transform.SetParent(encounter.transform, false);
        gate.transform.SetPositionAndRotation(Ground(gatePosition), Quaternion.LookRotation(direction));
        var modifier = gate.gameObject.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
        var wood = Material("Gate Wood", new Color(0.28f, 0.115f, 0.045f), Color.black);
        var trim = Material("Gate Trim", new Color(0.12f, 0.058f, 0.026f), Color.black);
        Box("Left post", gate.transform, new Vector3(-9, 4, 0), new Vector3(1.5f, 12, 2), trim, true);
        Box("Right post", gate.transform, new Vector3(9, 4, 0), new Vector3(1.5f, 12, 2), trim, true);
        Box("Left fence", gate.transform, new Vector3(-17, 2.5f, 0), new Vector3(15, 6, 1.4f), wood, true);
        Box("Right fence", gate.transform, new Vector3(17, 2.5f, 0), new Vector3(15, 6, 1.4f), wood, true);

        var hinge = new GameObject("Gate Hinge").transform;
        hinge.SetParent(gate.transform, false);
        hinge.localPosition = new Vector3(-8.3f, 0, 0);
        gate.hinge = hinge;
        for (int i = 0; i < 9; i++)
            Box("Wooden slat", hinge, new Vector3(0.9f + i * 1.85f, 3.4f, 0), new Vector3(1.66f, 8.8f, 0.65f), wood, false);
        foreach (float y in new[] { 1.4f, 5.5f })
            Box("Cross brace", hinge, new Vector3(8.3f, y, -0.48f), new Vector3(16.6f, 0.8f, 0.5f), trim, false);
        var collider = hinge.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(8.3f, 3.4f, 0);
        collider.size = new Vector3(16.6f, 8.8f, 1.1f);
        gate.gateCollider = collider;
        var obstacle = hinge.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = collider.center;
        obstacle.size = collider.size;
        obstacle.carving = true;
        gate.obstacle = obstacle;

        var sentry = UnityEngine.Object.Instantiate(template, sentryPosition, Quaternion.LookRotation(-direction), encounter.transform);
        sentry.name = "Evil Elf - Forest Sentry";
        sentry.detectionRadius = 48f;
        sentry.roamRadius = 18f;
        sentry.player = player.transform;
        sentry.gameObject.SetActive(false);
        gate.sentry = sentry;

        ConfigureProjectileAim(player, restoreCarrot: false);

        var source = UnityEngine.Object.FindObjectsByType<Seed>().First(s => s.carrotCharges == 0);
        var seed = UnityEngine.Object.Instantiate(source.gameObject);
        seed.name = "Magical Seed Loot";
        var seedPrefab = SavePrefab(seed).GetComponent<Seed>();
        var charge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        charge.name = "Wand Charge Loot";
        charge.transform.localScale = Vector3.one * 2.2f;
        charge.GetComponent<Collider>().isTrigger = true;
        charge.GetComponent<Renderer>().sharedMaterial = Material("Charge Glow", new Color(0.25f, 0.7f, 0.95f), new Color(0.08f, 0.25f, 0.35f));
        var pickup = charge.AddComponent<Seed>();
        pickup.seedCount = 0;
        pickup.carrotCharges = 2;
        var chargePrefab = SavePrefab(charge).GetComponent<Seed>();
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
        {
            enemy.seedDropPrefab = seedPrefab;
            enemy.wandChargeDropPrefab = chargePrefab;
        }

        sentry.player = null;
        var encounterPrefab = SavePrefab(encounter);
        PrefabUtility.InstantiatePrefab(encounterPrefab, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"FOREST_SETUP_OK: gate={gatePosition}, sentry={sentryPosition}, forward={direction}");
    }

    public static void PrepareProjectileAim()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        ConfigureProjectileAim(UnityEngine.Object.FindAnyObjectByType<BunnyController>(), restoreCarrot: true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PROJECTILE_AIM_SETUP_OK");
    }

    static void ConfigureProjectileAim(BunnyController player, bool restoreCarrot)
    {
        var attack = player.GetComponent<BunnyAttack>();
        const string path = Folder + "/Glowing Carrot Projectile.prefab";
        var carrotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<WandProjectile>();
        if (carrotPrefab == null)
        {
            var carrot = new GameObject("Glowing Carrot Projectile");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Carrot/carrot.obj");
            var meshObject = UnityEngine.Object.Instantiate(model, carrot.transform);
            meshObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            meshObject.transform.localScale = Vector3.one * 7f;
            foreach (var collider in meshObject.GetComponentsInChildren<Collider>())
                UnityEngine.Object.DestroyImmediate(collider);
            var orange = Material("Carrot Glow", new Color(1f, 0.34f, 0.065f), new Color(0.55f, 0.16f, 0.025f));
            var green = Material("Carrot Leaves", new Color(0.24f, 0.75f, 0.21f), new Color(0.04f, 0.14f, 0.025f));
            foreach (var renderer in meshObject.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    m != null && m.name.IndexOf("green", StringComparison.OrdinalIgnoreCase) >= 0 ? green : orange).ToArray();
            carrot.AddComponent<WandProjectile>();
            carrotPrefab = SavePrefab(carrot).GetComponent<WandProjectile>();
        }

        if (restoreCarrot || attack.projectilePrefab == null) attack.projectilePrefab = carrotPrefab;
        attack.aimCamera = Camera.main;
        if (attack.aimCamera == null) throw new InvalidOperationException("Main gameplay camera was not found.");

        var canvas = player.GetComponent<BunnyInteraction>().prompt.GetComponentInParent<Canvas>(true).rootCanvas;
        var crosshair = canvas.transform.Find("Wand Crosshair") as RectTransform;
        if (crosshair == null)
        {
            crosshair = new GameObject("Wand Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
            crosshair.SetParent(canvas.transform, false);
            crosshair.anchorMin = crosshair.anchorMax = crosshair.pivot = Vector2.one * 0.5f;
            crosshair.anchoredPosition = Vector2.zero;
            crosshair.sizeDelta = new Vector2(28, 28);
            Mark("Center", Vector2.zero, new Vector2(3, 3));
            Mark("Left", new Vector2(-8, 0), new Vector2(7, 2));
            Mark("Right", new Vector2(8, 0), new Vector2(7, 2));
            Mark("Top", new Vector2(0, 8), new Vector2(2, 7));
            Mark("Bottom", new Vector2(0, -8), new Vector2(2, 7));
        }

        attack.crosshair = crosshair.gameObject;
        crosshair.gameObject.SetActive(false);

        void Mark(string name, Vector2 position, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.rectTransform.SetParent(crosshair, false);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * 0.5f;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.color = new Color(0.93f, 0.98f, 1f, 0.92f);
            image.raycastTarget = false;
            var outline = image.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.02f, 0.08f, 0.12f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);
        }
    }

    static Material Material(string name, Color color, Color emission)
    {
        string path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.25f);
        if (emission != Color.black) material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
    }

    static GameObject SavePrefab(GameObject obj)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(obj, Folder + "/" + obj.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(obj);
        return prefab;
    }
}
#endif
