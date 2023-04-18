using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class FloraBlock : TrailBlock
{
    [SerializeField] int initialActiveCount = 3;
    int activeCount = 0;

    protected override void Start()
    {
        base.Start();
        var skimmer = GetComponentInChildren<Skimmer>(true);
        if (activeCount < initialActiveCount)
        {
            skimmer.gameObject.SetActive(true);
            activeCount++;
        }
        else skimmer.gameObject.SetActive(false);
    }
}
