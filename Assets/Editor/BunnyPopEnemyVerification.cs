#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class BunnyPopEnemyVerification
{
    const string Key = "BunnyPop.EnemyVerification";
    static bool running;
    static double timeout;
    static int errors, passed;
    static Exception failure;

    static BunnyPopEnemyVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            Application.targetFrameRate = 60;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-enemyTestSeed");
            int seed = index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : 17531;
            UnityEngine.Random.InitState(seed);
            running = true;
            timeout = EditorApplication.timeSinceStartup + 90;
            errors = passed = 0;
            failure = null;
            Application.logMessageReceived += Log;
            EditorApplication.update += Watch;
            var runner = UnityEngine.Object.FindAnyObjectByType<BunnyAttack>();
            runner.StartCoroutine(Guard(Checks()));
        };
    }
    [MenuItem("BunnyPop/Verify Enemy Behaviour")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        passed++;
        Debug.Log("ENEMY_PASS: " + message);
    }
    static void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Watch()
    {
        if (!running) return;
        if (failure != null) Finish(false, failure.ToString());
        else if (EditorApplication.timeSinceStartup > timeout) Finish(false, "Timed out");
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception e) { failure = e; yield break; }
            yield return next;
        }
        Finish(true, passed + " checks passed.");
    }

    static IEnumerator Checks()
    {
        var bunny = UnityEngine.Object.FindAnyObjectByType<BunnyController>();
        bunny.enabled = false;
        var controller = bunny.GetComponent<CharacterController>();
        controller.enabled = false;
        bunny.transform.position = new Vector3(2000, 1000, 2000);
        controller.enabled = true;
        var health = bunny.GetComponent<BunnyHealth>();
        yield return new WaitForSeconds(1f);
        var enemies = UnityEngine.Object.FindObjectsByType<EnemyAI>();
        Check(enemies.Length == 11, "All 11 scene enemies retain their scripts");
        var previous = enemies.Select(e => e.transform.position).ToArray();
        var travelled = new float[enemies.Length];
        float until = Time.time + 8f;
        while (Time.time < until)
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                travelled[i] += Vector3.Distance(previous[i], enemies[i].transform.position);
                previous[i] = enemies[i].transform.position;
            }
            yield return null;
        }
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            Debug.Log("ENEMY_OBSERVATION: " + e.name + " distance=" + travelled[i] + " state=" + e.CurrentState + " home=" + e.HomePosition
                + " radius=" + e.GetComponent<NavMeshAgent>().radius + " position=" + e.transform.position);
            Check(e.HasNavigation, e.name + " has active navigation");
            Check(travelled[i] > 2f, e.name + " actually roams over time");
            Check(Vector2.Distance(new Vector2(e.transform.position.x, e.transform.position.z), new Vector2(e.HomePosition.x, e.HomePosition.z))
                <= e.roamRadius + 2f, e.name + " patrol stays inside its home territory");
            e.enabled = false;
            e.GetComponent<NavMeshAgent>().enabled = false;
        }

        // Isolated, collider-backed navigation island for repeatable behavior transitions.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Enemy verification floor";
        floor.transform.position = new Vector3(1000, -1, 1000);
        floor.transform.localScale = new Vector3(120, 2, 120);
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trunk.name = "Enemy verification trunk";
        trunk.transform.position = new Vector3(985, 10, 1000);
        trunk.transform.localScale = new Vector3(8, 20, 10);
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
        foreach (var box in new[] { floor, trunk })
            sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(box.transform.position, box.transform.rotation, Vector3.one),
                size = box.transform.localScale, area = 0 });
        var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, new Bounds(new Vector3(1000, 10, 1000), new Vector3(130, 40, 130)), Vector3.zero, Quaternion.identity);
        var nav = NavMesh.AddNavMeshData(data);
        Check(nav.valid, "Behavior fixture has a valid collider-based NavMesh");
        var template = enemies.OfType<OrcAI>().First(e => e.GetComponent<NavMeshAgent>().agentTypeID == 0);
        var clone = UnityEngine.Object.Instantiate(template.gameObject, new Vector3(970, 0, 1000), Quaternion.identity);
        clone.name = "Behavior verification Orc";
        var ai = clone.GetComponent<OrcAI>();
        var agent = clone.GetComponent<NavMeshAgent>();
        agent.enabled = true;
        ai.enabled = true;
        ai.player = bunny.transform;
        ai.detectionRadius = 45;
        ai.attackRadius = 12;
        ai.attackCooldown = 0.9f;
        ai.attackWindup = 0.3f;
        ai.attackDamage = 7;
        ai.memoryDuration = 0.6f;
        ai.minIdleTime = 0.2f;
        ai.maxIdleTime = 0.4f;
        ai.roamRadius = 30;
        controller.enabled = false;
        bunny.transform.position = new Vector3(1000, 0, 1000);
        controller.enabled = true;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f);
        Check(ai.CurrentState != EnemyAI.BehaviourState.Chase && ai.CurrentState != EnemyAI.BehaviourState.Attack,
            "Solid trunk prevents detecting the player through it");

        controller.enabled = false;
        bunny.transform.position = new Vector3(2000, 1000, 2000);
        controller.enabled = true;
        typeof(EnemyAI).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ai, EnemyAI.BehaviourState.Patrol);
        agent.isStopped = false;
        agent.speed = 15;
        agent.stoppingDistance = 0.7f;
        Vector3 destination = new Vector3(1005, 0, 1000);
        agent.SetDestination(destination);
        bool clipped = false;
        var enemyBody = ai.GetComponentInChildren<Collider>();
        until = Time.time + 6f;
        while (Time.time < until && Vector3.Distance(ai.transform.position, destination) > 3f)
        {
            if (Physics.ComputePenetration(enemyBody, enemyBody.transform.position, enemyBody.transform.rotation,
                trunk.GetComponent<Collider>(), trunk.transform.position, trunk.transform.rotation, out _, out float depth) && depth > 0.1f)
                clipped = true;
            yield return null;
        }
        Check(!clipped, "Moving enemy body does not penetrate solid trunk");
        Check(Vector3.Distance(ai.transform.position, destination) < 4f, "Enemy physically completes detour around trunk");
        agent.Warp(new Vector3(970, 0, 1000));
        ai.transform.position = agent.nextPosition;
        agent.ResetPath();

        controller.enabled = false;
        bunny.transform.position = new Vector3(990, 0, 980);
        controller.enabled = true;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.4f);
        Check(ai.CurrentState == EnemyAI.BehaviourState.Chase, "Visible player triggers chase");
        Vector3 chaseStart = ai.transform.position;
        yield return new WaitForSeconds(3f);
        Check(Vector3.Distance(ai.transform.position, chaseStart) > 1f,
            "Chasing enemy physically advances; delta=" + Vector3.Distance(ai.transform.position, chaseStart)
            + " state=" + ai.CurrentState + " blocked=" + (ai.BlockingCollider != null ? ai.BlockingCollider.name : "none"));
        controller.enabled = false;
        bunny.transform.position = ai.transform.position + Vector3.right * 8f;
        controller.enabled = true;
        Physics.SyncTransforms();
        int hp = health.currentHealth;
        yield return new WaitForSeconds(1.2f);
        Check(health.currentHealth < hp && hp - health.currentHealth <= ai.attackDamage * 2,
            "Enemy attacks apply damage at cooldown intervals in range");
        Check(ai.CurrentState == EnemyAI.BehaviourState.Attack, "In-range enemy holds attack state");
        hp = health.currentHealth;
        controller.enabled = false;
        bunny.transform.position = new Vector3(2000, 1000, 2000);
        controller.enabled = true;
        yield return new WaitForSeconds(0.2f);
        Check(ai.CurrentState == EnemyAI.BehaviourState.Search || ai.CurrentState == EnemyAI.BehaviourState.Idle,
            "Lost player triggers last-seen search");
        yield return new WaitForSeconds(1.2f);
        Check(health.currentHealth == hp, "Out-of-range player takes no delayed damage");
        Check(ai.CurrentState != EnemyAI.BehaviourState.Chase && ai.CurrentState != EnemyAI.BehaviourState.Attack
            && ai.CurrentState != EnemyAI.BehaviourState.Search, "Enemy abandons chase after memory expires");

        var enemyHealth = clone.GetComponent<EnemyHealth>();
        var lootTemplate = new GameObject("Verification seed template");
        var seedPrefab = lootTemplate.AddComponent<Seed>();
        lootTemplate.AddComponent<SphereCollider>().isTrigger = true;
        lootTemplate.SetActive(false);
        enemyHealth.seedDropPrefab = seedPrefab;
        enemyHealth.seedDropChance = 1f;
        enemyHealth.TakeDamage(9999);
        Vector3 deathPosition = ai.transform.position;
        yield return new WaitForSeconds(0.5f);
        Check(ai.CurrentState == EnemyAI.BehaviourState.Dead && !agent.enabled, "Death exits AI and disables agent");
        Check(Vector3.Distance(deathPosition, ai.transform.position) < 0.01f, "Dead enemy cannot keep moving");
        Check(enemyHealth.DroppedSeed != null && enemyHealth.DroppedSeed.CanCollect, "Configured enemy loot drops an E-collectible seed");
        Check(errors == 0, "No runtime errors");
        nav.Remove();
    }
    static void Finish(bool success, string message)
    {
        running = false;
        EditorApplication.update -= Watch;
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false);
        Debug.Log((success ? "ENEMY_VERIFICATION_OK: " : "ENEMY_VERIFICATION_FAILED: ") + message);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
#endif
