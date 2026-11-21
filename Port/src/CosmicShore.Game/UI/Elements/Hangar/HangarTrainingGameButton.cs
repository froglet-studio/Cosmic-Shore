// Ported from Assets/_Scripts/UI/Elements/Hangar/HangarTrainingGameButton.cs (Hangar unit
// 2026-07-10) — verbatim; UnityEngine → CosmicShore.Engine, UnityEngine.UI →
// CosmicShore.Engine.UI.
using CosmicShore.UI;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    public class HangarTrainingGameButton : MonoBehaviour
    {
        [SerializeField] HangarTrainingModal HangarTrainingModal;
        [SerializeField] Image ElementOneImage;
        [SerializeField] Image ElementTwoImage;
        [SerializeField] Image BorderImage;
        [SerializeField] Sprite SelectedBorderSprite;
        [SerializeField] Sprite DeselectedBorderSprite;

        public SO_TrainingGame TrainingGame { get; private set; }
        public void AssignGame(SO_TrainingGame game)
        {
            TrainingGame = game;
            Deactivate();
        }

        public void Select()
        {
            HangarTrainingModal.SelectGame(TrainingGame);
            Activate();
        }

        public void SetActive(bool active)
        {
            if (active)
                Activate();
            else
                Deactivate();
        }

        void Activate()
        {
            ElementOneImage.sprite = TrainingGame.ElementOne.GetFullIcon(true);
            ElementTwoImage.sprite = TrainingGame.ElementTwo.GetFullIcon(true);
            BorderImage.sprite = SelectedBorderSprite;
        }

        void Deactivate() {

            ElementOneImage.sprite = TrainingGame.ElementOne.GetFullIcon(false);
            ElementTwoImage.sprite = TrainingGame.ElementTwo.GetFullIcon(false);
            BorderImage.sprite = DeselectedBorderSprite;
        }
    }
}
