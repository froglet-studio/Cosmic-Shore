namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A MODE's pilot for one hull: it flies the vessel itself — the four stick axes and the
    /// throttle, every frame — instead of handing <see cref="AIPilot"/> a point to steer at.
    ///
    /// <para><b>Why a steering hook is not enough.</b>
    /// <see cref="AIPilot.SetExternalTargetProvider"/> answers "where should the AI go", and
    /// AIPilot then swings the nose at it with its own stick law. That serves a mode whose
    /// objective is a PLACE. It cannot serve one whose objective is a LINE: Skim Race is won by
    /// holding the hull inside a ribbon's skimming band at 300 u/s, which needs a planner that
    /// knows the ribbon and a controller that leads the hull's 0.67 s rotation lag
    /// (<see cref="SkimFlightController"/>). AIPilot steers with that lag 0.67 s out of date,
    /// which at cruise is invisible and at full boost is a hull in the plates.</para>
    ///
    /// <para><b>Same hands as a human</b> (Docs/AISystem/ARCHITECTURE.md R1): a driver writes
    /// <c>InputStatus</c>'s axes and nothing else, so it can never reach past the flight model.
    /// AIPilot keeps the autopilot's lifecycle — a driver is asked only while the autopilot is on
    /// and the hull is moving — so stopping, pausing and handing a hull to a human still go
    /// through the one owner.</para>
    /// </summary>
    public interface IAIPilotDriver
    {
        /// <summary>
        /// Fly one frame. Return false to hand this frame back to AIPilot's own steering — for a
        /// driver that is not ready yet (its track has not been laid) or that cannot fly this hull.
        /// </summary>
        bool Drive(AIPilot pilot, IVessel vessel, float deltaTime);

        /// <summary>
        /// The pilot stopped, or this driver was replaced or moved to another hull: forget the
        /// flight in progress, so the next <see cref="Drive"/> picks the hull up from wherever it
        /// is rather than steering it back onto a line it has left.
        /// </summary>
        void Release();
    }
}
