using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Multiplayer HUD shared by every domain mode.
    ///
    /// With the domain-panel wiring assigned, the top bar is ONE centred row divided into a
    /// column per active domain (Jade / Ruby / Gold): the team's summed objective score on top,
    /// that team's player icons directly underneath. The local player's domain is always the
    /// first column, and the marker for "which one is mine" is that player's chip taking the
    /// domain colour at full strength - no names anywhere, because an icon already identifies a
    /// player and a name under one avatar made that column a different height from the rest.
    ///
    /// The build order is what produces the layout: the local domain goes into
    /// <see cref="MultiplayerHUDView.AllyDomainContainer"/> and the others into
    /// <see cref="MultiplayerHUDView.OpposingDomainsContainer"/>, and in the single-bar layout
    /// both resolve to the same transform - so this class needs no branch of its own, and a HUD
    /// still wired the old way (two groups flanking a centred player card) keeps working.
    /// When the wiring is missing entirely it falls back to the legacy per-player layout in
    /// PlayerScoreContainer so scenes that haven't been updated keep working.
    /// </summary>
    public class MultiplayerHUD : MiniGameHUD
    {
        [Header("Multiplayer View")]
        [SerializeField] protected MultiplayerHUDView multiplayerView;

        // Legacy per-player cards (used when domain wiring is absent).
        protected Dictionary<string, PlayerScoreEntry> _playerCards = new();
        // New per-domain panels (used when MultiplayerHUDView.HasDomainPanelWiring is true).
        protected Dictionary<Domains, DomainScorePanel> _domainPanels = new();
        bool _useDomainView;

        // Stats this HUD actually subscribed to. Unsubscription must run off THIS set,
        // never gameData.RoundStatsList: on a mid-turn scene exit ResetRuntimeData clears
        // the roster before the old scene's objects are destroyed, so a list-based
        // unsubscribe detaches nothing and HandlePlayerStatChanged leaks onto the
        // persistent human RoundStats (Docs/ScoringSystem/BUGS.md B15).
        readonly HashSet<IRoundStats> _subscribedStats = new();

        // Domain-panel build signature - a hash of every player's (name → Player.Domain) + the local
        // ally domain + domain count at the last build. The reconcile rebuilds whenever this changes
        // (a player moved domains, or the roster grew), so the boxes can't freeze on a stale layout.
        // Domain attribution is read from Player.Domain (the authoritative NetDomain mirror), never
        // RoundStats.Domain (a derived copy that can lag behind on a client).
        int _builtLayoutSignature;
        bool _domainPanelsBuilt;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllProgress;
                gameData.OnResetForReplay.OnRaised += ResetAllProgress;
                gameData.OnDomainMetricSumsChanged += RefreshDomainSums;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllProgress;
                gameData.OnResetForReplay.OnRaised -= ResetAllProgress;
                gameData.OnDomainMetricSumsChanged -= RefreshDomainSums;
            }
        }

        // Server-synced domain sums changed - refresh every domain box to the host's values.
        void RefreshDomainSums()
        {
            if (!_useDomainView) return;
            foreach (var kvp in _domainPanels)
                kvp.Value.UpdateSum(gameData.GetDomainMetricSum(kvp.Key));
        }

        void ResetAllProgress()
        {
            if (gameData?.RoundStatsList == null) return;

            if (_useDomainView)
            {
                foreach (var d in _domainPanels.Keys.ToList())
                    _domainPanels[d].UpdateSum(0);
            }
            else
            {
                foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
                    UpdatePlayerCard(stats.Name, 0);
            }
        }

        void RefreshAllProgress()
        {
            if (gameData?.RoundStatsList == null) return;

            if (_useDomainView)
            {
                foreach (var kvp in _domainPanels)
                    kvp.Value.UpdateSum(SumStatByDomain(kvp.Key));
            }
            else
            {
                foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
                    UpdatePlayerCard(stats.Name, GetInitialCardValue(stats));
            }
        }

        private void OnValidate()
        {
            if (multiplayerView == null) multiplayerView = GetComponent<MultiplayerHUDView>();
            if (view == null) view = multiplayerView;
        }

        protected override void OnMiniGameTurnStarted()
        {
            localRoundStats = gameData.LocalRoundStats;
            if (localRoundStats != null)
                localRoundStats.OnScoreChanged += UpdateScoreUI;

            _useDomainView = multiplayerView != null && multiplayerView.HasDomainPanelWiring;

            if (_useDomainView)
                InitializeDomainPanels();
            else
                InitializePlayerCards();

            // On a client, player domains / roster can still be replicating when the turn starts.
            // React to late-arriving players so their domain box appears and updates.
            if (gameData != null)
                gameData.OnPlayerAdded += HandlePlayerAdded;

            SubscribeToGameSpecificEvents();
        }

        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            if (gameData != null)
                gameData.OnPlayerAdded -= HandlePlayerAdded;
            UnsubscribeFromAllStats();
            UnsubscribeFromGameSpecificEvents();
            _playerCards.Clear();
            _domainPanels.Clear();
            _domainPanelsBuilt = false;
            if (multiplayerView != null) multiplayerView.ClearDomainPanels();
        }

        // ── Legacy per-player layout ─────────────────────────────────────────

        private void InitializePlayerCards()
        {
            view.ClearPlayerList();
            _playerCards.Clear();
            AssignAIProfiles();

            for (int i = 0; i < gameData.RoundStatsList.Count; i++)
            {
                CreateCardForPlayer(gameData.RoundStatsList[i], i);
            }
        }

        private void CreateCardForPlayer(IRoundStats stats, int staggerIndex)
        {
            var card = Instantiate(view.PlayerScoreEntryPrefab, view.PlayerScoreContainer);
            var isLocal = gameData.LocalPlayer != null && stats.Name == gameData.LocalPlayer.Name;
            var teamColor = ResolveDomainColor(stats.Domain);

            card.Setup(stats.Name, GetInitialCardValue(stats), teamColor, isLocal, staggerIndex);

            Sprite avatarSprite = null;
            if (!isLocal)
                avatarSprite = ResolveAIAvatarSprite(stats.Name);

            if (avatarSprite == null)
            {
                var player = gameData.Players.FirstOrDefault(p => p.Name == stats.Name);
                if (player != null)
                    avatarSprite = ResolveAvatarSprite(player.AvatarId);
            }
            card.SetAvatar(avatarSprite);

            _playerCards[stats.Name] = card;

            SubscribeToPlayerStats(stats);
        }

        // ── New per-domain layout ────────────────────────────────────────────

        void InitializeDomainPanels()
        {
            AssignAIProfiles();

            // Subscribe to every player's stat events ONCE per turn so any change - a metric tick or
            // a domain change - reconciles the panel layout (see DomainLayoutChanged).
            foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
                SubscribeToPlayerStats(stats);

            RebuildDomainPanels();
        }

        /// <summary>
        /// (Re)creates the per-domain panels from the CURRENT replicated roster + domains.
        /// Idempotent and safe to call repeatedly: a client calls it again whenever a domain or
        /// the roster replicates after the turn started, so the boxes match the host instead of
        /// being frozen at a stale turn-start snapshot. Does NOT (re)subscribe to stat events -
        /// subscription is owned by InitializeDomainPanels / HandlePlayerAdded.
        /// </summary>
        void RebuildDomainPanels()
        {
            multiplayerView.ClearDomainPanels();
            _domainPanels.Clear();

            var localDomain = gameData.LocalPlayer?.Domain ?? Domains.Blue;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);

            // Local player's own domain panel goes in the LEFT (ally) container -
            // always rendered when the local player has an active domain, even if
            // they happen to be the only member of it.
            if (localDomain != Domains.Blue)
                CreateDomainPanel(localDomain, multiplayerView.AllyDomainContainer);

            // Opposing-domain panels (1 or 2 depending on RequestedDomainCount) go
            // in the RIGHT container. Only domains that actually have players are
            // rendered, so 1v1 setups don't show empty panels.
            for (int i = 0; i < dc; i++)
            {
                var d = GameDataSO.ActiveDomains[i];
                if (d == localDomain) continue;
                if (!HasPlayersInDomain(d)) continue;
                CreateDomainPanel(d, multiplayerView.OpposingDomainsContainer);
            }

            _builtLayoutSignature = ComputeLayoutSignature();
            _domainPanelsBuilt = true;
        }

        bool HasPlayersInDomain(Domains domain)
        {
            var players = gameData.Players;
            for (int i = 0, count = players.Count; i < count; i++)
                if (players[i] != null && players[i].Domain == domain) return true;
            return false;
        }

        /// <summary>
        /// True when the desired domain layout changed since the last build - a player moved domains
        /// (membership), the local ally domain changed, the roster grew, or the domain count changed.
        /// Read from Player.Domain (authoritative), so it reflects real team changes even when the
        /// derived RoundStats.Domain copy lags. Allocation-free.
        /// </summary>
        bool DomainLayoutChanged() => !_domainPanelsBuilt || ComputeLayoutSignature() != _builtLayoutSignature;

        // Order-stable hash of (local ally domain, domain count, each player's name + Player.Domain).
        // gameData.Players only ever grows during a round, so sequential hashing is stable.
        int ComputeLayoutSignature()
        {
            int sig = 17;
            sig = sig * 31 + (int)(gameData.LocalPlayer?.Domain ?? Domains.Blue);
            sig = sig * 31 + Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
            var players = gameData.Players;
            for (int i = 0, count = players.Count; i < count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                sig = sig * 31 + (p.Name != null ? p.Name.GetHashCode() : 0);
                sig = sig * 31 + (int)p.Domain;
            }
            return sig;
        }

        /// <summary>
        /// Late roster arrival on a client (OnPlayerAdded): subscribe the new player's stats and
        /// rebuild so its domain box appears. Subscription is idempotent against re-adds.
        /// </summary>
        void HandlePlayerAdded(string playerName, Domains domain)
        {
            if (!_useDomainView) return;
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s != null && s.Name == playerName);
            if (stats != null) SubscribeToPlayerStats(stats);
            RebuildDomainPanels();
        }

        void CreateDomainPanel(Domains domain, Transform container)
        {
            if (!container || multiplayerView.DomainPanelPrefab == null) return;

            var panel = Instantiate(multiplayerView.DomainPanelPrefab, container);
            int sum = SumStatByDomain(domain);
            DomainColorSet colorSet = null;
            var themeColors = gameData?.ThemeManagerData?.ColorSet;
            if (themeColors != null) themeColors.TryGetColorSetByDomain(domain, out colorSet);

            if (colorSet != null)
                panel.Setup(domain, colorSet, sum);
            else
                panel.Setup(domain, ResolveDomainColor(domain), sum);

            // Color used to tint per-teammate avatar entries below the sum.
            var color = colorSet != null ? colorSet.ShipColor1 : ResolveDomainColor(domain);

            // Add a small icon per teammate (humans + AI on this domain), grouped by the
            // authoritative Player.Domain (NOT the derived RoundStats.Domain). Local player's name
            // is shown; others render avatar-only.
            var players = gameData.Players;
            for (int i = 0, count = players.Count; i < count; i++)
            {
                var p = players[i];
                if (p == null || p.Domain != domain) continue;
                bool isLocal = gameData.LocalPlayer != null && p.Name == gameData.LocalPlayer.Name;
                Sprite avatar = isLocal ? null : ResolveAIAvatarSprite(p.Name);
                if (avatar == null) avatar = ResolveAvatarSprite(p.AvatarId);
                panel.AddPlayerIcon(avatar, color, isLocal);
            }

            _domainPanels[domain] = panel;
        }

        // ── Stat dispatch ─────────────────────────────────────────────────────

        private void UnsubscribeFromAllStats()
        {
            foreach (var stats in _subscribedStats)
            {
                if (stats != null)
                    stats.OnAnyStatChanged -= HandlePlayerStatChanged;
            }
            _subscribedStats.Clear();
        }

        /// <summary>
        /// Mid-turn scene exits destroy the HUD without OnMiniGameTurnEnd ever firing.
        /// Mirror that cleanup here so the per-stats handlers and the OnPlayerAdded
        /// subscription always detach from the persistent objects.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (gameData != null)
                gameData.OnPlayerAdded -= HandlePlayerAdded;
            UnsubscribeFromAllStats();
        }

        /// <summary>
        /// The per-player card value = the mode's scoring metric, read from the active
        /// <see cref="GameDataSO.ScoringRule"/>. One metric drives the HUD, the "remaining"
        /// readout, the end condition and the scoreboard secondary, so they cannot diverge.
        /// </summary>
        protected virtual int GetInitialCardValue(IRoundStats stats)
            => gameData != null && gameData.ScoringRule != null
                ? gameData.ScoringRule.LiveMetric(stats)
                : 0;

        /// <summary>
        /// Subscribe to the generic stat-changed event - metric-agnostic, so the same base
        /// serves every domain mode. <see cref="HandlePlayerStatChanged"/> recomputes the card
        /// / domain sum from the rule metric.
        /// </summary>
        protected virtual void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;
            // Idempotent: OnPlayerAdded can fire for an already-tracked player and the rebuild
            // path may re-touch subscriptions - never double-invoke HandlePlayerStatChanged.
            stats.OnAnyStatChanged -= HandlePlayerStatChanged;
            stats.OnAnyStatChanged += HandlePlayerStatChanged;
            _subscribedStats.Add(stats);
        }

        protected virtual void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats == null) return;
            stats.OnAnyStatChanged -= HandlePlayerStatChanged;
            _subscribedStats.Remove(stats);
        }
        protected virtual void SubscribeToGameSpecificEvents() { }
        protected virtual void UnsubscribeFromGameSpecificEvents() { }

        /// <summary>
        /// Per-player-layout helper. <see cref="HandlePlayerStatChanged"/> dispatches here
        /// (per-player card) or to the domain panel depending on the inspector wiring.
        /// </summary>
        protected void UpdatePlayerCard(string playerName, int newValue)
        {
            if (_playerCards.TryGetValue(playerName, out var card))
                card.UpdateScore(newValue);
        }

        /// <summary>
        /// Single entry point for stat changes, invoked from each player's
        /// <see cref="IRoundStats.OnAnyStatChanged"/>. Routes the update to the active layout -
        /// per-player card or per-domain panel - and recomputes the domain sum on demand.
        /// </summary>
        protected void HandlePlayerStatChanged(IRoundStats stats)
        {
            if (stats == null) return;

            if (_useDomainView)
            {
                // Domain attribution lives on Player.Domain (authoritative). If a player moved
                // domains or the roster grew, the layout signature changes → rebuild. Sums are kept
                // current independently by RefreshDomainSums (OnDomainMetricSumsChanged) and the
                // CreateDomainPanel initial read.
                if (DomainLayoutChanged())
                    RebuildDomainPanels();
            }
            else
            {
                UpdatePlayerCard(stats.Name, GetInitialCardValue(stats));
            }
        }

        int SumStatByDomain(Domains domain)
        {
            // Read the SERVER-synced authoritative sum (MultiplayerDomainGamesController publishes it
            // via NetworkVariable → gameData). Clients no longer re-sum per-player stats here, which
            // could freeze for a client's OWN player when its own RoundStats replication lags.
            return gameData.GetDomainMetricSum(domain);
        }
    }
}
