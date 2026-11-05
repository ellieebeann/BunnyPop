#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class BunnyPopRevisionSetup
{
    public static void ConfigureWandVisibility()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var bunny=Object.FindAnyObjectByType<BunnyController>();
        var attack=bunny.GetComponent<BunnyAttack>();
        attack.wandRenderers=bunny.transform.Find("Carrot").GetComponentsInChildren<Renderer>(true);
        foreach(var renderer in attack.wandRenderers)
        {
            renderer.forceRenderingOff=attack.requiresWandChest;
            EditorUtility.SetDirty(renderer);
            if(PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        EditorUtility.SetDirty(attack);
        if(PrefabUtility.IsPartOfPrefabInstance(attack)) PrefabUtility.RecordPrefabInstancePropertyModifications(attack);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("WAND_VISIBILITY_SETUP_OK");
    }
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var bunny = Object.FindAnyObjectByType<BunnyController>();
        bunny.walkSpeed = 18; bunny.sprintSpeed = 32; bunny.crouchSpeed = 9; bunny.acceleration = 80;
        bunny.GetComponent<BunnyAttack>().projectileDamage = 20;
        foreach (var enemy in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
        {
            enemy.walkSpeed = 14; enemy.chaseSpeed = enemy is BeeAI ? 34 : 20;
            if (enemy is BeeAI bee) bee.moveSpeed = 24;
            var agent = enemy.GetComponent<NavMeshAgent>();
            agent.speed = enemy is BeeAI ? 24 : 14;
            EditorUtility.SetDirty(enemy); EditorUtility.SetDirty(agent);
            if (PrefabUtility.IsPartOfPrefabInstance(enemy))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
                PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
            }
        }
        var game = Object.FindAnyObjectByType<GameManager>();
        var canvas = game.pausePanel.GetComponentInParent<Canvas>(true);
        var objectives = canvas.transform.Find("ObjectiveButton");
        if (objectives != null) objectives.gameObject.SetActive(false);
        var board = (RectTransform)game.pausePanel.transform.Find("PauseMenu");
        board.pivot = new Vector2(0.36f, 0.5f); board.anchoredPosition = Vector2.zero;
        foreach (string name in new[] { "Resume", "Restart", "MainMenu" })
        {
            var rect = (RectTransform)game.pausePanel.transform.Find(name);
            var pos = rect.anchoredPosition; pos.x = 0; rect.anchoredPosition = pos;
        }
        foreach (string name in new[] { "Settings", "Help" })
        {
            var rect = (RectTransform)game.pausePanel.transform.Find(name);
            var pos = rect.anchoredPosition; pos.x = name == "Settings" ? -40 : 40; rect.anchoredPosition = pos;
        }
        foreach (var text in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
            text.text = text.text.Replace("Each bubble defeats one enemy.", "Bubbles deal 20 damage; a standard enemy takes three hits.");
        var prefab = PrefabUtility.LoadPrefabContents("Assets/ForestEncounter/Bubble Projectile.prefab");
        prefab.GetComponent<WandProjectile>().instantKill = false;
        PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/ForestEncounter/Bubble Projectile.prefab");
        PrefabUtility.UnloadPrefabContents(prefab);
        prefab = PrefabUtility.LoadPrefabContents("Assets/ForestEncounter/Wand Charge Loot.prefab");
        prefab.GetComponent<Seed>().carrotCharges = 3;
        PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/ForestEncounter/Wand Charge Loot.prefab");
        PrefabUtility.UnloadPrefabContents(prefab);
        EditorUtility.SetDirty(bunny); EditorUtility.SetDirty(bunny.GetComponent<BunnyAttack>());
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("REVISION_SETUP_OK");
    }
}
#endif
