using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the escort roles need from whoever runs the Ark they guard (Docs/Voyages/DEPTH.md §10, phase D2): the Ark
    /// escort (<see cref="ArkEscortRun"/>) and a voyage (<see cref="VoyageRun"/>) both host them, so a voyage's AI
    /// wingmates hunt the threats around the Ark by the same roles the escort tuned, never by a copy of them. The
    /// roles read it through <see cref="ArkEscortContext"/>, which one host owns per run.
    /// </summary>
    public interface IArkEscortHost
    {
        /// <summary>True while the run is on; a disarmed role does nothing.</summary>
        bool IsActive { get; }
        /// <summary>The Ark the seats guard, or null before it is laid.</summary>
        Ark Ark { get; }
        /// <summary>The world the Ark is in now.</summary>
        Cell CurrentCell { get; }
        /// <summary>Half the authored hull length: the lane tests keep this sphere clear.</summary>
        float HullHalfLength { get; }
        /// <summary>The shared context the seats read (threats ranked, claims, the lane-clear test).</summary>
        ArkEscortContext Context { get; }
        /// <summary>Live hostile creatures within <paramref name="radius"/> of <paramref name="at"/>, nearest first into
        /// <paramref name="into"/> when given.</summary>
        int CountThreats(Vector3 at, float radius, List<Fauna> into);
        /// <summary>True once the host has pacified <paramref name="cell"/> (the cultivator's stop signal).</summary>
        bool IsPacified(Cell cell);
        /// <summary>The squad control volume a world needs before it can be pacified (0 when pacifying is off).</summary>
        float PacifyMinVolume { get; }
    }
}
