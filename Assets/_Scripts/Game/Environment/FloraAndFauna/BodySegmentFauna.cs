using UnityEngine;
using System.Collections.Generic;
using CosmicShore;
using CosmicShore.Core;
using CosmicShore.Environment.FlowField;
using System.Collections;

public class BodySegmentFauna : Fauna
{
    public Worm ParentWorm { get; set; }
    public BodySegmentFauna PreviousSegment { get; set; }
    public BodySegmentFauna NextSegment { get; set; }
    public bool IsHead { get; set; }
    public bool IsTail { get; set; }

    [SerializeField] private float scale = 1f;

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

    protected override void Die()
    {
        base.Die();
        if (!IsHead && !IsTail)
        {
            ParentWorm.SplitWorm(this);
        }
        else if (IsHead)
        {
            ParentWorm.hasHead = false;
        }
        else if (IsTail)
        {
            ParentWorm.hasTail = false;
        }

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