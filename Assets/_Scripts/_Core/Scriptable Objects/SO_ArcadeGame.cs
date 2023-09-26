using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New ArcadeGame", menuName = "CosmicShore/ArcadeGame", order = 0)]
[System.Serializable]
public class SO_ArcadeGame : ScriptableObject
{
    public MiniGames Mode;
    public string Name;
    public string Description;
    public Sprite Icon;
    public Sprite SelectedIcon;
    public Sprite CardBackground;
    public VideoPlayer PreviewClip;
    public List<SO_Pilot> Pilots;
    [Min(1)] public int MinPlayers = 1;
    [Range(1, 4)] public int MaxPlayers = 2;
    public string SceneName;
}