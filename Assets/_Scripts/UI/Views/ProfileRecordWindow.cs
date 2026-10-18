using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The pilot record, opened from the Profile pilot card's expand button (Docs/MENU_REDESIGN_B.md §56, the approved
    /// Rev 10 boards; the Profile screen itself stays as it was). On a solid background with the lattice over it:
    /// <list type="bullet">
    /// <item>fly time, games and sessions (PlayerProfileData.Lifecycle);</item>
    /// <item>favourite vessel - the most flown, read from the hangar's per-vessel flight time
    /// (HangarCloudData.PreferredVessel / VesselRecord.FlightTimeSeconds, Shombith's cloud data, read only);</item>
    /// <item>favourite pilot - the player's pick (ProfileIdentity.FavouritePilot), CHANGE opens
    /// <see cref="FavouritePilotPicker"/>;</item>
    /// <item>fly time by vessel - the five most flown, as bars;</item>
    /// <item>the Audio Studio (<see cref="AudioStudioPanel"/>), its own component on the right.</item>
    /// </list>
    /// Every word is an authored field.
    /// </summary>
    public class ProfileRecordWindow : MonoBehaviour
    {
        [Serializable]
        public struct VesselRow
        {
            public CanvasGroup group;
            public Image icon;
            public TMP_Text nameText;
            [Tooltip("The bar's fill: its anchorMax.x is set to the vessel's share of the most-flown vessel's time.")]
            public RectTransform fill;
            public Image fillImage;
            public TMP_Text timeText;
        }

        [Header("Window")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Button closeButton;
        [SerializeField] FavouritePilotPicker picker;

        [Header("Data")]
        [SerializeField] SO_VesselList vessels;
        [SerializeField] SO_CaptainList captains;
        [SerializeField] AbilityLockupStyleSO lockupStyle;

        [Header("Pilot")]
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text flightTimeText;
        [SerializeField] TMP_Text gamesText;
        [SerializeField] TMP_Text sessionsText;

        [Header("Favourite vessel")]
        [SerializeField] TMP_Text favouriteVesselName;
        [SerializeField] TMP_Text favouriteVesselLine;
        [SerializeField] Image favouriteVesselIcon;

        [Header("Favourite pilot")]
        [SerializeField] TMP_Text favouritePilotName;
        [SerializeField] TMP_Text favouritePilotLine;
        [SerializeField] Image favouritePilotPortrait;
        [SerializeField] Button changePilotButton;

        [Header("Fly time by vessel")]
        [SerializeField] VesselRow[] rows = new VesselRow[5];
        [SerializeField] TMP_Text noFlightLine;

        [Header("Words (empty shows nothing)")]
        [Tooltip("Fly time with hours. {h} hours, {m} minutes.")]
        [SerializeField] string hoursTemplate = "{h}h {m}m";
        [Tooltip("Fly time under an hour. {m} minutes.")]
        [SerializeField] string minutesTemplate = "{m}m";
        [Tooltip("Under the favourite vessel. {time} is its fly time.")]
        [SerializeField] string mostFlownTemplate = "Most flown - {time}";
        [SerializeField] string noVesselName = "None yet";
        [SerializeField] string noVesselLine = "Fly any vessel and your favourite shows here.";
        [SerializeField] string noPilotName = "None picked";
        [SerializeField] string noPilotLine = "Pick the pilot you fly for.";
        [Tooltip("Under the favourite pilot. {element} and {vessel}.")]
        [SerializeField] string pilotLineTemplate = "The {element} {vessel}";
        [SerializeField] string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [SerializeField] string noFlightText = "Your fly time per vessel appears after your first flight.";

        [Inject] UGSDataService _data;

        PlayerDataService Profiles => PlayerDataService.Instance;
        GameObject _returnFocus;

        public bool IsOpen => group && group.alpha > 0f;

        void Awake()
        {
            if (!lockupStyle) lockupStyle = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            if (closeButton) closeButton.onClick.AddListener(Close);
            if (changePilotButton) changePilotButton.onClick.AddListener(OpenPicker);
            SetShown(false);
        }

        void OnEnable()
        {
            if (Profiles != null) Profiles.OnProfileChanged += OnProfileChanged;
        }

        void OnDisable()
        {
            if (Profiles != null) Profiles.OnProfileChanged -= OnProfileChanged;
            SetShown(false);
        }

        void OnProfileChanged(PlayerProfileData _)
        {
            if (IsOpen) Refresh();
        }

        public void Open()
        {
            var events = EventSystem.current;
            _returnFocus = events ? events.currentSelectedGameObject : null;
            Refresh();
            SetShown(true);
            if (events && changePilotButton) events.SetSelectedGameObject(changePilotButton.gameObject);
        }

        public void Close()
        {
            if (picker && picker.IsOpen) { picker.Close(); return; }
            SetShown(false);
            var events = EventSystem.current;
            if (events && _returnFocus) events.SetSelectedGameObject(_returnFocus);
        }

        void OpenPicker()
        {
            if (picker) picker.Open();
        }

        public void Refresh()
        {
            var profile = Profiles != null ? Profiles.CurrentProfile : null;
            Set(nameText, profile?.Identity.DisplayName ?? string.Empty);
            var life = profile?.Lifecycle;
            Set(flightTimeText, life != null ? FlightTime(life.TotalFlightTimeSeconds) : string.Empty);
            Set(gamesText, life != null ? life.GamesCompleted.ToString("N0", CultureInfo.InvariantCulture) : string.Empty);
            Set(sessionsText, life != null ? life.SessionCount.ToString("N0", CultureInfo.InvariantCulture) : string.Empty);
            RefreshVessels();
            RefreshPilot(profile?.Identity.FavouritePilot);
        }

        void RefreshVessels()
        {
            var hangar = _data != null && _data.Hangar != null && _data.Hangar.IsLoaded ? _data.Hangar.Data : null;
            var flown = new List<(VesselClassType type, string name, float seconds)>();
            if (hangar?.Vessels != null)
                foreach (var kv in hangar.Vessels)
                    if (kv.Value != null && kv.Value.FlightTimeSeconds >= 60f && Enum.TryParse(kv.Key, out VesselClassType type))
                        flown.Add((type, kv.Key, kv.Value.FlightTimeSeconds));
            flown.Sort((a, b) => b.seconds.CompareTo(a.seconds));

            if (flown.Count > 0)
            {
                var top = flown[0];
                var vessel = Find(top.type);
                Set(favouriteVesselName, vessel ? vessel.Name : top.name);
                Set(favouriteVesselLine, TemplateText.Fill(mostFlownTemplate, "time", FlightTime(top.seconds)));
                SetSprite(favouriteVesselIcon, vessel ? vessel.IconActive : null);
            }
            else
            {
                Set(favouriteVesselName, noVesselName);
                Set(favouriteVesselLine, noVesselLine);
                SetSprite(favouriteVesselIcon, null);
            }

            float best = flown.Count > 0 ? flown[0].seconds : 1f;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                bool used = i < flown.Count;
                KitVisibility.SetShown(row.group, used);
                if (!used) continue;
                var vessel = Find(flown[i].type);
                Set(row.nameText, vessel ? vessel.Name : flown[i].name);
                Set(row.timeText, FlightTime(flown[i].seconds));
                SetSprite(row.icon, vessel ? vessel.IconActive : null);
                if (row.fill) row.fill.anchorMax = new Vector2(Mathf.Clamp(flown[i].seconds / best, 0.02f, 1f), row.fill.anchorMax.y);
                if (row.fillImage && lockupStyle && vessel) row.fillImage.color = lockupStyle.ColourFor(vessel.PrimaryElement);
            }
            Set(noFlightLine, flown.Count == 0 ? noFlightText : string.Empty);
        }

        void RefreshPilot(string key)
        {
            var pilot = FindPilot(key);
            if (!pilot)
            {
                Set(favouritePilotName, noPilotName);
                Set(favouritePilotLine, noPilotLine);
                SetSprite(favouritePilotPortrait, null);
                return;
            }
            Set(favouritePilotName, pilot.Name);
            Set(favouritePilotLine, PilotLine(pilot));
            SetSprite(favouritePilotPortrait, pilot.Image);
        }

        public string PilotLine(SO_Captain pilot)
        {
            int e = (int)pilot.PrimaryElement - 1;
            string element = e >= 0 && e < elementNames.Length ? elementNames[e] : string.Empty;
            return TemplateText.Fill(pilotLineTemplate, "element", element, "vessel", pilot.Vessel ? pilot.Vessel.Name : string.Empty);
        }

        public string FlightTime(float seconds) => ProfileScreen.FormatFlightTime(seconds, hoursTemplate, minutesTemplate);

        SO_Vessel Find(VesselClassType type)
        {
            if (vessels == null || vessels.VesselList == null) return null;
            foreach (var v in vessels.VesselList)
                if (v && v.Class == type) return v;
            return null;
        }

        SO_Captain FindPilot(string key)
        {
            if (string.IsNullOrEmpty(key) || captains == null || captains.CaptainList == null) return null;
            foreach (var c in captains.CaptainList)
                if (c && c.name == key) return c;
            return null;
        }

        void SetShown(bool on)
        {
            if (!group) return;
            group.alpha = on ? 1f : 0f;
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s ?? string.Empty;
        }

        static void SetSprite(Image img, Sprite sprite)
        {
            if (!img) return;
            img.sprite = sprite;
            img.enabled = sprite;
        }
    }
}
