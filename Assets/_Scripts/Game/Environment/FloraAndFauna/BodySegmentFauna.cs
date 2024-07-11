using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Environment.FlowField;

public class BodySegmentFauna : Fauna
{
    public Worm ParentWorm { get; set; }
    public BodySegmentFauna PreviousSegment { get; set; }
    public BodySegmentFauna NextSegment { get; set; }
    public bool IsHead { get; set; }
    public bool IsTail { get; set; }

    [SerializeField] private float scale = 1f;

    private List<HealthBlock> healthBlocks = new List<HealthBlock>();

    protected override void Start()
    {
        base.Start();
        InitializeSegment();
    }

    private void InitializeSegment()
    {
        // Add initial health blocks and spindle
        AddHealthBlock(Instantiate(healthBlock, transform));
        AddSpindle(Instantiate(spindle, transform));

        // Set the scale of the segment
        transform.localScale = Vector3.one * scale;
    }

    public override void AddHealthBlock(HealthBlock block)
    {
        base.AddHealthBlock(block);
        healthBlocks.Add(block);
    }

    public override void RemoveHealthBlock(HealthBlock block)
    {
        base.RemoveHealthBlock(block);
        healthBlocks.Remove(block);

        if (healthBlocks.Count == 0)
        {
            DestroySegment();
        }
    }

    private void DestroySegment()
    {
        if (crystal != null)
        {
            crystal.ActivateCrystal();
        }

        if (!IsHead && !IsTail)
        {
            ParentWorm.SplitWorm(this);
        }
        else if (IsHead || IsTail)
        {
            ParentWorm.RegenerateSegment(this);
        }

        Destroy(gameObject);
    }

    public void SetScale(float newScale)
    {
        scale = newScale;
        transform.localScale = Vector3.one * scale;
    }

    protected override void Spawn()
    {
        // Implementation for spawning a body segment
        // This might be handled by the WormManager instead
    }

    // Additional methods for segment-specific behavior can be added here
}