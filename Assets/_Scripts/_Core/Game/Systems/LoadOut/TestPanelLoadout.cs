using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using StarWriter.Core.Favoriting;
using PlayFab.ClientModels;

public class TestPanelLoadout : MonoBehaviour
{
    public TMP_Text game_Mode_Text;
    public TMP_Text ship_Type_Text;
    public TMP_Text intensity_Text;
    public TMP_Text player_Count_Text;
    public TMP_Text active_Index_Text;

    public LoadoutSystem loadoutSystem;

    private void LateUpdate()
    { 
        int idx = loadoutSystem.GetActiveLoadoutsIndex();

        active_Index_Text.text = idx.ToString();

        Loadout loadout = loadoutSystem.GetActiveLoadout();

        game_Mode_Text.text = loadout.GameMode.ToString();
        ship_Type_Text.text= loadout.ShipType.ToString();
        intensity_Text.text= loadout.Intensity.ToString();
        player_Count_Text.text = loadout.PlayerCount.ToString();

    }
}
