using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The settings window's REGION tab: which Relay region the sessions this player hosts are served from
    /// (<see cref="RelayRegionPreference"/>), with this player's measured ping to every region
    /// (<see cref="RelayRegionProbe"/>) so a party spread over continents can pick the fair middle.
    ///
    /// <para>The host's region is the party's region; a guest's choice applies to the parties it hosts later. A solo
    /// host in the main menu moves its party session to the new region when Settings closes
    /// (<see cref="PartyInviteController.MoveSoloHostToChosenRegionAsync"/>); with guests aboard, in a match or in a
    /// friend's party the choice is saved and applies the next time this player hosts. The status line under the
    /// dropdown says which of those is happening (<see cref="Classify"/>).</para>
    /// </summary>
    public class RegionSettingsView : MonoBehaviour
    {
        /// <summary>A Relay region id and the name a player reads for it.</summary>
        [Serializable]
        public struct RegionName
        {
            [Tooltip("The Relay region id, as RelayService.ListRegionsAsync returns it (for example europe-west4).")]
            public string id;
            [Tooltip("What the player reads. Empty falls back to UGS's own description, then the id.")]
            public string displayName;
        }

        /// <summary>What a region change does from where this player is now.</summary>
        public enum Status
        {
            /// <summary>The hosted party session is already in the chosen region.</summary>
            Current = 0,
            /// <summary>A solo host in the menu: the session moves when Settings closes.</summary>
            MovesOnClose = 1,
            /// <summary>Saved; it applies the next party this player hosts (guests aboard, in a match, or not online).</summary>
            NextHostedParty = 2,
            /// <summary>In a friend's party: the host's region is used.</summary>
            FriendsParty = 3,
        }

        [Header("Controls")]
        [SerializeField] TMP_Dropdown regionDropdown;
        [SerializeField, Tooltip("Under the dropdown: what a change does from here (Status), and whether ping was measured.")]
        TMP_Text statusText;
        [SerializeField, Tooltip("Measures the ping to every region again.")]
        Button measureButton;

        [Header("Regions")]
        [SerializeField, Tooltip("Names for the Relay region ids. Also the regions offered when UGS cannot be reached to list them.")]
        RegionName[] regionNames = Array.Empty<RegionName>();

        [Header("Words (empty shows nothing)")]
        [SerializeField] string autoOption = "Auto - closest to you";
        [Tooltip("{region} is the region with the lowest ping, {ping} its ping in ms.")]
        [SerializeField] string autoOptionMeasured = "Auto - closest to you ({region}, {ping} ms)";
        [Tooltip("{region} is the region's name.")]
        [SerializeField] string regionOption = "{region}";
        [Tooltip("{region} is the region's name, {ping} this player's ping to it in ms.")]
        [SerializeField] string regionOptionMeasured = "{region}  -  {ping} ms";
        [SerializeField] string measuringLine = "Measuring your ping to each region...";
        [SerializeField] string noPingLine = "Ping could not be measured right now. You can still choose a region.";
        [Tooltip("{region} is the chosen region's name.")]
        [SerializeField] string currentLine = "Your party server is in {region}. Friends you invite join it there.";
        [Tooltip("{region} is the chosen region's name.")]
        [SerializeField] string movesOnCloseLine = "Your party server moves to {region} when you close Settings.";
        [SerializeField] string nextHostedPartyLine = "Saved. It applies to the next party you host.";
        [SerializeField] string friendsPartyLine = "You are in a friend's party, so the host's region is used. Yours applies to parties you host.";
        [SerializeField] string autoRegionName = "the region closest to you";

        [Inject] UgsRequestPolicy _requestPolicy;
        [Inject] SceneNameListSO _sceneNames;

        readonly List<string> _ids = new();
        IReadOnlyList<RelayRegionInfo> _measured;
        bool _measuring;
        bool _changedThisOpen;
        int _probeGeneration;
        ModalWindowManager _window;

        void Awake()
        {
            if (regionDropdown != null)
            {
                regionDropdown.onValueChanged.AddListener(OnRegionPicked);
                SettingsRowDropdownHitArea.Attach(regionDropdown);
            }
            if (measureButton != null) measureButton.onClick.AddListener(() => Measure(force: true));
        }

        // The SETTINGS modal never deactivates when it closes (ModalWindowManager.ModalWindowOut skips DisableWindow for
        // it), so OnEnable / OnDisable are scene load and unload, not open and close. Opening measures; closing applies -
        // both from the window's own events, as VoyageWindowTabs does.
        void OnEnable()
        {
            RelayRegionPreference.Changed += OnPreferenceChanged;
            _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window)
            {
                _window.OnModalOpened += Opened;
                _window.OnModalClosed += Closed;
            }
            if (!_window || _window.IsOpen) Opened();
            else Rebuild();
        }

        void OnDisable()
        {
            RelayRegionPreference.Changed -= OnPreferenceChanged;
            if (_window)
            {
                _window.OnModalOpened -= Opened;
                _window.OnModalClosed -= Closed;
            }
            _window = null;
            Closed();
        }

        void Opened()
        {
            _changedThisOpen = false;
            _measured = RelayRegionProbe.Last ?? _measured;
            Rebuild();
            Measure(force: false);
        }

        void Closed()
        {
            _probeGeneration++;   // a probe still running finishes into nothing
            _measuring = false;
            if (measureButton != null) measureButton.interactable = true;
            if (_changedThisOpen) ApplyAfterCloseAsync().Forget();
            _changedThisOpen = false;
        }

        void OnPreferenceChanged(string _) => Rebuild();

        void OnRegionPicked(int index)
        {
            if (index < 0 || index >= _ids.Count) return;
            _changedThisOpen = true;
            RelayRegionPreference.Set(_ids[index]);   // raises Changed → Rebuild
        }

        // ── Measuring ────────────────────────────────────────────────────────

        void Measure(bool force)
        {
            if (!RelayRegionProbe.CanProbe)
            {
                Rebuild();
                return;
            }
            MeasureAsync(force, ++_probeGeneration).Forget();
        }

        async UniTaskVoid MeasureAsync(bool force, int generation)
        {
            _measuring = true;
            if (measureButton != null) measureButton.interactable = false;
            Rebuild();
            IReadOnlyList<RelayRegionInfo> result;
            try { result = await RelayRegionProbe.ProbeAsync(_requestPolicy, force); }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[RegionSettingsView] Region probe failed ({e.GetType().Name}: {e.Message}).");
                result = Array.Empty<RelayRegionInfo>();
            }
            if (this == null || generation != _probeGeneration) return;   // closed, or a newer probe started
            _measuring = false;
            if (measureButton != null) measureButton.interactable = true;
            if (result.Count > 0) _measured = result;
            Rebuild();
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        void Rebuild()
        {
            if (regionDropdown != null)
            {
                var labels = BuildOptions(out int selected);
                regionDropdown.ClearOptions();
                regionDropdown.AddOptions(labels);
                if (selected >= 0 && selected < regionDropdown.options.Count) regionDropdown.SetValueWithoutNotify(selected);
                regionDropdown.RefreshShownValue();
            }
            if (statusText != null) statusText.text = StatusLine();
        }

        List<string> BuildOptions(out int selected)
        {
            _ids.Clear();
            var labels = new List<string>();
            var measured = _measured;
            bool anyPing = false;
            if (measured != null) foreach (var r in measured) anyPing |= r.HasPing;

            // Auto first: Relay's own pick, the region closest to the HOST (this player, for the parties it hosts).
            _ids.Add(string.Empty);
            if (anyPing && measured[0].HasPing)
                labels.Add(TemplateText.Fill(autoOptionMeasured, "region", NameOf(measured[0].Id, measured[0].Description),
                                             "ping", measured[0].PingMs.ToString()));
            else
                labels.Add(autoOption ?? string.Empty);

            if (measured != null && measured.Count > 0)
            {
                foreach (var r in measured)
                {
                    _ids.Add(r.Id);
                    labels.Add(r.HasPing
                        ? TemplateText.Fill(regionOptionMeasured, "region", NameOf(r.Id, r.Description), "ping", r.PingMs.ToString())
                        : TemplateText.Fill(regionOption, "region", NameOf(r.Id, r.Description)));
                }
            }
            else
            {
                foreach (var n in regionNames)
                {
                    var id = RelayRegionPreference.Normalize(n.id);
                    if (id.Length == 0 || _ids.Contains(id)) continue;
                    _ids.Add(id);
                    labels.Add(TemplateText.Fill(regionOption, "region", NameOf(id, null)));
                }
            }

            // The saved choice stays visible even when this listing does not carry it.
            var chosen = RelayRegionPreference.RegionId;
            selected = _ids.IndexOf(chosen);
            if (selected < 0)
            {
                _ids.Add(chosen);
                labels.Add(TemplateText.Fill(regionOption, "region", NameOf(chosen, null)));
                selected = _ids.Count - 1;
            }
            return labels;
        }

        string StatusLine()
        {
            if (_measuring) return measuringLine ?? string.Empty;
            var chosenName = RelayRegionPreference.IsAuto ? autoRegionName : NameOf(RelayRegionPreference.RegionId, null);
            var line = CurrentStatus() switch
            {
                Status.Current => TemplateText.Fill(currentLine, "region", chosenName),
                Status.MovesOnClose => TemplateText.Fill(movesOnCloseLine, "region", chosenName),
                Status.FriendsParty => friendsPartyLine ?? string.Empty,
                _ => nextHostedPartyLine ?? string.Empty,
            };
            bool measured = false;
            if (_measured != null) foreach (var r in _measured) measured |= r.HasPing;
            if (!measured && !string.IsNullOrEmpty(noPingLine))
                line = string.IsNullOrEmpty(line) ? noPingLine : line + "\n" + noPingLine;
            return line;
        }

        /// <summary>The name a player reads for a region id: the authored name, then UGS's description, then the id.</summary>
        string NameOf(string id, string description)
        {
            foreach (var n in regionNames)
                if (!string.IsNullOrEmpty(n.displayName) && string.Equals(RelayRegionPreference.Normalize(n.id), id, StringComparison.Ordinal))
                    return n.displayName;
            if (!string.IsNullOrEmpty(description)) return description;
            if (_measured != null)
                foreach (var r in _measured)
                    if (r.Id == id && !string.IsNullOrEmpty(r.Description)) return r.Description;
            return id;
        }

        // ── Applying ─────────────────────────────────────────────────────────

        Status CurrentStatus()
        {
            var hcs = HostConnectionService.Instance;
            var data = hcs != null ? hcs.ConnectionData : null;
            bool online = hcs != null && !hcs.IsOfflineSession;
            bool hasSession = hcs != null && hcs.PartySession != null;
            bool isHost = data != null && data.IsPartyHost;
            int others = data != null ? Mathf.Max(0, data.DistinctPartyMemberCount - 1) : 0;
            return Classify(online, hasSession, isHost, others, InMainMenu(), RelayRegionPreference.HostedSessionIsStale);
        }

        /// <summary>
        /// What a region change does from here. Pure, so it is tested without a session: no online session → it
        /// waits for the next hosted party; a guest → the host's region is used; a host with guests, or outside the
        /// main menu → saved for the next hosted party when the session is in another region; a solo host in the main
        /// menu → the session moves on close.
        /// </summary>
        public static Status Classify(bool online, bool hasSession, bool isHost, int otherMembers, bool inMainMenu, bool hostedSessionIsStale)
        {
            if (!online || !hasSession) return Status.NextHostedParty;
            if (!isHost) return Status.FriendsParty;
            if (!hostedSessionIsStale) return Status.Current;
            if (otherMembers > 0 || !inMainMenu) return Status.NextHostedParty;
            return Status.MovesOnClose;
        }

        bool InMainMenu()
        {
            var menu = _sceneNames != null && !string.IsNullOrEmpty(_sceneNames.MainMenuScene) ? _sceneNames.MainMenuScene : "Menu_Main";
            return SceneManager.GetActiveScene().name == menu;
        }

        /// <summary>
        /// Settings closed after a change: a solo host in the main menu moves its session now. Waits a frame first, so
        /// a close that is really the scene unloading (this view destroyed) does nothing.
        /// </summary>
        async UniTaskVoid ApplyAfterCloseAsync()
        {
            await UniTask.NextFrame();
            if (this == null) return;
            if (CurrentStatus() != Status.MovesOnClose) return;
            var controller = PartyInviteController.Instance;
            if (controller == null || controller.IsTransitioning) return;
            try { await controller.MoveSoloHostToChosenRegionAsync(); }
            catch (Exception e)
            {
                CSDebug.LogWarning($"[RegionSettingsView] Moving the party session to the chosen region failed ({e.GetType().Name}: {e.Message}).");
            }
        }
    }
}
