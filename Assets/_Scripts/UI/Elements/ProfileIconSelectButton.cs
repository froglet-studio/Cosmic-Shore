using CosmicShore.Core;
using CosmicShore.UI;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    public class ProfileIconSelectButton : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;

        [SerializeField] Image BorderImage;
        [SerializeField] Image IconImage;
        [HideInInspector]
        public ProfileIconSelectView IconView;
        ProfileIcon profileIcon;
        public ProfileIcon ProfileIcon {
            get => profileIcon;
            set
            {
                profileIcon = value;
                IconImage.sprite = value.IconSprite;
            }
        }

        public void OnClick()
        {
            // audioSystem is DI-injected; null only if this prefab instance wasn't injected.
            // Guard so a missed injection degrades to "no click sound" instead of an NRE that
            // blocks the avatar selection (the SelectIcon call below).
            if (audioSystem != null)
                audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);

            if (IconView != null)
                IconView.SelectIcon(this, ProfileIcon);
        }

        public void SetSelected(bool selected)
        {
            BorderImage.enabled = selected;
        }
    }
}
