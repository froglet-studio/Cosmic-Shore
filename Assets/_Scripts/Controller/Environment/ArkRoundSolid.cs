using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The ROUND Ark's solid (<c>ArkRound.fbx</c>, <c>Tools/Build/vessel_model/specs/ark_round.py</c>), for
    /// <see cref="ArkHullSolid"/>: a signed distance over the hull's envelope, with the one passage carved out of it - the
    /// tube from its lip in the gap, the junction hall at the stern and the three straight exits out of the hall (the
    /// squad's launch tubes). The gap between the mouth and the hull is open water, crossed by the walkways and with the
    /// omni crystal in the middle; the pods orbit outside, so their solid turns with them (<paramref name="pods"/>).
    ///
    /// The numbers are the artifact's (ark_round_geometry.js: x the nose axis, y up), carried into the model's Unity
    /// space by the spec's mapping: Unity (x, y, z) = (artifact z, artifact y, artifact x - <see cref="CentreX"/>) *
    /// <see cref="Scale"/>. <c>ArkRoundModelTests</c> holds both numbers to the rig table the build wrote.
    /// </summary>
    public static class ArkRoundSolid
    {
        /// <summary>Artifact units to model units (nose to jets = 196), and the artifact x the model's origin sits at.</summary>
        public const float Scale = 1.154979f, CentreX = -19.45f;

        // ── the artifact's numbers ───────────────────────────────────────────
        const float RT = 8f, HULL_END = 0f, GAP_END = 33f, CX = 21f, STERN = -82.8f, XB = STERN - 9.5f;
        const float WALK_R = 17.5f, JX0 = -71f, JX1 = STERN - 1.5f, JR = 15.5f, JCX = -77.5f, RB = 7.13f;
        const float BRANCH_EX = -91.8f, BRANCH_ER = 30.5f, POD_X0 = -4f, POD_X1 = -78f, POD_R0 = 31.5f, POD_R1 = 35.5f;
        /// <summary>How far the tube's turning rings and their prisms stand in from its wall (round 15: drums at RT - 1.25,
        /// prisms to about RT - 1.5). They turn about the tube's axis, so the solid needs no turn of its own.</summary>
        const float TubeLining = 1.5f;
        const float POD_PR = 4.6f, PRISM_SKIN = 0.9f, BELL_RING = 13.2f;
        static readonly (float x0, float x1)[] Decks = { (-16.5f, -7f), (-30f, -20.5f), (-43.5f, -34f), (-57f, -47.5f), (-70.5f, -61f) };
        static readonly (float x0, float x1, float r)[] MouthStack = { (34.6f, 40f, 12.5f), (40f, 46f, 14f), (46f, 52f, 13f), (52f, 57.5f, 11.5f), (57.5f, 62.6f, 9.5f) };

        /// <summary>Where the model reaches from its origin, model units (the hold-out's reach).</summary>
        public const float Reach = 125f;

        /// <summary>Signed distance (model units) from a point in the model's space at scale 1: negative inside the hull,
        /// positive outside, the tube, the hall and the exits open. <paramref name="pods"/> is how far the pods have turned
        /// (<see cref="ArkModel.TryGetSpinDelta"/>; identity = as built).</summary>
        public static float Distance(Vector3 modelLocal, Quaternion pods)
        {
            var a = ToArtifact(modelLocal);
            float solid = Mathf.Min(Mouth(a), Mathf.Min(Gap(a), Mathf.Min(Hull(a), Stern(a))));
            solid = Mathf.Min(solid, Pods(modelLocal, pods));
            return Mathf.Max(solid, -Passage(a)) * Scale;
        }

        /// <inheritdoc cref="Distance(Vector3, Quaternion)"/>
        public static float Distance(Vector3 modelLocal) => Distance(modelLocal, Quaternion.identity);

        /// <summary>True when the point (model space, scale 1) is in the open passage: the tube, the hall or an exit.</summary>
        public static bool InPassage(Vector3 modelLocal) => Passage(ToArtifact(modelLocal)) < 0f;

        /// <summary>A model-space point (scale 1) of the artifact point (x along the nose axis, y up, z).</summary>
        public static Vector3 FromArtifact(Vector3 a) => new Vector3(a.z, a.y, a.x - CentreX) * Scale;

        static Vector3 ToArtifact(Vector3 m) => new(m.z / Scale + CentreX, m.y / Scale, m.x / Scale);

        /// <summary>The direction (artifact frame) of the stern exit <paramref name="k"/> (0 top, then every third of a
        /// turn): from the hall's centre out through the stern.</summary>
        public static Vector3 ExitEnd(int k) => Radial(BRANCH_EX, BRANCH_ER, 2f * Mathf.PI * k / 3f);

        /// <summary>The hall's centre, artifact frame: the three exits run straight out of it.</summary>
        public static Vector3 HallCentre => new(JCX, 0f, 0f);

        static Vector3 Radial(float x, float r, float t) => new(x, r * Mathf.Cos(t), r * Mathf.Sin(t));

        // ── primitives (artifact units) ─────────────────────────────────────
        /// <summary>A ring about the x axis: x0..x1 long, radii r0..r1 (r0 = 0 a solid drum).</summary>
        static float Ring(Vector3 a, float x0, float x1, float r0, float r1)
        {
            float r = Mathf.Sqrt(a.y * a.y + a.z * a.z);
            float dx = Mathf.Max(x0 - a.x, a.x - x1);
            float dr = r0 > 0f ? Mathf.Max(r0 - r, r - r1) : r - r1;   // a solid drum has no inner wall on its axis
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dr, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dr), 0f);
        }

        static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude - r;
        }

        /// <summary>Distance from p to the segment a..b's axis (unclamped ends give a capped cylinder).</summary>
        static float Tube(Vector3 p, Vector3 a, Vector3 b, float r0, float r1)
        {
            var ab = b - a;
            float len = ab.magnitude;
            var u = ab / Mathf.Max(1e-6f, len);
            float s = Vector3.Dot(p - a, u);
            float rho = (p - (a + u * s)).magnitude;
            float ds = Mathf.Max(-s, s - len);
            float dr = r0 > 0f ? Mathf.Max(r0 - rho, rho - r1) : rho - r1;
            return new Vector2(Mathf.Max(ds, 0f), Mathf.Max(dr, 0f)).magnitude + Mathf.Min(Mathf.Max(ds, dr), 0f);
        }

        // ── the solid ───────────────────────────────────────────────────────
        /// <summary>The mouth: its back wall, the deck stack and the glass dome, and the two ring gates of prism slabs.</summary>
        static float Mouth(Vector3 a)
        {
            float d = Ring(a, GAP_END, GAP_END + 1.6f, 0f, WALK_R + 2f);
            foreach (var (x0, x1, r) in MouthStack) d = Mathf.Min(d, Ring(a, x0, x1, 0f, r + PRISM_SKIN));
            d = Mathf.Min(d, Capsule(a, new Vector3(62.6f, 0f, 0f), new Vector3(62.6f, 0f, 0f), 6.4f));      // the dome
            d = Mathf.Min(d, Ring(a, 37f - 2.7f, 37f + 2.7f, 18.6f, 24.4f));
            d = Mathf.Min(d, Ring(a, 63f - 2.7f, 63f + 2.7f, 18.6f, 24.4f));
            return d;
        }

        /// <summary>The gap: the omni crystal in its collar, and the three walkways.</summary>
        static float Gap(Vector3 a)
        {
            float d = (a - new Vector3(CX, 0f, 0f)).magnitude - 7.2f;
            d = Mathf.Min(d, Ring(a, CX - 0.8f, CX + 0.8f, 7.6f, 9.2f));
            for (int k = 0; k < 3; k++)
            {
                float t = Mathf.PI / 3f + 2f * Mathf.PI * k / 3f;
                d = Mathf.Min(d, Capsule(a, Radial(HULL_END, WALK_R, t), Radial(GAP_END, WALK_R, t), 1.8f));
            }
            return d;
        }

        /// <summary>The hull: the tube's wall from its lip to the hall, the hull's head, the five decks and the slots between
        /// them, the hall's skin.</summary>
        static float Hull(Vector3 a)
        {
            float d = Ring(a, JX0 - 0.5f, HULL_END + 8.6f, RT - TubeLining, RT + 1.2f);     // the tube's wall, its turning rings
            d = Mathf.Min(d, Ring(a, HULL_END + 7f, HULL_END + 9.2f, RT, RT + 1.9f));         // its lip
            d = Mathf.Min(d, Ring(a, -5.6f, 0.3f, RT, WALK_R + 2.4f));                       // the hull's head
            for (int i = 0; i < Decks.Length; i++)
            {
                var (x0, x1) = Decks[i];
                d = Mathf.Min(d, Ring(a, x0, x1, RT, DeckRadius(i)));
                // The slot fore of it, between it and the deck (or the hull's head) ahead, is closed to the lower of the
                // two: a crevice between two turning decks is no passage, and a vessel let into it sits inside the model.
                float foreX0 = i == 0 ? -5.6f : Decks[i - 1].x0;
                float foreR = i == 0 ? WALK_R + 2.4f : DeckRadius(i - 1);
                d = Mathf.Min(d, Ring(a, x1, foreX0, RT, Mathf.Min(DeckRadius(i), foreR)));
            }
            d = Mathf.Min(d, Ring(a, JX1, JX0, JR - 0.6f, JR + 0.6f));                        // the hall's skin
            return d;
        }

        /// <summary>A deck's outer radius, its prisms' skin included.</summary>
        static float DeckRadius(int i) => 15f - 0.03f * (Decks[i].x0 + Decks[i].x1) + PRISM_SKIN;

        /// <summary>The stern: its cap, the booster's skirt and the eight bells, and the three exits' walls.</summary>
        static float Stern(Vector3 a)
        {
            float d = Ring(a, JX1 - 0.6f, JX1 + 0.6f, 0f, 33.2f);                              // the stern cap
            d = Mathf.Min(d, Ring(a, XB, JX1, 0f, 21f));                                       // the booster
            d = Mathf.Min(d, Ring(a, JX1 - 5f, JX1, 0f, 30.6f));                               // its skirt
            d = Mathf.Min(d, Ring(a, XB - 12f, XB, 0f, 6.6f));                                 // the big bell
            for (int k = 0; k < 7; k++)
            {
                float t = (k + 0.5f) * 2f * Mathf.PI / 7f;
                var c = Radial(XB, BELL_RING, t);
                d = Mathf.Min(d, Tube(a, c, c - new Vector3(9f, 0f, 0f), 0f, 4f));
            }
            for (int k = 0; k < 3; k++)
                d = Mathf.Min(d, Tube(a, HallCentre, ExitEnd(k), RB - 0.6f, RB + 1.2f));
            return d;
        }

        /// <summary>The three pods on their cables, where they have turned to.</summary>
        static float Pods(Vector3 m, Quaternion turned)
        {
            var back = Quaternion.Inverse(turned) * m;    // the pods' own frame (they turn about the nose axis)
            var a = ToArtifact(back);
            float d = float.PositiveInfinity;
            for (int k = 0; k < 3; k++)
            {
                float t = Mathf.PI / 3f + 2f * Mathf.PI * k / 3f;
                d = Mathf.Min(d, Capsule(a, Radial(POD_X0, POD_R0, t), Radial(POD_X1, POD_R1, t), POD_PR + PRISM_SKIN));
            }
            return d;
        }

        // ── the passage ─────────────────────────────────────────────────────
        static float Passage(Vector3 a)
        {
            float d = Ring(a, JX0 - 1f, HULL_END + 12f, 0f, RT - TubeLining - 0.2f);          // the tube, inside its rings
            d = Mathf.Min(d, Ring(a, JX1 + 0.3f, JX0, 0f, JR - 0.9f));                        // the hall
            for (int k = 0; k < 3; k++)
            {
                var end = ExitEnd(k);
                var dir = (end - HallCentre).normalized;
                d = Mathf.Min(d, Tube(a, HallCentre, end + dir * 4f, 0f, RB - 0.9f));
            }
            return d;
        }
    }
}
