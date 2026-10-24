using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Jacks' arithmetic, ported line for line from the Swarm Vessel Lab
    /// (<c>Docs/Studios/SwarmVesselLab/src/shepherd.js</c>: <c>SEL</c>, <c>stepSheep</c>, <c>repellers</c>, <c>gatherPadDogs</c>,
    /// <c>camUpdate</c>). Pure: every length is passed in, already in game units (the lab's lengths times
    /// <see cref="JacksConfigSO.LabScale"/>), so the edit-mode tests can hold it to the lab's own numbers
    /// (<c>JacksMathTests</c>). Design: <c>R_VesselActions/JACKS.md</c>.
    /// </summary>
    public static class JacksMath
    {
        /// <summary>The lab's nominal phone: 6.3 CSS px per mm, and a landscape phone's short side, 390 px.</summary>
        public const float PxPerMm = 6.3f, PhoneShortPx = 390f;

        /// <summary>The lab's element range: -5 .. 15 (Garrett's Charge scale: 0% at -5, 100% at 15).</summary>
        public const float MinLevel = -5f, MaxLevel = 15f;

        /// <summary>
        /// The ring's floor (the lab's <c>SEL.floorU</c>): half a big thumb, in screen pixels on the nominal phone, times the
        /// margin, in world units of a view <paramref name="viewSpan"/> across the screen's short side. "The minimum
        /// repulsion radius should be larger than a big thumb's occlusion" (Garrett, 2026-10-20).
        /// </summary>
        public static float FloorUnits(float thumbMm, float margin, float viewSpan) =>
            thumbMm * 0.5f * PxPerMm * margin * viewSpan / PhoneShortPx;

        /// <summary>
        /// The jacks' view span (game units across the screen's short side) at which a ring of radius <paramref name="floor"/>
        /// is <paramref name="cover"/> of a <paramref name="thumbMm"/> thumb across, on a phone whose screen's short side is
        /// <paramref name="phoneShortMm"/>: the ring's diameter on screen is 2 x floor / span x phoneShortMm (Garrett,
        /// 2026-10-21: "zoomed out enough that on a full size mobile device an above average sized thumb barely covers the
        /// smallest jack repulsion radius").
        /// </summary>
        public static float ThumbViewSpan(float floor, float thumbMm, float cover, float phoneShortMm) =>
            2f * floor * phoneShortMm / Mathf.Max(1e-3f, thumbMm * cover);

        /// <summary>The ring every herder repels inside (the lab's <c>SEL.ring</c>): the floor at Space -5, +6% of it per level
        /// above. Space never takes it below the floor.</summary>
        public static float Ring(float floor, float space) => floor * (1f + 0.06f * Mathf.Max(0f, space + 5f));

        /// <summary>The herd's speed multiplier (the lab's <c>SEL.speed</c>): +6% per Time level, never below 0.4.</summary>
        public static float SpeedMultiplier(float time) => Mathf.Max(0.4f, 1f + 0.06f * time);

        /// <summary>
        /// How many of its species' feeds a herded creature needs for a birth, as a share (the lab's <c>SEL.meals</c> over its
        /// <c>breedFood</c>): 1 at Mass -5, 1/1.7 at Mass 2, 1/3 at Mass 15. The species' own <c>FeedsPerOffspring</c> is the
        /// Mass -5 number.
        /// </summary>
        public static float FeedsPerOffspringScale(float mass) => 1f / (1f + 0.1f * Mathf.Max(0f, mass + 5f));

        /// <summary>The share of the herd that is DANGER (the lab's <c>SEL.danger</c>): 0 at Charge -5, 1 at Charge 15.</summary>
        public static float DangerShare(float charge) => Mathf.Clamp01((charge + 5f) / 20f);

        /// <summary>A creature is danger while its birth roll (0..1, fixed for life) is under the danger share.</summary>
        public static bool IsDanger(float roll, float charge) => roll < DangerShare(charge);

        /// <summary>A creature's fixed roll, 0..1, from a stable id (vessel rule 18: a hash, not Random).</summary>
        public static float Roll01(int id)
        {
            uint n = (uint)id;
            n ^= n >> 16; n *= 0x7feb352dU; n ^= n >> 15; n *= 0x846ca68bU; n ^= n >> 16;
            return (n & 0xFFFFFF) / 16777216f;
        }

        /// <summary>The component of <paramref name="v"/> in the plane whose normal is <paramref name="up"/>.</summary>
        public static Vector3 InPlane(Vector3 v, Vector3 up) => v - up * Vector3.Dot(v, up);

        /// <summary>
        /// One herder's push on a creature (the lab's repeller loop): a cylinder along the plane's normal, so it reads from
        /// above. Inside the ring the push grows linearly to the centre (3.2 x (1 - d/r) along the in-plane offset) and the
        /// creature's panic is 1 - d/r. Outside the ring: nothing.
        /// </summary>
        public static Vector3 Repel(Vector3 creature, Vector3 herder, float ring, Vector3 up, out float panic)
        {
            panic = 0f;
            if (ring <= 0f) return Vector3.zero;
            var dv = InPlane(creature - herder, up);
            float d = dv.magnitude;
            if (d >= ring) return Vector3.zero;
            float k = 1f - d / ring;
            panic = k;
            return dv * (3.2f * k / (d > 1e-5f ? d : 1f));
        }

        /// <summary>The CALL (the lab's <c>calling</c> branch): an in-plane pull of 2.6 toward <paramref name="caller"/> (the
        /// jacks), beyond <paramref name="deadZone"/>; the caller raises panic to at least 0.5.</summary>
        public static Vector3 CallPull(Vector3 creature, Vector3 caller, Vector3 up, float deadZone)
        {
            var dv = InPlane(caller - creature, up);
            float d = dv.magnitude;
            return d > deadZone && d > 1e-5f ? dv * (2.6f / d) : Vector3.zero;
        }

        /// <summary>
        /// The flock's own terms (the lab's neighbour loop): separation inside <paramref name="separation"/> (weighted 1.6),
        /// alignment with the neighbours' mean velocity (0.5 / cruise) and cohesion toward their centre (0.6 / neighbour
        /// radius). The sums are what the caller gathered over neighbours within <paramref name="neighbourRadius"/>.
        /// </summary>
        public static Vector3 Flock(Vector3 separationSum, Vector3 velocitySum, Vector3 centreSum, int neighbours,
                                    Vector3 creature, float cruise, float neighbourRadius)
        {
            var g = separationSum * 1.6f;
            if (neighbours <= 0) return g;
            float n = neighbours;
            return g + velocitySum / n * (0.5f / Mathf.Max(1e-3f, cruise))
                     + (centreSum / n - creature) * (0.6f / Mathf.Max(1e-3f, neighbourRadius));
        }

        /// <summary>One neighbour's separation term (the lab's <c>(58 - d) / 58 / d</c>), added to the sum <see cref="Flock"/>
        /// weighs.</summary>
        public static Vector3 SeparationTerm(Vector3 offsetFromNeighbour, float separation)
        {
            float d = offsetFromNeighbour.magnitude;
            if (d >= separation || separation <= 0f) return Vector3.zero;
            if (d < 1e-5f) d = 1f;
            return offsetFromNeighbour * ((separation - d) / separation / d);
        }

        /// <summary>The creature's own wander along the board (the lab's seeded smooth wander, 0.25 each way).</summary>
        public static Vector3 Wander(float phase, float roll, Vector3 boardRight, Vector3 boardForward) =>
            boardRight * (Mathf.Cos(phase * 1.7f + roll * 9f) * 0.25f) + boardForward * (Mathf.Sin(phase * 1.3f + roll * 5f) * 0.25f);

        /// <summary>The wander's phase rate (1/s): 0.6 plus 0.8 x the creature's roll.</summary>
        public static float WanderRate(float roll) => 0.6f + roll * 0.8f;

        /// <summary>
        /// The in-plane velocity the creature wants (the lab's <c>want</c>): the in-plane steer, scaled to the cruise times
        /// 1 + 0.8 x panic, at its full length once the steer is 1 or more.
        /// </summary>
        public static Vector3 Want(Vector3 steer, Vector3 up, float cruise, float panic)
        {
            var gp = InPlane(steer, up);
            float gm = gp.magnitude, vmax = cruise * (1f + 0.8f * panic);
            return gm > 0.02f ? gp * (vmax * Mathf.Min(1f, gm) / gm) : Vector3.zero;
        }

        /// <summary>
        /// The speed across the plane the creature heads for (the game's reading of the lab's plane spring
        /// <c>vn' = -h x planePull - 1.8 vn + 40 gn</c>): its terminal velocity, <c>(-h x planePull + 40 gn) / 1.8</c>, capped at
        /// the creature's top speed either way. <paramref name="height"/> is its height above the plane, <paramref name="gn"/>
        /// the steer's component along the plane's normal.
        /// </summary>
        public static float PlaneSpeed(float height, float gn, float planePull, float top) =>
            Mathf.Clamp((-height * planePull + 40f * gn) / 1.8f, -top, top);

        /// <summary>A jack runs to its target along the plane at <paramref name="speed"/> (the lab's dog step), arriving
        /// exactly.</summary>
        public static Vector3 StepToward(Vector3 from, Vector3 to, float speed, float dt)
        {
            var dv = to - from;
            float l = dv.magnitude, step = speed * dt;
            return l > step && l > 1e-6f ? from + dv / l * step : to;
        }

        /// <summary>
        /// A gamepad stick's run on screen (the lab's <c>gatherPadDogs</c>): a 0.15 dead zone rescaled to 0..1, then a full
        /// stick runs at <paramref name="speed"/> along a 1.4 curve. Returns the screen-plane displacement this frame, x right
        /// and y UP the screen (stick up = up).
        /// </summary>
        public static Vector2 StickRun(Vector2 stick, float speed, float dt)
        {
            float x = DeadZone(stick.x), y = DeadZone(stick.y);
            float m = Mathf.Min(1f, Mathf.Sqrt(x * x + y * y));
            if (m <= 0f) return Vector2.zero;
            float len = Mathf.Sqrt(x * x + y * y);
            float step = speed * Mathf.Pow(m, 1.4f) * dt / len;
            return new Vector2(x * step, y * step);
        }

        /// <summary>The lab's stick dead zone: |v| under 0.15 is 0, the rest rescaled to 0..1.</summary>
        public static float DeadZone(float v)
        {
            float a = Mathf.Abs(v);
            return a < 0.15f ? 0f : Mathf.Sign(v) * (a - 0.15f) / 0.85f;
        }

        /// <summary>
        /// The Steady view's pan (the lab's <c>camUpdate</c>, <c>steady</c>): only the part of the offset to the goal that lies
        /// outside a slack box of half-size (<paramref name="halfWidth"/>, <paramref name="halfHeight"/>) moves the view,
        /// eased at 1.6/s. <paramref name="offsetRight"/> and <paramref name="offsetForward"/> are the goal's offset from the
        /// view's centre along the view's right and forward. <paramref name="ease"/> is the lab's 1.6/s by default.
        /// </summary>
        public static Vector2 SteadyPan(float offsetRight, float offsetForward, float halfWidth, float halfHeight, float dt,
                                        float ease = 1.6f)
        {
            float a = Mathf.Min(1f, dt * ease);
            float pr = offsetRight - Mathf.Clamp(offsetRight, -halfWidth, halfWidth);
            float pf = offsetForward - Mathf.Clamp(offsetForward, -halfHeight, halfHeight);
            return new Vector2(pr * a, pf * a);
        }

        /// <summary>
        /// The Steady view's heading (the lab's <c>camUpdate</c>): the last forward laid onto the new plane - the smallest
        /// turn that keeps the view straight down the plane - so it never turns with the hull's heading. Past 78° of
        /// tilt in one frame it takes the board's forward instead.
        /// </summary>
        public static Vector3 SteadyForward(Vector3 lastForward, Vector3 up, Vector3 boardForward)
        {
            var nf = InPlane(lastForward, up);
            if (nf.magnitude < 0.2f) nf = InPlane(boardForward, up);
            return nf.sqrMagnitude > 1e-8f ? nf.normalized : boardForward;
        }

        /// <summary>The goal the view frames: the herd's centre weighted toward the hull by <paramref name="hullWeight"/>
        /// (0 = the herd alone), or <paramref name="noHerd"/> with no herd.</summary>
        public static Vector3 FrameGoal(Vector3 herdCentre, int herdCount, Vector3 hull, float hullWeight, Vector3 noHerd) =>
            herdCount > 0 ? Vector3.Lerp(herdCentre, hull, Mathf.Clamp01(hullWeight)) : noHerd;

        /// <summary>
        /// A flying jack's place in the hull's frame (x right, y up, z forward): <paramref name="side"/> (-1 the left jack,
        /// +1 the right) times <paramref name="gap"/> from the centre line, moved by its stick (clamped to the unit disc)
        /// times <paramref name="reach"/>. So the fish are the thumbs (Garrett, 2026-10-21): one up and one down is a roll,
        /// both the same way a pitch or a yaw, apart is faster and together slower - the fleet's own two-thumb flight reads
        /// the same sticks.
        /// </summary>
        public static Vector3 FlightOffset(float side, Vector2 stick, float gap, float reach)
        {
            var s = Vector2.ClampMagnitude(stick, 1f);
            return new Vector3(side * gap + s.x * reach, s.y * reach, 0f);
        }

        /// <summary>The board's ease toward the hull's frame this frame (1 - e^(-rate dt)): 0 holds it still.</summary>
        public static float Ease(float rate, float dt) => rate > 0f ? 1f - Mathf.Exp(-rate * dt) : 0f;
    }
}
