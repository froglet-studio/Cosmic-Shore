// The presence orb and the party molecule's bonds (Docs/MENU_REDESIGN_B.md §45.4): you at the centre, your party
// bonded to you, online friends orbiting. Pure maths on a point's place and time - no textures - so the orb's rings,
// its orbiting dots and every bond animate in the shader and never dirty a canvas. Tools/Build/lattice_preview.py
// compiles and checks it with clang.
#ifndef COSMIC_UI_PRESENCE_INCLUDED
#define COSMIC_UI_PRESENCE_INCLUDED

static const float PRESENCE_TAU = 6.2831853;

// A point's angle round the centre, in turns (0..1).
float PresenceTurns(float2 p)
{
    return frac(atan2(p.y, p.x) * 0.15915494 + 1.0);
}

// Coverage of a ring of radius r and half-width w, softened over `soft`, at distance d from the centre.
float PresenceRing(float d, float r, float w, float soft)
{
    return 1.0 - smoothstep(w, w + soft, abs(d - r));
}

// The dashed orbit: `dashes` dashes round a ring of radius r, the whole ring turning once every spinSeconds (0: still).
float PresenceOrbit(float2 p, float t, float r, float w, float soft, float dashes, float spinSeconds)
{
    float a = frac(PresenceTurns(p) - (spinSeconds > 0.0 ? t / spinSeconds : 0.0));
    float dash = step(frac(a * dashes), 0.5);
    return PresenceRing(length(p), r, w, soft) * dash;
}

// `count` dots spaced evenly round the orbit (a count or a size of 0 draws none), turning with it; the first sits at
// `start` turns.
float PresenceDots(float2 p, float t, float r, float size, float soft, float count, float spinSeconds, float start)
{
    float n = floor(count + 0.5);
    if (n < 1.0 || size <= 0.0) return 0.0;
    float spin = (spinSeconds > 0.0 ? t / spinSeconds : 0.0) + start;
    float k = floor(frac(PresenceTurns(p) - spin) * n + 0.5);
    float ang = (k / n + spin) * PRESENCE_TAU;
    float2 c = float2(cos(ang), sin(ang)) * r;
    return 1.0 - smoothstep(size, size + soft, length(p - c));
}

// Distance from p to the segment a -> b (x), and how far along it p lies, 0 at a and 1 at b (y).
float2 PresenceSegment(float2 p, float2 a, float2 b)
{
    float2 ab = b - a;
    float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 0.000001));
    return float2(length(p - a - ab * h), h);
}

// A bond's light at `across` from its centre line and `along` it (outward from the centre, in the same units as
// `period`): a soft core with bright dashes flowing outward at `speed`, and a glow round it. Returns coverage.
float PresenceBond(float across, float along, float t, float width, float soft, float period, float speed)
{
    // period 0: a solid, still bond (no dashes to flow).
    float f = frac((along - t * speed) / max(period, 0.0001));
    float dash = period > 0.0 ? smoothstep(0.0, 0.2, f) * (1.0 - smoothstep(0.5, 0.7, f)) : 1.0;
    float core = 1.0 - smoothstep(width, width + soft, across);
    float glow = (1.0 - smoothstep(0.0, width * 4.0 + soft, across)) * 0.3;
    return core * (0.4 + 0.6 * dash) + glow * dash;
}

// The bond from the centre to the member at `member`, in the member's colour (its alpha 0: an empty seat, no bond).
// Returns premultiplied colour (rgb) and coverage (a).
float4 PresenceBondTo(float2 p, float t, float2 member, float4 colour, float width, float soft, float period, float speed)
{
    float2 s = PresenceSegment(p, float2(0.0, 0.0), member);
    float b = PresenceBond(s.x, s.y * length(member), t, width, soft, period, speed) * colour.a;
    return float4(colour.rgb * b, b);
}

#endif
