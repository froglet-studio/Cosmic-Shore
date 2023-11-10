using UnityEngine;
using UnityEngine.UI;

public class HangarMenuViewSelect : MonoBehaviour
{
    [SerializeField] GameObject exploreMenu;
    [SerializeField] SquadMenu squadMenu;
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