using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lightweight component for a single player row inside a TeamScorecard.
    /// Displays avatar icon, player name, and individual score.
    /// </summary>
    public class PlayerScoreEntry : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text scoreText;

        public void Populate(string playerName, string score, Sprite avatar = null)
        {
            if (playerNameText) playerNameText.text = playerName;
            if (scoreText) scoreText.text = score;

            if (avatarImage)
            {
                if (avatar != null)
                {
                    avatarImage.sprite = avatar;
                    avatarImage.enabled = true;
                }
                else
                {
                    avatarImage.enabled = false;
                }
            }
        }

        public void Show(bool visible) => gameObject.SetActive(visible);
    }
}
