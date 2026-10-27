using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    // Part of ArcadeGameConfigureModal (Docs/REFACTOR_BACKLOG.md M13): this region moved here verbatim from ArcadeGameConfigureModal.cs.
    public partial class ArcadeGameConfigureModal
    {
        #region Config change handlers

        void HandleIntensitySelected(int intensity)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return; // Clients cannot change intensity
            if (_weeklyChallengeLocked) return; // The challenge's terms are not negotiable

            intensity        = Mathf.Clamp(intensity, _selectedGame.MinIntensity, _selectedGame.MaxIntensity);
            bool changed     = config.Intensity != intensity;
            config.Intensity = intensity;

            foreach (var button in ActiveIntensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            if (_activePanel) _activePanel.HandleIntensityChanged(intensity);

            // The preview IS the mode's real cell at this intensity, so a changed number has to
            // rebuild the arena - leaving the old world under the new label would be the stale
            // frame the whole preview design exists to make impossible.
            if (changed) ArmPreviewForGame(_selectedGame, ResolvePreviewDefinition(_selectedGame.Mode));

            // Clients follow: their modal shows the host's number and their own LOCAL preview
            // rebuilds to it (kicking them out of flight when the arena differs - the session's
            // SetDefinition unwinds an active flight before standing the new world).
            if (changed && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyIntensityChanged(intensity);

            SyncGameDataConfig();
        }

        /// <summary>
        /// The host moved the intensity row while this client's lobby is open. Mirror the host's
        /// own handler minus authority: clamp, reflect on the row, and re-arm the LOCAL preview -
        /// the session rebuilds only when the arena actually differs, and a client who is IN the
        /// microgame at the old intensity is unwound out of it first (SetDefinition stops an
        /// active flight), exactly the "host changed it, I leave and can tap back in" flow.
        /// </summary>
        void HandleIntensityChangedOnClient(int intensity)
        {
            if (!IsClientMode || _selectedGame == null || config == null) return;

            intensity        = Mathf.Clamp(intensity, _selectedGame.MinIntensity, _selectedGame.MaxIntensity);
            bool changed     = config.Intensity != intensity;
            config.Intensity = intensity;

            foreach (var button in ActiveIntensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            if (_activePanel) _activePanel.HandleIntensityChanged(intensity);

            if (changed) ArmPreviewForGame(_selectedGame, ResolvePreviewDefinition(_selectedGame.Mode));
        }

        /// <summary>
        /// Whether this card shows the AI difficulty row: the mode's AI reads the setting
        /// (<see cref="AIDifficultyRules.IsOfferedFor"/>), the active panel has the row, and it is
        /// not the host's weekly challenge - whose terms are pinned, difficulty included. A guest
        /// never runs a weekly challenge (it only mirrors a lobby), so the lock is read on the
        /// host's side only.
        /// </summary>
        bool OffersAIDifficulty =>
            _selectedGame != null && _activePanel && _activePanel.HasAIDifficultyRow &&
            AIDifficultyRules.IsOfferedFor(_selectedGame.Mode) &&
            (IsClientMode || !_weeklyChallengeLocked);

        /// <summary>
        /// The AI difficulty row on every card (Rev 7, approved 2026-10-17: "the AI difficulty row shows, unlocked, wherever
        /// the mode supports it"; Shombith 2026-10-26: "you removed the easy medium hard difficulty UI please add that
        /// too"): lit on the config's value and pickable where the card offers it, shown LOCKED on the default where the
        /// mode's AI has one level. Passed every time, because the panel is a shared scene object and a row unlocked for
        /// Skim Race must not stay unlocked on the next card.
        /// </summary>
        void ApplyAIDifficultyPresentation()
        {
            if (!_activePanel || config == null) return;

            bool offered = OffersAIDifficulty;
            bool shown = _selectedGame != null && _activePanel.HasAIDifficultyRow;
            _activePanel.SetAIDifficultyAvailable(shown);
            if (!shown) return;
            _activePanel.ShowAIDifficulty(AIDifficultyRules.IsOfferedFor(_selectedGame.Mode)
                ? ShownAIDifficulty : AIDifficultyRules.Default);
            _activePanel.SetAIDifficultyOffered(offered);
            RefreshSeatLineup();     // each AI pedestal names its own level
        }

        /// <summary>
        /// Whether each AI can carry a difficulty of its own on this card: every card that offers the AI row, Arena
        /// and Arcade alike. Selecting an AI's pedestal points the row at that AI (Shombith, 2026-10-27: "if the UI
        /// already lets you change the AI vessel there, it should let you change the other values tied to that AI
        /// too"; Yash, 2026-10-27: "anywhere where there is AI ... This should also happen in arcade"). With no AI
        /// selected the row sets every AI at once, as before.
        /// </summary>
        bool SeatDifficultyAvailable => OffersAIDifficulty && config != null;

        /// <summary>The AI seat whose difficulty the row edits now, or -1 when it edits every AI (the card's).</summary>
        int EditedAiSeatForDifficulty =>
            SeatDifficultyAvailable && _vesselSeat != OwnVesselSeat && MaySelectAiSeat(_vesselSeat) ? _vesselSeat : -1;

        /// <summary>The level the row lights: the selected AI's own (its pick, else the card's), or the card's.</summary>
        AIDifficulty ShownAIDifficulty
        {
            get
            {
                int seat = EditedAiSeatForDifficulty;
                return seat >= 0 ? config.AIDifficultyAt(seat) : AIDifficultyRules.Resolve(config.AIDifficulty);
            }
        }

        /// <summary>Light the row on the level it edits now (the selection moved, or a value arrived).</summary>
        void ShowAIDifficultyForEditedSeat()
        {
            if (!_activePanel || config == null || _selectedGame == null || !_activePanel.HasAIDifficultyRow) return;
            if (!AIDifficultyRules.IsOfferedFor(_selectedGame.Mode)) return;
            _activePanel.ShowAIDifficulty(ShownAIDifficulty);
        }

        /// <summary>
        /// The host pressed a button on the AI difficulty row. Host only, like intensity. With an AI's pedestal
        /// selected it sets THAT AI's difficulty (replicated with the roster, so every guest's pedestal shows it);
        /// otherwise it sets every AI: the card's difficulty, replicated and remembered with the host terms, and
        /// every seat's own level is cleared so the press means what it says.
        /// </summary>
        void HandleAIDifficultySelected(AIDifficulty difficulty)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return;           // Only the host decides how the AI flies
            if (!OffersAIDifficulty) return;    // Includes the weekly challenge, whose terms are pinned

            difficulty = AIDifficultyRules.Resolve(difficulty);
            config.SyncAIVesselSlots();

            int seat = EditedAiSeatForDifficulty;
            if (seat >= 0)
            {
                bool seatChanged = config.AIDifficulties[seat] != difficulty;
                config.AIDifficulties[seat] = difficulty;
                ShowAIDifficultyForEditedSeat();
                if (!seatChanged) return;
                BroadcastRosterToClients();
                RefreshSeatLineup();
                return;
            }

            bool changed = config.AIDifficulty != difficulty;
            bool hadSeatLevels = config.AnySeatDifficulty();
            config.AIDifficulty = difficulty;
            for (int i = 0; i < config.AIDifficulties.Count; i++) config.AIDifficulties[i] = 0;

            ShowAIDifficultyForEditedSeat();

            if (changed && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyAIDifficultyChanged((int)difficulty);
            if (hadSeatLevels) BroadcastRosterToClients();
            if (changed || hadSeatLevels) RefreshSeatLineup();
        }

        /// <summary>The host changed the AI difficulty while this guest's lobby is open: mirror it.</summary>
        void HandleAIDifficultyChangedOnClient(int difficulty)
        {
            if (!IsClientMode || _selectedGame == null || config == null) return;

            config.AIDifficulty = AIDifficultyRules.Resolve(difficulty);
            ShowAIDifficultyForEditedSeat();
            RefreshSeatLineup();
        }

        /// <summary>The gamepad's left/right on the difficulty row: one step easier or harder, from the level it
        /// shows (the selected AI's, or every AI's).</summary>
        void CycleAIDifficulty(int direction)
        {
            if (config == null || !OffersAIDifficulty) return;
            HandleAIDifficultySelected(AIDifficultyRules.Step(ShownAIDifficulty, direction));
        }

        void HandlePlayerCountSelected(int playerCount)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return;

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            int pcMax = Mathf.Min(_selectedGame.MaxSeats, MaxSupportedPlayers);
            // A party can be LARGER than the card allows (four humans on a 3-player card), and
            // then effectiveMin > pcMax. Mathf.Clamp resolves an inverted range by returning the
            // MAX, so the count silently came back as fewer players than are actually present -
            // and SelectedPlayerCount is what sizes the spawn ring, so two pilots spawned on the
            // same pose, interpenetrating. The party is the fact on the ground; the card's
            // ceiling is a preference, so the party wins and the stepper is simply pinned.
            playerCount        = effectiveMin > pcMax
                ? effectiveMin
                : Mathf.Clamp(playerCount, effectiveMin, pcMax);
            config.PlayerCount = playerCount;

            if (pcStepper)
                pcStepper.SetValue(playerCount);

            // PC change may shrink the DC bound (DC <= PC). Re-clamp + re-bound the DC stepper.
            int newDcMax = ComputeMaxDomainCount();
            int newDc = Mathf.Clamp(config.DomainCount, MinDomainsForGame, newDcMax);
            if (newDc != config.DomainCount)
                config.DomainCount = newDc;
            if (dcStepper)
                dcStepper.Initialize(MinDomainsForGame, newDcMax, config.DomainCount);

            RefreshTileVisibility();
            RefreshRoster();
            SyncGameDataConfig();
        }

        #endregion
    }
}
