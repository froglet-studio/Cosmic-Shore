using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A <b>TAIL</b> — the long streak that lets OTHER players see and find whatever wears it.
    ///
    /// <b>Not a vessel's alone.</b> A tail is defined by its PURPOSE, not its owner, and the
    /// Sparrow's skyburst missile carries this same marker on the same shared prefab: it crosses
    /// ~360 u in three seconds and everyone in the arena has a reason to see it coming
    /// (<c>Docs/VESSEL_TAIL_AND_JETS.md</c> §4.2). A round has no <see cref="VesselTailAndJets"/>
    /// — its <c>Projectile</c> paints, mounts and sizes the tail itself, per flight.
    ///
    /// The platform has three distinct things streaming off the back of a vessel, and they are
    /// NOT interchangeable (<c>Docs/VESSEL_TAIL_AND_JETS.md</c>):
    ///
    /// <list type="bullet">
    /// <item><b>Trail</b> — conserved PRISM mass laid by <see cref="VesselPrismController"/>.
    ///       Gameplay-bearing, persistent, grazeable, scored. Nothing here touches it.</item>
    /// <item><b>Tail</b> — this. A pure-visual streak hung off the VESSEL ROOT, authored to sit
    ///       clear of its own pilot's view, whose whole job is legibility at range: it is how a
    ///       rival or a teammate spots you across a cell. Seen by everyone.</item>
    /// <item><b>Jet</b> — <see cref="VesselJet"/>. Short plumes at the model's engines that tell
    ///       a pilot THEIR OWN ship is moving. TUNED for that pilot, but drawn on every machine:
    ///       a rival's plumes are how you read their thrust up close. (A per-viewer switch was
    ///       added once and removed; this line still claimed it.)</item>
    /// </list>
    ///
    /// The component itself is deliberately a marker: it carries no tuning, because the tail's
    /// look is authored on the prefab (<c>_Prefabs/Spacevessels/Components/VesselTail.prefab</c>)
    /// and its COLOUR is not the tail's to choose — <see cref="VesselTailAndJets"/> paints every
    /// tail and jet on a vessel with that vessel's live domain, exactly as the prism trail is
    /// painted. What the marker buys is that "does this vessel have a tail?" is a question the
    /// audit tool and the vessel spec can ask of any prefab, instead of a name convention.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselTail : MonoBehaviour
    {
        [Tooltip("Multiplies the prefab's authored ribbon width for THIS vessel. A TrailRenderer's " +
                 "width is world-space and ignores transform scale, so a hull much larger or " +
                 "smaller than the Dolphin (the tuned reference) has to say so here. The fleet " +
                 "derives it from the vessel's own camera distance / 20 — see " +
                 "Docs/VESSEL_TAIL_AND_JETS.md.")]
        [SerializeField] float widthScale = 1f;

        void Awake() => VesselFXWidth.Apply(this, widthScale);
    }
}
