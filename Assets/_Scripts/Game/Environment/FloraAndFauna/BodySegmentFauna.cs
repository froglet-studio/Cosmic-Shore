using CosmicShore;
using CosmicShore.Game;

/// <summary>
/// Individual segment of a <see cref="Worm"/> creature.
/// Handles segment-specific death logic (splitting, head/tail status).
/// </summary>
public class BodySegmentFauna : Fauna
{
    public Worm ParentWorm { get; set; }
    public BodySegmentFauna PreviousSegment { get; set; }
    public BodySegmentFauna NextSegment { get; set; }
    public bool IsHead;
    public bool IsTail;

    protected override void Die(string killerName = "")
    {
        if (!IsHead && !IsTail)
        {
            ParentWorm.SplitWorm(this);
        }
        else if (IsHead)
        {
            ParentWorm.UpdateHeadStatus(false);
        }
        else if (IsTail)
        {
            ParentWorm.UpdateTailStatus(false);
        }
        ParentWorm.RemoveSegment(this);
    }
}
