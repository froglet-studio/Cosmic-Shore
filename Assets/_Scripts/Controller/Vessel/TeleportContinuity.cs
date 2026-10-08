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
    /// <para><b>Through a Butterfly fold gate the cut is made AT THE MOUTHS</b>
    /// (<see cref="FoldGate.TryResolveTransit"/>): the old ribbon runs into the near ring and the
    /// new one runs out of the far ring, so a tail reads as passing through the portal. And the
    /// camera following the vessel is carried through the pair rather than cut to the other side
    /// (<c>CustomCameraController.CarryThroughPortal</c>) — which is what makes a transit
    /// seamless from the pilot's own seat.</para>
    ///
    /// <para><b>Through a wormhole</b> (<see cref="WormholeMouth.TryResolveTransit"/>) the same two
    /// things happen on spheres instead of rings: the cut is made on the near and far mouths, and
    /// the camera is carried through the near sphere
    /// (<c>CustomCameraController.CarryThroughSphere</c>).</para>
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
        /// </summary>
        public static void OnTeleported(Transform vessel, Vector3 from, Vector3 to, float speed)
        {
            if (!vessel) return;
            if ((to - from).sqrMagnitude < MinimumJump * MinimumJump) return;

            Vector3 departAt = from;
            Vector3 arriveAt = to;

            if (FoldGate.TryResolveTransit(from, to, out var near, out var nearMouth, out var farMouth))
            {
                departAt = nearMouth;
                arriveAt = farMouth;
                CarryCameras(vessel, near, from, farMouth - nearMouth);
            }
            else if (WormholeMouth.TryResolveTransit(from, to, out var mouth, out var entry, out var exit))
            {
                // Through a wormhole the cut is made on the two SPHERES: the old ribbon runs into
                // the near mouth, the new one starts at the same spot on the far one (inside it,
                // where only the view through the near mouth can see it).
                departAt = entry;
                arriveAt = exit;
                CarryCamerasThroughSphere(vessel, mouth, exit - entry);
            }

            CutRibbons(vessel, departAt, arriveAt, speed);
        }

        /// <summary>
        /// Carry the gameplay camera through the portal if it is following this vessel. Asked of
        /// the ACTIVE controller only — the player's rig, the death camera and the replay camera
        /// are all <see cref="CustomCameraController"/>s, and whichever one is on screen is the one
        /// whose picture must not cut. The controller's own identity guard ignores the call if it
        /// is following somebody else.
        /// </summary>
        static void CarryCameras(Transform vessel, FoldGate near, Vector3 from, Vector3 shift)
        {
            var manager = CameraManager.Instance;
            if (manager == null) return;
            if (manager.GetActiveController() is not CustomCameraController controller) return;

            // Which side of the mouth the vessel went out on. It is on that side of BOTH planes
            // (the map is a translation), so reading it off the near plane is exact.
            float side = FoldGateGeometry.Axial(from, near.Centre, near.Axis);
            Vector3 exitNormal = near.Axis * (side >= 0f ? 1f : -1f);

            controller.CarryThroughPortal(vessel, near.Centre, exitNormal, near.RingRadius, shift);
        }

        /// <summary>
        /// The wormhole counterpart of <see cref="CarryCameras"/>: the camera keeps framing the
        /// ship through the near sphere and is moved across when it reaches it
        /// (<c>CustomCameraController.CarryThroughSphere</c>).
        /// </summary>
        static void CarryCamerasThroughSphere(Transform vessel, WormholeMouth near, Vector3 shift)
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
