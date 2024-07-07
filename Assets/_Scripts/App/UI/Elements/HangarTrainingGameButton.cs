using CosmicShore.App.UI.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class HangarTrainingGameButton : MonoBehaviour
    {
        [SerializeField] HangarMenu HangarMenu;
        [SerializeField] Image ElementOneImage;
        [SerializeField] Image ElementTwoImage;
        [SerializeField] Image BorderImage;
        [SerializeField] Sprite SelectedBorderSprite;
        [SerializeField] Sprite DeselectedBorderSprite;

        SO_TrainingGame Game;
        public void AssignGame(SO_TrainingGame game)
        {
            Game = game;
            SetInactive();
        }

        public void Select()
        {
            HangarMenu.SelectTrainingGame(Game);
        }

        public void SetActive()
        {
            ElementOneImage.sprite = Game.ElementOne.GetFullIcon(true);
            ElementTwoImage.sprite = Game.ElementTwo.GetFullIcon(true);
            BorderImage.sprite = SelectedBorderSprite;
        }

        public void SetInactive() {

            ElementOneImage.sprite = Game.ElementOne.GetFullIcon(false);
            ElementTwoImage.sprite = Game.ElementTwo.GetFullIcon(false);
            BorderImage.sprite = DeselectedBorderSprite;
        }
    }
}
