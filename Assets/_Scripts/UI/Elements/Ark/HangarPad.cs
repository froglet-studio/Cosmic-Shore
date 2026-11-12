using CosmicShore.Data;

namespace CosmicShore.UI
{
    /// <summary>How a squad seat's pad in the Ark's hangar looks (Docs/MENU_REDESIGN_B.md section 169, Shombith 2026-11-06:
    /// "if they are offline the vessel lighting can be off, but anyone can take their place ... that place will be empty").</summary>
    public enum HangarPadState
    {
        /// <summary>An open seat: the pad stands empty until someone takes it.</summary>
        Empty = 0,
        /// <summary>You, an AI wingmate or an online friend: the vessel is lit and hovers, the pad glows in the domain.</summary>
        Live = 1,
        /// <summary>A friend who is offline: the vessel stands powered down, no pad light.</summary>
        Dark = 2,
    }

    /// <summary>One pad of the hangar: the seat's vessel and how it shows (<see cref="SquadsPage.HangarChanged"/>).</summary>
    public readonly struct HangarPad
    {
        public readonly VesselClassType Vessel;
        public readonly HangarPadState State;

        public HangarPad(VesselClassType vessel, HangarPadState state)
        {
            Vessel = vessel;
            State = state;
        }

        public bool Same(HangarPad other) => Vessel == other.Vessel && State == other.State;
    }
}
