using CosmicShore.App.UI.Menus;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class HangarMenuViewSelect : MonoBehaviour
    {
        [SerializeField] GameObject exploreMenu;    // TODO: P1 Retype from GameObject to ExploreMenu
        [SerializeField] SquadView squadMenu;
        [SerializeField] Toggle SquadButton;
        [SerializeField] Toggle ExploreButton;

        void Start()
        {
            SquadButton.Select();
            ToggleView(false);
        }

        public void ToggleView(bool loadout)
        {
            squadMenu.gameObject.SetActive(loadout);
            exploreMenu.SetActive(!loadout);
        }
    }
}