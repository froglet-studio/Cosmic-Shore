using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Ability", menuName = "CosmicShore/ShipAbility", order = 4)]
public class SO_ShipAbility : ScriptableObject
{
    [SerializeField] public ShipActions Action;
    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite SelectedIcon;
    [SerializeField] public VideoPlayer PreviewClip;
}