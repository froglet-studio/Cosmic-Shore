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
            audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);
            IconView.SelectIcon(this, ProfileIcon);
        }

        public void SetSelected(bool selected)
        {
            BorderImage.enabled = selected;
        }
    }
}
