using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How well an AI Dolphin SEES and DODGES prisms that are not the course's doors - rival and own trails, stray
    /// blocks, anything on the TrailBlocks layer (SWITCHBACK.md "AI: any prism in the way"). One row per AIDifficulty
    /// (1 Easy .. 4 Legendary; <see cref="ForLevel"/>).
    /// </summary>
    [Serializable]
    public struct SwitchbackAvoidSkill
    {
        [Tooltip("u ahead the pilot looks for prisms.")]
        public float SenseRange;
        [Tooltip("s between looks (its reaction time); looks are staggered per pilot.")]
        public float ReactionSeconds;
        [Tooltip("u of air the pilot wants between its line and any prism.")]
        public float Clearance;
        [Tooltip("How many cones of escape directions it tries (1 = one ring at 25 deg; 4 = 12/25/40/60 deg).")]
        public int Rings;
        [Tooltip("0..1 share of the sensed prisms a blast slot must cover for the pilot to blast instead of dodge (> 1 = never).")]
        public float BlastMinCoverage;
        [Tooltip("u: the farthest crystal it will drift into to blast a sensed obstacle.")]
        public float BlastCrystalRange;
        [Tooltip("0..1 of the gate radius: this pilot's own lane through the ring, so AIs do not fly one another's trails.")]
        public float LaneShare;

        public SwitchbackAvoidSkill(float senseRange, float reactionSeconds, float clearance, int rings,
                                    float blastMinCoverage, float blastCrystalRange, float laneShare)
        {
            SenseRange = senseRange; ReactionSeconds = reactionSeconds; Clearance = clearance; Rings = rings;
            BlastMinCoverage = blastMinCoverage; BlastCrystalRange = blastCrystalRange; LaneShare = laneShare;
        }

        /// <summary>
        /// The emergency sweep's look-ahead in seconds of real flight (reach = speed x this, at least
        /// <see cref="SwitchbackPrismAvoidance.EmergencyMinReach"/>): Easy sees an impact 0.45 s out, Legendary 1.0 s (each +0.1 s after the Nov 2026 hit soak).
        /// Present at every level - only how early it sees changes.
        /// </summary>
        public static float EmergencySeconds(int level) => level switch { 1 => 0.45f, 3 => 0.8f, 4 => 1.0f, _ => 0.6f };

        public static SwitchbackAvoidSkill ForLevel(int level) => level switch
        {
            1 => new SwitchbackAvoidSkill(70f, 0.45f, 5f, 1, 2f, 0f, 0.35f),
            3 => new SwitchbackAvoidSkill(150f, 0.18f, 9f, 3, 0.55f, 160f, 0.45f),
            4 => new SwitchbackAvoidSkill(200f, 0.10f, 10f, 4, 0.45f, 220f, 0.5f),
            _ => new SwitchbackAvoidSkill(110f, 0.30f, 7f, 2, 0.70f, 110f, 0.4f),
        };
    }

    /// <summary>What the sense decided for one look.</summary>
    public enum SwitchbackAvoidAction : byte { Clear = 0, Avoid = 1, Blast = 2 }

    /// <summary>
    /// The pure geometry of "a prism is in the way": is the line blocked, which corridor round it is clearest, and can a
    /// nearby crystal's blast slot clear it instead. Point sets in, no allocation, no physics - the physics overlap that
    /// fills the points is <see cref="SwitchbackBlastAI"/>'s, so this runs headless in sb_harness exactly as in a match.
    /// </summary>
    public static class SwitchbackPrismAvoidance
    {
        static readonly float[] RingAngles = { 12f, 25f, 40f, 60f };
        const int Azimuths = 8;
        /// <summary>Score lost per degree a corridor turns off the goal (u of clearance per deg).</summary>
        const float TurnCost = 0.08f;

        /// <summary>Closest any of the first <paramref name="n"/> points comes to the segment a→a+dir*len (∞ when none
        /// lies along it), and how far along the nearest blocker sits.</summary>
        public static float SegmentClearance(Vector3 a, Vector3 dir, float len, Vector3[] pts, int n, out float along,
                                             float[] radii = null)
        {
            float best = float.PositiveInfinity; along = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                Vector3 w = pts[i] - a;
                float t = Vector3.Dot(w, dir);
                if (t < 0f || t > len) continue;
                float d = (w - dir * t).magnitude - (radii != null ? radii[i] : 0f);
                if (d < best) { best = d; along = t; }
            }
            return best;
        }

        public static bool Blocked(Vector3 self, Vector3 dir, float range, float clearance, Vector3[] pts, int n, out float along,
                                   float[] radii = null) =>
            SegmentClearance(self, dir, range, pts, n, out along, radii) < clearance;

        /// <summary>The clearest corridor near <paramref name="goalDir"/>: the goal itself if clear, else the escape
        /// direction (rings of 8 round the goal, as many rings as the skill has) that keeps the most air for the least
        /// turn. <paramref name="clearance"/> is what that corridor keeps.</summary>
        public static Vector3 ChooseCorridor(Vector3 self, Vector3 goalDir, float range, Vector3[] pts, int n,
                                             in SwitchbackAvoidSkill skill, out float clearance, float[] radii = null)
        {
            clearance = SegmentClearance(self, goalDir, range, pts, n, out _, radii);
            if (clearance >= skill.Clearance) return goalDir;
            Vector3 right = Vector3.Cross(Mathf.Abs(goalDir.y) < 0.95f ? Vector3.up : Vector3.right, goalDir).normalized;
            Vector3 up = Vector3.Cross(goalDir, right);
            Vector3 best = goalDir;
            float bestScore = Mathf.Min(clearance, range);
            int rings = Mathf.Clamp(skill.Rings, 1, RingAngles.Length);
            for (int r = 0; r < rings; r++)
            {
                float ang = rings == 1 ? 25f : RingAngles[r];
                float s = Mathf.Sin(ang * Mathf.Deg2Rad), c = Mathf.Cos(ang * Mathf.Deg2Rad);
                for (int k = 0; k < Azimuths; k++)
                {
                    float az = k * (2f * Mathf.PI / Azimuths);
                    Vector3 d = (goalDir * c + (right * Mathf.Cos(az) + up * Mathf.Sin(az)) * s).normalized;
                    float cl = Mathf.Min(SegmentClearance(self, d, range, pts, n, out _, radii), range);
                    float score = Mathf.Min(cl, skill.Clearance * 3f) - ang * TurnCost;
                    if (score > bestScore) { bestScore = score; best = d; clearance = cl; }
                }
            }
            return best;
        }

        /// <summary>Mean of the points within the first <paramref name="n"/> that lie inside the blocked tube.</summary>
        public static Vector3 BlockerCentroid(Vector3 self, Vector3 dir, float range, float band, Vector3[] pts, int n, out int count)
        {
            Vector3 sum = Vector3.zero; count = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 w = pts[i] - self;
                float t = Vector3.Dot(w, dir);
                if (t < 0f || t > range || (w - dir * t).sqrMagnitude > band * band) continue;
                sum += pts[i]; count++;
            }
            return count > 0 ? sum / count : self + dir * range;
        }

        /// <summary>
        /// Can a crystal blow the blocker away? The best crystal ahead (within the skill's range, not behind, and closer
        /// than the blocker) whose slot - fired from the crystal at the blocker, its gape along the blocker's widest
        /// spread - covers at least <see cref="SwitchbackAvoidSkill.BlastMinCoverage"/> of the blocking prisms.
        /// </summary>
        public static bool TryBlast(Vector3 self, Vector3 heading, Vector3 blocker, float band, Vector3[] pts, int n,
                                    Vector3[] crystals, int nc, in SwitchbackBlastSlot slot, in SwitchbackAvoidSkill skill,
                                    out Vector3 crystal, out Vector3 fireForward, out Vector3 fireUp, out float coverage)
        {
            crystal = default; fireForward = default; fireUp = default; coverage = 0f;
            if (skill.BlastMinCoverage > 1f || skill.BlastCrystalRange <= 0f || nc <= 0) return false;
            float toBlocker = Vector3.Distance(self, blocker);
            bool found = false;
            for (int j = 0; j < nc; j++)
            {
                Vector3 c = crystals[j];
                Vector3 toC = c - self;
                float dc = toC.magnitude;
                if (dc > skill.BlastCrystalRange || dc < 1f || dc >= toBlocker || Vector3.Dot(toC, heading) < 0.5f * dc) continue;  // within 60 deg ahead
                Vector3 f = blocker - c;
                float df = f.magnitude;
                if (df < 5f || df > slot.Reach || Vector3.Distance(c, blocker) >= toBlocker + 30f) continue;
                f /= df;
                Vector3 u = SpreadAxis(f, pts, n, blocker, band);
                int hit = 0, tot = 0;
                for (int i = 0; i < n; i++)
                {
                    if ((pts[i] - blocker).sqrMagnitude > (band * 3f) * (band * 3f)) continue;
                    tot++;
                    if (slot.Covers(c, f, u, pts[i], 2f)) hit++;
                }
                float cov = tot > 0 ? hit / (float)tot : 0f;
                if (cov >= skill.BlastMinCoverage && cov > coverage)
                {
                    coverage = cov; crystal = c; fireForward = f; fireUp = u; found = true;
                }
            }
            return found;
        }

        /// <summary>The blocker's widest spread across <paramref name="f"/> (the slot's gape goes along it).</summary>
        static Vector3 SpreadAxis(Vector3 f, Vector3[] pts, int n, Vector3 centre, float band)
        {
            Vector3 right = Vector3.Cross(Mathf.Abs(f.y) < 0.95f ? Vector3.up : Vector3.right, f).normalized;
            Vector3 up = Vector3.Cross(f, right);
            // 2x2 covariance in (right, up), principal axis in closed form.
            float sxx = 0f, syy = 0f, sxy = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 w = pts[i] - centre;
                if (w.sqrMagnitude > (band * 3f) * (band * 3f)) continue;
                float x = Vector3.Dot(w, right), y = Vector3.Dot(w, up);
                sxx += x * x; syy += y * y; sxy += x * y;
            }
            float th = 0.5f * Mathf.Atan2(2f * sxy, sxx - syy);
            return (right * Mathf.Cos(th) + up * Mathf.Sin(th)).normalized;
        }

        /// <summary>This pilot's lane through a ring: a fixed point in the ring's plane, <paramref name="share"/> of the
        /// radius out at most, from the pilot's seed. Deterministic per pilot, so every AI keeps its own line and no
        /// AI flies the exact path a rival (whose trail is still there) just flew.</summary>
        public static Vector3 LaneOffset(uint seed, Vector3 axis, float radius, float share)
        {
            Vector3 right = Vector3.Cross(Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right, axis).normalized;
            Vector3 up = Vector3.Cross(axis, right);
            uint h = SwitchbackBlastPlanner.Hash(seed);
            float az = (h & 0xFFFF) / 65536f * 2f * Mathf.PI;
            float rr = Mathf.Sqrt(0.25f + 0.75f * ((h >> 16) / 65536f));   // never the dead centre
            return (right * Mathf.Cos(az) + up * Mathf.Sin(az)) * (rr * share * radius);
        }
    
        /// <summary>The emergency sweep never looks shorter than this, u (slow hulls still need room to turn).</summary>
        /// <summary>
        /// Whether a sensed prism is one to dodge (SWITCHBACK.md "Step 1: dodge only what costs", 2026-11-02). A plain prism
        /// costs the Dolphin about 0.25 s when it is hit (x0.5 speed easing back over 1 s), a Danger prism about 1.5 s (a dead
        /// stop easing back over 3 s), and a dodge costs more than either when it pulls the hull off its line: in the real
        /// soak (`soak_results.md`) 35 of 38 course hits were side-swipes taken while dodging, and offline every level that
        /// dodged more raced slower. So with <paramref name="dangerOnly"/> only a Danger prism is dodged, and nothing is
        /// dodged within <paramref name="noDodgeNear"/> u of the ring: there the line to the mouth (or a blocked gate's eye,
        /// ringed by Danger rakes) is the line. <paramref name="noDodgeNear"/> 0 = dodge up to the ring.
        /// </summary>
        public static bool Dodges(bool isDanger, float distanceToRing, bool dangerOnly, float noDodgeNear) =>
            (isDanger || !dangerOnly) && !(noDodgeNear > 0f && distanceToRing < noDodgeNear);

        public const float EmergencyMinReach = 25f;
        /// <summary>The sweep's sphere: the hull's half-width, u, plus a share of the level's clearance.</summary>
        public const float ShipRadius = 3f;

        /// <summary>How far ahead the emergency sweep looks at <paramref name="speed"/> u/s.</summary>
        public static float EmergencyReach(float speed, float seconds) => Mathf.Max(EmergencyMinReach, speed * seconds);

        /// <summary>
        /// The dodge for an imminent hit: off the travel line <paramref name="dir"/>, away from the obstacle's centre
        /// (<paramref name="away"/> = self - obstacle), falling back to the hit normal, then to <paramref name="up"/>.
        /// The nearer the hit (<paramref name="urgency"/> 0 far .. 1 touching) the harder the break. Unit length.
        /// </summary>
        public static Vector3 EscapeDirection(Vector3 dir, Vector3 away, Vector3 normal, Vector3 up, float urgency)
        {
            Vector3 lat = Vector3.ProjectOnPlane(away, dir);
            if (lat.sqrMagnitude < 0.01f) lat = Vector3.ProjectOnPlane(normal, dir);
            if (lat.sqrMagnitude < 0.01f) lat = Vector3.ProjectOnPlane(up, dir);
            if (lat.sqrMagnitude < 0.01f) lat = Vector3.Cross(dir, Vector3.right);
            float k = Mathf.Lerp(0.6f, 0.95f, Mathf.Clamp01(urgency));
            return (dir * (1f - k) + lat.normalized * k).normalized;
        }
    }
}
