using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>How a card's number grows with its level (the deck page's three rules, <c>cards.py</c>).</summary>
    public enum KrillStatKind
    {
        /// <summary><c>a + b × (level − 1)</c>.</summary>
        Linear = 0,
        /// <summary><c>a × b^(level − 1)</c>: a steady percentage step.</summary>
        Geometric = 1,
        /// <summary>Hand-written steps at levels 1, 5, 10 and 15 (<see cref="KrillCardStat.Steps"/>); no number.</summary>
        Steps = 2,
    }

    /// <summary>One number on a card, by the card's own formula.</summary>
    [Serializable]
    public sealed class KrillCardStat
    {
        [Tooltip("The card's own label for this number (the deck page's).")]
        [SerializeField] string label;
        [SerializeField] KrillStatKind kind;
        [Tooltip("Linear: the level-1 value. Geometric: the level-1 value.")]
        [SerializeField] float a;
        [Tooltip("Linear: added per level. Geometric: the ratio per level.")]
        [SerializeField] float b;
        [Tooltip("The unit the deck page prints after the number.")]
        [SerializeField] string unit;
        [Tooltip("Decimals the deck page shows. 0 = a COUNT: the value rounds down (the page's rule).")]
        [SerializeField] int decimals;
        [Tooltip("Steps only: the text at levels 1, 5, 10 and 15.")]
        [SerializeField] string[] steps = Array.Empty<string>();

        public string Label => label;
        public KrillStatKind Kind => kind;
        public string Unit => unit;
        public int Decimals => decimals;
        public IReadOnlyList<string> Steps => steps;

        public KrillCardStat() { }

        public KrillCardStat(string label, KrillStatKind kind, float a, float b, string unit, int decimals, string[] steps = null)
        {
            this.label = label;
            this.kind = kind;
            this.a = a;
            this.b = b;
            this.unit = unit;
            this.decimals = decimals;
            this.steps = steps ?? Array.Empty<string>();
        }

        /// <summary>
        /// The number at <paramref name="level"/>, exactly the Krill Lab's <c>sv()</c>: linear or geometric, and a count
        /// (0 decimals) rounds DOWN. A <see cref="KrillStatKind.Steps"/> stat has no number (NaN).
        /// </summary>
        public float ValueAt(int level)
        {
            if (kind == KrillStatKind.Steps) return float.NaN;
            int n = Mathf.Max(0, level - 1);
            double v = kind == KrillStatKind.Linear ? a + (double)b * n : a * Math.Pow(b, n);
            return decimals == 0 ? (float)Math.Floor(v + 1e-9) : (float)v;
        }
    }

    /// <summary>A level at which a card changes what it does (the roguelike jolts at 5, 10 and 15).</summary>
    [Serializable]
    public sealed class KrillMilestone
    {
        [SerializeField] int level;
        [SerializeField, TextArea] string text;

        public int Level => level;
        public string Text => text;

        public KrillMilestone() { }
        public KrillMilestone(int level, string text) { this.level = level; this.text = text; }
    }

    /// <summary>One of the 80 cards. Every word on it is authored data a person can edit (CLAUDE.md, player-facing text).</summary>
    [Serializable]
    public sealed class KrillCard
    {
        [Tooltip("The card's id in cards.json (e.g. charge-lasers). The game keys held levels on it.")]
        [SerializeField] string id;
        [SerializeField] Element element;
        [Tooltip("The card's name, as the player reads it.")]
        [SerializeField] string displayName;
        [Tooltip("The card's kind (Build, Form, Speed...), as the player reads it.")]
        [SerializeField] string kind;
        [Tooltip("The phase it shines in, as the player reads it.")]
        [SerializeField] string shinesIn;
        [Tooltip("What it does, as the player reads it.")]
        [SerializeField, TextArea] string text;
        [SerializeField] KrillCardStat[] stats = Array.Empty<KrillCardStat>();
        [SerializeField] KrillMilestone[] milestones = Array.Empty<KrillMilestone>();
        [Tooltip("True when the game runs this card (the Krill Lab's wired set). Hands are dealt only from wired cards.")]
        [SerializeField] bool wired;

        public string Id => id;
        public Element Element => element;
        public string DisplayName => displayName;
        public string Kind => kind;
        public string ShinesIn => shinesIn;
        public string Text => text;
        public IReadOnlyList<KrillCardStat> Stats => stats;
        public IReadOnlyList<KrillMilestone> Milestones => milestones;
        public bool Wired => wired;

        public KrillCard() { }

        public KrillCard(string id, Element element, string displayName, bool wired, params KrillCardStat[] stats)
        {
            this.id = id;
            this.element = element;
            this.displayName = displayName;
            this.wired = wired;
            this.stats = stats ?? Array.Empty<KrillCardStat>();
        }

        /// <summary>Stat <paramref name="index"/> at <paramref name="level"/>; NaN when the card has no such stat.</summary>
        public float Value(int index, int level) =>
            index >= 0 && index < stats.Length ? stats[index].ValueAt(level) : float.NaN;
    }

    /// <summary>
    /// The Krill's 80 cards (design: <c>Docs/Studios/SWARM_CARD_VESSEL.md</c>). GENERATED from
    /// <c>Docs/Studios/SwarmCardVessel/cards.json</c> by <c>Tools/Build/author_krill_assets.py</c>, whose <c>--check</c> is
    /// the drift gate, and the wired set is the Krill Lab's (<c>Docs/Studios/KrillLab.html</c>, <c>IMPL</c>). Edit the card
    /// list (<c>cards.py</c>) and re-run the tool; never hand-edit a number here.
    /// </summary>
    [CreateAssetMenu(fileName = "KrillCardDeck", menuName = "ScriptableObjects/Vessel Actions/Krill Card Deck")]
    public sealed class KrillCardDeckSO : ScriptableObject
    {
        [Tooltip("The card list's revision (cards.json).")]
        [SerializeField] int revision;
        [SerializeField] List<KrillCard> cards = new();

        public int Revision => revision;
        public IReadOnlyList<KrillCard> Cards => cards;

        Dictionary<string, KrillCard> _byId;

        /// <summary>The card with this id, or null.</summary>
        public KrillCard Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null || _byId.Count != cards.Count)
            {
                _byId = new Dictionary<string, KrillCard>(cards.Count);
                foreach (var c in cards)
                    if (c != null && !string.IsNullOrEmpty(c.Id)) _byId[c.Id] = c;
            }
            return _byId.TryGetValue(id, out var card) ? card : null;
        }

        /// <summary>The wired cards of one element, in deck order (what a hand is drawn from).</summary>
        public void Wired(Element element, List<KrillCard> into)
        {
            into.Clear();
            foreach (var c in cards)
                if (c != null && c.Wired && c.Element == element) into.Add(c);
        }

        /// <summary>For tests: a deck built in code.</summary>
        public static KrillCardDeckSO CreateForTests(IEnumerable<KrillCard> list)
        {
            var deck = CreateInstance<KrillCardDeckSO>();
            deck.cards = new List<KrillCard>(list);
            return deck;
        }

        void OnValidate() => _byId = null;
    }
}
