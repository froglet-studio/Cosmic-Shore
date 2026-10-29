using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
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

            if (harbour.IsUnderway)
            {
                var job = harbour.CurrentJob;
                var cards = Use(1);
                if (job == null)
                {
                    cards[0].Show(content, style, null, null, string.Empty, lines.VoyageName, null);
                    return;
                }
                var to = content.Location(job.To);
                cards[0].Show(content, style, job.From, VoyageRules.Route(content, job.From, job.To),
                    VoyageRules.Fill(lines.CardUnderway, content.Location(job.From), to, to, content.Person(job.PilotId)),
                    job.Title, null);
                return;
            }

            var jobs = harbour.Board(out var state);
            string here = state?.At;
            if (jobs.Count == 0)
            {
                Use(1)[0].Show(content, style, here, null, string.Empty, lines.NoWork, null);
                return;
            }

            var shown = Use(jobs.Count);
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                var from = content.Location(job.From);
                var to = content.Location(job.To);
                string eyebrow = VoyageRules.Fill(job.IsReturn ? lines.CardHomeRoute : lines.CardRoute, from, to, to,
                    content.Person(job.PilotId));
                var taken = job;
                shown[i].Show(content, style, here, VoyageRules.Route(content, job.From, job.To), eyebrow, job.Title,
                    () => SetSail(taken));
            }
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
                copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + _cards.Count);
                _cards.Add(copy);
            }
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i]) _cards[i].gameObject.SetActive(i < need);
            return _cards;
        }
    }
}
