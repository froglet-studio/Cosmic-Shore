using UnityEngine;

[CreateAssetMenu(fileName = "New Narration Line", menuName = "Create SO/Narration/Line")]
public class NarrationLine : ScriptableObject
{
    [SerializeField]
    private NarrationCharacter speaker;
    [SerializeField]
    [TextArea(10, 100)]
    private string text;
  
    public NarrationCharacter Speaker => speaker;
    public string Text => text;
}
