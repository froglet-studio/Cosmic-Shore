using CosmicShore.App.Systems;
using CosmicShore.App.UI.FX;
using System.Collections;
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

        [SerializeField] protected float CardFlipAnimDuration = .5f;
        [SerializeField] IconEmitter IconEmitter;

        public void SetTier(int tier) { RewardTier = tier; }
        public void SetReward(DailyChallengeReward reward)
        { 
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
            if (DailyChallengeSystem.Instance.ClaimReward(RewardTier))
                StartCoroutine(PurchaseVisualEffectCoroutine());
            
        }

        public void MarkClaimed()
        {
            Debug.Log($"DailyChallengeRewardButton - MarkClaimed:{RewardTier}");
            NotEarnedButton.gameObject.SetActive(false);
            ClaimButton.gameObject.SetActive(false);
            CollectedButton.gameObject.SetActive(true);
        }

        protected IEnumerator PurchaseVisualEffectCoroutine()
        {
            IconEmitter.EmitIcons();

            // Wait for emitting to complete
            yield return new WaitForSecondsRealtime(1.5f);

            float halfDuration = CardFlipAnimDuration / 2f;
            float elapsedTime = 0f;

            // Rotate to 90 degrees Y
            while (elapsedTime < halfDuration)
            {
                float t = elapsedTime / halfDuration;
                float easedT = EaseInOutQuad(t);
                float angle = Mathf.Lerp(0, 90, easedT);
                ClaimButton.transform.localRotation = Quaternion.Euler(angle, 0, 0);
                CollectedButton.transform.localRotation = Quaternion.Euler(angle, 0, 0);

                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure the final rotation is exactly 90 degrees
            ClaimButton.transform.localRotation = Quaternion.Euler(90, 0, 0);
            CollectedButton.transform.localRotation = Quaternion.Euler(90, 0, 0);

            // Hide the claim button and show the collected button
            ClaimButton.gameObject.SetActive(false);
            CollectedButton.gameObject.SetActive(true);

            // Reset the timer
            elapsedTime = 0f;

            // Rotate back to 0 degrees Y
            while (elapsedTime < halfDuration)
            {
                float t = elapsedTime / halfDuration;
                float easedT = EaseInOutQuad(t);
                float angle = Mathf.Lerp(90, 0, easedT);
                ClaimButton.transform.localRotation = Quaternion.Euler(angle, 0, 0);
                CollectedButton.transform.localRotation = Quaternion.Euler(angle, 0, 0);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure the final rotation is exactly 0 degrees
            ClaimButton.transform.localRotation = Quaternion.Euler(0, 0, 0);
            CollectedButton.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }

        protected float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
    }
}