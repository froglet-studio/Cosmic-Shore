using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage's jobs on the Voyage window (Garrett, 2026-10-28: "the jobs should be the different voyages"): one
    /// voyage card per job the squad's pilots bring from where the Ark is now, and the way home when it is away, under the
    /// window's one galaxy map (Garrett, 2026-11-01). OPEN on a card expands it to its brief and focuses the map on its
    /// course; the map stays. Set sail, on the open card, enters freestyle and sails that job
    /// (<see cref="VoyageHarbour.SetSail"/>).
    /// When the job is done the player leaves freestyle and this window offers the next jobs, from the job's destination,
    /// grown from the news the last outcome wrote.
    ///
    /// <para>The first card is the prefab's own (<see cref="template"/>); the rest are copies of it, made here. While a
    /// voyage is out the one card is the job under way, its Set sail dark.</para>
    /// </summary>
    public class VoyageJobsView : MonoBehaviour
    {
        [Tooltip("The card every job is drawn on; copies of it stand beside it, one per job.")]
        [SerializeField] VoyageJobCard template;

        [Tooltip("The vessels the brief names the squad's hulls from (their card names).")]
        [SerializeField] SO_VesselList vessels;
        [Tooltip("The galaxy map's height above the cards.")]
        [SerializeField] float mapHeight = 360f;

        const string MapName = "GalaxyMap";

        readonly List<VoyageJobCard> _cards = new();
        VoyageMapGraphic _map;
        RectTransform _mapHost;
        int _open = -1;
        ModalWindowManager _window;

        void OnEnable()
        {
            VoyageHarbour.Changed += Refresh;
            // The window is shown and hidden by its canvas group, so it is re-read on every open: the squad (and so the
            // pilots bringing jobs) may have changed since.
            _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window) _window.OnModalOpened += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            VoyageHarbour.Changed -= Refresh;
            if (_window) _window.OnModalOpened -= Refresh;
            _window = null;
        }

        /// <summary>Redraw the map and the cards from the jobs on offer now.</summary>
        public void Refresh()
        {
            if (!template) return;
            var harbour = VoyageHarbour.Current;
            var voyage = harbour ? harbour.Content : null;
            if (!harbour || !voyage || voyage.Content == null)
            {
                // No harbour stands yet (the toybox is built once the menu's client is ready): a plain card, no map.
                var shelf = voyage ? voyage : VoyageContentSO.Load();
                var card = Use(0)[0];
                card.ShowStepper(null, null, null, null);
                card.Show(string.Empty, shelf ? shelf.Lines.VoyageName : null, null, null, false, null, null, null, null);
                ShowMap(false);
                return;
            }
            var content = voyage.Content;
            var lines = voyage.Lines;
            var style = harbour.Definition;

            var escort = style ? style.ArkEscort : null;
            if (harbour.IsEscorting && escort)
            {
                var card = Use(1)[0];
                ShowEscort(card, lines, escort, harbour.EscortDifficulty, sail: false);
                ShowMap(false);
                return;
            }

            if (harbour.IsUnderway)
            {
                var job = harbour.CurrentJob;
                var cards = Use(1);
                cards[0].ShowStepper(null, null, null, null);
                if (job == null)
                {
                    cards[0].Show(string.Empty, lines.VoyageName, null, null, false, null, null, null, null);
                    Map(content, style, null, null, null);
                    return;
                }
                var to = content.Location(job.To);
                var underway = VoyageRules.Route(content, job.From, job.To);
                string course = CourseLine(content, lines, underway);
                cards[0].Show(
                    VoyageRules.Fill(lines.CardUnderway, content.Location(job.From), to, to, content.Person(job.PilotId)),
                    job.Title, course, Brief(content, lines, job, underway), true, null, null, null, null);
                Map(content, style, job.From, underway, course);
                return;
            }

            var jobs = harbour.Board(out var state);
            string here = state?.At;
            int extra = escort ? 1 : 0;
            if (jobs.Count == 0)
            {
                _open = -1;
                var none = Use(1 + extra);
                none[0].ShowStepper(null, null, null, null);
                none[0].Show(string.Empty, lines.NoWork, null, null, false, null, null, null, null);
                if (escort) ShowEscort(none[1], lines, escort, Difficulty, sail: true);
                Map(content, style, here, null, null);
                return;
            }
            if (_open >= jobs.Count) _open = -1;

            var shown = Use(jobs.Count + extra);
            if (escort) ShowEscort(shown[jobs.Count], lines, escort, Difficulty, sail: true);
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                var from = content.Location(job.From);
                var to = content.Location(job.To);
                string eyebrow = VoyageRules.Fill(job.IsReturn ? lines.CardHomeRoute : lines.CardRoute, from, to, to,
                    content.Person(job.PilotId));
                var taken = job;
                int index = i;
                bool open = i == _open;
                var route = VoyageRules.Route(content, job.From, job.To);
                string course = CourseLine(content, lines, route);
                shown[i].ShowStepper(null, null, null, null);
                shown[i].Show(eyebrow, job.Title, course, open ? Brief(content, lines, job, route) : null, open,
                    () => OpenCard(index), lines.CardOpen, lines.CardClose, () => SetSail(taken));
                if (open) Map(content, style, here, route, course);
            }
            if (_open < 0) Map(content, style, here, null, null);
        }

        /// <summary>Open card <paramref name="index"/> (closing any other), or close it if it is the open one.</summary>
        void OpenCard(int index)
        {
            _open = _open == index ? -1 : index;
            Refresh();
        }

        // ── The one galaxy map ───────────────────────────────────────────────

        /// <summary>
        /// Draw the window's one galaxy map: every place with the squad's spot, or, with a card open, that job's course
        /// framed (Garrett, 2026-11-01: the map "remains" while a card is drilled into).
        /// </summary>
        void Map(VoyageContent content, VoyageToyDefinitionSO style, string here, IReadOnlyList<string> route,
            string caption)
        {
            var map = EnsureMap();
            if (!map) return;
            ShowMap(true);
            map.Show(content, style, here, route, caption, focus: route != null);
        }

        void ShowMap(bool show)
        {
            if (_mapHost) _mapHost.gameObject.SetActive(show);
        }

        /// <summary>
        /// The map stands where the cards are, just before the first one, as wide as the column; it masks itself so a
        /// focused map's far places do not spill over the cards.
        /// </summary>
        VoyageMapGraphic EnsureMap()
        {
            if (_map) return _map;
            if (!template) return null;
            var parent = template.transform.parent;
            var existing = parent ? parent.Find(MapName) : null;
            if (existing)
            {
                _mapHost = (RectTransform)existing;
                _map = existing.GetComponentInChildren<VoyageMapGraphic>(true);
                if (_map) return _map;
            }
            else
            {
                var host = new GameObject(MapName, typeof(RectTransform));
                host.transform.SetParent(parent, false);
                host.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
                _mapHost = (RectTransform)host.transform;
                var templateRect = (RectTransform)template.transform;
                _mapHost.anchorMin = templateRect.anchorMin;
                _mapHost.anchorMax = templateRect.anchorMax;
                _mapHost.pivot = templateRect.pivot;
                _mapHost.sizeDelta = new Vector2(templateRect.sizeDelta.x, mapHeight);
                var le = host.AddComponent<LayoutElement>();
                le.preferredHeight = mapHeight;
                le.minHeight = mapHeight;
                host.AddComponent<RectMask2D>();
            }
            var go = new GameObject("Map", typeof(RectTransform));
            go.transform.SetParent(_mapHost, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _map = go.AddComponent<VoyageMapGraphic>();
            _map.SetLabelTemplate(template.LabelTemplate);
            return _map;
        }

        /// <summary>
        /// The open card's brief, the content's <see cref="VoyageLines.CardDetails"/>: the mission, the place, the squad
        /// that will sail it, the course's intensity, the pay and who brought the job.
        /// </summary>
        string Brief(VoyageContent content, VoyageLines lines, VoyageJob job, IReadOnlyList<string> route)
        {
            string template = job.IsReturn ? lines?.CardHomeDetails : lines?.CardDetails;
            if (string.IsNullOrEmpty(template)) return null;
            var to = content.Location(job.To);
            var person = content.Person(job.PilotId);
            var kind = content.Job(job.TemplateId);
            var sea = VoyageChart.For(content)?.Along(route);
            string text = VoyageRules.Fill(template, content.Location(job.From), to, to, person);
            return TemplateText.Fill(text,
                "summary", job.Summary,
                "place", to?.Blurb,
                "story", person?.Story,
                "squad", Squad(lines),
                "intensity", sea != null ? sea.Intensity.ToString(CultureInfo.InvariantCulture) : string.Empty,
                "reward", kind != null && !job.IsReturn ? kind.Reward.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>The active squad as its seats: each one's vessel and pilot, through the authored seat line.</summary>
        string Squad(VoyageLines lines)
        {
            var squad = VoyageRun.ActiveSquad(null);
            if (squad == null || string.IsNullOrEmpty(lines.CardSquadSeat)) return string.Empty;
            var catalog = PilotArtifactCatalogSO.Load();
            var seats = new List<string>();
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            {
                var slot = squad.GetSlot(i);
                string vessel = vessels && vessels.TryGetVesselByClass(slot.Vessel, out var v, reportMissing: false)
                    ? v.CardName : string.Empty;
                var pilot = catalog ? catalog.ResolvePilot(slot, null) : null;
                seats.Add(TemplateText.Fill(lines.CardSquadSeat, "vessel", vessel, "pilot", pilot ? pilot.DisplayName : string.Empty));
            }
            return string.Join(lines.CardSquadSeparator ?? string.Empty, seats);
        }

        /// <summary>
        /// The card's course line on the real HyperSea (Docs/Voyages/VOYAGE_BOARD.md): how many light years the Ark
        /// sails (light takes as many years), the thickest water on the way and how rough the course is, filled into the
        /// content's authored <see cref="VoyageLines.CardCourse"/>. Null when the content is not charted.
        /// </summary>
        static string CourseLine(VoyageContent content, VoyageLines lines, IReadOnlyList<string> route)
        {
            if (lines == null || string.IsNullOrEmpty(lines.CardCourse)) return null;
            var voyage = VoyageChart.For(content)?.Along(route);
            if (voyage == null) return null;
            string ly = voyage.LightYears.ToString("N0", CultureInfo.InvariantCulture);
            return TemplateText.Fill(lines.CardCourse,
                "ly", ly,
                "years", ly,
                "minutes", voyage.Minutes.ToString("N0", CultureInfo.InvariantCulture),
                "sea", SeaName(lines, voyage.ThickestSea),
                "intensity", voyage.Intensity.ToString(CultureInfo.InvariantCulture));
        }

        static string SeaName(VoyageLines lines, SeaKind sea) => sea switch
        {
            SeaKind.Spray => lines.SeaSpray,
            SeaKind.Froth => lines.SeaFroth,
            SeaKind.Water => lines.SeaWater,
            SeaKind.Thick => lines.SeaThick,
            _ => lines.SeaReef,
        };

        const string DifficultyKey = "ArkEscort.Difficulty";
        const int DefaultDifficulty = 3;

        /// <summary>The escort difficulty this viewer last picked (a per-device convenience).</summary>
        static int Difficulty
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, DefaultDifficulty),
                ArkEscortSO.MinDifficulty, ArkEscortSO.MaxDifficulty);
            set => PlayerPrefs.SetInt(DifficultyKey,
                Mathf.Clamp(value, ArkEscortSO.MinDifficulty, ArkEscortSO.MaxDifficulty));
        }

        /// <summary>The Ark escort's card: its title at the picked difficulty, a stepper to change it, and Set sail.</summary>
        void ShowEscort(VoyageJobCard card, VoyageLines lines, ArkEscortSO escort, int difficulty, bool sail)
        {
            string d = difficulty.ToString();
            // The voyage this escort sails (or will): its worlds in order and its modifiers, as the card's course line.
            var voyage = sail ? ArkEscortRun.PreviewVoyage(escort, difficulty) : VoyageHarbour.Current?.EscortVoyage;
            string legs = (voyage is { Legs: { Count: > 0 } } ? voyage.Legs.Count : escort.Legs.Count).ToString();
            string course = voyage is { WorldNames: { Count: > 0 } } ? voyage.Describe() : null;
            // The escort is a card of its own kind: always open, its difficulty and Set sail on show.
            card.Show(TemplateText.Fill(escort.CardLine, "legs", legs, "difficulty", d),
                TemplateText.Fill(escort.CardTitle, "difficulty", d, "legs", legs), course, null, true, null, null, null,
                sail ? () => SetSailEscort(difficulty) : null);
            if (sail)
                card.ShowStepper(escort.LowerLabel, escort.RaiseLabel,
                    () => { Difficulty = difficulty - 1; Refresh(); },
                    () => { Difficulty = difficulty + 1; Refresh(); });
            else card.ShowStepper(null, null, null, null);
        }

        static void SetSailEscort(int difficulty)
        {
            var harbour = VoyageHarbour.Current;
            if (!harbour) return;
            harbour.SetSailEscort(difficulty, FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include));
        }

        static void SetSail(VoyageJob job)
        {
            var harbour = VoyageHarbour.Current;
            if (!harbour) return;
            harbour.SetSail(job, FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include));
        }

        /// <summary><paramref name="count"/> cards showing (the template first, then copies after it), the rest hidden;
        /// at least the template stands, so the window never loses its third voyage.</summary>
        List<VoyageJobCard> Use(int count)
        {
            if (_cards.Count == 0) _cards.Add(template);
            int need = Mathf.Max(1, count);
            while (_cards.Count < need)
            {
                var copy = Instantiate(template, template.transform.parent);
                copy.name = template.name + "_" + _cards.Count;
                // A copy of a card that showed the escort's stepper carries its buttons, unowned: the copy makes its own.
                foreach (var b in copy.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (b.name is "StepDown" or "StepUp") Destroy(b.gameObject);
                copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + _cards.Count);
                _cards.Add(copy);
            }
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i]) _cards[i].gameObject.SetActive(i < need);
            return _cards;
        }
    }
}
