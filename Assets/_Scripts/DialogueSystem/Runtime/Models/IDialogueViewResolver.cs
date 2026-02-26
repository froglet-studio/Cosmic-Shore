namespace CosmicShore.DialogueSystem.Runtime.Models
{
    public interface IDialogueViewResolver
    {
        IDialogueView ResolveView(DialogueSet set);
    }
}
