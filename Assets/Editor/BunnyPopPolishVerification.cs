#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BunnyPopPolishVerification
{
    const string Key = "BunnyPop.PolishVerification";
    static bool running;
    static int checks, errors;
    static double deadline;
    static BunnyPopPolishRunner runner;
    static BunnyPopPolishVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            running = true; checks = errors = 0; deadline = EditorApplication.timeSinceStartup + 150;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += Log; EditorApplication.update += Watch;
            runner = new GameObject("Polish verification runner").AddComponent<BunnyPopPolishRunner>();
            Object.DontDestroyOnLoad(runner);
            runner.StartCoroutine(Guard(Verify()));
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Watch() { if (running && EditorApplication.timeSinceStartup > deadline) Finish(false, "Timed out"); }
    static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text);
        checks++; Debug.Log("POLISH_PASS " + text);
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            object next;
            try { if (!routine.MoveNext()) break; next = routine.Current; }
            catch (Exception error) { Finish(false, error.ToString()); yield break; }
            yield return next;
        }
        Finish(errors == 0, checks + " checks; runtime errors=" + errors);
    }
    static void Finish(bool success, string message)
    {
        if (!running) return;
        running = false; Time.timeScale = 1;
        Application.logMessageReceived -= Log; EditorApplication.update -= Watch;
        Debug.Log((success ? "POLISH_VERIFICATION_OK " : "POLISH_VERIFICATION_FAILED ") + message);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1); else EditorApplication.ExitPlaymode();
    }
    static void Click(GameObject panel, string name)
    {
        var button = panel.transform.Find(name)?.GetComponent<UnityEngine.UI.Button>();
        Check(button != null && button.interactable && button.onClick.GetPersistentEventCount() > 0, name + " button has a callback");
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current) { position=RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits=new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button, name+" receives pointer hits without an overlapping graphic");
        button.onClick.Invoke();
    }
    static IEnumerator Verify()
    {
        yield return null; yield return null;
        var menu = Object.FindAnyObjectByType<MainMenuManager>();
        Check(menu != null && Cursor.lockState == CursorLockMode.None, "Main menu opens with an unlocked cursor");
        Check(Object.FindObjectsByType<EventSystem>().Length == 1, "Menu has one EventSystem");
        Check(menu.settingsPanel == null && menu.howToPlayPanel == null, "Original main menu has no replacement overlays");
        Capture("menu");
        menu.OpenLeaveConfirm(); Check(menu.leaveConfirmPanel.activeSelf, "Quit confirmation opens");
        menu.CloseLeaveConfirm(); Check(!menu.leaveConfirmPanel.activeSelf, "Quit confirmation cancels");
        Click(Object.FindAnyObjectByType<Canvas>().gameObject, "Buttons/Play");
        for (int i=0; i<180 && SceneManager.GetActiveScene().name != "SampleScene"; i++) yield return null;
        yield return null; yield return null;
        var game = GameManager.instance;
        game.ContinueFromObjectives(); // Gameplay regressions start after the intro.
        var bunny = Object.FindAnyObjectByType<BunnyController>();
        var health = bunny.GetComponent<BunnyHealth>(); var attack = bunny.GetComponent<BunnyAttack>();
        Check(game != null && !GameManager.IsPaused && Time.timeScale == 1, "Play starts a fresh running game");
        Check(GameManager.WantsCursorLocked && (Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked), "Gameplay requests a locked cursor (batch Editor cannot capture the OS pointer)");
        Check(game.allSeeds == 5 && game.TimeRemaining > 295, "Five-seed objective and five-minute timer are configured");
        Check(Object.FindObjectsByType<Seed>().Count(s => s.carrotCharges == 0) == 5, "Five guaranteed world seeds exist");
        var gate = Object.FindAnyObjectByType<ForestGate>();
        Check(gate.Unlock(health), "Forest gate unlocks for the seed route");
        yield return new WaitForSeconds(0.2f);
        var filter = new NavMeshQueryFilter { agentTypeID=0, areaMask=NavMesh.AllAreas };
        foreach(var seed in Object.FindObjectsByType<Seed>().Where(s=>s.carrotCharges==0))
        {
            var terrain=Terrain.activeTerrain;
            Vector3 goal=seed.transform.position; goal.y=terrain.SampleHeight(goal)+terrain.transform.position.y;
            var path=new NavMeshPath();
            Check(NavMesh.SamplePosition(bunny.transform.position,out var start,10,filter)
                && NavMesh.SamplePosition(goal,out var end,10,filter)
                && NavMesh.CalculatePath(start.position,end.position,filter,path) && path.status==NavMeshPathStatus.PathComplete,
                seed.name+" has a complete route from camp");
        }
        Check(game.EnemiesRemaining == 12, "Goal includes eleven roaming enemies and the gate sentry");
        Check(Object.FindObjectsByType<EventSystem>().Length == 1, "Gameplay has one EventSystem");
        Check(attack.wandRenderers != null && attack.wandRenderers.Length > 0 && attack.wandRenderers.All(r=>r.forceRenderingOff), "Held carrot is hidden before unlocking the wand");
        Check(!attack.HasWand && game.wandChest.Interact(health) && attack.HasWand, "Camp chest unlocks the wand");
        Check(attack.wandRenderers.All(r=>!r.forceRenderingOff) && !bunny.transform.Find("Bunny").GetComponent<Renderer>().forceRenderingOff, "Opening the chest reveals the carrot and keeps Bunny visible");
        Check(!attack.projectilePrefab.instantKill && attack.projectileDamage == 20 && attack.projectilePrefab.popEffect != null && attack.castEffect != null && attack.projectilePrefab.popSound != null, "Bubble uses 20 damage with pop sound and casting/impact effects");
        var camera = attack.aimCamera.GetComponent<CameraController>();
        Check(!camera.cameraPivot.IsChildOf(bunny.transform), "Camera rig does not inherit Bunny rotations");
        CaptureMenuViews(bunny, attack.aimCamera);
        Check(bunny.walkSpeed == 18 && bunny.sprintSpeed == 32, "Player uses increased movement speeds");
        Check(!game.winPanel.GetComponentInParent<Canvas>(true).transform.Find("ObjectiveButton").gameObject.activeSelf, "Green Objectives button is hidden");
        Check(((RectTransform)game.pausePanel.transform.Find("PauseMenu")).anchoredPosition == Vector2.zero
            && ((RectTransform)game.pausePanel.transform.Find("Resume")).anchoredPosition.x == 0, "Pause board and controls are centered");
        Capture("hud");
        game.PauseGame();
        float timer = game.TimeRemaining;
        yield return new WaitForSecondsRealtime(0.15f);
        Check(GameManager.IsPaused && game.TimeRemaining == timer && Cursor.lockState == CursorLockMode.None, "Pause freezes the timer and releases the cursor"); Capture("pause");
        Click(game.pausePanel, "Resume");
        Check(!GameManager.IsPaused && Time.timeScale == 1 && GameManager.WantsCursorLocked, "Resume restores gameplay and requests cursor lock");
        game.PauseGame(); Click(game.pausePanel, "Restart");
        yield return null; yield return null; yield return null;
        game = GameManager.instance; game.ContinueFromObjectives(); bunny = Object.FindAnyObjectByType<BunnyController>();
        health = bunny.GetComponent<BunnyHealth>(); attack = bunny.GetComponent<BunnyAttack>();
        Check(!GameManager.IsPaused && Time.timeScale == 1 && GameManager.WantsCursorLocked, "Restart from pause resets static state and requests cursor lock");
        Check(!attack.HasWand && attack.wandRenderers.All(r=>r.forceRenderingOff), "Restart unequips and hides the carrot again");
        attack.UnlockWand();
        // Preserve real agents and animations while testing their ground binding and movement.
        bunny.enabled = false;
        bunny.controller.enabled = false; bunny.transform.position = new Vector3(2000, 1000, 2000); bunny.controller.enabled = true;
        yield return new WaitForSeconds(1);
        var bees = Object.FindObjectsByType<BeeAI>();
        var starts = bees.Select(b => b.transform.position).ToArray();
        yield return new WaitForSeconds(2);
        for(int i=0;i<bees.Length;i++)
        {
            var bee = bees[i];
            Check(bee.HasNavigation && bee.moveSpeed >= 18 && bee.chaseSpeed >= 26, bee.name + " has fast patrol/chase and valid ground navigation");
            Check(Vector3.Distance(starts[i], bee.transform.position)>1, bee.name + " moves while retaining its flying animation");
            Check(bee.GetComponent<Animator>().GetBool("IsFlying"), bee.name + " keeps IsFlying");
            Check(bee.GetComponentInChildren<Collider>().bounds.min.y >= Terrain.activeTerrain.SampleHeight(bee.transform.position), bee.name+" collider hovers above the physical ground");
        }
        var aiList = Object.FindObjectsByType<EnemyAI>();
        foreach (var ai in aiList) { ai.enabled = false; ai.GetComponent<NavMeshAgent>().enabled = false; }
        // Sweep an actual bubble at a scene bee in an isolated fixture, then verify grounded loot.
        var beeHealth = bees[0].GetComponent<EnemyHealth>();
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name="Drop test ground";
        floor.transform.position = new Vector3(1500, 990, 1500); floor.transform.localScale = new Vector3(100, 2, 100);
        bunny.controller.enabled=false; bunny.transform.position=new Vector3(1500,992,1500); bunny.controller.enabled=true;
        beeHealth.transform.position=new Vector3(1520,1030,1500); Physics.SyncTransforms();
        var distance=(float)typeof(EnemyAI).GetMethod("PlayerDistance",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(bees[0],null);
        var sight=typeof(EnemyAI).GetMethod("CanSeePlayer",BindingFlags.NonPublic|BindingFlags.Instance);
        Check(distance<=30 && (bool)sight.Invoke(bees[0],null),"Bee can sense a visible player below its hover height");
        camera=attack.aimCamera.GetComponent<CameraController>(); camera.enabled=false;
        attack.enabled=false;
        typeof(BunnyAttack).GetField("isAiming",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(attack,true);
        typeof(CameraController).GetField("pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(camera,-25f);
        typeof(CameraController).GetField("yaw",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(camera,0f);
        bunny.controller.enabled=false; bunny.transform.position=new Vector3(1500,992,1500); bunny.controller.enabled=true;
        var move=typeof(BunnyController).GetMethod("Move",BindingFlags.NonPublic|BindingFlags.Instance);
        var follow=typeof(CameraController).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
        Physics.SyncTransforms(); follow.Invoke(camera,null);
        Quaternion heading=camera.transform.rotation; float maxShake=0;
        for(int i=0;i<45;i++)
        {
            move.Invoke(bunny,new object[]{new Vector2(0.7f,0.7f),false,false,false});
            follow.Invoke(camera,null);
            maxShake=Mathf.Max(maxShake,Quaternion.Angle(heading,camera.transform.rotation));
            yield return null;
        }
        Check(maxShake<0.05f,"Holding aim while strafing and looking up keeps the camera heading stable");
        camera.enabled=true; attack.enabled=true;
        beeHealth.transform.position = new Vector3(1500, 1000, 1500); Physics.SyncTransforms();
        var bounds = beeHealth.GetComponentInChildren<Collider>().bounds;
        bees[0].enabled = true;
        bees[0].PlayActionSound(bees[0].attackSound);
        var buzz = bees[0].gameObject.AddComponent<AudioSource>(); buzz.clip=bees[0].attackSound; buzz.loop=true; buzz.Play();
        var shot = Object.Instantiate(attack.projectilePrefab, bounds.center - Vector3.forward*30, Quaternion.identity);
        shot.Launch(bunny.transform, attack.projectileDamage, Physics.AllLayers);
        yield return new WaitForSeconds(0.6f);
        Check(!beeHealth.IsDead && beeHealth.currentHealth == beeHealth.maxHealth - 20, "One bubble damages the enemy without instantly killing it");
        for (int i=0;i<2;i++)
        {
            var nextShot=Object.Instantiate(attack.projectilePrefab,bounds.center-Vector3.forward*30,Quaternion.identity);
            nextShot.Launch(bunny.transform,attack.projectileDamage,Physics.AllLayers);
            yield return new WaitForSeconds(0.6f);
        }
        Check(beeHealth.IsDead && beeHealth.GetComponent<Animator>().GetBool("IsDead"), "Three bubbles defeat a standard enemy and preserve the death animation");
        Check(bees[0].GetComponentsInChildren<AudioSource>(true).All(source=>!source.isPlaying && source.mute), "Bee attack and buzzing audio stop on death");
        var orc=aiList.OfType<OrcAI>().First(o=>!o.GetComponent<EnemyHealth>().IsDead);
        orc.enabled=true; orc.PlayActionSound(orc.attackSound);
        yield return null;
        Check(orc.GetComponentsInChildren<AudioSource>().Any(source=>source.isPlaying), "Orc attack sound plays on an enemy-owned source");
        orc.GetComponent<EnemyHealth>().TakeDamage(int.MaxValue);
        orc.PlayActionSound(orc.attackSound);
        yield return new WaitForSeconds(0.2f);
        Check(orc.GetComponentsInChildren<AudioSource>(true).All(source=>!source.isPlaying && source.mute), "Dead Orc cuts off attack audio and cannot restart it");
        var drop = beeHealth.DroppedSeed;
        Check(drop != null && Mathf.Abs(drop.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y)-991.55f)<1, "Flying enemy drop settles above solid ground");
        Check(shot == null, "Bubble is destroyed on impact");
        // At actual world heights, physics ground checks work independently of the NavMesh.
        game.ToggleObjectives(); Check(game.objectivePanel.activeSelf && GameManager.IsPaused, "Objectives panel pauses gameplay"); game.ResumeGame();
        foreach (var seed in Object.FindObjectsByType<Seed>().Where(s=>s.carrotCharges==0).ToArray()) Check(seed.Collect(health), "E seed collection updates inventory");
        Check(game.seeds == 5 && !game.HasEnded && !game.altar.Interact(health), "Five seeds alone cannot skip enemies and the altar");
        foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
            if (!enemy.IsDead) { enemy.seedDropChance=0; enemy.TakeDamage(int.MaxValue); }
        game.RefreshHUD();
        Check(game.CanRestore && !game.HasEnded && game.objectiveText.text.Contains("altar"), "Clearing enemies points the player back to the altar");
        Check(game.altar.Interact(health) && game.winPanel.activeSelf && game.HasEnded, "Restoring seeds triggers victory"); Capture("victory");
        Click(game.winPanel, "Button"); yield return null; yield return null; yield return null;
        game=GameManager.instance; game.ContinueFromObjectives(); health=Object.FindAnyObjectByType<BunnyHealth>();
        Check(game.seeds==0 && game.TimeRemaining>295 && !GameManager.IsPaused, "Victory restart resets progress");
        health.TakeDamage(int.MaxValue); yield return new WaitForSecondsRealtime(2);
        Check(game.losePanel.activeSelf && game.HasEnded && !GameManager.IsPaused, "Health death finishes its animation before showing Game Over"); Capture("defeat");
        Click(game.losePanel,"Button"); yield return null; yield return null; yield return null;
        game=GameManager.instance; game.ContinueFromObjectives();
        typeof(GameManager).GetProperty("TimeRemaining").SetValue(game,0.01f);
        yield return new WaitForSecondsRealtime(0.1f);
        Check(game.HasEnded && game.losePanel.activeSelf, "Timer expiry triggers Game Over");
        Click(game.losePanel,"MainMenuButton"); yield return null; yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name=="MainMenu" && Cursor.lockState==CursorLockMode.None && Time.timeScale==1, "Return to main menu resets time and cursor");
    }
    public static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var canvas = Object.FindAnyObjectByType<Canvas>(); var camera = Camera.main;
        if (canvas==null || camera==null) return;
        var mode=canvas.renderMode; var previousCamera=canvas.worldCamera;
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
        var rt=new RenderTexture(1600,900,24); rt.Create();
        camera.targetTexture=rt;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
        RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply(); RenderTexture.active=null;
        File.WriteAllBytes("Logs/polish-"+name+".png",texture.EncodeToPNG());
        camera.targetTexture=null; canvas.renderMode=mode; canvas.worldCamera=previousCamera;
        Object.Destroy(texture); rt.Release(); Object.Destroy(rt);
    }
    public static void Build() => BuildTo("Builds/Polished");
    public static void BuildRevision() => BuildTo("Builds/Revised");
    public static void BuildLatest() => BuildTo("Builds/Latest");
    public static void BuildRelease() => BuildTo("Builds/Release");
    static void BuildTo(string folder)
    {
        Directory.CreateDirectory(folder);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/SampleScene.unity"},
            locationPathName=folder+"/BunnyPop.exe", target=BuildTarget.StandaloneWindows64, options=BuildOptions.None });
        Debug.Log($"POLISH_BUILD {report.summary.result} errors={report.summary.totalErrors} size={report.summary.totalSize}");
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Windows build failed.");
    }
    static void CaptureMenuViews(BunnyController bunny, Camera camera)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        var canvas = Object.FindAnyObjectByType<Canvas>(); bool enabled = canvas.enabled; canvas.enabled = false;
        var terrain = Terrain.activeTerrain;
        var rt = new RenderTexture(1600,900,24); rt.Create(); camera.targetTexture = rt;
        for(int i=0;i<4;i++)
        {
            Vector3 p=bunny.transform.position+Quaternion.Euler(0,i*90,0)*Vector3.forward*35;
            p.y=terrain.SampleHeight(p)+terrain.transform.position.y+22;
            camera.transform.position=p; camera.transform.LookAt(bunny.controller.bounds.center+Vector3.up*2);
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply(); RenderTexture.active=null;
            File.WriteAllBytes("Logs/menu-view-"+i+".png",texture.EncodeToPNG()); Object.Destroy(texture);
        }
        camera.targetTexture=null; camera.transform.SetPositionAndRotation(position,rotation); canvas.enabled=enabled;
        rt.Release(); Object.Destroy(rt);
    }
}
public sealed class BunnyPopPolishRunner : MonoBehaviour { }
#endif
