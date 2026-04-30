namespace CosmicShore.Core
{
    public interface IDialogueService
    {
        void PlayDialogueById(string setId);
        void PlayDialogueSet(DialogueSet set);
        bool IsPlaying { get; }
    }
}
