using CosmicShore;
using CosmicShore.Integrations.PlayFab.Economy;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Ship", menuName = "CosmicShore/Ship/Ship", order = 1)]
[System.Serializable]
public class SO_Ship : ScriptableObject
{
    [SerializeField] public ShipTypes Class;

    [SerializeField] public string Name;
    [SerializeField] public string Summary;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite SelectedIcon;
    [SerializeField] public VideoPlayer PreviewClip;
    [SerializeField] public Sprite PreviewImage;
    [SerializeField] public Sprite TrailPreviewImage;
    [SerializeField] public Sprite CardSilohoutte;
    [SerializeField] public Sprite CardSilohoutteActive;
    [SerializeField] public List<SO_ShipAbility> Abilities;
    [SerializeField] public List<SO_Captain> Captains;
    [FormerlySerializedAs("TrainingGames")]
    [SerializeField] public List<SO_ArcadeGame> Games;
    [SerializeField] public List<SO_TrainingGame> TrainingGames;
    [SerializeField] public GameplayParameter gameplayParameter1 = new GameplayParameter("Casual", "Challenging", .5f);
    [SerializeField] public GameplayParameter gameplayParameter2 = new GameplayParameter("Relaxing", "Thrilling", .5f);
    [SerializeField] public GameplayParameter gameplayParameter3 = new GameplayParameter("Solo", "Social", .5f);

    /// <summary>
    /// A flag indicating whether the Ship Class is locked. Ship Class is locked if it is not owned by the player (in the player's inventory).
    /// </summary>
    public bool IsLocked
    {
        get => !CaptainManager.Instance.UnlockedShips.Contains(this);
    }


}

[System.Serializable]
public struct GameplayParameter
{
    public string LeftHandLabel;
    public string RightHandLabel;
    [Range(0,1)]
    public float Value;

    public GameplayParameter(string leftHandLabel, string rightHandLabel, float value)
    {
        LeftHandLabel = leftHandLabel;
        RightHandLabel = rightHandLabel;
        Value = value;
    }
}