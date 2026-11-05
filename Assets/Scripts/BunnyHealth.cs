using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BunnyHealth : MonoBehaviour
{
    public GameObject LosePanel;
    public int maxHealth = 100, currentHealth;
    public Slider healthBar;
    public AudioClip gameOverSound;
    public float damageGracePeriod = 0.6f, deathPresentationDelay = 1.5f;
    public bool IsDead { get; private set; }
    private float nextDamage;
    void Awake() { currentHealth = maxHealth; }
    void Start()
    {
        if (LosePanel != null) LosePanel.SetActive(false);
        if (healthBar != null) { healthBar.maxValue = maxHealth; healthBar.value = currentHealth; }
    }
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0 || Time.timeScale == 0f || Time.time < nextDamage) return;
        nextDamage = Time.time + damageGracePeriod;
        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);
        if (healthBar != null) healthBar.value = currentHealth;
        if (currentHealth > 0)
        {
            var animator = GetComponent<Animator>();
            if (animator != null) animator.SetTrigger("Hit");
            return;
        }
        IsDead = true;
        GameManager.instance?.BeginDeath();
        GetComponent<BunnyController>()?.TriggerDeath();
        StartCoroutine(GameOver());
    }
    public int GetHealth() { return currentHealth; }
    IEnumerator GameOver()
    {
        // Allow the scaled Animator to finish before freezing the world.
        yield return new WaitForSeconds(deathPresentationDelay);
        if (GameManager.instance != null) { GameManager.instance.GameOver(); yield break; }
        if (LosePanel != null) LosePanel.SetActive(true);
        if (gameOverSound != null && Camera.main != null) AudioSource.PlayClipAtPoint(gameOverSound, Camera.main.transform.position);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }
}
