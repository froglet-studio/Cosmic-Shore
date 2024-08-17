using CosmicShore.App.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI
{
    public class DailyChallengeRewardButton : MonoBehaviour
    {
        [Range(1,3)]
        [SerializeField] int RewardTier;
        [SerializeField] TMP_Text ScoreRequirementLabel;
        [SerializeField] TMP_Text ClaimButtonValueLabel;
        [SerializeField] TMP_Text NotEarnedButtonValueLabel;
        [SerializeField] Button ClaimButton;
        [SerializeField] Button NotEarnedButton;
        [SerializeField] Button CollectedButton;

        DailyChallengeReward Reward;

        public void SetTier(int tier) { RewardTier = tier; }
        public void SetReward(DailyChallengeReward reward)
        { 
            Reward = reward;
            ClaimButtonValueLabel.text = reward.Value.ToString();
            NotEarnedButtonValueLabel.text = reward.Value.ToString();
            ScoreRequirementLabel.text = $"Score {reward.ScoreRequirement}";
        }

        public void MakeRewardAvailable()
        {
            Debug.Log("DailyChallengeRewardButton - SetRewardAvailable");
            CollectedButton.gameObject.SetActive(false);
            NotEarnedButton.gameObject.SetActive(false);
            ClaimButton.gameObject.SetActive(true);
        }

        public void ClaimReward()
        {
            Debug.Log($"DailyChallengeRewardButton - ClaimReward:{RewardTier}");
            if(DailyChallengeSystem.Instance.ClaimReward(RewardTier))
                MarkClaimed();
        }

        public void MarkClaimed()
        {
            Debug.Log($"DailyChallengeRewardButton - MarkClaimed:{RewardTier}");
            NotEarnedButton.gameObject.SetActive(false);
            ClaimButton.gameObject.SetActive(false);
            CollectedButton.gameObject.SetActive(true);
        }
    }
}