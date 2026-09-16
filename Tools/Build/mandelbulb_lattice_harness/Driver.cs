// Emits the SHIPPED MandelbulbLattice's own answers as JSON, so the offline model can be proved
// against the code that actually runs rather than against a transcription of it.
//
//   Driver <power> <pitch> <iterations> <bailout> <maxSiteRadius>
//          <riserBias> <coplanarCos> <planarTau> <maxCells> <pad> <thickness> <containDrop>
//
// stdout: {"power":..,"pitch":..,"seed":[x,y,z],
//          "sites":[[x,y,z,nx,ny,nz],...],
//          "plates":[[sx,sy,sz,cells,cx,cy,cz,r..,u..,f..,size..],...]}
//
// "sites" is the full connected shell reached by the SAME 26-neighbour walk MandelbulbFlora
// grows with, from the SAME seed - so the harness proves the reachable set, not merely the
// membership predicate. "plates" is what the plant actually lays, in growth order, each stamped
// with the integer cell that identifies its patch.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Driver
{
    static CultureInfo Inv => CultureInfo.InvariantCulture;

    static void F(StringBuilder sb, float v) => sb.Append(v.ToString("R", Inv));

    static void V(StringBuilder sb, Vector3 v)
    {
        F(sb, v.x); sb.Append(','); F(sb, v.y); sb.Append(','); F(sb, v.z);
    }

    public static int Main(string[] argv)
    {
        if (argv.Length < 12)
        {
            Console.Error.WriteLine("usage: Driver power pitch iters bailout maxSiteRadius " +
                                    "riser coplanar tau maxCells pad thickness containDrop");
            return 2;
        }
        int power = int.Parse(argv[0], Inv);
        float pitch = float.Parse(argv[1], Inv);
        int iters = int.Parse(argv[2], Inv);
        float bailout = float.Parse(argv[3], Inv);
        int bound = int.Parse(argv[4], Inv);
        var rules = new MandelbulbLattice.PlatingRules(
            float.Parse(argv[5], Inv), float.Parse(argv[6], Inv), float.Parse(argv[7], Inv),
            int.Parse(argv[8], Inv), float.Parse(argv[9], Inv), float.Parse(argv[10], Inv),
            float.Parse(argv[11], Inv), bound);

        var lattice = MandelbulbLattice.For(power, pitch, iters, bailout);
        var seed = lattice.SeedSite(bound);

        var claimed = new HashSet<Vector3Int> { seed };
        var frontier = new Queue<Vector3Int>();
        frontier.Enqueue(seed);
        var order = new List<Vector3Int>();
        while (frontier.Count > 0)
        {
            var site = frontier.Dequeue();
            order.Add(site);
            foreach (var d in MandelbulbLattice.Neighbour26)
            {
                var next = site + d;
                if (Math.Abs(next.x) > bound || Math.Abs(next.y) > bound || Math.Abs(next.z) > bound) continue;
                if (!claimed.Add(next)) continue;
                if (!lattice.IsShellSite(next)) continue;
                frontier.Enqueue(next);
            }
        }

        var plates = lattice.Plating(rules);

        var sb = new StringBuilder();
        sb.Append("{\"power\":").Append(power)
          .Append(",\"pitch\":").Append(pitch.ToString("R", Inv))
          .Append(",\"iterations\":").Append(iters)
          .Append(",\"seed\":[").Append(seed.x).Append(',').Append(seed.y).Append(',').Append(seed.z)
          .Append("],\"sites\":[");
        for (int i = 0; i < order.Count; i++)
        {
            var s = order[i];
            var n = lattice.Normal(s);
            if (i > 0) sb.Append(',');
            sb.Append('[').Append(s.x).Append(',').Append(s.y).Append(',').Append(s.z).Append(',');
            V(sb, n);
            sb.Append(']');
        }
        sb.Append("],\"plates\":[");
        for (int i = 0; i < plates.Count; i++)
        {
            var p = plates[i];
            if (i > 0) sb.Append(',');
            sb.Append('[').Append(p.Seed.x).Append(',').Append(p.Seed.y).Append(',').Append(p.Seed.z)
              .Append(',').Append(p.Cells).Append(',');
            V(sb, p.Centre); sb.Append(',');
            V(sb, p.Right); sb.Append(',');
            V(sb, p.Up); sb.Append(',');
            V(sb, p.Forward); sb.Append(',');
            V(sb, p.Size);
            sb.Append(']');
        }
        sb.Append("]}");
        Console.Out.Write(sb.ToString());
        return 0;
    }
}
