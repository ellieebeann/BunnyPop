using UnityEngine;

public sealed class WandChest : MonoBehaviour, IWorldInteraction
{
    public Transform lid;
    public AudioClip openSound;
    public bool IsOpened { get; private set; }
    public bool CanInteract => !IsOpened;
    public string Prompt => "[E] Open chest - Carrot Wand";
    private Quaternion openedRotation;
    public bool Interact(BunnyHealth player)
    {
        if (IsOpened || player == null || player.IsDead || Time.timeScale == 0) return false;
        IsOpened = true;
        player.GetComponent<BunnyAttack>()?.UnlockWand();
        if (lid != null) openedRotation = lid.localRotation * Quaternion.Euler(-105, 0, 0);
        GameAudio.PlayWorld(openSound, transform.position);
        GameManager.instance?.RefreshHUD();
        return true;
    }
    void Update()
    {
        if (IsOpened && lid != null) lid.localRotation = Quaternion.RotateTowards(lid.localRotation, openedRotation, Time.deltaTime * 130);
    }
}
