// Drives the SHIPPED MandelbulbSurface.cs. stdout is a DATA channel: every line is
// whitespace-separated numbers under a one-word tag, and nothing else may write to it
// (csc's own diagnostics are redirected to stderr by run.sh — a single warning landing
// in front of this would make the caller's parse fail naming nothing).
using System;
using System.Globalization;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using CosmicShore.Gameplay;

static class Driver
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => v.ToString("R", Inv);
    static double D(string s) => double.Parse(s, Inv);

    static int Main(string[] argv)
    {
        if (argv.Length == 0) { Console.Error.WriteLine("usage: probe|bake|grow|shipped|gasket|field|crit|posetable|selftest"); return 2; }
        switch (argv[0])
        {
            case "probe": return Probe(argv);
            case "bake": return Bake(argv);
            case "grow": return Grow(argv);
            case "shipped": return Shipped(argv);
            case "gasket": return Gasket(argv);
            case "field": return Field(argv);
            case "crit": return Crit(argv);
            case "posetable": return PoseTable(argv);
            case "selftest": return SelfTest();
            default: Console.Error.WriteLine("unknown command " + argv[0]); return 2;
        }
    }

    // probe <power> <cx> <cy> <cz> <w> <h> <iterations> <bailout> <sigma> <mandel>
    // Reports whether this Julia constant is worth baking at all: a constant whose set is
    // a ball gives a near-flat field, and a plant grown on it is a sphere.
    static int Probe(string[] a)
    {
        int w = (int)D(a[5]), h = (int)D(a[6]);
        Bulb.Mandel = a.Length <= 10 || D(a[10]) != 0.0;
        var raw = Bulb.March(w, h, D(a[1]), D(a[2]), D(a[3]), D(a[4]), (int)D(a[7]), D(a[8]), 2.2, 0.03);
        var sm = Bulb.Smooth(raw, w, h, D(a[9]));
        Stats(raw, out double rMin, out double rMax, out double rMean, out double rStd);
        Stats(sm, out double sMin, out double sMax, out double sMean, out double sStd);
        double miss = 0; foreach (var v in raw) if (v <= 0.0301f) miss++;
        Console.WriteLine($"raw {F(rMin)} {F(rMax)} {F(rMean)} {F(rStd)}");
        Console.WriteLine($"smooth {F(sMin)} {F(sMax)} {F(sMean)} {F(sStd)}");
        Console.WriteLine($"miss {F(miss / raw.Length)}");
        return 0;
    }

    static void Stats(float[] v, out double min, out double max, out double mean, out double std)
    {
        min = double.MaxValue; max = double.MinValue; double s = 0, s2 = 0;
        foreach (var x in v) { if (x < min) min = x; if (x > max) max = x; s += x; s2 += (double)x * x; }
        mean = s / v.Length;
        std = Math.Sqrt(Math.Max(0.0, s2 / v.Length - mean * mean));
    }

    // bake <power> <cx> <cy> <cz> <delta> <w> <h> <degree> <iterations> <bailout> <sigma> <relief> <mandel>
    //
    // Four marches, never fifty-six: the surface's response to c is linear over the safe
    // basin, so mean + the three one-sided differences ARE the first-order expansion of
    // R(c) and a plant is the shared basis plus three floats.
    //
    // TWO normalisations happen here and nowhere else, because both are pure scales in
    // coefficient space and doing them offline costs the runtime nothing:
    //
    //  * The mean radius is driven to exactly 1, so the growth rules read in plant radii
    //    and a species' RadiusMin/Max mean the same thing at every fractal order.
    //  * The non-DC band is multiplied by `relief`. MEASURED: the true outer surface's
    //    radial relief is 1.2% of the radius at power 12 and 17% at power 3 — at the top
    //    of that range the lobes read, and at the bottom the plant is a ball of curves
    //    however the rule is tuned. The gain is the one dial that fixes it, it cannot
    //    self-intersect (a radial height field is star-shaped by construction), and it is
    //    applied to the modes as well so the morph stays consistent with the mean.
    static int Bake(string[] a)
    {
        double power = D(a[1]), cx = D(a[2]), cy = D(a[3]), cz = D(a[4]), delta = D(a[5]);
        int w = (int)D(a[6]), h = (int)D(a[7]), degree = (int)D(a[8]), iter = (int)D(a[9]);
        double bail = D(a[10]), sigma = D(a[11]);
        double relief = a.Length > 12 ? D(a[12]) : 1.0;
        Bulb.Mandel = a.Length <= 13 || D(a[13]) != 0.0;

        float[] FitAt(double ax, double ay, double az)
        {
            var raw = Bulb.March(w, h, power, ax, ay, az, iter, bail, 2.2, 0.03);
            var sm = Bulb.Smooth(raw, w, h, sigma);
            return Bulb.Fit(sm, w, h, degree);
        }

        var mean = FitAt(cx, cy, cz);
        var mx = FitAt(cx + delta, cy, cz);
        var my = FitAt(cx, cy + delta, cz);
        var mz = FitAt(cx, cy, cz + delta);
        for (int i = 0; i < mean.Length; i++)
        {
            mx[i] -= mean[i]; my[i] -= mean[i]; mz[i] -= mean[i];
        }
        // Y(0,0) is the constant 1/sqrt(4*pi), so coeff[0]*that IS the mean radius.
        double meanRadius = mean[0] / Math.Sqrt(4.0 * Math.PI);
        double inv = 1.0 / Math.Max(meanRadius, 1e-9);
        for (int i = 0; i < mean.Length; i++)
        {
            double g = (i == 0 ? 1.0 : relief) * inv;
            mean[i] = (float)(mean[i] * g); mx[i] = (float)(mx[i] * g);
            my[i] = (float)(my[i] * g); mz[i] = (float)(mz[i] * g);
        }
        Console.WriteLine($"scale {F(meanRadius)} {F(relief)}");
        Emit("mean", mean); Emit("mode0", mx); Emit("mode1", my); Emit("mode2", mz);

        // What the fit actually costs: the reconstruction's error against the field it
        // was fitted to. It does not converge (the class doc says why) and this number is
        // the honest statement of that, not a target.
        var rawRef = Bulb.March(w, h, power, cx, cy, cz, iter, bail, 2.2, 0.03);
        float[] smRef = Bulb.Smooth(rawRef, w, h, sigma);
        var rec = MandelbulbSurface.Reconstruct(degree, mean, w, h);
        double se = 0, sv = 0, m0 = 0;
        // compare against the field the fit saw, put through the same two scales
        var refScaled = new float[smRef.Length];
        for (int i = 0; i < smRef.Length; i++)
            refScaled[i] = (float)((meanRadius + (smRef[i] - meanRadius) * relief) * inv);
        smRef = refScaled;
        foreach (var v in smRef) m0 += v; m0 /= smRef.Length;
        for (int i = 0; i < smRef.Length; i++) { double d = rec[i] - smRef[i]; se += d * d; sv += (smRef[i] - m0) * (smRef[i] - m0); }
        Console.WriteLine($"fiterror {F(Math.Sqrt(se / smRef.Length))} {F(1.0 - se / Math.Max(sv, 1e-12))}");
        Stats(rec, out double rmin, out double rmax, out double rmean, out double rstd);
        Console.WriteLine($"recon {F(rmin)} {F(rmax)} {F(rmean)} {F(rstd)}");
        return 0;
    }

    static void Emit(string tag, float[] v)
    {
        var sb = new System.Text.StringBuilder(tag);
        foreach (var x in v) { sb.Append(' '); sb.Append(((double)x).ToString("R", Inv)); }
        Console.WriteLine(sb.ToString());
    }

    // grow <inputFile>
    //   grid    <w> <h> <degree>
    //   basis   <n> <mean...> <mode0...> <mode1...> <mode2...>
    //   weights <w0> <w1> <w2>
    //   rules   <field> <swirl> <fieldMix> <momentum> <step> <maxSteps> <lanes> <laneGap>
    //           <hopSeek> <hopJitter> <seeds> <seedSpread> <maxTurn> <rMin> <rMax>
    //           <minRun> <lengthFactor> <girthTaper> <twistDegreesPerStep>
    //   seed    <s>
    //   budget  <n>
    static int Grow(string[] a)
    {
        int gw = 0, gh = 0, degree = 0, seed = 1, budget = 0;
        float[][] basis = null;
        float w0 = 0, w1 = 0, w2 = 0;
        var rules = new MandelbulbSurface.GrowthRules();

        foreach (var raw in File.ReadAllLines(a[1]))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            switch (t[0])
            {
                case "grid": gw = (int)D(t[1]); gh = (int)D(t[2]); degree = (int)D(t[3]); break;
                case "weights": w0 = (float)D(t[1]); w1 = (float)D(t[2]); w2 = (float)D(t[3]); break;
                case "seed": seed = (int)D(t[1]); break;
                case "budget": budget = (int)D(t[1]); break;
                case "basis":
                {
                    int n = (int)D(t[1]);
                    basis = new float[4][];
                    int k = 2;
                    for (int b = 0; b < 4; b++)
                    {
                        basis[b] = new float[n];
                        for (int i = 0; i < n; i++) basis[b][i] = (float)D(t[k++]);
                    }
                    break;
                }
                case "rules":
                    rules.Field = (MandelbulbSurface.SteeringField)(int)D(t[1]);
                    rules.SwirlDegrees = (float)D(t[2]);
                    rules.FieldMix = (float)D(t[3]);
                    rules.Momentum = (float)D(t[4]);
                    rules.StepSize = (float)D(t[5]);
                    rules.MaxSteps = (int)D(t[6]);
                    rules.LanesPerSeed = (int)D(t[7]);
                    rules.LaneGap = (float)D(t[8]);
                    rules.HopSeek = (float)D(t[9]);
                    rules.HopJitter = (float)D(t[10]);
                    rules.SeedCount = (int)D(t[11]);
                    rules.SeedSpreadDegrees = (float)D(t[12]);
                    rules.MaxTurnDegrees = (float)D(t[13]);
                    rules.RadiusMin = (float)D(t[14]);
                    rules.RadiusMax = (float)D(t[15]);
                    rules.MinRun = (int)D(t[16]);
                    rules.LengthFactor = (float)D(t[17]);
                    rules.GirthTaper = t.Length > 18 ? (float)D(t[18]) : 1f;
                    rules.TwistDegreesPerStep = t.Length > 19 ? (float)D(t[19]) : 0f;
                    rules.DiveCount = t.Length > 20 ? (int)D(t[20]) : 0;
                    rules.DiveStepFraction = t.Length > 21 ? (float)D(t[21]) : 0f;
                    rules.DiveAngleDegrees = t.Length > 22 ? (float)D(t[22]) : 0f;
                    rules.DiveStopRadius = t.Length > 23 ? (float)D(t[23]) : 0f;
                    rules.DiveMaxSteps = t.Length > 24 ? (int)D(t[24]) : 0;
                    rules.DiveSwirlDegrees = t.Length > 25 ? (float)D(t[25]) : 0f;
                    rules.DiveStrideCeiling = t.Length > 26 ? (float)D(t[26]) : 0f;
                    rules.DiveGirthFloor = t.Length > 27 ? (float)D(t[27]) : 0f;
                    rules.DiveAxisAlign = t.Length > 28 ? (float)D(t[28]) : 0f;
                    rules.DiveDescent = t.Length > 29 ? (float)D(t[29]) : 0f;
                    rules.SkeletonSeeds = t.Length > 30 ? (int)D(t[30]) : 0;
                    rules.WalkStep = t.Length > 31 ? (float)D(t[31]) : 0f;
                    rules.MinPersistence = t.Length > 32 ? (float)D(t[32]) : 0f;
                    rules.GirthReference = t.Length > 33 ? (float)D(t[33]) : 0f;
                    rules.GasketLevels = t.Length > 34 ? (int)D(t[34]) : 0;
                    rules.DiscSeeds = t.Length > 35 ? (int)D(t[35]) : 0;
                    rules.DiscPad = t.Length > 36 ? (float)D(t[36]) : 0f;
                    rules.DiscMinRadius = t.Length > 37 ? (float)D(t[37]) : 0f;
                    rules.RingShrink = t.Length > 38 ? (float)D(t[38]) : 0f;
                    rules.RingFlatten = t.Length > 39 ? (float)D(t[39]) : 0f;
                    rules.RingGirthExponent = t.Length > 40 ? (float)D(t[40]) : 0f;
                    rules.RingSamples = t.Length > 41 ? (int)D(t[41]) : 0;
                    rules.GasketOctave = t.Length > 42 ? (float)D(t[42]) : 0f;
                    rules.DiscRelaxRate = t.Length > 43 ? (float)D(t[43]) : 0f;
                    rules.RingGirthFloor = t.Length > 44 ? (float)D(t[44]) : 0f;
                    break;
            }
        }

        var coeffs = MandelbulbSurface.Compose(basis, w0, w1, w2);
        var field = MandelbulbSurface.Reconstruct(degree, coeffs, gw, gh);
        var surface = new MandelbulbSurface.Surface(field, gw, gh);
        var growth = new MandelbulbSurface.Growth(surface, rules, seed);

        Console.WriteLine($"surface {F(surface.MeanRadius)} {growth.SeedCount}");
        var frame = new MandelbulbSurface.Frame();
        int laid = 0;
        while (laid < budget && growth.TryNext(out var addr))
        {
            MandelbulbSurface.Pose(surface, addr, ref frame, out var p, out var f, out var u);
            Console.WriteLine(string.Join(" ", new[]
            {
                "p",
                F(addr.Theta), F(addr.Phi), F(addr.RadialOffset), F(addr.Dive), F(addr.TanA), F(addr.TanB), F(addr.TanR),
                F(addr.Length), F(addr.Girth), F(addr.Roll),
                addr.Curve.ToString(Inv), addr.Lane.ToString(Inv),
                F(p.x), F(p.y), F(p.z), F(f.x), F(f.y), F(f.z), F(u.x), F(u.y), F(u.z)
            }));
            laid++;
        }
        Console.WriteLine($"done {laid} {growth.CurvesTraced} {growth.DivesSpent}");
        return 0;
    }

    // shipped <element> <w0> <w1> <w2> <gridW> <seed> <budget> <rules...>
    //
    // Grows straight off MandelbulbSurfaceTables — the SHIPPED basis, the SHIPPED growth rule —
    // so the model has something to be proven against that is the game rather than a copy of it.
    static int Shipped(string[] a)
    {
        var element = (CosmicShore.Data.Element)Enum.Parse(typeof(CosmicShore.Data.Element), a[1], true);
        float w0 = (float)D(a[2]), w1 = (float)D(a[3]), w2 = (float)D(a[4]);
        int gw = (int)D(a[5]), gh = gw / 2;
        int seed = (int)D(a[6]), budget = (int)D(a[7]);

        var rules = ParseRules(a, 8);

        var basis = MandelbulbSurfaceTables.For(element);
        var coeffs = MandelbulbSurface.Compose(basis, w0, w1, w2);
        var field = MandelbulbSurface.Reconstruct(MandelbulbSurfaceTables.Degree, coeffs, gw, gh);
        var surface = new MandelbulbSurface.Surface(field, gw, gh);
        var growth = new MandelbulbSurface.Growth(surface, rules, seed);

        Console.WriteLine($"surface {F(surface.MeanRadius)} {growth.SeedCount}");
        var frame = new MandelbulbSurface.Frame();
        int laid = 0;
        while (laid < budget && growth.TryNext(out var addr))
        {
            MandelbulbSurface.Pose(surface, addr, ref frame, out var p, out var f, out var u);
            Console.WriteLine(string.Join(" ", new[]
            {
                "p",
                F(addr.Theta), F(addr.Phi), F(addr.RadialOffset), F(addr.Dive), F(addr.TanA), F(addr.TanB), F(addr.TanR),
                F(addr.Length), F(addr.Girth), F(addr.Roll),
                addr.Curve.ToString(Inv), addr.Lane.ToString(Inv),
                F(p.x), F(p.y), F(p.z), F(f.x), F(f.y), F(f.z), F(u.x), F(u.y), F(u.z)
            }));
            laid++;
        }
        Console.WriteLine($"done {laid} {growth.CurvesTraced} {growth.DivesSpent}");
        return 0;
    }

    // The 44 growth-rule columns, in the ONE order the wire format has: `o` is the index of
    // `Field`. Extracted so `shipped` and `gasket` cannot drift — two hand-written copies of a
    // 44-column positional parser is a transcription error waiting for the 45th column.
    static MandelbulbSurface.GrowthRules ParseRules(string[] a, int o)
    {
        float G(int i, float dflt = 0f) => a.Length > o + i ? (float)D(a[o + i]) : dflt;
        return new MandelbulbSurface.GrowthRules
        {
            Field = (MandelbulbSurface.SteeringField)(int)G(0),
            SwirlDegrees = G(1),
            FieldMix = G(2),
            Momentum = G(3),
            StepSize = G(4),
            MaxSteps = (int)G(5),
            LanesPerSeed = (int)G(6),
            LaneGap = G(7),
            HopSeek = G(8),
            HopJitter = G(9),
            SeedCount = (int)G(10),
            SeedSpreadDegrees = G(11),
            MaxTurnDegrees = G(12),
            RadiusMin = G(13),
            RadiusMax = G(14),
            MinRun = (int)G(15),
            LengthFactor = G(16),
            GirthTaper = G(17),
            TwistDegreesPerStep = G(18),
            DiveCount = (int)G(19),
            DiveStepFraction = G(20),
            DiveAngleDegrees = G(21),
            DiveStopRadius = G(22),
            DiveMaxSteps = (int)G(23),
            DiveSwirlDegrees = G(24),
            DiveStrideCeiling = G(25),
            DiveGirthFloor = G(26),
            DiveAxisAlign = G(27),
            DiveDescent = G(28),
            SkeletonSeeds = (int)G(29),
            WalkStep = G(30),
            MinPersistence = G(31),
            GirthReference = G(32),
            GasketLevels = (int)G(33),
            DiscSeeds = (int)G(34),
            DiscPad = G(35),
            DiscMinRadius = G(36),
            RingShrink = G(37),
            RingFlatten = G(38),
            RingGirthExponent = G(39),
            RingSamples = (int)G(40),
            GasketOctave = G(41),
            DiscRelaxRate = G(42),
            RingGirthFloor = G(43),
        };
    }

    // gasket <element> <w0> <w1> <w2> <gridW> <rules...>
    //
    // APOLLONIA's disc set, straight out of the shipped Growth. The set is the species —
    // the prism stream is one ring per disc in lay order — but it is NOT recoverable from
    // that stream: a ring's ρ can be back-solved from its prism centres only after
    // `RingFlatten` and `RingShrink` have already moved them, and a disc's LEVEL is not
    // written into any address field at all (the Lane is the size OCTAVE, deliberately not
    // the recursion level). So this verb REFLECTS over Growth's private `_discs`,
    // `_discOrder`, `_rhoRef` and `_ringSamples` rather than re-deriving them.
    //
    // Reflection inside the harness is the honest choice here: the alternative is either a
    // public accessor on shipped gameplay code that only a test reads, or an inference chain
    // that could hide the very disagreement the verb exists to find. The harness is the only
    // caller, it never ships, and reading a private field cannot perturb what it measures.
    //
    //   disc <index> <level> <lane> <rho> <ax> <ay> <az>     (disc-index order)
    //   order <i0> <i1> ...                                  (the lay order, ρ descending)
    //   ring <N> <rhoRef>
    static int Gasket(string[] a)
    {
        var element = (CosmicShore.Data.Element)Enum.Parse(typeof(CosmicShore.Data.Element), a[1], true);
        int gw = (int)D(a[5]), gh = gw / 2;
        var basis = MandelbulbSurfaceTables.For(element);
        var coeffs = MandelbulbSurface.Compose(basis, (float)D(a[2]), (float)D(a[3]), (float)D(a[4]));
        var field = MandelbulbSurface.Reconstruct(MandelbulbSurfaceTables.Degree, coeffs, gw, gh);
        var surface = new MandelbulbSurface.Surface(field, gw, gh);
        var rules = ParseRules(a, 6);
        var growth = new MandelbulbSurface.Growth(surface, rules, 1);
        // The gasket builds LAZILY (the peak census must never run on the planting frame),
        // so nothing below exists until something asks. SeedCount is that ask.
        int seedCount = growth.SeedCount;

        const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var gt = growth.GetType();
        var fDiscs = gt.GetField("_discs", BF);
        var fOrder = gt.GetField("_discOrder", BF);
        var fRhoRef = gt.GetField("_rhoRef", BF);
        var fSamples = gt.GetField("_ringSamples", BF);
        if (fDiscs == null || fOrder == null || fRhoRef == null || fSamples == null)
        {
            // A renamed field must be LOUD: a silently empty table would read as "this plant
            // has no discs", which every gate below would then happily agree with.
            Console.Error.WriteLine("gasket: Growth no longer carries _discs/_discOrder/_rhoRef/_ringSamples");
            return 2;
        }
        var discs = (IList)fDiscs.GetValue(growth);
        var order = (int[])fOrder.GetValue(growth);
        float rhoRef = (float)fRhoRef.GetValue(growth);
        int samples = (int)fSamples.GetValue(growth);

        FieldInfo fAxis = null, fRho = null, fLevel = null, fLane = null;
        for (int i = 0; i < discs.Count; i++)
        {
            var d = discs[i];
            if (fAxis == null)
            {
                var dt = d.GetType();
                fAxis = dt.GetField("Axis", BF); fRho = dt.GetField("Rho", BF);
                fLevel = dt.GetField("Level", BF); fLane = dt.GetField("Lane", BF);
                if (fAxis == null || fRho == null || fLevel == null || fLane == null)
                {
                    Console.Error.WriteLine("gasket: Disc no longer carries Axis/Rho/Level/Lane");
                    return 2;
                }
            }
            var ax = (Vector3)fAxis.GetValue(d);
            Console.WriteLine(string.Join(" ", new[]
            {
                "disc", i.ToString(Inv), ((int)fLevel.GetValue(d)).ToString(Inv),
                ((int)fLane.GetValue(d)).ToString(Inv), F((float)fRho.GetValue(d)),
                F(ax.x), F(ax.y), F(ax.z)
            }));
        }
        var sb = new System.Text.StringBuilder("order");
        for (int i = 0; i < order.Length; i++) { sb.Append(' '); sb.Append(order[i].ToString(Inv)); }
        Console.WriteLine(sb.ToString());
        Console.WriteLine($"ring {samples.ToString(Inv)} {F(rhoRef)}");
        Console.WriteLine($"gasket {discs.Count.ToString(Inv)} {seedCount.ToString(Inv)}");
        return 0;
    }

    // crit <element> <w0> <w1> <w2> <gridW>
    // The surface's critical points as the SHIPPED detector finds them: `crit <kind> <theta>
    // <phi> <radius> <sharpness> <ev.x> <ev.y> <er.x> <er.y>` in scan order, then `saddle <i>`
    // rows giving the farthest-point ORDER as indices into that list, then `peak <i>` rows.
    static int Crit(string[] a)
    {
        var element = (CosmicShore.Data.Element)Enum.Parse(typeof(CosmicShore.Data.Element), a[1], true);
        int gw = (int)D(a[5]), gh = gw / 2;
        var basis = MandelbulbSurfaceTables.For(element);
        var coeffs = MandelbulbSurface.Compose(basis, (float)D(a[2]), (float)D(a[3]), (float)D(a[4]));
        var field = MandelbulbSurface.Reconstruct(MandelbulbSurfaceTables.Degree, coeffs, gw, gh);
        var surface = new MandelbulbSurface.Surface(field, gw, gh);
        var all = surface.CriticalPoints();
        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i];
            Console.WriteLine(string.Join(" ", new[] { "crit", ((int)c.Kind).ToString(Inv), F(c.Theta), F(c.Phi),
                F(c.Radius), F(c.Sharpness), F(c.ValleyX), F(c.ValleyY), F(c.RidgeX), F(c.RidgeY) }));
        }
        var order = surface.Saddles();
        for (int i = 0; i < order.Count; i++)
        {
            int idx = -1;
            for (int q = 0; q < all.Count; q++) if (ReferenceEquals(all[q], order[i])) { idx = q; break; }
            Console.WriteLine($"saddle {idx.ToString(Inv)}");
        }
        // ... then `peak <i>` rows: the PEAKS in farthest-point order (the gasket's level 0).
        var peaks = surface.Peaks();
        for (int i = 0; i < peaks.Count; i++)
        {
            int idx = -1;
            for (int q = 0; q < all.Count; q++) if (ReferenceEquals(all[q], peaks[i])) { idx = q; break; }
            Console.WriteLine($"peak {idx.ToString(Inv)}");
        }
        return 0;
    }

    // posetable <element> <gridW>
    //
    // Pose is the one PURE function on a prism's own data, and the FALL is the first thing
    // to exercise it off the surface — Dive lifts a prism off its own ray and TanR sends it
    // through the branch that hangs `up` off the ray instead of the normal. Neither is
    // reachable from the `shipped` verb's statistics, so they get a table of their own: a
    // fixed sweep of addresses spanning Dive in [-0.05, 0.97] and TanR in [-0.996, 0.996],
    // with TanA/TanB completing a unit heading and both roll states, posed against the
    // SHIPPED surface at weights 0.
    //
    // Each row prints the ADDRESS IT USED as well as the pose. That is deliberate and it is
    // the whole reason this can be held to 1e-5: the caller re-poses the identical float32
    // inputs (the "R" format round-trips a float exactly through a double) instead of trying
    // to reproduce this table's arithmetic in another language, where 0.996f and 0.996 are
    // different numbers and the disagreement would be about the TABLE rather than about Pose.
    //   pt <theta> <phi> <off> <dive> <tanA> <tanB> <tanR> <roll> <radius> <p.xyz> <f.xyz> <u.xyz>
    static int PoseTable(string[] a)
    {
        var element = (CosmicShore.Data.Element)Enum.Parse(typeof(CosmicShore.Data.Element), a[1], true);
        int gw = (int)D(a[2]), gh = gw / 2;
        var basis = MandelbulbSurfaceTables.For(element);
        var coeffs = MandelbulbSurface.Compose(basis, 0f, 0f, 0f);
        var field = MandelbulbSurface.Reconstruct(MandelbulbSurfaceTables.Degree, coeffs, gw, gh);
        var surface = new MandelbulbSurface.Surface(field, gw, gh);

        // The thetas stay off the poles, where the (eTheta, ePhi) chart is singular. That is a
        // property of the CHART and not of Pose, so sampling there would measure the frame's
        // 1e-4 sinTheta floor rather than the thing under test.
        float[] thetas = { 0.35f, 0.9f, 1.5707964f, 2.2f, 2.85f };
        float[] phis = { 0.2f, 1.7f, 3.4f, 5.6f };
        float[] dives = { -0.05f, 0f, 0.2f, 0.55f, 0.85f, 0.97f };
        // 0 is in the list on purpose: TanR == 0 is the SURFACE branch, so one table covers
        // both arms and a mutation that deletes either one has nowhere to hide.
        float[] tanRs = { -0.996f, -0.55f, -0.08f, 0f, 0.08f, 0.55f, 0.996f };
        float[] rolls = { 0f, 0.9f };
        float[] offs = { -0.03f, 0f, 0.05f };

        var frame = new MandelbulbSurface.Frame();
        int row = 0;
        foreach (var th in thetas)
            foreach (var ph in phis)
                foreach (var dv in dives)
                    foreach (var tr in tanRs)
                        foreach (var rl in rolls)
                        {
                            // The tangential part is whatever is left of a UNIT heading once
                            // TanR has taken its share, swung around the tangent plane row by
                            // row so the table is not four repetitions of one direction.
                            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - tr * tr));
                            float ang = 0.37f * row;
                            float ta = s * Mathf.Cos(ang), tb = s * Mathf.Sin(ang);
                            float off = offs[row % offs.Length];
                            var addr = new MandelbulbSurface.PrismAddress(th, ph, off, dv, ta, tb, tr,
                                                                          1f, 1f, rl, 0, 0);
                            MandelbulbSurface.Pose(surface, addr, ref frame, out var p, out var f, out var u);
                            Console.WriteLine(string.Join(" ", new[]
                            {
                                "pt",
                                F(addr.Theta), F(addr.Phi), F(addr.RadialOffset), F(addr.Dive),
                                F(addr.TanA), F(addr.TanB), F(addr.TanR), F(addr.Roll),
                                F(frame.Radius),
                                F(p.x), F(p.y), F(p.z), F(f.x), F(f.y), F(f.z), F(u.x), F(u.y), F(u.z)
                            }));
                            row++;
                        }
        Console.WriteLine($"posetable {row}");
        return 0;
    }

    // field <element> <w0> <w1> <w2> <gridW>
    // The reconstructed height field itself, row by row. This is the one PURE function in the
    // chain -- it cannot amplify a rounding difference -- so it is the part the offline model is
    // held to exactly, where the sequential walk can only be held to its statistics.
    static int Field(string[] a)
    {
        var element = (CosmicShore.Data.Element)Enum.Parse(typeof(CosmicShore.Data.Element), a[1], true);
        int gw = (int)D(a[5]), gh = gw / 2;
        var basis = MandelbulbSurfaceTables.For(element);
        var coeffs = MandelbulbSurface.Compose(basis, (float)D(a[2]), (float)D(a[3]), (float)D(a[4]));
        var field = MandelbulbSurface.Reconstruct(MandelbulbSurfaceTables.Degree, coeffs, gw, gh);
        Console.WriteLine($"grid {gw} {gh}");
        var sb = new System.Text.StringBuilder();
        for (int j = 0; j < gh; j++)
        {
            sb.Clear(); sb.Append('r');
            for (int i = 0; i < gw; i++) { sb.Append(' '); sb.Append(F(field[j * gw + i])); }
            Console.WriteLine(sb.ToString());
        }
        return 0;
    }

    // Negative controls for the two conventions the whole species rests on.
    static int SelfTest()
    {
        int degree = 8, w = 128, h = 64;
        int fail = 0;

        // 1. Reconstruct(Y(l,m)) must be that harmonic, and Fit must return it unchanged.
        foreach (var (l, m) in new[] { (0, 0), (1, 0), (2, 0), (2, 1), (3, -2), (5, 4) })
        {
            var c = new float[(degree + 1) * (degree + 1)];
            c[l * (l + 1) + m] = 1f;
            var f = MandelbulbSurface.Reconstruct(degree, c, w, h);
            var back = Bulb.Fit(f, w, h, degree);
            double worst = 0;
            for (int i = 0; i < c.Length; i++) worst = Math.Max(worst, Math.Abs(back[i] - c[i]));
            Console.WriteLine($"roundtrip {l} {m} {F(worst)}");
            if (worst > 5e-3) fail++;
        }

        // 2. Compose is linear in the weights.
        var basis = new float[4][];
        var rng = new Random(7);
        for (int b = 0; b < 4; b++)
        {
            basis[b] = new float[(degree + 1) * (degree + 1)];
            for (int i = 0; i < basis[b].Length; i++) basis[b][i] = (float)(rng.NextDouble() - 0.5);
        }
        var a1 = MandelbulbSurface.Compose(basis, 0.3f, -0.2f, 0.7f);
        var a2 = MandelbulbSurface.Compose(basis, 0.6f, -0.4f, 1.4f);
        double lin = 0;
        for (int i = 0; i < a1.Length; i++)
            lin = Math.Max(lin, Math.Abs((a2[i] - basis[0][i]) - 2f * (a1[i] - basis[0][i])));
        Console.WriteLine($"linear {F(lin)}");
        if (lin > 1e-5) fail++;

        // 3. Pose must invert the address: a prism placed from (theta,phi,offset) must sit
        //    on that ray at that radius. This is the RADIAL-lift claim.
        var fieldC = new float[(degree + 1) * (degree + 1)];
        fieldC[0] = 3.5f; fieldC[2 * 3 + 1] = 0.4f; fieldC[5] = -0.3f;
        var surf = new MandelbulbSurface.Surface(MandelbulbSurface.Reconstruct(degree, fieldC, w, h), w, h);
        var fr = new MandelbulbSurface.Frame();
        double worstPose = 0;
        for (int k = 0; k < 200; k++)
        {
            float th = (float)(0.05 + rng.NextDouble() * (Math.PI - 0.1));
            float ph = (float)(rng.NextDouble() * 2 * Math.PI);
            float off = (float)(rng.NextDouble() - 0.5);
            var addr = new MandelbulbSurface.PrismAddress(th, ph, off, 0f, 1f, 0f, 0f, 1f, 1f, 0f, 0, 0);
            MandelbulbSurface.Pose(surf, addr, ref fr, out var p, out var fwd, out var up);
            float r = surf.Sample(th, ph) + off;
            var want = new Vector3(
                (float)(Math.Sin(th) * Math.Cos(ph)) * r,
                (float)(Math.Sin(th) * Math.Sin(ph)) * r,
                (float)Math.Cos(th) * r);
            worstPose = Math.Max(worstPose, (p - want).magnitude);
            worstPose = Math.Max(worstPose, Math.Abs(Vector3.Dot(fwd, up)));
            worstPose = Math.Max(worstPose, Math.Abs(fwd.magnitude - 1f));
        }
        Console.WriteLine($"pose {F(worstPose)}");
        if (worstPose > 1e-3) fail++;

        Console.WriteLine($"selftest {(fail == 0 ? "ok" : "FAIL")} {fail}");
        return fail == 0 ? 0 : 1;
    }
}
