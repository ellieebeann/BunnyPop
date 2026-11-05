using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    public string gameSceneName = "SampleScene";

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip backgroundMusic;
    public AudioClip buttonClickSound;

    [Header("Loading Video (Optional)")]
    public GameObject loadingScreenUI;
    public VideoPlayer loadingVideoPlayer;

    [Header("Leave Confirmation Panel")]
    public GameObject leaveConfirmPanel;
    public GameObject settingsPanel;
    public GameObject howToPlayPanel;

    private bool isStartingGame = false;
    public void OpenSettings() { PlayButtonClickSound(); if (settingsPanel != null) settingsPanel.SetActive(true); }
    public void CloseSettings() { if (settingsPanel != null) settingsPanel.SetActive(false); }
    public void OpenInstructions() { PlayButtonClickSound(); if (howToPlayPanel != null) howToPlayPanel.SetActive(true); }
    public void CloseInstructions() { if (howToPlayPanel != null) howToPlayPanel.SetActive(false); }

    void Start()
    {
        // Ensure time is running normally
        Time.timeScale = 1f;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);

        // Ensure panels start hidden
        if (loadingScreenUI != null)
        {
            loadingScreenUI.SetActive(false);
        }

        if (leaveConfirmPanel != null)
        {
            leaveConfirmPanel.SetActive(false);
        }

        // Setup and play background music
        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Unlock cursor for menu interaction
        GameManager.LockCursor(false);
    }

    public void PlayGame()
    {
        if (isStartingGame) return;
        isStartingGame = true;

        PlayButtonClickSound();

        if (loadingScreenUI != null && loadingVideoPlayer != null)
        {
            loadingScreenUI.SetActive(true);
            loadingVideoPlayer.loopPointReached += OnLoadingVideoEnd;
            loadingVideoPlayer.errorReceived += OnVideoError;
            loadingVideoPlayer.isLooping = false;
            loadingVideoPlayer.Play();
            StartCoroutine(LoadingFallback());
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    IEnumerator LoadingFallback()
    {
        yield return new WaitForSecondsRealtime(8f);
        if (isStartingGame) SceneManager.LoadScene(gameSceneName);
    }
    void OnVideoError(VideoPlayer player, string message) { if (isStartingGame) SceneManager.LoadScene(gameSceneName); }
    void OnDestroy()
    {
        if (loadingVideoPlayer == null) return;
        loadingVideoPlayer.loopPointReached -= OnLoadingVideoEnd;
        loadingVideoPlayer.errorReceived -= OnVideoError;
    }

    private void OnLoadingVideoEnd(VideoPlayer vp)
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // Opens the "Are you sure you want to hop away?" confirmation panel
    public void OpenLeaveConfirm()
    {
        PlayButtonClickSound();
        if (leaveConfirmPanel != null)
        {
            leaveConfirmPanel.SetActive(true);
        }
    }

    // Closes the confirmation panel when clicking "No"
    public void CloseLeaveConfirm()
    {
        PlayButtonClickSound();
        if (leaveConfirmPanel != null)
        {
            leaveConfirmPanel.SetActive(false);
        }
    }

    // Actually exits the game when clicking "Yes"
    public void ConfirmLeaveGame()
    {
        PlayButtonClickSound();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayButtonClickSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }
}
