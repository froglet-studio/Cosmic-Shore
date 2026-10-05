// The research arena as an IWearWorld (the wearer's body surface): a worn prism hangs off its creature's body; ONE
// PoseBody per creature per tick moves every worn prism (the harness writes each prism's point the way the glue's batched
// pass does) and counts the research's cost model (wearers.py sync_body): prism writes, 8 u bucket crossings
// (PrismSpatialIndex rebuckets), and container writes (one per creature whose body moved).
using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Gameplay;

sealed class WearArena : Arena, IWearWorld
{
    readonly Dictionary<int, Dictionary<int, Vector3>> _bodies = new();   // creature -> (handle -> local)
    readonly Dictionary<int, int> _wornIn = new();                         // handle -> creature
    public long PrismWrites, Rebuckets, ContainerWrites, DangerToggles, Wears, Unwears;
    public readonly HashSet<int> Dangerous = new();

    public WearArena(int seed, float r = 1200f) : base(seed, r) { }

    public int WornIn(int h) => _wornIn.TryGetValue(h, out int c) ? c : -1;

    public void Wear(int h, int creature, Vector3 local)
    {
        if (_wornIn.TryGetValue(h, out int was)) _bodies[was].Remove(h);
        if (!_bodies.TryGetValue(creature, out var b)) _bodies[creature] = b = new Dictionary<int, Vector3>();
        b[h] = local; _wornIn[h] = creature;
        Carried.Add(h); Wears++;
    }

    public void Unwear(int h)
    {
        if (!_wornIn.TryGetValue(h, out int c)) return;
        _bodies[c].Remove(h); _wornIn.Remove(h);
        Carried.Remove(h); Dangerous.Remove(h); Unwears++;
    }

    public void PoseBody(int creature, Vector3 pos, Vector3 right, Vector3 up, Vector3 forward, float squash)
    {
        if (!_bodies.TryGetValue(creature, out var b) || b.Count == 0) return;
        bool moved = false;
        foreach (var kv in b)
        {
            var l = kv.Value * squash;
            var tgt = pos + right * l.X + up * l.Y + forward * l.Z;
            var old = Pos[kv.Key];
            if (Vector3.DistanceSquared(old, tgt) <= 0.25f) continue;
            if (MathF.Floor(old.X / 8f) != MathF.Floor(tgt.X / 8f) || MathF.Floor(old.Y / 8f) != MathF.Floor(tgt.Y / 8f)
                || MathF.Floor(old.Z / 8f) != MathF.Floor(tgt.Z / 8f)) Rebuckets++;
            Pos[kv.Key] = tgt; PrismWrites++; Moves++; moved = true;
        }
        if (moved) ContainerWrites++;
    }

    public void SetBodyDanger(int creature, bool danger)
    {
        DangerToggles++;
        if (!_bodies.TryGetValue(creature, out var b)) return;
        foreach (int h in b.Keys) { if (danger) Dangerous.Add(h); else Dangerous.Remove(h); }
    }
}
