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

        SO_TrainingGame TrainingGame;
        public void AssignGame(SO_TrainingGame game)
        {
            TrainingGame = game;
            SetInactive();
        }

        public void Select()
        {
            HangarMenu.SelectTrainingGame(TrainingGame.Game);
        }

        public void SetActive()
        {
            ElementOneImage.sprite = TrainingGame.ElementOne.GetFullIcon(true);
            ElementTwoImage.sprite = TrainingGame.ElementTwo.GetFullIcon(true);
            BorderImage.sprite = SelectedBorderSprite;
        }

        public void SetInactive() {

            ElementOneImage.sprite = TrainingGame.ElementOne.GetFullIcon(false);
            ElementTwoImage.sprite = TrainingGame.ElementTwo.GetFullIcon(false);
            BorderImage.sprite = DeselectedBorderSprite;
        }
    }
}