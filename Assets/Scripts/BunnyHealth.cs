using UnityEngine;
using UnityEngine.UI;

public class BunnyHealth : MonoBehaviour
{
    public GameObject LosePanel;

    public int maxHealth = 100;
    public int currentHealth;

    public Slider healthBar;

    public AudioClip gameOverSound;

    private BunnyController bunnyController;

    void Start()
    {
        Time.timeScale = 1f;

        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        bunnyController = GetComponent<BunnyController>();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        currentHealth = Mathf.Clamp(
            currentHealth,
            0,
            maxHealth
        );

        if (healthBar != null)
        {
            healthBar.value = currentHealth;
        }

        Debug.Log("Health: " + currentHealth);
        Debug.Log("TAKE DAMAGE: " + damage);

        if (currentHealth <= 0)
        {
            Debug.Log("Player Died!");
            GameOver();
        }
    }

    public int GetHealth()
    {
        return currentHealth;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Enemy"))
        {
            TakeDamage(3);
        }
    }

    private void GameOver()
    {
        if (bunnyController != null)
        {
            bunnyController.TriggerDeath();
        }

        if (LosePanel != null)
        {
            LosePanel.SetActive(true);
        }

        if (gameOverSound != null && Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(
                gameOverSound,
                Camera.main.transform.position
            );
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }
}