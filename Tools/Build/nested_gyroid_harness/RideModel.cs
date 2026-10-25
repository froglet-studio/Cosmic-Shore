// A MODEL of the Urchin's surface ride kernel (Assets/_Scripts/Controller/Vessel/BlockscapeFollower.cs),
// transcribed - the shipped kernel is a MonoBehaviour over UnityEngine.Vector3 and cannot run here. What is
// transcribed: ground refresh (spatial query radius, nearest-centre rule, and the LAYERED hover-point rule),
// the smoothed normal, the rim wrap, the inertial surface velocity, the hover spring, and the layered climb.
// If BlockscapeFollower changes, change this with it - the comments name the method each block mirrors.
using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Gameplay;

sealed class RideModel
{
    // BlockscapeFollower serialized defaults (Urchin.prefab overrides none of these).
    public float HoverHeight = 2f, NormalTrackingRate = 5f, HoverTrackingRate = 5f;
    public float GroundSearchRadiusScale = 2.5f, SurfaceInertiaRate = 4f, RimWrapMargin = 1f;
    public float LayerClimbFraction = 0.6f, LayerClimbDeadzone = 0.35f;
    public bool Layered;

    readonly NestedGyroidLattice L;
    readonly Dictionary<long, List<int>> grid = new();
    const float Cell = 30f;

    public Vector3 Position, SurfaceNormal, Velocity;
    public int Ground;

    public RideModel(NestedGyroidLattice lattice)
    {
        L = lattice;
        for (int i = 0; i < L.Count; i++)
        {
            long k = Key(L.Position[i]);
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
            l.Add(i);
        }
    }

    static long Key(Vector3 p) => Key((long)MathF.Floor(p.X / Cell), (long)MathF.Floor(p.Y / Cell), (long)MathF.Floor(p.Z / Cell));
    static long Key(long x, long y, long z) => ((x + 4096) << 26) | ((y + 4096) << 13) | (z + 4096);

    void Query(Vector3 p, float r, List<int> into)
    {
        into.Clear();
        long bx = (long)MathF.Floor(p.X / Cell), by = (long)MathF.Floor(p.Y / Cell), bz = (long)MathF.Floor(p.Z / Cell);
        int span = (int)MathF.Ceiling(r / Cell);
        for (long x = -span; x <= span; x++)
        for (long y = -span; y <= span; y++)
        for (long z = -span; z <= span; z++)
            if (grid.TryGetValue(Key(bx + x, by + y, bz + z), out var l))
                foreach (int i in l) if (Vector3.DistanceSquared(L.Position[i], p) <= r * r) into.Add(i);
    }

    float Extent(int i) { var s = L.Size[i]; return MathF.Max(s.X, MathF.Max(s.Y, s.Z)); }

    // OrientNormal
    Vector3 Orient(int i, Vector3 reference) { var n = L.Forward[i]; return Vector3.Dot(n, reference) < 0f ? -n : n; }

    // Attach
    public void Attach(int prism, Vector3 from)
    {
        Ground = prism;
        SurfaceNormal = Orient(prism, from - L.Position[prism]);
        Position = L.Position[prism] + SurfaceNormal * HoverHeight;
        Velocity = Vector3.Zero;
    }

    // GroundScore: the layered rule measures to each candidate's HOVER POINT.
    float Score(int i) =>
        Layered
            ? Vector3.DistanceSquared(L.Position[i] + Orient(i, SurfaceNormal) * HoverHeight, Position)
            : Vector3.DistanceSquared(L.Position[i], Position);

    // The pilot's stack intent this frame: +1 up G, -1 down G, 0 = stay on the layer (IsLayerEligible).
    int _intent;

    // True when the last refresh saw a layer to climb INTO - the spring lets go only then, so pitching out of
    // the outermost skin keeps you on it instead of floating away.
    bool _ahead;

    // Same layer always; the next strut or the next sheet (stack +-1 or +-2) only in the direction of intent.
    bool Eligible(int c)
    {
        if (!Layered) return true;
        int delta = L.Stack[c] - L.Stack[Ground];
        if (delta == 0) return true;
        bool ahead = _intent != 0 && Math.Sign(delta) == _intent && Math.Abs(delta) <= 2;
        _ahead |= ahead;
        return ahead;
    }

    readonly List<int> cand = new();

    // RefreshGroundPrism
    void RefreshGround()
    {
        float radius = MathF.Max(1f, Extent(Ground)) * GroundSearchRadiusScale + HoverHeight;
        Query(Position, radius, cand);
        _ahead = false;
        int best = Ground;
        float bestSq = Score(Ground);
        foreach (int c in cand)
        {
            if (c == Ground || !Eligible(c)) continue;
            float d = Score(c);
            if (d < bestSq) { bestSq = d; best = c; }
        }
        Ground = best;
    }

    static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - Vector3.Dot(v, n) * n;
    static Vector3 Slerp(Vector3 a, Vector3 b, float t)
    {
        float d = Math.Clamp(Vector3.Dot(a, b), -1f, 1f);
        float th = MathF.Acos(d) * t;
        var rel = b - a * d;
        if (rel.LengthSquared() < 1e-10f) return a;
        rel = Vector3.Normalize(rel);
        return a * MathF.Cos(th) + rel * MathF.Sin(th);
    }

    // ResolveSurfaceFrame
    void Frame(out Vector3 target, out Vector3 anchor)
    {
        var center = L.Position[Ground];
        var authored = Orient(Ground, SurfaceNormal);
        var offset = Position - center;
        var inPlane = offset - authored * Vector3.Dot(offset, authored);
        float mag = inPlane.Length();
        float rim = MathF.Max(1f, Extent(Ground));
        float over = mag - rim;
        if (over <= 0f) { target = authored; anchor = center; return; }
        var rimPoint = center + inPlane * (rim / MathF.Max(mag, 1e-4f));
        var radial = Position - rimPoint;
        float wrap = Math.Clamp(over / MathF.Max(0.01f, rim * RimWrapMargin), 0f, 1f);
        target = Vector3.Normalize(Slerp(authored, radial.LengthSquared() > 1e-6f ? Vector3.Normalize(radial) : authored, wrap));
        anchor = rimPoint;
    }

    // RideTheTrail, one frame. forward = the hull's forward (the pilot's aim), throttle in [-1, 1].
    public void Tick(Vector3 forward, float throttle, float speed, float dt)
    {
        // Climb intent: the aim's normal component past the deadzone, signed in G (the ground's Forward is +∇G).
        float pitch = Vector3.Dot(forward, SurfaceNormal) * MathF.Sign(throttle);
        float climb = MathF.Abs(pitch) <= LayerClimbDeadzone ? 0f : MathF.Sign(pitch) * (MathF.Abs(pitch) - LayerClimbDeadzone) / (1f - LayerClimbDeadzone);
        _intent = climb == 0f ? 0 : Math.Sign(climb * Vector3.Dot(SurfaceNormal, L.Forward[Ground]));
        RefreshGround();
        Frame(out var targetNormal, out var anchor);
        SurfaceNormal = Vector3.Normalize(Slerp(SurfaceNormal, targetNormal, 1f - MathF.Exp(-NormalTrackingRate * dt)));

        float targetSpeed = MathF.Abs(throttle) * speed;
        var tangent = ProjectOnPlane(forward, SurfaceNormal);
        if (tangent.LengthSquared() > 1e-6f) tangent = Vector3.Normalize(tangent);
        var desired = tangent * (MathF.Sign(throttle) * targetSpeed);
        Velocity = ProjectOnPlane(Velocity, SurfaceNormal);
        Velocity = Vector3.Lerp(Velocity, desired, 1f - MathF.Exp(-SurfaceInertiaRate * dt));
        var move = Velocity * dt;

        // Layered climb: the aim's component along the normal steps the ride through the stack.
        if (Layered) move += SurfaceNormal * (climb * targetSpeed * LayerClimbFraction * dt);

        float height = Vector3.Dot(Position + move - anchor, SurfaceNormal);
        bool climbing = Layered && _intent != 0 && _ahead;
        if (!climbing) move += SurfaceNormal * ((HoverHeight - height) * (1f - MathF.Exp(-HoverTrackingRate * dt)));
        Position += move;
    }
}
