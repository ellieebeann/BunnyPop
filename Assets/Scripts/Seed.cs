using UnityEngine;

public class Seed : MonoBehaviour
{
    public int seedCount = 1;
    public AudioClip seedSound;
    public int carrotCharges;
    private bool collected;
    public bool CanCollect => !collected && isActiveAndEnabled;

    public bool Collect(BunnyHealth player)
    {
        if (!CanCollect || player == null || player.IsDead || Time.timeScale == 0f || GameManager.instance == null) return false;
        if (carrotCharges > 0)
        {
            var attack = player.GetComponent<BunnyAttack>();
            if (attack == null || !attack.AddCarrotCharges(carrotCharges)) return false;
        }
        collected = true;
        if (carrotCharges <= 0) GameManager.instance.AddSeeds(seedCount);
        if (seedSound != null) AudioSource.PlayClipAtPoint(seedSound, transform.position);
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }
}
