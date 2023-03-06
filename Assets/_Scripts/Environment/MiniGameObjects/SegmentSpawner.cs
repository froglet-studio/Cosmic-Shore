using System;
using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;

public class SegmentSpawner : MonoBehaviour
{
    [SerializeField] TrailBlock blueBlockTrail;
    [SerializeField] int numberOfSegments = 1;
    List<Trail> trails = new List<Trail>();

    [SerializeField] float aa;
    [SerializeField] float bb;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        // Clear out last run
        foreach (Trail trail in trails) { 
            foreach (var block in trail.TrailList)
            {
                Destroy(block);
            }    
        }
        trails.Clear();

        for (int i = 0; i < numberOfSegments; i++)
        {
            CreateBatmanSegment();
        }
        var heart = CreateHeartSegment();
        heart.transform.position = new Vector3(90, 90, 0);

    }

    GameObject CreateHeartSegment()
    {
        GameObject container = new GameObject();
        container.name = "heart";

        var trail = new Trail();

        int blockCount = 60;
        for (int block = 0; block < blockCount; block++)
        {
            var t = ((float) block / (float) blockCount)*Mathf.PI*2;
            // x = 16sin^3(t)
            var x = Mathf.Pow(Mathf.Sin(t), 3) * 16;
            var y = (13*Mathf.Cos(t)) - (5*Mathf.Cos(2*t)) - (2 * Mathf.Cos(3 * t)) - (Mathf.Cos(4 * t));
            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::" + block, trail, Vector3.one, blueBlockTrail, container);
        }

        trails.Add(trail);

        return container;
    }

    void CreateSpiralSegment()
    {
        GameObject container = new GameObject();
        container.name = "spiral";

        //x(t) = aa · exp(bb · t) · cos(t) y(t) = aa · exp(bb · t) · sin(t).
        var trail = new Trail();

        float blockCount = Mathf.PI * 8;
        for (float block = .1f; block < blockCount; block += .2f) //(1.2f-block/blockCount)
        {
            var t = ((float)block / (float)blockCount);
            // x = 16sin^3(t)
            var x = aa * Mathf.Exp(bb*t) * Mathf.Cos(t);
            var y = aa * Mathf.Exp(bb*t) * Mathf.Sin(t);
            var z = (block / blockCount) * 12;
            var position = new Vector3(x, y, z);
            CreateBlock(position, Vector3.zero, "SEGMENT::" + block, trail, Vector3.one, blueBlockTrail, container);
        }

        trails.Add(trail);
    }

    void CreateBatmanSegment()
    {
        GameObject container = new GameObject();
        container.name = "batman";

        Func<float, float> outerWing = (x) => (float)(2 * Math.Sqrt(-Math.Abs(Math.Abs(x) - 1) * Math.Abs(3 - Math.Abs(x)) / ((Math.Abs(x) - 1) * (3 - Math.Abs(x)))) * (1 + Math.Abs(Math.Abs(x) - 3) / (Math.Abs(x) - 3)) * Math.Sqrt(1 - Math.Pow((x / 7), 2)) + (5 + 0.97f * (Math.Abs(x - 0.5f) + Math.Abs(x + 0.5f)) - 3 * (Math.Abs(x - 0.75f) + Math.Abs(x + 0.75f))) * (1 + Math.Abs(1 - Math.Abs(x)) / (1 - Math.Abs(x))));
        Func<float, float> head1 = (x) => (float)(9-8*Math.Abs(x));
        Func<float, float> head2 = (x) => (float)(3*Math.Abs(x)+.75);
        Func<float, float> head3 = (x) => (float)(2.25);
        Func<float, float> bottom = (x) => (float)(Math.Abs(x / 2) - 0.0913722 * (Math.Pow(x, 2)) - 3 + Math.Sqrt(1 - Math.Pow((Math.Abs(Math.Abs(x) - 2) - 1), 2)));
        Func<float, float> innerWing = (x) => (float)((2.71052 + (1.5 - .5 * Math.Abs(x)) - 1.35526 * Math.Sqrt(4 - Math.Pow((Math.Abs(x) - 1), 2))) * Math.Sqrt(Math.Abs(Math.Abs(x) - 1) / (Math.Abs(x) - 1)) + 0.9);

        var trail = new Trail();

        // Outer Wings
        for (float block = 3.2f; block < 7; block+=.2f)
        {
            var x = block * 5;
            var y = outerWing(block) * 5;

            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);

            if (block > 4)
            {
                position = new Vector3(x, -y, 0);
                CreateBlock(position, Vector3.zero, "SEGMENT::" + block + "::1", trail, Vector3.one, blueBlockTrail, container);
            }

            position = new Vector3(-x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::" + block + "::2", trail, Vector3.one, blueBlockTrail, container);

            if (block > 4)
            {
                position = new Vector3(-x, -y, 0);
                CreateBlock(position, Vector3.zero, "SEGMENT::" + block + "::3", trail, Vector3.one, blueBlockTrail, container);
            }
        }

        // Inner Wings
        for (float block = 1.2f; block <= 3; block += .2f)
        {
            var x = block * 5;
            var y = innerWing(block) * 5;

            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::INNER::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);

            position = new Vector3(-x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::INNER::" + block + "::1", trail, Vector3.one, blueBlockTrail, container);
        }

        // Bottom
        for (float block = 0; block <= 4; block += .2f)
        {
            var x = block * 5;
            var y = (bottom(block) * 5)-2;

            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::BOTTOM::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);

            position = new Vector3(-x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::BOTTOM::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);
        }

        // Head
        for (float block = .75f; block <= 1; block += .025f)
        {
            var x = block * 5;
            var y = (head1(block) * 5) + 5;

            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::HEAD1::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);
            position = new Vector3(-x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::HEAD1::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);
        }
        // Head2
        for (float block = .5f; block <= .75; block += .025f)
        {
            var x = block * 5;
            var y = (head2(block) * 5) + 5;

            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::HEAD2::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);

            position = new Vector3(-x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::HEAD2::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);
        }
        // Head
        for (float block = -.5f; block <= .5f; block += .025f)
        {
            var x = block * 5;
            var y = (head3(block) * 5)+5;

            var position = new Vector3(x, y, 0);
            CreateBlock(position, Vector3.zero, "SEGMENT::HEAD3::" + block + "::0", trail, Vector3.one, blueBlockTrail, container);
        }


        trails.Add(trail);

    }

    //sin(arctan(x,y)+(x^2+y^2)^1/2) = 0

    void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = Teams.None;
        Block.ownerId = "public";
        Block.PlayerName = "";
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        Block.transform.SetParent(container.transform, false);
        Block.ID = blockId;
        Block.InnerDimensions = scale;
        Block.Trail = trail;
        trail.Add(Block);

    }
}