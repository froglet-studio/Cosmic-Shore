using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.UserActions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Game", menuName = "CosmicShore/Game/ArcadeGame", order = 0)]
[System.Serializable]
public class SO_ArcadeGame : ScriptableObject
{
    public GameModes Mode;
    public string DisplayName;
    public string Description;
    public Sprite Icon;
    public Sprite SelectedIcon;
    public Sprite CardBackground;
    public VideoPlayer PreviewClip;
    public List<SO_Captain> Captains;
    public bool GolfScoring;
    [Min(1)] public int MinPlayers = 1;
    [Range(1, 3)] public int MaxPlayers = 2;
    [Min(1)] public int MinIntensity = 1;
    [Range(1, 4)] public int MaxIntensity = 4;
    public string SceneName;
    public CallToActionTargetType CallToActionTargetType;
    public UserActionType ViewUserAction;
    public UserActionType PlayUserAction;
}