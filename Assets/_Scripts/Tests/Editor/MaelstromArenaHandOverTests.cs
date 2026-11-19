#if UNITY_EDITOR
using System;
using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Maelstrom hub hands the arena it previewed to the prism pool when a round launches, so the round
    /// re-issues those prisms instead of cloning the same arena a second time.
    ///
    /// WHY THIS MATTERS: a Maelstrom round used to lay its arena twice - once as the hub's preview, once as the
    /// round - 13-15 thousand prisms each, with "Prisms reused from the environment pool 0" both times, and each
    /// lay is a long frame on a host whose Relay link dies after about 10 s of silence. One of those lays froze a
    /// host for 72 s and ended the tournament for everyone (Docs/Reports/MAELSTROM_MULTIPLAYER_RUNS.md finding 1;
    /// Docs/PartySystem/BUGS.md B45). The hand-over itself is a coroutine on a live Cell and cannot run in edit
    /// mode, so these tests hold the launch-hold rule and the wires the hand-over depends on; the drain is
    /// in the verification checklist.
    /// </summary>
    [TestFixture]
    public class MaelstromArenaHandOverTests
    {
        const string CellPath = "Assets/_Scripts/Controller/Environment/Cell.cs";
        const string LobbyPath = "Assets/_Scripts/Controller/Arcade/Maelstrom/MaelstromLobby.cs";
        const string ViewPath = "Assets/_Scripts/Controller/Arcade/Maelstrom/MaelstromSceneView.cs";
        const string PreviewPath = "Assets/_Scripts/Controller/Arcade/Maelstrom/MaelstromPreviewHost.cs";

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path);
        }

        /// <summary>The text of <paramref name="signature"/>'s member, up to the next member at the same indent.</summary>
        static string Body(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, $"'{signature}' not found.");
            int end = source.IndexOf("\n        }\n", start, StringComparison.Ordinal);
            if (end < 0) end = source.IndexOf("\n        }\r\n", start, StringComparison.Ordinal);
            Assert.Greater(end, start, $"end of '{signature}' not found.");
            return source.Substring(start, end - start);
        }

        // ── The host's launch hold ─────────────────────────────────────────

        [Test]
        public void Launch_GoesAtOnce_WhenNothingIsBeingHandedOver() =>
            Assert.IsTrue(MaelstromLobby.ShouldLaunchNow(handOverRunning: false, now: 10f, deadline: 14f),
                "a hub with no preview must launch exactly as it did before");

        [Test]
        public void Launch_Waits_WhileTheHandOverRuns() =>
            Assert.IsFalse(MaelstromLobby.ShouldLaunchNow(handOverRunning: true, now: 10f, deadline: 14f));

        [Test]
        public void Launch_GoesAtTheDeadline_EvenIfTheHandOverIsStillRunning()
        {
            Assert.IsTrue(MaelstromLobby.ShouldLaunchNow(handOverRunning: true, now: 14f, deadline: 14f),
                "a stalled hand-over must never hold the party on the launch sheet");
            Assert.IsTrue(MaelstromLobby.ShouldLaunchNow(handOverRunning: true, now: 20f, deadline: 14f));
        }

        [Test]
        public void Launch_WithAZeroHold_NeverWaits() =>
            Assert.IsTrue(MaelstromLobby.ShouldLaunchNow(handOverRunning: true, now: 10f, deadline: 10f));

        // ── The wires ──────────────────────────────────────────────────────

        [Test]
        public void Cell_SceneExitRetire_ParksInThePool_AndBuildsNothing()
        {
            string routine = Body(Read(CellPath), "IEnumerator RetireForSceneExitRoutine(");
            Assert.IsTrue(routine.Contains("ReturnRetiredTrailToPool("),
                "pooled trail must go back to ITS pool; destroying it corrupts that pool's accounting");
            Assert.IsTrue(routine.Contains("ReleaseRetiredWorld("),
                "the environment must be released to EnvironmentPrismPool, a slice per frame");
            Assert.IsTrue(routine.Contains("ResetBooksAfterRetire()"));
            Assert.IsFalse(routine.Contains("BuildEnvironmentNow"), "a scene exit stands no new world");
            Assert.IsFalse(routine.Contains("Destroy(retiring)"),
                "the drain owns the root; a second Destroy here would kill prisms before they are parked");
        }

        [Test]
        public void Cell_SceneExitRetire_RefusesDuringASwap_AndASwapRefusesDuringIt()
        {
            string cell = Read(CellPath);
            string entry = Body(cell, "public bool RetireWorldIntoPoolForSceneExit(");
            Assert.IsTrue(entry.Contains("_swapping || _retiringForSceneExit"),
                "a world still being laid cannot be handed over");
            string swap = Body(cell, "public bool RequestCellSwap(");
            Assert.IsTrue(swap.Contains("_retiringForSceneExit"),
                "a swap started mid hand-over would lay into a world that is being parked");
        }

        [Test]
        public void EveryPeer_StartsTheHandOver_AtTheCountdownsZero()
        {
            string refresh = Body(Read(ViewPath), "void RefreshCountdown()");
            Assert.IsTrue(refresh.Contains("ParkArenaForRound()"),
                "each machine lays its own arena, so each must park its own preview; the view runs on all of them");
        }

        [Test]
        public void Host_HoldsTheLoad_BehindTheHandOver()
        {
            string lobby = Read(LobbyPath);
            int park = lobby.IndexOf("ParkArenaForRound()", StringComparison.Ordinal);
            int hold = lobby.IndexOf("ShouldLaunchNow(handOverRunning", StringComparison.Ordinal);
            int launch = lobby.IndexOf("tc.BeginNextRound();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(park, 0);
            Assert.Greater(hold, park, "the hold must follow the hand-over's start");
            Assert.Greater(launch, hold, "BeginNextRound (the scene load) must sit behind the hold");
            Assert.AreEqual(launch, lobby.LastIndexOf("tc.BeginNextRound();", StringComparison.Ordinal),
                "one launch site: a second, unheld BeginNextRound would unload the hub mid hand-over");
        }

        [Test]
        public void Preview_WaitsForABuildInFlight_ThenEasesTheWorldAway()
        {
            string park = Body(Read(PreviewPath), "async UniTaskVoid ParkArenaAsync(");
            int wait = park.IndexOf("parking.IsSwappingConfig", StringComparison.Ordinal);
            int retire = park.IndexOf("RetireWorldIntoPoolForSceneExit(parkSliceMs, parkSuctionSeconds)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(wait, 0,
                "everyone readying at once snaps the countdown to 3 s, usually before the preview is laid: " +
                "without the wait the common launch hands nothing over");
            Assert.Greater(retire, wait, "the retire must follow the wait and pass the suction: nothing opaque covers the hub");
            Assert.IsTrue(park.Contains("finally"), "IsParkingArena must clear on every exit, or the host waits the full limit");
        }

        [Test]
        public void Cell_SceneExitRetire_RetiresLifeWithTheWorld_AndCannotLatchItsFlag()
        {
            string routine = Body(Read(CellPath), "IEnumerator RetireForSceneExitRoutine(");
            Assert.IsTrue(routine.Contains("RetireWithWorld()"),
                "the root is destroyed with the scene still loaded: without this every plant runs its death path");
            Assert.IsTrue(routine.Contains("leaveSpawnedNetworkLifeForms: true"),
                "Netcode refuses to parent a spawned NetworkObject under a plain root, one exception per creature");
            Assert.IsTrue(routine.Contains("finally"), "a step that throws must not leave the cell refusing every later swap");
        }

        [Test]
        public void Cell_Drain_ReleasesNewestFirst()
        {
            string drain = Body(Read(CellPath), "IEnumerator ReleaseRetiredWorld(");
            Assert.IsTrue(drain.Contains("for (int i = prisms.Length - 1; i >= 0; i--)"),
                "detaching from the front of one container shifts every later sibling: the square of the count (B36)");
        }
    }
}
#endif
