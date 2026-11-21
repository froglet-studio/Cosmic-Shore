// Headless proof of the cell's controlling-domain resolver (QA-SWARM-ROUND11-13). Compiles the
// SHIPPED Domains.cs + InitialControllingDomain.cs (pure, no Unity) and asserts:
//   C1  solo freestyle, Swarm cell (OpposingLocalPilot), empty nucleus: the cell does NOT seed in
//       the pilot's colour - even after the pilot has laid trail (gameData's volume leader = pilot);
//   C2  the legacy path (Unset) still seeds in the pilot's colour - the defect, reproduced;
//   C3  a real prism-count leader (nucleus claim) beats the authored start;
//   C4  Opposing() never returns the pilot or Blue;
//   C5  ControllingDomain() never returns Blue over every input;
//   C6  Unset is byte-for-byte the pre-change resolver over every input (no other cell changes);
//   C7  peers agree: with the server's starting controller replicated, every client pilot resolves
//       the server's colour (the server decides);
//   C8  the Unity glue: Cell.ControllingDomain / StartingController route through the resolver and
//       CellNetworkSync mirrors + pins StartingController (textual, from the shipped sources).
using System;
using System.IO;
using CosmicShore.Data;

static class Program
{
    static int failures;
    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok) failures++;
    }

    static readonly Domains[] All = { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold };
    static readonly Domains[] Playable = { Domains.Jade, Domains.Ruby, Domains.Gold };
    static readonly InitialControllingDomain[] Rules =
    {
        InitialControllingDomain.Unset, InitialControllingDomain.OpposingLocalPilot,
        InitialControllingDomain.Jade, InitialControllingDomain.Ruby, InitialControllingDomain.Gold,
        (InitialControllingDomain)7, // an unknown value authored by hand
    };
    static readonly float[] Volumes = { 0f, 1f, 500f };

    // The resolver exactly as Cell.ControllingDomain shipped before this change (47f33d37b).
    static Domains Legacy(Domains dominant, Domains volumeLeader, float volume, Domains local)
    {
        if (dominant != Domains.Blue) return dominant;
        if (volumeLeader != Domains.Blue && volume > 0f) return volumeLeader;
        if (local != Domains.Blue) return local;
        return Domains.Jade;
    }

    // What a peer computes: Cell.ControllingDomain's live reads fed to the shipped resolver.
    static Domains Resolve(InitialControllingDomain rule, Domains dominant, Domains volumeLeader, float volume,
        Domains pilot, Domains? replicatedStart = null)
    {
        var start = replicatedStart ?? CellControlRules.StartingController(rule, pilot);
        return CellControlRules.ControllingDomain(dominant, start, volumeLeader, volume, pilot);
    }

    static int Main(string[] args)
    {
        var swarm = InitialControllingDomain.OpposingLocalPilot;
        Console.WriteLine("C1 solo freestyle, Swarm cell, empty nucleus");
        foreach (var pilot in Playable)
        {
            var atSpawn = Resolve(swarm, Domains.Blue, Domains.Jade, 0f, pilot);   // RoundStats empty
            var afterTrail = Resolve(swarm, Domains.Blue, pilot, 500f, pilot);     // pilot laid trail outside
            Check(atSpawn != pilot && afterTrail != pilot,
                $"pilot {pilot}: seeds {atSpawn}, after trail {afterTrail} (hostile)");
        }

        Console.WriteLine("C2 legacy (Unset) reproduces the friendly start");
        foreach (var pilot in Playable)
            Check(Resolve(InitialControllingDomain.Unset, Domains.Blue, pilot, 500f, pilot) == pilot,
                $"pilot {pilot}: Unset seeds the pilot's own colour");

        Console.WriteLine("C3 a nucleus claim beats the authored start");
        foreach (var pilot in Playable)
        foreach (var claimant in Playable)
            Check(Resolve(swarm, claimant, pilot, 500f, pilot) == claimant,
                $"pilot {pilot}, nucleus held by {claimant} -> {claimant}");

        Console.WriteLine("C4 Opposing");
        foreach (var d in All)
        {
            var o = CellControlRules.Opposing(d);
            Check(o != d && o != Domains.Blue, $"Opposing({d}) = {o}");
        }
        Check(CellControlRules.StartingController(InitialControllingDomain.Unset, Domains.Jade) == Domains.Blue, "Unset start = Blue (none)");
        Check(CellControlRules.StartingController((InitialControllingDomain)7, Domains.Jade) == Domains.Blue, "unknown rule start = Blue (none)");
        Check(CellControlRules.StartingController(InitialControllingDomain.Gold, Domains.Gold) == Domains.Gold, "fixed Gold start = Gold, whoever flies");

        int combos = 0, blue = 0, legacyDiff = 0, peerDiff = 0;
        foreach (var rule in Rules)
        foreach (var dominant in All)
        foreach (var leader in All)
        foreach (var vol in Volumes)
        foreach (var pilot in All)
        {
            combos++;
            var r = Resolve(rule, dominant, leader, vol, pilot);
            if (r == Domains.Blue) blue++;
            if ((rule == InitialControllingDomain.Unset || rule == (InitialControllingDomain)7)
                && r != Legacy(dominant, leader, vol, pilot)) legacyDiff++;
            // C7: the server (host pilot = pilot) resolves the start; every client pilot pins it.
            var serverStart = CellControlRules.StartingController(rule, pilot);
            var serverAnswer = Resolve(rule, dominant, leader, vol, pilot);
            // A start of Blue (Unset) is "none": peers then fall back locally exactly as before,
            // so agreement is only claimed for an authored start - the change's scope.
            if (serverStart == Domains.Blue) continue;
            foreach (var clientPilot in All)
                if (Resolve(rule, dominant, leader, vol, clientPilot, serverStart) != serverAnswer) peerDiff++;
        }
        Console.WriteLine("C5-C7 exhaustive over " + combos + " inputs");
        Check(blue == 0, $"never Blue ({blue} Blue answers)");
        Check(legacyDiff == 0, $"Unset / unknown == the pre-change resolver ({legacyDiff} differences)");
        Check(peerDiff == 0, $"authored start: every client pilot resolves the server's colour ({peerDiff} disagreements)");

        Console.WriteLine("C8 Unity glue (shipped sources)");
        if (args.Length >= 2)
        {
            var cell = File.ReadAllText(args[0]);
            var sync = File.ReadAllText(args[1]);
            Check(cell.Contains("CellControlRules.ControllingDomain(") && cell.Contains("dominant, StartingController, top.Team, top.Volume, LocalPilotDomain"),
                "Cell.ControllingDomain feeds the resolver (dominant, start, volume leader, pilot)");
            Check(cell.Contains("CellControlRules.StartingController(rule, LocalPilotDomain)") && cell.Contains("cellConfigData.InitialControllingDomain"),
                "Cell.StartingController resolves the ACTIVE config's authored rule (follows Cell Selector swaps)");
            Check(cell.Contains("if (_replicatedStartingController.HasValue)"), "a client's replicated start wins over its local resolve");
            Check(sync.Contains("var starting = cell.StartingController;") && sync.Contains("_netStartingController.Value = starting"),
                "CellNetworkSync: the server mirrors StartingController");
            Check(sync.Contains("cell.SetReplicatedStartingController(_netStartingController.Value)") && sync.Contains("cell.SetReplicatedStartingController(next)"),
                "CellNetworkSync: clients pin it on spawn (late join) and on change");
        }
        else Check(false, "usage: Program <Cell.cs> <CellNetworkSync.cs>");

        Console.WriteLine(failures == 0 ? "\nOK - controlling-domain resolver" : $"\nFAIL - {failures} check(s)");
        return failures == 0 ? 0 : 1;
    }
}
