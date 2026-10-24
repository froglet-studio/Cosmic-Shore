using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Flea's arithmetic, ported line for line from the Vessel Studio's concept fleet
    /// (<c>Docs/Studios/VesselStudio/studio-kinds.js</c>, kind <c>ricochet</c>: <c>step</c>, <c>normal</c>, <c>onContact</c>;
    /// the element scaling is <c>studio-vessel.js</c> <c>mults</c>; the numbers are <c>vessels/flea.json</c> and the kind's
    /// <c>defaults</c>). The Flea crawls between surfaces and bounces off mass: press RT inside a short window round the
    /// contact for a PERFECT bounce that gains speed (a chain), miss it for a DULL one that loses speed; hold LT to crouch and
    /// release to hop. Pure, so <c>FleaMathTests</c> holds it to the page's own numbers. Design: <c>R_VesselActions/FLEA.md</c>.
    /// </summary>
    public static class FleaMath
    {
        /// <summary>Every number the step reads. Field names are the page's keys.</summary>
        public struct Params
        {
            public float crawl, window, perfectGain, dullLoss, topSpeed, grip, drag, hopCharge, hopMin, hopMax;

            /// <summary>The kind's <c>defaults</c> (studio-kinds.js, <c>K.ricochet.defaults</c>).</summary>
            public static Params Studio => new Params
            {
                crawl = 50f, window = 0.18f, perfectGain = 1.35f, dullLoss = 0.85f, topSpeed = 260f,
                grip = 2.2f, drag = 0.25f, hopCharge = 0.6f, hopMin = 60f, hopMax = 160f,
            };
        }

        /// <summary>The speed a crouching Flea settles to, and how fast (studio-kinds.js <c>step</c>: <c>15</c>, <c>4</c>).</summary>
        public const float CrouchSpeed = 15f, CrouchRate = 4f;
        /// <summary>How fast a Flea below its crawl speed comes back up to it (<c>step</c>: <c>2.0</c>).</summary>
        public const float CrawlCatchUp = 2f;
        /// <summary>A chain that is not kept up for this long is lost (<c>step</c>: <c>4</c>).</summary>
        public const float ChainHold = 4f;
        /// <summary>A hop needs at least this much charge (<c>step</c>: <c>0.05</c>).</summary>
        public const float MinHopCharge = 0.05f;
        /// <summary>A bounce puts the hull this far out along the normal (<c>onContact</c>: <c>2.5</c>).</summary>
        public const float BouncePush = 2.5f;
        /// <summary>The trigger depth that counts as a press (<c>step</c>: RT <c>&gt; 0.5</c>, LT <c>&gt; 0.3</c>).</summary>
        public const float RtPress = 0.5f, LtPress = 0.3f;

        /// <summary>
        /// The element scaling (studio-vessel.js <c>mults</c>): a parameter times <c>lerp(range0, range1, clamp(level / 10))</c>.
        /// The ranges are <c>vessels/flea.json</c>'s: Charge "Spring" scales <c>perfectGain</c> 1 to 1.15, Mass "Momentum"
        /// <c>topSpeed</c> 1 to 1.2, Space "Big Hop" <c>hopMax</c> 1 to 1.5, Time "Wide Window" <c>window</c> 1 to 1.7.
        /// These are the studio's proposals, not a signed-off element map.
        /// </summary>
        public static Params AtLevels(Params p, float charge, float mass, float space, float time)
        {
            p.perfectGain *= Scale(1f, 1.15f, charge);
            p.topSpeed *= Scale(1f, 1.2f, mass);
            p.hopMax *= Scale(1f, 1.5f, space);
            p.window *= Scale(1f, 1.7f, time);
            return p;
        }

        static float Scale(float a, float b, float level) => a + (b - a) * Mathf.Clamp01(level / 10f);

        /// <summary>The Flea's state between frames (the page's <c>P.speed</c>, <c>P.course</c> and <c>P.K</c>).</summary>
        public struct State
        {
            public float speed;
            public Vector3 course;
            public float pressT, bounceT, dullAt, charge;
            public int chain, perfects;
            public bool crouch, ltWas, rtWas;

            /// <summary>The page's <c>init</c>: every clock at -9 (long ago), nothing charged.</summary>
            public static State Start(Vector3 course, float speed) => new State
            {
                speed = speed, course = course, pressT = -9f, bounceT = -9f, dullAt = -9f,
            };
        }

        /// <summary>What a frame did, for the HUD and the feedback.</summary>
        public enum Event { None = 0, LatePerfect = 1, Hop = 2 }

        /// <summary>
        /// One frame (the page's <c>step</c>, after its <c>ctx.steer</c>): the RT press, the crouch, the hop, the crawl, the
        /// velocity grip toward the nose, and the chain's decay. <paramref name="forward"/> is the hull's nose; the caller
        /// moves the hull by <c>course x speed</c>.
        /// </summary>
        public static Event Step(ref State s, in Params p, Vector3 forward, float lt, float rt, float time, float boost, float dt)
        {
            var ev = Event.None;
            // an RT press: the window opens; a press just AFTER a dull bounce (inside half the window) turns it perfect
            bool rtNow = rt > RtPress;
            if (rtNow && !s.rtWas)
            {
                s.pressT = time;
                if (time - s.dullAt <= p.window / 2f)
                {
                    s.speed = Mathf.Min(p.topSpeed, s.speed * (p.perfectGain / p.dullLoss));
                    s.chain++; s.perfects++; s.dullAt = -9f; ev = Event.LatePerfect;
                }
            }
            s.rtWas = rtNow;

            s.crouch = lt > LtPress;
            if (s.crouch)
            {
                s.ltWas = true;
                s.charge = Mathf.Min(1f, s.charge + dt / p.hopCharge);
                s.speed += (CrouchSpeed - s.speed) * (1f - Mathf.Exp(-CrouchRate * dt));
            }
            else
            {
                bool released = s.ltWas; s.ltWas = false;
                if (released && s.charge > MinHopCharge)
                {
                    s.speed = Mathf.Max(s.speed, Mathf.Lerp(p.hopMin, p.hopMax, s.charge));
                    s.course = forward; s.charge = 0f; ev = Event.Hop;
                }
                else
                {
                    float k = s.speed > p.crawl ? p.drag : CrawlCatchUp;
                    s.speed += (p.crawl * boost - s.speed) * (1f - Mathf.Exp(-k * dt));
                }
            }
            s.course = Vector3.Lerp(s.course, forward, 1f - Mathf.Exp(-p.grip * dt)).normalized;
            if (time - s.bounceT > ChainHold) s.chain = 0;
            return ev;
        }

        /// <summary>
        /// The bounce surface (the page's <c>normal</c>): a prism is a long box, so the normal is the hull's offset from the
        /// prism's AXIS (its local z), and a hull at a shallow angle skips along it keeping its along-track speed. On the
        /// axis (under 0.5 u off it) the hull's own up is used.
        /// </summary>
        public static Vector3 BounceNormal(Vector3 hull, Vector3 prismPosition, Vector3 prismAxis, Vector3 hullUp)
        {
            Vector3 d = hull - prismPosition;
            Vector3 n = d - prismAxis * Vector3.Dot(d, prismAxis);
            if (n.sqrMagnitude < 0.25f) n = hullUp;
            return n.normalized;
        }

        /// <summary>
        /// A contact (the page's <c>onContact</c>): the velocity reflects off <paramref name="normal"/>; inside half the window
        /// after an RT press the bounce is PERFECT (speed x perfectGain, the chain grows), else DULL (x dullLoss, the chain
        /// breaks and a late press can still save it). The caller pushes the hull out by <see cref="BouncePush"/>.
        /// </summary>
        /// <returns>True for a perfect bounce.</returns>
        public static bool Bounce(ref State s, in Params p, Vector3 normal, float time)
        {
            Vector3 vel = s.course * s.speed;
            float vn = Vector3.Dot(vel, normal);
            if (vn < 0f) vel -= 2f * vn * normal;
            bool perfect = time - s.pressT >= 0f && time - s.pressT <= p.window / 2f;
            s.speed = Mathf.Min(p.topSpeed, vel.magnitude * (perfect ? p.perfectGain : p.dullLoss));
            if (vel.sqrMagnitude > 1e-4f) s.course = vel.normalized;
            s.bounceT = time;
            if (perfect) { s.chain++; s.perfects++; }
            else { s.chain = 0; s.dullAt = time; }
            return perfect;
        }

        /// <summary>The page's <c>active</c>: a chain that is still alive (a bounce in the last 2.5 s), or a crouch.</summary>
        public static bool Active(in State s, float time) => (s.chain > 0 && time - s.bounceT < 2.5f) || s.crouch;
    }
}
