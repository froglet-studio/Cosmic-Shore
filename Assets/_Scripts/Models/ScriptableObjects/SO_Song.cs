using UnityEngine;

[CreateAssetMenu(fileName = "New Song", menuName = "CosmicShore/Song", order = 20)]
public class SO_Song : ScriptableObject
{
    [SerializeField] AudioClip clip;
    [SerializeField] string decription;
    [SerializeField] string author;

    public string Decription { get => decription; set => decription = value; }
    public string Author { get => author; set => author = value; }
    public AudioClip Clip { get => clip; set => clip = value; }
}