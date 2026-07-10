// Arc G part 2 — the shared surface a WINDOWED host needs from any steppable round
// handle: world view for the wireframe render pass (players, active crystal, course),
// the per-frame stepping contract, and the end-game readout for the HUD. Implemented
// by HexRaceRoundHandle and CrystalCaptureRoundHandle; the mode host switches drivers
// on GameDataSO.GameMode without knowing a mode's internals.
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using IPlayer = CosmicShore.Gameplay.IPlayer;

namespace CosmicShore.Cli
{
    /// <summary>Extra wireframe scene elements a mode contributes (AstroLeague: ball/goals/arena).</summary>
    public enum RoundSceneMarkerKind { Octahedron = 0, Ring = 1 }

    public readonly struct RoundSceneMarker
    {
        public readonly Vector3 Position;
        public readonly float Size;
        public readonly RoundSceneMarkerKind Kind;
        /// <summary>Ring orientation: the plane normal (Octahedron ignores it).</summary>
        public readonly Vector3 Normal;
        public readonly float R, G, B, A;

        public RoundSceneMarker(Vector3 position, float size, RoundSceneMarkerKind kind,
            Vector3 normal, float r, float g, float b, float a)
        {
            Position = position; Size = size; Kind = kind; Normal = normal;
            R = r; G = g; B = b; A = a;
        }
    }

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

        /// <summary>Per-domain score in the mode's OWN metric (crystals / jousts / goals).</summary>
        int DomainScore(Domains domain);

        /// <summary>Per-player score in the mode's own metric, for the HUD roster.</summary>
        int PlayerScore(IPlayer player);

        /// <summary>What the chase camera looks at (default: the active crystal).</summary>
        Vector3? LookTarget => ActiveCrystal ? ActiveCrystal.transform.position : (Vector3?)null;

        /// <summary>Mode-specific wireframe extras (default: none).</summary>
        IEnumerable<RoundSceneMarker> SceneMarkers => Enumerable.Empty<RoundSceneMarker>();

        /// <summary>One engine frame; true when the round's objective/end condition lands.</summary>
        bool StepFrame();

        /// <summary>Idempotent end-of-stepping (observer detach + frame stamp).</summary>
        void CompleteStepping();

        /// <summary>Score + publish + log standings (no-op unless the round ended).</summary>
        void FinishAndScore();
    }
}
