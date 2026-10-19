using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Choose your favourite pilot (Docs/MENU_REDESIGN_B.md §56, the R10FavPilot board), over the pilot record. The same
    /// pilots the Hangar's Pilots tab shows (SO_CaptainList), four per vessel; LB / RB (or the arrows) page through the
    /// vessels. A tile press marks it; SET AS FAVOURITE saves it to the profile (ProfileIdentity.FavouritePilot, the
    /// pilot asset's name) and closes.
    /// </summary>
    public class FavouritePilotPicker : MonoBehaviour
    {
        [Serializable]
        public struct PilotTile
        {
            public CanvasGroup group;
            public KitTile tile;
            public Image portrait;
            public TMP_Text nameText;
            public TMP_Text lineText;
            [Tooltip("Shown on the pilot that is the favourite now.")]
            public GameObject favouriteMark;
        }

        [SerializeField] CanvasGroup group;
        [SerializeField] SO_CaptainList captains;
        [SerializeField] ProfileRecordWindow record;
        [SerializeField] TMP_Text vesselText;
        [SerializeField] Button previousButton;
        [SerializeField] Button nextButton;
        [SerializeField] PilotTile[] tiles = new PilotTile[4];
        [SerializeField] Button setButton;
        [SerializeField] Button backButton;

        readonly List<List<SO_Captain>> _byVessel = new();
        int _page;
        int _marked;
        GameObject _returnFocus;

        public bool IsOpen => group && group.alpha > 0f;

        void Awake()
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                int index = i;
                if (tiles[i].tile) tiles[i].tile.onClick.AddListener(() => Mark(index));
            }
            if (previousButton) previousButton.onClick.AddListener(() => Page(-1));
            if (nextButton) nextButton.onClick.AddListener(() => Page(1));
            if (setButton) setButton.onClick.AddListener(SetFavourite);
            if (backButton) backButton.onClick.AddListener(Close);
            SetShown(false);
        }

        void OnDisable() => SetShown(false);

        public void Open()
        {
            Build();
            if (_byVessel.Count == 0) return;
            string fav = PlayerDataService.Instance?.CurrentProfile?.Identity.FavouritePilot;
            _page = 0;
            _marked = 0;
            for (int p = 0; p < _byVessel.Count; p++)
            {
                int i = _byVessel[p].FindIndex(c => c.name == fav);
                if (i >= 0) { _page = p; _marked = i; break; }
            }
            var events = EventSystem.current;
            _returnFocus = events ? events.currentSelectedGameObject : null;
            Draw();
            SetShown(true);
            SelectMarked();
        }

        public void Close()
        {
            SetShown(false);
            var events = EventSystem.current;
            if (events && _returnFocus) events.SetSelectedGameObject(_returnFocus);
        }

        void Build()
        {
            _byVessel.Clear();
            if (captains == null || captains.CaptainList == null) return;
            var groups = new SortedDictionary<int, List<SO_Captain>>();
            foreach (var c in captains.CaptainList)
            {
                if (!c || !c.Vessel) continue;
                int key = (int)c.Vessel.Class;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<SO_Captain>();
                list.Add(c);
            }
            foreach (var list in groups.Values)
            {
                list.Sort((a, b) => ((int)a.PrimaryElement).CompareTo((int)b.PrimaryElement));
                _byVessel.Add(list);
            }
        }

        void Page(int step)
        {
            if (_byVessel.Count == 0) return;
            _page = (_page + step + _byVessel.Count) % _byVessel.Count;
            _marked = 0;
            Draw();
            SelectMarked();
        }

        void Mark(int index)
        {
            _marked = index;
            Draw();
        }

        void SetFavourite()
        {
            var list = _byVessel.Count > 0 ? _byVessel[_page] : null;
            if (list == null || _marked >= list.Count) return;
            PlayerDataService.Instance?.SetFavouritePilot(list[_marked].name);
            Close();
        }

        void Draw()
        {
            var list = _byVessel[_page];
            string fav = PlayerDataService.Instance?.CurrentProfile?.Identity.FavouritePilot;
            if (vesselText) vesselText.text = list.Count > 0 && list[0].Vessel ? list[0].Vessel.Name : string.Empty;
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                bool used = i < list.Count;
                KitVisibility.SetShown(t.group, used);
                if (!used) continue;
                var p = list[i];
                if (t.portrait) { t.portrait.sprite = p.Image; t.portrait.enabled = p.Image; }
                if (t.nameText) t.nameText.text = p.Name;
                if (t.lineText) t.lineText.text = record ? record.PilotLine(p) : string.Empty;
                if (t.favouriteMark) t.favouriteMark.SetActive(p.name == fav);
                if (t.tile) t.tile.IsSelected = i == _marked;
            }
        }

        void SelectMarked()
        {
            var events = EventSystem.current;
            if (events && _marked < tiles.Length && tiles[_marked].tile) events.SetSelectedGameObject(tiles[_marked].tile.gameObject);
        }

        void SetShown(bool on)
        {
            if (!group) return;
            group.alpha = on ? 1f : 0f;
            group.interactable = on;
            group.blocksRaycasts = on;
        }
    }
}
