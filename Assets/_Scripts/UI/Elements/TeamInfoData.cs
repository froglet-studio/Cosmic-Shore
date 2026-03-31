using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
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
        [Tooltip("The AvatarIcon container GameObject. Enabled only on the selected team.")]
        [SerializeField] private GameObject avatarIconContainer;

        [Tooltip("The child Image inside avatarIconContainer that displays the avatar sprite.")]
        [SerializeField] private Image avatarIconImage;

        public Domains Domain => domain;
        public Button Button => button;

        public void SetSelected(bool selected)
        {
            if (backgroundImage)
                backgroundImage.sprite = selected ? selectedSprite : unselectedSprite;

            if (labelText)
                labelText.color = selected ? selectedTextColor : unselectedTextColor;

            if (avatarIconContainer)
                avatarIconContainer.SetActive(selected);
        }

        public void SetAvatarSprite(Sprite sprite)
        {
            if (!avatarIconImage) return;

            if (sprite != null)
            {
                avatarIconImage.sprite = sprite;
                avatarIconImage.enabled = true;
            }
            else
            {
                avatarIconImage.enabled = false;
            }
        }
    }
}
