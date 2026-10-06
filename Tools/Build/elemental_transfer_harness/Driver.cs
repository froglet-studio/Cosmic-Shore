// Exercises the transfer arithmetic the shipped C# cannot be asked about in the editor: the
// conserving take (whole petals only, clamped to what is held), and the class -> form rule.
// It re-implements AccrueElementalLoss's arithmetic against the SAME constants the shipped file
// declares, then asserts the invariants the design rests on.
using System;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;

static class Driver
{
    const float Petal = 0.1f;
    static int _fail;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) _fail++;
    }

    // Mirror of ResourceSystem.AccrueElementalLoss over a single element.
    class Pot
    {
        public float Held;       // base level, normalized
        public float Pending;
        public Pot(float held) { Held = held; }

        public int Accrue(float amount)
        {
            if (amount <= 0f) return 0;
            const float Eps = 1e-4f;
            float takeable = Math.Max(0f, Held - 0f);
            if (takeable + Eps < Petal) { Pending = 0f; return 0; }

            Pending += Math.Min(amount, takeable);
            int petals = (int)Math.Floor((Pending + Eps) / Petal);
            int cap = (int)Math.Floor((takeable + Eps) / Petal);
            if (petals > cap) petals = cap;
            if (petals <= 0) return 0;

            Pending = Math.Max(0f, Pending - petals * Petal);
            Held -= petals * Petal;
            return petals;
        }
    }

    static float AssetFloat(string asset, string key)
    {
        var m = System.Text.RegularExpressions.Regex.Match(asset, "^  " + key + @": (-?[0-9.]+)\s*$",
                                                           System.Text.RegularExpressions.RegexOptions.Multiline);
        if (!m.Success) { Check(false, $"effect asset has no {key}"); return float.NaN; }
        return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    static int Main()
    {
        Console.WriteLine("T1  nothing partial ever leaves");
        var p = new Pot(1.0f);                       // level 10
        Check(p.Accrue(0.01f) == 0, "one bullet (0.1 petal) takes nothing yet");
        Check(Math.Abs(p.Held - 1.0f) < 1e-5, "  ... and the level has not moved");
        int got = 0;
        for (int i = 0; i < 9; i++) got += p.Accrue(0.01f);
        Check(got == 1, "the tenth bullet settles exactly one petal");
        Check(Math.Abs(p.Held - 0.9f) < 1e-5, "  ... and the level stepped exactly one level");

        Console.WriteLine("T2  ten cheap hits == one dear hit of the same total");
        var cheap = new Pot(1.0f); var dear = new Pot(1.0f);
        int c = 0; for (int i = 0; i < 30; i++) c += cheap.Accrue(0.01f);
        int d = dear.Accrue(0.30f);
        Check(c == d && c == 3, $"both took 3 petals (cheap={c} dear={d})");
        Check(Math.Abs(cheap.Held - dear.Held) < 1e-5, "  ... and both pots agree");

        Console.WriteLine("T3  you cannot take what is not there (conservation)");
        var empty = new Pot(0f);
        Check(empty.Accrue(0.5f) == 0, "a stripped pilot yields nothing to a 5-petal hit");
        Check(Math.Abs(empty.Held) < 1e-5, "  ... and cannot be driven below empty");
        var thin = new Pot(0.2f);                    // holds exactly 2 petals
        Check(thin.Accrue(1.0f) == 2, "a 10-petal hit on a 2-petal pilot takes exactly 2");
        Check(Math.Abs(thin.Held) < 1e-5, "  ... leaving them empty, not negative");

        Console.WriteLine("T4  a float sum of authored magnitudes still settles");
        // 0.01f x 10 is NOT 0.1f in float32; without the epsilon this is the case that hangs.
        var eps = new Pot(1.0f); int e = 0;
        for (int i = 0; i < 10; i++) e += eps.Accrue(0.01f);
        Check(e == 1, "ten 0.01 accruals settle a petal despite float drift");

        Console.WriteLine("T5  the form is a property of the verb");
        Check(ElementalTransfer.FormFor(CombatHitClass.Strike) == ElementalTransferForm.Steal,
              "Strike (contact) steals");
        foreach (var cls in new[] { CombatHitClass.Bullet, CombatHitClass.Debuff,
                                    CombatHitClass.MissileShockwave, CombatHitClass.MissileBlast,
                                    CombatHitClass.MissileDirect })
            Check(ElementalTransfer.FormFor(cls) == ElementalTransferForm.Eject,
                  $"{cls} (ranged) ejects");

        Console.WriteLine("T6  the drain table still prices ten points to the petal");
        Check(Math.Abs(CombatHitDrain.PerElementFor(CombatHitClass.MissileDirect) + 0.30f) < 1e-5,
              "a 30-point direct strike is 3 petals");
        Check(Math.Abs(CombatHitDrain.PerElementFor(CombatHitClass.Bullet) + 0.01f) < 1e-5,
              "a 1-point bullet is 0.1 petal");
        Check(CombatHitDrain.PerElementFor(CombatHitClass.Strike) == 0f,
              "Strike is declined here (authored per weapon)");
        Check(CombatHitDrain.PerElementFor(CombatHitClass.Debuff) == 0f,
              "Debuff is declined here (authored per weapon)");

        // The settle epsilon is 1e-4 normalized - a thousandth of a petal - and it exists so a
        // float32 sum of authored magnitudes (T4) is not left a ULP short forever. This control
        // brackets THAT boundary rather than a value inside it: the first accrual is ten epsilons
        // short and must settle nothing, the second crosses and must settle exactly one. A control
        // picked inside the tolerance (0.0999) passes for the wrong reason and proves nothing.
        Console.WriteLine("T7  negative control - the epsilon brackets one thousandth of a petal");
        var nc = new Pot(1.0f);
        Check(nc.Accrue(0.099f) == 0, "0.99 of a petal (10x the epsilon short) settles nothing");
        Check(Math.Abs(nc.Held - 1.0f) < 1e-5, "  ... and the level has not moved");
        Check(nc.Accrue(0.001f) == 1, "  ... and the crumb that crosses settles exactly one");
        Check(nc.Accrue(0.0f) == 0, "a zero accrual is a no-op");
        Check(new Pot(1.0f).Accrue(-0.5f) == 0, "a negative accrual cannot GRANT a petal");

        // The petal-burn switch (Docs/ELEMENTAL_ECONOMY.md §4.1). The magnitudes come from the
        // SHIPPED asset, through the game's own resolver (PetalBurnRule.cs compiles in), into the
        // same settlement mirror as T1-T7: one hostile contact burns FIVE whole petals per element
        // under Shipped and ONE under Tuned, still clamped to what is held.
        Console.WriteLine("T8  the petal-burn switch - Shipped 5 / Tuned 1 petal per element per contact");
        string asset = File.ReadAllText(Environment.GetEnvironmentVariable("PETAL_BURN_ASSET"));
        float shipped = AssetFloat(asset, "debuffMagnitude");
        float tuned = AssetFloat(asset, "tunedDebuffMagnitude");
        float ms = PetalBurnRules.Magnitude(PetalBurnRule.Shipped, shipped, tuned);
        float mt = PetalBurnRules.Magnitude(PetalBurnRule.Tuned, shipped, tuned);
        Check(ms == shipped && mt == tuned, $"the resolver picks the asset's sizes (shipped {shipped}, tuned {tuned})");
        Check(PetalBurnRules.Magnitude((PetalBurnRule)7, shipped, tuned) == shipped,
              "  ... and an unknown rule falls back to Shipped");
        Check((int)PetalBurnRule.Shipped == 0 && (int)PetalBurnRule.Tuned == 1,
              "  ... and the enum's serialized values are Shipped=0, Tuned=1 (a silent cell plays Shipped)");
        // The effect burns -magnitude on each of the four elements; a start-of-match pilot holds 5.
        int burnedS = 0, burnedT = 0;
        for (int el = 0; el < 4; el++) { burnedS += new Pot(0.5f).Accrue(-ms); burnedT += new Pot(0.5f).Accrue(-mt); }
        Check(burnedS == 20, $"Shipped: one contact on a 5-petal-per-element pilot burns 5 x 4 = {burnedS}");
        Check(burnedT == 4, $"Tuned: one contact burns 1 x 4 = {burnedT}");
        var full = new Pot(1.0f);
        Check(full.Accrue(-ms) == 5 && full.Accrue(-ms) == 5 && Math.Abs(full.Held) < 1e-5,
              "Shipped strips a full 10-petal element in exactly 2 contacts (burn-rules.md)");
        var tp = new Pot(1.0f);
        int contacts = 0;
        while (tp.Held > 1e-5f && contacts < 100) { if (tp.Accrue(-mt) != 1) break; contacts++; }
        Check(contacts == 10, $"Tuned takes exactly one whole petal per contact - 10 contacts to strip a full element ({contacts})");
        var thin3 = new Pot(0.3f);
        Check(thin3.Accrue(-ms) == 3 && Math.Abs(thin3.Held) < 1e-5,
              "Shipped on a 3-petal element is clamped to 3, leaving it empty, not negative");
        var bare = new Pot(0f);
        Check(bare.Accrue(-mt) == 0 && bare.Pending == 0f, "Tuned on an empty element burns nothing and banks nothing");

        Console.WriteLine(_fail == 0 ? "\nALL PASS" : $"\n{_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }
}
