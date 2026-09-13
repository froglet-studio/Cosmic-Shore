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
    /// The look is authored on the shared prefab
    /// (<c>_Prefabs/Spacevessels/Components/Jet/VesselJet.prefab</c>) and the colour comes from
    /// <see cref="VesselTailAndJets"/>, which paints every tail and jet with the vessel's live
    /// domain. What the marker buys is that "does this vessel have jets, and where?" is a question
    /// the audit tool and the vessel spec can ask of any prefab, instead of a name convention that
    /// the next hull spells differently.
    /// </summary>
    [DisallowMultipleComponent]
    public class VesselJet : MonoBehaviour
    {
        [Tooltip("Multiplies the prefab's authored plume width for THIS vessel — same reason and " +
                 "same derivation as VesselTail.widthScale.")]
        [SerializeField] float widthScale = 1f;

        [Tooltip("Optional. Name of the bone or model part this jet mounts on, e.g. 'b_Tail1.L'. " +
                 "Leave empty to stay wherever the prefab parents it. Resolved by NAME at Awake " +
                 "and re-parented, keeping this instance's authored local position as an offset " +
                 "FROM the bone — so (0,0,0) means pinned exactly to it.")]
        [SerializeField] string mountBone;

        /// <summary>
        /// The bone or model part this jet mounts on, empty when it stays where the prefab
        /// parented it. Exposed so tooling can ask the jet rather than reaching into the
        /// serialized field by name — a <c>FindProperty</c> that misses after a rename returns
        /// null, which is indistinguishable from "this jet has no mount" and makes a reader
        /// silently report the wrong number.
        /// </summary>
        public string MountBone => mountBone;

        void Awake()
        {
            MountOnBone();
            VesselFXWidth.Apply(this, widthScale);
        }

        /// <summary>
        /// Re-parent onto a named bone on this vessel's model.
        ///
        /// <b>Why by NAME and not by reference.</b> A jet that belongs on an engine has to follow
        /// that engine when the model animates, which means parenting to a bone. A bone inside a
        /// nested model prefab can only be referenced from the vessel prefab by the model file's
        /// own sub-asset id, and that id does not survive a re-export — so an FX mount authored
        /// that way silently detaches the next time the art is updated. Resolving by name is the
        /// same choice <see cref="VesselAnimation"/> already makes for animated parts, and for the
        /// same reason: it is what makes an art swap cheap (<c>Docs/VESSEL_CONSTRUCTION.md</c> §5).
        ///
        /// Fails LOUD and harmlessly: an unresolvable name is an error naming the vessel and the
        /// bone, and the jet stays where the prefab put it rather than vanishing.
        /// </summary>
        /// <summary>
        /// Resolve this jet's <see cref="MountBone"/> against its own vessel, or null when it
        /// declares none or the name does not resolve. This is the ONE implementation: the audit
        /// tool calls it too, so what it reports is what <see cref="MountOnBone"/> will do rather
        /// than a second transcription of the same search that drifts the first time this one is
        /// retuned.
        /// </summary>
        public Transform ResolveMountBone()
        {
            if (string.IsNullOrEmpty(mountBone)) return null;

            // Search from the vessel, not from transform.root — during a spawn the root may still
            // be the scene root, and a bone on a DIFFERENT vessel must never be a candidate.
            var owner = GetComponentInParent<VesselTailAndJets>();
            Transform searchRoot = owner != null ? owner.transform : transform.root;
            return FindDescendant(searchRoot, mountBone);
        }

        void MountOnBone()
        {
            if (string.IsNullOrEmpty(mountBone)) return;

            Transform bone = ResolveMountBone();
            if (bone == null)
            {
                var owner = GetComponentInParent<VesselTailAndJets>();
                string vessel = owner != null ? owner.name : transform.root.name;
                Debug.LogError($"[VesselJet] '{name}' on '{vessel}' wants to mount on bone " +
                               $"'{mountBone}', which is not in that vessel's hierarchy. The jet is " +
                               $"left where the prefab parented it.", this);
                return;
            }

            // worldPositionStays:false keeps the authored local TRS, so the instance's own
            // position is an offset FROM the bone and (0,0,0) pins it exactly.
            transform.SetParent(bone, false);
        }

        static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendant(root.GetChild(i), childName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
