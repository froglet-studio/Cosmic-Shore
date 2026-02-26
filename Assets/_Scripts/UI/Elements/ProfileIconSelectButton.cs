using CosmicShore.Core;
using CosmicShore.UI;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    public class ProfileIconSelectButton : MonoBehaviour
    {
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
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.OptionClick);
            IconView.SelectIcon(this, ProfileIcon);
        }

        public void SetSelected(bool selected)
        {
            BorderImage.enabled = selected;
        }
    }
}
