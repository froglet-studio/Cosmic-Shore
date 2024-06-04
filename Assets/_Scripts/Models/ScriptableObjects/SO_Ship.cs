using CosmicShore;
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
    [SerializeField] public List<SO_TrainingGame> TrainingGames2;
}