using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Player
{
    /// <summary>
    /// The Brood Rush half of <c>--race-judge</c> (<c>"mode": "BroodRush"</c>): matches of the Mass-Space arena game flown
    /// by every seat's SHIPPED AI, in teams, headless. The measure is net control of space (Docs/ElementalLoop/PLAYBOOK.md):
    /// each pilot's volume books as the scoreboard keeps them (VolumeLedger: VC = volume created in its own domain, VD =
    /// volume it took off the opposing domains, lost, and what it still holds, counted from the live prisms at the end),
    /// beside the broods each team scored and how long each held the nucleus claim.
    ///
    /// The host seat cannot be left out of an arcade launch, so it is PARKED: its vessel is held far outside the cell
    /// every frame, where no fauna senses and no nucleus counts, and its books are reported apart ("host") and left out
    /// of every team sum.
    /// </summary>
    internal sealed partial class RaceJudge
    {
        /// <summary>Where the parked host is held, from the cell's centre: far past the membrane and any fauna sense radius.</summary>
        static readonly Vector3 HostParkOffset = new(0f, 40000f, 0f);
        const float SampleEvery = 0.5f;

        void SeatTeams(GameDataSO gameData)
        {
            int teams = _seatTeam.Max() + 1;
            if (teams > GameDataSO.ActiveDomains.Length) throw new InvalidOperationException($"{teams} teams; the game seats {GameDataSO.ActiveDomains.Length}");
            gameData.RequestedDomainCount = Math.Max(2, teams);
            gameData.SetRequestedAIDomains(_seatTeam.Select(t => GameDataSO.ActiveDomains[t]).ToList());
        }

        /// <summary>The parked host's hull: one the card lists that no seat flies, else any hull no seat flies (added to
        /// the card in memory like an off-card seat). Brood Rush seats each hull once, so a host on a seated hull would take
        /// it from its AI.</summary>
        VesselClassType BroodHostHull(SO_ArcadeGame card)
        {
            foreach (var v in card.Vessels)
                if (v != null && !_hulls.Contains(v.Class)) return v.Class;
            foreach (var h in Enum.GetValues<VesselClassType>().Where(h => (int)h > 0 && !_hulls.Contains(h)))
            {
                if (TrainingHost.LoadAsset(_runtime, $"Assets/_SO_Assets/Classes/SO_Class_{h}.asset") is not SO_Vessel vessel) continue;
                card.Vessels.Add(vessel);
                _notes.Add($"host seated off the card: {h}");
                return h;
            }
            throw new InvalidOperationException("no hull is left for the host");
        }

        /// <summary>The live vessel's trail volume multiplier as its prism controller evaluates it now (proves an override reached the spawn).</summary>
        static double? TrailVolumeOf(IPlayer p)
        {
            var pc = p.Vessel?.VesselStatus?.VesselPrismController;
            if (pc == null) return null;
            var f = typeof(VesselPrismController).GetField("trailVolume", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return f?.GetValue(pc) is ElementalFloat ef ? Math.Round(ef.EvaluateLive(p.Vessel.VesselStatus), 3) : null;
        }

        static BroodRushController FindBrood() => CosmicShore.Engine.Object.FindAnyObjectByType<BroodRushController>();

        IEnumerator BroodMatches(GameDataSO gameData, VesselClassType hostHull, int intensity)
        {
            for (int r = 0; r < _seeds.Count; r++)
            {
                int seed = _seeds[r];
                double raceWall = _wall.Elapsed.TotalSeconds;
                BroodRushController controller = null;
                Cell cell = null;
                List<IPlayer> ais = null;
                double deadline = Now + 180;
                while (Now < deadline)
                {
                    if (SceneManager.GetActiveScene()?.name == gameData.SceneName)
                    {
                        controller = FindBrood();
                        cell = CosmicShore.Engine.Object.FindAnyObjectByType<Cell>();
                        ais = gameData.Players.Where(p => p != null && p.IsInitializedAsAI && p.Vessel != null).ToList();
                        if (controller != null && cell != null && ais.Count >= _hulls.Count) break;
                    }
                    yield return 0.25f;
                }
                var race = new JsonObject { ["seed"] = seed, ["mode"] = "teams", ["game"] = _mode, ["intensity"] = intensity, ["host_hull"] = hostHull.ToString() };
                _races.Add(race);
                if (controller == null || cell == null || ais == null || ais.Count < _hulls.Count)
                {
                    race["outcome"] = "scene-not-ready";
                    yield break;
                }
                yield return 1f;
                ApplyControllerOverrides(controller);
                deadline = Now + 120;
                while (!gameData.IsTurnRunning && Now < deadline)
                {
                    controller.OnReadyClicked();
                    double repress = Now + 15;
                    while (!gameData.IsTurnRunning && Now < repress && Now < deadline) yield return 0.25f;
                }
                if (!gameData.IsTurnRunning)
                {
                    race["outcome"] = "not-started";
                    yield break;
                }
                CosmicShore.Engine.Random.InitState(unchecked(seed * 7919 + 17));

                var host = gameData.LocalPlayer;
                // "ai_objective": "nucleus" - an experiment, in memory only: every AI steers for the nucleus while outside it and
                // flies straight on through while inside, the way BroodRushObjectiveProvider guides a player (the shipped mode
                // gives its AIs no objective). Overrides crystal seeking, like any mode's steering hook.
                if (_spec["ai_objective"]?.GetValue<string>() == "nucleus")
                    foreach (var p in ais)
                        if (p.Vessel?.VesselStatus is Component c && c && c.TryGetComponent(out AIPilot pilot))
                        {
                            var tr = p.Vessel.Transform;
                            pilot.SetExternalTargetProvider(() => cell && tr && cell.IsInsideNucleus(tr.position)
                                ? tr.position + tr.forward * 200f
                                : cell ? cell.transform.position : Vector3.zero);
                        }
                var startLevels = ais.Select(p => p.Vessel?.VesselStatus?.ResourceSystem is { } rs
                    ? $"M{rs.GetLevel(Element.Mass)} C{rs.GetLevel(Element.Charge)} S{rs.GetLevel(Element.Space)} T{rs.GetLevel(Element.Time)}"
                    : "?").ToArray();
                var insideS = new float[ais.Count];
                var speedSum = new float[ais.Count];
                int samples = 0;
                var claimS = new Dictionary<string, float>();
                var timeline = new JsonArray();
                var broodAt = new JsonArray();
                var lastBroods = new Dictionary<Domains, int>();
                float start = Time.time, nextSample = 0f;
                string outcome;
                while (true)
                {
                    // Park the host every frame, before anything this frame reads its position.
                    if (host?.Vessel?.Transform is { } ht) ht.position = cell.transform.position + HostParkOffset;
                    float t = Time.time - start;
                    if (t >= nextSample)
                    {
                        nextSample = t + SampleEvery;
                        samples++;
                        for (int k = 0; k < ais.Count; k++)
                        {
                            speedSum[k] += ais[k].Vessel?.VesselStatus?.Speed ?? 0f;
                            if (ais[k].Vessel?.Transform is { } tr && cell.IsInsideNucleus(tr.position)) insideS[k] += SampleEvery;
                        }
                        string claim = cell.TryGetNucleusClaim(out var c) ? c.ToString() : "none";
                        claimS[claim] = (claimS.TryGetValue(claim, out var s0) ? s0 : 0f) + SampleEvery;
                        if (Math.Abs(t % 5f) < SampleEvery) timeline.Add(new JsonArray(Math.Round(t, 1), claim));
                        foreach (var d in GameDataSO.ActiveDomains.Take(gameData.RequestedDomainCount))
                        {
                            int b = ScoringMetrics.SumByDomain(gameData, ScoringMetric.Goals, d);
                            if (b > (lastBroods.TryGetValue(d, out var lb) ? lb : 0)) broodAt.Add(new JsonArray(Math.Round(t, 1), d.ToString()));
                            lastBroods[d] = b;
                        }
                    }
                    if (!gameData.IsTurnRunning) { outcome = "ended"; break; }
                    if (t > _limit) { outcome = "timeout"; break; }
                    yield return null;
                }
                float raceS = Time.time - start;
                // The controller counts the live field at its own end; count again here so a timeout reads the same way.
                var roster = gameData.RoundStatsList.Where(s => s != null).Select(s => s.Name).ToList();
                gameData.VolumeLedger.CountLive(PrismSpatialIndex.Instance, roster);
                var books = gameData.VolumeLedger;

                var broods = new JsonObject();
                foreach (var d in GameDataSO.ActiveDomains.Take(gameData.RequestedDomainCount))
                    broods[d.ToString()] = ScoringMetrics.SumByDomain(gameData, ScoringMetric.Goals, d);
                var seats = new JsonArray();
                for (int k = 0; k < ais.Count; k++)
                {
                    var p = ais[k];
                    var line = books.Get(p.Name);
                    seats.Add(new JsonObject
                    {
                        ["hull"] = (p.Vessel?.VesselStatus?.VesselType ?? VesselClassType.Any).ToString(),
                        ["requested"] = k < _hulls.Count ? _hulls[k].ToString() : null,
                        ["name"] = p.Name,
                        ["domain"] = p.Domain.ToString(),
                        ["vc"] = Math.Round(line.Created, 1),
                        ["vd"] = Math.Round(line.Reduced, 1),
                        ["lost"] = Math.Round(line.Lost, 1),
                        ["held"] = line.HasCount ? Math.Round(line.Counted, 1) : null,
                        ["super_shields"] = line.SuperShieldsBroken,
                        ["nucleus_s"] = Math.Round(insideS[k], 1),
                        ["start_levels"] = startLevels[k],
                        // Where the books came from: the trail and abilities as the round stats count them, and how fast it flew.
                        ["speed"] = Math.Round(samples > 0 ? speedSum[k] / samples : 0f, 1),
                        ["blocks"] = p.RoundStats?.BlocksCreated,
                        ["volume_created"] = p.RoundStats is { } rs0 ? Math.Round(rs0.VolumeCreated, 1) : null,
                        ["stolen"] = p.RoundStats is { } rs1 ? Math.Round(rs1.VolumeStolen, 1) : null,
                        ["destroyed"] = p.RoundStats is { } rs2 ? Math.Round(rs2.HostileVolumeDestroyed, 1) : null,
                        ["crystals"] = p.RoundStats?.CrystalsCollected,
                        ["trail_volume"] = TrailVolumeOf(p),
                        ["ability_s"] = p.RoundStats is { } rs3 ? new JsonArray(Math.Round(rs3.Button1AbilityActiveTime, 1), Math.Round(rs3.Button2AbilityActiveTime, 1),
                            Math.Round(rs3.Button3AbilityActiveTime, 1), Math.Round(rs3.LeftStickAbilityActiveTime, 1), Math.Round(rs3.RightStickAbilityActiveTime, 1),
                            Math.Round(rs3.FullSpeedStraightAbilityActiveTime, 1)) : null,
                    });
                }
                var hostLine = host != null ? books.Get(host.Name) : default;
                race["host"] = new JsonObject
                {
                    ["name"] = host?.Name, ["domain"] = host?.Domain.ToString(),
                    ["vc"] = Math.Round(hostLine.Created, 1), ["vd"] = Math.Round(hostLine.Reduced, 1),
                    ["lost"] = Math.Round(hostLine.Lost, 1), ["held"] = hostLine.HasCount ? Math.Round(hostLine.Counted, 1) : null,
                };
                race["outcome"] = outcome;
                race["race_s"] = Math.Round(raceS, 3);
                race["wall_s"] = Math.Round(_wall.Elapsed.TotalSeconds - raceWall, 2);
                race["winner"] = gameData.WinnerDomain.ToString();
                race["broods"] = broods;
                race["brood_at"] = broodAt;
                race["claim_s"] = new JsonObject(claimS.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)Math.Round(kv.Value, 1))));
                race["claim_timeline"] = timeline;
                race["seats"] = seats;
                Console.WriteLine($"[race-judge] brood seed {seed}: {outcome} {raceS:0.0} s sim in {_wall.Elapsed.TotalSeconds - raceWall:0.0} s wall - " +
                                  broods.ToJsonString() + " " + string.Join(", ", seats.Select(s => $"{s!["hull"]}/{s["domain"]} VC {s["vc"]} VD {s["vd"]} in {s["nucleus_s"]}s")));
                Flush(running: true);

                if (r + 1 >= _seeds.Count) break;
                deadline = Now + 30;
                while (gameData.IsTurnRunning && Now < deadline) yield return 0.25f;
                yield return 2.5f;
                controller = FindBrood();
                CosmicShore.Engine.Random.InitState(_seeds[r + 1]);
                if (controller != null) controller.RequestReplay();
                deadline = Now + 120;
                while (controller != null && Now < deadline) yield return 0.25f;
            }
        }
    }
}
