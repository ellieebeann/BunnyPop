using UnityEngine;

public class SlimeAI : EnemyAI
{
    public float minEmoteInterval = 2f, maxEmoteInterval = 5f;
    private float nextEmote;
    protected override void OnIdle()
    {
        if (animator == null || Time.time < nextEmote) return;
        animator.SetTrigger(Random.value < 0.5f ? "Dance" : "Jump");
        nextEmote = Time.time + Random.Range(minEmoteInterval, Mathf.Max(minEmoteInterval, maxEmoteInterval));
    }
}
