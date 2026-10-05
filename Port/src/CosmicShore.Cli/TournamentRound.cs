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

        /// <summary>Per-leg crystal target for HexRace / Crystal Capture legs (kept low so each leg is a quick sprint).</summary>
        public int LegCrystalTarget = 6;

        /// <summary>Per-leg joust target for Joust legs (kept low so each leg is a quick sprint).</summary>
        public int LegJoustTarget = 3;

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
    /// as on device. Every drawn leg runs on its REAL headless harness — Skim Race →
    /// <see cref="HexRaceRound"/>, Joust → <see cref="JoustRound"/>, Crystal Capture →
    /// <see cref="CrystalCaptureRound"/> — so the whole shuffle chains three genuine game modes:
    /// the draw/repeat-avoidance/intensity path picks the mode, its ranked <c>ScoreResult</c>s
    /// feed the real fold.
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

                    // Every drawn mode runs its REAL headless harness (see the class doc). The
                    // leg seed comes off the shared seeded RNG, so the whole session is
                    // deterministic per --seed.
                    int legSeed = Random.Range(1, int.MaxValue);
                    var legOutcome = RunLeg(gameData.GameMode, legSeed, options);
                    result.EngineErrors.AddRange(legOutcome.EngineErrors);
                    if (!legOutcome.Finished)
                    {
                        Log($"  ✗ leg {legNumber} did not finish (seed {legSeed}) — aborting the shuffle.");
                        return result;
                    }

                    Log($"  leg ({legOutcome.HarnessName} harness, seed {legSeed}): {legOutcome.Summary}");

                    // The synced, ranked results land on every peer → the network-free fold.
                    gameData.SetResults(legOutcome.Results);
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

        /// <summary>One leg's outcome, normalized across the three mode harnesses for the fold.</summary>
        sealed class LegOutcome
        {
            public bool Finished;
            public string HarnessName = "";
            public string Summary = "";
            public List<ScoreResult> Results = new();
            public List<string> EngineErrors = new();
        }

        /// <summary>
        /// Dispatches the drawn mode to its REAL headless harness: HexRace draw →
        /// <see cref="HexRaceRound"/>, Joust draw → <see cref="JoustRound"/>, Crystal Capture
        /// draw → <see cref="CrystalCaptureRound"/>. Each harness returns the ranked standings
        /// its mode's controller synced (the per-peer <c>GameDataSO.Results</c> payload).
        /// </summary>
        static LegOutcome RunLeg(GameModes mode, int legSeed, TournamentRoundOptions options)
        {
            switch (mode)
            {
                case GameModes.MultiplayerJoust:
                {
                    var leg = JoustRound.Run(new JoustRoundOptions
                    {
                        PlayerCount = options.PlayerCount,
                        Seed = legSeed,
                        JoustTarget = options.LegJoustTarget,
                    });
                    return new LegOutcome
                    {
                        Finished = leg.Finished,
                        HarnessName = "Joust",
                        Summary = $"{leg.WinnerDomain} domain wins in {leg.FinishTime:F2}s " +
                                  $"· {leg.TotalJousts} jousts · winner '{leg.WinnerName}'",
                        Results = leg.Standings.Select(s =>
                            new ScoreResult(s.Rank, s.Name, s.Domain, s.Score, s.ScoreText, s.Secondary)).ToList(),
                        EngineErrors = leg.EngineErrors,
                    };
                }

                case GameModes.MultiplayerCrystalCapture:
                {
                    var leg = CrystalCaptureRound.Run(new CrystalCaptureRoundOptions
                    {
                        PlayerCount = options.PlayerCount,
                        Seed = legSeed,
                        CrystalTarget = options.LegCrystalTarget,
                    });
                    return new LegOutcome
                    {
                        Finished = leg.Finished,
                        HarnessName = "Crystal Capture",
                        Summary = $"{leg.WinnerDomain} domain wins with {leg.WinnerDomainCrystals} crystals " +
                                  $"· {leg.TotalClaims} claims · winner '{leg.WinnerName}'",
                        Results = leg.Standings.Select(s =>
                            new ScoreResult(s.Rank, s.Name, s.Domain, s.Score, s.ScoreText, null)).ToList(),
                        EngineErrors = leg.EngineErrors,
                    };
                }

                default: // GameModes.HexRace (Skim Race)
                {
                    var leg = HexRaceRound.Run(new HexRaceRoundOptions
                    {
                        PlayerCount = options.PlayerCount,
                        Seed = legSeed,
                        CrystalTarget = options.LegCrystalTarget,
                    });
                    return new LegOutcome
                    {
                        Finished = leg.Finished,
                        HarnessName = "HexRace",
                        Summary = $"{leg.WinnerDomain} domain wins in {leg.FinishTime:F2}s " +
                                  $"· {leg.TotalClaims} crystals · winner '{leg.WinnerName}'",
                        Results = leg.Standings.Select(s =>
                            new ScoreResult(s.Rank, s.Name, s.Domain, s.Score, s.ScoreText, s.Secondary)).ToList(),
                        EngineErrors = leg.EngineErrors,
                    };
                }
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
