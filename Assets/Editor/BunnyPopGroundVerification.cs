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
