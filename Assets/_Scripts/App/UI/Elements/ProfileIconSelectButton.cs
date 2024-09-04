using CosmicShore.App.UI.Views;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
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
            IconView.SelectIcon(this, ProfileIcon);
        }

        public void SetSelected(bool selected)
        {
            BorderImage.enabled = selected;
        }
    }
}
