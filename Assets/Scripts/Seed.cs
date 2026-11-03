using UnityEngine;

public class Seed : MonoBehaviour
{
    public int seedCount = 1;
    public AudioClip seedSound;
    private bool collected;
    public bool CanCollect => !collected && isActiveAndEnabled;

    public bool Collect(BunnyHealth player)
    {
        if (!CanCollect || player == null || player.IsDead || Time.timeScale == 0f || GameManager.instance == null) return false;
        collected = true;
        GameManager.instance.AddSeeds(seedCount);
        if (seedSound != null) AudioSource.PlayClipAtPoint(seedSound, transform.position);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
