using System.Collections.Generic;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// ARENA pilot swap: a human hands their hull to the AI and takes over an AI teammate's hull,
    /// mid-match (<c>Docs/HomeHub/ARCHITECTURE.md</c> §3.7).
    ///
    /// <para><b>It is the Cellular Duel vessel swap, generalised to any two pilots of one team.</b>
    /// Nothing new is spawned and nothing is destroyed: the two Player objects exchange their live
    /// vessels (<see cref="IPlayer.ChangeVessel"/> + <see cref="IVessel.ChangePlayer"/>, the path
    /// <c>GameDataSO.SwapVessels</c> already uses), the server moves each vessel's NetworkObject
    /// ownership to the machine that now flies it, and the AI's autopilot moves with it. Because a
    /// swap only EXCHANGES hulls it can never put two pilots in one hull, so the arena's
    /// one-pilot-per-hull rule survives any number of swaps without being re-checked.</para>
    ///
    /// <para><b>Score follows the PILOT, not the hull.</b> <c>RoundStats</c> rides the Player, and
    /// every hit and kill is credited through <c>VesselStatus.Player</c>, which the swap re-points -
    /// so what you earn in the teammate's hull is yours, and what the AI earns in the hull you left
    /// is its. Both are on one domain, so the team total is untouched by construction.</para>
    ///
    /// <para><b>Flow (owner detects, server decides, everyone applies).</b> The local pilot's
    /// <c>InputController</c> reads <see cref="PilotSwapGesture"/> and calls
    /// <see cref="Player.RequestPilotSwap"/>, which picks the next teammate hull round a ring every
    /// peer orders the same way (<see cref="TryPickRingTarget"/>) and asks the server by VESSEL id.
    /// The server re-validates against its own state (<see cref="TryValidateServer"/> - a stale or
    /// contested request, e.g. two humans reaching for one AI in the same frame, is refused rather
    /// than resolved against whatever that AI flies by then), hands ownership over
    /// (<see cref="TransferOwnershipServer"/>) and broadcasts; every peer runs
    /// <see cref="ApplyLocal"/>.</para>
    ///
    /// <para><b>Stated limit:</b> element levels are simulated on the machine that OWNS a hull and
    /// never replicate, so a hull that changes machines keeps the levels its NEW owner's replica
    /// held (its starting elements, plus anything that replica saw) - crystal-earned levels the
    /// previous owner simulated do not travel with it.</para>
    /// </summary>
    public static class PilotSwap
    {
        /// <summary>Minimum seconds between two swaps by one pilot - client-side debounce and the
        /// server's own rate limit, so a held or mashed button cannot churn ownership.</summary>
        public const float CooldownSeconds = 0.75f;

        /// <summary>
        /// Step <paramref name="direction"/> (+1 next, -1 previous) round the team's hulls from the
        /// pilot's own hull. <paramref name="teamHullIds"/> is every hull the pilot could be in -
        /// their own plus each AI teammate's - in ANY order; the ring is ordered by vessel
        /// NetworkObjectId, which every peer agrees on, so "next" names the same hull everywhere.
        /// False when there is no teammate hull to move to.
        /// </summary>
        public static bool TryPickRingTarget(IList<ulong> teamHullIds, ulong ownHullId, int direction,
                                             out ulong targetHullId)
        {
            targetHullId = 0;
            if (teamHullIds == null || direction == 0) return false;

            var ring = new List<ulong>(teamHullIds.Count);
            for (int i = 0; i < teamHullIds.Count; i++)
                if (!ring.Contains(teamHullIds[i])) ring.Add(teamHullIds[i]);
            if (!ring.Contains(ownHullId)) ring.Add(ownHullId);
            if (ring.Count < 2) return false;

            ring.Sort();
            int own = ring.IndexOf(ownHullId);
            int step = direction > 0 ? 1 : -1;
            int n = ring.Count;
            targetHullId = ring[((own + step) % n + n) % n];
            return targetHullId != ownHullId;
        }

        /// <summary>
        /// The hulls <paramref name="self"/> could swap into right now, plus their own: every live
        /// vessel flown by an AI on the same domain. Read off the replicated Player state, so it is
        /// the same set on every peer.
        /// </summary>
        public static void CollectTeamHulls(Player self, IList<IPlayer> players, List<ulong> into)
        {
            into.Clear();
            if (!self || players == null) return;

            var domain = self.NetDomain.Value;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] is not Player p || !p || !p.IsSpawned) continue;
                if (p != self && !p.NetIsAI.Value) continue;          // never another human's hull
                if (p.NetDomain.Value != domain) continue;            // never the other team's
                if (!IsAlive(p.Vessel)) continue;
                into.Add(p.Vessel.VesselNetId);
            }
        }

        /// <summary>
        /// Server-side authority on one request. Everything is re-read from the server's own state:
        /// the requester's client picked the target from replicated values that may be a tick old.
        /// </summary>
        public static bool TryValidateServer(Player requester, ulong targetHullId, GameDataSO gameData,
                                             out Player ai, out string refusal)
        {
            ai = null;
            refusal = null;

            if (gameData == null || !gameData.IsArenaMatch) { refusal = "not an arena match"; return false; }
            if (!gameData.IsTurnRunning)                    { refusal = "the match is not running"; return false; }
            if (!requester || requester.NetIsAI.Value)      { refusal = "requester is not a human pilot"; return false; }
            if (!IsAlive(requester.Vessel))                 { refusal = "requester has no vessel"; return false; }

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.SpawnManager.SpawnedObjects.TryGetValue(targetHullId, out var hullObj) || !hullObj)
            { refusal = "target hull is not spawned"; return false; }

            if (!hullObj.TryGetComponent(out VesselController hull) || hull.VesselStatus == null)
            { refusal = "target is not a vessel"; return false; }

            if (hull.VesselStatus.Player is not Player pilot || !pilot || pilot == requester)
            { refusal = "target hull has no other pilot"; return false; }

            if (!pilot.NetIsAI.Value)                        { refusal = "target hull is flown by a human"; return false; }
            if (pilot.NetDomain.Value != requester.NetDomain.Value) { refusal = "target hull is on another team"; return false; }
            if (!ReferenceEquals(pilot.Vessel, hull))        { refusal = "target pilot/hull pairing is stale"; return false; }

            ai = pilot;
            return true;
        }

        /// <summary>
        /// Move each vessel's NetworkObject to the machine that flies it after the swap: the
        /// human's client takes the AI's hull, the server (the AI's owner) takes the human's.
        /// Runs BEFORE the broadcast, so the ownership messages are queued ahead of the apply RPC;
        /// the vessel's replica subscription follows ownership on its own
        /// (<c>VesselController.OnGainedOwnership</c>), so neither arrival order is load-bearing.
        /// For the HOST's own pilot both hulls are already server-owned and nothing moves.
        /// </summary>
        public static void TransferOwnershipServer(Player human, Player ai)
        {
            if (human.Vessel is not VesselController humanHull || ai.Vessel is not VesselController aiHull) return;

            var humanClient = human.OwnerClientId;
            var aiClient = ai.OwnerClientId;

            // The AI lets go of EVERYTHING before the hull changes hands, and it has to happen HERE,
            // while the server still owns the hull, so the releases travel the owner's route
            // (owner -> server -> every peer) instead of stopping on this machine only. A hull the
            // AI was mid-drift in used to arrive with the drift still held - the course locked and
            // the nose free - which read as a spin the new pilot could not get out of.
            aiHull.ToggleAIPilot(false);
            aiHull.VesselStatus.ActionHandler.ReleaseHeldInputs();

            // The hull a human now flies must outlive that human exactly as the hull they spawned
            // in does (ServerPlayerVesselInitializer.SpawnVesselForPlayer), or a pilot who drops
            // after a swap takes the hull with them and the departed-pilot AI takeover finds no
            // ship to fly. AI hulls are spawned without the flag, so it is set here.
            aiHull.NetworkObject.DontDestroyWithOwner = true;
            humanHull.NetworkObject.DontDestroyWithOwner = true;

            if (aiHull.NetworkObject.OwnerClientId != humanClient)
                aiHull.NetworkObject.ChangeOwnership(humanClient);
            if (humanHull.NetworkObject.OwnerClientId != aiClient)
                humanHull.NetworkObject.ChangeOwnership(aiClient);
        }

        /// <summary>
        /// Exchange the two pilots' hulls on THIS machine. Runs on every peer, server included.
        /// </summary>
        public static void ApplyLocal(Player human, Player ai, GameDataSO gameData)
        {
            if (!human || !ai) return;
            var humanHull = human.Vessel;   // the hull the human is leaving -> the AI's
            var aiHull = ai.Vessel;         // the hull the human is taking
            if (!IsAlive(humanHull) || !IsAlive(aiHull)) return;

            bool server = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            // The AI lets go of its hull BEFORE anybody else takes it. The autopilot normally runs
            // only on the server (Player.StartPlayer's AI branch), but "normally" is not a promise
            // a swap can lean on: an autopilot left running on the hull a human now flies writes
            // the stick every frame and the human reads as "still under AI control". Stop it
            // wherever it is running.
            if (server || aiHull.VesselStatus.AutoPilotEnabled) aiHull.ToggleAIPilot(false);

            human.ChangeVessel(aiHull);
            ai.ChangeVessel(humanHull);

            // ChangePlayer re-binds everything a hull decides by who flies it: the platform laws
            // (corridor, speed tunnel, vision band, rear view) for the local pilot, the HUD, the
            // input subscription (releasing anything held on the hull being left), the camera and
            // which machine's transformer simulates. Identity-guarded clears, so the order of these
            // two calls cannot cancel the new binding.
            humanHull.ChangePlayer(ai);
            aiHull.ChangePlayer(human);

            // Both hulls are mid-flight, and each is about to be simulated by a transformer that
            // was NOT the one flying it a frame ago on at least one machine.
            humanHull.VesselStatus.VesselTransformer.AdoptCurrentMotion();
            aiHull.VesselStatus.VesselTransformer.AdoptCurrentMotion();

            if (server)
            {
                // The hull the human left is now the AI's: configure it the way a backfill bot is
                // configured (mode-aware seeking, skill from intensity), hand it the MODE's
                // steering hooks the bot carried on its old hull (a race line, a strike plan -
                // without them it would fly the new hull on the platform default), and put the
                // pilot in it.
                var leftPilot = humanHull.VesselStatus.AIPilot;
                var takenPilot = aiHull.VesselStatus.AIPilot;
                if (leftPilot)
                {
                    ServerPlayerVesselInitializerWithAI.ConfigureAIPilotForMode(leftPilot, gameData);
                    leftPilot.TakeModeHooksFrom(takenPilot);
                }
                humanHull.ToggleAIPilot(true);
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[PilotSwap] {human.Name} took {aiHull.VesselStatus.VesselType} from AI {ai.Name}, " +
                $"who now flies {humanHull.VesselStatus.VesselType}.");

            if (human.IsLocalUser)
                VerifyLocalTakeoverAsync(human, aiHull).Forget();
        }

        /// <summary>
        /// On the machine of the human who just swapped, check a few frames later that the swap
        /// actually landed the way the player needs it: they fly the new hull, its autopilot is
        /// off, their input is live, and the gameplay camera follows it. Each is a separate
        /// system (Player, AIPilot, InputController, CameraManager) and a swap that half-lands
        /// reads on screen as ONE symptom - "the D-pad handed my ship to the AI and left me
        /// watching it" - so a failure is a WARNING that names which one, not a guess.
        /// </summary>
        static async UniTaskVoid VerifyLocalTakeoverAsync(Player human, IVessel hull)
        {
            await UniTask.DelayFrame(3);
            if (!human || !IsAlive(hull)) return;

            var problems = new List<string>(4);
            if (!ReferenceEquals(human.Vessel, hull))
                problems.Add("the player's Vessel is not the hull they took");
            if (!ReferenceEquals(hull.VesselStatus.Player, human))
                problems.Add("the hull's VesselStatus.Player is not the player");
            if (hull.VesselStatus.AutoPilotEnabled)
                problems.Add("the hull's autopilot is still running");
            if (human.InputStatus != null && human.InputStatus.Paused)
                problems.Add("the player's input is paused");

            var cm = CameraManager.Instance;
            var follow = hull.VesselStatus.CameraFollowTarget;
            if (cm != null && follow && cm.PlayerFollowTarget != follow)
            {
                // Not a report-only item: the camera is the one piece the player has no other
                // way to recover, so put it right and say that it had to be.
                problems.Add($"the gameplay camera was following '{(cm.PlayerFollowTarget ? cm.PlayerFollowTarget.name : "nothing")}' - re-pointed");
                cm.SetupGamePlayCameras(follow);
            }

            if (problems.Count == 0)
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                    $"[PilotSwap] {human.Name} is flying {hull.VesselStatus.VesselType}: takeover verified.");
                return;
            }

            CSDebug.LogWarning($"[PilotSwap] {human.Name}'s takeover of {hull.VesselStatus.VesselType} " +
                               $"did not fully land: {string.Join("; ", problems)}.");
        }

        static bool IsAlive(IVessel vessel) => vessel is UnityEngine.Object o && o;
    }
}
