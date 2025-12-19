using CosmicShore.DialogueSystem.Models;

public interface IDialogueViewResolver
{
    IDialogueView ResolveView(DialogueSet set);
}