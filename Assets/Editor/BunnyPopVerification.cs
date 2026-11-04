#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Runs the actual gameplay components in Play mode, including their coroutines.
[InitializeOnLoad]
public static class BunnyPopVerification
{
    private const string Key = "BunnyPopVerification.Running";
    private static int stage;
    private static double deadline, timeout;
    private static BunnyController bunny;
    private static BunnyHealth health;
    private static BunnyAttack attack;
    private static EnemyHealth enemy;
    private static GameObject wall;
    private static int initialHP;
    private static int runtimeErrors;
    private static bool movementDone;
    private static Exception movementFailure;

    static BunnyPopVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                Application.targetFrameRate = 60;
                stage = 0;
                deadline = EditorApplication.timeSinceStartup + 2;
                timeout = EditorApplication.timeSinceStartup + 60;
                Application.logMessageReceived += Log;
                EditorApplication.update += Tick;
            }
        };
    }

    [MenuItem("BunnyPop/Verify Gameplay")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) runtimeErrors++;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("BUNNYPOP_CHECK_FAILED: " + message);
        Debug.Log("BUNNYPOP_PASS: " + message);
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup > timeout) { Finish(false, "Verification timeout."); return; }
        if (EditorApplication.timeSinceStartup < deadline) return;
        try
        {
            switch (stage++)
            {
                case 0:
                    bunny = UnityEngine.Object.FindAnyObjectByType<BunnyController>();
                    health = bunny.GetComponent<BunnyHealth>();
                    attack = bunny.GetComponent<BunnyAttack>();
                    Check(attack.attackPoint != null && attack.enemyLayers.value != 0, "Attack scene references assigned");
                    var agents = UnityEngine.Object.FindObjectsByType<EnemyAI>();
                    Check(agents.Length == 11, "All eleven existing enemies retain AI");
                    foreach (var ai in agents)
                    {
                        var agent = ai.GetComponent<NavMeshAgent>();
                        Check(agent != null && agent.isOnNavMesh, ai.name + " placed on baked NavMesh");
                        ai.enabled = false;
                        agent.enabled = false;
                    }
                    Check(NavMesh.CalculateTriangulation().indices.Length > 0, "Persistent NavMesh contains walkable polygons");
                    VerifyTreeDetour();
                    // Freeze player input and move the real scene components to an isolated fixture.
                    bunny.enabled = false;
                    bunny.controller.enabled = false;
                    bunny.transform.SetPositionAndRotation(new Vector3(1000, 1000, 1000), Quaternion.identity);
                    bunny.controller.enabled = true;
                    enemy = agents.First(a => a.GetComponentInChildren<Collider>() != null).GetComponent<EnemyHealth>();
                    enemy.gameObject.SetActive(true);
                    var body = enemy.GetComponentInChildren<Collider>();
                    enemy.transform.position += attack.attackPoint.position + Vector3.forward * 4f - body.bounds.center;
                    var duplicate = new GameObject("Duplicate hit collider");
                    duplicate.transform.SetParent(enemy.transform, false);
                    duplicate.transform.position = body.bounds.center;
                    duplicate.AddComponent<SphereCollider>().radius = 1;
                    Physics.SyncTransforms();
                    initialHP = enemy.currentHealth;
                    movementDone = false;
                    movementFailure = null;
                    attack.StartCoroutine(MovementChecks());
                    deadline = EditorApplication.timeSinceStartup + 0.1;
                    break;
                case 1:
                    if (movementFailure != null) throw movementFailure;
                    if (!movementDone) { stage--; deadline = EditorApplication.timeSinceStartup + 0.1; return; }
                    Check(enemy.currentHealth == initialHP - attack.attackDamage, "Timed melee hits once despite multiple colliders");
                    attack.PerformAttack();
                    Check(enemy.currentHealth == initialHP - attack.attackDamage, "Animation event cannot duplicate damage");
                    attack.CancelAttack();
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "Verification trunk";
                    Vector3 launchPoint = bunny.controller.bounds.center;
                    var enemyBody = enemy.GetComponentInChildren<Collider>();
                    enemy.transform.position += launchPoint + Vector3.forward * 30f - enemyBody.bounds.center;
                    wall.transform.position = launchPoint + Vector3.forward * 10f;
                    wall.transform.localScale = new Vector3(20, 20, 1);
                    Physics.SyncTransforms();
                    deadline = EditorApplication.timeSinceStartup + 0.7;
                    break;
                case 2:
                    var aim = new GameObject("Obstacle verification camera").AddComponent<Camera>();
                    aim.enabled = false;
                    aim.transform.SetPositionAndRotation(bunny.controller.bounds.center + new Vector3(6f, 0f, -16f), Quaternion.identity);
                    aim.transform.LookAt(wall.transform.position);
                    attack.aimCamera = aim;
                    Check(attack.TryAttack(true), "Q starts a ranged wand attack");
                    deadline = EditorApplication.timeSinceStartup + 0.6;
                    break;
                case 3:
                    Check(enemy.currentHealth == initialHP - attack.attackDamage, "Solid trunk blocks the wand projectile");
                    UnityEngine.Object.Destroy(wall);
                    attack.CancelAttack();
                    var manager = GameManager.instance;
                    manager.allSeeds = 999;
                    int seedsBefore = manager.seeds;
                    var seed = UnityEngine.Object.FindAnyObjectByType<Seed>();
                    Check(seed != null && seed.Collect(health), "E interaction collection updates existing manager");
                    Check(manager.seeds == seedsBefore + seed.seedCount, "Seed counter increments");
                    Check(!seed.Collect(health), "Seed cannot be collected twice");
                    health.TakeDamage(10);
                    Check(health.currentHealth == health.maxHealth - 10, "Enemy damage reduces HP");
                    health.TakeDamage(10);
                    Check(health.currentHealth == health.maxHealth - 10, "Damage grace period prevents contact burst");
                    enemy.TakeDamage(999);
                    Check(enemy.IsDead && enemy.currentHealth == 0, "Enemy lethal damage clamps HP");
                    Check(enemy.GetComponent<Animator>().GetBool("IsDead"), "Enemy IsDead parameter set");
                    Check(!enemy.GetComponent<NavMeshAgent>().enabled, "Dead enemy navigation stops");
                    deadline = EditorApplication.timeSinceStartup + 0.8;
                    break;
                case 4:
                    Check(enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("CharacterArmature|Death")
                        || enemy.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|Death"),
                        "Enemy entered death animation");
                    health.TakeDamage(999);
                    Check(health.IsDead && health.currentHealth == 0, "Player lethal damage clamps HP");
                    Check(bunny.GetComponent<Animator>().GetBool("IsDead"), "Player IsDead parameter set");
                    Check(Time.timeScale == 1f, "Death does not freeze Animator immediately");
                    Check(!attack.TryAttack(false), "Dead player cannot attack");
                    deadline = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case 5:
                    Check(bunny.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("CharacterArmature|Death"), "Player entered death animation");
                    deadline = EditorApplication.timeSinceStartup + 1.3;
                    break;
                case 6:
                    Check(health.LosePanel.activeSelf && Time.timeScale == 0f, "Lose panel and pause follow death animation");
                    Check(runtimeErrors == 0, "No runtime errors during verification");
                    Finish(true, "Combat, cooldowns, occlusion, pickups, both death animations and scene navigation passed.");
                    break;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    private static IEnumerator MovementChecks()
    {
        enemy.gameObject.SetActive(false);
        bunny.cameraTransform = new GameObject("Verification heading").transform;
        bunny.cameraTransform.rotation = Quaternion.Euler(0, 90, 0);
        // Exercise the same movement step used by keyboard input, with deterministic controls.
        var step = typeof(BunnyController).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
        var canStand = typeof(BunnyController).GetMethod("CanStand", BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(1000, 998.8f, 1000);
        floor.transform.localScale = new Vector3(200, 2, 200);
        Physics.SyncTransforms();
        float end = Time.time + 0.3f;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.zero, false, false, false }); yield return null; }
        Vector3 start = bunny.transform.position;
        end = Time.time + 0.4f;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.up, false, false, false }); yield return null; }
        try
        {
            Vector3 delta = bunny.transform.position - start;
            Check(delta.x > 1f, "Camera-relative movement follows camera yaw");
            Check(bunny.controller.isGrounded, "Player remains grounded while walking");
        }
        catch (Exception e) { movementFailure = e; yield break; }
        bunny.cameraTransform.rotation = Quaternion.identity;
        // Crouch, then add a ceiling in the standing clearance volume.
        end = Time.time + 0.3f;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.zero, false, true, false }); yield return null; }
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.transform.position = bunny.transform.position + Vector3.up * 11f;
        ceiling.transform.localScale = new Vector3(30, 1, 30);
        Physics.SyncTransforms();
        try
        {
            Check(bunny.controller.height < bunny.standingHeight, "Crouch reduces player capsule height");
            Check(!(bool)canStand.Invoke(bunny, null), "Overhead obstruction prevents standing");
        }
        catch (Exception e) { movementFailure = e; yield break; }
        UnityEngine.Object.Destroy(ceiling);
        yield return null;
        end = Time.time + 0.4f;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.zero, false, false, false }); yield return null; }
        float baseY = bunny.transform.position.y;
        step.Invoke(bunny, new object[] { Vector2.zero, false, false, true });
        end = Time.time + 0.15f;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.zero, false, false, false }); yield return null; }
        try { Check(bunny.transform.position.y > baseY + 1f, "Grounded jump lifts player"); }
        catch (Exception e) { movementFailure = e; yield break; }
        end = Time.time + 1.3f;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.zero, false, false, false }); yield return null; }
        try { Check(Mathf.Abs(bunny.transform.position.y - baseY) < 0.2f, "Player lands back on the floor after jump"); }
        catch (Exception e) { movementFailure = e; yield break; }
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trunk.transform.position = bunny.transform.position + Vector3.forward * 10f + Vector3.up * 10f;
        trunk.transform.localScale = new Vector3(30, 20, 6);
        Physics.SyncTransforms();
        end = Time.time + 1.2f;
        float trunkZ = trunk.transform.position.z;
        while (Time.time < end) { step.Invoke(bunny, new object[] { Vector2.up, true, false, false }); yield return null; }
        try { Check(bunny.transform.position.z < trunkZ - 3f, "Sprinting player cannot pass through solid trunk"); }
        catch (Exception e) { movementFailure = e; yield break; }
        UnityEngine.Object.Destroy(trunk);
        UnityEngine.Object.Destroy(floor);
        bunny.controller.enabled = false;
        bunny.transform.SetPositionAndRotation(new Vector3(1000, 1000, 1000), Quaternion.identity);
        bunny.controller.enabled = true;
        enemy.gameObject.SetActive(true);
        var body = enemy.GetComponentInChildren<Collider>();
        enemy.transform.position += attack.attackPoint.position + Vector3.forward * 4f - body.bounds.center;
        Physics.SyncTransforms();
        try
        {
            Check(attack.TryAttack(false), "Melee attack starts");
            Check(!attack.TryAttack(false), "Attack spam is rejected");
        }
        catch (Exception e) { movementFailure = e; yield break; }
        yield return new WaitForSeconds(0.3f);
        movementDone = true;
    }

    private static void VerifyTreeDetour()
    {
        var path = new NavMeshPath();
        foreach (var terrain in Terrain.activeTerrains)
        {
            var data = terrain.terrainData;
            foreach (var tree in data.treeInstances)
            {
                var prefab = data.treePrototypes[tree.prototypeIndex].prefab;
                var trunk = prefab != null ? prefab.GetComponentInChildren<CapsuleCollider>() : null;
                if (trunk == null) continue;
                Vector3 center = terrain.transform.position + Vector3.Scale(tree.position, data.size);
                float offset = Mathf.Max(10f, trunk.radius * tree.widthScale * prefab.transform.lossyScale.x + 7f);
                if (!NavMesh.SamplePosition(center - Vector3.right * offset, out var left, 5f, NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(center + Vector3.right * offset, out var right, 5f, NavMesh.AllAreas)) continue;
                if (!NavMesh.Raycast(left.position, right.position, out _, NavMesh.AllAreas)) continue;
                if (!NavMesh.CalculatePath(left.position, right.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 3) continue;
                Check(true, "Baked path detours around terrain tree trunks");
                return;
            }
        }
        throw new Exception("Could not verify a reachable detour around a terrain tree.");
    }
    private static void Finish(bool success, string message)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false);
        Debug.Log((success ? "BUNNYPOP_VERIFICATION_OK: " : "BUNNYPOP_VERIFICATION_FAILED: ") + message);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
#endif
