using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The Starlings' four objectives, in the order the cycle runs them (the lab's <c>PHASES</c>). Never renumber.</summary>
    public enum StarlingPhase
    {
        Omnicrystal = 0,
        ElementalCrystals = 1,
        OpposingVessels = 2,
        OpposingMass = 3,
    }

    /// <summary>The ids of the cards the game runs (the Starlings Lab's <c>IMPL</c>), as they are in <c>cards.json</c>.</summary>
    public static class StarlingCardIds
    {
        public const string MoundBuilders = "mass-mound-builders";
        public const string DangerDrip = "mass-danger-drip";
        public const string Carapace = "mass-carapace";
        public const string Brood = "mass-brood";
        public const string BigFamily = "mass-big-family";
        public const string Snowball = "mass-snowball";
        public const string Quickening = "time-quickening";
        public const string Agility = "time-agility";
        public const string Dragonfly = "time-dragonfly";
        public const string SkipAhead = "time-skip-ahead";
        public const string HurryUp = "time-hurry-up";
        public const string BlinkStep = "time-blink-step";
        public const string HolePunch = "space-hole-punch";
        public const string WideBite = "space-wide-bite";
        public const string Annex = "space-annex";
        public const string ShieldEaters = "space-shield-eaters";
        public const string BlackHole = "space-black-hole";
        public const string Split = "space-split";
        public const string Lasers = "charge-lasers";
        public const string Emp = "charge-emp";
        public const string TeslaCoil = "charge-tesla-coil";
        public const string ShortCircuit = "charge-short-circuit";
        public const string Sting = "charge-sting";
        public const string ChainLightning = "charge-chain-lightning";
    }

    /// <summary>One card in the hand: which card, and the level it arrives at.</summary>
    public readonly struct StarlingHandCard
    {
        public readonly StarlingCard Card;
        public readonly int Level;

        public StarlingHandCard(StarlingCard card, int level)
        {
            Card = card;
            Level = level;
        }

        public bool IsEmpty => Card == null;
    }

    /// <summary>
    /// The Starlings' phase cycle and card hand, with no world in it (design: <c>R_VesselActions/STARLING.md</c>). A line-for-line
    /// port of the Starlings Lab's <c>endPhase</c>, <c>dealHand</c>, <c>pick</c>, <c>phaseLen</c>, <c>capNow</c> and
    /// <c>minNow</c> (<c>Docs/Studios/StarlingLab.html</c>), so <see cref="StarlingSwarmExecutor"/> only reports what happened in
    /// the world and these rules decide what it means. Pure, so <c>StarlingRulesTests</c> holds it to the lab.
    ///
    /// <para><b>The cycle.</b> Omnicrystal, then Elemental crystals, then Opposing vessels, then Opposing mass, then round
    /// again. Each phase ends on its timer (cut by Hurry Up) or early on its goal: Y elemental crystals, a debuff landed on
    /// a rival, X enemy prisms removed. The omnicrystal phase deals a hand when it ends, crystal or not; an omnicrystal the
    /// swarm collects in any other phase deals one too.</para>
    ///
    /// <para><b>The hand.</b> One card per element, drawn from that element's wired cards, at the element's level. A pick
    /// adds the card's level to what you hold of it, capped at 15; a new hand replaces an unpicked one.</para>
    /// </summary>
    public sealed class StarlingRules
    {
        /// <summary>The level no card can pass.</summary>
        public const int MaxLevel = 15;

        /// <summary>The hand's slots, in the HUD's four-icon order (charge, mass, space, time).</summary>
        public static readonly Element[] HandOrder = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>The Big Family card's baselines the lab subtracts (its level-1 swarm is the shipped cap + 6).</summary>
        const float BigFamilyCapBase = 40f, BigFamilyMinBase = 6f;

        readonly StarlingConfigSO _config;
        readonly Dictionary<string, int> _held = new();
        readonly StarlingHandCard[] _hand = new StarlingHandCard[4];
        readonly List<StarlingCard> _scratch = new();
        uint _seed;
        uint _draws;

        public StarlingPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public int CollectedThisPhase { get; private set; }
        public int RemovedThisPhase { get; private set; }
        public bool DebuffedThisPhase { get; private set; }
        public int HandsDealt { get; private set; }
        public bool HasHand { get; private set; }
        public bool BlackHoleUsed { get; set; }

        /// <summary>Raised after the cycle moves on: (the phase that ended, whether it ended on its goal).</summary>
        public event System.Action<StarlingPhase, bool> PhaseEnded;

        public StarlingRules(StarlingConfigSO config)
        {
            _config = config;
            Reset(7);
        }

        /// <summary>A new run: nothing held, no hand, the omnicrystal phase.</summary>
        public void Reset(uint seed)
        {
            _seed = seed;
            _draws = 0;
            _held.Clear();
            for (int i = 0; i < _hand.Length; i++) _hand[i] = default;
            HasHand = false;
            HandsDealt = 0;
            Phase = StarlingPhase.Omnicrystal;
            ClearPhaseProgress();
            BlackHoleUsed = false;
        }

        // ---- what the cards say ----

        /// <summary>The level held of <paramref name="cardId"/> (0 = not held).</summary>
        public int Level(string cardId) => _held.TryGetValue(cardId, out int level) ? level : 0;

        public bool Has(string cardId) => Level(cardId) > 0;

        /// <summary>Stat <paramref name="stat"/> of a held card at its held level; NaN when it is not held.</summary>
        public float Value(string cardId, int stat)
        {
            int level = Level(cardId);
            if (level <= 0) return float.NaN;
            var card = _config && _config.Deck ? _config.Deck.Find(cardId) : null;
            return card != null ? card.Value(stat, level) : float.NaN;
        }

        /// <summary><see cref="Value"/>, or <paramref name="ifNotHeld"/> when the card is not held (or has no such number).</summary>
        public float ValueOr(string cardId, int stat, float ifNotHeld)
        {
            float v = Value(cardId, stat);
            return float.IsNaN(v) ? ifNotHeld : v;
        }

        /// <summary>The swarm cap now: the shipped cap, raised by Big Family (the lab's <c>capNow</c>).</summary>
        public int CapNow => Mathf.RoundToInt(_config.Cap) +
                             (Has(StarlingCardIds.BigFamily) ? Mathf.RoundToInt(Value(StarlingCardIds.BigFamily, 0) - BigFamilyCapBase) : 0);

        /// <summary>The protected minimum now (the lab's <c>minNow</c>).</summary>
        public int MinNow => Mathf.RoundToInt(_config.MinSize) +
                             (Has(StarlingCardIds.BigFamily) ? Mathf.RoundToInt(Value(StarlingCardIds.BigFamily, 1) - BigFamilyMinBase) : 0);

        /// <summary>How long <paramref name="phase"/> runs: its timer, cut by Hurry Up (the lab's <c>phaseLen</c>).</summary>
        public float PhaseLength(StarlingPhase phase)
        {
            float cut = Has(StarlingCardIds.HurryUp) ? Value(StarlingCardIds.HurryUp, 0) / 100f : 0f;
            return _config.PhaseTimer(phase) * (1f - cut);
        }

        /// <summary>The phase timer's remaining share, 1 at the phase's start and 0 at its end.</summary>
        public float PhaseRemaining01 => Mathf.Clamp01(1f - PhaseTime / Mathf.Max(0.01f, PhaseLength(Phase)));

        /// <summary>How many swarms the Split card makes (the lab's <c>swarmCount</c>): 2 / 3 / 4 at levels 5 / 10 / 15,
        /// and below level 5 two while its timed split runs (<paramref name="timedSplitRunning"/>).</summary>
        public int SwarmCount(bool timedSplitRunning)
        {
            int level = Level(StarlingCardIds.Split);
            if (level <= 0) return 1;
            if (level >= 15) return 4;
            if (level >= 10) return 3;
            if (level >= 5) return 2;
            return timedSplitRunning ? 2 : 1;
        }

        // ---- the hand ----

        /// <summary>The card in hand slot <paramref name="slot"/> (<see cref="HandOrder"/>); empty with no hand.</summary>
        public StarlingHandCard HandCard(int slot) => HasHand && slot >= 0 && slot < _hand.Length ? _hand[slot] : default;

        /// <summary>The hand slot of <paramref name="element"/>, or -1.</summary>
        public static int SlotOf(Element element) => System.Array.IndexOf(HandOrder, element);

        /// <summary>Deal a hand (the lab's <c>dealHand</c>): one wired card per element, at the element's level.</summary>
        public void DealHand()
        {
            HandsDealt++;
            HasHand = true;
            for (int i = 0; i < HandOrder.Length; i++)
            {
                var element = HandOrder[i];
                _scratch.Clear();
                if (_config.Deck) _config.Deck.Wired(element, _scratch);
                var card = _scratch.Count > 0 ? _scratch[Mathf.Min(_scratch.Count - 1, (int)(Next01() * _scratch.Count))] : null;
                _hand[i] = new StarlingHandCard(card, Mathf.Clamp(_config.LevelFor(element), 1, MaxLevel));
            }
        }

        /// <summary>Pick the card of <paramref name="element"/> from the hand (the lab's <c>pick</c>). Returns false when
        /// there is no hand or that slot is empty; the hand is spent either way only on a real pick.</summary>
        public bool Pick(Element element)
        {
            int slot = SlotOf(element);
            if (!HasHand || slot < 0 || _hand[slot].IsEmpty) return false;
            var c = _hand[slot];
            _held[c.Card.Id] = Mathf.Min(MaxLevel, Level(c.Card.Id) + c.Level);
            HasHand = false;
            for (int i = 0; i < _hand.Length; i++) _hand[i] = default;
            return true;
        }

        /// <summary>The level held of the hand slot's card's element, summed (what the HUD shows on a slot with no hand).</summary>
        public int HeldInElement(Element element)
        {
            int sum = 0;
            if (!_config.Deck) return 0;
            foreach (var kv in _held)
            {
                var card = _config.Deck.Find(kv.Key);
                if (card != null && card.Element == element) sum += kv.Value;
            }
            return sum;
        }

        // ---- what the world reports ----

        /// <summary>An elemental crystal was collected. It counts toward Y in its own phase, or in any phase while the swarm
        /// is split (the lab's rule: a second swarm is always on the next objective).</summary>
        public void OnElementalCollected(bool split)
        {
            if (Phase == StarlingPhase.ElementalCrystals || split) CollectedThisPhase++;
        }

        /// <summary>An omnicrystal was collected: it ends its own phase early, and deals a hand in any other.</summary>
        public void OnOmniCollected()
        {
            if (Phase == StarlingPhase.Omnicrystal) EndPhase(true);
            else DealHand();
        }

        /// <summary>An enemy prism was destroyed or stolen.</summary>
        public void OnPrismRemoved() => RemovedThisPhase++;

        /// <summary>A scored hit landed on a rival vessel (the lab's <c>debuff</c>).</summary>
        public void OnDebuff() => DebuffedThisPhase = true;

        /// <summary>Advance the clock and end the phase on its goal or its timer (the end of the lab's <c>simStep</c>).</summary>
        public void Tick(float dt)
        {
            PhaseTime += dt;
            bool met = (Phase == StarlingPhase.ElementalCrystals && CollectedThisPhase >= _config.GoalY)
                       || (Phase == StarlingPhase.OpposingVessels && DebuffedThisPhase)
                       || (Phase == StarlingPhase.OpposingMass && RemovedThisPhase >= _config.GoalX);
            if (met) EndPhase(true);
            else if (PhaseTime >= PhaseLength(Phase)) EndPhase(false);
        }

        /// <summary>The lab's <c>endPhase</c>.</summary>
        public void EndPhase(bool early)
        {
            var ended = Phase;
            if (ended == StarlingPhase.Omnicrystal) DealHand();
            Phase = (StarlingPhase)(((int)Phase + 1) % 4);
            ClearPhaseProgress();
            if (Phase == StarlingPhase.OpposingMass) BlackHoleUsed = false;
            PhaseEnded?.Invoke(ended, early);
        }

        void ClearPhaseProgress()
        {
            PhaseTime = 0f;
            CollectedThisPhase = 0;
            RemovedThisPhase = 0;
            DebuffedThisPhase = false;
        }

        /// <summary>A deterministic draw in [0, 1) from the run's seed (vessel rule 18: no UnityEngine.Random in a vessel's
        /// rules, so a replay deals the same hands).</summary>
        public float Next01() => Hash01(_seed, _draws++);

        public static float Hash01(uint seed, uint n)
        {
            uint h = seed * 0x9E3779B1u ^ (n + 0x7F4A7C15u) * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h >> 8) / 16777216f;
        }
    }
}
