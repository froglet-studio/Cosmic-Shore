// Arc G part 2 — the shared surface a WINDOWED host needs from any steppable round
// handle: world view for the wireframe render pass (players, active crystal, course),
// the per-frame stepping contract, and the end-game readout for the HUD. Implemented
// by HexRaceRoundHandle and CrystalCaptureRoundHandle; the mode host switches drivers
// on GameDataSO.GameMode without knowing a mode's internals.
using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using IPlayer = CosmicShore.Gameplay.IPlayer;

namespace CosmicShore.Cli
{
    public interface IRoundDriver : IDisposable
    {
        /// <summary>Display name for the HUD title ("HEX RACE", "CRYSTAL CAPTURE").</summary>
        string GameLabel { get; }

        /// <summary>Scoring blurb for the standings block.</summary>
        string ScoringLabel { get; }

        GameDataSO GameData { get; }
        IReadOnlyList<IPlayer> Players { get; }
        Crystal ActiveCrystal { get; }
        Vector3[] Course { get; }
        Element[] CourseElements { get; }
        int CourseIndex { get; }
        int Target { get; }
        int FramesStepped { get; }
        int MaxFrames { get; }

        /// <summary>True once the round clock runs (HexRace: always; capture: after countdown).</summary>
        bool Live { get; }

        /// <summary>Time.time at clock start — HUD t = Time.time − ClockStart while Live.</summary>
        float ClockStart { get; }

        bool Finished { get; }
        string WinnerName { get; }
        Domains WinnerDomain { get; }
        int TotalClaims { get; }
        IEnumerable<(int Rank, string Name, Domains Domain, int Crystals, string ScoreText)> StandingRows { get; }

        /// <summary>One engine frame; true when the round's objective/end condition lands.</summary>
        bool StepFrame();

        /// <summary>Idempotent end-of-stepping (observer detach + frame stamp).</summary>
        void CompleteStepping();

        /// <summary>Score + publish + log standings (no-op unless the round ended).</summary>
        void FinishAndScore();
    }
}
