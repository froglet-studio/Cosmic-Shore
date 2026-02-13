using CosmicShore;
using CosmicShore.Game;

public class BodySegmentFauna : Fauna
{
    public Worm ParentWorm { get; set; }
    public BodySegmentFauna PreviousSegment { get; set; }
    public BodySegmentFauna NextSegment { get; set; }
    public bool IsHead;
    public bool IsTail;

    protected override void Die(string killername = "")
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

    public override void Initialize(Cell cell)
    {
        throw new System.NotImplementedException();
    }

    protected override void Spawn()
    {

    }

}