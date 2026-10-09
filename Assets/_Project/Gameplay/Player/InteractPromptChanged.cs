namespace FishGame.Gameplay
{
    // Authority: none (local presentation message on the EventBus). A null or empty Prompt means hide.
    // Prompt is the raw NetworkInteractable.GetPrompt text, without the key glyph (T14 composes the key).
    public readonly struct InteractPromptChanged
    {
        public readonly string Prompt;
        public readonly bool CanInteract;

        public InteractPromptChanged(string prompt, bool canInteract)
        {
            Prompt = prompt;
            CanInteract = canInteract;
        }
    }
}
