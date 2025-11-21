using CosmicShore.DialogueSystem.Models;
using UnityEngine;

namespace CosmicShore.DialogueSystem.View
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