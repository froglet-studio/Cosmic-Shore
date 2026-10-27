using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A replay watched as the match itself (<c>Docs/REPLAYS.md</c> §6.3): WATCH launches the mode's REAL scene
    /// the way the Arcade does, with the recorded intensity, course seed, seats, hulls and domains, and
    /// <see cref="ReplayMatchDirector"/> then flies every real vessel along its recorded path and re-runs its
    /// recorded actions, so the trails, guns, boosts, explosions, prisms and the cell are the game's own.
    ///
    /// <para><b>Why the real scene and not a picture of it.</b> A picture (the earlier ghost-and-box replay)
    /// can only ever show what was recorded. Running the scene shows everything the game draws, with the
    /// recording steering it: positions are recorded rather than re-simulated, because the simulation does not
    /// repeat itself (<c>Docs/THEATER.md</c> §1), and actions are recorded so every ability fires as it did.</para>
    ///
    /// <para>This class only holds what is being watched across the scene load and starts the load; the
    /// director does the watching. Nothing here runs unless a replay is being watched.</para>
    /// </summary>
    public static class ReplayMatch
    {
        /// <summary>The replay being watched, or null.</summary>
        public static TheaterRecording Recording { get; private set; }
        public static ReplaySummary Summary { get; private set; }

        /// <summary>Seconds into the recording the next load fast-forwards to (a skip back reloads the scene).</summary>
        public static float StartAt { get; set; }

        /// <summary>True while a replay is being watched (its scene loading, playing, or reloading for a skip back).</summary>
        public static bool IsActive => Recording != null;

        /// <summary>
        /// Launch <paramref name="card"/>'s scene to watch <paramref name="recording"/>. False (nothing changed)
        /// when the recording has no vessels or there is no card for its mode.
        /// </summary>
        public static bool TryLaunch(TheaterRecording recording, ReplaySummary summary, SO_ArcadeGame card,
                                     GameDataSO gameData, float startAt = 0f)
        {
            if (recording == null || recording.Tracks.Count == 0 || !card || !gameData) return false;

            Recording = recording;
            Summary = summary;
            StartAt = Mathf.Max(0f, startAt);
            Configure(gameData, card);
            gameData.InvokeGameLaunch();
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Replay] launching {card.Mode} to watch {summary?.RecordingFile}");
            return true;
        }

        /// <summary>Load the scene again and fast-forward to <paramref name="seconds"/> (the skip back).</summary>
        public static void Relaunch(GameDataSO gameData, float seconds)
        {
            if (!IsActive || !gameData) return;
            StartAt = Mathf.Clamp(seconds, 0f, Recording.Duration);
            CourseSeedOverride.Forced = Summary != null ? Summary.CourseSeed : 0;
            gameData.InvokeGameLaunch();
        }

        /// <summary>Stop watching: the next match draws its own course and is recorded as usual.</summary>
        public static void Clear()
        {
            Recording = null;
            Summary = null;
            StartAt = 0f;
            CourseSeedOverride.Forced = 0;
        }

        /// <summary>The seat the local player takes: the recording's first human pilot, else its first track.</summary>
        public static int HumanTrack(TheaterRecording recording)
        {
            for (int i = 0; i < recording.Tracks.Count; i++)
                if (!recording.Tracks[i].IsAI) return i;
            return 0;
        }

        /// <summary>What the Arcade's Start writes (<c>ArcadeGameConfigureModal.SyncAllGameDataForLaunch</c>), from the recording.</summary>
        static void Configure(GameDataSO gameData, SO_ArcadeGame card)
        {
            var tracks = Recording.Tracks;
            int human = HumanTrack(Recording);

            gameData.SyncFromArcadeGame(card);
            gameData.IsTraining = false;
            gameData.IsWeeklyChallenge = false;
            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = Mathf.Clamp(Summary != null ? Summary.Intensity : 1, 1, 4);

            var hull = (VesselClassType)tracks[human].VesselType;
            var domain = (Domains)tracks[human].Domain;
            if (gameData.selectedVesselClass != null) gameData.selectedVesselClass.Value = hull;
            // The host never re-reads selectedVesselClass in the game scene: the hull goes on its Player,
            // and the host is the server, so it writes its own domain too.
            if (gameData.LocalPlayer is Player local)
            {
                local.NetDefaultVesselType.Value = hull;
                local.NetDomain.Value = domain;
            }

            var aiVessels = new List<VesselClassType>();
            var aiDomains = new List<Domains>();
            var domains = new HashSet<Domains> { domain };
            for (int i = 0; i < tracks.Count; i++)
            {
                if (i == human) continue;
                aiVessels.Add((VesselClassType)tracks[i].VesselType);
                aiDomains.Add((Domains)tracks[i].Domain);
                domains.Add((Domains)tracks[i].Domain);
            }
            gameData.ConfigurePlayerCounts(tracks.Count, 1);
            gameData.SetRequestedAIVessels(aiVessels);
            gameData.SetRequestedAIDomains(aiDomains);
            gameData.RequestedDomainCount = Mathf.Max(1, domains.Count);

            CourseSeedOverride.Forced = Summary != null ? Summary.CourseSeed : 0;
            MenuReturnCard.Clear();
        }
    }
}
