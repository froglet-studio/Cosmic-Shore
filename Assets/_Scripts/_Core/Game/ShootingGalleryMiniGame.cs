using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;
public class ShootingGalleryMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] SegmentSpawner SegmentSpawner;


    public static new ShipTypes PlayerShipType = ShipTypes.GunManta;

    protected override void Start()
    {
        base.Start();

        

        gameMode = MiniGames.ShootingGallery;
        SegmentSpawner.Seed = new System.Random().Next();
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        SegmentSpawner.numberOfSegments = 60;

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;

        SegmentSpawner.Initialize();

        ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();

        FormRing();
    }


    [SerializeField] protected TrailBlock trailBlock;
    [SerializeField] float blockCount = 20;
    [SerializeField] int ringCount = 3;
    [SerializeField] float radius = 60f;
    [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
    [SerializeField] protected Material blockMaterial;
    protected List<Trail> trails = new List<Trail>();

    public void SetBlockMaterial(Material material)
    {
        blockMaterial = material;
    }

    void FormRing()
    {
        trails.Add(new Trail(true));
        for (int block = 0; block < blockCount; block++)
        {
            CreateRingBlock(block, trails[0]);
        }
    }

    virtual protected void CreateRingBlock(int i, Trail trail)
    {
        var position = transform.position +
                             radius * Mathf.Cos((i / blockCount) * 2 * Mathf.PI) * transform.right +
                             radius * Mathf.Sin((i / blockCount) * 2 * Mathf.PI) * transform.up +
                             radius * transform.forward;
        CreateBlock(position, position, trail);
    }

    virtual protected TrailBlock CreateBlock(Vector3 position, Vector3 lookPosition, Trail trail)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = ActivePlayer.Team;
        Block.ownerId = ActivePlayer.PlayerUUID;
        Block.PlayerName = ActivePlayer.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        //Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + position;
        Block.InnerDimensions = blockScale;
        //Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        trail.Add(Block);
        return Block;
    }
}