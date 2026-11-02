using UnityEngine;

public class Seed : MonoBehaviour
{
    public int seedCount = 1;
    public AudioClip seedSound;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.instance.AddSeeds(seedCount);
            AudioSource.PlayClipAtPoint(seedSound, transform.position);
            Destroy(gameObject);
        }
    }
}