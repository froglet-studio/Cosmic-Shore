using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorPaletteManager : MonoBehaviour
{
    public Dictionary<string, Material> materials;

    [SerializeField] List<Material> materialsList;

    //[SerializeField] List<SO_Color_Palette> AllColorPalettes;
    [SerializeField] List<SO_Color_Palette> AvailableColorPalettes;

    [SerializeField] SO_Color_Palette defaultColorPalette;

    private SO_Color_Palette activeColorPalette;

    // Start is called before the first frame update
    void Start()
    {
        //TODO if(no saved favorite.ColorPalette
        activeColorPalette = defaultColorPalette;
    }
    public void SetActiveColorPalette(SO_Color_Palette sO_Color_Palette)
    {
        activeColorPalette = sO_Color_Palette;
    }

    public SO_Color_Palette GetActiveColorPalette()
    {
        return activeColorPalette;
    }

    private void SetAllMaterialColors()
    {
        Material material = GetComponent<Material>();
        //material.SetColor(AvailableColorPalettes[i].)
    }

    public Material GetTeamMaterial(float teamNum)
    {
        string key = "Team" + teamNum.ToString();
        Material newMaterial = materials[key];

        return newMaterial;
    }
}
