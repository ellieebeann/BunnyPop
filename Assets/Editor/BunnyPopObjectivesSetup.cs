#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BunnyPopObjectivesSetup
{
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (game == null || game.objectivePanel == null) throw new InvalidOperationException("Objectives panel is not assigned.");
        var button = game.objectivePanel.GetComponentInChildren<UnityEngine.UI.Button>(true);
        if (button == null) throw new InvalidOperationException("Objectives action button is missing.");
        game.objectiveActionText = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (game.objectiveActionText == null) throw new InvalidOperationException("Objectives button label is missing.");
        bool wired = false;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (button.onClick.GetPersistentTarget(i) != game) continue;
            string method = button.onClick.GetPersistentMethodName(i);
            if (method == nameof(GameManager.ResumeGame)) UnityEventTools.RemovePersistentListener(button.onClick, i);
            if (method == nameof(GameManager.ContinueFromObjectives)) wired = true;
        }
        if (!wired) UnityEventTools.AddPersistentListener(button.onClick, game.ContinueFromObjectives);
        game.objectiveActionText.text = "Start";
        EditorUtility.SetDirty(game); EditorUtility.SetDirty(button); EditorUtility.SetDirty(game.objectiveActionText);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("OBJECTIVES_SETUP_OK");
    }
}
#endif
