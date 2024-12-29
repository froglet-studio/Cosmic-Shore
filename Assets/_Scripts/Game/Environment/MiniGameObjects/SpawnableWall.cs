using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Environment.FlowField;

public class SpawnableWall : SpawnableAbstractBase
{
    [SerializeField] TrailBlock trailBlock;
    [SerializeField] Crystal crystal;
    [SerializeField] float blockSize = 1f;
    [SerializeField] float padding = .1f;
    public int Width = 6;
    public int Height = 6;
    static ushort WallCount = 0;

    private void Awake()
    {
        WallCount++;
    }

    public override GameObject Spawn()
    {
        return Spawn(1);
    }

    public override GameObject Spawn(int intensityLevel)
    {
        Width = 6 - intensityLevel;
        Height = 6 - intensityLevel;
        GameObject container = new GameObject("Wall");
        var trail = new Trail();
        var size = new Vector3(1, 1, .1f);
        var blockSpacing = blockSize + padding;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Vector3 position = new Vector3(x * blockSpacing, y * blockSpacing, 0);
                var correction = new Vector3(blockSpacing * .5f , blockSpacing * .5f, 0);
                CreateBlock(position + correction, Vector3.up, $"WB:{WallCount}:{x}:{y}", trail, size * blockSize, trailBlock, container, Vector3.forward, Teams.Blue, false);
                if (crystal != null)
                {
                    var newCrystal = Instantiate(crystal,container.transform);
                    newCrystal.transform.position = position + (Vector3.forward * Random.Range(-1f,1f) * Width * blockSize);
                    newCrystal.transform.localScale *= 5f * Mathf.Pow(Random.Range(.1f,1f),16) + 1f;
                }
            }
        }

        trails.Add(trail);
        return container;
    }
}