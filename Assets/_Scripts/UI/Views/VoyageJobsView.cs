using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage's jobs on the Voyage window (Garrett, 2026-10-28: "the jobs should be the different voyages"): one
    /// voyage card per job the squad's pilots bring from where the Ark is now, and the way home when it is away, each
    /// drawn over the galaxy map, then the Ark escort's card with its difficulty. Set sail loads the voyage scene with the
    /// choice (<see cref="VoyageLaunch"/>); freestyle is not involved. When the voyage is over the scene returns to the
    /// menu and this window offers the next jobs, from where the squad anchored, grown from the news the last outcome wrote.
    ///
    /// <para>The first card is the prefab's own (<see cref="template"/>); the rest are copies of it, made here.</para>
    /// </summary>
    public class VoyageJobsView : MonoBehaviour
    {
        [Tooltip("The card every job is drawn on; copies of it stand beside it, one per job.")]
        [SerializeField] VoyageJobCard template;

        readonly List<VoyageJobCard> _cards = new();
        ModalWindowManager _window;

        void OnEnable()
        {
            // The window is shown and hidden by its canvas group, so it is re-read on every open: the squad (and so the
            // pilots bringing jobs) may have changed since.
            _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window) _window.OnModalOpened += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (_window) _window.OnModalOpened -= Refresh;
            _window = null;
        }

        /// <summary>Redraw the cards from the jobs on offer now.</summary>
        public void Refresh()
        {
            if (!template) return;
            var style = VoyageToyDefinitionSO.Load();
            var voyage = style ? style.Content : VoyageContentSO.Load();
            if (!voyage || voyage.Content == null)
            {
                Use(0);
                return;
            }
            var content = voyage.Content;
            var lines = voyage.Lines;
            var escort = style ? style.ArkEscort : null;
            var gameData = VoyageLaunch.ResolveGameData();

            var jobs = VoyageHarbour.Board(style, VoyageRun.ActiveSquad(gameData), VoyageRun.LiveDomain(gameData),
                out var state);
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
                shown[i].ShowStepper(null, null, null, null);
                shown[i].Show(content, style, here, VoyageRules.Route(content, job.From, job.To), eyebrow, job.Title,
                    () => SetSail(taken));
            }
        }

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
            // The voyage this escort will sail: its worlds in order and its modifiers, under the card's line.
            var voyage = sail ? ArkEscortRun.PreviewVoyage(escort, difficulty) : null;
            string legs = (voyage is { Legs: { Count: > 0 } } ? voyage.Legs.Count : escort.Legs.Count).ToString();
            string line = TemplateText.Fill(escort.CardLine, "legs", legs, "difficulty", d);
            if (voyage is { WorldNames: { Count: > 0 } }) line += "\n" + voyage.Describe();
            card.Show(content, style, null, null,
                line,
                TemplateText.Fill(escort.CardTitle, "difficulty", d, "legs", legs),
                sail ? () => SetSailEscort(difficulty) : null);
            if (sail)
                card.ShowStepper(escort.LowerLabel, escort.RaiseLabel,
                    () => { Difficulty = difficulty - 1; Refresh(); },
                    () => { Difficulty = difficulty + 1; Refresh(); });
            else card.ShowStepper(null, null, null, null);
        }

        static void SetSailEscort(int difficulty) =>
            VoyageLaunch.Begin(VoyageLaunchRequest.ForEscort(difficulty));

        static void SetSail(VoyageJob job) =>
            VoyageLaunch.Begin(VoyageLaunchRequest.ForJob(job));

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
