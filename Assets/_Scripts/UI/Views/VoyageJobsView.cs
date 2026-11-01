using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage's jobs on the Voyage window (Garrett, 2026-10-28: "the jobs should be the different voyages"): one
    /// voyage card per job the squad's pilots bring from where the Ark is now, and the way home when it is away, each
    /// drawn over the galaxy map. Set sail enters freestyle and sails that job (<see cref="VoyageHarbour.SetSail"/>).
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

        readonly List<VoyageJobCard> _cards = new();
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

        /// <summary>Redraw the cards from the jobs on offer now.</summary>
        public void Refresh()
        {
            if (!template) return;
            var harbour = VoyageHarbour.Current;
            var voyage = harbour ? harbour.Content : null;
            if (!harbour || !voyage || voyage.Content == null)
            {
                Use(0);
                return;
            }
            var content = voyage.Content;
            var lines = voyage.Lines;
            var style = harbour.Definition;

            var escort = style ? style.ArkEscort : null;
            if (harbour.IsEscorting && escort)
            {
                var card = Use(1)[0];
                ShowEscort(card, content, style, escort, harbour.EscortDifficulty, sail: false);
                return;
            }

            if (harbour.IsUnderway)
            {
                var job = harbour.CurrentJob;
                var cards = Use(1);
                cards[0].ShowStepper(null, null, null, null);
                if (job == null)
                {
                    cards[0].Show(content, style, null, null, string.Empty, lines.VoyageName, null);
                    return;
                }
                var to = content.Location(job.To);
                var underway = VoyageRules.Route(content, job.From, job.To);
                cards[0].Show(content, style, job.From, underway,
                    VoyageRules.Fill(lines.CardUnderway, content.Location(job.From), to, to, content.Person(job.PilotId)),
                    job.Title, null, CourseLine(content, lines, underway));
                return;
            }

            var jobs = harbour.Board(out var state);
            string here = state?.At;
            int extra = escort ? 1 : 0;
            if (jobs.Count == 0)
            {
                var none = Use(1 + extra);
                none[0].ShowStepper(null, null, null, null);
                none[0].Show(content, style, here, null, string.Empty, lines.NoWork, null);
                if (escort) ShowEscort(none[1], content, style, escort, Difficulty, sail: true);
                return;
            }

            var shown = Use(jobs.Count + extra);
            if (escort) ShowEscort(shown[jobs.Count], content, style, escort, Difficulty, sail: true);
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                var from = content.Location(job.From);
                var to = content.Location(job.To);
                string eyebrow = VoyageRules.Fill(job.IsReturn ? lines.CardHomeRoute : lines.CardRoute, from, to, to,
                    content.Person(job.PilotId));
                var taken = job;
                var route = VoyageRules.Route(content, job.From, job.To);
                shown[i].ShowStepper(null, null, null, null);
                shown[i].Show(content, style, here, route, eyebrow, job.Title, () => SetSail(taken),
                    CourseLine(content, lines, route));
            }
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
        void ShowEscort(VoyageJobCard card, VoyageContent content, VoyageToyDefinitionSO style, ArkEscortSO escort,
            int difficulty, bool sail)
        {
            string d = difficulty.ToString();
            string legs = escort.Legs.Count.ToString();
            card.Show(content, style, null, null,
                TemplateText.Fill(escort.CardLine, "legs", legs, "difficulty", d),
                TemplateText.Fill(escort.CardTitle, "difficulty", d, "legs", legs),
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
