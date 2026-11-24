using CosmicShore.Data;

namespace CosmicShore.UI
{
    /// <summary>What stands on one of the hangar's pads: the seat's vessel, whether anyone holds the seat (an open seat's
    /// pad stands empty) and whether its pilot is here (an offline mate's hull is powered down).</summary>
    public readonly struct SquadHangarSeat : System.IEquatable<SquadHangarSeat>
    {
        public readonly VesselClassType Vessel;
        public readonly bool Filled;
        public readonly bool Live;

        public SquadHangarSeat(VesselClassType vessel, bool filled, bool live)
        {
            Vessel = vessel;
            Filled = filled;
            Live = live;
        }

        public bool Equals(SquadHangarSeat o) => Vessel == o.Vessel && Filled == o.Filled && Live == o.Live;
        public override bool Equals(object obj) => obj is SquadHangarSeat o && Equals(o);
        public override int GetHashCode() => ((int)Vessel * 4) ^ (Filled ? 1 : 0) ^ (Live ? 2 : 0);
    }
}
