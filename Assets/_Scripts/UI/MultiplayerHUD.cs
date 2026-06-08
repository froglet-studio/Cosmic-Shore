using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Multiplayer HUD shared by HexRace, Joust, and Crystal Capture.
    /// When the MultiplayerHUDView has the domain-panel wiring assigned (ally /
    /// opposing containers + DomainScorePanel prefab), the HUD shows one card
    /// per active domain (Jade / Ruby / Gold) — sum on top, smaller player
    /// avatars underneath. When the wiring is missing it falls back to the
    /// legacy per-player layout in PlayerScoreContainer so scenes that haven't
    /// been updated keep working.
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

        protected override void OnEnable()
        {
            base.OnEnable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += RefreshAllProgress;
                gameData.OnResetForReplay.OnRaised += ResetAllProgress;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= RefreshAllProgress;
                gameData.OnResetForReplay.OnRaised -= ResetAllProgress;
            }
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

            SubscribeToGameSpecificEvents();
        }

        protected override void OnMiniGameTurnEnd()
        {
            base.OnMiniGameTurnEnd();
            UnsubscribeFromAllStats();
            UnsubscribeFromGameSpecificEvents();
            _playerCards.Clear();
            _domainPanels.Clear();
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
            multiplayerView.ClearDomainPanels();
            _domainPanels.Clear();
            AssignAIProfiles();

            // Subscribe to every player's stat events so any teammate's update
            // can recompute the domain sum.
            foreach (var stats in gameData.RoundStatsList.Where(s => s != null))
                SubscribeToPlayerStats(stats);

            var localDomain = gameData.LocalPlayer?.Domain ?? Domains.Blue;
            int dc = Mathf.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);

            // Local player's own domain panel goes in the LEFT (ally) container —
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
                bool anyPlayers = gameData.RoundStatsList.Any(s => s != null && s.Domain == d);
                if (!anyPlayers) continue;
                CreateDomainPanel(d, multiplayerView.OpposingDomainsContainer);
            }
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

            // Add a small icon per teammate (humans + AI on this domain). Local
            // player's name is shown; others render avatar-only.
            var teammates = gameData.RoundStatsList
                .Where(s => s != null && s.Domain == domain)
                .ToList();
            foreach (var s in teammates)
            {
                bool isLocal = gameData.LocalPlayer != null && s.Name == gameData.LocalPlayer.Name;
                var avatar = ResolveAvatarForStats(s, isLocal);
                panel.AddPlayerIcon(s.Name, avatar, color, isLocal);
            }

            _domainPanels[domain] = panel;
        }

        Sprite ResolveAvatarForStats(IRoundStats stats, bool isLocal)
        {
            Sprite sprite = null;
            if (!isLocal)
                sprite = ResolveAIAvatarSprite(stats.Name);

            if (sprite == null)
            {
                var player = gameData.Players.FirstOrDefault(p => p.Name == stats.Name);
                if (player != null) sprite = ResolveAvatarSprite(player.AvatarId);
            }
            return sprite;
        }

        // ── Stat dispatch ─────────────────────────────────────────────────────

        private void UnsubscribeFromAllStats()
        {
            foreach (var stats in gameData.RoundStatsList)
                UnsubscribeFromPlayerStats(stats);
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
        /// Subscribe to the generic stat-changed event — metric-agnostic, so the same base
        /// serves every domain mode. <see cref="HandlePlayerStatChanged"/> recomputes the card
        /// / domain sum from the rule metric.
        /// </summary>
        protected virtual void SubscribeToPlayerStats(IRoundStats stats)
        {
            if (stats != null) stats.OnAnyStatChanged += HandlePlayerStatChanged;
        }

        protected virtual void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            if (stats != null) stats.OnAnyStatChanged -= HandlePlayerStatChanged;
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
        /// <see cref="IRoundStats.OnAnyStatChanged"/>. Routes the update to the active layout —
        /// per-player card or per-domain panel — and recomputes the domain sum on demand.
        /// </summary>
        protected void HandlePlayerStatChanged(IRoundStats stats)
        {
            if (stats == null) return;

            if (_useDomainView)
            {
                if (_domainPanels.TryGetValue(stats.Domain, out var panel))
                    panel.UpdateSum(SumStatByDomain(stats.Domain));
            }
            else
            {
                UpdatePlayerCard(stats.Name, GetInitialCardValue(stats));
            }
        }

        int SumStatByDomain(Domains domain)
        {
            int sum = 0;
            var list = gameData.RoundStatsList;
            for (int i = 0, count = list.Count; i < count; i++)
            {
                var s = list[i];
                if (s != null && s.Domain == domain) sum += GetInitialCardValue(s);
            }
            return sum;
        }
    }
}
