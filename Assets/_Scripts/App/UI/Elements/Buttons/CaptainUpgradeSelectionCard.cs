using CosmicShore.App.UI.Menus;
using CosmicShore.Integrations.Playfab.Economy;
using CosmicShore.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class CaptainUpgradeSelectionCard : MonoBehaviour
    {
        [SerializeField] HangarMenu HangarMenu;
        [SerializeField] int Index;
        [SerializeField] TMP_Text LevelText;
        [SerializeField] Image BorderImage;
        [SerializeField] Image CaptainElementImage;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteSelected;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteDeselected;

        Captain captain;

        public void AssignCaptain(SO_Captain so_Captain)
        {
            captain = CaptainManager.Instance.GetCaptainByName(so_Captain.Name);
            LevelText.text = captain.Level.ToString();
        }

        public void ToggleSelected(bool selected)
        {
            if (selected)
            {
                BorderImage.sprite = CaptainSelectButtonBorderSpriteSelected;
                CaptainElementImage.sprite = captain.SelectedIcon;
            }
            else
            {
                BorderImage.sprite = CaptainSelectButtonBorderSpriteDeselected;
                CaptainElementImage.sprite = captain.Icon;
            }
        }

        public void OnClick()
        {
            HangarMenu.SelectCaptain(Index);
        }
    }
}
