using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The squad's places beside the Ark (Shombith, 2026-11-17: "the vessel should always spawn side by side with the ark.
    /// Their spawn points should be two besides the ark and one on top of the ark"): seat 1 off the port side, seat 2 off
    /// the starboard side, seat 3 over the back, all facing the Ark's heading, and the hangar the intro launches them
    /// from (<see cref="ArkSortie"/>). Every place is in the Ark's own frame, so it sails with the Ark; the seat decides
    /// the slot, so every machine in a party puts the same pilot in the same place. The geometry is pure for the tests.
    /// </summary>
    public static class ArkFormation
    {
        /// <summary>The slots, in seat order.</summary>
        public enum Slot
        {
            Port = 0,
            Starboard = 1,
            Top = 2,
        }

        /// <summary>The ArkReverse hangar's three pads (the model's <c>hangar.slot.0..2</c>, 0 the deepest) and its two
        /// door hinges, in the model's frame (x right, y up, z nose; Docs/VesselModels/ArkReverse.md "The level").</summary>
        const string PadBone = "hangar.slot.";
        const string DoorTopBone = "hangar.door.T";
        const string DoorBottomBone = "hangar.door.B";
        const int PadCount = 3;

        /// <summary>A model-less Ark (the procedural spindle) has no hangar: its squad launches from just ahead of the nose.</summary>
        const float SpindleHalfWidthShare = 0.3f;

        public static Slot SlotOf(int seat) => (Slot)(((seat % 3) + 3) % 3);

        /// <summary>
        /// The slot for <paramref name="seat"/> in the Ark's local frame (world units, unscaled by the Ark's transform),
        /// given the hull's local <paramref name="bounds"/>: clear of the hull's widest part to port or starboard, or of
        /// its highest part on top, level with the hull's middle plus <see cref="ArkFormationSettings.AlongShare"/> of its
        /// length.
        /// </summary>
        public static Vector3 SlotLocal(int seat, Bounds bounds, ArkFormationSettings settings)
        {
            settings ??= new ArkFormationSettings();
            var c = bounds.center;
            float z = c.z + settings.AlongShare * bounds.size.z;
            return SlotOf(seat) switch
            {
                Slot.Port => new Vector3(bounds.min.x - settings.AbeamMargin, c.y, z),
                Slot.Starboard => new Vector3(bounds.max.x + settings.AbeamMargin, c.y, z),
                _ => new Vector3(c.x, bounds.max.y + settings.AboveMargin, z),
            };
        }

        /// <summary>The world pose of <paramref name="seat"/>'s slot beside <paramref name="ark"/>, facing its heading.</summary>
        public static Pose SlotPose(Ark ark, int seat, Bounds localBounds, ArkFormationSettings settings) =>
            SlotPose(ark.transform, seat, localBounds, settings);

        /// <inheritdoc cref="SlotPose(Ark, int, Bounds, ArkFormationSettings)"/>
        public static Pose SlotPose(Transform ark, int seat, Bounds localBounds, ArkFormationSettings settings) =>
            new(ark.TransformPoint(SlotLocal(seat, localBounds, settings)), ark.rotation);

        /// <summary>
        /// The Ark's extent in its own frame: every hull renderer (the model and its plates; not its effects), read once
        /// at the formation's start. A hull with nothing drawn yet is taken as the spindle: <paramref name="halfLength"/>
        /// long each way and a share of that wide and high.
        /// </summary>
        public static Bounds LocalBounds(Ark ark, float halfLength)
        {
            var fallback = new Bounds(Vector3.zero,
                new Vector3(halfLength * 2f * SpindleHalfWidthShare, halfLength * 2f * SpindleHalfWidthShare, halfLength * 2f));
            if (!ark) return fallback;
            var t = ark.transform;
            bool any = false;
            var b = new Bounds();
            foreach (var r in ark.GetComponentsInChildren<Renderer>())
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer or TrailRenderer or LineRenderer) continue;
                var w = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = w.center + Vector3.Scale(w.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var local = t.InverseTransformPoint(corner);
                    if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                    else b.Encapsulate(local);
                }
            }
            // A world AABB read back into a turned frame overstates the hull a little: harmless for a slot, which only
            // has to be clear of it.
            return any ? b : fallback;
        }

        /// <summary>
        /// Where the vessel <paramref name="launchOrder"/> places into the launch waits, in the Ark's local frame: its
        /// hangar pad (the first to go nearest the doors, so nobody launches through a vessel still waiting), and the
        /// point ahead of the doors it flies out to. A model with no hangar waits at its nose.
        /// </summary>
        public static void Hangar(Ark ark, int launchOrder, float halfLength, ArkFormationSettings settings,
                                  out Vector3 padLocal, out Vector3 exitLocal)
        {
            settings ??= new ArkFormationSettings();
            var t = ark.transform;
            var model = ark.Model;
            int pad = PadCount - 1 - Mathf.Clamp(launchOrder, 0, PadCount - 1);
            if (model && model.TryGetBone(PadBone + pad, out var padBone) &&
                model.TryGetBone(DoorTopBone, out var top) && model.TryGetBone(DoorBottomBone, out var bottom))
            {
                padLocal = t.InverseTransformPoint(padBone.position) + Vector3.up * settings.PadLift;
                var mouth = t.InverseTransformPoint(0.5f * (top.position + bottom.position));
                // Straight out along the hangar's own line (the pad's height and side), past the jaws.
                exitLocal = new Vector3(padLocal.x, mouth.y, mouth.z + settings.ExitClear);
                return;
            }
            padLocal = new Vector3(0f, 0f, halfLength);
            exitLocal = padLocal + Vector3.forward * settings.ExitClear;
        }

        /// <summary>
        /// A launch at <paramref name="t"/> (0 on the pad, 1 in the slot), in the Ark's frame: straight out of the hangar
        /// (<paramref name="exitShare"/> of the time, speeding up), then a turn out to the slot's side and back along
        /// the hull into it, slowing to a stop. <paramref name="velocity"/> is the move's direction and speed relative to
        /// the Ark, per unit of t.
        /// </summary>
        public static Vector3 LaunchPath(Vector3 pad, Vector3 exit, Vector3 slot, float exitShare, float t, out Vector3 velocity)
        {
            t = Mathf.Clamp01(t);
            exitShare = Mathf.Clamp(exitShare, 0.05f, 0.95f);
            if (t < exitShare)
            {
                float u = t / exitShare;
                velocity = (exit - pad) * (2f * u / exitShare);
                return Vector3.Lerp(pad, exit, u * u);
            }
            float s = (t - exitShare) / (1f - exitShare);
            // Ease out: fast as it leaves the doors, settling into the slot.
            float e = 1f - (1f - s) * (1f - s);
            float de = 2f * (1f - s) / (1f - exitShare);
            Vector3 d = slot - exit;
            Vector3 side = Vector3.ProjectOnPlane(d, Vector3.forward);
            float span = d.magnitude;
            Vector3 c1 = exit + Vector3.forward * (span * 0.35f) + side * 0.35f;
            Vector3 c2 = slot + Vector3.forward * (span * 0.45f);
            velocity = Bezier1(exit, c1, c2, slot, e) * de;
            return Bezier(exit, c1, c2, slot, e);
        }

        /// <summary>
        /// Which way a launching vessel faces, in the Ark's frame: along its move across and up, but always nose forward
        /// (a vessel falling back into its slot is slower than the Ark, not flying backwards), settling to the Ark's
        /// heading as it stops.
        /// </summary>
        public static Quaternion LaunchFacing(Vector3 velocity, float settle)
        {
            float speed = velocity.magnitude;
            if (speed < 1e-4f) return Quaternion.identity;
            var dir = new Vector3(velocity.x, velocity.y, Mathf.Max(Mathf.Abs(velocity.z), 0.6f * speed));
            var facing = Quaternion.LookRotation(dir.normalized, Vector3.up);
            return Quaternion.Slerp(facing, Quaternion.identity, Mathf.Clamp01(settle));
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        static Vector3 Bezier1(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }
    }
}
