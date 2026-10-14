using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

public static partial class Fleet
{
    public record Vessel(string Name, (int, string[])[] Shared, (int, string[])[] Touch, (int, string[])[] Pad);
}

class FakeInput : IInputStatus
{
    public event Action<bool> OnToggleInputPaused { add { } remove { } }
    public bool Paused { get; set; }
    public InputDeviceType ActiveInputDevice { get; set; }
}
class FakePlayer : IPlayer { }
class FakeStatus : IVesselStatus
{
    public IPlayer Player { get; set; } = new FakePlayer();
    public IInputStatus InputStatus { get; set; }
    public bool IsLocalUser { get; set; }
    public bool AutoPilotEnabled => false;
    public string PlayerName => "pilot";
    public readonly List<string> Log = new();
}
class Rec : ShipActionSO
{
    public Rec(string n) { name = n; }
    public override void StartAction(ActionExecutorRegistry e, IVesselStatus s) => ((FakeStatus)s).Log.Add("start " + name);
    public override void StopAction(ActionExecutorRegistry e, IVesselStatus s) => ((FakeStatus)s).Log.Add("stop " + name);
}

/// <summary>One vessel as two machines see it: the OWNER's copy and a PEER's copy (the server or
/// another client). Each copy has its own input status, i.e. its own idea of the pilot's device.</summary>
class Vessel2
{
    public R_VesselActionHandler Owner, Peer;
    public FakeStatus OwnerStatus, PeerStatus;
    public FakeInput OwnerInput, PeerInput;

    public Vessel2(Fleet.Vessel v, InputDeviceType ownerDevice, InputDeviceType peerSees)
    {
        var acts = new Dictionary<string, Rec>();
        Rec A(string n) => acts.TryGetValue(n, out var r) ? r : acts[n] = new Rec(n);
        List<InputEventShipActionMapping> L((int, string[])[] m) => m.Select(e => new InputEventShipActionMapping
            { InputEvent = (InputEvents)e.Item1, ShipActions = e.Item2.Select(A).Cast<ShipActionSO>().ToList() }).ToList();

        R_VesselActionHandler Copy(bool owner, FakeStatus st)
        {
            var h = new R_VesselActionHandler { IsSpawned = true, IsOwner = owner };
            h.HarnessSetMaps(L(v.Shared), L(v.Touch), L(v.Pad), new ScriptableEventAbilityStats());
            h.Initialize(st);
            return h;
        }
        OwnerInput = new FakeInput { ActiveInputDevice = ownerDevice };
        PeerInput = new FakeInput { ActiveInputDevice = peerSees };
        OwnerStatus = new FakeStatus { InputStatus = OwnerInput, IsLocalUser = true };
        PeerStatus = new FakeStatus { InputStatus = PeerInput, IsLocalUser = false };
        Owner = Copy(true, OwnerStatus);
        Peer = Copy(false, PeerStatus);
        Owner.Copies = Peer.Copies = new List<R_VesselActionHandler> { Owner, Peer };
    }
}

static class Driver
{
    static readonly InputDeviceType[] Devices = (InputDeviceType[])Enum.GetValues(typeof(InputDeviceType));

    static string[] Expected(Fleet.Vessel v, InputDeviceType device, int ie)
    {
        var ovr = device == InputDeviceType.Touch ? v.Touch : v.Pad;
        var hit = ovr.Where(e => e.Item1 == ie).SelectMany(e => e.Item2).ToArray();
        if (hit.Length == 0) hit = v.Shared.Where(e => e.Item1 == ie).SelectMany(e => e.Item2).ToArray();
        return hit;
    }

    static int Main()
    {
        Console.WriteLine($"handler RPC signature: ({R_VesselActionHandler.Signature})");
        int pairs = 0, diverged = 0, wrongOnOwner = 0, aiPairs = 0, aiDiverged = 0, holds = 0, stranded = 0;
        var samples = new List<string>();
        var sampled = new HashSet<string>();   // one example per vessel and kind
        foreach (var v in Fleet.All)
        {
            var inputs = v.Shared.Concat(v.Touch).Concat(v.Pad).Select(e => e.Item1).Distinct().OrderBy(i => i).ToArray();
            foreach (var ownerDev in Devices)
            foreach (var peerSees in Devices)
            foreach (var ie in inputs)
            {
                // 1. A human press: the owner's input system raises it, it travels owner -> server -> all.
                var x = new Vessel2(v, ownerDev, peerSees);
                x.Owner.HarnessPress((InputEvents)ie);
                x.Owner.HarnessRelease((InputEvents)ie);
                pairs++;
                var exp = Expected(v, ownerDev, ie);
                var expLog = exp.Select(a => "start " + a).Concat(exp.Select(a => "stop " + a)).ToList();
                if (!x.OwnerStatus.Log.SequenceEqual(expLog)) wrongOnOwner++;
                if (!x.PeerStatus.Log.SequenceEqual(x.OwnerStatus.Log))
                {
                    diverged++;
                    if (ownerDev != peerSees && sampled.Add("diverged " + v.Name))
                        samples.Add($"  {v.Name}: {ownerDev} pilot, peer thinks {peerSees}, input {ie}: owner [{string.Join(", ", x.OwnerStatus.Log)}] / peer [{string.Join(", ", x.PeerStatus.Log)}]");
                }

                // 2. The same press from a server-owned AI (PerformShipControllerActionsReplicated).
                var ai = new Vessel2(v, ownerDev, peerSees);
                ai.Owner.PerformShipControllerActionsReplicated((InputEvents)ie);
                ai.Owner.StopShipControllerActionsReplicated((InputEvents)ie);
                aiPairs++;
                if (!ai.PeerStatus.Log.SequenceEqual(ai.OwnerStatus.Log)) aiDiverged++;
            }

            // 3. The pilot switches device MID-HOLD (picks up a pad, or puts it down) and the
            //    switch has reached every copy before the release. The release must stop what the
            //    press started, on every copy.
            foreach (var from in Devices)
            foreach (var to in Devices)
            foreach (var ie in inputs)
            {
                var started = Expected(v, from, ie);
                if (started.Length == 0) continue;
                var x = new Vessel2(v, from, from);
                x.Owner.HarnessPress((InputEvents)ie);
                x.OwnerInput.ActiveInputDevice = to; x.PeerInput.ActiveInputDevice = to;
                x.Owner.HarnessRelease((InputEvents)ie);
                holds++;
                var want = started.Select(a => "stop " + a).ToList();
                bool ok = x.OwnerStatus.Log.Where(l => l.StartsWith("stop ")).SequenceEqual(want)
                       && x.PeerStatus.Log.Where(l => l.StartsWith("stop ")).SequenceEqual(want);
                if (!ok)
                {
                    stranded++;
                    if (sampled.Add("stranded " + v.Name))
                        samples.Add($"  STRANDED {v.Name}: pressed {ie} on {from}, released on {to}: owner [{string.Join(", ", x.OwnerStatus.Log)}]");
                }
            }
        }
        // 4. The NON-networked path (the legacy single-player spawn: nothing is spawned, a press is
        //    local): the same mid-hold switch, with no RPC to carry anything.
        int localHolds = 0, localStranded = 0, lateHolds = 0, lateWrong = 0;
        foreach (var v in Fleet.All)
        {
            var inputs = v.Shared.Concat(v.Touch).Concat(v.Pad).Select(e => e.Item1).Distinct().ToArray();
            foreach (var from in Devices)
            foreach (var to in Devices)
            foreach (var ie in inputs)
            {
                var started = Expected(v, from, ie);
                if (started.Length == 0) continue;
                var x = new Vessel2(v, from, from);
                x.Owner.IsSpawned = false;
                x.Owner.HarnessPress((InputEvents)ie);
                x.OwnerInput.ActiveInputDevice = to;
                x.Owner.HarnessRelease((InputEvents)ie);
                localHolds++;
                if (!x.OwnerStatus.Log.Where(l => l.StartsWith("stop ")).SequenceEqual(started.Select(a => "stop " + a)))
                    localStranded++;

                // 5. A peer that never ran the press (it joined mid-hold), then the switch, then the
                //    release: the peer has no record, so only what the release CARRIES can tell it
                //    what the press started.
                var y = new Vessel2(v, from, from);
                y.Owner.Copies = new List<R_VesselActionHandler> { y.Owner };
                y.Owner.HarnessPress((InputEvents)ie);
                y.Owner.Copies = y.Peer.Copies = new List<R_VesselActionHandler> { y.Owner, y.Peer };
                y.OwnerInput.ActiveInputDevice = to; y.PeerInput.ActiveInputDevice = to;
                y.Owner.HarnessRelease((InputEvents)ie);
                lateHolds++;
                if (!y.PeerStatus.Log.SequenceEqual(started.Select(a => "stop " + a))) lateWrong++;
            }
        }
        Console.WriteLine($"human presses: {pairs} (vessel x owner device x peer view x input); peer ran something different: {diverged}; owner ran the wrong thing: {wrongOnOwner}");
        Console.WriteLine($"AI replicated presses: {aiPairs}; peer ran something different: {aiDiverged}");
        Console.WriteLine($"mid-hold device switches: {holds}; release failed to stop what the press started: {stranded}");
        Console.WriteLine($"single-machine mid-hold switches: {localHolds}; stranded: {localStranded}");
        Console.WriteLine($"late-joiner releases after a switch: {lateHolds}; peer stopped the wrong thing: {lateWrong}");
        Console.WriteLine($"RPCs routed: {R_VesselActionHandler.PressRpcs} press, {R_VesselActionHandler.ReleaseRpcs} release");
        foreach (var s in samples) Console.WriteLine(s);
        int testsRun = 0, testsFailed = 0;
        foreach (var type in typeof(Driver).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(NUnit.Framework.TestFixtureAttribute), false).Length == 0) continue;
            foreach (var m in type.GetMethods())
            {
                if (m.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Length == 0) continue;
                testsRun++;
                try { m.Invoke(Activator.CreateInstance(type), null); }
                catch (System.Reflection.TargetInvocationException e)
                { testsFailed++; Console.WriteLine($"  FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        }
        Console.WriteLine($"shipped edit-mode tests: {testsRun} run, {testsFailed} failed");
        if (testsRun == 0 && R_VesselActionHandler.Signature.Contains("device"))
        { Console.WriteLine("  no [Test] found - the test file is not in the build"); testsFailed++; }
        return diverged + aiDiverged + stranded + wrongOnOwner + localStranded + lateWrong + testsFailed == 0 ? 0 : 1;
    }
}
