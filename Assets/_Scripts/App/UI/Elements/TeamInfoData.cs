using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class TeamInfoData : MonoBehaviour
    {
        [Header("Domain")]
        [SerializeField] private Domains domain = Domains.Unassigned;

        [Header("Button")]
        [SerializeField] private Button button;

        [Header("Background Sprites")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite unselectedSprite;

        [Header("Label")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color unselectedTextColor = Color.gray;

        [Header("Avatar Icon")]
        [SerializeField] private Image avatarIcon;

        public Domains Domain => domain;
        public Button Button => button;

        public void SetSelected(bool selected)
        {
            if (backgroundImage)
                backgroundImage.sprite = selected ? selectedSprite : unselectedSprite;

            if (labelText)
                labelText.color = selected ? selectedTextColor : unselectedTextColor;
        }

        public void SetAvatarSprite(Sprite sprite)
        {
            if (!avatarIcon) return;

            if (sprite != null)
            {
                avatarIcon.sprite = sprite;
                avatarIcon.enabled = true;
            }
            else
            {
                avatarIcon.enabled = false;
            }
        }
    }
}
