using CosmicShore.DialogueSystem.Models;
using UnityEngine;
using CosmicShore.DialogueSystem.Runtime;
namespace CosmicShore.DialogueSystem.Runtime
{
    public sealed class DialogueViewResolver : MonoBehaviour, IDialogueViewResolver
    {
        [SerializeField] private MainMenuDialogueView mainMenuView;
        [SerializeField] private InGameRadioDialogueView inGameRadioView;
        [SerializeField] private RewardDialogueView rewardView;

        public IDialogueView ResolveView(DialogueSet set)
        {
            return set.channel switch
            {
                DialogueChannel.MainMenu => mainMenuView,
                DialogueChannel.InGameRadio => inGameRadioView,
                DialogueChannel.Reward => rewardView,
                _ => mainMenuView
            };
        }
    }
}