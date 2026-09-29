using UnityEngine;
namespace CosmicShore.Core
{
    public sealed class DialogueViewResolver : MonoBehaviour, IDialogueViewResolver
    {
        [SerializeField] private MainMenuDialogueView mainMenuView;
        [SerializeField] private InGameRadioDialogueView inGameRadioView;
        [SerializeField] private RewardDialogueView rewardView;

        [Header("Optional overrides (any IDialogueView MonoBehaviour, e.g. QuestDialoguePanelView / QuestRewardRevealView)")]
        [Tooltip("Takes precedence over the MainMenu view when set.")]
        [SerializeField] private MonoBehaviour mainMenuOverride;
        [Tooltip("Takes precedence over the InGameRadio view when set.")]
        [SerializeField] private MonoBehaviour inGameRadioOverride;
        [Tooltip("Takes precedence over the Reward view when set.")]
        [SerializeField] private MonoBehaviour rewardOverride;

        public IDialogueView ResolveView(DialogueSet set)
        {
            return set.channel switch
            {
                DialogueChannel.MainMenu => Prefer(mainMenuOverride, mainMenuView),
                DialogueChannel.InGameRadio => Prefer(inGameRadioOverride, inGameRadioView),
                DialogueChannel.Reward => Prefer(rewardOverride, rewardView),
                _ => Prefer(mainMenuOverride, mainMenuView)
            };
        }

        static IDialogueView Prefer(MonoBehaviour overrideView, IDialogueView fallback)
            => overrideView is IDialogueView view ? view : fallback;

        void OnValidate()
        {
            if (mainMenuOverride != null && mainMenuOverride is not IDialogueView)
                Debug.LogWarning($"[Dialogue] mainMenuOverride '{mainMenuOverride.name}' does not implement IDialogueView — it will be ignored.", this);
            if (inGameRadioOverride != null && inGameRadioOverride is not IDialogueView)
                Debug.LogWarning($"[Dialogue] inGameRadioOverride '{inGameRadioOverride.name}' does not implement IDialogueView — it will be ignored.", this);
            if (rewardOverride != null && rewardOverride is not IDialogueView)
                Debug.LogWarning($"[Dialogue] rewardOverride '{rewardOverride.name}' does not implement IDialogueView — it will be ignored.", this);
        }
    }
}