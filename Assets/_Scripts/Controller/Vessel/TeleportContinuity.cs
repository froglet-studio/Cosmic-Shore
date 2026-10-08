using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What happens to everything that FOLLOWS a vessel when the vessel is teleported — its
    /// ribbons and the camera watching it. Called from <c>VesselTransformer.SetPose</c>, the one
    /// place every pose write lands on every machine, so no teleport can skip it and no peer is
    /// left drawing the old version.
    ///
    /// <para><b>Ribbons are CUT, never streaked.</b> A <see cref="TrailRenderer"/> records world
    /// positions, so a hull that jumps five hundred units draws one straight ribbon across the
    /// arena between where it was and where it is — a line nothing flew along. Every trail under
    /// the vessel (tail, jets, any other streak) is split in two at the jump: the ribbon laid so
    /// far is handed to a <see cref="TeleportRibbonGhost"/> that ends where the vessel LEFT and
    /// drains away from its tail the way it would have aged out anyway, and the live renderer
    /// starts again where the vessel ARRIVED. Continuity of existence both ways: nothing laid
    /// blinks out, and nothing is drawn that was not flown.</para>
    ///
    /// <para><b>Through a wormhole the cut is made AT THE MOUTHS</b>
    /// (<see cref="WormholeMouth.TryResolveTransit"/> — the pair every Butterfly fold leaves): the
    /// old ribbon runs into the near sphere and the new one starts at the same spot on the far one,
    /// so a tail reads as passing through. And the camera
    /// following the vessel is carried through the near sphere rather than cut to the other side
    /// (<c>CustomCameraController.CarryThroughSphere</c>) — which is what makes a transit seamless
    /// from the pilot's own seat.</para>
    ///
    /// <para><b>The wormhole's toll is taken here too</b>
    /// (<see cref="WormholeMouth.LevyToll"/>): a pilot not of the pair's domain loses petals at the
    /// near mouth, left on its surface as crystals. Here, not in the mouth's own transit, because
    /// only the vessel's owner runs that, and every peer has to take the toll off its own copy of
    /// the pilot's elemental levels.</para>
    ///
    /// <para><b>Every trail, not only the marked ones.</b> <see cref="VesselTailAndJets"/> scopes
    /// its COLOUR work to the tail/jet markers so it never fights a trail somebody else paints
    /// (the Rhino's blade tracers). A cut is a different kind of operation: it copies whatever
    /// the renderer is showing at that instant and hands the renderer back untouched, so it
    /// fights nobody — and a streak across the arena is a defect on every ribbon, identity or
    /// not.</para>
    /// </summary>
    public static class TeleportContinuity
    {
        static readonly List<TrailRenderer> TrailScratch = new();

        /// <summary>
        /// Ignore jumps shorter than this — a pose written in place (a vessel swap inheriting
        /// the old hull's pose, a re-seat) is not a teleport anything could see.
        /// </summary>
        const float MinimumJump = 1f;

        /// <summary>
        /// The vessel rooted at <paramref name="vessel"/> has just been moved from
        /// <paramref name="from"/> to <paramref name="to"/>. <paramref name="speed"/> is its speed
        /// at the jump, used only to estimate how old each point of a ribbon was.
        /// <paramref name="status"/> is the pilot that jumped, charged a wormhole's toll if it owes
        /// one; null skips the toll.
        /// </summary>
        public static void OnTeleported(Transform vessel, Vector3 from, Vector3 to, float speed,
                                        IVesselStatus status)
        {
            if (!vessel) return;
            if ((to - from).sqrMagnitude < MinimumJump * MinimumJump) return;

            Vector3 departAt = from;
            Vector3 arriveAt = to;

            if (WormholeMouth.TryResolveTransit(from, to, out var mouth, out var entry, out var exit))
            {
                // The cut is made on the two SPHERES: the old ribbon runs into the near mouth, the
                // new one starts at the same spot on the far one (inside it, where only the view
                // through the near mouth can see it).
                departAt = entry;
                arriveAt = exit;
                CarryCameras(vessel, mouth, exit - entry);
                mouth.LevyToll(status, entry);
            }

            CutRibbons(vessel, departAt, arriveAt, speed);
        }

        /// <summary>
        /// Carry the gameplay camera through the near sphere if it is following this vessel: it
        /// keeps framing the ship through the mouth and is moved across when it reaches it
        /// (<c>CustomCameraController.CarryThroughSphere</c>). Asked of the ACTIVE controller only —
        /// the player's rig, the death camera and the replay camera are all
        /// <see cref="CustomCameraController"/>s, and whichever one is on screen is the one whose
        /// picture must not cut. The controller's own identity guard ignores the call if it is
        /// following somebody else.
        /// </summary>
        static void CarryCameras(Transform vessel, WormholeMouth near, Vector3 shift)
        {
            var manager = CameraManager.Instance;
            if (manager == null) return;
            if (manager.GetActiveController() is not CustomCameraController controller) return;
            controller.CarryThroughSphere(vessel, near.Centre, near.Radius, shift);
        }

        static void CutRibbons(Transform vessel, Vector3 departAt, Vector3 arriveAt, float speed)
        {
            TrailScratch.Clear();
            vessel.GetComponentsInChildren(false, TrailScratch);
            for (int i = 0; i < TrailScratch.Count; i++)
            {
                var trail = TrailScratch[i];
                if (trail == null || !trail.enabled) continue;
                if (trail.positionCount > 0)
                    TeleportRibbonGhost.Spawn(trail, departAt, speed);

                // Start the live ribbon again AT the arrival point. Without the seed point the
                // renderer would still start there, one frame of travel later - which leaves a
                // visible gap between the far ring and the start of the tail.
                trail.Clear();
                trail.AddPosition(arriveAt);
            }
        }
    }
}
