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
    /// <c>minNow</c> (<c>Docs/Studios/StarlingsLab.html</c>), so <see cref="StarlingSwarmExecutor"/> only reports what happened in
    /// the world and these rules decide what it means. Pure, so <c>StarlingRulesTests</c> holds it to the lab.
    ///
    /// <para><b>The cycle.</b> Omnicrystal, then Elemental crystals, then Opposing vessels, then Opposing mass, then round
    /// again. Each phase ends on its timer (cut by Hurry Up) or early on its goal: Y elemental crystals, a debuff landed on
    /// a rival, X enemy prisms removed. The omnicrystal phase deals a hand when it ends, crystal or not; an omnicrystal the
    /// swarm collects in any other phase deals one too.</para>
    ///
    /// <para><b>The hand.</b> Until a draw element is set, one card per element (the <i>spread</i> hand), drawn from that
    /// element's wired cards, at the element's level. A pick adds the card's level to what you hold of it, capped at 15; a
    /// new hand replaces an unpicked one.</para>
    ///
    /// <para><b>The draw element</b> (Garrett, 2026-11-01: "after picking a card you pick an element or it keeps the old
    /// one. when you get an omnicrystal draw (or the timeout draw) it is 5 cards in that element, not 1 of each"). With no
    /// hand up the pilot may set <see cref="DrawElement"/>; the first pick sets it to the picked card's element when none
    /// is set, so there is always an old one to keep. Once set, every hand is an <i>element</i> hand: up to
    /// <see cref="ElementHandSize"/> different wired cards of that element, at its level, and the pilot takes one by
    /// moving <see cref="Focus"/> and picking it (<see cref="PickAt"/>).</para>
    /// </summary>
    public sealed class StarlingRules
    {
        /// <summary>The level no card can pass.</summary>
        public const int MaxLevel = 15;

        /// <summary>The most cards an element hand offers.</summary>
        public const int ElementHandSize = 5;

        /// <summary>The spread hand's slots, in the HUD's four-icon order (charge, mass, space, time).</summary>
        public static readonly Element[] HandOrder = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>The Big Family card's baselines the lab subtracts (its level-1 swarm is the shipped cap + 6).</summary>
        const float BigFamilyCapBase = 40f, BigFamilyMinBase = 6f;

        readonly StarlingConfigSO _config;
        readonly Dictionary<string, int> _held = new();
        readonly StarlingHandCard[] _hand = new StarlingHandCard[ElementHandSize];
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

        /// <summary>The element the next hand is drawn from; <see cref="Element.None"/> deals the spread hand.</summary>
        public Element DrawElement { get; private set; } = Element.None;

        /// <summary>The element of the hand up now; <see cref="Element.None"/> for a spread hand or no hand.</summary>
        public Element HandElement { get; private set; } = Element.None;

        /// <summary>True while an element hand is up (pick by <see cref="Focus"/> and <see cref="PickAt"/>).</summary>
        public bool IsElementHand => HasHand && HandElement != Element.None;

        /// <summary>How many cards the hand up now has (4 for a spread hand); 0 with no hand.</summary>
        public int HandSize { get; private set; }

        /// <summary>The card of an element hand the pilot has highlighted (0-based); 0 on every new hand.</summary>
        public int Focus { get; private set; }

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
            ClearHand();
            HandsDealt = 0;
            DrawElement = Element.None;
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

        /// <summary>Card <paramref name="index"/> of the hand up now: for a spread hand the index is the
        /// <see cref="HandOrder"/> slot; for an element hand it is the card's place in the row. Empty with no hand.</summary>
        public StarlingHandCard HandCard(int index) => HasHand && index >= 0 && index < HandSize ? _hand[index] : default;

        /// <summary>The hand slot of <paramref name="element"/>, or -1.</summary>
        public static int SlotOf(Element element) => System.Array.IndexOf(HandOrder, element);

        /// <summary>Deal a hand (the lab's <c>dealHand</c>): with no <see cref="DrawElement"/>, one wired card per element;
        /// with one, up to <see cref="ElementHandSize"/> different wired cards of it. Either way at the element's level.</summary>
        public void DealHand()
        {
            HandsDealt++;
            HasHand = true;
            Focus = 0;
            for (int i = 0; i < _hand.Length; i++) _hand[i] = default;
            if (DrawElement == Element.None)
            {
                HandElement = Element.None;
                HandSize = HandOrder.Length;
                for (int i = 0; i < HandOrder.Length; i++)
                {
                    var element = HandOrder[i];
                    _scratch.Clear();
                    if (_config.Deck) _config.Deck.Wired(element, _scratch);
                    var card = _scratch.Count > 0 ? _scratch[Mathf.Min(_scratch.Count - 1, (int)(Next01() * _scratch.Count))] : null;
                    _hand[i] = new StarlingHandCard(card, LevelOf(element));
                }
                return;
            }

            // An element hand: a partial shuffle of the element's wired cards, so no card is offered twice.
            HandElement = DrawElement;
            _scratch.Clear();
            if (_config.Deck) _config.Deck.Wired(DrawElement, _scratch);
            HandSize = Mathf.Min(ElementHandSize, _scratch.Count);
            for (int i = 0; i < HandSize; i++)
            {
                int j = i + Mathf.Min(_scratch.Count - i - 1, (int)(Next01() * (_scratch.Count - i)));
                (_scratch[i], _scratch[j]) = (_scratch[j], _scratch[i]);
                _hand[i] = new StarlingHandCard(_scratch[i], LevelOf(DrawElement));
            }
            if (HandSize == 0) ClearHand();   // nothing wired in the element: no hand rather than an empty one
        }

        int LevelOf(Element element) => Mathf.Clamp(_config.LevelFor(element), 1, MaxLevel);

        /// <summary>Pick the card of <paramref name="element"/> from a spread hand (the lab's <c>pick</c>). False when there
        /// is no spread hand or that slot is empty.</summary>
        public bool Pick(Element element) => !IsElementHand && PickAt(SlotOf(element));

        /// <summary>Pick card <paramref name="index"/> of the hand up now (<see cref="HandCard"/>). False when there is no
        /// hand or that card is empty; the hand is spent only on a real pick. The first pick sets the draw element to the
        /// card's element when none is set.</summary>
        public bool PickAt(int index)
        {
            if (!HasHand || index < 0 || index >= HandSize || _hand[index].IsEmpty) return false;
            var c = _hand[index];
            _held[c.Card.Id] = Mathf.Min(MaxLevel, Level(c.Card.Id) + c.Level);
            if (DrawElement == Element.None) DrawElement = c.Card.Element;
            ClearHand();
            return true;
        }

        /// <summary>Take the highlighted card of an element hand.</summary>
        public bool PickFocus() => IsElementHand && PickAt(Focus);

        /// <summary>Move the highlight along an element hand by <paramref name="step"/>, wrapping at the ends.</summary>
        public void MoveFocus(int step)
        {
            if (!IsElementHand || HandSize <= 0) return;
            Focus = ((Focus + step) % HandSize + HandSize) % HandSize;
        }

        /// <summary>Set the element the next hand is drawn from. Only with no hand up (a hand up is picked from first);
        /// false otherwise, or for an element that is not one of the four.</summary>
        public bool ChooseDrawElement(Element element)
        {
            if (HasHand || SlotOf(element) < 0) return false;
            DrawElement = element;
            return true;
        }

        void ClearHand()
        {
            for (int i = 0; i < _hand.Length; i++) _hand[i] = default;
            HasHand = false;
            HandElement = Element.None;
            HandSize = 0;
            Focus = 0;
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
    /// <summary>
    /// The Starlings pilot's camera arithmetic (Garrett, 2026-10-21: "x-diff should control zoom and y-diff should control
    /// camera roll. this should be pinch and two finger spin on mobile"). Pure, so <c>StarlingRulesTests</c> holds it;
    /// <see cref="StarlingVesselTransformer"/> feeds it the input and writes the camera.
    /// </summary>
    public static class StarlingPilotControls
    {
        /// <summary>A centred axis (-1..1) with a dead band: zero inside <paramref name="deadZone"/>, then a ramp to ±1.</summary>
        public static float Axis(float centred, float deadZone)
        {
            float magnitude = System.Math.Abs(centred);
            if (magnitude <= deadZone || deadZone >= 1f) return 0f;
            return System.Math.Sign(centred) * System.Math.Min(1f, (magnitude - deadZone) / (1f - deadZone));
        }

        /// <summary>x-diff (0..1, 0.5 at rest) as a centred axis: sticks spread apart is +1 (zoom in), squeezed is -1.</summary>
        public static float SpreadAxis(float xDiff, float deadZone) => Axis((xDiff - 0.5f) * 2f, deadZone);

        /// <summary>
        /// The camera distance (negative, behind the view) after <paramref name="dt"/> s of zooming at <paramref name="axis"/>:
        /// exponential in the distance, so a zoom feels the same near and far, and held between <paramref name="nearest"/> and
        /// <paramref name="farthest"/> times <paramref name="neutral"/>, the camera's authored distance.
        /// </summary>
        public static float Zoomed(float distance, float neutral, float axis, float rate, float dt, float nearest, float farthest)
            => Clamp(distance * (float)System.Math.Exp(-axis * rate * dt), neutral, nearest, farthest);

        /// <summary>A pinch, 1:1: fingers twice as far apart halve the distance.</summary>
        public static float Pinched(float distance, float neutral, float previousSpan, float span, float nearest, float farthest)
            => span <= 1e-3f || previousSpan <= 1e-3f ? Clamp(distance, neutral, nearest, farthest)
                                                       : Clamp(distance * previousSpan / span, neutral, nearest, farthest);

        /// <summary>The signed angle (degrees, counter-clockwise positive) from one finger-to-finger vector to the next.</summary>
        public static float Spun(float fromX, float fromY, float toX, float toY)
        {
            double a = System.Math.Atan2(fromX * toY - fromY * toX, fromX * toX + fromY * toY);
            return (float)(a * 180.0 / System.Math.PI);
        }

        /// <summary>
        /// y-diff as a camera roll axis. Rolling the CAMERA turns the world the other way on screen, so the camera rolls
        /// against y-diff: the world then turns the way a hull rolls under the same sticks in flight (Garrett, 2026-10-21:
        /// "it is currently rotating the flipped directions").
        /// </summary>
        public static float StickRoll(float yDiff, float deadZone) => -Axis(yDiff, deadZone);

        /// <summary>A two-finger spin as a camera roll (degrees): against the fingers, so the world turns WITH them.</summary>
        public static float SpinRoll(float fromX, float fromY, float toX, float toY) => -Spun(fromX, fromY, toX, toY);

        static float Clamp(float distance, float neutral, float nearest, float farthest)
        {
            if (neutral >= 0f) return distance;                       // not a chase camera: leave it alone
            float near = neutral * nearest, far = neutral * farthest;  // both negative; far is the more negative
            return distance > near ? near : distance < far ? far : distance;
        }
    }

    /// <summary>
    /// One pick button's press (Garrett, 2026-10-21: "holding a trigger when selecting an element should give details about
    /// the card. a long press on mobile should do the same"). A press that is let go quickly PICKS on release; a press held
    /// past the hold time, or made while a trigger is pulled, shows the card's details instead and picks nothing. Pure, so
    /// <c>StarlingRulesTests</c> holds it; <see cref="StarlingSwarmExecutor"/> owns one.
    ///
    /// <para><b>Browsing</b> (Garrett, 2026-10-21: "when you hold an element while you have no cards you should be able to
    /// peruse all your abilities"): while the details are open, pressing another element's button turns them to that
    /// element and picks nothing. They stay open while any pick button is still down, and close when the last one is let
    /// go.</para>
    /// </summary>
    public sealed class StarlingPickPress
    {
        float _since;
        int _down;   // a bit per element whose button is down

        /// <summary>The element whose button is down; <see cref="Element.None"/> when none is.</summary>
        public Element Pressed { get; private set; } = Element.None;

        /// <summary>The element whose card details show; <see cref="Element.None"/> when none do.</summary>
        public Element Details { get; private set; } = Element.None;

        public void Begin(Element element, float now, bool triggerHeld)
        {
            _down |= Bit(element);
            Pressed = element;
            if (Details != Element.None) { Details = element; return; }   // browsing: the open details turn to this element
            _since = now;
            Details = triggerHeld ? element : Element.None;
        }

        /// <summary>Every frame while a button is down: a held press or a trigger pulled mid-press opens the details.</summary>
        public void Tick(float now, bool triggerHeld, float holdTime)
        {
            if (Pressed == Element.None || Details != Element.None) return;
            if (triggerHeld || now - _since >= holdTime) Details = Pressed;
        }

        /// <summary>The button came up. True when it was a quick press, i.e. pick the card.</summary>
        public bool End(Element element)
        {
            _down &= ~Bit(element);
            if (Details != Element.None)
            {
                // Browsing: the details stay open on a button still down, and close with the last one.
                if (_down == 0) Pressed = Details = Element.None;
                else if (Details == element) Pressed = Details = AnyDown();
                return false;
            }
            if (Pressed != element) return false;
            Pressed = Element.None;
            return true;
        }

        public void Clear()
        {
            _down = 0;
            Pressed = Details = Element.None;
        }

        static int Bit(Element element) => 1 << (int)element;

        Element AnyDown()
        {
            foreach (var element in StarlingRules.HandOrder)
                if ((_down & Bit(element)) != 0) return element;
            return Element.None;
        }
    }
}
