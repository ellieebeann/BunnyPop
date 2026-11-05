public interface IWorldInteraction
{
    bool CanInteract { get; }
    string Prompt { get; }
    bool Interact(BunnyHealth player);
}
