using CosmicShore;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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

    [Tooltip("Owned from the very first launch, before any purchase - the starter vessel. " +
             "This is the DURABLE answer to 'did the player earn this?', unlike isLocked, which " +
             "Unlock() rewrites at runtime (and which the editor then persists into the asset). " +
             "UGSDataService seeds every vessel with this flag into HANGAR_DATA on first load, so " +
             "starters show up as owned in cloud data, and a debug unlock reset re-grants them.")]
    [SerializeField] bool ownedFromStart;

    [Tooltip("Currency cost to unlock this vessel. 0 = free once currency system is bypassed.")]
    [SerializeField] public int UnlockCost = 100;

    /// <summary>
    /// Whether this vessel is locked FOR PLAY right now. In builds, resets to the serialized
    /// default on launch. Will be synced with UGS once backend integration is complete.
    ///
    /// Honours the master developer unlock (<see cref="CosmicShore.Core.DeveloperUnlockGate"/>),
    /// which is ON by default until the FTUE is designed - so this reads false for every vessel
    /// unless somebody has deliberately turned the gate off. That is the point: one switch, and
    /// all ~20 readers of this property (hangar cards, lock overlays, arcade rosters, vessel
    /// selection) open together instead of each learning about the gate.
    ///
    /// Code that GRANTS or REVOKES ownership must read <see cref="IsLockedByEntitlement"/>
    /// instead - see that property.
    /// </summary>
    public bool IsLocked => isLocked && !CosmicShore.Core.DeveloperUnlockGate.AllUnlocked;

    /// <summary>
    /// The raw entitlement: whether the player has actually earned or bought this vessel,
    /// ignoring the master developer unlock.
    ///
    /// This exists because gating a READ also reaches the WRITE path's guards. With the gate on,
    /// <see cref="IsLocked"/> is false for every vessel, so VesselUnlockSystem's
    /// `if (!vessel.IsLocked) return false` would refuse every unlock and persist none of them -
    /// the developer convenience would silently corrupt the thing it was meant to bypass.
    /// Anything that grants, revokes or persists ownership reads THIS; anything that asks
    /// "may the player use this right now" reads <see cref="IsLocked"/>.
    /// </summary>
    public bool IsLockedByEntitlement => isLocked;

    /// <summary>
    /// Whether the player owns this vessel from first launch with nothing spent. Read this, not
    /// <see cref="IsLocked"/>, when you need to know what was AUTHORED - <see cref="Unlock"/>
    /// mutates isLocked at runtime and the editor writes that mutation back into the asset.
    /// </summary>
    public bool OwnedFromStart => ownedFromStart;

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
