using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Seed Collectibles")]
    public int seeds = 0;
    public int allSeeds = 10;
    public TMP_Text seedsText;

    [Header("UI Panels & Buttons")]
    public GameObject winPanel;
    public GameObject losePanel;
    public GameObject pausePanel;
    public GameObject pauseButton;

    [Header("Audio")]
    public AudioClip winSound;

    [Header("Scene Settings")]
    public string mainMenuSceneName = "MainMenu";

    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Unfreeze time on start
        Time.timeScale = 1f;

        // Ensure panels start hidden
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);

        // Lock cursor for gameplay
        LockCursor(true);

        UpdateSeedsUI();
    }

    void Update()
    {
        // Toggle pause menu using Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Wag i-toggle kung tapos na ang laro (Win/Lose)
            if (winPanel != null && winPanel.activeSelf) return;
            if (losePanel != null && losePanel.activeSelf) return;

            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void AddSeeds(int amount)
    {
        seeds += amount;
        UpdateSeedsUI();
    }

    private void UpdateSeedsUI()
    {
        if (seedsText != null)
        {
            seedsText.text = "Seeds Collected: " + seeds.ToString();
        }

        if (seeds >= allSeeds)
        {
            WinGame();
        }
    }

    public void WinGame()
    {
        Time.timeScale = 0f;
        if (winPanel != null) winPanel.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);

        if (winSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);
        }

        LockCursor(false);
    }

    public void GameOver()
    {
        Time.timeScale = 0f;
        if (losePanel != null) losePanel.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);

        LockCursor(false);
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null) pausePanel.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);

        LockCursor(false);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);

        LockCursor(true);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    private void LockCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}