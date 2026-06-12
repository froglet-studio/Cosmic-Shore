using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// V14: trail layer — MaterialPropertyAnimator start/target capture + property-block
// retargeting, Trail index math (Add/RemoveOldest/LookAhead/Project ping-pong + loop
// wrap), TrailFollower walking logic, PrismProperties defaults. V15 restored the
// Prism-typed members, so trail blocks are full PrismTestRig prisms and the animator
// resolves its domain materials through a wired theme.
public class TrailLayerTests
{
    // ── doubles ─────────────────────────────────────────────────────────────

    // MonoBehaviour IVesselStatus implementor so TrailFollower.Start can discover it
    // via GetComponent<IVesselStatus>() on the same GameObject.
    class TrailVesselStatusStub : MonoBehaviour, IVesselStatus
    {
        public IVessel Vessel => null;
        public bool AlignmentEnabled { get; set; }
        public Material AOEConicExplosionMaterial { get; set; }
        public Material AOEExplosionMaterial { get; set; }
        public bool IsAttached { get; set; }
        public Prism AttachedPrism { get; set; }
        public Quaternion blockRotation { get; set; }
        public bool IsBoosting { get; set; }
        public float BoostMultiplier { get; set; }
        public float Inertia => 0f;
        public float ChargedBoostCharge { get; set; }
        public bool IsChargedBoostDischarging { get; set; }
        public Vector3 Course { get; set; }
        public bool IsDrifting { get; set; }
        public Transform CameraFollowTarget { get; set; }
        public bool GunsActive { get; set; }
        public bool HasLiveProjectiles { get; set; }
        public bool IsOverheating { get; set; }
        public IPlayer Player { get; set; }
        public bool IsPortrait { get; set; }
        public ResourceSystem ResourceSystem => null;
        public VesselAnimation VesselAnimation => null;
        public List<GameObject> ShipGeometries { get; set; }
        public Transform ShipTransform => null;
        public VesselTransformer VesselTransformer { get; set; }
        public string Name => "trail-stub";
        public VesselClassType VesselType => VesselClassType.Manta;
        public GameObject OrientationHandle => null;
        public Material ShipMaterial { get; set; }
        public Material SkimmerMaterial { get; set; }
        public float Speed { get; set; }
        public bool IsSingleStickControls { get; set; }
        public bool IsSlowed { get; set; }
        public bool IsStationary { get; set; }
        public bool IsTranslationRestricted { get; set; }
        public IVesselHUDController VesselHUDController => null;
        public VesselCustomization Customization => null;
        public R_ShipElementStatsHandler ElementalStatsHandler => null;
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public void ResetForPlay() { }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Priv).SetValue(target, value);

    static T Get<T>(object target, string field)
        => (T)target.GetType().GetField(field, Priv).GetValue(target);

    /// <summary>Straight-line trail with one full rig prism per z position.</summary>
    static Trail MakeTrail(bool isLoop, params float[] zPositions)
    {
        var trail = new Trail(isLoop);
        foreach (var z in zPositions)
            trail.Add(PrismTestRig.CreatePrismAt(new Vector3(0f, 0f, z), $"block z={z}"));
        return trail;
    }

    /// <summary>
    /// Follower wired to a stub IVesselStatus + real VesselTransformer (SpeedMultiplier 1),
    /// attached to <paramref name="trail"/> at block 0 heading Forward. Speeds are
    /// reflection-configured exactly as the inspector would have serialized them.
    /// </summary>
    static (TrailFollower follower, TrailVesselStatusStub status) MakeFollower(
        GameLoop loop, Trail trail, float friendly = 10f, float hostile = 10f, float destroyed = 10f)
    {
        var go = new GameObject("follower");
        go.SetActive(false);
        var status = go.AddComponent<TrailVesselStatusStub>();
        status.VesselTransformer = go.AddComponent<VesselTransformer>();
        var follower = go.AddComponent<TrailFollower>();
        Set(follower, "FriendlyTerrainSpeed", friendly);
        Set(follower, "HostileTerrainSpeed", hostile);
        Set(follower, "DestroyedTerrainSpeed", destroyed);
        go.SetActive(true);
        loop.Tick(0f); // run Start → binds IVesselStatus

        Set(follower, "attachedTrail", trail);
        Set(follower, "direction", TrailFollowerDirection.Forward);
        follower.Throttle = 1f;
        return (follower, status);
    }

    // ── PrismProperties ─────────────────────────────────────────────────────

    [Fact]
    public void PrismProperties_Defaults()
    {
        var props = new PrismProperties();

        Assert.Equal("TrailBlocks", props.DefaultLayerName);
        Assert.Equal((ushort)0, props.Index);
        Assert.False(props.IsShielded);
        Assert.False(props.IsSuperShielded);
        Assert.False(props.IsDangerous);
        Assert.False(props.IsTransparent);
        Assert.Null(props.Trail);
        Assert.Equal(0f, props.TimeCreated);
        Assert.Equal(0f, props.volume);
        Assert.Equal(0f, props.speedDebuffAmount);
        Assert.Equal(Vector3.zero, props.position);
    }

    // ── Trail ───────────────────────────────────────────────────────────────

    [Fact]
    public void Trail_Add_AssignsSequentialIndices_AndIgnoresDuplicates()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f);

        Assert.Equal(3, trail.TrailList.Count);
        Assert.Equal(0, trail.GetBlockIndex(trail.TrailList[0]));
        Assert.Equal(2, trail.GetBlockIndex(trail.TrailList[2]));

        trail.Add(trail.TrailList[1]); // duplicate
        Assert.Equal(3, trail.TrailList.Count);
        Assert.Equal(1, trail.GetBlockIndex(trail.TrailList[1]));

        Assert.Equal(-1, trail.GetBlockIndex(null));
    }

    [Fact]
    public void Trail_RemoveOldest_ReturnsFrontAndReindexes()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f);
        var first = trail.TrailList[0];
        var second = trail.TrailList[1];

        var removed = trail.RemoveOldest();

        Assert.Same(first, removed);
        Assert.Equal(2, trail.TrailList.Count);
        Assert.Equal(0, trail.GetBlockIndex(second));
        Assert.Equal(-1, trail.GetBlockIndex(first));

        trail.RemoveOldest();
        trail.RemoveOldest();
        Assert.Null(trail.RemoveOldest()); // empty trail
    }

    [Fact]
    public void Trail_Clear_EmptiesTrail()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f);
        var block = trail.TrailList[0];

        trail.Clear();

        Assert.Empty(trail.TrailList);
        Assert.Equal(-1, trail.GetBlockIndex(block));
    }

    [Fact]
    public void Trail_GetBlock_NegativeIndexReturnsFirst()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f);

        Assert.Same(trail.TrailList[0], trail.GetBlock(-3));
        Assert.Same(trail.TrailList[1], trail.GetBlock(1));
    }

    [Fact]
    public void Trail_LookAhead_AccumulatesBlocksUntilDistanceCovered()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);

        // 10 to b1 + 10 to b2 covers 15: [b0, b1, b2]
        var blocks = trail.LookAhead(0, 0f, TrailFollowerDirection.Forward, 15f);
        Assert.Equal(new[] { trail.TrailList[0], trail.TrailList[1], trail.TrailList[2] }, blocks);

        // Halfway between b0 and b1, 5 remaining covers 4: [b0, b1]
        blocks = trail.LookAhead(0, 0.5f, TrailFollowerDirection.Forward, 4f);
        Assert.Equal(new[] { trail.TrailList[0], trail.TrailList[1] }, blocks);
    }

    [Fact]
    public void Trail_Project_Forward_LandsWithinSegment()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);

        var position = trail.Project(0, 0f, TrailFollowerDirection.Forward, 4f,
            out int endIndex, out float finalLerp, out var outDirection, out var heading);

        Assert.Equal(4f, position.z, 3);
        Assert.Equal(0, endIndex);
        Assert.Equal(0.4f, finalLerp, 3);
        Assert.Equal(TrailFollowerDirection.Forward, outDirection);
        Assert.Equal(1f, heading.z, 3);
    }

    [Fact]
    public void Trail_Project_Backward_LandsWithinSegment()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);

        var position = trail.Project(2, 0f, TrailFollowerDirection.Backward, 4f,
            out int endIndex, out float finalLerp, out var outDirection, out var heading);

        Assert.Equal(16f, position.z, 3);
        Assert.Equal(2, endIndex);
        Assert.Equal(0.4f, finalLerp, 3);
        Assert.Equal(TrailFollowerDirection.Backward, outDirection);
        Assert.Equal(-1f, heading.z, 3);
    }

    [Fact]
    public void Trail_Project_AtTrailStart_PingPongsToForward()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);

        // Walking backward off the front of a non-loop trail reflects to forward.
        var position = trail.Project(0, 0f, TrailFollowerDirection.Backward, 4f,
            out int endIndex, out float finalLerp, out var outDirection, out var heading);

        Assert.Equal(TrailFollowerDirection.Forward, outDirection);
        Assert.Equal(4f, position.z, 3);
        Assert.Equal(1, endIndex);
        Assert.Equal(0.4f, finalLerp, 3);
        Assert.Equal(1f, heading.z, 3);
    }

    [Fact]
    public void Trail_Project_LoopTrail_WrapsWithoutFlip()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: true, 0f, 10f, 20f, 30f);

        // Forward off the tail of a loop wraps to block 0 instead of reflecting.
        var position = trail.Project(3, 0f, TrailFollowerDirection.Forward, 4f,
            out int endIndex, out float finalLerp, out var outDirection, out var heading);

        Assert.Equal(TrailFollowerDirection.Forward, outDirection);
        Assert.Equal(26f, position.z, 3);
        Assert.Equal(3, endIndex);
        Assert.Equal(-1f, heading.z, 3);
    }

    // ── TrailFollower ───────────────────────────────────────────────────────

    [Fact]
    public void TrailFollower_Start_BindsVesselStatus()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f);
        var (follower, status) = MakeFollower(loop, trail);

        Assert.Same(status, Get<IVesselStatus>(follower, "vesselData"));
        // Domain default path (null Player) resolves to Jade.
        Assert.Equal(Domains.Jade, Get<Domains>(follower, "domain"));
    }

    [Fact]
    public void TrailFollower_IsAttached_Detach_AttachedPrism()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f);
        var (follower, _) = MakeFollower(loop, trail);
        Set(follower, "attachedBlockIndex", 1);

        Assert.True(follower.IsAttached);
        Assert.Same(trail.GetBlock(1), follower.AttachedPrism);

        follower.Detach();
        Assert.False(follower.IsAttached);
    }

    [Fact]
    public void TrailFollower_RideTheTrail_WhenDetached_DoesNothing()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f);
        var (follower, status) = MakeFollower(loop, trail);
        follower.Detach();

        loop.Tick(0.1f);
        follower.RideTheTrail();

        Assert.Equal(Vector3.zero, follower.transform.position);
        Assert.Equal(0f, status.Speed);
    }

    [Fact]
    public void TrailFollower_RideTheTrail_MovesAlongTrail_AndWritesSpeedAndCourse()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);
        var (follower, status) = MakeFollower(loop, trail);

        loop.Tick(0.1f); // deltaTime 0.1 → walks Throttle(1) × speed(10) × 0.1 = 1 unit
        follower.RideTheTrail();

        Assert.Equal(1f, follower.transform.position.z, 3);
        Assert.Equal(0f, follower.transform.position.x, 3);
        Assert.Equal(1f, status.Course.z, 3);
        Assert.Equal(10f, status.Speed, 3); // terrain speed × SpeedMultiplier(1)
        Assert.Equal(0.1f, Get<float>(follower, "percentTowardNextBlock"), 3);
        Assert.Equal(0, Get<int>(follower, "attachedBlockIndex"));
        Assert.Equal(TrailFollowerDirection.Forward, follower.Direction);
    }

    [Fact]
    public void TrailFollower_RideTheTrail_CrossingABlockBoundary_AdvancesAttachedIndex()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);
        var (follower, _) = MakeFollower(loop, trail);

        loop.Tick(1.5f); // walks 15 world units at speed 10
        follower.RideTheTrail();

        Assert.Equal(1, Get<int>(follower, "attachedBlockIndex"));
        // Verbatim Project behavior: currentBlock stays the walk origin inside the
        // accumulation loop, so the final lerp spans b0→b2 — z lands at 5.
        Assert.Equal(5f, follower.transform.position.z, 3);
        Assert.Equal(0.25f, Get<float>(follower, "percentTowardNextBlock"), 3);
    }

    [Fact]
    public void TrailFollower_RideTheTrail_Backward_MovesDownTheTrail()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);
        var (follower, status) = MakeFollower(loop, trail);
        Set(follower, "attachedBlockIndex", 2);
        Set(follower, "direction", TrailFollowerDirection.Backward);

        loop.Tick(0.1f); // walks 1 unit toward -z
        follower.RideTheTrail();

        Assert.Equal(19f, follower.transform.position.z, 3);
        Assert.Equal(-1f, status.Course.z, 3);
        Assert.Equal(TrailFollowerDirection.Backward, follower.Direction);
        Assert.Equal(2, Get<int>(follower, "attachedBlockIndex"));
    }

    [Fact]
    public void TrailFollower_SetDirection_FlipsLerpAndShiftsIndex()
    {
        using var loop = new GameLoop();
        var trail = MakeTrail(isLoop: false, 0f, 10f, 20f, 30f);
        var (follower, _) = MakeFollower(loop, trail);
        Set(follower, "attachedBlockIndex", 2);
        Set(follower, "percentTowardNextBlock", 0.3f);

        follower.SetDirection(TrailFollowerDirection.Backward);
        Assert.Equal(TrailFollowerDirection.Backward, follower.Direction);
        Assert.Equal(0.7f, Get<float>(follower, "percentTowardNextBlock"), 3);
        Assert.Equal(3, Get<int>(follower, "attachedBlockIndex"));

        follower.SetDirection(TrailFollowerDirection.Backward); // same direction → no-op
        Assert.Equal(0.7f, Get<float>(follower, "percentTowardNextBlock"), 3);
        Assert.Equal(3, Get<int>(follower, "attachedBlockIndex"));

        follower.SetDirection(TrailFollowerDirection.Forward);
        Assert.Equal(0.3f, Get<float>(follower, "percentTowardNextBlock"), 3);
        Assert.Equal(2, Get<int>(follower, "attachedBlockIndex"));
    }

    // ── MaterialPropertyAnimator ────────────────────────────────────────────

    static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
    static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
    static readonly int SpreadId = Shader.PropertyToID("_Spread");

    static Material MakeMaterial(Color bright, Color dark, Vector4 spread)
    {
        var material = new Material(Shader.Find("CosmicShore/Prism"));
        material.SetColor(BrightColorId, bright);
        material.SetColor(DarkColorId, dark);
        material.SetVector(SpreadId, spread);
        return material;
    }

    static (MaterialPropertyAnimator animator, MeshRenderer renderer) MakeAnimator(Material current)
    {
        // V15: ValidateMaterials resolves the prism's domain materials through the theme,
        // so the rig's Blue (default-domain) block materials point at the renderer's
        // current material — the start-state capture still reads the original material.
        var rig = PrismTestRig.Create("prism", beforeActivate: r =>
        {
            r.GameObject.GetComponent<MeshRenderer>().sharedMaterial = current;
            var blueSet = r.Theme.TeamMaterialSets[Domains.Blue];
            blueSet.BlockMaterial = current;
            blueSet.TransparentBlockMaterial = current;
        });
        return (rig.MaterialAnimator, rig.GameObject.GetComponent<MeshRenderer>());
    }

    [Fact]
    public void MaterialPropertyAnimator_MissingMeshRenderer_DisablesSelf()
    {
        using var loop = new GameLoop();
        var go = new GameObject("no-renderer");
        go.SetActive(false);
        var animator = go.AddComponent<MaterialPropertyAnimator>();
        go.SetActive(true);

        Assert.False(animator.enabled);
        Assert.Null(animator.PropertyBlock);
    }

    [Fact]
    public void MaterialPropertyAnimator_UpdateMaterial_CapturesStartFromSharedMaterialAndTargetsFromTransparent()
    {
        using var loop = new GameLoop();
        var current = MakeMaterial(Color.red, Color.blue, new Vector4(1f, 2f, 3f, 0f));
        var (animator, renderer) = MakeAnimator(current);
        var transparent = MakeMaterial(Color.green, Color.white, new Vector4(4f, 5f, 6f, 0f));
        var opaque = MakeMaterial(Color.gray, Color.black, new Vector4(7f, 8f, 9f, 0f));
        bool completed = false;

        Assert.Equal(1f, animator.AnimationProgress); // idle default
        animator.UpdateMaterial(transparent, opaque, 0.5f, () => completed = true);

        Assert.True(animator.IsAnimating);
        Assert.Equal(0f, animator.AnimationProgress);
        Assert.Equal(0.5f, animator.Duration);
        Assert.Equal(Color.red, animator.StartBrightColor);
        Assert.Equal(Color.blue, animator.StartDarkColor);
        Assert.Equal(new Vector3(1f, 2f, 3f), animator.StartSpread);
        Assert.Equal(Color.green, animator.TargetBrightColor);
        Assert.Equal(Color.white, animator.TargetDarkColor);
        Assert.Equal(new Vector3(4f, 5f, 6f), animator.TargetSpread);
        Assert.False(completed);

        animator.OnAnimationComplete(); // MaterialStateManager drives this at progress 1
        Assert.True(completed);

        // Completion latches the new material pair for transparency swaps.
        animator.SetTransparency(true);
        Assert.Same(transparent, renderer.sharedMaterial);
        animator.SetTransparency(false);
        Assert.Same(opaque, renderer.sharedMaterial);
    }

    [Fact]
    public void MaterialPropertyAnimator_UpdateMaterial_NullMaterials_Rejected()
    {
        using var loop = new GameLoop();
        var current = MakeMaterial(Color.red, Color.blue, Vector4.zero);
        var (animator, _) = MakeAnimator(current);

        animator.UpdateMaterial(null, MakeMaterial(Color.gray, Color.black, Vector4.zero));

        Assert.False(animator.IsAnimating);
        Assert.Equal(1f, animator.AnimationProgress);
    }

    [Fact]
    public void MaterialPropertyAnimator_RetargetMidAnimation_CapturesStartFromPropertyBlock()
    {
        using var loop = new GameLoop();
        var current = MakeMaterial(Color.red, Color.blue, new Vector4(1f, 2f, 3f, 0f));
        var (animator, renderer) = MakeAnimator(current);
        var transparent = MakeMaterial(Color.green, Color.white, new Vector4(4f, 5f, 6f, 0f));
        var opaque = MakeMaterial(Color.gray, Color.black, Vector4.zero);

        animator.UpdateMaterial(transparent, opaque); // now animating

        // Mid-animation visual state lives on the renderer's property block.
        var midBright = new Color(0.5f, 0.25f, 0f, 1f);
        var midDark = new Color(0f, 0.25f, 0.5f, 1f);
        var block = new MaterialPropertyBlock();
        block.SetColor(BrightColorId, midBright);
        block.SetColor(DarkColorId, midDark);
        block.SetVector(SpreadId, new Vector4(2.5f, 3.5f, 4.5f, 0f));
        renderer.SetPropertyBlock(block);

        var transparent2 = MakeMaterial(Color.yellow, Color.cyan, new Vector4(9f, 9f, 9f, 0f));
        animator.UpdateMaterial(transparent2, opaque);

        Assert.Equal(midBright, animator.StartBrightColor);
        Assert.Equal(midDark, animator.StartDarkColor);
        Assert.Equal(new Vector3(2.5f, 3.5f, 4.5f), animator.StartSpread);
        Assert.Equal(Color.yellow, animator.TargetBrightColor);
        Assert.True(animator.IsAnimating);
    }

    [Fact]
    public void MaterialPropertyBlock_RoundTripsThroughRenderer_AsSnapshot()
    {
        using var loop = new GameLoop();
        var go = new GameObject("renderer");
        var renderer = go.AddComponent<MeshRenderer>();

        var source = new MaterialPropertyBlock();
        source.SetColor(BrightColorId, new Color(0.1f, 0.2f, 0.3f, 1f));
        source.SetVector(SpreadId, new Vector4(1f, 2f, 3f, 4f));
        renderer.SetPropertyBlock(source);
        source.SetColor(BrightColorId, Color.white); // mutate after set — renderer holds a snapshot

        var dest = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(dest);

        Assert.Equal(new Color(0.1f, 0.2f, 0.3f, 1f), dest.GetColor(BrightColorId));
        Assert.Equal(new Vector4(1f, 2f, 3f, 4f), dest.GetVector(SpreadId));
        Assert.Equal(Color.clear, dest.GetColor(DarkColorId)); // unset default
        Assert.Equal(0f, dest.GetFloat(SpreadId));
    }
}
