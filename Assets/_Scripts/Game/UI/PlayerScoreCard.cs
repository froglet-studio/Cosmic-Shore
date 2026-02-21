using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class PlayerScoreCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text playerScoreText;
        [SerializeField] private Image domainIndicatorImage;
        [SerializeField] private Image playerAvatarImage;

        public void Setup(string playerName, int initialCrystals, Color domainColor, bool isLocalPlayer)
        {
            playerNameText.text = playerName;
            UpdateScore(initialCrystals);
            if (!domainIndicatorImage) return;
            domainIndicatorImage.gameObject.SetActive(true);
            domainIndicatorImage.color = domainColor;
        }

        public void SetAvatar(Sprite avatarSprite)
        {
            if (!playerAvatarImage) return;
            if (avatarSprite != null)
            {
                playerAvatarImage.sprite = avatarSprite;
                playerAvatarImage.enabled = true;
            }
            else
            {
                playerAvatarImage.enabled = false;
            }
        }

        public void UpdateScore(int crystalCount)
        {
            playerScoreText.text = $"{crystalCount}";
        }
    }
}