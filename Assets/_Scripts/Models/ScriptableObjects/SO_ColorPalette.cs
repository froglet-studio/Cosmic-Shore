using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Color Palette", menuName = "CosmicShore/Color Palette", order = 30)]
[System.Serializable]
public class SO_Color_Palette : ScriptableObject
{
    [SerializeField] public string PaletteUUID;
    [SerializeField] public Color Team_One_Color_1;
    [SerializeField] public Color Team_One_Color_2;
    [SerializeField] public Color Team_Two_Color_1;
    [SerializeField] public Color Team_Two_Color_2;
    [SerializeField] public Color UI_Color_1;
    [SerializeField] public Color Trail_Color_1;
}
