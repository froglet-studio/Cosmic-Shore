using CosmicShore.UI.Views;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Game.Managers;
using CosmicShore.Models.ScriptableObjects;
namespace CosmicShore.UI.Elements
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
