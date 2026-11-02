using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public int seeds = 0;
    public int allSeeds = 10;
    public GameObject WinPanel;

    public AudioClip winSound;
    public TMP_Text seedsText;

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
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UpdateSeedsUI();
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
            seedsText.text = ("Seeds Collected: ") + seeds.ToString();
        }

        if (seeds >= allSeeds)
        {
            WinGame();
        }
    }

    public void WinGame()
    {
        if (WinPanel != null)
        {
            WinPanel.SetActive(true);
        }

        if (winSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
    }
}