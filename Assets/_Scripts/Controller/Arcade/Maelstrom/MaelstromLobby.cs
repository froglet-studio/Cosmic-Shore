using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Maelstrom hub's ready-up and countdown: who has pressed READY, which round is drawn,
    /// and when it launches.
    ///
    /// <para><b>It is a plain MonoBehaviour, and that is the point.</b> Its predecessor
    /// (<c>MaelstromLobbyNetwork</c>) was a <c>NetworkBehaviour</c> that had to be placed in the
    /// Maelstrom scene to exist at all - and it never was. The scene shipped with
    /// <c>lobbyNetwork: {fileID: 0}</c>, so the ready-up it was written to drive had not run once:
    /// the hub fell through to a local fallback where the host's button started the round
    /// immediately and nobody else's did anything. Placing it correctly would have fixed that
    /// scene; moving the state onto <see cref="Player"/> - which is networked, persistent, and
    /// already in every scene - means there is nothing left to place, so no future edit of that
    /// scene can un-wire it again. The trade is stated on
    /// <see cref="Player.NetMaelstromRound"/>: the session ticket is written to every player
    /// rather than to one object.</para>
    ///
    /// <para><b>Authority.</b> The host arms the deadline, counts the presses and calls
    /// <see cref="MaelstromController.BeginNextRound"/>. Every peer only READS - the countdown it
    /// renders is derived from one synchronised <c>ServerTime</c> instant, so no peer ticks a
    /// clock of its own and two machines cannot disagree about how long is left.</para>
    ///
    /// <para><b>The two countdowns are one countdown.</b> "Auto-start after 30s" and "everyone is
    /// ready, go in 3" are the same deadline at different values: readying up SNAPS the deadline
    /// in to <see cref="allReadySeconds"/>. That is why the hub can show its 3-2-1 with a single
    /// rule - <c>SecondsRemaining &lt;= 3</c> - and why an unready lobby gets exactly the same
    /// final three seconds as a ready one, which is what the design asked for.</para>
    ///
    /// <para>Only ever active in <see cref="MaelstromPhase.Lobby"/> (the opening lobby and the
    /// between-round hub). It stays idle on the summary/stats screen, which shares the scene.</para>
    /// </summary>
    public class MaelstromLobby : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] MaelstromDataSO tournamentData;

        [Header("Countdown (seconds)")]
        [Tooltip("Auto-start delay when NOT everyone is ready. The countdown text only appears " +
                 "for the last few seconds of it - see CountdownVisibleSeconds.")]
        [SerializeField, Min(1f)] float autoStartSeconds = 30f;

        [Tooltip("Start delay once EVERY connected player has readied. The deadline SNAPS in to " +
                 "this, so readying up is what turns the wait into a 3-2-1.")]
        [SerializeField, Min(1f)] float allReadySeconds = 3f;

        [Tooltip("How many seconds out the GAME STARTS IN counter becomes visible. Matches " +
                 "allReadySeconds so an all-ready start and the tail of an auto-start read " +
                 "identically.")]
        [SerializeField, Min(1f)] float countdownVisibleSeconds = 3f;

        // The host's authoritative copy. Mirrored onto every Player so peers can read it; held
        // here as well so a player object spawning late can be caught up (see PublishTicket).
        MaelstromRoundTicket _authoritative = MaelstromRoundTicket.None;

        bool _armed;      // server: deadline set for this hub visit
        bool _started;    // server: BeginNextRound already fired (one-shot)

        readonly List<IPlayer> _humans = new();

        static bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        /// <summary>True while the hub (not the summary/stats screen) owns the screen.</summary>
        public bool IsCountingDown =>
            MaelstromController.Instance != null &&
            MaelstromController.Instance.Phase == MaelstromPhase.Lobby;

        /// <summary>The ticket as THIS peer currently sees it.</summary>
        public MaelstromRoundTicket Ticket { get; private set; } = MaelstromRoundTicket.None;

        /// <summary>The drawn mode this hub is standing in front of, or null before the draw lands.</summary>
        public SO_ArcadeGame PendingGame => tournamentData != null ? tournamentData.PendingGame : null;

        /// <summary>The intensity rolled for the drawn mode (1 until the draw lands).</summary>
        public int PendingIntensity =>
            tournamentData != null && tournamentData.PendingIntensity > 0 ? tournamentData.PendingIntensity : 1;

        /// <summary>
        /// Whole seconds until the round launches (ceil, never negative). Shows the full
        /// auto-start delay before the host has armed anything, so the hub never opens on "0".
        /// </summary>
        public int SecondsRemaining
        {
            get
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !Ticket.IsArmed) return Mathf.CeilToInt(autoStartSeconds);
                return Mathf.Max(0, Mathf.CeilToInt((float)(Ticket.StartServerTime - nm.ServerTime.Time)));
            }
        }

        /// <summary>True while the 3-2-1 should be on screen - the last few seconds either way.</summary>
        public bool ShowFinalCountdown => Ticket.IsArmed && SecondsRemaining <= Mathf.CeilToInt(countdownVisibleSeconds);

        /// <summary>This peer's own ready state.</summary>
        public bool LocalReady
        {
            get
            {
                var local = LocalPlayer();
                return local != null && local.IsMaelstromReady;
            }
        }

        public int ReadyCount
        {
            get
            {
                RefreshHumans();
                int n = 0;
                for (int i = 0; i < _humans.Count; i++)
                    if (_humans[i].IsMaelstromReady) n++;
                return n;
            }
        }

        public int TotalPlayers
        {
            get
            {
                RefreshHumans();
                return _humans.Count;
            }
        }

        /// <summary>
        /// The human pilots the hub waits on, in a stable order, so the view can draw a face per
        /// ready press. AI are absent by construction: they have no button, so a chip for one
        /// could never change and would read as a player who never arrives - the same argument
        /// the connecting panel's roster already makes.
        /// </summary>
        public IReadOnlyList<IPlayer> HumanPlayers
        {
            get
            {
                RefreshHumans();
                return _humans;
            }
        }

        // ── Local input ──────────────────────────────────────────────────────────

        public void ToggleLocalReady() => SetLocalReady(!LocalReady);

        public void SetLocalReady(bool ready)
        {
            var local = LocalPlayer();
            if (local == null) return;
            if (local.IsMaelstromReady == ready) return;
            local.SetMaelstromReady(ready);
        }

        // ── Drive ────────────────────────────────────────────────────────────────

        void Update()
        {
            ReadTicket();

            if (!IsHost) return;
            if (!IsCountingDown) return;

            var tc = MaelstromController.Instance;
            var nm = NetworkManager.Singleton;
            if (tc == null || nm == null) return;

            if (!_armed)
            {
                // Draw the round BEFORE arming, so the ticket the party receives already names the
                // mode its preview has to build. Arming first would publish a countdown against a
                // round nobody can see yet, and every peer would spend the first frames of the hub
                // showing an empty preview frame for a game that had in fact been chosen.
                tc.PrepareNextRound();

                // Fix the field before anything reads it: SeatCount pilots, the party's humans
                // plus AI for the rest, and the AI roster DEALT here rather than by whichever
                // round happens to backfill first. The hub is where a player decides whether to
                // ready up, so it is the last place that should be vague about who they are
                // racing.
                RefreshHumans();
                tc.ApplyRoster(_humans);

                _authoritative = new MaelstromRoundTicket
                {
                    GameIndex = (short)Mathf.Clamp(tournamentData != null ? tournamentData.PendingGameIndex : -1,
                                                   -1, short.MaxValue),
                    Intensity = (byte)Mathf.Clamp(tournamentData != null ? tournamentData.PendingIntensity : 0, 0, 255),
                    StartServerTime = nm.ServerTime.Time + autoStartSeconds,
                };
                _armed = true;
                ClearReadyFlags();
            }

            MaybeSnapToAllReady(nm);
            PublishTicket();

            if (!_started && nm.ServerTime.Time >= _authoritative.StartServerTime)
            {
                _started = true;
                tc.BeginNextRound();
            }
        }

        /// <summary>
        /// Once every connected human has readied, pull the deadline in to
        /// <see cref="allReadySeconds"/>.
        ///
        /// <para>Deliberately one-way. Un-readying after the snap does NOT push the deadline back
        /// out: the party has already been shown a 3-2-1, and letting one player cancel it is a
        /// griefing lever on a screen whose whole job is to get everyone into the next round.</para>
        /// </summary>
        void MaybeSnapToAllReady(NetworkManager nm)
        {
            RefreshHumans();
            if (_humans.Count == 0) return;

            for (int i = 0; i < _humans.Count; i++)
                if (!_humans[i].IsMaelstromReady) return;

            double snap = nm.ServerTime.Time + allReadySeconds;
            if (snap < _authoritative.StartServerTime)
                _authoritative.StartServerTime = snap;
        }

        /// <summary>
        /// Push the authoritative ticket onto every player whose copy differs. Re-checked every
        /// frame rather than written once, so a client that connects (or whose Player spawns) in
        /// the middle of a hub is caught up without anyone having to notice it arrived.
        /// </summary>
        void PublishTicket()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            var spawned = nm.SpawnManager.SpawnedObjectsList;
            foreach (var no in spawned)
            {
                if (no != null && no.TryGetComponent<Player>(out var p))
                    p.SetMaelstromRoundServer(_authoritative);
            }
        }

        void ClearReadyFlags()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                if (no != null && no.TryGetComponent<Player>(out var p))
                    p.SetMaelstromReady(false);
        }

        /// <summary>
        /// Mirror the replicated ticket into <see cref="MaelstromDataSO"/> so every other reader -
        /// the preview host, the round cards, the splash formatter - asks the same asset the host
        /// does and never has to know which side of the wire it is on.
        /// </summary>
        void ReadTicket()
        {
            var ticket = ResolveTicket();
            Ticket = ticket;

            if (tournamentData == null) return;

            // The HOST owns PendingGameIndex outright (it drew it); overwriting it from a ticket
            // that has not been published yet this frame would blank the draw it just made.
            if (IsHost) return;

            if (!ticket.HasGame)
            {
                tournamentData.ClearPendingRound();
                return;
            }

            tournamentData.PendingGameIndex = ticket.GameIndex;
            tournamentData.PendingIntensity = ticket.Intensity;
            var game = tournamentData.PendingGame;
            if (game != null)
            {
                tournamentData.NextGameName = game.DisplayName;
                tournamentData.NextGameIntensity = ticket.Intensity;
            }
        }

        MaelstromRoundTicket ResolveTicket()
        {
            if (IsHost) return _authoritative;

            // The local player is the one Player every peer is guaranteed to hold, so it is read
            // first; any other is an equally valid copy, because the host writes them identically.
            var local = LocalPlayer() as Player;
            if (local != null && local.IsSpawned)
            {
                var t = local.NetMaelstromRound.Value;
                if (t.HasGame || t.IsArmed) return t;
            }

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null)
            {
                foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (no == null || !no.TryGetComponent<Player>(out var p) || !p.IsSpawned) continue;
                    var t = p.NetMaelstromRound.Value;
                    if (t.HasGame || t.IsArmed) return t;
                }
            }

            return MaelstromRoundTicket.None;
        }

        // ── Roster ───────────────────────────────────────────────────────────────

        int _humansFrame = -1;

        // Cached per frame: the view asks for the tally, the total and the roster separately, and
        // each answer is a sweep of the spawn list. One sweep per frame is plenty for a screen
        // that changes when somebody presses a button.
        void RefreshHumans()
        {
            if (_humansFrame == Time.frameCount) return;
            _humansFrame = Time.frameCount;

            _humans.Clear();
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return;

            foreach (var no in nm.SpawnManager.SpawnedObjectsList)
            {
                if (no == null || !no.TryGetComponent<Player>(out var p)) continue;
                if (!p.IsSpawned || p.IsInitializedAsAI) continue;
                if (SpectatorSession.IsSpectatorClient(p.OwnerClientId)) continue;
                _humans.Add(p);
            }

            // Stable across frames and across peers: OwnerClientId is the one ordering every
            // machine already agrees on (the same argument PartyRoster makes for party seating).
            _humans.Sort(static (a, b) => a.OwnerClientNetId.CompareTo(b.OwnerClientNetId));
        }

        IPlayer LocalPlayer()
        {
            if (gameData != null && gameData.LocalPlayer != null) return gameData.LocalPlayer;

            var nm = NetworkManager.Singleton;
            var obj = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (obj != null && obj.TryGetComponent<Player>(out var p)) return p;
            return null;
        }

        /// <summary>
        /// Called by the view when the hub screen opens, so a second hub visit re-arms. Without it
        /// the one-shot guards below would hold from the previous round and the countdown would
        /// never start again.
        /// </summary>
        public void ArmForNewHub(GameDataSO data, MaelstromDataSO tournament)
        {
            if (data != null) gameData = data;
            if (tournament != null) tournamentData = tournament;

            _armed = false;
            _started = false;
            _authoritative = MaelstromRoundTicket.None;
            Ticket = MaelstromRoundTicket.None;

            if (tournamentData == null)
                CSDebug.LogError("[MaelstromLobby] No MaelstromDataSO - the hub cannot draw or " +
                                 "preview a round.");
        }
    }
}
