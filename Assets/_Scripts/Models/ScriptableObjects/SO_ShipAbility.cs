using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Ability", menuName = "CosmicShore/Ship/ShipAbility", order = 4)]
public class SO_ShipAbility : ScriptableObject
{
    [SerializeField] public ShipActions Action;
    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite SelectedIcon;
    [SerializeField] public VideoPlayer PreviewClip;
    /// <summary>
    /// A backlink to the ship this Ability is attached to. This is not necessarily a 1 to 1 mapping.
    /// Since it in not 1:1, we wont serialize this and will assign to this field at runtime when needed.
    /// If we later decide to enforce unique Ability to Class mappings, then we should refactor to serialize this.
    /// </summary>
    [HideInInspector] public SO_Ship Ship;
}