using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorPaletteManager : MonoBehaviour
{
    [SerializeField]
    SO_Color_Palette defaultColorPalette;

    [SerializeField] List<SO_Color_Palette> AllColorPalettes;
    [SerializeField] List<SO_Color_Palette> Color_Palettes;

    //TODO Vnext Add List<SO_Color_Palette> UnlockedColorPalettes maybe in the store or on a auth server

    private SO_Color_Palette activeColorPalette;

    // Start is called before the first frame update
    void Start()
    {
        //TODO if(no saved favorite.ColorPalette
        activeColorPalette = defaultColorPalette;
    }
    public void SetActiveColorPalette(Loadout favorite)
    {
        //activeColorPalette = favorite.ColorPlaette;
    }
    public void SetActiveColorPalette(SO_Color_Palette colorPalette)
    {
        activeColorPalette = colorPalette;
    }
    public SO_Color_Palette GetActiveColorPalette()
    {
        return activeColorPalette;
    }

    //TODO Unlock palettes from the store
}
