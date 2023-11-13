using CosmicShore.App.Systems.UserActions;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Menus
{
    public class ArcadeMenu : MonoBehaviour
    {
        [SerializeField] ExploreMenu exploreMenu;
        [SerializeField] LoadoutMenu loadoutMenu;
        [SerializeField] Toggle LoadoutButton;
        [SerializeField] Toggle ExploreButton;

        void Start()
        {
            LoadoutButton.Select();
        }

        public void ToggleView(bool loadout)
        {
            if (loadout)
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeLoadoutMenu);
            else
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeExploreMenu);

            loadoutMenu.gameObject.SetActive(loadout);
            exploreMenu.gameObject.SetActive(!loadout);
        }
    }
}