using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New MiniGame", menuName = "TailGlider/MiniGame", order = 0)]
[System.Serializable]
public class SO_MiniGame : ScriptableObject
{
    public MiniGames Mode;
    public string Name;
    public string Description;
    public Sprite Icon;
    public Sprite SelectedIcon;
    public VideoPlayer PreviewClip;
    [Min(1)] public int MinPlayers = 1;
    [Range(1, 4)] public int MaxPlayers = 2;
    public string SceneName;
}