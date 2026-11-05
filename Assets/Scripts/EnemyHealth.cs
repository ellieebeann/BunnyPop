using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public string enemyName = "Enemy";
    public int maxHealth = 50, currentHealth;
    public AudioClip hitSound, deathSound;
    public float corpseLifetime = 4f;
    [Header("Optional seed loot (assign a prefab to enable)")]
    public Seed seedDropPrefab;
    public Seed wandChargeDropPrefab;
    public Seed bonusSeedDropPrefab;
    [Range(0f, 1f)] public float bonusSeedChance = 0.35f;
    [Range(0f, 1f)] public float seedDropChance = 0.35f;
    public Seed DroppedSeed { get; private set; }
    public bool IsDead { get; private set; }
    private Animator animator;
    void Awake() { currentHealth = maxHealth; animator = GetComponent<Animator>(); }
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        if (currentHealth > 0)
        {
            var enemyAI = GetComponent<EnemyAI>();
            if (enemyAI != null) enemyAI.TakeDamage();
            else if (animator != null) animator.SetTrigger("Hit");
            if (enemyAI != null) enemyAI.PlayActionSound(hitSound);
            else if (hitSound != null) GameAudio.PlayWorld(hitSound, transform.position);
            return;
        }
        IsDead = true;
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.TriggerDeath();
        if (animator != null) { animator.ResetTrigger("Hit"); animator.ResetTrigger("Attack"); animator.SetBool("IsDead", true); }
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        Seed loot = seedDropPrefab;
        if (wandChargeDropPrefab != null && (loot == null || Random.value < 0.5f)) loot = wandChargeDropPrefab;
        if (loot != null && (seedDropChance >= 1f || Random.value < seedDropChance))
        {
            DroppedSeed = Instantiate(loot, transform.position, Quaternion.identity);
            DroppedSeed.gameObject.SetActive(true);
            DroppedSeed.SettleOnGround(transform);
        }
        if (bonusSeedDropPrefab != null && Random.value < bonusSeedChance)
        {
            var bonus = Instantiate(bonusSeedDropPrefab, transform.position + Vector3.right * 4, Quaternion.identity);
            bonus.gameObject.SetActive(true); bonus.SettleOnGround(transform);
        }
        if (deathSound != null) GameAudio.PlayWorld(deathSound, transform.position);
        Destroy(gameObject, corpseLifetime);
        GameManager.instance?.RefreshHUD();
    }
}
