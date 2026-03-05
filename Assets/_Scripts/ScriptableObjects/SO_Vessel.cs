using CosmicShore;
using CosmicShore.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Vessel", menuName = "CosmicShore/Vessel/Vessel", order = 1)]
[System.Serializable]
public class SO_Vessel : ScriptableObject
{
    [Header("Vessel Identity")]
    [SerializeField] public VesselClassType Class;
    [SerializeField] public string Name;
    [SerializeField] public string Description;

    [Header("Element Configuration")]
    [SerializeField] public Element PrimaryElement;
    [SerializeField] public SO_Element Element;
    [SerializeField] public ResourceCollection InitialResourceLevels;

    [Header("Visuals")]
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

    [Header("Abilities & Games")]
    [FormerlySerializedAs("Abilities")]
    [SerializeField] public List<SO_VesselAbility> Abilities;
    [FormerlySerializedAs("TrainingGames")]
    [SerializeField] public List<SO_ArcadeGame> Games;
    [SerializeField] public List<SO_TrainingGame> TrainingGames;

    [Header("Gameplay Parameters")]
    [SerializeField] public GameplayParameter gameplayParameter1 = new GameplayParameter("Casual", "Challenging", .5f);
    [SerializeField] public GameplayParameter gameplayParameter2 = new GameplayParameter("Relaxing", "Thrilling", .5f);
    [SerializeField] public GameplayParameter gameplayParameter3 = new GameplayParameter("Solo", "Social", .5f);

    [Header("Unlock Configuration")]
    [Tooltip("Whether this vessel is locked. Set to true for vessels that must be purchased.")]
    [SerializeField] bool isLocked;

    [Tooltip("Currency cost to unlock this vessel. 0 = free once currency system is bypassed.")]
    [SerializeField] public int UnlockCost = 100;

    /// <summary>
    /// Whether this vessel is currently locked. In builds, resets to the serialized default on launch.
    /// Will be synced with UGS once backend integration is complete.
    /// </summary>
    public bool IsLocked => isLocked;

    /// <summary>
    /// Unlocks this vessel at runtime. In builds the change is lost on restart (by design until UGS sync).
    /// </summary>
    public void Unlock() => isLocked = false;

    /// <summary>
    /// Locks this vessel at runtime. Intended for debug/testing.
    /// </summary>
    public void Lock() => isLocked = true;
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
