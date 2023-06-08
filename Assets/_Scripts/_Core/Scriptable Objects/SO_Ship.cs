using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "New Ship", menuName = "TailGlider/Ship", order = 1)]
[System.Serializable]
public class SO_Ship : ScriptableObject
{
    [SerializeField] public ShipTypes Class;
    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite SelectedIcon;
    [SerializeField] public Sprite TrailPreviewImage;
    [SerializeField] public Sprite PreviewImage;
    [SerializeField] public SO_Pilot ChargePilot;
    [SerializeField] public SO_Pilot MassPilot;
    [SerializeField] public SO_Pilot SpaceTimePilot;
    [SerializeField] public List<SO_ShipAbility> Abilities;
    [SerializeField] public List<SO_MiniGame> MiniGames;
}