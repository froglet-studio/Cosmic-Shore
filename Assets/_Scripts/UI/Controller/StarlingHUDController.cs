using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Starlings' phase cycle and hand to <see cref="StarlingHUDView"/> (design: <c>R_VesselActions/STARLING.md</c> §5).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executor - a serialized reference on its prefab, never
    /// reached for by type through the hierarchy (vessel rule 14), so an unwired HUD shows in the inspector. Every word is
    /// the view's authored template filled by <see cref="TemplateText.Fill"/>; the card names are the deck's.</para>
    ///
    /// <para>The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it, so a re-init that hands
    /// this vessel to an AI or a remote owner stops driving the HUD (rule 5).</para>
    /// </summary>
    public class StarlingHUDController : VesselHUDController
    {
        [Header("Starling")]
        [SerializeField] StarlingHUDView view;
        [Tooltip("This vessel's swarm executor: the phase cycle, the hand and the swarm.")]
        [SerializeField] StarlingSwarmExecutor starlings;

        bool _isLocalPilotHud;
        Element _detailsShown = Element.None;
        int _detailsLevel = -1;
        readonly StringBuilder _lines = new();
        readonly List<StarlingCard> _scratch = new();
        int _secondsShown = -1, _countShown = -1, _capShown = -1;
        StarlingPhase _phaseShown = (StarlingPhase)(-1);

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<StarlingHUDView>(true);

            // The fleet's pilot gate (AnglerHUDController / StoatHUDController / BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
            _secondsShown = _countShown = _capShown = -1;
            _phaseShown = (StarlingPhase)(-1);
            _detailsShown = Element.None;
            _detailsLevel = -1;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !starlings) return;
            var rules = starlings.Rules;
            if (rules == null) return;

            int seconds = Mathf.CeilToInt(Mathf.Max(0f, rules.PhaseLength(rules.Phase) - rules.PhaseTime));
            if (seconds != _secondsShown || rules.Phase != _phaseShown)   // the line is rebuilt only on a change
            {
                _secondsShown = seconds;
                _phaseShown = rules.Phase;
                view.SetPhase(TemplateText.Fill(view.PhaseTemplate,
                    "phase", view.PhaseName((int)rules.Phase),
                    "seconds", seconds.ToString(CultureInfo.InvariantCulture)));
            }

            view.SetObjective((int)rules.Phase, rules.PhaseRemaining01);
            UpdateDetails(rules);

            int count = starlings.MemberCount, cap = rules.CapNow;
            if (count != _countShown || cap != _capShown)
            {
                _countShown = count;
                _capShown = cap;
                view.SetSwarm(TemplateText.Fill(view.SwarmTemplate,
                    "count", count.ToString(CultureInfo.InvariantCulture),
                    "cap", cap.ToString(CultureInfo.InvariantCulture)));
            }

            for (int slot = 0; slot < StarlingRules.HandOrder.Length; slot++)
            {
                var element = StarlingRules.HandOrder[slot];
                int held = Mathf.Min(StarlingRules.MaxLevel, rules.HeldInElement(element));
                var card = rules.HandCard(slot);
                if (!card.IsEmpty)
                    view.SetCard(slot,
                        TemplateText.Fill(view.CardNameTemplate, "name", card.Card.DisplayName),
                        TemplateText.Fill(view.CardLevelTemplate, "level", card.Level.ToString(CultureInfo.InvariantCulture)),
                        true, held / (float)StarlingRules.MaxLevel);
                else
                    view.SetCard(slot, string.Empty,
                        held > 0 ? TemplateText.Fill(view.HeldTemplate, "level", held.ToString(CultureInfo.InvariantCulture)) : string.Empty,
                        false, held / (float)StarlingRules.MaxLevel);
            }
        }
            /// <summary>The card details while a pick is held open (a trigger held, or a long press): the hand's card of that
        /// element at the level it arrives at, else the cards held of it. Rebuilt only when what it shows changes.</summary>
        void UpdateDetails(StarlingRules rules)
        {
            var element = starlings.DetailsElement;
            int slot = element == Element.None ? -1 : StarlingRules.SlotOf(element);
            var card = slot >= 0 ? rules.HandCard(slot) : default;
            int level = card.IsEmpty ? -1 : card.Level;
            if (element == _detailsShown && level == _detailsLevel) return;
            _detailsShown = element;
            _detailsLevel = level;
            if (element == Element.None) { view.SetDetails(Element.None, null, null, null); return; }

            _lines.Clear();
            string title, levelLine;
            if (!card.IsEmpty)
            {
                var c = card.Card;
                title = TemplateText.Fill(view.DetailsTitleTemplate, "name", c.DisplayName);
                levelLine = TemplateText.Fill(view.DetailsLevelTemplate, "level", Num(card.Level));
                Line(c.Text);
                foreach (var stat in c.Stats)
                    Line(TemplateText.Fill(view.DetailsStatTemplate, "label", stat.Label, "value", StatValue(stat, card.Level),
                                           "unit", stat.Unit));
                int held = rules.Level(c.Id);
                foreach (var m in c.Milestones)
                {
                    if (m.Level <= held) continue;
                    Line(TemplateText.Fill(view.DetailsMilestoneTemplate, "level", Num(m.Level), "text", m.Text));
                    break;
                }
            }
            else
            {
                // No hand: the cards held of this element, under the element's glyph.
                title = levelLine = string.Empty;
                if (starlings.Config && starlings.Config.Deck)
                {
                    starlings.Config.Deck.Wired(element, _scratch);
                    foreach (var c in _scratch)
                    {
                        int held = rules.Level(c.Id);
                        if (held > 0) Line(TemplateText.Fill(view.DetailsHeldTemplate, "name", c.DisplayName, "level", Num(held)));
                    }
                }
                if (_lines.Length == 0) Line(view.DetailsNothingHeld);
            }
            view.SetDetails(element, title, levelLine, _lines.ToString());
        }

        void Line(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_lines.Length > 0) _lines.Append('\n');
            _lines.Append(text);
        }

        static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);

        /// <summary>A number as the deck page prints it, or a stepped stat's text at the highest step at or below the level.</summary>
        static string StatValue(StarlingCardStat stat, int level)
        {
            if (stat.Kind == StarlingStatKind.Steps)
            {
                var steps = stat.Steps;
                int index = level >= 15 ? 3 : level >= 10 ? 2 : level >= 5 ? 1 : 0;
                return steps.Count == 0 ? string.Empty : steps[System.Math.Min(index, steps.Count - 1)];
            }
            return stat.ValueAt(level).ToString("F" + System.Math.Max(0, stat.Decimals), CultureInfo.InvariantCulture);
        }
    }
}
