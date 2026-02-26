namespace CosmicShore.DialogueSystem.Runtime
{
    public interface IDialogueViewResolver
    {
        IDialogueView ResolveView(DialogueSet set);
    }
}
