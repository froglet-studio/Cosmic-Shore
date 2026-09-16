// Emits the SHIPPED MandelbulbLattice's own answers as JSON, so the offline model can be proved
// against the code that actually runs rather than against a transcription of it.
//
//   Driver <power> <pitch> <iterations> <bailout> <maxSiteRadius>
//
// stdout: {"power":..,"pitch":..,"seed":[x,y,z],"sites":[[x,y,z,nx,ny,nz],...]}
// Sites are the full connected shell reached by the SAME 26-neighbour walk MandelbulbFlora grows
// with, started at the SAME seed - so the harness proves the reachable set, not merely the
// membership predicate.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Driver
{
    public static int Main(string[] argv)
    {
        if (argv.Length < 5) { Console.Error.WriteLine("usage: Driver power pitch iters bailout maxSiteRadius"); return 2; }
        var inv = CultureInfo.InvariantCulture;
        int power = int.Parse(argv[0], inv);
        float pitch = float.Parse(argv[1], inv);
        int iters = int.Parse(argv[2], inv);
        float bailout = float.Parse(argv[3], inv);
        int bound = int.Parse(argv[4], inv);

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

        var sb = new StringBuilder();
        sb.Append("{\"power\":").Append(power)
          .Append(",\"pitch\":").Append(pitch.ToString("R", inv))
          .Append(",\"iterations\":").Append(iters)
          .Append(",\"seed\":[").Append(seed.x).Append(',').Append(seed.y).Append(',').Append(seed.z)
          .Append("],\"sites\":[");
        for (int i = 0; i < order.Count; i++)
        {
            var s = order[i];
            var n = lattice.Normal(s);
            if (i > 0) sb.Append(',');
            sb.Append('[').Append(s.x).Append(',').Append(s.y).Append(',').Append(s.z).Append(',')
              .Append(n.x.ToString("R", inv)).Append(',')
              .Append(n.y.ToString("R", inv)).Append(',')
              .Append(n.z.ToString("R", inv)).Append(']');
        }
        sb.Append("]}");
        Console.Out.Write(sb.ToString());
        return 0;
    }
}
