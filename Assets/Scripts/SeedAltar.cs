using UnityEngine;

public sealed class SeedAltar : MonoBehaviour, IWorldInteraction
{
    public bool CanInteract => GameManager.instance != null && !GameManager.instance.HasEnded;
    public string Prompt
    {
        get
        {
            var game = GameManager.instance;
            if (game == null) return "Camp altar";
            if (game.seeds < game.allSeeds) return "Camp altar - recover all " + game.allSeeds + " seeds";
            if (game.requireAllEnemies && game.EnemiesRemaining > 0) return "Camp altar - defeat the remaining enemies";
            return "[E] Restore the magic seeds";
        }
    }
    public bool Interact(BunnyHealth player)
    {
        return player != null && !player.IsDead && Time.timeScale > 0 && GameManager.instance != null && GameManager.instance.RestoreSeeds();
    }
}
