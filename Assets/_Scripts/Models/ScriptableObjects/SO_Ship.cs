using CosmicShore;
using CosmicShore.App.Systems.VesselUnlock;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Vessel", menuName = "CosmicShore/Vessel/Vessel", order = 1)]
[System.Serializable]
public class SO_Ship : ScriptableObject
{
    [SerializeField] public VesselClassType Class;
    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [FormerlySerializedAs("SelectedIcon")]
    [SerializeField] public Sprite IconActive;
    [FormerlySerializedAs("Icon")]
    [SerializeField] public Sprite IconInactive;
    [SerializeField] public Sprite PreviewImage;
    [SerializeField] public Sprite SquadImage;
    [SerializeField] public Sprite TrailPreviewImage;
    [SerializeField] public Sprite CardSilohoutteActive;
    [FormerlySerializedAs("CardSilohoutte")]
    [SerializeField] public Sprite CardSilohoutteInactive;
    [SerializeField] public List<SO_ShipAbility> Abilities;
    [FormerlySerializedAs("TrainingGames")]
    [SerializeField] public List<SO_ArcadeGame> Games;
    [SerializeField] public List<SO_TrainingGame> TrainingGames;
    [SerializeField] public GameplayParameter gameplayParameter1 = new GameplayParameter("Casual", "Challenging", .5f);
    [SerializeField] public GameplayParameter gameplayParameter2 = new GameplayParameter("Relaxing", "Thrilling", .5f);
    [SerializeField] public GameplayParameter gameplayParameter3 = new GameplayParameter("Solo", "Social", .5f);

    [Header("Unlock Configuration")]
    [Tooltip("Whether this vessel is unlocked from the start (e.g. Squirrel).")]
    [SerializeField] public bool UnlockedByDefault;

    [Tooltip("Currency cost to unlock this vessel. 0 = free once currency system is bypassed.")]
    [SerializeField] public int UnlockCost = 100;

    /// <summary>
    /// A flag indicating whether the Vessel Class is locked. Vessel Class is locked if it is not unlocked by the player.
    /// </summary>
    public bool IsLocked
    {
        get => !VesselUnlockSystem.IsUnlocked(this);
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
