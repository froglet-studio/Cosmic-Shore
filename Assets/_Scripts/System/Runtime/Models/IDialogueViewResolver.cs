namespace CosmicShore.Core
{
    public interface IDialogueViewResolver
    {
        IDialogueView ResolveView(DialogueSet set);
    }
}
