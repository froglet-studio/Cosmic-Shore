using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Ship", menuName = "CosmicShore/Ship", order = 1)]
[System.Serializable]
public class SO_Ship : ScriptableObject
{
    [SerializeField] public ShipTypes Class;

    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite ActiveIcon;
    [SerializeField] public Sprite InactiveIcon;
    [SerializeField] public Sprite ShipIcon;
    [SerializeField] public Sprite PreviewImage;
    [SerializeField] public Sprite TrailPreviewImage;
    [SerializeField] public Sprite CardSilohoutte;
    [SerializeField] public Sprite CardSilohoutteActive;
    [SerializeField] public List<SO_ShipAbility> Abilities;
    [SerializeField] public List<SO_Vessel> Vessels;
    [SerializeField] public List<SO_ArcadeGame> TrainingGames;
}