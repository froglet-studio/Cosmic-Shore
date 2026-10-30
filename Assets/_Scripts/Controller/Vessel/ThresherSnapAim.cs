using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's SNAP AIM (Garrett, 2026-10-30: "when we pull both triggers fast we want to be able to aim and
    /// shoot ourselves like Thor"). Both triggers keep their analog finesse - LT's partial pull is a brake and a
    /// sliding anchor, RT's depth the chain's rigidity - and a fast pull of both passes through those depths on its
    /// way to full. For those few frames the brake and the anchor do exactly what they do for a partial pull, the
    /// hull is swung off its line, and then the plant's hook and spin ride the camera round for another half second
    /// before the launch view eases it back. The crosshair then started from a heading the pilot never chose.
    ///
    /// <para><b>The rule.</b> LT leaving rest and both triggers reaching a full pull within
    /// <c>snapAimSeconds</c> is a SNAP: the pilot meant the aimed launch, not a brake. The aim it seeds is the
    /// hull's FLIGHT DIRECTION on the last frame LT was at rest - before any of the pull touched the ship - and the
    /// local camera stops riding the hook from that moment and eases straight into the launch view along it
    /// (<see cref="Holding"/>). The physics is untouched: every depth reaches the solver exactly as before, so every
    /// partial pull flies as it did and every peer simulates the same chain. Only the local pilot's camera and the
    /// aim it seeds change, and the aim was already local (<c>ThresherExecutor.LetGo</c>).</para>
    ///
    /// <para><b>The stick latch.</b> A pilot who snaps mid-turn still has the stick over, and the launch view's
    /// sticks swing the aim at 180 deg/s. So a snap ignores the sticks until they have come back to centre once
    /// (<see cref="AimSticks"/>).</para>
    ///
    /// Pure, so <c>ThresherAnalogTriggerTests</c> flies it with the real solver (and the chain harness runs them offline).
    /// </summary>
    public struct ThresherSnapAim
    {
        Vector3 _restHeading;
        bool _hasRest;
        float _sinceRest;
        bool _decided;
        bool _engaged;
        bool _holding;
        float _holdAge;
        float _dropTime;
        bool _stickLatched;

        /// <summary>This plant began as a snap: the launch view seeds its aim from <see cref="Heading"/>.</summary>
        public bool Engaged => _engaged;
        /// <summary>The flight direction on the last frame LT was at rest (unit).</summary>
        public Vector3 Heading => _restHeading;
        /// <summary>The camera holds the snapped heading and eases into the launch view before the view's own rules
        /// have qualified (no crosshair yet, and a let-go still releases on the tangent).</summary>
        public bool Holding => _holding;

        /// <summary>Forget everything (disable, re-init).</summary>
        public void Clear() => this = default;

        /// <summary>
        /// Once per frame, after the chain step. <paramref name="leftDepth"/> / <paramref name="rightDepth"/> are the
        /// trigger depths the solver was handed (a phone's both-thumbs-off already reads as both full);
        /// <paramref name="heading"/> is the hull's flight direction; <paramref name="planted"/> the plant is in force.
        /// <paramref name="window"/> 0 turns the snap off.
        /// </summary>
        public void Tick(float leftDepth, float rightDepth, Vector3 heading, bool planted, bool launchViewUp,
                         float fullPull, float letGoBelow, float restDepth, float window, float holdSeconds,
                         float dropSeconds, float dt)
        {
            if (leftDepth < restDepth)
            {
                // At rest: the pull has not touched the ship yet. Remember where it was flying.
                if (heading.sqrMagnitude > 1e-6f) { _restHeading = heading.normalized; _hasRest = true; }
                _sinceRest = 0f;
                _decided = false;
                _engaged = false;
                _holding = false;
                _stickLatched = false;
                return;
            }
            _sinceRest += dt;

            // One decision per pull: the first frame both triggers are full.
            if (!_decided && leftDepth >= fullPull && rightDepth >= fullPull)
            {
                _decided = true;
                _engaged = window > 0f && planted && _hasRest && _sinceRest <= window;
                _holding = _engaged;
                _stickLatched = _engaged;
                _holdAge = 0f;
                _dropTime = 0f;
            }
            if (_engaged && !planted) { _engaged = false; _holding = false; }   // let go, a windmill, a re-seed
            if (!_holding) return;

            // The launch view's own rules take over the moment they qualify; the hold never comes back after.
            _holdAge += dt;
            if (launchViewUp || _holdAge > holdSeconds) { _holding = false; return; }
            // Ride out a sag, as the launch view does (a trigger held "at 100%" dips under letGoBelow now and then).
            bool slackened = leftDepth < letGoBelow || rightDepth < letGoBelow;
            _dropTime = slackened ? _dropTime + dt : 0f;
            if (_dropTime >= dropSeconds) _holding = false;
        }

        /// <summary>The sticks the aim reads: zero after a snap until both have come back inside
        /// <paramref name="centre"/> once, so a stick still over from the turn the pilot snapped in does not swing
        /// the aim they just kept.</summary>
        public void AimSticks(ref float x, ref float y, float centre)
        {
            if (!_stickLatched) return;
            if (Mathf.Abs(x) < centre && Mathf.Abs(y) < centre) { _stickLatched = false; return; }
            x = 0f;
            y = 0f;
        }
    }
}
