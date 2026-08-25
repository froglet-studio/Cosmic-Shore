using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel's <b>JET</b> — an engine plume that tells a pilot their own ship is moving.
    ///
    /// A jet is mounted on the MODEL, at whatever that hull calls an engine, and it points where
    /// that engine points. That is the whole placement rule: a jet must come out of somewhere the
    /// model says thrust comes out of, so it reads as the ship working rather than as a decal
    /// stuck behind it. Contrast <see cref="VesselTail"/>, which hangs off the vessel ROOT because
    /// its job is a silhouette at range, not a mechanism up close.
    ///
    /// <b>A jet is TUNED for its own pilot, not hidden from everyone else.</b> Its size, its
    /// placement and its short life are all chosen against the pilot's own camera — that is what
    /// "tuned for the pilot" means — but other players still see it, and should: a rival's plumes
    /// are how you read their thrust in a close fight. Only the TAIL is authored for distance.
    ///
    /// The component is a marker: no tuning, because the look is authored on the shared prefab
    /// (<c>_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab</c>), and no colour, because
    /// <see cref="VesselTailAndJets"/> paints every tail and jet with the vessel's live domain.
    /// What the marker buys is that "does this vessel have jets, and where?" is a question the
    /// audit tool and the vessel spec can ask of any prefab, instead of a name convention that
    /// the next hull spells differently.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselJet : MonoBehaviour
    {
        [Tooltip("Multiplies the prefab's authored plume width for THIS vessel — same reason and " +
                 "same derivation as VesselTail.widthScale.")]
        [SerializeField] float widthScale = 1f;

        void Awake() => VesselFXWidth.Apply(this, widthScale);
    }
}
