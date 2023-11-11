using CosmicShore.Core.LoadoutFavoriting;
using UnityEngine;
using UnityEngine.UI;

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
            UserActionMonitor.Instance.CompleteAction(UserActionType.ViewArcadeLoadoutMenu);
        else
            UserActionMonitor.Instance.CompleteAction(UserActionType.ViewArcadeExploreMenu);

        loadoutMenu.gameObject.SetActive(loadout);
        exploreMenu.gameObject.SetActive(!loadout);
    }
}