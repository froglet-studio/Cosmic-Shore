using System.Collections;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage lobby's part of the switcher (Docs/Voyages/PARTY.md §7; Shombith, 2026-11-21: "if the host gets into the
    /// voyages screen and selects a voyage, just like arcade the client should be pulled inside that screen for them to
    /// select their configuration"). On the host it tells the party where its Voyage window stands; on a friend's machine
    /// it follows: out of freestyle, into the Voyage window, onto the host's voyage and page, where the friend picks their
    /// own seat. The friend's window closes again when the host closes theirs, if following opened it.
    /// </summary>
    public partial class ScreenSwitcher
    {
        const float VoyageLobbyTick = 0.25f, VoyageLobbyResend = 3f;
        // A friend's Voyage window builds its board a frame or two after it opens: give it this many ticks to offer the
        // host's voyage before the follow stands down until the host moves again.
        const int VoyageFollowTries = 16;

        VoyageLobbyState _voyageLobbySent, _voyageLobbyFollowed, _voyageLobbyTrying;
        float _voyageLobbyResendAt;
        bool _voyageOpenedByFollow;
        int _voyageFollowTries;

        IEnumerator VoyageLobbyCoroutine()
        {
            var wait = new WaitForSecondsRealtime(VoyageLobbyTick);
            while (true)
            {
                yield return wait;
                VoyageLobbyLink.EnsureListening();
                if (VoyageCourseLink.HostWithParty) PublishVoyageLobby();
                else if (VoyageCourseLink.IsFollower) FollowVoyageLobby();
                else
                {
                    _voyageLobbySent = null;
                    _voyageLobbyFollowed = null;
                    _voyageOpenedByFollow = false;
                }
            }
        }

        // ── The host ─────────────────────────────────────────────────────────

        void PublishVoyageLobby()
        {
            var state = HostVoyageLobby();
            if (state.Equals(_voyageLobbySent) && Time.unscaledTime < _voyageLobbyResendAt) return;
            VoyageLobbyLink.Send(state);
            _voyageLobbySent = state;
            // Again every few seconds: a friend who joined since, or whose menu was busy, still hears it.
            _voyageLobbyResendAt = Time.unscaledTime + VoyageLobbyResend;
        }

        VoyageLobbyState HostVoyageLobby()
        {
            var mission = FindModal(ModalWindows.MISSION);
            var board = mission && mission.IsOpen ? mission.GetComponentInChildren<VoyageBoard>(true) : null;
            var open = board ? board.Open : null;
            var domain = VoyageLobbyLink.ArkDomain;
            return new VoyageLobbyState
            {
                Open = open != null && !InFreestyle,
                Key = open?.Key ?? string.Empty,
                Page = board ? (int)board.Shown : 0,
                Difficulty = board ? board.Difficulty : 0,
                Domain = (int)domain,
                VotesJade = VoyageLobbyLink.VotesFor(CosmicShore.Data.Domains.Jade),
                VotesRuby = VoyageLobbyLink.VotesFor(CosmicShore.Data.Domains.Ruby),
                VotesGold = VoyageLobbyLink.VotesFor(CosmicShore.Data.Domains.Gold),
            };
        }

        // ── A friend ─────────────────────────────────────────────────────────

        void FollowVoyageLobby()
        {
            var state = VoyageLobbyLink.Latest;
            if (state == null || !state.MovesFrom(_voyageLobbyFollowed)) return;
            if (!ReferenceEquals(state, _voyageLobbyTrying))
            {
                _voyageLobbyTrying = state;
                _voyageFollowTries = 0;
            }

            var mission = FindModal(ModalWindows.MISSION);
            if (!mission)
            {
                _voyageLobbyFollowed = state;
                return;
            }

            if (!state.Open)
            {
                // The host closed their window (or left the voyage open on nothing): close ours if following opened it.
                if (_voyageOpenedByFollow && mission.IsOpen) mission.ModalWindowOut();
                _voyageOpenedByFollow = false;
                _voyageLobbyFollowed = state;
                return;
            }

            // A flying friend cannot be shown a window: leave freestyle first, then follow once the blend has ended (the
            // exit's start closes every window again; ArcadeGameConfigureModal.HandleConfigOpenedOnClient).
            if (InFreestyle)
            {
                RequestExitFreestyle(() => { });
                return;
            }
            if (IsFreestyleSettling) return;

            if (!mission.IsOpen)
            {
                if (!OpenModal(ModalWindows.MISSION))
                {
                    _voyageLobbyFollowed = state;
                    return;
                }
                _voyageOpenedByFollow = true;
                return;
            }

            var board = mission.GetComponentInChildren<VoyageBoard>(true);
            if (board && board.Follow(state.Key, (VoyageBoard.Page)state.Page, state.Difficulty))
            {
                _voyageLobbyFollowed = state;
                return;
            }
            if (++_voyageFollowTries < VoyageFollowTries) return;
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[Voyage] The host's voyage {state.Key} is not on this board - not followed.");
            _voyageLobbyFollowed = state;
        }
    }
}
