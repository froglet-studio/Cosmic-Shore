using UnityEngine;


[CreateAssetMenu(fileName = "New Character", menuName = "Create SO/Narration/Character")]
public class NarrationCharacter : ScriptableObject
{
    [SerializeField]
    private string characterName;

    public string CharacterName => characterName;
}
