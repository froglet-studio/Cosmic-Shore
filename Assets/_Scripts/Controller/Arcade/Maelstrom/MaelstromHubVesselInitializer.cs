using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Injectors;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns every pilot a vessel in the Maelstrom hub, so the round everybody is waiting for is
    /// something they can fly rather than a picture of a world.
    ///
    /// <para>It is the ordinary <see cref="ServerPlayerVesselInitializer"/> chain - the same one
    /// every game scene runs - with two hub-shaped differences and, deliberately, no third:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Autopilot on arrival.</b> A hub vessel flies itself until its pilot taps into the
    /// preview window, exactly as the menu's lava-lamp vessel does. The hub is a waiting room, so
    /// a ship that sat still until someone touched a stick would read as a frozen scene.</item>
    /// <item><b>The upcoming mode's HULL.</b> Fifteen of the sixteen pool modes lock to one vessel,
    /// so the hub hands you that one - the warm-up is in the ship you are about to race in, and
    /// the hull swap is paid for here instead of inside the next scene's connecting screen.</item>
    /// </list>
    ///
    /// <para><b>It does NOT touch domain</b>, and that is the one thing to be careful of when
    /// reading this beside <c>MenuServerPlayerVesselInitializer</c>, which it otherwise resembles.
    /// The menu resets every human to Jade because the lava lamp has no teams; a Maelstrom hub sits
    /// in the middle of a running tournament whose entire scoreboard is per-domain, so resetting a
    /// pilot's domain between rounds would hand their points to a team they are no longer on.</para>
    ///
    /// <para>Hub vessels are ordinary scene vessels (<c>destroyWithScene: true</c>): the round
    /// launch is a Single load, which is exactly what should take them away.</para>
    /// </summary>
    public class MaelstromHubVesselInitializer : ServerPlayerVesselInitializer
    {
        [Header("Hub Bots")]
        [SerializeField, Range(0f, 1f), Tooltip(
            "Pilot skill (0..1) for the tournament's AI, flying the hub. The hub has no intensity " +
            "of its own - the drawn round's intensity belongs to the round - so it is authored.")]
        float botSkill = 0.5f;

        [SerializeField, Min(0f), Tooltip(
            "Speed a hub bot is launched at. A bot is released UNDER WAY, never at a standstill: " +
            "the pair-init hands every vessel a dead stop (ResetForPlay zeroes speed), a vessel " +
            "under VesselPrismController's 3 u/s gate lays NO TRAIL, and the AI's own drift PINS " +
            "cruise speed at whatever the vessel carried in - so a bot that drifts before it has " +
            "accelerated stays pinned near zero and reads as broken rather than slow. 60 is the " +
            "low end of the fleet's flight band. Same number and same reason as the menu's " +
            "companionLaunchSpeed.")]
        float botLaunchSpeed = 60f;

        readonly List<Player> _bots = new();

        /// <summary>Seats already given a body, by seat name - the idempotency key.</summary>
        readonly HashSet<string> _seated = new();

        /// <summary>
        /// Nobody is finishing a match here, so a pilot who leaves the hub leaves nothing behind
        /// for the AI to take over. Same reasoning as the menu's.
        /// </summary>
        protected override bool ConvertDepartedPlayersToAI => false;

        /// <summary>
        /// Fly the hull the NEXT round will be played in when that round locks to exactly one, and
        /// otherwise take the pilot's own pick verbatim.
        ///
        /// <para>The base clamps against <c>gameData.AllowedVesselClasses</c>, which is the wrong
        /// answer in a hub for a subtle reason: that list deliberately survives the per-scene
        /// reset (it is pre-launch config the game scene still has to read), so between rounds it
        /// still holds the hull of the round that just FINISHED. Clamping to it would put everyone
        /// in the last mode's ship to warm up for the next one.</para>
        /// </summary>
        /// <remarks>
        /// Unlike the base it deliberately does NOT call <c>Player.ServerForceVesselType</c> to
        /// write the resolved hull back onto <c>NetDefaultVesselType</c>. That variable is the
        /// PLAYER'S standing preference, and the menu reads it verbatim on the way home ("the menu
        /// is where you are allowed to fly anything"); stamping a warm-up hull into it would send
        /// a pilot back to the lava lamp flying whatever the Maelstrom happened to draw last. The
        /// base keeps it honest because a game scene's respawns, HUD and telemetry all read it -
        /// none of which a hub has.
        /// </remarks>
        protected override VesselClassType ResolveSpawnVesselType(Player networkPlayer)
        {
            var pending = MaelstromController.Instance?.PendingGame;
            if (pending != null && pending.Vessels != null && pending.Vessels.Count == 1 && pending.Vessels[0] != null)
                return pending.Vessels[0].Class;

            return networkPlayer.NetDefaultVesselType.Value;
        }

        protected override async UniTask OnPlayerReadyToSpawnAsync(Player player, CancellationToken ct)
        {
            await base.OnPlayerReadyToSpawnAsync(player, ct);
            ActivateAutopilot(player);
        }

        // ── The tournament's AI, flying the hub ──────────────────────────────────

        /// <summary>
        /// Give every dealt AI seat a body in the hub, so the field a player is about to race is
        /// on screen rather than only in a list.
        ///
        /// <para><b>Why the hub spawns its own rather than letting the round do it.</b> A round's
        /// bots are spawned by that scene's <c>ServerPlayerVesselInitializerWithAI</c> from
        /// <c>RequestedAIBackfillCount</c> and die with it. The hub is a different scene and a
        /// different moment - it is where the party decides whether to ready up - so it spawns the
        /// same seats itself and takes them down again at launch
        /// (<see cref="DespawnHubBots"/>), leaving the round to spawn its own. Skipping that
        /// teardown would field every bot twice.</para>
        ///
        /// <para>Idempotent and safe to call every host tick: a seat that already has a body is
        /// skipped by NAME, which is the seat's identity and the key the summary resolves its face
        /// from. Seats arrive once (the deal is one-shot) but a player joining or leaving the hub
        /// re-runs the deal, so this has to notice a seat that is genuinely new without re-bodying
        /// the ones already flying.</para>
        /// </summary>
        public void EnsureHubBots(IReadOnlyList<MaelstromAISeat> seats, SO_ArcadeGame pendingGame)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (seats == null || seats.Count == 0 || _cts == null) return;

            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (seat == null || string.IsNullOrEmpty(seat.Name)) continue;
                if (!_seated.Add(seat.Name)) continue;

                SpawnHubBotAsync(seat, ResolveBotVessel(pendingGame), _cts.Token).Forget();
            }
        }

        /// <summary>
        /// Take the hub's bots away. Called immediately before the round launches: the next scene
        /// spawns the same seats itself, and a hub bot that survived the load would be a second
        /// body for a pilot that already has one.
        ///
        /// <para>Explicit rather than left to the scene load, because these are spawned
        /// <c>destroyWithScene: false</c> - the same race the menu's companions document, where a
        /// client's scene-synchronize batches with the spawn and destroys the just-spawned
        /// NetworkObjects.</para>
        /// </summary>
        public void DespawnHubBots()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                _bots.Clear();
                _seated.Clear();
                return;
            }

            for (int i = 0; i < _bots.Count; i++)
            {
                var bot = _bots[i];
                if (!bot) continue;

                string name = bot.Name;
                if (bot.Vessel != null) DespawnVessel(bot.Vessel);
                if (bot.IsSpawned) bot.NetworkObject.Despawn(true);

                // Prune the roster BY NAME as well as despawning: Players and RoundStatsList are
                // name-keyed, and a destroyed entry left behind shadows the live one the round is
                // about to spawn under the same seat name.
                gameData.RemovePlayerData(name);
            }

            _bots.Clear();
            _seated.Clear();
        }

        /// <summary>
        /// The hull a hub bot flies: the drawn round's, when that round locks to one - the same
        /// answer <see cref="ResolveSpawnVesselType"/> gives a human, so the warm-up looks like the
        /// race. A mixed-hull round rolls per bot rather than fielding four of one ship.
        /// </summary>
        VesselClassType ResolveBotVessel(SO_ArcadeGame pendingGame)
        {
            if (pendingGame != null && pendingGame.Vessels != null && pendingGame.Vessels.Count > 0)
            {
                var pick = pendingGame.Vessels.Count == 1
                    ? pendingGame.Vessels[0]
                    : pendingGame.Vessels[Random.Range(0, pendingGame.Vessels.Count)];
                if (pick != null) return pick.Class;
            }
            return VesselClassType.Random;
        }

        /// <summary>
        /// The server-side release. Deliberately the SAME chain as the menu's AI companion and a
        /// game scene's backfill bot - spawn the Player NetworkObject, claim it, stamp its
        /// NetworkVariables, spawn its vessel, initialize the pair, configure the pilot, start it -
        /// so a hub bot is an ordinary networked AI player rather than a third kind of bot.
        /// </summary>
        async UniTaskVoid SpawnHubBotAsync(MaelstromAISeat seat, VesselClassType vesselClass, CancellationToken ct)
        {
            var playerPrefab = ResolveAiPlayerPrefab();
            if (!playerPrefab)
            {
                CSDebug.LogError("[MaelstromHubVesselInit] No AI Player prefab on the live " +
                                 "NetworkConfig - the hub cannot field its bots.");
                _seated.Remove(seat.Name);
                return;
            }

            var botNO = Instantiate(playerPrefab);
            GameObjectInjector.InjectRecursive(botNO.gameObject, _container);
            botNO.Spawn(false);

            if (!botNO.TryGetComponent(out Player bot))
            {
                CSDebug.LogError("[MaelstromHubVesselInit] AI Player prefab is missing its Player component.");
                botNO.Despawn(true);
                _seated.Remove(seat.Name);
                return;
            }

            // Claimed in the SAME frame as the spawn. A server-owned Player carries the HOST's
            // OwnerClientId and Player.OnNetworkSpawn has already raised the spawn event from
            // inside Spawn(), so without this the human path reads it as the host asking for a
            // second vessel.
            ClaimExternallySpawnedPlayer(bot);

            bot.NetIsAI.Value = true;
            bot.NetDefaultVesselType.Value = vesselClass;
            bot.NetDomain.Value = seat.Domain;
            bot.NetName.Value = seat.Name;

            var vesselNO = SpawnVesselForPlayer(bot.OwnerClientId, bot, vesselClass);
            if (!vesselNO || !vesselNO.TryGetComponent(out IVessel vessel))
            {
                if (vesselNO) vesselNO.Despawn(true);
                botNO.Despawn(true);
                _seated.Remove(seat.Name);
                return;
            }

            clientPlayerVesselInitializer.InitializePlayerAndVessel(bot, vessel);

            // AddPlayer (inside the pair init) hands out a pose from the cell ring; ResetForPlay
            // zeroes the speed. Only the speed is overridden - the ring is exactly where a hub bot
            // should start, and it is the ring the round itself will use.
            vessel.SetInitialSpeed(botLaunchSpeed);

            var aiPilot = vesselNO.GetComponentInChildren<AIPilot>();
            // shouldSeekPlayers: FALSE. There is no match on in a hub, and a bot that hunted the
            // pilots waiting in it would be attacking people who cannot score, cannot lose and in
            // most cases are not even flying yet.
            if (aiPilot) aiPilot.ConfigureForGameMode(gameData, shouldSeekPlayers: false, botSkill);

            // StartPlayer, NOT ActivateAutopilot: for a player whose NetIsAI is set it already does
            // the autopilot branch itself, and doing both starts the AI pilot TWICE - which
            // duplicates every ability coroutine and cannot be cleaned up. (Recorded on the menu's
            // companion path; the same trap, same shape.)
            bot.StartPlayer();
            _bots.Add(bot);

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[MaelstromHubVesselInit] Hub bot '{seat.Name}' ({vesselClass}, {seat.Domain}) is flying.");

            await UniTask.Delay(postSpawnDelayMs, DelayType.UnscaledDeltaTime, cancellationToken: ct);
            NotifyClients(bot);
        }

        /// <summary>
        /// The AI Player prefab, read off the live <c>NetworkConfig.PlayerPrefab</c> - which IS the
        /// prefab every game scene wires by hand, so the hub carries no second reference that can
        /// drift from the registered NetworkPrefab.
        /// </summary>
        static NetworkObject ResolveAiPlayerPrefab()
        {
            var prefab = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.NetworkConfig?.PlayerPrefab
                : null;
            return prefab ? prefab.GetComponent<NetworkObject>() : null;
        }

        static void ActivateAutopilot(Player player)
        {
            if (player?.Vessel == null)
            {
                CSDebug.LogError("[MaelstromHubVesselInit] Player or Vessel not available after " +
                                 "initialization - the hub vessel will not fly itself.");
                return;
            }

            player.StartPlayer();
            player.Vessel.ToggleAIPilot(true);
            player.InputController?.SetPause(true);
        }
    }
}
