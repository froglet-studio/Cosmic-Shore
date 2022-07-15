using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Song", menuName = "Create SO/Song")]
public class SO_Song : ScriptableObject
{
    [SerializeField]
    private AudioClip clip;
    
    [SerializeField]
    private string decription;
    [SerializeField]
    private string author;
    

    public string Decription { get => decription; set => decription = value; }
    public string Author { get => author; set => author = value; }
    public AudioClip Clip { get => clip; set => clip = value; }
}
