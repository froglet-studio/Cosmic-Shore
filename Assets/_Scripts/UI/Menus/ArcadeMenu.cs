using StarWriter.Core.HangerBuilder;
using StarWriter.Core.LoadoutFavoriting;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
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
        loadoutMenu.gameObject.SetActive(loadout);
        exploreMenu.gameObject.SetActive(!loadout);
    }
}