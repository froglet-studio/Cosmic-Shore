namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reads a stream of (time, left trigger, right trigger) samples and recognises rapid
    /// strings of TAPS as sword combos: RR, LL, RL, LR at the second press, and the eight
    /// three-letter finishers (RRR … LRR) at the third. Pure C# — no Unity state — so the
    /// shipped logic is exactly the tested logic.
    ///
    /// What keeps it out of the way of the analog swordsmanship:
    ///  - a press held longer than <see cref="Settings.TapMaxHoldSeconds"/> is a HOLD (the
    ///    pilot is positioning the sword) and breaks the chain;
    ///  - presses farther apart than <see cref="Settings.ComboWindowSeconds"/> never link;
    ///  - two presses closer than <see cref="Settings.ChordWindowSeconds"/> with both
    ///    triggers down are a CHORD — the energize stance — and suppress combos until both
    ///    triggers are released.
    /// A two-letter combo is emitted immediately (no waiting to see whether a third comes),
    /// and the chain stays open so a third press within the window upgrades it into the
    /// finisher. After a finisher the chain clears.
    /// </summary>
    public sealed class RhinoSwordComboDetector
    {
        public struct Settings
        {
            public float PressThreshold;
            public float ReleaseThreshold;
            public float ComboWindowSeconds;
            public float TapMaxHoldSeconds;
            public float ChordWindowSeconds;
        }

        /// <summary>A recognised combo: <see cref="Length"/> presses, oldest first in <see cref="Sequence"/>.</summary>
        public readonly struct Combo
        {
            public readonly string Sequence;
            public Combo(string sequence) => Sequence = sequence;
            public int Length => Sequence?.Length ?? 0;
            public bool IsFinisher => Length >= MaxLength;
        }

        public const int MaxLength = 3;

        Settings _settings;

        bool _leftHeld, _rightHeld;
        float _leftPressedAt, _rightPressedAt;

        readonly char[] _chain = new char[MaxLength];
        int _chainLength;
        float _lastPressAt = float.NegativeInfinity;
        bool _chordLatched;
        float _suppressUntil = float.NegativeInfinity;

        public RhinoSwordComboDetector(Settings settings) => _settings = settings;

        public void Configure(Settings settings) => _settings = settings;

        public int ChainLength => _chainLength;

        /// <summary>Ignore new presses (for chaining) until <paramref name="time"/>; edges are still tracked.</summary>
        public void SuppressPressesUntil(float time)
        {
            if (time > _suppressUntil) _suppressUntil = time;
            _chainLength = 0;
        }

        public void Reset()
        {
            _leftHeld = _rightHeld = false;
            _chainLength = 0;
            _lastPressAt = float.NegativeInfinity;
            _chordLatched = false;
            _suppressUntil = float.NegativeInfinity;
        }

        /// <summary>
        /// Feed one sample. Returns true and fills <paramref name="combo"/> when a press
        /// completes a two- or three-letter combo this step.
        /// </summary>
        public bool Step(float now, float left, float right, out Combo combo)
        {
            combo = default;

            bool leftPress = Edge(ref _leftHeld, ref _leftPressedAt, left, now, out bool leftRelease, out float leftHeldFor);
            bool rightPress = Edge(ref _rightHeld, ref _rightPressedAt, right, now, out bool rightRelease, out float rightHeldFor);

            // A HOLD is positioning, not a tap: it breaks the chain the moment it releases.
            if ((leftRelease && leftHeldFor > _settings.TapMaxHoldSeconds)
                || (rightRelease && rightHeldFor > _settings.TapMaxHoldSeconds))
                _chainLength = 0;

            if (_chordLatched)
            {
                if (!_leftHeld && !_rightHeld) _chordLatched = false;
                return false;
            }

            // Both pressed on one step, or one pressed within the chord window of the other
            // while it is still down: the both-triggers stance. Never a combo.
            if ((leftPress && rightPress)
                || (leftPress && _rightHeld && now - _rightPressedAt < _settings.ChordWindowSeconds)
                || (rightPress && _leftHeld && now - _leftPressedAt < _settings.ChordWindowSeconds))
            {
                _chainLength = 0;
                _chordLatched = true;
                return false;
            }

            if (leftPress) return Press('L', now, _rightHeld, _rightPressedAt, out combo);
            if (rightPress) return Press('R', now, _leftHeld, _leftPressedAt, out combo);
            return false;
        }

        bool Press(char side, float now, bool otherHeld, float otherPressedAt, out Combo combo)
        {
            combo = default;
            if (now < _suppressUntil) return false;

            // A rolled press (the other trigger still down from a tap a moment ago) links; the
            // other trigger having been held past a tap's length means it was being held to
            // position the sword, so this press starts fresh.
            if (otherHeld && now - otherPressedAt > _settings.TapMaxHoldSeconds) _chainLength = 0;
            if (_chainLength > 0 && now - _lastPressAt > _settings.ComboWindowSeconds) _chainLength = 0;

            _chain[_chainLength++] = side;
            _lastPressAt = now;

            if (_chainLength < 2) return false;

            combo = new Combo(new string(_chain, 0, _chainLength));
            if (_chainLength >= MaxLength) _chainLength = 0;
            return true;
        }

        bool Edge(ref bool held, ref float pressedAt, float value, float now, out bool released, out float heldFor)
        {
            released = false;
            heldFor = 0f;
            if (!held)
            {
                if (value < _settings.PressThreshold) return false;
                held = true;
                pressedAt = now;
                return true;
            }

            if (value <= _settings.ReleaseThreshold)
            {
                held = false;
                released = true;
                heldFor = now - pressedAt;
            }
            return false;
        }
    }
}
