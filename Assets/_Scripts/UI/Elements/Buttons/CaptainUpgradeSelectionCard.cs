using CosmicShore.UI;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using Reflex.Attributes;
namespace CosmicShore.UI
{
    public class CaptainUpgradeSelectionCard : MonoBehaviour
    {
        [Inject] CaptainManager _captainManager;
        [SerializeField] HangarCaptainsView HangarCaptainsView;
        [SerializeField] int Index;
        [SerializeField] TMP_Text LevelText;
        [SerializeField] Image BorderImage;
        [SerializeField] Image CaptainElementImage;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteSelected;
        [SerializeField] Sprite CaptainSelectButtonBorderSpriteDeselected;

        Captain captain;
        bool selected;

        public void AssignCaptain(SO_Captain so_Captain)
        {
            captain = _captainManager.GetCaptainByName(so_Captain.Name);
            LevelText.text = captain.Level.ToString();
        }

        public void RefreshCaptainData()
        {
            captain = _captainManager.GetCaptainByName(captain.Name);
            LevelText.text = captain.Level.ToString();
            CaptainElementImage.sprite = captain.SO_Element.GetIcon(captain.Level, selected);
        }

        public void ToggleSelected(bool selected)
        {
            this.selected = selected;
            CaptainElementImage.sprite = captain.SO_Element.GetIcon(captain.Level, selected);
            BorderImage.sprite = selected ? CaptainSelectButtonBorderSpriteSelected : CaptainSelectButtonBorderSpriteDeselected;
        }

        public void OnClick()
        {
            HangarCaptainsView.SelectCaptain(Index);
        }
    }
}