using CosmicShore.App.Systems;
using CosmicShore.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class DailyChallengeGameView : View
    {
        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] TMP_Text TimeRemaining;
        [SerializeField] GameObject PreviewWindow;
        [SerializeField] Image IntensityImage;
        [SerializeField] Image ShipClassImage;

        [Header("Reward Progress")]
        [SerializeField] Image ProgressIndicatorBackground;
        [SerializeField] Image ProgressIndicatorVertical;
        [SerializeField] float ProgressIndicatorVerticalMaxHeight = 148;
        [SerializeField] Image ProgressIndicatorBottom;
        [SerializeField] Image ProgressIndicatorMiddle;
        [SerializeField] Image ProgressIndicatorTop;
        [SerializeField] Color ProgressIndicatorColor;
        [SerializeField] TMP_Text RewardScoreLabelOne;
        [SerializeField] TMP_Text RewardScoreLabelTwo;
        [SerializeField] TMP_Text RewardScoreLabelThree;

        [Header("RewardButtons")]
        [SerializeField] GameplayRewardButton RewardButtonOne;
        [SerializeField] GameplayRewardButton RewardButtonTwo;
        [SerializeField] GameplayRewardButton RewardButtonThree;

        [Header("Dynamic Images")]
        [SerializeField] List<Sprite> IntensitySprites;

        public override void UpdateView()
        {
            var game = SelectedModel as SO_TrainingGame;
            GameTitle.text = $"{game.Game.DisplayName}";

            var preview = Instantiate(game.Game.PreviewClip);
            preview.transform.SetParent(PreviewWindow.transform, false);
            preview.GetComponent<RectTransform>().sizeDelta = new Vector2(256, 144);

            IntensityImage.sprite = IntensitySprites[game.DailyChallengeIntensity - 1];
            ShipClassImage.sprite = Hangar.Instance.GetShipSOByShipType(game.ShipClass.Class).Icon;

            RewardButtonOne.SetReward(game.DailyChallengeTierOneReward);
            RewardButtonTwo.SetReward(game.DailyChallengeTierTwoReward);
            RewardButtonThree.SetReward(game.DailyChallengeTierThreeReward);

            if (DailyChallengeSystem.Instance.RewardState.RewardTierOneSatisfied)
            {
                ProgressIndicatorBottom.color = ProgressIndicatorColor;
                
                var indicatorHieght = ProgressIndicatorVerticalMaxHeight / 2f * (DailyChallengeSystem.Instance.RewardState.HighScore / (float)game.DailyChallengeTierTwoReward.ScoreRequirement);
                indicatorHieght = Mathf.Min(indicatorHieght, ProgressIndicatorVerticalMaxHeight / 2f);
                ProgressIndicatorVertical.rectTransform.sizeDelta = new Vector2(ProgressIndicatorVertical.rectTransform.sizeDelta.x, indicatorHieght);

                if (DailyChallengeSystem.Instance.RewardState.RewardTierOneClaimed)
                    RewardButtonOne.MarkClaimed();
                else
                    RewardButtonOne.MakeRewardAvailable();
            }
            if (DailyChallengeSystem.Instance.RewardState.RewardTierTwoSatisfied)
            {
                ProgressIndicatorMiddle.color = ProgressIndicatorColor;

                var indicatorHieght = ProgressIndicatorVerticalMaxHeight * (DailyChallengeSystem.Instance.RewardState.HighScore / (float)game.DailyChallengeTierThreeReward.ScoreRequirement);
                indicatorHieght = Mathf.Min(indicatorHieght, ProgressIndicatorVerticalMaxHeight);

                ProgressIndicatorVertical.rectTransform.sizeDelta = new Vector2(ProgressIndicatorVertical.rectTransform.sizeDelta.x, indicatorHieght);


                if (DailyChallengeSystem.Instance.RewardState.RewardTierTwoClaimed)
                    RewardButtonTwo.MarkClaimed();
                else
                    RewardButtonTwo.MakeRewardAvailable();
            }
            if (DailyChallengeSystem.Instance.RewardState.RewardTierThreeSatisfied)
            {
                ProgressIndicatorTop.color = ProgressIndicatorColor;

                if (DailyChallengeSystem.Instance.RewardState.RewardTierThreeClaimed)
                    RewardButtonThree.MarkClaimed();
                else
                    RewardButtonThree.MakeRewardAvailable();
            }

            Canvas.ForceUpdateCanvases();
        }
    }
}