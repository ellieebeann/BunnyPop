using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    [Header("Goal")]
    public int seeds;
    public int allSeeds = 5;
    public float stageDuration = 300f;
    public bool requireAllEnemies = true;
    public bool requireAltar = true;
    public SeedAltar altar;
    public WandChest wandChest;
    public TMP_Text seedsText, objectiveText, timerText, healthText, enemyText;
    public UnityEngine.UI.Image[] seedSlots;
    public Color emptySeedColor = new Color(0.3f, 0.23f, 0.14f, 0.8f);
    public Color collectedSeedColor = Color.white;
    [Header("UI Panels & Buttons")]
    public GameObject winPanel, losePanel, pausePanel, pauseButton, objectivePanel, settingsPanel;
    public TMP_Text outcomeText;
    public TMP_Text objectiveActionText;
    [Header("Audio")]
    public AudioClip winSound, loseSound;
    [Header("Scene Settings")]
    public string mainMenuSceneName = "MainMenu";
    public static bool IsPaused { get; private set; }
    public static bool WantsCursorLocked { get; private set; }
    public bool HasEnded { get; private set; }
    public bool IsDying { get; private set; }
    public bool IsAwaitingStart { get; private set; }
    public float TimeRemaining { get; private set; }
    public int EnemiesRemaining { get; private set; }
    public bool CanRestore => seeds >= allSeeds && (!requireAllEnemies || EnemiesRemaining == 0);
    private EnemyHealth[] enemies;
    private BunnyHealth player;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        IsAwaitingStart = objectivePanel != null;
        IsPaused = IsAwaitingStart;
        Time.timeScale = IsAwaitingStart ? 0f : 1f;
        TimeRemaining = stageDuration;
    }
    void Start()
    {
        player = Object.FindAnyObjectByType<BunnyHealth>();
        enemies = Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include);
        Set(winPanel, false); Set(losePanel, false); Set(pausePanel, false);
        Set(objectivePanel, IsAwaitingStart); Set(settingsPanel, false); Set(pauseButton, !IsAwaitingStart);
        SetObjectiveActionLabel(IsAwaitingStart ? "Start" : "Return to game");
        LockCursor(!IsAwaitingStart);
        RefreshHUD();
    }
    void Update()
    {
        if (HasEnded || IsDying || IsAwaitingStart) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (IsPaused) ResumeGame(); else PauseGame();
        }
        if (Input.GetKeyDown(KeyCode.Tab)) ToggleObjectives();
        if (!IsPaused)
        {
            TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
            if (TimeRemaining <= 0f) GameOver("Time ran out. The forest still needs you.");
            // A click reacquires focus after leaving the window or resizing Game view.
            if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) LockCursor(true);
        }
        RefreshHUD();
    }
    public void AddSeeds(int amount)
    {
        if (HasEnded || IsDying || amount <= 0) return;
        seeds = Mathf.Min(allSeeds, seeds + amount);
        RefreshHUD();
        if (!requireAltar && CanRestore) WinGame();
    }
    public void RefreshHUD()
    {
        EnemiesRemaining = 0;
        if (enemies != null) foreach (var enemy in enemies) if (enemy != null && !enemy.IsDead) EnemiesRemaining++;
        if (seedsText != null) seedsText.text = seeds + " / " + allSeeds;
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(TimeRemaining);
            timerText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            timerText.color = TimeRemaining <= 60 ? new Color(1f, 0.45f, 0.32f) : Color.white;
        }
        if (healthText != null && player != null) healthText.text = player.GetHealth() + " / " + player.maxHealth;
        if (enemyText != null) enemyText.text = EnemiesRemaining + " enemies left";
        if (objectiveText != null)
        {
            if (CanRestore) objectiveText.text = "Return to the camp altar  [E]";
            else if (wandChest != null && !wandChest.IsOpened) objectiveText.text = "Open the chest at camp  [E]";
            else if (seeds < allSeeds) objectiveText.text = "Recover the magic seeds  " + seeds + " / " + allSeeds;
            else if (requireAllEnemies && EnemiesRemaining > 0) objectiveText.text = "Seeds recovered! Defeat " + EnemiesRemaining + " remaining enemies";
            else objectiveText.text = "Return to the camp altar  [E]";
        }
        if (seedSlots != null) for (int i = 0; i < seedSlots.Length; i++)
            if (seedSlots[i] != null) seedSlots[i].color = i < seeds ? collectedSeedColor : emptySeedColor;
    }
    public bool RestoreSeeds()
    {
        RefreshHUD();
        if (HasEnded || IsDying || IsPaused || !CanRestore) return false;
        WinGame();
        return true;
    }
    public void WinGame()
    {
        if (HasEnded) return;
        EndGame(true, "All seeds restored. Carrot Hollow is blooming again!");
    }
    public void GameOver() => GameOver("Your adventure isn't over. Give it another try!");
    public void GameOver(string reason)
    {
        if (HasEnded) return;
        EndGame(false, reason);
    }
    void EndGame(bool won, string reason)
    {
        HasEnded = true; IsDying = false; IsPaused = false;
        Time.timeScale = 0;
        Set(pausePanel, false); Set(objectivePanel, false); Set(settingsPanel, false);
        Set(winPanel, won); Set(losePanel, !won); Set(pauseButton, false);
        var panel = won ? winPanel : losePanel;
        var label = panel != null ? panel.transform.Find("OutcomeMessage")?.GetComponent<TMP_Text>() : outcomeText;
        if (label != null) label.text = reason;
        GameAudio.PlayUI(won ? winSound : loseSound);
        LockCursor(false);
    }
    public void BeginDeath()
    {
        IsDying = true;
        Set(pauseButton, false);
        Set(objectivePanel, false);
    }
    public void PauseGame()
    {
        if (HasEnded || IsDying || IsAwaitingStart) return;
        IsPaused = true; Time.timeScale = 0;
        Set(pausePanel, true); Set(pauseButton, false); Set(objectivePanel, false); Set(settingsPanel, false);
        LockCursor(false);
    }
    public void ResumeGame()
    {
        if (HasEnded || IsDying || IsAwaitingStart) return;
        IsPaused = false; Time.timeScale = 1;
        Set(pausePanel, false); Set(objectivePanel, false); Set(settingsPanel, false); Set(pauseButton, true);
        LockCursor(true);
    }
    public void ToggleObjectives()
    {
        if (HasEnded || IsDying || IsAwaitingStart) return;
        if (objectivePanel != null && objectivePanel.activeSelf) { ResumeGame(); return; }
        SetObjectiveActionLabel("Return to game");
        PauseGame(); Set(pausePanel, false); Set(objectivePanel, true);
    }
    public void ContinueFromObjectives()
    {
        if (HasEnded || IsDying) return;
        IsAwaitingStart = false;
        SetObjectiveActionLabel("Return to game");
        ResumeGame();
    }
    void SetObjectiveActionLabel(string label)
    {
        if (objectiveActionText != null) objectiveActionText.text = label;
    }
    public void OpenSettings() { Set(pausePanel, false); Set(settingsPanel, true); }
    public void CloseSettings() { Set(settingsPanel, false); Set(pausePanel, true); }
    public void RestartGame()
    {
        IsPaused = false; Time.timeScale = 1; LockCursor(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void LoadMainMenu()
    {
        IsPaused = false; Time.timeScale = 1; LockCursor(false);
        SceneManager.LoadScene(mainMenuSceneName);
    }
    public void ExitGame()
    {
        Time.timeScale = 1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    void OnApplicationFocus(bool focused)
    {
        if (focused && !HasEnded && !IsPaused && !IsDying) LockCursor(true);
    }
    void OnDestroy()
    {
        if (instance != this) return;
        instance = null; IsPaused = false; Time.timeScale = 1;
    }
    static void Set(GameObject target, bool active) { if (target != null) target.SetActive(active); }
    public static void LockCursor(bool locked)
    {
        WantsCursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
