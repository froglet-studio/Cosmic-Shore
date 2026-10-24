namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's aimed launch on a PHONE (Garrett, 2026-10-21: "the thresher's aimed launch should be both
    /// thumbs off and aimed with gyro"). A pad reaches the launch view with both triggers at a full pull during a
    /// planted spin; a phone can never hold both, because each trigger is a thumb LIFT (the right lift is the
    /// Plant, the left the Winch) and lifting the second thumb lets go of the first. So on a phone, lifting the
    /// other thumb off a planted spin IS "both triggers full": the plant holds, the chain pulls rigid, the launch
    /// view comes up exactly as on the pad, and the tilt virtual thumb (<c>TouchControlProfile.tiltSteersWhileIdle</c>,
    /// written into the same sticks the pad aims with) turns the crosshair. A thumb back on the glass is the pad's
    /// LT let-go: it arms the aimed launch (boost and launch line unchanged) or, with no launch view up, lets go on
    /// the orbit's tangent.
    ///
    /// <para><b>The ordering, and why the spin survives it.</b> When the last thumb leaves the glass,
    /// <c>TouchInputStrategy</c> raises, on one frame and in this order, the RELEASE of the lift's event
    /// (<c>OnlyLeftStickAction</c> → <c>ThresherExecutor.EndPlant</c>: "1 → 0 is let go of everything") and then the
    /// PRESS of <c>IdleAction</c> (bound on the Thresher's touch map → <see cref="IdlePressed"/>). Both travel the
    /// one reliable, ordered press/release path (<c>R_VesselActionHandler</c>: owner → server → every peer, the
    /// owner's own copy included), so every peer runs them back to back. With analog triggers <c>EndPlant</c> only
    /// lowers the held flag - the let-go is decided at the next trigger poll - so when <see cref="IdlePressed"/> runs
    /// the solver is still planted, the hold engages, and that poll reads both triggers full instead of letting go:
    /// the spin, the chain and the ball never see the release. Should the two events ever land a frame apart, the
    /// one-poll grace that <see cref="PlantReleased"/> starts keeps a planted ball planted for that one poll. It costs
    /// a phone's plain release (a thumb landing back) one frame; the pad never takes this path.</para>
    ///
    /// <para><b>Every peer decides it the same way</b> (vessel rule 48): from the press and release events every peer
    /// replays, the replicated input device and the chain's own simulated mode - never from a camera or a local
    /// touch count. <c>IdleAction</c> is bound in the TOUCH override map only, so it resolves only for a press made
    /// on a phone. Digital triggers (<c>analogTriggers</c> off) never reach it: there <c>EndPlant</c> lets go at once,
    /// so the idle press finds the ball free.</para>
    ///
    /// Pure, so <c>ThresherAnalogTriggerTests</c> flies it with the real solver (and the chain harness runs them offline).
    /// </summary>
    public struct ThresherTouchLaunch
    {
        bool _held;
        int _graceSteps;

        /// <summary>Both thumbs are off a planted spin: the triggers read as both at a full pull.</summary>
        public bool Held => _held;

        /// <summary>The ball is stopping or planted - the plant is in force.</summary>
        public static bool Planted(ThresherMode mode) => mode == ThresherMode.Skidding || mode == ThresherMode.Pivot;

        /// <summary>A phone's Plant was released (a thumb landed back, or the last one left): a planted ball's let-go
        /// waits one poll, in case the idle press that follows the release on the same frame arrives a frame late.</summary>
        public void PlantReleased() => _graceSteps = 1;

        /// <summary><c>IdleAction</c> pressed - the last thumb left the glass. Holds while the plant is still in force
        /// and no launch is armed (lifting off again while an armed launch waits for its point changes nothing).</summary>
        public void IdlePressed(ThresherMode mode, bool launchArmed)
        {
            _held = Planted(mode) && !launchArmed;
            if (_held) _graceSteps = 0;
        }

        /// <summary><c>IdleAction</c> released - a thumb is back on the glass: the hold lets go (the pad's LT let-go).</summary>
        public void IdleReleased() => _held = false;

        /// <summary>Forget everything (disable, re-init).</summary>
        public void Clear()
        {
            _held = false;
            _graceSteps = 0;
        }

        /// <summary>
        /// Once per trigger poll, before the depths reach the solver: while held, both read a full pull; during the
        /// grace a planted ball's LT reads a full pull; otherwise the depths pass through untouched. A plant that
        /// ended some other way (a windmill, a re-seed) ends the hold.
        /// </summary>
        public void Apply(ThresherMode mode, ref float leftDepth, ref float rightDepth)
        {
            bool planted = Planted(mode);
            if (_held && !planted) _held = false;
            if (_held)
            {
                leftDepth = 1f;
                rightDepth = 1f;
                return;
            }
            if (_graceSteps <= 0) return;
            _graceSteps--;
            if (planted) leftDepth = 1f;
        }
    }
}
