#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class BunnyPopGroundVerification
{
    const string Key = "BunnyPop.GroundVerification";
    static double timeout;
    static bool running;
    static int errors, checks;

    static BunnyPopGroundVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            running = true;
            errors = checks = 0;
            timeout = EditorApplication.timeSinceStartup + 45;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += Log;
            EditorApplication.update += Watch;
            UnityEngine.Object.FindAnyObjectByType<BunnyAttack>().StartCoroutine(Guard(Verify()));
        };
    }
    [MenuItem("BunnyPop/Verify Player Grounding")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Log(string text, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Watch()
    {
        if (running && EditorApplication.timeSinceStartup > timeout) Finish(false, "Timeout");
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception e) { Finish(false, e.ToString()); yield break; }
            yield return next;
        }
        Finish(true, checks + " checks passed");
    }
    static void Check(bool ok, string text)
    {
        if (!ok) throw new Exception(text);
        checks++;
        Debug.Log("GROUND_PASS: " + text);
    }
    static void ResetPlayer(BunnyController bunny, Vector3 position)
    {
        bunny.controller.enabled = false;
        bunny.transform.SetPositionAndRotation(position, Quaternion.identity);
        bunny.controller.enabled = true;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(BunnyController).GetField("planarVelocity", fields).SetValue(bunny, Vector3.zero);
        typeof(BunnyController).GetField("verticalSpeed", fields).SetValue(bunny, 0f);
        typeof(BunnyController).GetField("lastGrounded", fields).SetValue(bunny, float.NegativeInfinity);
        typeof(BunnyController).GetField("lastJump", fields).SetValue(bunny, float.NegativeInfinity);
        typeof(BunnyController).GetField("jumpInProgress", fields).SetValue(bunny, false);
        typeof(BunnyController).GetField("moveSupportNormal", fields).SetValue(bunny, Vector3.zero);
        typeof(BunnyController).GetField("steepSupport", fields).SetValue(bunny, false);
        Physics.SyncTransforms();
    }
    static IEnumerator Verify()
    {
        var bunny = UnityEngine.Object.FindAnyObjectByType<BunnyController>();
        bunny.enabled = false;
        foreach (var ai in UnityEngine.Object.FindObjectsByType<EnemyAI>())
        {
            ai.enabled = false;
            ai.GetComponent<NavMeshAgent>().enabled = false;
        }
        bunny.cameraTransform = new GameObject("Ground verification heading").transform;
        var animator = bunny.GetComponent<Animator>();
        var move = typeof(BunnyController).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic);
        Action<Vector2, bool, bool> step = (input, sprint, jump) => move.Invoke(bunny, new object[] { input, sprint, false, jump });
        var vertices = new Vector3[242];
        var triangles = new int[120 * 6];
        for (int i = 0; i <= 120; i++)
        {
            float z = i * 0.5f;
            float y = 0.25f * Mathf.Sin(z * 1.2f) + 0.1f * Mathf.Sin(z * 3.7f);
            vertices[i * 2] = new Vector3(0, y, z);
            vertices[i * 2 + 1] = new Vector3(40, y, z);
            if (i == 120) continue;
            int j = i * 6, v = i * 2;
            triangles[j] = v; triangles[j + 1] = v + 2; triangles[j + 2] = v + 1;
            triangles[j + 3] = v + 1; triangles[j + 4] = v + 2; triangles[j + 5] = v + 3;
        }
        var mesh = new Mesh { name = "Bumpy road verification mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        var road = new GameObject("Bumpy road verification");
        road.transform.position = new Vector3(980, 1000, 1000);
        road.AddComponent<MeshCollider>().sharedMesh = mesh;
        ResetPlayer(bunny, new Vector3(1000, 1000.7f, 1003));
        float until = Time.time + 0.5f;
        while (Time.time < until) { step(Vector2.zero, false, false); yield return null; }
        Check(bunny.IsGrounded && !animator.GetBool("IsJumping"), "Player settles onto bumpy road");
        int falseAirFrames = 0, groundedFrames = 0;
        Vector3 start = bunny.transform.position;
        until = Time.time + 2.5f;
        while (Time.time < until)
        {
            step(Vector2.up, false, false);
            if (animator.GetBool("IsJumping")) falseAirFrames++;
            if (bunny.IsGrounded) groundedFrames++;
            yield return null;
        }
        Check(bunny.transform.position.z > start.z + 20, "Player actually traverses the bumpy mesh");
        Check(falseAirFrames == 0, "No in-air animation flicker while walking on bumpy road; frames=" + falseAirFrames);
        Check(groundedFrames > 10, "Ground probe recognizes repeated uneven-road contact");
        step(Vector2.zero, false, true);
        Check(animator.GetBool("IsJumping") && !bunny.IsGrounded, "Requested jump immediately enters in-air state");
        float jumpStart = bunny.transform.position.y;
        until = Time.time + 0.2f;
        while (Time.time < until) { step(Vector2.zero, false, false); yield return null; }
        Check(bunny.transform.position.y > jumpStart + 1f, "Ground snap does not cancel an intentional jump");
        until = Time.time + 1.3f;
        while (Time.time < until) { step(Vector2.zero, false, false); yield return null; }
        Check(bunny.IsGrounded && !animator.GetBool("IsJumping"), "Landing restores grounded animation");
        until = Time.time + 1.5f;
        while (Time.time < until) { step(Vector2.up, true, false); yield return null; }
        Check(bunny.transform.position.z > 1065, "Player passes the road's real ledge");
        Check(!bunny.IsGrounded && animator.GetBool("IsJumping"), "Walking off a real ledge still triggers falling");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(1044, 1000, 1030);
        wall.transform.localScale = new Vector3(1, 30, 40);
        ResetPlayer(bunny, new Vector3(1041, 1000, 1030));
        until = Time.time + 0.25f;
        while (Time.time < until) { step(Vector2.zero, false, false); yield return null; }
        Check(!bunny.IsGrounded && animator.GetBool("IsJumping"), "Vertical wall contact does not count as walkable ground");
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.transform.SetPositionAndRotation(new Vector3(1100, 1000, 1100), Quaternion.Euler(-35, 0, 0));
        ramp.transform.localScale = new Vector3(30, 2, 100);
        ResetPlayer(bunny, new Vector3(1100, 989, 1080));
        until = Time.time + 0.5f;
        while (Time.time < until) { step(Vector2.zero, false, false); yield return null; }
        Vector3 rampStart = bunny.transform.position;
        until = Time.time + 1.4f;
        while (Time.time < until) { step(Vector2.up, false, false); yield return null; }
        Check(bunny.transform.position.z > rampStart.z + 10f && bunny.transform.position.y > rampStart.y + 6f,
            "Bunny climbs a 35-degree incline; displacement=" + (bunny.transform.position - rampStart));
        Check(bunny.IsGrounded && animator.GetCurrentAnimatorStateInfo(0).IsName("CharacterArmature|Walk"),
            "Walk animation plays while climbing");
        until = Time.time + 0.5f;
        while (Time.time < until) { step(Vector2.up, true, false); yield return null; }
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("CharacterArmature|Run"), "Sprint enters original run clip");
        until = Time.time + 0.5f;
        while (Time.time < until)
        {
            move.Invoke(bunny, new object[] { Vector2.zero, false, true, false });
            yield return null;
        }
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("CharacterArmature|Duck"), "Crouch enters original duck clip");
        ramp.transform.rotation = Quaternion.Euler(-55, 0, 0);
        ResetPlayer(bunny, new Vector3(1100, 1005, 1100));
        until = Time.time + 0.6f;
        while (Time.time < until) { step(Vector2.zero, false, false); yield return null; }
        Vector3 steepStart = bunny.transform.position;
        var vertical = typeof(BunnyController).GetField("verticalSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
        int hops = 0;
        float nextPress = Time.time;
        until = Time.time + 2f;
        while (Time.time < until)
        {
            float before = (float)vertical.GetValue(bunny);
            bool press = Time.time >= nextPress;
            if (press) nextPress = Time.time + 0.1f;
            step(Vector2.up, false, press);
            if (before <= 0f && (float)vertical.GetValue(bunny) > 0f) hops++;
            yield return null;
        }
        Check(hops >= 2 && bunny.transform.position.z > steepStart.z + 3f,
            "Repeated presses climb the 55-degree slope with separate jumps; hops=" + hops
            + ", displacement=" + (bunny.transform.position - steepStart));

        var slime = UnityEngine.Object.FindAnyObjectByType<SlimeAI>();
        var slimeAnimator = slime.GetComponent<Animator>();
        slimeAnimator.Play("MonsterArmature|Idle");
        yield return null;
        var idle = slimeAnimator.GetCurrentAnimatorClipInfo(0);
        Check(idle.Length > 0 && idle[0].clip.name == "MonsterArmature|Idle", "Slime idle uses the imported runtime clip");
        slimeAnimator.SetBool("IsWalking", true);
        yield return new WaitForSeconds(0.35f);
        Check(slimeAnimator.GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|Walk"), "Slime Animator switches from idle to walk");
        slime.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);
        yield return new WaitForSeconds(0.35f);
        Check(slimeAnimator.GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|Death"), "Slime enters death animation at zero HP");

        var bee = UnityEngine.Object.FindAnyObjectByType<BeeAI>();
        var beeAnimator = bee.GetComponent<Animator>();
        Check(beeAnimator.GetBool("IsFlying") && beeAnimator.GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|Flying"),
            "Bee starts in its original flying animation");
        beeAnimator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.3f);
        Check(beeAnimator.GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|Bite_Front"), "Bee attack trigger plays bite");
        bee.GetComponent<EnemyHealth>().TakeDamage(1);
        yield return new WaitForSeconds(0.3f);
        Check(beeAnimator.GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|HitRecieve"), "Bee damage plays hit reaction");
        bee.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);
        yield return new WaitForSeconds(0.3f);
        Check(beeAnimator.GetCurrentAnimatorStateInfo(0).IsName("MonsterArmature|Death"), "Bee death takes priority");

        var gate = UnityEngine.Object.FindAnyObjectByType<ForestGate>();
        var health = bunny.GetComponent<BunnyHealth>();
        var terrain = Terrain.activeTerrain;
        Vector3 approach = gate.transform.TransformPoint(new Vector3(0, 0, -8));
        approach.y = terrain.SampleHeight(approach) + terrain.transform.position.y + 0.5f;
        ResetPlayer(bunny, approach);
        bunny.cameraTransform.rotation = gate.transform.rotation;
        until = Time.time + 0.8f;
        while (Time.time < until) { step(Vector2.up, false, false); yield return null; }
        Check(gate.transform.InverseTransformPoint(bunny.transform.position).z < 0f
            && !gate.sentry.gameObject.activeSelf, "Closed gate blocks passage and keeps the sentry dormant");
        var interaction = bunny.GetComponent<BunnyInteraction>();
        var findTarget = typeof(BunnyInteraction).GetMethod("FindTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        Check((Component)findTarget.Invoke(interaction, new object[] { bunny.controller.bounds.center }) == gate
            && gate.Unlock(health), "E interaction selects and unlocks the forest gate");
        Check(!gate.gateCollider.enabled && !gate.obstacle.enabled && !gate.sentry.gameObject.activeSelf,
            "Unlocked gate clears the route before the player enters");
        until = Time.time + 0.85f;
        while (Time.time < until) { step(Vector2.up, false, false); yield return null; }
        Check(gate.sentry.HasNavigation && gate.sentry.CurrentState == EnemyAI.BehaviourState.Chase,
            "Crossing the gate activates the sentry and starts a chase");
        gate.sentry.enabled = false;
        gate.sentry.GetComponent<NavMeshAgent>().enabled = false;
        ResetPlayer(bunny, new Vector3(1200, 1000, 1200));
        var target = gate.sentry.GetComponent<EnemyHealth>();
        var body = target.GetComponentInChildren<Collider>();
        target.transform.position += bunny.controller.bounds.center + Vector3.forward * 25f - body.bounds.center;
        Physics.SyncTransforms();
        var wand = bunny.GetComponent<BunnyAttack>();
        Check(wand.aimCamera != null && wand.crosshair != null, "Gameplay camera and centered crosshair are assigned");
        var testCamera = new GameObject("Verification shoulder camera").AddComponent<Camera>();
        testCamera.enabled = false;
        testCamera.transform.position = bunny.controller.bounds.center + new Vector3(9f, 3f, -16f);
        testCamera.transform.LookAt(body.bounds.center);
        wand.aimCamera = testCamera;
        bunny.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Physics.SyncTransforms();
        int hp = target.currentHealth;
        int charges = wand.CarrotCharges;
        Check(wand.TryAttack(true), "Wand attack starts");
        yield return new WaitForSeconds(0.85f);
        Check(target.currentHealth == Mathf.Max(0, hp - wand.projectileDamage) && wand.CarrotCharges == charges - 1,
            "Carrot hits the crosshair target from an offset camera and spends one charge");
        var chargePrefab = target.wandChargeDropPrefab;
        target.seedDropPrefab = null;
        target.seedDropChance = 1f;
        target.TakeDamage(int.MaxValue);
        Check(target.DroppedSeed != null && target.DroppedSeed.carrotCharges == chargePrefab.carrotCharges
            && target.DroppedSeed.Collect(health) && wand.CarrotCharges == charges,
            "Defeated enemy drops a collectible wand recharge");
        Check(errors == 0, "No runtime errors during grounding verification");
    }
    static void Finish(bool ok, string text)
    {
        running = false;
        EditorApplication.update -= Watch;
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key, false);
        Debug.Log((ok ? "GROUND_VERIFICATION_OK: " : "GROUND_VERIFICATION_FAILED: ") + text);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
#endif
