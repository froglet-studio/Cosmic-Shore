using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Aims one of the Squirrel's drift jets: along the vessel's COURSE while it is drifting,
    /// out to the side otherwise.
    ///
    /// <para>Four of these run on every Squirrel, which is the menu's default hull and therefore
    /// the freestyle hull — so anything unguarded here is multiplied by four and by every frame
    /// of every session. Both hazards it used to carry are of that shape: <c>vesselStatus</c> was
    /// dereferenced with no null test (a jet on a hull whose reference did not survive a prefab
    /// edit would throw four times a frame forever), and <c>Course</c> is a plain Vector3 field
    /// that reads ZERO between the vessel spawning and its transformer's first write — and
    /// <c>Quaternion.LookRotation</c> logs "Look rotation viewing vector is zero" as an ERROR for
    /// every such call. Neither failure stops, because nothing about the next frame is different.</para>
    /// </summary>
    public class DriftJet : MonoBehaviour
    {
        [SerializeField] bool flip = false;

        [SerializeField] VesselStatus vesselStatus;

        // A parent is required for the up-vector; a jet is always nested under the hull, so this
        // is a fail-safe rather than a supported configuration.
        Transform Hull => transform.parent;

        void Update()
        {
            if (!vesselStatus) return;

            var hull = Hull;
            if (!hull) return;

            // LookRotation is undefined for a zero forward and Unity reports it rather than
            // failing - hold the current pose until there is a direction to aim at.
            Vector3 forward = vesselStatus.IsDrifting
                ? vesselStatus.Course
                : (flip ? -hull.right : hull.right);

            if (forward.sqrMagnitude < 1e-8f) return;

            transform.rotation = Quaternion.Lerp(
                transform.rotation, Quaternion.LookRotation(forward, hull.forward), .06f);
        }
    }
}
