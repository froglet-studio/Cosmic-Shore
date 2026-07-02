using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Random = CosmicShore.Engine.Random;

namespace CosmicShore.Cli
{
    /// <summary>Knobs for one headless Tournament (Maelstrom) session.</summary>
    public sealed class TournamentRoundOptions
    {
        public int PlayerCount = 4;
        public int Seed = 42;

        /// <summary>Per-leg crystal target (kept low so each leg is a quick sprint).</summary>
        public int LegCrystalTarget = 6;

        /// <summary>Lobby-chosen intensity — the per-game ceiling X (each leg draws in [1..X]).</summary>
        public int IntensityCeiling = 2;
    }

    public sealed class TournamentRoundResult
    {
        public bool Finished;
        public Domains WinnerDomain = Domains.Blue;
        public int GamesPlayed;
        public int WinnerPoints;
        public bool ReachedSummary;

        /// <summary>Deterministic line-by-line log: draws, leg results, standings, final summary.</summary>
        public List<string> Transcript = new();

        /// <summary>Error/Exception entries captured from the engine log across all legs (expected empty).</summary>
        public List<string> EngineErrors = new();
    }

    /// <summary>
    /// Headless Tournament (Maelstrom) session through the REAL ported meta systems:
    /// <c>TournamentController</c> (the persistent brain — scene-load-driven phases, host random
    /// draw of mode + intensity, network-free standings fold), <c>TournamentDataSO</c> (the
    /// per-domain {2,1,0} placement fold, race-to-6 / MaxGames-cap <c>IsShuffleComplete</c>),
    /// <c>TournamentStateMachine</c>, and <c>TournamentStandingsFormatter</c>.
    ///
    /// Scene arc note: the port has no scene transitions yet, so each host-driven Single load is
    /// announced through the engine's <c>SceneManager.NotifySceneLoaded</c> port surface — the
    /// controller's verbatim <c>HandleSceneLoaded</c> drives lobby → game → hub → summary exactly
    /// as on device. And because only the HexRace controller chain is ported so far, EVERY drawn
    /// leg (Skim Race, Joust, or Crystal Capture) is simulated by the real headless
    /// <see cref="HexRaceRound"/> — the drawn mode still exercises the real draw/repeat-avoidance/
    /// intensity path, and its ranked <c>ScoreResult</c>s feed the real fold. Swap in the Joust /
    /// Crystal Capture rounds when their controllers port.
    /// </summary>
    public static class TournamentRound
    {
        public static TournamentRoundResult Run(TournamentRoundOptions options, Action<string> liveLog = null)
        {
            options ??= new TournamentRoundOptions();
            var result = new TournamentRoundResult();
            void Log(string line)
            {
                result.Transcript.Add(line);
                liveLog?.Invoke(line);
            }

            SceneManager.ResetSceneLoadedSubscribers();
            NetworkManager.Singleton = null;   // offline single-process → this peer is the host

            try
            {
                // ── the meta stack: GameDataSO + TournamentDataSO + controller ─────
                var gameData = ScriptableObject.CreateInstance<GameDataSO>();
                gameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.OnLaunchGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
                gameData.SelectedIntensity.Value = Mathf.Clamp(options.IntensityCeiling, 1, 4);

                var tournament = ScriptableObject.CreateInstance<TournamentDataSO>();
                tournament.GameQueue = new List<SO_ArcadeGame>
                {
                    MakeCard("Skim Race", "MinigameHexRace", GameModes.HexRace),
                    MakeCard("Joust", "MinigameJoust_Gameplay", GameModes.MultiplayerJoust),
                    MakeCard("Crystal Capture", "MinigameCrystalCaptureMultiplayer_Gameplay", GameModes.MultiplayerCrystalCapture),
                };
                tournament.ModeCard = MakeCard("Maelstrom", "Maelstrom", GameModes.Tournament);
                tournament.OnTournamentStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                tournament.OnGameResultRecorded = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                tournament.OnStandingsChanged = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
                tournament.OnTournamentCompleted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

                var sceneNames = ScriptableObject.CreateInstance<SceneNameListSO>();
                var controller = new TournamentController(gameData, tournament, sceneNames);

                Random.InitState(options.Seed);

                // Enter the Maelstrom lobby: fresh start — reset standings, capture the ceiling,
                // resolve the race-to-N target (no End Game Conditions asset → serialized 6).
                SceneManager.NotifySceneLoaded(tournament.LobbySceneName);
                Log($"LOBBY — {tournament.ModeName}: pool [{string.Join(", ", tournament.GameQueue.Select(g => g.DisplayName))}], " +
                    $"race to {tournament.EffectiveWinTarget} (cap {tournament.MaxGames}), intensity ceiling {tournament.IntensityCeiling}");
                Log("");

                // ── the shuffle: hub → draw → leg → fold → hub … until decided ─────
                while (!tournament.IsShuffleComplete)
                {
                    controller.BeginNextRound();   // host draw: random pool mode + intensity ∈ [1..X]
                    int legNumber = tournament.GamesPlayed + 1;
                    Log($"GAME {legNumber} — drew {tournament.NextGameName} · Intensity {tournament.NextGameIntensity} " +
                        $"(scene {gameData.SceneName})");

                    SceneManager.NotifySceneLoaded(gameData.SceneName);   // party follows the Single load → InGame

                    // Only the HexRace chain is ported — every leg runs the real headless HexRace
                    // round (see the class doc). The leg seed comes off the shared seeded RNG, so
                    // the whole session is deterministic per --seed.
                    int legSeed = Random.Range(1, int.MaxValue);
                    var leg = HexRaceRound.Run(new HexRaceRoundOptions
                    {
                        PlayerCount = options.PlayerCount,
                        Seed = legSeed,
                        CrystalTarget = options.LegCrystalTarget,
                    });
                    result.EngineErrors.AddRange(leg.EngineErrors);
                    if (!leg.Finished)
                    {
                        Log($"  ✗ leg {legNumber} did not finish (seed {legSeed}) — aborting the shuffle.");
                        return result;
                    }

                    Log($"  leg (via HexRace harness, seed {legSeed}): {leg.WinnerDomain} domain wins in {leg.FinishTime:F2}s " +
                        $"· {leg.TotalClaims} crystals · winner '{leg.WinnerName}'");

                    // The synced, ranked results land on every peer → the network-free fold.
                    gameData.SetResults(leg.Standings.Select(s =>
                        new ScoreResult(s.Rank, s.Name, s.Domain, s.Score, s.ScoreText, s.Secondary)));
                    gameData.InvokeMiniGameEnd();   // TournamentController.RecordResults + race-to-6 check

                    var standings = tournament.BuildSortedStandings();
                    Log($"  standings after game {tournament.GamesPlayed}: " + string.Join(" · ",
                        standings.Select(s => $"{s.Domain} {s.TotalPoints}")));
                    Log("");

                    controller.AdvanceToNextGame();                          // Continue → Maelstrom scene
                    SceneManager.NotifySceneLoaded(tournament.LobbySceneName); // hub mid-run, summary once decided
                }

                result.ReachedSummary = controller.IsShowingSummary;
                result.GamesPlayed = tournament.GamesPlayed;

                var finalStandings = tournament.BuildSortedStandings();
                if (finalStandings.Count > 0)
                {
                    result.WinnerDomain = finalStandings[0].Domain;
                    result.WinnerPoints = finalStandings[0].TotalPoints;
                }

                Log("SUMMARY (TournamentStandingsFormatter.FormatFinal):");
                foreach (var line in TournamentStandingsFormatter.FormatFinal(tournament).Split('\n'))
                    Log("  " + line.TrimEnd());

                result.Finished = tournament.IsShuffleComplete && result.ReachedSummary;
                return result;
            }
            finally
            {
                SceneManager.ResetSceneLoadedSubscribers();
                NetworkManager.Singleton = null;
            }
        }

        static SO_ArcadeGame MakeCard(string displayName, string sceneName, GameModes mode)
        {
            var card = ScriptableObject.CreateInstance<SO_ArcadeGame>();
            card.DisplayName = displayName;
            card.SceneName = sceneName;
            card.Mode = mode;
            card.IsMultiplayer = true;
            return card;
        }
    }
}
