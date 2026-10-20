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
              "  ... and the enum's serialized values are Shipped=0, Tuned=1 (a silent cell plays the field default, Tuned)");
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

        // The networked eject (ElementalTransfer.ApplyAllAuthoritative): only the shooter's owner
        // relays, every other replay moves nothing, and the settled count survives the packing
        // that carries it to the other peers. The NotOurs row is the double-take control: route
        // it Local and a victim hit by a human would pay once per peer that replayed the shot.
        Console.WriteLine("T9  a networked eject is settled once, by the shooter's owner, on the victim's owner");
        var ej = ElementalTransferForm.Eject;
        Check(ElementalTransfer.RouteFor(ej, false, false, false) == ElementalTransferRoute.Local, "offline settles locally");
        Check(ElementalTransfer.RouteFor(ej, true, true, false) == ElementalTransferRoute.Local, "a victim with no relay settles locally");
        Check(ElementalTransfer.RouteFor(ej, true, true, true) == ElementalTransferRoute.Relay, "the shooter's owner relays to the victim's owner");
        Check(ElementalTransfer.RouteFor(ej, true, false, true) == ElementalTransferRoute.NotOurs, "a replay of somebody else's shot moves nothing");
        Check(ElementalTransfer.RouteFor(ElementalTransferForm.Burn, true, false, true) == ElementalTransferRoute.Local,
              "  ... and a burn is never relayed (it settles where it ran, as before)");
        uint pk = ElementalTransfer.PackPetals(1, 0, 3, 15);
        Check(ElementalTransfer.PetalsIn(pk, Element.Charge) == 1 && ElementalTransfer.PetalsIn(pk, Element.Mass) == 0 &&
              ElementalTransfer.PetalsIn(pk, Element.Space) == 3 && ElementalTransfer.PetalsIn(pk, Element.Time) == 15,
              "settled counts round-trip through the packing");
        Check(ElementalTransfer.TotalPetals(pk) == 19, "  ... and sum to what settled");
        Check(ElementalTransfer.PackPetals(0, 0, 0, 0) == 0u, "nothing settled packs to 0 (publish nothing)");
        uint ov = ElementalTransfer.PackPetals(-4, 300, 0, 0);
        Check(ElementalTransfer.PetalsIn(ov, Element.Charge) == 0 && ElementalTransfer.PetalsIn(ov, Element.Mass) == 255 &&
              ElementalTransfer.PetalsIn(ov, Element.Space) == 0, "a count clamps to its own byte and never bleeds into the next element");
        Check(ElementalTransfer.ApplyAllAuthoritative(ej, null, null, 0.3f, UnityEngine.Vector3.zero,
                                                      ElementalDebuffSources.Other) == 0,
              "a null victim settles nothing on the local route");

        // The callers #1007 left settling on every peer (the sniper strip, the blast debuff, the
        // joust/sword steal) now go through ApplyAuthoritative with the attacker passed. This drives
        // the REAL routing against fake hulls carrying a fake relay, so what is asserted is what
        // ElementalTransfer does with a hull's relay, not a re-statement of the table.
        Console.WriteLine("T10 the steal and the element subset are settled once, on the right machines");
        var st = ElementalTransferForm.Steal;
        Check(ElementalTransfer.RouteFor(st, true, true, true) == ElementalTransferRoute.Relay,
              "the thief's owner relays a steal to the victim's owner");
        Check(ElementalTransfer.RouteFor(st, true, false, true) == ElementalTransferRoute.NotOurs,
              "  ... and a replay of somebody else's joust moves nothing");
        Check(ElementalTransfer.RouteFor(ElementalTransferForm.Burn, true, true, true) == ElementalTransferRoute.Local,
              "a burn has no attacker to own it and settles where it ran");

        Check(ElementalTransfer.DecidedHere(false, false) && ElementalTransfer.DecidedHere(true, true) &&
              !ElementalTransfer.DecidedHere(true, false),
              "a hit is decided (and scored) offline or on the shooter's owner, nowhere else");
        Check(ElementalTransfer.IsDecidedHere(null), "  ... and an anonymous attacker is decided where it ran");

        Check(ElementalTransfer.MaskOf(new[] { Element.Mass, Element.Space }) == 0b0110,
              "the Manta bomb's authored Mass+Space list is bits 1 and 2");
        Check(ElementalTransfer.MaskOf(new[] { Element.Mass, Element.Mass, Element.Omni, Element.None }) == 0b0010,
              "  ... duplicates collapse and Omni/None drop, so a list cannot take an element twice");
        Check(ElementalTransfer.MaskOf((Element[])null) == 0, "  ... and a null list is the empty mask");

        // Machine A owns the shooter; the victim is owned elsewhere.
        var victimRelay = new FakeRelay { Networked = true, Owned = false };
        var shooterRelay = new FakeRelay { Networked = true, Owned = true };
        var victimHull = FakeStatus.On(victimRelay);
        var shooterHull = FakeStatus.On(shooterRelay);
        int here = ElementalTransfer.ApplyAuthoritative(st, victimHull, shooterHull, ElementalTransfer.AllElementsMask,
                                                        0.5f, UnityEngine.Vector3.zero, ElementalDebuffSources.VesselContact);
        Check(here == 0 && victimRelay.Takes == 1 && victimRelay.LastForm == st && victimRelay.LastPayee == shooterRelay,
              "the thief's owner hands the steal to the victim's relay, naming itself as the payee");
        Check(victimHull.ResourceSystem.Asked[(int)Element.Mass] == 0 && shooterHull.ResourceSystem.Granted[(int)Element.Mass] == 0,
              "  ... and touches neither copy of the levels itself");

        // Machine B: the same contact, replayed by a peer that does not own the shooter.
        var replayShooter = FakeStatus.On(new FakeRelay { Networked = true, Owned = false });
        var replayVictimRelay = new FakeRelay { Networked = true, Owned = true };
        var replayVictim = FakeStatus.On(replayVictimRelay);
        replayVictim.ResourceSystem.Settles[(int)Element.Charge] = 3;
        int moved = ElementalTransfer.ApplyAuthoritative(st, replayVictim, replayShooter, ElementalTransfer.AllElementsMask,
                                                         0.5f, UnityEngine.Vector3.zero, ElementalDebuffSources.VesselContact);
        Check(moved == 0 && replayVictimRelay.Takes == 0 && replayVictim.ResourceSystem.Asked[(int)Element.Charge] == 0,
              "a replay on the victim's own machine moves nothing (the thief's owner will send it)");

        // The victim's owner settles: only the masked elements are asked, and a steal pays.
        var owned = FakeStatus.On(new FakeRelay { Networked = true, Owned = true });
        owned.ResourceSystem.Settles[(int)Element.Mass] = 2;
        owned.ResourceSystem.Settles[(int)Element.Space] = 1;
        owned.ResourceSystem.Settles[(int)Element.Charge] = 9;   // must never be asked under the Manta mask
        uint settled = ElementalTransfer.SettleTake(owned, 0b0110, 0.12f, ElementalDebuffSources.Explosion);
        Check(owned.ResourceSystem.Asked[(int)Element.Charge] == 0 && owned.ResourceSystem.Asked[(int)Element.Time] == 0 &&
              owned.ResourceSystem.Asked[(int)Element.Mass] == 1 && owned.ResourceSystem.Asked[(int)Element.Space] == 1,
              "the owner asks only the masked elements (Mass, Space), once each");
        Check(ElementalTransfer.PetalsIn(settled, Element.Mass) == 2 && ElementalTransfer.PetalsIn(settled, Element.Space) == 1 &&
              ElementalTransfer.TotalPetals(settled) == 3, "  ... and packs exactly what came loose");
        var thief = FakeStatus.On(new FakeRelay { Networked = true, Owned = true });
        ElementalTransfer.GrantSettled(thief, settled);
        Check(thief.ResourceSystem.Granted[(int)Element.Mass] == 2 && thief.ResourceSystem.Granted[(int)Element.Space] == 1 &&
              thief.ResourceSystem.Granted[(int)Element.Charge] == 0,
              "the thief's owner is granted exactly the settled petals (conservation across two machines)");

        // Offline: no relay on either hull, so the steal settles and pays right here, as before.
        var offVictim = FakeStatus.On(null);
        var offThief = FakeStatus.On(null);
        offVictim.ResourceSystem.Settles[(int)Element.Time] = 1;
        int off = ElementalTransfer.ApplyAuthoritative(st, offVictim, offThief, ElementalTransfer.AllElementsMask,
                                                       0.5f, UnityEngine.Vector3.zero, ElementalDebuffSources.VesselContact);
        Check(off == 1 && offThief.ResourceSystem.Granted[(int)Element.Time] == 1 &&
              offVictim.ResourceSystem.Asked[(int)Element.Charge] == 1,
              "offline the steal settles and pays locally, on all four elements, exactly as before");
        Check(ElementalTransfer.ApplyAuthoritative(ej, offVictim, offThief, 0, 0.5f, UnityEngine.Vector3.zero,
                                                   ElementalDebuffSources.Other) == 0,
              "an empty mask takes nothing");

        T11Silence();

        Console.WriteLine(_fail == 0 ? "\nALL PASS" : $"\n{_fail} FAILED");
        return _fail == 0 ? 0 : 1;
    }

    // SILENCE (Docs/ElementalAbilitySystem/SILENCE.md): every SCORED HIT silences its victim for what
    // it is worth (ten points to the petal, on four elements) when CombatHitDrain.TryAdmit admits it -
    // whether or not the victim holds petals (Garrett, after a Dog Fight run where a fight between
    // fresh pilots silenced nobody). A silenced attacker is refused and takes nothing. Driven against
    // the REAL CombatHitDrain / VesselCombatHitLatch / ElementalTransfer / VesselSilence on the config
    // SO's own defaults (1 s per petal, 8 s ceiling, Explosion | VesselContact | Other).
    static void T11Silence()
    {
        Console.WriteLine("T11 a scored hit silences its victim for what it is worth; a silenced attacker takes nothing");
        double now = 100d;
        VesselSilence.Clock = () => now;
        VesselSilence.ClearAll();

        // The pure rules.
        Check(Math.Abs(VesselSilence.SecondsForPoints(1, 1f) - 0.4f) < 1e-5f &&
              Math.Abs(VesselSilence.SecondsForPoints(8, 1f) - 3.2f) < 1e-5f &&
              Math.Abs(VesselSilence.SecondsForPoints(10, 1f) - 4f) < 1e-5f,
              "a hit is worth its points at ten to the petal on four elements: bullet 0.4 s, Strike 3.2 s, shockwave 4 s");
        Check(VesselSilence.SecondsForPoints(0, 1f) == 0f && VesselSilence.SecondsForPoints(-5, 1f) == 0f &&
              VesselSilence.SecondsForPoints(10, 0f) == 0f, "  ... and a worthless hit, or a zero rate, buys nothing");
        Check(VesselSilence.Extend(2f, 3f, 8f) == 5f, "a new hit ADDS to the silence left");
        Check(VesselSilence.Extend(6f, 5f, 8f) == 8f, "  ... up to the ceiling");
        Check(VesselSilence.Extend(-1f, 2f, 8f) == 2f, "  ... and an expired window counts as none");
        Check(VesselSilence.Extend(1f, 2f, 0f) == 0f, "a zero ceiling switches silence off");
        var src = ElementalDebuffSources.Explosion | ElementalDebuffSources.VesselContact | ElementalDebuffSources.Other;
        Check(VesselSilence.Silences(ElementalDebuffSources.VesselContact, src) &&
              !VesselSilence.Silences(ElementalDebuffSources.DangerPrism, src) &&
              !VesselSilence.Silences(ElementalDebuffSources.WormholeToll, src) &&
              !VesselSilence.Silences(ElementalDebuffSources.None, src),
              "contact, blast and gun hits silence; a danger prism, the toll and an unclassed hit do not");

        // THE DOG FIGHT CASE: a fresh pilot holding nothing is hit by a Strike and is silenced anyway.
        var thief = FakeStatus.On(null);
        var fresh = FakeStatus.On(null);
        fresh.Domain = Domains.Ruby;
        Check(CombatHitDrain.TryAdmit(fresh, thief, CombatHitClass.Strike, 0f, ElementalDebuffSources.VesselContact, out _),
              "a Strike on a pilot with no petals is admitted (it scores)");
        Check(Math.Abs(VesselSilence.RemainingSeconds(fresh.PlayerName) - 3.2f) < 1e-4f,
              $"  ... and silences them for its worth, 3.2 s, though they had nothing to take ({VesselSilence.RemainingSeconds(fresh.PlayerName)})");
        Check(!VesselSilence.IsSilenced(thief.PlayerName), "  ... and the attacker is not silenced");

        // The silenced pilot can no longer take or score; anybody can still hit them.
        var bystander = FakeStatus.On(null);
        bystander.ResourceSystem.Settles[(int)Element.Charge] = 4;
        int back = ElementalTransfer.ApplyAuthoritative(ElementalTransferForm.Steal, bystander, fresh,
            ElementalTransfer.AllElementsMask, 0.5f, UnityEngine.Vector3.zero, ElementalDebuffSources.VesselContact);
        Check(back == 0 && bystander.ResourceSystem.Asked[(int)Element.Charge] == 0 &&
              fresh.ResourceSystem.Granted[(int)Element.Charge] == 0,
              "a silenced pilot's steal asks nothing of its victim and is paid nothing");
        Check(ElementalTransfer.Apply(ElementalTransferForm.Eject, bystander, fresh, Element.Charge, 0.5f,
                                      UnityEngine.Vector3.zero, ElementalDebuffSources.Other) == 0,
              "  ... nor does a silenced pilot's ranged strip");
        Check(!CombatHitDrain.TryAdmit(bystander, fresh, CombatHitClass.Bullet, 0f, ElementalDebuffSources.Other, out _) &&
              !VesselSilence.IsSilenced(bystander.PlayerName),
              "  ... and TryAdmit refuses the silenced pilot's hit: no score, and no silence on its target");

        // A burn never silences, and is never refused for an attacker's silence.
        var burnt = FakeStatus.On(null);
        burnt.ResourceSystem.Settles[(int)Element.Space] = 2;
        ElementalTransfer.Apply(ElementalTransferForm.Burn, burnt, null, Element.Space, 0.2f,
                                UnityEngine.Vector3.zero, ElementalDebuffSources.DangerPrism);
        Check(!VesselSilence.IsSilenced(burnt.PlayerName), "a danger-prism burn never silences");

        // A take, however big, no longer silences on its own: silence is the admitted hit's.
        var robbed = FakeStatus.On(null);
        robbed.ResourceSystem.Settles[(int)Element.Mass] = 3;
        Check(ElementalTransfer.ApplyAuthoritative(ElementalTransferForm.Steal, robbed, FakeStatus.On(null),
                  ElementalTransfer.AllElementsMask, 0.5f, UnityEngine.Vector3.zero, ElementalDebuffSources.VesselContact) == 3 &&
              !VesselSilence.IsSilenced(robbed.PlayerName),
              "a take that skipped TryAdmit moves its petals and silences nobody (one hit, one silence)");

        // A warded victim is not admitted, so not silenced.
        var warded = FakeStatus.On(null);
        warded.ResourceSystem.Ward = ElementalDebuffSources.Other;
        Check(!CombatHitDrain.TryAdmit(warded, FakeStatus.On(null), CombatHitClass.Bullet, 0f, ElementalDebuffSources.Other, out _) &&
              !VesselSilence.IsSilenced(warded.PlayerName), "a warded pilot is neither scored on nor silenced");

        // The toll is a hit that scores but does not silence.
        var payer = FakeStatus.On(null);
        Check(CombatHitDrain.TryAdmit(payer, FakeStatus.On(null), CombatHitClass.Debuff, 0f, ElementalDebuffSources.WormholeToll, out _) &&
              !VesselSilence.IsSilenced(payer.PlayerName), "the wormhole toll scores but silences nobody");

        // Protects: the silenced pilot's hits deflect off everything but their own domain's mass.
        Check(VesselSilence.Protects(fresh.PlayerName, Domains.Jade) &&
              !VesselSilence.Protects(fresh.PlayerName, Domains.Ruby) &&
              !VesselSilence.Protects(thief.PlayerName, Domains.Jade),
              "a silenced pilot's hit is refused on rival mass, allowed on their own, and nobody else is touched");
        Check(VesselSilence.SuppressesDanger(fresh.PlayerName) && !VesselSilence.SuppressesDanger(thief.PlayerName),
              "a silenced pilot lays no new danger; an unsilenced one does");

        // A missile that upgrades its tier adds only the difference, as its score does.
        var shooter = FakeStatus.On(null);
        var target = FakeStatus.On(null);
        CombatHitDrain.TryAdmit(target, shooter, CombatHitClass.MissileShockwave, 5f, ElementalDebuffSources.Explosion, out _);
        now += 3.0;   // 4 s - 3 s = 1 s left
        bool up = CombatHitDrain.TryAdmit(target, shooter, CombatHitClass.MissileBlast, 5f, ElementalDebuffSources.Explosion, out int sup);
        Check(up && sup > 0 && Math.Abs(VesselSilence.RemainingSeconds(target.PlayerName) - 5f) < 1e-3f,
              $"shockwave then blast: 1 s left + the 10-point difference (4 s) = 5 s, not 1 + 8 ({VesselSilence.RemainingSeconds(target.PlayerName)})");

        // Stacking to the ceiling, and expiry.
        CombatHitDrain.TryAdmit(fresh, FakeStatus.On(null), CombatHitClass.MissileDirect, 0f, ElementalDebuffSources.Explosion, out _);
        Check(Math.Abs(VesselSilence.RemainingSeconds(fresh.PlayerName) - 8f) < 1e-4f, "a direct rocket on top clamps to the 8 s ceiling");
        now += 7.9;
        Check(VesselSilence.IsSilenced(fresh.PlayerName), "  ... still silenced at 7.9 s");
        now += 0.2;
        Check(!VesselSilence.IsSilenced(fresh.PlayerName) && !VesselSilence.Protects(fresh.PlayerName, Domains.Jade),
              "  ... and free at 8.1 s, with every gate open again");

        // Networked: the hit's decider extends the window here and sends the SAME extension to every
        // other peer through the victim's relay - even though it does not own the victim.
        var victimRelay = new FakeRelay { Networked = true, Owned = false };
        var netVictim = FakeStatus.On(victimRelay);
        var netShooter = FakeStatus.On(new FakeRelay { Networked = true, Owned = true });
        CombatHitDrain.TryAdmit(netVictim, netShooter, CombatHitClass.MissileShockwave, 0f, ElementalDebuffSources.Explosion, out _);
        Check(victimRelay.Publishes == 1 && Math.Abs(victimRelay.LastPublished - 4f) < 1e-4f &&
              VesselSilence.IsSilenced(netVictim.PlayerName),
              "the decider silences the victim here and publishes the 4 s extension once, though it does not own them");
        var peerCopy = FakeStatus.On(new FakeRelay { Networked = true, Owned = true });
        VesselSilence.ExtendBy(peerCopy, 2.5f);
        VesselSilence.ExtendBy(peerCopy, 1f);
        Check(Math.Abs(VesselSilence.RemainingSeconds(peerCopy.PlayerName) - 3.5f) < 1e-4f,
              "a peer receiving two attackers' extensions counts both (2.5 + 1 = 3.5 s)");
        VesselSilence.Lift(peerCopy.PlayerName);
        Check(!VesselSilence.IsSilenced(peerCopy.PlayerName), "a turn reset lifts the silence");

        // The tally the `silence` console command prints.
        var empty = FakeStatus.On(null);
        CombatHitDrain.Apply(empty, FakeStatus.On(null), CombatHitClass.MissileBlast, 0, UnityEngine.Vector3.zero,
                             ElementalDebuffSources.Explosion);
        Check(VesselSilence.Tallies.TryGetValue(empty.PlayerName, out var et) && et.Takes == 1 && et.EmptyTakes == 1 &&
              et.Petals == 0, "a take that found nothing is counted as an empty take");
        Check(VesselSilence.Tallies.TryGetValue(fresh.PlayerName, out var ft) && ft.Hits == 2 && ft.Silences == 1,
              $"the fresh pilot's tally reads 2 hits that silenced, in 1 window ({ft.Hits}, {ft.Silences})");

        VesselSilence.ClearAll();
    }
}

// A hull's relay as ElementalTransfer sees it: two facts and two calls, recorded. It is also the
// hull's silence relay (NetworkVesselImpactor implements both), so a publish is recorded too.
class FakeRelay : IElementalLossRelay, ISilenceRelay
{
    public int Publishes;
    public float LastPublished;
    public void PublishSilence(float seconds) { Publishes++; LastPublished = seconds; }
    public bool Networked, Owned;
    public int Takes, Grants;
    public ElementalTransferForm LastForm;
    public IElementalLossRelay LastPayee;
    public bool IsNetworked => Networked;
    public bool IsOwnedHere => Networked && Owned;
    public int RelayTakeToOwner(ElementalTransferForm form, int elementMask, float normalizedAmountPerElement,
                                UnityEngine.Vector3 impactVelocity, ElementalDebuffSources source, IElementalLossRelay payee)
    {
        Takes++; LastForm = form; LastPayee = payee;
        return 0;
    }
    public void RelayGrantToOwner(uint packedPetals) { Grants++; }
}

class FakeVessel : IVessel
{
    public UnityEngine.Transform Transform { get; set; }
    public IVesselStatus VesselStatus { get; set; }
}

// A pilot whose hull carries the given relay (null = a hull with none: offline / a mini hull).
class FakeStatus : IVesselStatus
{
    public IVessel Vessel { get; set; }
    // Every fake is a different pilot: silence is keyed by player name, so two fakes sharing one
    // would silence each other.
    static int s_next;
    public string PlayerName { get; } = "fake" + (++s_next);
    public Domains Domain { get; set; }
    public ResourceSystem ResourceSystem { get; } = new ResourceSystem();
    public UnityEngine.Vector3 Course { get; set; }
    public float Speed { get; set; }

    public static FakeStatus On(FakeRelay relay)
    {
        var s = new FakeStatus();
        s.Vessel = new FakeVessel { Transform = new UnityEngine.Transform { Attached = relay }, VesselStatus = s };
        return s;
    }
}
