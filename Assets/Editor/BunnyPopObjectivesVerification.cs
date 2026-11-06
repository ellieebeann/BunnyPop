#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BunnyPopObjectivesVerification
{
    const string Key = "BunnyPop.ObjectivesVerification";
    static bool running;
    static int checks, errors;
    static double timeout;
    static BunnyPopPolishRunner runner;
    static BunnyPopObjectivesVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false); running = true; checks = errors = 0;
            timeout = EditorApplication.timeSinceStartup + 90;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += Log; EditorApplication.update += Watch;
            runner = new GameObject("Objectives verification runner").AddComponent<BunnyPopPolishRunner>();
            Object.DontDestroyOnLoad(runner); runner.StartCoroutine(Guard(Verify()));
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    static void Watch() { if (running && EditorApplication.timeSinceStartup > timeout) Finish(false, "Timeout"); }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Debug.Log("OBJECTIVES_PASS " + message);
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
        running = false; Application.logMessageReceived -= Log; EditorApplication.update -= Watch;
        Debug.Log((success ? "OBJECTIVES_VERIFICATION_OK " : "OBJECTIVES_VERIFICATION_FAILED ") + message);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1); else EditorApplication.ExitPlaymode();
    }
    static void Click(Transform root, string callback)
    {
        var button = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(b =>
            Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == callback));
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable, callback + " button is available");
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button,
            callback + " receives pointer input (target=" + button.name + ", top=" + (hits.Count > 0 ? hits[0].gameObject.name : "none") + ")");
        button.onClick.Invoke();
    }
    static void CheckIntro(GameManager game, string entry)
    {
        Check(game != null && game.IsAwaitingStart && game.objectivePanel.activeInHierarchy, entry + " opens Objectives immediately");
        Check(game.objectiveActionText.text == "Start", entry + " uses Start label");
        Check(GameManager.IsPaused && Time.timeScale == 0 && !GameManager.WantsCursorLocked, entry + " pauses gameplay and unlocks the cursor");
        Check(game.TimeRemaining == game.stageDuration, entry + " keeps the full timer");
    }
    static IEnumerator Verify()
    {
        yield return null; yield return null;
        var game = GameManager.instance;
        CheckIntro(game, "Direct SampleScene entry");
        Vector3 position = Object.FindAnyObjectByType<BunnyController>().transform.position;
        yield return new WaitForSecondsRealtime(0.3f);
        Check(game.TimeRemaining == game.stageDuration && Object.FindAnyObjectByType<BunnyController>().transform.position == position, "Timer and player stay still before Start");
        game.ResumeGame(); game.PauseGame(); game.ToggleObjectives();
        Check(game.IsAwaitingStart && game.objectivePanel.activeSelf && Time.timeScale == 0, "Normal pause/resume/objective shortcuts cannot skip Start");
        BunnyPopPolishVerification.Capture("objectives-start");
        yield return null;
        Click(game.objectivePanel.transform, nameof(GameManager.ContinueFromObjectives));
        Check(!game.IsAwaitingStart && !GameManager.IsPaused && Time.timeScale == 1 && GameManager.WantsCursorLocked, "Start enters gameplay and requests cursor lock");
        yield return new WaitForSecondsRealtime(0.2f);
        Check(game.TimeRemaining < game.stageDuration, "Countdown begins after Start");
        game.PauseGame();
        BunnyPopPolishVerification.Capture("objectives-pause");
        yield return null;
        Click(game.pausePanel.transform, nameof(GameManager.ToggleObjectives));
        Check(game.objectivePanel.activeSelf && !game.pausePanel.activeSelf && game.objectiveActionText.text == "Return to game", "Pause-menu Objectives uses Return to game");
        float remaining = game.TimeRemaining;
        yield return new WaitForSecondsRealtime(0.2f);
        Check(game.TimeRemaining == remaining && Time.timeScale == 0, "Reopened Objectives keeps gameplay paused");
        BunnyPopPolishVerification.Capture("objectives-return");
        yield return null;
        Click(game.objectivePanel.transform, nameof(GameManager.ContinueFromObjectives));
        Check(!GameManager.IsPaused && !game.objectivePanel.activeSelf && game.TimeRemaining == remaining, "Return resumes the current game without resetting progress");
        game.PauseGame(); yield return null;
        Click(game.pausePanel.transform, nameof(GameManager.RestartGame));
        yield return null; yield return null; yield return null;
        game = GameManager.instance; CheckIntro(game, "Restart");
        game.LoadMainMenu();
        yield return null; yield return null; yield return null;
        Check(SceneManager.GetActiveScene().name == "MainMenu" && Time.timeScale == 1, "Main menu remains usable after leaving the intro");
        var menu = Object.FindAnyObjectByType<MainMenuManager>();
        var menuCanvas = Object.FindAnyObjectByType<Canvas>();
        Click(menuCanvas.transform, nameof(MainMenuManager.PlayGame));
        float deadline = Time.realtimeSinceStartup + 20;
        while (SceneManager.GetActiveScene().name != "SampleScene" && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null; yield return null;
        game = GameManager.instance; CheckIntro(game, "Main Menu Play");
        Click(game.objectivePanel.transform, nameof(GameManager.ContinueFromObjectives));
        Check(!game.IsAwaitingStart && !GameManager.IsPaused && Time.timeScale == 1, "Main Menu Play also waits for Start before gameplay");
    }
}
#endif
